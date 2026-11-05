using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Equilibrium.Simulation.World
{
    /// <summary>
    /// First runtime drivetrain: compatible adjacent rotation ports, straight shafts and fuel engines.
    /// No gears or inferred junctions. Ratios are 1; all efficiencies and source ratings come from content.
    /// </summary>
    internal static class StraightMechanicalSolver
    {
        public static void Solve(ProcessingSystem system, WorldView world, MechanicalResult[] results)
        {
            int n = world.BlockIndexCount; var links = new List<int>[n];
            for (int i = 0; i < n; i++) if (world.IsAlive(new BlockRef(i))) links[i] = new List<int>();
            for (int i = 0; i < n; i++)
            {
                var b = new BlockRef(i); if (links[i] == null) continue;
                for (int p = 0; p < world.PortCount(b); p++)
                    if (world.Port(b, p).Kind == PortKind.RotationOut && world.Connected(b, p, out var target, out _))
                    { if (!links[i].Contains(target.Index)) links[i].Add(target.Index); if (!links[target.Index].Contains(i)) links[target.Index].Add(i); }
            }
            var seen = new bool[n]; var queue = new Queue<int>();
            for (int first = 0; first < n; first++)
            {
                if (links[first] == null || seen[first]) continue;
                var component = new List<int>(); queue.Enqueue(first); seen[first] = true;
                while (queue.Count > 0)
                {
                    int at = queue.Dequeue(); component.Add(at);
                    foreach (int next in links[at]) if (!seen[next]) { seen[next] = true; queue.Enqueue(next); }
                }
                SolveComponent(component);
            }

            void SolveComponent(List<int> component)
            {
                var engines = component.Where(i => system.IsEngine(new BlockRef(i))).ToArray();
                var active = new List<int>();
                foreach (int e in engines)
                {
                    int slot = world.SlotGlobal(new BlockRef(e), 0), item = world.SlotItemHandle(slot);
                    if (item >= 0 && system.Items[slot] == item && Math.Min(system.Counts[slot], world.SlotCountOf(slot)) > system.Bags[e].FuelDebt &&
                        world.Registry.Item(world.Registry.Items.FromHandle(item)).EnergyJ > 0) active.Add(e);
                }
                ReasonCode failure = engines.Length == 0 ? ReasonCode.NoPowerSource : active.Count == 0 ? ReasonCode.NoFuel : ReasonCode.None;
                double omega = active.Count == 0 ? 0 : Number(active[0], "omega_src");
                foreach (int e in active) if (Math.Abs(Number(e, "omega_src") - omega) > 1e-9) failure = ReasonCode.SourceConflict;
                if (omega <= 0 && failure == ReasonCode.None) failure = ReasonCode.NoPowerSource;
                double capacity = active.Sum(e => Number(e, "tau_max")), demand = 0;
                var efficiencies = new Dictionary<int, double>();
                if (active.Count > 0)
                {
                    // A straight component has a unique path. Refuse multi-source transmission rather
                    // than inventing source sharing through lossy paths; matching co-located source graphs
                    // are reserved for the full segmented solver.
                    if (active.Count > 1) failure = ReasonCode.SourceConflict;
                    var pending = new Queue<int>(); pending.Enqueue(active[0]); efficiencies[active[0]] = 1;
                    while (pending.Count > 0)
                    {
                        int at = pending.Dequeue();
                        foreach (int next in links[at]) if (!efficiencies.ContainsKey(next))
                        {
                            double eta = system.IsShaft(new BlockRef(next)) ? Number(next, "eta_shaft") : 1;
                            efficiencies[next] = efficiencies[at] * eta; pending.Enqueue(next);
                        }
                    }
                    foreach (int i in component)
                        if (system.TryMachine(new BlockRef(i), out var machine) && system.State(new BlockRef(i))?.Phase == MachinePhase.Working)
                            demand += machine.TauReq / efficiencies[i];
                    if (demand > capacity && failure == ReasonCode.None) failure = ReasonCode.Stalled;
                }
                double fraction = 1;
                if (failure == ReasonCode.None && demand > 0)
                {
                    int e = active[0], slot = world.SlotGlobal(new BlockRef(e), 0);
                    var fuel = world.Registry.Item(world.Registry.Items.FromHandle(system.Items[slot]));
                    double cost = demand * omega / (fuel.EnergyJ * Number(e, "eta_engine"));
                    double available = Math.Min(system.Counts[slot], world.SlotCountOf(slot)) - system.Bags[e].FuelDebt;
                    // Exhausting fuel partway through a one-second tick can never create free energy.
                    fraction = Math.Min(1, available / cost);
                    double debt = system.Bags[e].FuelDebt + cost * fraction;
                    int consume = (int)Math.Floor(debt + 1e-9);
                    system.Counts[slot] -= consume; if (system.Counts[slot] == 0) system.Items[slot] = -1;
                    system.Bags[e].FuelDebt = Math.Max(0, debt - consume);
                }
                foreach (int i in component)
                {
                    double eta = efficiencies.TryGetValue(i, out double value) ? value : 0;
                    double watts = 0;
                    if (failure == ReasonCode.None && system.TryMachine(new BlockRef(i), out var machine) && system.State(new BlockRef(i))?.Phase == MachinePhase.Working)
                        // Path loss is already included in source demand. Applying eta here again
                        // would charge the same transmission loss twice (creator decision 2026-11-05).
                        watts = machine.TauReq * omega * fraction;
                    if (system.IsEngine(new BlockRef(i)) && failure == ReasonCode.None) watts = demand * omega * fraction;
                    results[i] = new MechanicalResult(watts, failure == ReasonCode.None ? omega : 0, demand, capacity, eta, failure);
                }
            }

            double Number(int block, string key)
            {
                var item = system.Item(new BlockRef(block));
                if (!item.Extras.TryGetValue(key, out string raw) || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                    double.IsNaN(value) || double.IsInfinity(value) || value < 0 || (key.StartsWith("eta_", StringComparison.Ordinal) && (value <= 0 || value > 1)))
                    throw new InvalidOperationException("Invalid mechanical content: " + item.Id.Canonical + "/" + key);
                return value;
            }
        }
    }
}

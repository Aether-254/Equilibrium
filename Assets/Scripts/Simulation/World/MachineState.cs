using System;
using System.Collections.Generic;
using Equilibrium.Content;

namespace Equilibrium.Simulation.World
{
    public enum MachinePhase : byte { Idle, Working, Blocked, Starved }

    public readonly struct MachineReservation
    {
        public readonly int LocalSlot, Item, Count;
        public MachineReservation(int slot, int item, int count) { LocalSlot = slot; Item = item; Count = count; }
    }

    public readonly struct MachineProduct
    {
        public readonly ItemId Item;
        public readonly int Count;
        public readonly bool Byproduct;
        public MachineProduct(ItemId item, int count, bool byproduct) { Item = item; Count = count; Byproduct = byproduct; }
    }

    /// <summary>Persistent machine state. Slot references are local, so removal/replacement can relocate it.</summary>
    public sealed class MachineState
    {
        public MachinePhase Phase { get; internal set; }
        public ReasonCode Reason { get; internal set; }
        public string RecipeId { get; internal set; }
        public string PinnedRecipe { get; internal set; }
        public double WorkDone { get; internal set; }
        public double ReceivedWatts { get; internal set; }
        public long CompletedBatches { get; internal set; }
        internal List<MachineReservation> Held = new List<MachineReservation>();
        internal List<MachineProduct> Pending = new List<MachineProduct>();
        internal bool[] PoweredHistory = new bool[60];
        internal int HistoryIndex, Observed, Powered;
        public IReadOnlyList<MachineReservation> Reservations => Held.AsReadOnly();
        public IReadOnlyList<MachineProduct> PendingProducts => Pending.AsReadOnly();
        public double Utilization => Observed == 0 ? 0 : (double)Powered / Observed;
        internal void Observe(bool powered)
        {
            if (PoweredHistory[HistoryIndex]) Powered--;
            PoweredHistory[HistoryIndex] = powered; if (powered) Powered++;
            HistoryIndex = (HistoryIndex + 1) % PoweredHistory.Length; Observed = Math.Min(60, Observed + 1);
        }
        internal MachineState Clone()
        {
            var copy = (MachineState)MemberwiseClone();
            copy.Held = new List<MachineReservation>(Held); copy.Pending = new List<MachineProduct>(Pending);
            copy.PoweredHistory = (bool[])PoweredHistory.Clone(); return copy;
        }
    }

    public readonly struct MechanicalResult
    {
        public readonly double Watts, Omega, SourceDemand, SourceCapacity, Efficiency;
        public readonly ReasonCode Reason;
        public MechanicalResult(double watts, double omega, double demand, double capacity, double efficiency, ReasonCode reason)
        { Watts = watts; Omega = omega; SourceDemand = demand; SourceCapacity = capacity; Efficiency = efficiency; Reason = reason; }
    }
}

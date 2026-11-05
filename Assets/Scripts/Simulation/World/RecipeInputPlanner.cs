using System;
using System.Collections.Generic;
using System.Linq;
using Equilibrium.Content;

namespace Equilibrium.Simulation.World
{
    /// <summary>
    /// Finds concrete slot reservations. A capacity flow handles overlapping tags without greedy
    /// double-spending; non-mixing tag ingredients select one concrete ItemId for the whole ingredient.
    /// </summary>
    internal static class RecipeInputPlanner
    {
        public static List<MachineReservation> Find(WorldView world, BlockRef block, RecipeDef recipe,
            int[] items, int[] counts, int[] reserved)
        {
            var slots = new List<int>(); int start = world.SlotGlobal(block, 0);
            for (int i = 0; i < world.SlotCount(block); i++)
                if (world.ContainerKeyOf(start + i) == "input" && counts[start + i] > reserved[start + i]) slots.Add(start + i);
            var chosen = new int[recipe.Inputs.Count]; Array.Fill(chosen, -1);
            return Choose(0);

            List<MachineReservation> Choose(int index)
            {
                if (index == recipe.Inputs.Count) return Flow();
                var input = recipe.Inputs[index];
                if (!input.IsTag || recipe.AllowMixedTagInputs) return Choose(index + 1);
                foreach (int item in slots.Select(s => items[s]).Distinct().OrderBy(h => world.Registry.Items.FromHandle(h).Canonical, StringComparer.Ordinal))
                {
                    if (!Matches(world.Registry, input, item)) continue;
                    chosen[index] = item; var result = Choose(index + 1); if (result != null) return result;
                }
                chosen[index] = -1; return null;
            }

            List<MachineReservation> Flow()
            {
                int ingredientStart = 1 + slots.Count, sink = ingredientStart + recipe.Inputs.Count, n = sink + 1;
                var remaining = new int[n, n];
                for (int s = 0; s < slots.Count; s++)
                {
                    int g = slots[s]; remaining[0, s + 1] = counts[g] - reserved[g];
                    for (int i = 0; i < recipe.Inputs.Count; i++)
                        if ((chosen[i] < 0 || chosen[i] == items[g]) && Matches(world.Registry, recipe.Inputs[i], items[g]))
                            remaining[s + 1, ingredientStart + i] = counts[g] - reserved[g];
                }
                long needed = 0, moved = 0;
                for (int i = 0; i < recipe.Inputs.Count; i++) { remaining[ingredientStart + i, sink] = recipe.Inputs[i].Count; needed += recipe.Inputs[i].Count; }
                var parent = new int[n]; var queue = new Queue<int>();
                while (moved < needed)
                {
                    Array.Fill(parent, -1); parent[0] = 0; queue.Clear(); queue.Enqueue(0);
                    while (queue.Count > 0 && parent[sink] < 0)
                    {
                        int a = queue.Dequeue();
                        for (int b = 1; b < n; b++) if (parent[b] < 0 && remaining[a, b] > 0) { parent[b] = a; queue.Enqueue(b); }
                    }
                    if (parent[sink] < 0) return null;
                    int amount = int.MaxValue;
                    for (int b = sink; b != 0; b = parent[b]) amount = Math.Min(amount, remaining[parent[b], b]);
                    for (int b = sink; b != 0; b = parent[b]) { remaining[parent[b], b] -= amount; remaining[b, parent[b]] += amount; }
                    moved += amount;
                }
                var result = new List<MachineReservation>();
                for (int s = 0; s < slots.Count; s++)
                    if (remaining[s + 1, 0] > 0) result.Add(new MachineReservation(slots[s] - start, items[slots[s]], remaining[s + 1, 0]));
                return result;
            }
        }

        internal static bool Matches(ContentRegistry registry, RecipeIo input, int handle)
            => handle >= 0 && (input.IsTag ? registry.InTag(registry.Items.FromHandle(handle), input.Tag) : input.Item.Handle == handle);
    }
}

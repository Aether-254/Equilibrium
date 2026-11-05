using System;
using System.Collections.Generic;
using Equilibrium.Content;

namespace Equilibrium.Simulation.World
{
    internal readonly struct SlotRange
    {
        public readonly int Start, End;
        public SlotRange(int start, int end) { Start = start; End = end; }
    }

    /// <summary>
    /// Reusable private slot workspace. Available tracks only unreserved stock present at Snapshot:
    /// a newly delivered item cannot be extracted again in the same tick, even through a chest.
    /// </summary>
    internal sealed class LogisticsSlots
    {
        public int[] Items = Array.Empty<int>(), Counts = Array.Empty<int>();
        public int Length { get; private set; }
        private int[] _available = Array.Empty<int>();
        private int[] _backupItem = Array.Empty<int>(), _backupCount = Array.Empty<int>(), _backupAvailable = Array.Empty<int>();
        private readonly List<int> _tidyItems = new List<int>();
        private WorldView _view;

        public void Capture(WorldView view)
        {
            _view = view; Length = view.S.SlotItem.Count;
            if (Items.Length < Length)
            {
                int n = Math.Max(Length, Items.Length * 2);
                Array.Resize(ref Items, n); Array.Resize(ref Counts, n); Array.Resize(ref _available, n);
            }
            for (int i = 0; i < Length; i++)
            {
                Items[i] = view.SlotItemHandle(i); Counts[i] = view.SlotCountOf(i);
                _available[i] = Math.Max(0, Counts[i] - view.SlotReservedOf(i));
            }
        }

        public int Available(SlotRange range, ItemId item)
        {
            long sum = 0;
            for (int g = range.Start; g < range.End; g++) if (Items[g] == item.Handle) sum += _available[g];
            return (int)Math.Min(int.MaxValue, sum);
        }

        public int OriginalStock(SlotRange range, ItemId item)
        {
            long sum = 0;
            for (int g = range.Start; g < range.End; g++)
                if (Items[g] == item.Handle) sum += (long)_available[g] + _view.SlotReservedOf(g);
            return (int)Math.Min(int.MaxValue, sum);
        }

        public ItemId FirstAvailable(SlotRange range)
        {
            for (int g = range.Start; g < range.End; g++)
                if (Items[g] >= 0 && _available[g] > 0) return _view.Registry.Items.FromHandle(Items[g]);
            return ItemId.None;
        }

        public bool Allows(SlotRange range, ItemId item)
        {
            for (int g = range.Start; g < range.End; g++) if (_view.Filtered(g, item)) return true;
            return false;
        }

        public void Take(SlotRange range, ItemId item, int count)
        {
            for (int g = range.End - 1; g >= range.Start && count > 0; g--)
            {
                if (Items[g] != item.Handle) continue;
                int take = Math.Min(count, _available[g]);
                Counts[g] -= take; _available[g] -= take; count -= take;
                if (Counts[g] == 0) Items[g] = -1;
            }
            if (count != 0) throw new InvalidOperationException("Resolved source budget was overdrawn.");
        }

        public void PutGroup(int slot, ItemId item, int count)
        {
            if (Counts[slot] != 0) throw new InvalidOperationException("Belt groups must never merge.");
            Items[slot] = item.Handle; Counts[slot] = count; _available[slot] = 0;
        }

        public int Insert(SlotRange range, ItemId item, int requested, bool exact)
        {
            if (requested <= 0) return 0;
            int n = range.End - range.Start;
            if (_backupItem.Length < n)
            {
                Array.Resize(ref _backupItem, n); Array.Resize(ref _backupCount, n); Array.Resize(ref _backupAvailable, n);
            }
            Array.Copy(Items, range.Start, _backupItem, 0, n);
            Array.Copy(Counts, range.Start, _backupCount, 0, n);
            Array.Copy(_available, range.Start, _backupAvailable, 0, n);
            int inserted = Fill(range, item, requested);
            if (inserted < requested && Tidy(range)) inserted += Fill(range, item, requested - inserted);
            if (inserted == 0 || (exact && inserted != requested))
            {
                Array.Copy(_backupItem, 0, Items, range.Start, n);
                Array.Copy(_backupCount, 0, Counts, range.Start, n);
                Array.Copy(_backupAvailable, 0, _available, range.Start, n);
                return 0;
            }
            return inserted;
        }

        private int Fill(SlotRange range, ItemId item, int remaining)
        {
            int placed = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int g = range.Start; g < range.End && placed < remaining; g++)
                {
                    if (Items[g] != (pass == 0 ? item.Handle : -1) || !_view.Filtered(g, item)) continue;
                    int amount = Math.Min(remaining - placed, Math.Max(0, _view.EffectiveCapacity(g, item) - Counts[g]));
                    if (amount == 0) continue;
                    Items[g] = item.Handle; Counts[g] += amount; placed += amount;
                }
            return placed;
        }

        private bool Tidy(SlotRange range)
        {
            _tidyItems.Clear();
            for (int g = range.Start; g < range.End; g++)
                if (Items[g] >= 0 && !_tidyItems.Contains(Items[g])) _tidyItems.Add(Items[g]);
            _tidyItems.Sort((a, b) => string.CompareOrdinal(
                _view.Registry.Items.FromHandle(a).Canonical, _view.Registry.Items.FromHandle(b).Canonical));
            bool changed = false;
            foreach (int handle in _tidyItems)
            {
                var item = _view.Registry.Items.FromHandle(handle);
                for (int dst = range.Start; dst < range.End; dst++)
                {
                    if (Items[dst] != handle || !_view.Filtered(dst, item)) continue;
                    int cap = _view.EffectiveCapacity(dst, item);
                    for (int src = dst + 1; src < range.End && Counts[dst] < cap; src++)
                    {
                        if (Items[src] != handle || _view.SlotReservedOf(src) > 0) continue;
                        int move = Math.Min(cap - Counts[dst], Counts[src]);
                        if (move <= 0) continue;
                        int original = Math.Min(move, _available[src]);
                        _available[src] -= original; _available[dst] += original;
                        Counts[src] -= move; Counts[dst] += move; changed = true;
                        if (Counts[src] == 0) Items[src] = -1;
                    }
                }
            }
            return changed;
        }
    }
}

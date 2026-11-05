using System;
using System.Collections.Generic;
using Equilibrium.Content;

namespace Equilibrium.Simulation.World
{
    /// <summary>
    /// Container transaction math (container GDD Deterministic Insertion). Pure functions over the
    /// world view so systems can compute <c>acceptable</c>/<c>available</c> in Collect and the writer
    /// can execute the identical plan in Commit.
    /// </summary>
    public static class ContainerOps
    {
        /// <summary>
        /// Acceptable count for an item across the slot range after at most one hypothetical tidy.
        /// GDD formula: sum over eligible slots of (effectiveCapacity - count).
        /// </summary>
        public static int Acceptable(WorldView v, int slotStart, int slotEnd, ItemId item)
        {
            int total = 0;
            for (int g = slotStart; g < slotEnd; g++)
            {
                if (!v.Filtered(g, item)) continue;
                int held = v.SlotItemHandle(g);
                if (held >= 0 && held != item.Handle) continue;
                total += v.EffectiveCapacity(g, item) - v.SlotCountOf(g);
            }
            // A tidy can only free whole empty slots by merging same-item partials; the total free
            // effective capacity is invariant under tidy, so no second pass is needed for the sum.
            return Math.Max(0, total);
        }

        /// <summary>Available (unreserved) count of item across the range (container GDD, reserved excluded).</summary>
        public static int Available(WorldView v, int slotStart, int slotEnd, ItemId item)
        {
            int total = 0;
            for (int g = slotStart; g < slotEnd; g++)
                if (v.SlotItemHandle(g) == item.Handle) total += v.SlotCountOf(g) - v.SlotReservedOf(g);
            return Math.Max(0, total);
        }

        /// <summary>Whether any slot in the range currently holds the item.</summary>
        public static bool Holds(WorldView v, int slotStart, int slotEnd, ItemId item)
        {
            for (int g = slotStart; g < slotEnd; g++) if (v.SlotItemHandle(g) == item.Handle && v.SlotCountOf(g) > 0) return true;
            return false;
        }
    }

    /// <summary>
    /// The only mutator (ADR-0001/0004). Structural operations mark dirty chunks. Slot operations
    /// implement the container GDD insertion algorithm exactly and are all-or-nothing per call.
    /// </summary>
    public sealed partial class WorldWriter : IWorldWriter
    {
        private readonly WorldState _s;
        private readonly BlockDefTable _defs;
        private readonly WorldView _view;
        private readonly List<int> _freeBlocks = new List<int>();
        private DropRecord[] _committedDrops = new DropRecord[8];
        private int _committedDropCount;

        public WorldWriter(WorldState state, BlockDefTable defs)
        {
            _s = state; _defs = defs; _view = new WorldView(state);
        }

        public WorldView View => _view;
        public ReadOnlySpan<DropRecord> CommittedDrops => new ReadOnlySpan<DropRecord>(_committedDrops, 0, _committedDropCount);

        /// <summary>
        /// Captures and clears drops produced by committed structural removals. The caller routes
        /// the returned immutable batch to warehouse/drop delivery at the tick boundary.
        /// </summary>
        public DropRecord[] CaptureCommittedDrops()
        {
            if (_committedDropCount == 0) return Array.Empty<DropRecord>();
            var captured = new DropRecord[_committedDropCount];
            Array.Copy(_committedDrops, captured, _committedDropCount);
            Array.Clear(_committedDrops, 0, _committedDropCount);
            _committedDropCount = 0;
            return captured;
        }

        /// <summary>
        /// TickEngine commit entry point. Structural commands have already been validated against
        /// the tick snapshot; any mismatch here is a commit fault and leaves no partial mutation.
        /// </summary>
        public void Apply(in Transaction accepted)
        {
            if (accepted.Payload.Kind == PayloadKind.ProcessingBatch)
            {
                ApplyProcessing(in accepted);
                return;
            }
            if (accepted.Payload.Kind == PayloadKind.LogisticsBatch)
            {
                ApplyLogistics(in accepted);
                return;
            }
            if (accepted.Payload.Kind != PayloadKind.Structural)
                throw new InvalidOperationException($"WorldWriter cannot apply {accepted.Payload.Kind}.");

            ReasonCode reason;
            switch ((StructuralOperation)accepted.Payload.A)
            {
                case StructuralOperation.Place:
                    CellRef cell = WorldTransactions.DecodeCell(accepted.Target, _s.Width, _s.Height);
                    reason = Place(
                        new BlockDefHandle(accepted.Payload.B),
                        cell,
                        accepted.Payload.C,
                        null,
                        out _);
                    break;
                case StructuralOperation.Remove:
                    EnsureBlockTarget(accepted.Target);
                    reason = Remove(new BlockRef(accepted.Target.Index), out DropRecord drop);
                    if (reason == ReasonCode.None) AddCommittedDrop(drop);
                    break;
                case StructuralOperation.Rotate:
                    EnsureBlockTarget(accepted.Target);
                    reason = Rotate(new BlockRef(accepted.Target.Index), accepted.Payload.B);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown structural operation {accepted.Payload.A}.");
            }

            if (reason != ReasonCode.None)
                throw new InvalidOperationException($"Structural command rejected during commit: {reason}.");
        }

        private static void EnsureBlockTarget(EntityRef target)
        {
            if (target.Kind != WorldEntityKinds.Block || target.Index < 0)
                throw new InvalidOperationException($"Expected block target, got {target}.");
        }

        private void AddCommittedDrop(DropRecord drop)
        {
            if (_committedDropCount == _committedDrops.Length)
                Array.Resize(ref _committedDrops, _committedDrops.Length * 2);
            _committedDrops[_committedDropCount++] = drop;
        }

        /// <summary>
        /// Captures immutable cell/ingress views only for chunks changed since the previous capture.
        /// A presentation or save consumer keeps unchanged chunk views in its own cache and applies
        /// this delta at a committed tick boundary.
        /// </summary>
        public WorldSnapshotDelta CaptureDirtyChunkViews()
        {
            if (_s.DirtyChunkIndices.Count == 0) return _s.EmptySnapshotDelta;
            var changed = new WorldChunkView[_s.DirtyChunkIndices.Count];
            for (int i = 0; i < changed.Length; i++)
                changed[i] = WorldChunkView.Capture(_s, _s.DirtyChunkIndices[i]);
            _s.ClearDirty();
            ulong revision = ++_s.SnapshotRevision;
            _s.EmptySnapshotDelta = new WorldSnapshotDelta(revision, Array.Empty<WorldChunkView>());
            return new WorldSnapshotDelta(revision, changed);
        }

        // ---------------- Structural ----------------

        public ReasonCode Place(BlockDefHandle def, CellRef cell, int orientation, PersistentBag restore, out BlockRef placed)
        {
            Touch();
            placed = BlockRef.None;
            if (!_s.InBounds(cell)) return ReasonCode.OutOfBounds;
            int ci = _s.CellIndex(cell);
            if (_s.CellToBlock[ci] >= 0) return ReasonCode.CellOccupied;
            var d = _defs.Get(def);

            int b = AllocBlock();
            _s.BlockDef[b] = def;
            _s.Orientation[b] = (byte)(orientation & 3);
            _s.AnchorCell[b] = cell;
            _s.ControllerCoord[b] = CellRef.None;
            _s.Alive[b] = true;
            _s.Accessors[b] = new AccessorConfiguration[4];
            _s.LiveBlockCount++;

            // slots
            _s.SlotStart[b] = _s.SlotItem.Count;
            int slotCount = 0;
            for (int c = 0; c < d.Containers.Count; c++)
            {
                var cs = d.Containers[c];
                for (int si = 0; si < cs.Slots.Count; si++)
                {
                    var sp = cs.Slots[si];
                    _s.SlotItem.Add(-1); _s.SlotCountArr.Add(0); _s.SlotCapacity.Add(sp.Capacity); _s.SlotFilter.Add(sp.Filter);
                    _s.SlotReserved.Add(0); _s.SlotContainerLocal.Add(c); _s.SlotKey.Add(sp.Key); _s.ContainerKeyOfSlot.Add(cs.Key);
                    slotCount++;
                }
            }
            _s.SlotCount[b] = slotCount;

            // ports with binding verification (container GDD edge case PortBindingMissing)
            _s.PortStart[b] = _s.Ports.Count;
            foreach (var p in d.Ports)
            {
                bool ok = p.ContainerKey == null || _view.ResolveBinding(new BlockRef(b), in p, out _, out _);
                _s.Ports.Add(ok ? p : p.WithBindingMissing());
            }
            _s.PortCount[b] = d.Ports.Count;

            _s.Bags[b] = new PersistentBag();
            _s.CellToBlock[ci] = b;
            _s.MarkDirty(cell);
            placed = new BlockRef(b);

            if (restore != null) RestoreBag(placed, restore);
            return ReasonCode.None;
        }

        /// <summary>Removes a block; returns its drop record (persistent bag only, GDD rule 6).</summary>
        public ReasonCode Remove(BlockRef b, out DropRecord drop)
        {
            Touch();
            drop = default;
            if (!_view.IsAlive(b)) return ReasonCode.TargetInvalid;
            var d = _defs.Get(_s.BlockDef[b.Index]);
            var bag = CaptureBag(b);
            CellRef cell = _s.AnchorCell[b.Index];
            _s.CellToBlock[_s.CellIndex(cell)] = -1;
            // Ingress blockers belong to the removed block, not to the empty grid coordinate.
            _s.Ingress[_s.CellIndex(cell)] = 0xF;
            _s.Alive[b.Index] = false;
            _s.Accessors[b.Index] = null;
            _s.LiveBlockCount--;
            _s.MarkDirty(cell);
            var item = d.DropItemHandle >= 0 ? _s.Registry.Items.FromHandle(d.DropItemHandle) : ItemId.None;
            drop = new DropRecord(item, bag);
            return ReasonCode.None;
        }

        public ReasonCode Rotate(BlockRef b, int orientation)
        {
            Touch();
            if (!_view.IsAlive(b)) return ReasonCode.TargetInvalid;
            _s.Orientation[b.Index] = (byte)(orientation & 3);
            _s.MarkDirty(_s.AnchorCell[b.Index]);
            return ReasonCode.None;
        }

        public void SetIngress(CellRef cell, Side side, bool enabled)
        {
            Touch();
            if (!_s.InBounds(cell)) return;
            int ci = _s.CellIndex(cell);
            byte mask = (byte)(1 << (int)side);
            if (enabled) _s.Ingress[ci] |= mask; else _s.Ingress[ci] &= (byte)~mask;
            _s.MarkDirty(cell);
        }

        public void SetController(BlockRef b, CellRef controller)
        {
            Touch();
            _s.ControllerCoord[b.Index] = controller;
            _s.MarkDirty(_s.AnchorCell[b.Index]);
        }

        // ---------------- Slots ----------------

        /// <summary>
        /// Inserts up to <paramref name="count"/> of item into the slot range using the deterministic
        /// algorithm; returns the number placed. Caller decides atomicity (all-or-nothing when the
        /// returned plan is smaller than requested) by checking <see cref="ContainerOps.Acceptable"/> first.
        /// </summary>
        public int Insert(int slotStart, int slotEnd, ItemId item, int count)
        {
            Touch();
            if (count <= 0) return 0;
            int placed = FillPass(slotStart, slotEnd, item, count);
            if (placed < count)
            {
                if (Tidy(slotStart, slotEnd)) placed += FillPass(slotStart, slotEnd, item, count - placed);
            }
            return placed;
        }

        private int FillPass(int slotStart, int slotEnd, ItemId item, int count)
        {
            int placed = 0;
            // 1. existing same-item slots, ascending index
            for (int g = slotStart; g < slotEnd && placed < count; g++)
            {
                if (_s.SlotItem[g] != item.Handle || !_view.Filtered(g, item)) continue;
                int room = _view.EffectiveCapacity(g, item) - _s.SlotCountArr[g];
                if (room <= 0) continue;
                int take = Math.Min(room, count - placed);
                _s.SlotCountArr[g] += take; placed += take;
            }
            // 2. empty slots, ascending index
            for (int g = slotStart; g < slotEnd && placed < count; g++)
            {
                if (_s.SlotItem[g] >= 0 || !_view.Filtered(g, item)) continue;
                int take = Math.Min(_view.EffectiveCapacity(g, item), count - placed);
                if (take <= 0) continue;
                _s.SlotItem[g] = item.Handle; _s.SlotCountArr[g] = take; placed += take;
            }
            return placed;
        }

        /// <summary>
        /// Auto-tidy (container GDD): merge same-item slots toward the lowest index respecting filters
        /// and effective capacity. Ordering fixed by SlotIndex then ItemId canonical string. Returns
        /// true if any slot changed.
        /// </summary>
        public bool Tidy(int slotStart, int slotEnd)
        {
            Touch();
            bool changed = false;
            // Collect distinct items in canonical order for determinism.
            var items = new List<int>();
            for (int g = slotStart; g < slotEnd; g++) if (_s.SlotItem[g] >= 0 && !items.Contains(_s.SlotItem[g])) items.Add(_s.SlotItem[g]);
            items.Sort((a, b) => string.CompareOrdinal(_s.Registry.Items.FromHandle(a).Canonical, _s.Registry.Items.FromHandle(b).Canonical));

            foreach (int h in items)
            {
                var item = _s.Registry.Items.FromHandle(h);
                for (int dst = slotStart; dst < slotEnd; dst++)
                {
                    if (_s.SlotItem[dst] != h) continue;
                    int cap = _view.EffectiveCapacity(dst, item);
                    for (int src = dst + 1; src < slotEnd && _s.SlotCountArr[dst] < cap; src++)
                    {
                        if (_s.SlotItem[src] != h) continue;
                        if (_s.SlotReserved[src] > 0) continue; // never move reserved stock
                        int move = Math.Min(cap - _s.SlotCountArr[dst], _s.SlotCountArr[src]);
                        if (move <= 0) continue;
                        _s.SlotCountArr[dst] += move; _s.SlotCountArr[src] -= move; changed = true;
                        if (_s.SlotCountArr[src] == 0) _s.SlotItem[src] = -1;
                    }
                }
            }
            return changed;
        }

        /// <summary>Extracts up to count of item (unreserved) from the range, highest index first to keep low slots full. Returns taken.</summary>
        public int Extract(int slotStart, int slotEnd, ItemId item, int count)
        {
            Touch();
            int taken = 0;
            for (int g = slotEnd - 1; g >= slotStart && taken < count; g--)
            {
                if (_s.SlotItem[g] != item.Handle) continue;
                int free = _s.SlotCountArr[g] - _s.SlotReserved[g];
                int take = Math.Min(free, count - taken);
                if (take <= 0) continue;
                _s.SlotCountArr[g] -= take; taken += take;
                if (_s.SlotCountArr[g] == 0) _s.SlotItem[g] = -1;
            }
            return taken;
        }

        public int Reserve(int slotStart, int slotEnd, ItemId item, int count)
        {
            Touch();
            int reserved = 0;
            for (int g = slotStart; g < slotEnd && reserved < count; g++)
            {
                if (_s.SlotItem[g] != item.Handle) continue;
                int free = _s.SlotCountArr[g] - _s.SlotReserved[g];
                int take = Math.Min(free, count - reserved);
                if (take <= 0) continue;
                _s.SlotReserved[g] += take; reserved += take;
            }
            return reserved;
        }

        public int Release(int slotStart, int slotEnd, ItemId item, int count)
        {
            Touch();
            int released = 0;
            for (int g = slotStart; g < slotEnd && released < count; g++)
            {
                if (_s.SlotItem[g] != item.Handle) continue;
                int take = Math.Min(_s.SlotReserved[g], count - released);
                _s.SlotReserved[g] -= take; released += take;
            }
            return released;
        }

        /// <summary>Consumes reserved stock (machine completion): decrements both reserved and count.</summary>
        public int ConsumeReserved(int slotStart, int slotEnd, ItemId item, int count)
        {
            Touch();
            int consumed = 0;
            for (int g = slotStart; g < slotEnd && consumed < count; g++)
            {
                if (_s.SlotItem[g] != item.Handle) continue;
                int take = Math.Min(_s.SlotReserved[g], count - consumed);
                if (take <= 0) continue;
                _s.SlotReserved[g] -= take; _s.SlotCountArr[g] -= take; consumed += take;
                if (_s.SlotCountArr[g] == 0) _s.SlotItem[g] = -1;
            }
            return consumed;
        }

        // ---------------- Persistent bag ----------------

        public void SetProgress(BlockRef b, float progress) { Touch(); _s.Bags[b.Index].Progress = progress; }
        public void SetEnergy(BlockRef b, float energy) { Touch(); _s.Bags[b.Index].Energy = energy; }

        public PersistentBag CaptureBag(BlockRef b)
        {
            if (!_view.IsAlive(b)) throw new ArgumentException("Cannot capture a missing block.", nameof(b));
            var bag = _s.Bags[b.Index].Clone();
            bag.Items.Clear(); bag.Reservations.Clear(); bag.Slots.Clear();
            int s = _s.SlotStart[b.Index], e = s + _s.SlotCount[b.Index];
            for (int g = s; g < e; g++)
            {
                bag.Slots.Add(new PersistentSlot(_s.ContainerKeyOfSlot[g], _s.SlotKey[g], _s.SlotItem[g], _s.SlotCountArr[g], _s.SlotReserved[g]));
                if (_s.SlotItem[g] < 0 || _s.SlotCountArr[g] == 0) continue;
                bag.Items.Add((_s.SlotItem[g], _s.SlotCountArr[g]));
                if (_s.SlotReserved[g] > 0) bag.Reservations.Add((_s.SlotItem[g], _s.SlotReserved[g]));
            }
            // slot keys travel so re-placement lands items in the same logical slots when possible
            var keys = new System.Text.StringBuilder();
            for (int g = s; g < e; g++) if (_s.SlotItem[g] >= 0) keys.Append(_s.ContainerKeyOfSlot[g]).Append('/').Append(_s.SlotKey[g]).Append('=').Append(_s.Registry.Items.FromHandle(_s.SlotItem[g]).Canonical).Append(':').Append(_s.SlotCountArr[g]).Append(';');
            if (keys.Length > 0) bag.Extras["slots"] = keys.ToString();
            return bag;
        }

        private void RestoreBag(BlockRef b, PersistentBag bag)
        {
            var target = _s.Bags[b.Index];
            target.Progress = bag.Progress; target.Energy = bag.Energy;
            target.Machine = bag.Machine?.Clone(); target.FuelDebt = bag.FuelDebt;
            foreach (var kv in bag.Extras) if (kv.Key != "slots") target.Extras[kv.Key] = kv.Value;

            int s = _s.SlotStart[b.Index], e = s + _s.SlotCount[b.Index];
            if (bag.Slots.Count > 0)
            {
                foreach (var slot in bag.Slots)
                {
                    int found = -1;
                    for (int g = s; g < e; g++) if (_s.ContainerKeyOfSlot[g] == slot.Container && _s.SlotKey[g] == slot.Key) { found = g; break; }
                    if (found < 0) throw new InvalidOperationException("Saved slot binding is missing: " + slot.Container + "/" + slot.Key);
                    _s.SlotItem[found] = slot.Item; _s.SlotCountArr[found] = slot.Count; _s.SlotReserved[found] = slot.Reserved;
                }
                return;
            }
            var leftovers = new List<(int item, int count)>();
            // First try exact slot keys recorded at capture.
            if (bag.Extras.TryGetValue("slots", out var slots))
            {
                foreach (var entry in slots.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = entry.IndexOf('='); int colon = entry.LastIndexOf(':');
                    string path = entry.Substring(0, eq); string canon = entry.Substring(eq + 1, colon - eq - 1); int count = int.Parse(entry.Substring(colon + 1));
                    int slash = path.IndexOf('/');
                    string ck = path.Substring(0, slash), sk = path.Substring(slash + 1);
                    int handle = _s.Registry.Items.Intern(canon).Handle;
                    bool placed = false;
                    for (int g = s; g < e; g++)
                    {
                        if (_s.ContainerKeyOfSlot[g] == ck && _s.SlotKey[g] == sk && _s.SlotItem[g] < 0)
                        {
                            _s.SlotItem[g] = handle; _s.SlotCountArr[g] = count; placed = true; break;
                        }
                    }
                    if (!placed) leftovers.Add((handle, count));
                }
            }
            else leftovers.AddRange(bag.Items);

            // Leftovers: deterministic insertion into any slot; anything that still does not fit goes to Extras overflow.
            foreach (var (item, count) in leftovers)
            {
                var id = _s.Registry.Items.FromHandle(item);
                int placed = Insert(s, e, id, count);
                if (placed < count) target.Extras["overflow:" + id.Canonical] = (count - placed).ToString();
            }
            foreach (var (item, count) in bag.Reservations)
                Reserve(s, e, _s.Registry.Items.FromHandle(item), count);
        }

        // ---------------- Allocation / compaction ----------------

        private int AllocBlock()
        {
            if (_freeBlocks.Count > 0)
            {
                int i = _freeBlocks[_freeBlocks.Count - 1];
                _freeBlocks.RemoveAt(_freeBlocks.Count - 1);
                return i;
            }
            _s.BlockDef.Add(BlockDefHandle.None); _s.Orientation.Add(0); _s.AnchorCell.Add(CellRef.None); _s.ControllerCoord.Add(CellRef.None);
            _s.SlotStart.Add(0); _s.SlotCount.Add(0); _s.PortStart.Add(0); _s.PortCount.Add(0); _s.Bags.Add(null); _s.Alive.Add(false);
            _s.Accessors.Add(null);
            return _s.BlockDef.Count - 1;
        }

        /// <summary>
        /// Between ticks only (ADR-0004): recycle dead block indices. BlockRef values for dead blocks
        /// become invalid; live indices are preserved so references held across ticks stay valid.
        /// Slot/port storage of dead blocks is left in place; a full rebuild is done by save/load.
        /// </summary>
        public int Compact()
        {
            Touch();
            int recycled = 0;
            for (int i = 0; i < _s.Alive.Count; i++)
            {
                if (_s.Alive[i] || _freeBlocks.Contains(i)) continue;
                if (_s.BlockDef[i].IsNone && _s.Bags[i] == null) continue; // never used
                _s.BlockDef[i] = BlockDefHandle.None; _s.Bags[i] = null; _s.SlotCount[i] = 0; _s.PortCount[i] = 0;
                _freeBlocks.Add(i); recycled++;
            }
            return recycled;
        }
    }
}

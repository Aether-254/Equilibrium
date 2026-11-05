using System;
using System.Collections.Generic;

namespace Equilibrium.Simulation
{
    /// <summary>
    /// Resolve phase: (1) build the dependency graph and topologically order transactions,
    /// rejecting cycles with <see cref="ReasonCode.TransactionCycle"/>; (2) walk in topological
    /// order, rejecting dependents of rejected transactions with <see cref="ReasonCode.DependencyFailed"/>;
    /// (3) group remaining candidates by contested resource and, when demand exceeds capacity,
    /// accept in (-priority, tieBreakKey) order until capacity is exhausted, rejecting the rest
    /// with <see cref="ReasonCode.Contended"/>. Arrays are reused across ticks (no steady-state allocation).
    /// </summary>
    internal sealed class Resolver
    {
        // Adjacency (CSR): for each transaction, the list of dependents that wait on it.
        private int[] _headStart = new int[0];
        private int[] _adj = new int[0];
        private int[] _indegree = new int[0];
        private int[] _order = new int[0];
        private int[] _queue = new int[0];
        private int[] _depth = new int[0];

        // Conflict grouping.
        private long[] _keys = new long[0];
        private int[] _demand = new int[0];
        private int[] _capacity = new int[0];
        private int[] _byKey = new int[0];
        private ulong[] _tie = new ulong[0];
        private long[] _releaseKeys = new long[0];
        private int[] _releaseAmount = new int[0];
        private int[] _byRelease = new int[0];

        public int LastLongestChain { get; private set; }

        private readonly KeyComparer _keyComparer;
        private readonly GroupComparer _groupComparer;
        private readonly ReleaseComparer _releaseComparer;
        private TransactionBuffer _currentBuffer;

        public Resolver(int initialCapacity)
        {
            _keyComparer = new KeyComparer(this);
            _groupComparer = new GroupComparer(this);
            _releaseComparer = new ReleaseComparer(this);
            Ensure(Math.Max(1, initialCapacity));
        }

        private sealed class ReleaseComparer : IComparer<int>
        {
            private readonly Resolver _r;
            public ReleaseComparer(Resolver r) { _r = r; }
            public int Compare(int a, int b)
            {
                int c = _r._releaseKeys[a].CompareTo(_r._releaseKeys[b]);
                return c != 0 ? c : a.CompareTo(b);
            }
        }

        private sealed class KeyComparer : IComparer<int>
        {
            private readonly Resolver _r;
            public KeyComparer(Resolver r) { _r = r; }
            public int Compare(int a, int b)
            {
                int c = _r._keys[a].CompareTo(_r._keys[b]);
                return c != 0 ? c : a.CompareTo(b);
            }
        }

        private sealed class GroupComparer : IComparer<int>
        {
            private readonly Resolver _r;
            public GroupComparer(Resolver r) { _r = r; }
            public int Compare(int a, int b)
            {
                int pa = _r._currentBuffer[a].Priority, pb = _r._currentBuffer[b].Priority;
                if (pa != pb) return pb.CompareTo(pa);
                int c = _r._tie[a].CompareTo(_r._tie[b]);
                return c != 0 ? c : a.CompareTo(b);
            }
        }

        private void Ensure(int n)
        {
            if (_indegree.Length >= n) return;
            int cap = Math.Max(n, _indegree.Length * 2);
            Array.Resize(ref _headStart, cap + 1);
            Array.Resize(ref _indegree, cap);
            Array.Resize(ref _order, cap);
            Array.Resize(ref _queue, cap);
            Array.Resize(ref _depth, cap);
            Array.Resize(ref _keys, cap);
            Array.Resize(ref _demand, cap);
            Array.Resize(ref _capacity, cap);
            Array.Resize(ref _byKey, cap);
            Array.Resize(ref _tie, cap);
            Array.Resize(ref _releaseKeys, cap);
            Array.Resize(ref _releaseAmount, cap);
            Array.Resize(ref _byRelease, cap);
        }

        public void Run(TransactionBuffer buffer, ITieBreakSource tieBreak, IConflictKeyProvider conflicts, in TickSnapshot snapshot)
        {
            int n = buffer.Count;
            LastLongestChain = 0;
            _currentBuffer = buffer;
            if (n == 0) return;
            Ensure(n);
            int e = buffer.DependencyCount;
            if (_adj.Length < e) Array.Resize(ref _adj, Math.Max(e, _adj.Length * 2));

            // --- 1. Topological order over dependency edges (dependency -> dependent). ---
            Array.Clear(_indegree, 0, n);
            Array.Clear(_headStart, 0, n + 1);
            for (int i = 0; i < e; i++)
            {
                var (dependent, dependency) = buffer.Dependency(i);
                _headStart[dependency + 1]++;
                _indegree[dependent]++;
            }
            for (int i = 0; i < n; i++) _headStart[i + 1] += _headStart[i];
            // fill adjacency using a moving cursor stored temporarily in _byKey
            Array.Copy(_headStart, _byKey, n);
            for (int i = 0; i < e; i++)
            {
                var (dependent, dependency) = buffer.Dependency(i);
                _adj[_byKey[dependency]++] = dependent;
            }

            int qh = 0, qt = 0, emitted = 0;
            for (int i = 0; i < n; i++)
            {
                _depth[i] = 0;
                if (_indegree[i] == 0) _queue[qt++] = i;
            }
            while (qh < qt)
            {
                int v = _queue[qh++];
                _order[emitted++] = v;
                if (_depth[v] > LastLongestChain) LastLongestChain = _depth[v];
                for (int k = _headStart[v]; k < _headStart[v + 1]; k++)
                {
                    int w = _adj[k];
                    if (_depth[v] + 1 > _depth[w]) _depth[w] = _depth[v] + 1;
                    if (--_indegree[w] == 0) _queue[qt++] = w;
                }
            }
            if (emitted < n)
            {
                // leftovers are on or downstream of a cycle
                for (int i = 0; i < n; i++)
                {
                    if (_indegree[i] > 0)
                    {
                        ref Transaction tx = ref buffer[i];
                        tx.Accepted = false;
                        tx.Reason = ReasonCode.TransactionCycle;
                    }
                }
            }

            // --- 2. Cascade dependency failures in topological order. ---
            for (int oi = 0; oi < emitted; oi++)
            {
                int v = _order[oi];
                ref Transaction tv = ref buffer[v];
                if (tv.Accepted) continue;
                for (int k = _headStart[v]; k < _headStart[v + 1]; k++)
                {
                    ref Transaction tw = ref buffer[_adj[k]];
                    if (tw.Accepted)
                    {
                        tw.Accepted = false;
                        tw.Reason = ReasonCode.DependencyFailed;
                    }
                }
            }

            // --- 3. Conflict groups. ---
            // Releases from accepted transactions add to the free capacity of their release key
            // within the same tick (a group vacating a cell lets the upstream group enter; the
            // dependency edge guarantees ordering, this guarantees capacity accounting).
            int candidates = 0;
            int releases = 0;
            for (int i = 0; i < n; i++)
            {
                ref Transaction tx = ref buffer[i];
                if (!tx.Accepted) { _keys[i] = 0; _releaseKeys[i] = 0; continue; }
                conflicts.Describe(in tx, in snapshot, out _keys[i], out _demand[i], out _capacity[i], out _releaseKeys[i], out _releaseAmount[i]);
                if (_keys[i] != 0) _byKey[candidates++] = i;
                if (_releaseKeys[i] != 0) _byRelease[releases++] = i;
            }
            if (candidates == 0) return;

            // Sort candidate indices by key so groups are contiguous; stable by index for determinism.
            Array.Sort(_byKey, 0, candidates, _keyComparer);
            Array.Sort(_byRelease, 0, releases, _releaseComparer);

            int gs = 0;
            int rs = 0;
            while (gs < candidates)
            {
                int ge = gs + 1;
                long key = _keys[_byKey[gs]];
                int totalDemand = _demand[_byKey[gs]];
                while (ge < candidates && _keys[_byKey[ge]] == key) { totalDemand += _demand[_byKey[ge]]; ge++; }
                int cap = _capacity[_byKey[gs]];
                // add releases on this key from transactions still accepted
                while (rs < releases && _releaseKeys[_byRelease[rs]] < key) rs++;
                for (int r = rs; r < releases && _releaseKeys[_byRelease[r]] == key; r++)
                    if (buffer[_byRelease[r]].Accepted) cap += _releaseAmount[_byRelease[r]];
                if (totalDemand > cap)
                {
                    // Draw one tie-break key per member, then order by (-priority, key).
                    for (int g = gs; g < ge; g++) _tie[_byKey[g]] = tieBreak.NextKey();
                    Array.Sort(_byKey, gs, ge - gs, _groupComparer);
                    int remaining = cap;
                    for (int g = gs; g < ge; g++)
                    {
                        ref Transaction tx = ref buffer[_byKey[g]];
                        if (_demand[_byKey[g]] <= remaining)
                        {
                            remaining -= _demand[_byKey[g]];
                        }
                        else
                        {
                            tx.Accepted = false;
                            tx.Reason = ReasonCode.Contended;
                        }
                    }
                }
                gs = ge;
            }

            // Losers may have dependents already marked accepted; cascade once more in topo order.
            for (int oi = 0; oi < emitted; oi++)
            {
                int v = _order[oi];
                ref Transaction tv = ref buffer[v];
                if (tv.Accepted) continue;
                for (int k = _headStart[v]; k < _headStart[v + 1]; k++)
                {
                    ref Transaction tw = ref buffer[_adj[k]];
                    if (tw.Accepted)
                    {
                        tw.Accepted = false;
                        tw.Reason = ReasonCode.DependencyFailed;
                    }
                }
            }
        }
    }
}

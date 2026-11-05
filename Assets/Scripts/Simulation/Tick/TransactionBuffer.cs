using System;

namespace Equilibrium.Simulation
{
    /// <summary>
    /// Growable, reusable store of transactions for one tick plus their dependency edges.
    /// Story 001 provides the API surface; story 002 makes it pool-backed and allocation-free
    /// in steady state. Cleared at Snapshot; ids are indices into this buffer.
    /// </summary>
    public sealed class TransactionBuffer
    {
        private Transaction[] _items;
        private int _count;

        // Dependency edges stored as parallel arrays: dependent -> dependency.
        private int[] _depFrom;
        private int[] _depTo;
        private int _depCount;

        public TransactionBuffer(int initialCapacity = 256, int initialDependencyCapacity = 256)
        {
            _items = new Transaction[Math.Max(1, initialCapacity)];
            _depFrom = new int[Math.Max(1, initialDependencyCapacity)];
            _depTo = new int[_depFrom.Length];
        }

        public int Count => _count;
        public int DependencyCount => _depCount;

        /// <summary>Adds a transaction and returns its id for this tick.</summary>
        public TransactionId Add(in Transaction tx, int systemIndex)
        {
            if (_count == _items.Length)
                Array.Resize(ref _items, _items.Length * 2);
            var id = new TransactionId(_count);
            _items[_count] = tx;
            _items[_count].Id = id;
            _items[_count].SystemIndex = systemIndex;
            _items[_count].Reason = ReasonCode.None;
            _items[_count].Accepted = false;
            _count++;
            return id;
        }

        /// <summary>Declares that <paramref name="dependent"/> may only commit if <paramref name="dependency"/> commits.</summary>
        public void AddDependency(TransactionId dependent, TransactionId dependency)
        {
            if (dependent.IsNone || dependency.IsNone) throw new ArgumentException("dependency ids must be valid");
            if (_depCount == _depFrom.Length)
            {
                Array.Resize(ref _depFrom, _depFrom.Length * 2);
                Array.Resize(ref _depTo, _depTo.Length * 2);
            }
            _depFrom[_depCount] = dependent.Index;
            _depTo[_depCount] = dependency.Index;
            _depCount++;
        }

        public ref Transaction this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count) throw new IndexOutOfRangeException();
                return ref _items[index];
            }
        }

        public ref Transaction Get(TransactionId id) => ref this[id.Index];

        public ReadOnlySpan<Transaction> AsSpan() => new ReadOnlySpan<Transaction>(_items, 0, _count);

        public (int dependent, int dependency) Dependency(int edgeIndex)
        {
            if ((uint)edgeIndex >= (uint)_depCount) throw new IndexOutOfRangeException();
            return (_depFrom[edgeIndex], _depTo[edgeIndex]);
        }

        /// <summary>Resets counts without releasing capacity.</summary>
        public void Clear()
        {
            _count = 0;
            _depCount = 0;
        }
    }
}

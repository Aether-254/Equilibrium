using System;
using System.Collections.Generic;

namespace Equilibrium.Content
{
    /// <summary>
    /// Interns canonical ItemId strings to dense handles. One table per <see cref="ContentRegistry"/>;
    /// no static state so tests and multiple registries can coexist.
    /// </summary>
    public sealed class ItemIdTable
    {
        private readonly Dictionary<string, int> _handles = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _canonical = new List<string>();
        private readonly List<int> _colon = new List<int>();

        public int Count => _canonical.Count;

        /// <summary>Parses and interns. Returns false with an error for invalid input.</summary>
        public bool TryParse(string raw, out ItemId id, out ContentError error)
        {
            id = ItemId.None;
            if (!ItemId.TryCanonicalize(raw, out string canonical, out error)) return false;
            id = Intern(canonical);
            return true;
        }

        public ItemId Parse(string raw)
        {
            if (!TryParse(raw, out var id, out var err)) throw new ContentException(err, raw);
            return id;
        }

        /// <summary>Interns an already-canonical string. Caller guarantees validity.</summary>
        public ItemId Intern(string canonical)
        {
            if (!_handles.TryGetValue(canonical, out int h))
            {
                h = _canonical.Count;
                _handles.Add(canonical, h);
                _canonical.Add(canonical);
                _colon.Add(canonical.IndexOf(':'));
            }
            return new ItemId(h, this);
        }

        public bool TryGet(string canonical, out ItemId id)
        {
            if (_handles.TryGetValue(canonical, out int h)) { id = new ItemId(h, this); return true; }
            id = ItemId.None;
            return false;
        }

        public ItemId FromHandle(int handle)
        {
            if ((uint)handle >= (uint)_canonical.Count) throw new ArgumentOutOfRangeException(nameof(handle));
            return new ItemId(handle, this);
        }

        internal string CanonicalOf(int handle) => _canonical[handle];
        internal string NamespaceOf(int handle) => _canonical[handle].Substring(0, _colon[handle]);
        internal string PathOf(int handle) => _canonical[handle].Substring(_colon[handle] + 1);
    }
}

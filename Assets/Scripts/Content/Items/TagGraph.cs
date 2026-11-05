using System;
using System.Collections.Generic;

namespace Equilibrium.Content
{
    /// <summary>Interned tag identity. Canonical form is "namespace:path" without the '#' prefix.</summary>
    public readonly struct TagId : IEquatable<TagId>
    {
        public readonly int Handle;
        private readonly TagGraph _graph;
        internal TagId(int handle, TagGraph graph) { Handle = handle; _graph = graph; }
        public static readonly TagId None = default;
        public bool IsNone => _graph == null;
        public string Canonical => _graph == null ? string.Empty : _graph.CanonicalOf(Handle);
        public bool Equals(TagId other) => Handle == other.Handle && ReferenceEquals(_graph, other._graph);
        public override bool Equals(object obj) => obj is TagId o && Equals(o);
        public override int GetHashCode() => Handle;
        public override string ToString() => IsNone ? "<none>" : "#" + Canonical;
    }

    /// <summary>
    /// Hierarchical tag graph (spec s2A). '/' in a path means inclusion: members of a:b/c are
    /// members of a:b. Tags may reference other tags; reference cycles are hard errors. Addon
    /// removals win over additions and cascade from a parent to every descendant tag.
    /// Membership is expanded once at <see cref="Build"/> into per-tag bitsets over item handles.
    /// </summary>
    public sealed class TagGraph
    {
        private readonly ItemIdTable _items;
        private readonly Dictionary<string, int> _handles = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _canonical = new List<string>();
        private readonly List<HashSet<int>> _direct = new List<HashSet<int>>();
        private readonly List<HashSet<int>> _removed = new List<HashSet<int>>();
        private readonly List<HashSet<int>> _refs = new List<HashSet<int>>();
        private readonly List<string> _owningNamespace = new List<string>();
        private List<BitSet> _members;
        private bool _built;

        public int MaxDepth { get; set; } = 16;
        public int Count => _canonical.Count;

        public TagGraph(ItemIdTable items) { _items = items; }

        /// <summary>Canonicalizes a tag name: strips '#', adds default namespace, validates shape.</summary>
        public static bool TryCanonicalize(string raw, out string canonical, out ContentError error)
        {
            canonical = null; error = ContentError.None;
            if (string.IsNullOrEmpty(raw)) { error = ContentError.InvalidTagName; return false; }
            string s = raw[0] == '#' ? raw.Substring(1) : raw;
            if (!ItemId.TryCanonicalize(s, out canonical, out _)) { error = ContentError.InvalidTagName; return false; }
            return true;
        }

        public TagId Intern(string canonical, string owningNamespace)
        {
            if (_built) throw new InvalidOperationException("TagGraph already built");
            if (!_handles.TryGetValue(canonical, out int h))
            {
                h = _canonical.Count;
                _handles.Add(canonical, h);
                _canonical.Add(canonical);
                _direct.Add(new HashSet<int>());
                _removed.Add(new HashSet<int>());
                _refs.Add(new HashSet<int>());
                _owningNamespace.Add(owningNamespace);
            }
            return new TagId(h, this);
        }

        public bool TryGet(string canonical, out TagId id)
        {
            if (_handles.TryGetValue(canonical, out int h)) { id = new TagId(h, this); return true; }
            id = TagId.None; return false;
        }

        public void AddMember(TagId tag, ItemId item) => _direct[tag.Handle].Add(item.Handle);
        public void RemoveMember(TagId tag, ItemId item) => _removed[tag.Handle].Add(item.Handle);
        public void AddReference(TagId tag, TagId referenced) => _refs[tag.Handle].Add(referenced.Handle);

        internal string CanonicalOf(int handle) => _canonical[handle];

        /// <summary>
        /// Resolves references and hierarchy into membership bitsets. Returns diagnostics; tags
        /// involved in reference cycles are reported and left empty (their content is rejected).
        /// </summary>
        public List<ContentDiagnostic> Build()
        {
            var diags = new List<ContentDiagnostic>();
            int n = _canonical.Count;
            _members = new List<BitSet>(n);
            for (int i = 0; i < n; i++) _members.Add(new BitSet(_items.Count));

            // Depth check.
            for (int i = 0; i < n; i++)
            {
                int depth = 1;
                foreach (char c in _canonical[i]) if (c == '/') depth++;
                if (depth > MaxDepth) diags.Add(new ContentDiagnostic(ContentError.TagDepthExceeded, _owningNamespace[i], "#" + _canonical[i]));
            }

            // Children: tag j is a child of tag i when j's canonical starts with i's canonical + '/'.
            var children = new List<List<int>>(n);
            for (int i = 0; i < n; i++) children.Add(new List<int>());
            for (int j = 0; j < n; j++)
            {
                string cj = _canonical[j];
                int slash = cj.LastIndexOf('/');
                while (slash > 0)
                {
                    string parent = cj.Substring(0, slash);
                    if (_handles.TryGetValue(parent, out int p)) children[p].Add(j);
                    slash = parent.LastIndexOf('/');
                }
            }

            // Edges for topological resolution: a tag depends on its children and its references.
            var indegree = new int[n];
            var dependents = new List<List<int>>(n);
            for (int i = 0; i < n; i++) dependents.Add(new List<int>());
            for (int i = 0; i < n; i++)
            {
                foreach (int c in children[i]) { dependents[c].Add(i); indegree[i]++; }
                foreach (int r in _refs[i]) { dependents[r].Add(i); indegree[i]++; }
            }
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++) if (indegree[i] == 0) queue.Enqueue(i);
            var order = new List<int>(n);
            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                order.Add(v);
                foreach (int d in dependents[v]) if (--indegree[d] == 0) queue.Enqueue(d);
            }
            var inCycle = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (indegree[i] > 0)
                {
                    inCycle[i] = true;
                    diags.Add(new ContentDiagnostic(ContentError.TagCycle, _owningNamespace[i], "#" + _canonical[i]));
                }
            }

            // Effective removals for a tag = its own removals plus every ancestor's (spec s2A.5:
            // removing from a parent removes from all children that inherited it). Since a child's
            // members flow up into the parent via OR, applying the ancestor set at the child level
            // keeps both directions consistent in one expansion pass.
            var effectiveRemoved = new List<HashSet<int>>(n);
            for (int i = 0; i < n; i++)
            {
                var set = new HashSet<int>(_removed[i]);
                string ci = _canonical[i];
                int slash = ci.LastIndexOf('/');
                while (slash > 0)
                {
                    string parent = ci.Substring(0, slash);
                    if (_handles.TryGetValue(parent, out int p)) set.UnionWith(_removed[p]);
                    slash = parent.LastIndexOf('/');
                }
                effectiveRemoved.Add(set);
            }

            // Expand in dependency order: direct + children + references, minus effective removals.
            foreach (int v in order)
            {
                var m = _members[v];
                foreach (int item in _direct[v]) m.Set(item);
                foreach (int c in children[v]) if (!inCycle[c]) m.Or(_members[c]);
                foreach (int r in _refs[v]) if (!inCycle[r]) m.Or(_members[r]);
                foreach (int item in effectiveRemoved[v]) m.Clear(item);
            }

            _built = true;
            return diags;
        }

        public bool Contains(TagId tag, ItemId item)
        {
            if (!_built) throw new InvalidOperationException("TagGraph not built");
            return _members[tag.Handle].Get(item.Handle);
        }

        public IEnumerable<ItemId> Members(TagId tag)
        {
            if (!_built) throw new InvalidOperationException("TagGraph not built");
            var m = _members[tag.Handle];
            for (int i = 0; i < _items.Count; i++) if (m.Get(i)) yield return _items.FromHandle(i);
        }
    }

    internal sealed class BitSet
    {
        private ulong[] _words;
        public BitSet(int bits) { _words = new ulong[(Math.Max(bits, 1) + 63) / 64]; }
        private void Ensure(int bit) { int need = (bit >> 6) + 1; if (need > _words.Length) Array.Resize(ref _words, Math.Max(need, _words.Length * 2)); }
        public void Set(int bit) { Ensure(bit); _words[bit >> 6] |= 1UL << (bit & 63); }
        public void Clear(int bit) { if ((bit >> 6) < _words.Length) _words[bit >> 6] &= ~(1UL << (bit & 63)); }
        public bool Get(int bit) => (bit >> 6) < _words.Length && (_words[bit >> 6] & (1UL << (bit & 63))) != 0;
        public void ClearAll() => Array.Clear(_words, 0, _words.Length);
        public void Or(BitSet other)
        {
            if (other._words.Length > _words.Length) Array.Resize(ref _words, other._words.Length);
            for (int i = 0; i < other._words.Length; i++) _words[i] |= other._words[i];
        }
    }
}

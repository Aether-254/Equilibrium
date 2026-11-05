using System;
using System.Text.RegularExpressions;

namespace Equilibrium.Content
{
    /// <summary>
    /// Stable, namespaced item identity (spec s2). Runtime code compares by interned <see cref="Handle"/>;
    /// data and saves use <see cref="Canonical"/>. Parse via <see cref="ItemIdTable"/>.
    /// </summary>
    public readonly struct ItemId : IEquatable<ItemId>, IComparable<ItemId>
    {
        public const string DefaultNamespace = "equilibrium";
        public static readonly Regex Pattern = new Regex(@"^[a-z0-9][a-z0-9._-]*:[a-z0-9][a-z0-9._/-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public readonly int Handle;
        private readonly ItemIdTable _table;

        internal ItemId(int handle, ItemIdTable table) { Handle = handle; _table = table; }

        public static readonly ItemId None = default;
        public bool IsNone => _table == null;

        public string Canonical => _table == null ? string.Empty : _table.CanonicalOf(Handle);
        public string Namespace => _table == null ? string.Empty : _table.NamespaceOf(Handle);
        public string Path => _table == null ? string.Empty : _table.PathOf(Handle);

        public bool Equals(ItemId other) => Handle == other.Handle && ReferenceEquals(_table, other._table);
        public override bool Equals(object obj) => obj is ItemId o && Equals(o);
        public override int GetHashCode() => Handle;
        public int CompareTo(ItemId other) => string.CompareOrdinal(Canonical, other.Canonical);
        public override string ToString() => IsNone ? "<none>" : Canonical;

        public static bool operator ==(ItemId a, ItemId b) => a.Equals(b);
        public static bool operator !=(ItemId a, ItemId b) => !a.Equals(b);

        /// <summary>
        /// Normalizes raw config/display input: adds the default namespace when omitted, rejects
        /// anything not matching the spec regex. Does not intern.
        /// </summary>
        public static bool TryCanonicalize(string raw, out string canonical, out ContentError error)
        {
            canonical = null;
            error = ContentError.None;
            if (string.IsNullOrEmpty(raw)) { error = ContentError.InvalidItemId; return false; }
            string candidate = raw.IndexOf(':') < 0 ? DefaultNamespace + ":" + raw : raw;
            if (!Pattern.IsMatch(candidate)) { error = ContentError.InvalidItemId; return false; }
            if (candidate.Contains("//") || candidate.EndsWith("/", StringComparison.Ordinal)) { error = ContentError.InvalidItemId; return false; }
            canonical = candidate;
            return true;
        }
    }
}

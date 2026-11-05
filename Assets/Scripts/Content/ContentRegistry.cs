using System;
using System.Collections.Generic;

namespace Equilibrium.Content
{
    public enum RegistryState { Unloaded, LoadingBase, LoadingAddons, Ready, Fatal }

    /// <summary>
    /// Validated runtime content: items, tags, recipes, machines, market entries, unlock nodes.
    /// Built by <see cref="ContentLoader"/>; read-only afterwards (registry: content_registry).
    /// </summary>
    public sealed class ContentRegistry
    {
        public const int DefaultMaxStack = 64;

        public ItemIdTable Items { get; } = new ItemIdTable();
        public TagGraph Tags { get; }
        public RegistryState State { get; internal set; } = RegistryState.Unloaded;

        private readonly Dictionary<int, ItemDef> _defs = new Dictionary<int, ItemDef>();
        private readonly Dictionary<string, RecipeDef> _recipes = new Dictionary<string, RecipeDef>(StringComparer.Ordinal);
        private readonly Dictionary<string, MachineDef> _machines = new Dictionary<string, MachineDef>(StringComparer.Ordinal);
        private readonly Dictionary<int, MarketItemDef> _market = new Dictionary<int, MarketItemDef>();
        private readonly Dictionary<string, UnlockNodeDef> _unlocks = new Dictionary<string, UnlockNodeDef>(StringComparer.Ordinal);
        private readonly HashSet<string> _loadedNamespaces = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<RecipeDef> _recipeOrder = new List<RecipeDef>();

        public ContentRegistry() { Tags = new TagGraph(Items); }

        public IReadOnlyCollection<string> LoadedNamespaces => _loadedNamespaces;
        public IReadOnlyList<RecipeDef> RecipesInOrder => _recipeOrder;
        public int ItemCount => _defs.Count;

        public bool TryGetItem(ItemId id, out ItemDef def) => _defs.TryGetValue(id.Handle, out def);
        public ItemDef Item(ItemId id) => _defs.TryGetValue(id.Handle, out var d) ? d : throw new KeyNotFoundException(id.Canonical);
        public bool InTag(ItemId item, TagId tag) => Tags.Contains(tag, item);

        /// <summary>item-registry GDD rule 7 / container edge case: UnknownItem falls back to the default knob.</summary>
        public int MaxStackOf(ItemId item) => _defs.TryGetValue(item.Handle, out var d) && !d.IsUnknown ? d.MaxStack : DefaultMaxStack;
        public bool TryGetRecipe(string id, out RecipeDef r) => _recipes.TryGetValue(id, out r);
        public bool TryGetMachine(string id, out MachineDef m) => _machines.TryGetValue(id, out m);
        public bool TryGetMarket(ItemId item, out MarketItemDef m) => _market.TryGetValue(item.Handle, out m);
        public bool TryGetUnlock(string id, out UnlockNodeDef u) => _unlocks.TryGetValue(id, out u);
        public IEnumerable<UnlockNodeDef> Unlocks => _unlocks.Values;
        public bool NamespaceLoaded(string ns) => _loadedNamespaces.Contains(ns);

        /// <summary>Recipes applicable to a machine, in stable content order (machine-processing GDD rule 4).</summary>
        public IEnumerable<RecipeDef> RecipesFor(MachineDef machine)
        {
            foreach (var r in _recipeOrder)
            {
                if (r.Machine != null && r.Machine == machine.Id) { yield return r; continue; }
                if (!r.MachineTag.IsNone)
                    foreach (var t in machine.Tags) if (t.Equals(r.MachineTag)) { yield return r; break; }
            }
        }

        /// <summary>
        /// Resolves an item reference from a save. Unknown namespaces yield an UnknownItem def that
        /// keeps the original id and uses the default max stack (spec s2B.3).
        /// </summary>
        public ItemDef ResolveOrUnknown(string canonical)
        {
            var id = Items.Intern(canonical);
            if (_defs.TryGetValue(id.Handle, out var def)) return def;
            var unknown = new ItemDef(id, DefaultMaxStack, canonical, null, false, 0f, null, isUnknown: true, null);
            _defs[id.Handle] = unknown;
            return unknown;
        }

        internal bool TryAddItem(ItemDef def)
        {
            if (_defs.TryGetValue(def.Id.Handle, out var existing) && !existing.IsUnknown) return false;
            _defs[def.Id.Handle] = def;
            return true;
        }
        internal void AddRecipe(RecipeDef r) { _recipes[r.Id] = r; _recipeOrder.Add(r); }
        internal void AddMachine(MachineDef m) => _machines[m.Id] = m;
        internal void AddMarket(MarketItemDef m) => _market[m.Item.Handle] = m;
        internal void AddUnlock(UnlockNodeDef u) => _unlocks[u.Id] = u;
        internal void MarkNamespaceLoaded(string ns) => _loadedNamespaces.Add(ns);
    }
}

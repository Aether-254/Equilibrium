using System.Collections.Generic;
using UnityEngine;

namespace Equilibrium.Content.Unity
{
    /// <summary>
    /// Inspector-authored item definition. <see cref="Export"/> yields the plain record the shared
    /// validator consumes (ADR-0003). Field names mirror <see cref="ItemDefData"/> so the JSON
    /// reference export and this asset stay in lock-step.
    /// </summary>
    [CreateAssetMenu(menuName = "Equilibrium/Content/Item Def", fileName = "item_")]
    public sealed class ItemDefAsset : ScriptableObject
    {
        [Tooltip("namespace:path (default namespace may be omitted)")] public string Id;
        [Min(1)] public int MaxStack = 64;
        public string DisplayNameKey;
        public Sprite Icon;
        public bool Tradeable;
        [Tooltip("Joules per item when used as fuel; 0 = not a fuel")] public float EnergyJ;
        [Tooltip("Block definition key when placeable")] public string BlockDef;
        public List<ExtraField> Extras = new List<ExtraField>();

        public ItemDefData Export()
        {
            var d = new ItemDefData
            {
                Id = Id, MaxStack = MaxStack, DisplayNameKey = DisplayNameKey,
                Icon = Icon != null ? Icon.name : null, Tradeable = Tradeable, EnergyJ = EnergyJ, BlockDef = string.IsNullOrEmpty(BlockDef) ? null : BlockDef,
            };
            foreach (var e in Extras) if (!string.IsNullOrEmpty(e.Key)) d.Extras[e.Key] = e.Value;
            return d;
        }
    }

    [System.Serializable]
    public struct ExtraField { public string Key; public string Value; }

    [CreateAssetMenu(menuName = "Equilibrium/Content/Tag Def", fileName = "tag_")]
    public sealed class TagDefAsset : ScriptableObject
    {
        [Tooltip("#namespace:path or #path")] public string TagName;
        public List<string> Add = new List<string>();
        public List<string> Remove = new List<string>();
        public List<string> References = new List<string>();

        public TagDefData Export() => new TagDefData { Name = TagName, Add = new List<string>(Add), Remove = new List<string>(Remove), References = new List<string>(References) };
    }

    [System.Serializable]
    public struct RecipeIoField
    {
        [Tooltip("ItemId or #tag")] public string Item;
        [Min(1)] public int Count;
        [Range(0f, 1f)] public float Probability;
        public RecipeIoData Export() => new RecipeIoData { Item = Item, Count = Mathf.Max(1, Count), Probability = Probability <= 0f ? 1f : Probability };
    }

    [CreateAssetMenu(menuName = "Equilibrium/Content/Recipe Def", fileName = "recipe_")]
    public sealed class RecipeDefAsset : ScriptableObject
    {
        public string Id;
        [Tooltip("Machine def id or #machine-tag")] public string Machine;
        public List<RecipeIoField> Inputs = new List<RecipeIoField>();
        public List<RecipeIoField> Outputs = new List<RecipeIoField>();
        public List<RecipeIoField> Byproducts = new List<RecipeIoField>();
        [Min(0f)] public float WorkRequired;
        public bool AllowMixedTagInputs;
        public bool Irreversible;
        public bool AcceptWhileWorking = true;

        public RecipeDefData Export()
        {
            var r = new RecipeDefData { Id = Id, Machine = Machine, WorkRequired = WorkRequired, AllowMixedTagInputs = AllowMixedTagInputs, Irreversible = Irreversible, AcceptWhileWorking = AcceptWhileWorking };
            foreach (var io in Inputs) r.Inputs.Add(io.Export());
            foreach (var io in Outputs) r.Outputs.Add(io.Export());
            foreach (var io in Byproducts) r.Byproducts.Add(io.Export());
            return r;
        }
    }

    [CreateAssetMenu(menuName = "Equilibrium/Content/Machine Def", fileName = "machine_")]
    public sealed class MachineDefAsset : ScriptableObject
    {
        public string Id;
        [Min(0f)] public float TauReq;
        public List<string> Tags = new List<string>();
        public MachineDefData Export() => new MachineDefData { Id = Id, TauReq = TauReq, Tags = new List<string>(Tags) };
    }

    [CreateAssetMenu(menuName = "Equilibrium/Content/Market Item Def", fileName = "market_")]
    public sealed class MarketItemDefAsset : ScriptableObject
    {
        public string Item;
        [Min(0.01f)] public float BasePrice = 1f;
        [Range(0f, 0.5f)] public float SpreadFraction = 0.2f;
        [Min(1)] public int MaxSellVolumePerWindow = 200;
        [Min(1)] public int MaxBuyVolumePerWindow = 500;
        [Min(1)] public int WindowTicks = 60;
        public MarketItemDefData Export() => new MarketItemDefData { Item = Item, BasePrice = BasePrice, SpreadFraction = SpreadFraction, MaxSellVolumePerWindow = MaxSellVolumePerWindow, MaxBuyVolumePerWindow = MaxBuyVolumePerWindow, WindowTicks = WindowTicks };
    }

    [System.Serializable]
    public struct UnlockConditionField
    {
        public string Kind; public string Subject; public string Op; public double Value; public int WindowTicks;
        public UnlockConditionData Export() => new UnlockConditionData { Kind = Kind, Subject = Subject, Op = Op, Value = Value, WindowTicks = WindowTicks };
    }

    [System.Serializable]
    public struct UnlockGroupField
    {
        public List<UnlockConditionField> All;
        public UnlockConditionGroupData Export()
        {
            var g = new UnlockConditionGroupData();
            if (All != null) foreach (var c in All) g.All.Add(c.Export());
            return g;
        }
    }

    [CreateAssetMenu(menuName = "Equilibrium/Content/Unlock Node Def", fileName = "unlock_")]
    public sealed class UnlockNodeDefAsset : ScriptableObject
    {
        public string Id;
        [Min(1)] public int Tier = 1;
        [Tooltip("stateVariable | measurement | process | control | opportunity")] public string DepthTag;
        public List<string> Grants = new List<string>();
        public List<UnlockGroupField> ConditionGroups = new List<UnlockGroupField>();
        public UnlockNodeDefData Export()
        {
            var n = new UnlockNodeDefData { Id = Id, Tier = Tier, DepthTag = DepthTag, Grants = new List<string>(Grants) };
            foreach (var g in ConditionGroups) n.ConditionGroups.Add(g.Export());
            return n;
        }
    }

    /// <summary>
    /// Root asset listing every base-content definition. Loaded from Resources at startup and
    /// exported to the reference addon by the editor tool. Addressables label loading (ADR-0003)
    /// is deferred until the Addressables package is added; a catalog asset keeps the same
    /// IContentSource boundary so the swap is local to this class.
    /// </summary>
    [CreateAssetMenu(menuName = "Equilibrium/Content/Catalog", fileName = "ContentCatalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        public const string ResourcesPath = "ContentCatalog";
        public string Version = "0.1.0";
        public List<ItemDefAsset> Items = new List<ItemDefAsset>();
        public List<TagDefAsset> Tags = new List<TagDefAsset>();
        public List<RecipeDefAsset> Recipes = new List<RecipeDefAsset>();
        public List<MachineDefAsset> Machines = new List<MachineDefAsset>();
        public List<MarketItemDefAsset> Market = new List<MarketItemDefAsset>();
        public List<UnlockNodeDefAsset> Unlocks = new List<UnlockNodeDefAsset>();

        public ContentPack Export()
        {
            var p = new ContentPack();
            p.Manifest.Namespace = ItemId.DefaultNamespace;
            p.Manifest.Version = Version;
            p.Manifest.IsBase = true;
            foreach (var a in Items) if (a != null) p.Items.Add(a.Export());
            foreach (var a in Tags) if (a != null) p.Tags.Add(a.Export());
            foreach (var a in Recipes) if (a != null) p.Recipes.Add(a.Export());
            foreach (var a in Machines) if (a != null) p.Machines.Add(a.Export());
            foreach (var a in Market) if (a != null) p.Market.Add(a.Export());
            foreach (var a in Unlocks) if (a != null) p.Unlocks.Add(a.Export());
            return p;
        }
    }

    /// <summary>Base-content source backed by the <see cref="ContentCatalog"/> in Resources.</summary>
    public sealed class ScriptableObjectContentSource : IContentSource
    {
        private readonly ContentCatalog _catalog;
        public ScriptableObjectContentSource(ContentCatalog catalog) { _catalog = catalog; }

        public static ScriptableObjectContentSource FromResources()
        {
            var catalog = Resources.Load<ContentCatalog>(ContentCatalog.ResourcesPath);
            if (catalog == null) throw new System.InvalidOperationException($"Resources/{ContentCatalog.ResourcesPath} not found; run Equilibrium/Content/Generate Tier 1 Assets.");
            return new ScriptableObjectContentSource(catalog);
        }

        public ContentPack Load() => _catalog.Export();
    }
}

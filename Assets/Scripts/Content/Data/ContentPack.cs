using System.Collections.Generic;

namespace Equilibrium.Content
{
    /// <summary>
    /// Raw, un-validated content as produced by any <see cref="IContentSource"/> (ScriptableObject
    /// export or JSON parse). All identifiers are strings here; the loader canonicalizes, interns,
    /// validates, and builds the <see cref="ContentRegistry"/>. Keeping this layer string-based is
    /// what lets one validator serve both authoring paths (ADR-0003).
    /// </summary>
    public sealed class ContentPack
    {
        public AddonManifest Manifest = new AddonManifest();
        public List<ItemDefData> Items = new List<ItemDefData>();
        public List<TagDefData> Tags = new List<TagDefData>();
        public List<RecipeDefData> Recipes = new List<RecipeDefData>();
        public List<MachineDefData> Machines = new List<MachineDefData>();
        public List<MarketItemDefData> Market = new List<MarketItemDefData>();
        public List<UnlockNodeDefData> Unlocks = new List<UnlockNodeDefData>();
    }

    public sealed class AddonManifest
    {
        public string Namespace;
        public string Version = "0.0.0";
        public List<string> DependsOn = new List<string>();
        public List<string> Modifies = new List<string>();
        public bool IsBase;
    }

    public sealed class ItemDefData
    {
        public string Id;
        public int MaxStack = 64;
        public string DisplayNameKey;
        public string Icon;
        public bool Tradeable;
        /// <summary>Joules per item when used as fuel; 0 = not a fuel.</summary>
        public float EnergyJ;
        /// <summary>Block definition key when this item is placeable; null otherwise.</summary>
        public string BlockDef;
        public Dictionary<string, string> Extras = new Dictionary<string, string>();
    }

    public sealed class TagDefData
    {
        /// <summary>Tag name with or without '#', namespace optional for the default namespace.</summary>
        public string Name;
        public List<string> Add = new List<string>();
        public List<string> Remove = new List<string>();
        public List<string> References = new List<string>();
    }

    public sealed class RecipeIoData
    {
        /// <summary>ItemId or Tag reference ('#...').</summary>
        public string Item;
        public int Count = 1;
        /// <summary>0..1; 1 for deterministic outputs.</summary>
        public float Probability = 1f;
    }

    public sealed class RecipeDefData
    {
        public string Id;
        public string Machine;            // machine def key or '#tag'
        public List<RecipeIoData> Inputs = new List<RecipeIoData>();
        public List<RecipeIoData> Outputs = new List<RecipeIoData>();
        public List<RecipeIoData> Byproducts = new List<RecipeIoData>();
        public float WorkRequired;        // joules
        public bool AllowMixedTagInputs;
        public bool Irreversible;
        public bool AcceptWhileWorking = true;
    }

    public sealed class MachineDefData
    {
        public string Id;
        /// <summary>Torque demand while Working (N*m). 0 for unpowered blocks.</summary>
        public float TauReq;
        public List<string> Tags = new List<string>();
    }

    public sealed class MarketItemDefData
    {
        public string Item;
        public float BasePrice;
        public float SpreadFraction = 0.2f;
        public int MaxSellVolumePerWindow = 200;
        public int MaxBuyVolumePerWindow = 500;
        public int WindowTicks = 60;
    }

    public sealed class UnlockNodeDefData
    {
        public string Id;
        public int Tier = 1;
        public string DepthTag;                              // stateVariable|measurement|process|control|opportunity
        public List<string> Grants = new List<string>();
        public List<UnlockConditionGroupData> ConditionGroups = new List<UnlockConditionGroupData>();
    }

    public sealed class UnlockConditionGroupData
    {
        public List<UnlockConditionData> All = new List<UnlockConditionData>();
    }

    public sealed class UnlockConditionData
    {
        public string Kind;      // metric|task|license|phenomenon|relationship
        public string Subject;
        public string Op;        // gte|lte|eq|first
        public double Value;
        public int WindowTicks;
    }
}

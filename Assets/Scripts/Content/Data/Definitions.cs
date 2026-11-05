using System.Collections.Generic;

namespace Equilibrium.Content
{
    /// <summary>Validated, interned item definition (spec s2.1, item-registry GDD rule 7).</summary>
    public sealed class ItemDef
    {
        public ItemId Id { get; }
        public int MaxStack { get; }
        public string DisplayNameKey { get; }
        public string Icon { get; }
        public bool Tradeable { get; }
        public float EnergyJ { get; }
        public string BlockDef { get; }
        public bool IsUnknown { get; }
        public IReadOnlyDictionary<string, string> Extras { get; }

        public ItemDef(ItemId id, int maxStack, string displayNameKey, string icon, bool tradeable, float energyJ, string blockDef, bool isUnknown, IReadOnlyDictionary<string, string> extras)
        {
            Id = id; MaxStack = maxStack; DisplayNameKey = displayNameKey; Icon = icon; Tradeable = tradeable;
            EnergyJ = energyJ; BlockDef = blockDef; IsUnknown = isUnknown; Extras = extras ?? EmptyExtras;
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyExtras = new Dictionary<string, string>();
    }

    public readonly struct RecipeIo
    {
        public readonly ItemId Item;      // None when Tag is set
        public readonly TagId Tag;        // None when Item is set
        public readonly int Count;
        public readonly float Probability;
        public bool IsTag => !Tag.IsNone;
        public RecipeIo(ItemId item, TagId tag, int count, float probability) { Item = item; Tag = tag; Count = count; Probability = probability; }
    }

    public sealed class RecipeDef
    {
        public string Id { get; }
        public string Machine { get; }
        public TagId MachineTag { get; }
        public IReadOnlyList<RecipeIo> Inputs { get; }
        public IReadOnlyList<RecipeIo> Outputs { get; }
        public IReadOnlyList<RecipeIo> Byproducts { get; }
        public float WorkRequired { get; }
        public bool AllowMixedTagInputs { get; }
        public bool Irreversible { get; }
        public bool AcceptWhileWorking { get; }

        public RecipeDef(string id, string machine, TagId machineTag, IReadOnlyList<RecipeIo> inputs, IReadOnlyList<RecipeIo> outputs, IReadOnlyList<RecipeIo> byproducts,
            float workRequired, bool allowMixedTagInputs, bool irreversible, bool acceptWhileWorking)
        {
            Id = id; Machine = machine; MachineTag = machineTag; Inputs = inputs; Outputs = outputs; Byproducts = byproducts;
            WorkRequired = workRequired; AllowMixedTagInputs = allowMixedTagInputs; Irreversible = irreversible; AcceptWhileWorking = acceptWhileWorking;
        }
    }

    public sealed class MachineDef
    {
        public string Id { get; }
        public float TauReq { get; }
        public IReadOnlyList<TagId> Tags { get; }
        public MachineDef(string id, float tauReq, IReadOnlyList<TagId> tags) { Id = id; TauReq = tauReq; Tags = tags; }
    }

    public sealed class MarketItemDef
    {
        public ItemId Item { get; }
        public float BasePrice { get; }
        public float SpreadFraction { get; }
        public int MaxSellVolumePerWindow { get; }
        public int MaxBuyVolumePerWindow { get; }
        public int WindowTicks { get; }
        public MarketItemDef(ItemId item, float basePrice, float spread, int maxSell, int maxBuy, int windowTicks)
        {
            Item = item; BasePrice = basePrice; SpreadFraction = spread; MaxSellVolumePerWindow = maxSell; MaxBuyVolumePerWindow = maxBuy; WindowTicks = windowTicks;
        }
    }

    public sealed class UnlockNodeDef
    {
        public string Id { get; }
        public int Tier { get; }
        public string DepthTag { get; }
        public IReadOnlyList<string> Grants { get; }
        public IReadOnlyList<UnlockConditionGroupData> ConditionGroups { get; }
        public UnlockNodeDef(string id, int tier, string depthTag, IReadOnlyList<string> grants, IReadOnlyList<UnlockConditionGroupData> groups)
        {
            Id = id; Tier = tier; DepthTag = depthTag; Grants = grants; ConditionGroups = groups;
        }
    }
}

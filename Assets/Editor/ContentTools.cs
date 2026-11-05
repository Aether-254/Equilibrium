using System.IO;
using Equilibrium.Content;
using Equilibrium.Content.Unity;
using UnityEditor;
using UnityEngine;

namespace Equilibrium.Editor
{
    /// <summary>
    /// Editor tooling for the content pipeline (item-registry story 004):
    /// - Generate Tier 1 Assets: materializes <see cref="Tier1Content"/> as ScriptableObjects + a ContentCatalog in Resources.
    /// - Export Reference Addon: writes the catalog to Addons/_reference/ in the JSON addon layout.
    /// - Validate Catalog: runs the shared loader and logs diagnostics.
    /// </summary>
    public static class ContentTools
    {
        private const string AssetRoot = "Assets/Content";
        private const string ResourcesRoot = "Assets/Resources";
        private const string ReferenceAddonRoot = "Addons/_reference";

        [MenuItem("Equilibrium/Content/Generate Tier 1 Assets")]
        public static void GenerateTier1Assets()
        {
            var pack = Tier1Content.Build();
            Directory.CreateDirectory(AssetRoot);
            Directory.CreateDirectory(ResourcesRoot);
            var catalog = LoadOrCreate<ContentCatalog>($"{ResourcesRoot}/{ContentCatalog.ResourcesPath}.asset");
            catalog.Version = pack.Manifest.Version;
            catalog.Items.Clear(); catalog.Tags.Clear(); catalog.Recipes.Clear(); catalog.Machines.Clear(); catalog.Market.Clear(); catalog.Unlocks.Clear();

            foreach (var d in pack.Items)
            {
                var a = LoadOrCreate<ItemDefAsset>($"{AssetRoot}/Items/item_{Slug(d.Id)}.asset");
                a.Id = d.Id; a.MaxStack = d.MaxStack; a.DisplayNameKey = d.DisplayNameKey; a.Tradeable = d.Tradeable; a.EnergyJ = d.EnergyJ; a.BlockDef = d.BlockDef;
                a.Extras.Clear(); foreach (var kv in d.Extras) a.Extras.Add(new ExtraField { Key = kv.Key, Value = kv.Value });
                EditorUtility.SetDirty(a); catalog.Items.Add(a);
            }
            foreach (var d in pack.Tags)
            {
                var a = LoadOrCreate<TagDefAsset>($"{AssetRoot}/Tags/tag_{Slug(d.Name)}.asset");
                a.TagName = d.Name; a.Add = new System.Collections.Generic.List<string>(d.Add); a.Remove = new System.Collections.Generic.List<string>(d.Remove); a.References = new System.Collections.Generic.List<string>(d.References);
                EditorUtility.SetDirty(a); catalog.Tags.Add(a);
            }
            foreach (var d in pack.Recipes)
            {
                var a = LoadOrCreate<RecipeDefAsset>($"{AssetRoot}/Recipes/recipe_{Slug(d.Id)}.asset");
                a.Id = d.Id; a.Machine = d.Machine; a.WorkRequired = d.WorkRequired; a.AllowMixedTagInputs = d.AllowMixedTagInputs; a.Irreversible = d.Irreversible; a.AcceptWhileWorking = d.AcceptWhileWorking;
                a.Inputs.Clear(); foreach (var io in d.Inputs) a.Inputs.Add(new RecipeIoField { Item = io.Item, Count = io.Count, Probability = io.Probability });
                a.Outputs.Clear(); foreach (var io in d.Outputs) a.Outputs.Add(new RecipeIoField { Item = io.Item, Count = io.Count, Probability = io.Probability });
                a.Byproducts.Clear(); foreach (var io in d.Byproducts) a.Byproducts.Add(new RecipeIoField { Item = io.Item, Count = io.Count, Probability = io.Probability });
                EditorUtility.SetDirty(a); catalog.Recipes.Add(a);
            }
            foreach (var d in pack.Machines)
            {
                var a = LoadOrCreate<MachineDefAsset>($"{AssetRoot}/Machines/machine_{Slug(d.Id)}.asset");
                a.Id = d.Id; a.TauReq = d.TauReq; a.Tags = new System.Collections.Generic.List<string>(d.Tags);
                EditorUtility.SetDirty(a); catalog.Machines.Add(a);
            }
            foreach (var d in pack.Market)
            {
                var a = LoadOrCreate<MarketItemDefAsset>($"{AssetRoot}/Market/market_{Slug(d.Item)}.asset");
                a.Item = d.Item; a.BasePrice = d.BasePrice; a.SpreadFraction = d.SpreadFraction; a.MaxSellVolumePerWindow = d.MaxSellVolumePerWindow; a.MaxBuyVolumePerWindow = d.MaxBuyVolumePerWindow; a.WindowTicks = d.WindowTicks;
                EditorUtility.SetDirty(a); catalog.Market.Add(a);
            }
            foreach (var d in pack.Unlocks)
            {
                var a = LoadOrCreate<UnlockNodeDefAsset>($"{AssetRoot}/Unlocks/unlock_{Slug(d.Id)}.asset");
                a.Id = d.Id; a.Tier = d.Tier; a.DepthTag = d.DepthTag; a.Grants = new System.Collections.Generic.List<string>(d.Grants);
                a.ConditionGroups.Clear();
                foreach (var g in d.ConditionGroups)
                {
                    var gf = new UnlockGroupField { All = new System.Collections.Generic.List<UnlockConditionField>() };
                    foreach (var c in g.All) gf.All.Add(new UnlockConditionField { Kind = c.Kind, Subject = c.Subject, Op = c.Op, Value = c.Value, WindowTicks = c.WindowTicks });
                    a.ConditionGroups.Add(gf);
                }
                EditorUtility.SetDirty(a); catalog.Unlocks.Add(a);
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Equilibrium] Generated Tier 1 content: {catalog.Items.Count} items, {catalog.Recipes.Count} recipes -> {ResourcesRoot}/{ContentCatalog.ResourcesPath}.asset");
        }

        [MenuItem("Equilibrium/Content/Export Reference Addon")]
        public static void ExportReferenceAddon()
        {
            var catalog = Resources.Load<ContentCatalog>(ContentCatalog.ResourcesPath);
            if (catalog == null) { Debug.LogError("[Equilibrium] No ContentCatalog in Resources; run Generate Tier 1 Assets first."); return; }
            string root = Path.Combine(Directory.GetCurrentDirectory(), ReferenceAddonRoot);
            JsonContentWriter.WriteTo(root, catalog.Export());
            Debug.Log($"[Equilibrium] Reference addon exported to {root}");
        }

        [MenuItem("Equilibrium/Content/Validate Catalog")]
        public static void ValidateCatalog()
        {
            var catalog = Resources.Load<ContentCatalog>(ContentCatalog.ResourcesPath);
            if (catalog == null) { Debug.LogError("[Equilibrium] No ContentCatalog in Resources."); return; }
            var reg = new ContentRegistry();
            var report = new ContentLoader().Load(reg, catalog.Export(), System.Array.Empty<ContentPack>());
            foreach (var d in report.Diagnostics) { if (d.IsWarning) Debug.LogWarning(d.ToString()); else Debug.LogError(d.ToString()); }
            Debug.Log(report.BaseRejected ? "[Equilibrium] Catalog INVALID" : $"[Equilibrium] Catalog valid: {reg.ItemCount} items, state {reg.State}");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
            return a;
        }

        private static string Slug(string s) => (s ?? "unnamed").Replace('#', '_').Replace(':', '_').Replace('/', '_');
    }
}

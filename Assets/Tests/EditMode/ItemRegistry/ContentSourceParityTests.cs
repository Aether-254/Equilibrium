using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Equilibrium.Content;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.ItemRegistry
{
    /// <summary>Story item-registry-004 (source parity, Tier 1 content, load budget).</summary>
    public class ContentSourceParityTests
    {
        private static JsonContentSource SourceFromFiles(Dictionary<string, string> files, string root = "root")
        {
            string Norm(string p) => p.Replace('\\', '/');
            return new JsonContentSource(root,
                readText: path =>
                {
                    string rel = Norm(path).Substring(Norm(root).Length).TrimStart('/');
                    if (!files.TryGetValue(rel, out var text)) throw new System.IO.FileNotFoundException(path);
                    return text;
                },
                listJsonFiles: dir =>
                {
                    string rel = Norm(dir).Substring(Norm(root).Length).TrimStart('/');
                    return files.Keys.Where(k => k.StartsWith(rel + "/", StringComparison.Ordinal) && k.EndsWith(".json", StringComparison.Ordinal))
                                     .Select(k => root + "/" + k).ToArray();
                });
        }

        [Test]
        public void test_tier1_pack_loads_expected_thirteen_items_one_recipe()
        {
            var (reg, report) = Packs.Load(Tier1Content.Build());
            Assert.IsFalse(report.BaseRejected, string.Join("; ", report.Diagnostics));
            Assert.AreEqual(13, reg.ItemCount);
            Assert.IsTrue(reg.TryGetRecipe("log_to_plank", out var r));
            Assert.AreEqual(1350f, r.WorkRequired);
            Assert.IsTrue(reg.TryGetMachine("saw", out var saw));
            Assert.AreEqual(30f, saw.TauReq);
            Assert.AreEqual(1, reg.RecipesFor(saw).Count());
            Assert.IsTrue(reg.TryGetMarket(reg.Items.Parse("plank"), out var m));
            Assert.AreEqual(10f, m.BasePrice);
            Assert.AreEqual(8000f, reg.Item(reg.Items.Parse("wood")).EnergyJ);
            reg.Tags.TryGet("equilibrium:gear", out var gear);
            Assert.IsTrue(reg.InTag(reg.Items.Parse("gear32"), gear));
        }

        [Test]
        public void test_tier1_json_round_trip_expected_equal_pack()
        {
            var original = Tier1Content.Build();
            var files = JsonContentWriter.ToFiles(original);
            var parsed = SourceFromFiles(files).Load();

            Assert.AreEqual(original.Manifest.Namespace, parsed.Manifest.Namespace);
            Assert.AreEqual(original.Items.Count, parsed.Items.Count);
            for (int i = 0; i < original.Items.Count; i++)
            {
                Assert.AreEqual(original.Items[i].Id, parsed.Items[i].Id);
                Assert.AreEqual(original.Items[i].MaxStack, parsed.Items[i].MaxStack);
                Assert.AreEqual(original.Items[i].EnergyJ, parsed.Items[i].EnergyJ);
                Assert.AreEqual(original.Items[i].BlockDef, parsed.Items[i].BlockDef);
            }
            Assert.AreEqual(original.Tags.Count, parsed.Tags.Count);
            Assert.AreEqual(original.Recipes.Count, parsed.Recipes.Count);
            Assert.AreEqual(original.Recipes[0].WorkRequired, parsed.Recipes[0].WorkRequired);
            Assert.AreEqual(original.Machines.Count, parsed.Machines.Count);
            Assert.AreEqual(original.Market.Count, parsed.Market.Count);
            Assert.AreEqual(original.Unlocks.Count, parsed.Unlocks.Count);
            Assert.AreEqual(original.Unlocks[2].ConditionGroups[0].All.Count, parsed.Unlocks[2].ConditionGroups[0].All.Count);

            var (regA, repA) = Packs.Load(original);
            var (regB, repB) = Packs.Load(parsed);
            Assert.AreEqual(repA.Rejected.Count, repB.Rejected.Count);
            Assert.AreEqual(regA.ItemCount, regB.ItemCount);
        }

        [Test]
        public void test_unknown_item_field_expected_preserved_in_extras()
        {
            var items = JsonContentSource.ParseItems("[{\"Id\":\"wood\",\"MaxStack\":64,\"future\":42,\"color\":\"#ffcc00\"}]");
            Assert.AreEqual("42", items[0].Extras["future"]);
            Assert.AreEqual("#ffcc00", items[0].Extras["color"]);
            var back = JsonContentWriter.ToFiles(new ContentPack { Items = items })["items/items.json"];
            StringAssert.Contains("\"future\"", back);
        }

        [Test]
        public void test_same_defect_via_json_and_in_memory_expected_same_error_code()
        {
            var inMemory = Packs.Base(Packs.Item("wood"), Packs.Item("wood"));
            var (_, repMem) = Packs.Load(inMemory);

            var files = JsonContentWriter.ToFiles(inMemory);
            var (_, repJson) = Packs.Load(SourceFromFiles(files).Load());

            Assert.IsTrue(Packs.Rejected(repMem, "equilibrium", ContentError.DuplicateItemId));
            Assert.IsTrue(Packs.Rejected(repJson, "equilibrium", ContentError.DuplicateItemId));
        }

        [Test]
        public void test_manifest_parse_expected_dependencies()
        {
            var m = JsonContentSource.ParseManifest("{\"Namespace\":\"farmer_addon\",\"Version\":\"1.2.0\",\"DependsOn\":[\"equilibrium\"],\"Modifies\":[]}");
            Assert.AreEqual("farmer_addon", m.Namespace);
            Assert.AreEqual("1.2.0", m.Version);
            CollectionAssert.AreEqual(new[] { "equilibrium" }, m.DependsOn);
        }

        [Test]
        public void test_tier1_load_time_expected_under_200_ms()
        {
            Packs.Load(Tier1Content.Build()); // warm up
            var sw = Stopwatch.StartNew();
            var files = JsonContentWriter.ToFiles(Tier1Content.Build());
            var pack = SourceFromFiles(files).Load();
            var (reg, _) = Packs.Load(pack);
            sw.Stop();
            TestContext.WriteLine($"Tier 1 serialize+parse+load: {sw.Elapsed.TotalMilliseconds:F1} ms");
            Assert.Less(sw.Elapsed.TotalMilliseconds, 200);
            Assert.AreEqual(RegistryState.Ready, reg.State);
        }

        [Test]
        public void test_tier1_unlocks_expected_all_valid()
        {
            var (reg, report) = Packs.Load(Tier1Content.Build());
            Assert.AreEqual(5, reg.Unlocks.Count());
            Assert.IsFalse(report.Diagnostics.Any(d => !d.IsWarning));
        }
    }
}

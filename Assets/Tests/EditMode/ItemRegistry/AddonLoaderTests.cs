using System.Linq;
using Equilibrium.Content;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.ItemRegistry
{
    /// <summary>Story item-registry-003 (TR-item-004, TR-item-005).</summary>
    public class AddonLoaderTests
    {
        [Test]
        public void test_missing_namespace_expected_namespace_missing()
        {
            var addon = new ContentPack();
            var (reg, report) = Packs.Load(Packs.Base(Packs.Item("wood")), addon);
            Assert.IsTrue(report.Rejected.Any(r => r.code == ContentError.NamespaceMissing));
        }

        [Test]
        public void test_reserved_namespace_expected_namespace_reserved()
        {
            var addon = Packs.Addon("equilibrium");
            var (reg, report) = Packs.Load(Packs.Base(Packs.Item("wood")), addon);
            Assert.IsTrue(Packs.Rejected(report, "equilibrium", ContentError.NamespaceReserved));
        }

        [Test]
        public void test_dependency_rejected_expected_dependent_rejected_too()
        {
            var a = Packs.Addon("a"); a.Items.Add(Packs.Item("a:bad", maxStack: 0));
            var b = Packs.Addon("b", "a"); b.Items.Add(Packs.Item("b:ok"));
            var (reg, report) = Packs.Load(Packs.Base(Packs.Item("wood")), a, b);
            Assert.IsTrue(Packs.Rejected(report, "a", ContentError.InvalidMaxStack));
            Assert.IsTrue(Packs.Rejected(report, "b", ContentError.DependencyRejected));
            Assert.IsFalse(reg.NamespaceLoaded("b"));
        }

        [Test]
        public void test_undeclared_cross_namespace_modification_expected_rejected()
        {
            var a = Packs.Addon("a", "equilibrium");
            a.Tags.Add(Packs.Tag("#equilibrium:ingot", add: new[] { "equilibrium:wood" })); // modifies base without declaring
            var (reg, report) = Packs.Load(Packs.Base(Packs.Item("wood")), a);
            Assert.IsTrue(Packs.Rejected(report, "a", ContentError.UndeclaredModification));
        }

        [Test]
        public void test_declared_modification_expected_accepted()
        {
            var a = Packs.Addon("a", "equilibrium"); a.Manifest.Modifies.Add("equilibrium");
            a.Tags.Add(Packs.Tag("#equilibrium:ingot", add: new[] { "equilibrium:wood" }));
            var (reg, report) = Packs.Load(Packs.Base(Packs.Item("wood")), a);
            Assert.That(report.Accepted, Does.Contain("a"));
            reg.Tags.TryGet("equilibrium:ingot", out var t);
            Assert.IsTrue(reg.InTag(reg.Items.Parse("wood"), t));
        }

        [Test]
        public void test_addon_cycle_expected_both_rejected()
        {
            var a = Packs.Addon("a", "b");
            var b = Packs.Addon("b", "a");
            var (reg, report) = Packs.Load(Packs.Base(Packs.Item("wood")), a, b);
            Assert.IsTrue(Packs.Rejected(report, "a", ContentError.AddonCycle));
            Assert.IsTrue(Packs.Rejected(report, "b", ContentError.AddonCycle));
        }

        [Test]
        public void test_topological_order_expected_remove_applied_after_add()
        {
            var basePack = Packs.Base(Packs.Item("x"));
            basePack.Tags.Add(Packs.Tag("#t"));
            var adder = Packs.Addon("adder", "equilibrium"); adder.Manifest.Modifies.Add("equilibrium");
            adder.Tags.Add(Packs.Tag("#equilibrium:t", add: new[] { "equilibrium:x" }));
            var remover = Packs.Addon("remover", "adder"); remover.Manifest.Modifies.Add("equilibrium");
            remover.Tags.Add(Packs.Tag("#equilibrium:t", remove: new[] { "equilibrium:x" }));
            // pass remover first to prove ordering is by graph, not by argument order
            var (reg, report) = Packs.Load(basePack, remover, adder);
            Assert.AreEqual(new[] { "equilibrium", "adder", "remover" }, report.Accepted.ToArray());
            reg.Tags.TryGet("equilibrium:t", out var t);
            Assert.IsFalse(reg.InTag(reg.Items.Parse("x"), t));
        }

        [Test]
        public void test_unknown_namespace_in_save_expected_unknown_item_with_original_id()
        {
            var (reg, _) = Packs.Load(Packs.Base(Packs.Item("wood")));
            var def = reg.ResolveOrUnknown("farmer_addon:seed");
            Assert.IsTrue(def.IsUnknown);
            Assert.AreEqual("farmer_addon:seed", def.Id.Canonical);
            Assert.AreEqual(ContentRegistry.DefaultMaxStack, def.MaxStack);
        }

        [Test]
        public void test_unknown_item_restored_when_addon_loads_expected_real_def()
        {
            var farmer = Packs.Addon("farmer_addon"); farmer.Items.Add(Packs.Item("farmer_addon:seed", 99));
            var (reg, report) = Packs.Load(Packs.Base(Packs.Item("wood")), farmer);
            Assert.That(report.Accepted, Does.Contain("farmer_addon"));
            var def = reg.ResolveOrUnknown("farmer_addon:seed");
            Assert.IsFalse(def.IsUnknown);
            Assert.AreEqual(99, def.MaxStack);
        }

        [Test]
        public void test_registry_state_expected_ready_after_load()
        {
            var (reg, _) = Packs.Load(Packs.Base(Packs.Item("wood")));
            Assert.AreEqual(RegistryState.Ready, reg.State);
        }

        [Test]
        public void test_base_invalid_expected_fatal_state()
        {
            var (reg, report) = Packs.Load(Packs.Base(Packs.Item("Bad Id")));
            Assert.IsTrue(report.BaseRejected);
            Assert.AreEqual(RegistryState.Fatal, reg.State);
        }

        [Test]
        public void test_unlock_validation_expected_depth_and_non_task_path_errors()
        {
            var noDepth = Packs.Addon("u1"); noDepth.Unlocks.Add(new UnlockNodeDefData { Id = "n", DepthTag = null });
            var taskOnly = Packs.Addon("u2"); taskOnly.Unlocks.Add(new UnlockNodeDefData
            {
                Id = "n", DepthTag = "measurement",
                ConditionGroups = { new UnlockConditionGroupData { All = { new UnlockConditionData { Kind = "task", Subject = "t1" } } } }
            });
            var forbidden = Packs.Addon("u3"); forbidden.Unlocks.Add(new UnlockNodeDefData { Id = "n", DepthTag = "control", Grants = { "multiplier:2x" } });
            var (reg, report) = Packs.Load(Packs.Base(Packs.Item("wood")), noDepth, taskOnly, forbidden);
            Assert.IsTrue(Packs.Rejected(report, "u1", ContentError.UnlockWithoutDepth));
            Assert.IsTrue(Packs.Rejected(report, "u2", ContentError.NoNonTaskPath));
            Assert.IsTrue(Packs.Rejected(report, "u3", ContentError.ForbiddenRewardType));
        }
    }
}

using System.Linq;
using Equilibrium.Content;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.ItemRegistry
{
    /// <summary>Story item-registry-002 (TR-item-002, TR-item-003).</summary>
    public class TagGraphTests
    {
        [Test]
        public void test_duplicate_item_within_pack_expected_duplicate_item_id_rejection()
        {
            var basePack = Packs.Base(Packs.Item("wood"), Packs.Item("equilibrium:wood"));
            var (reg, report) = Packs.Load(basePack);
            Assert.IsTrue(report.BaseRejected);
            Assert.IsTrue(Packs.Rejected(report, "equilibrium", ContentError.DuplicateItemId));
        }

        [Test]
        public void test_duplicate_item_across_packs_expected_second_pack_rejected_first_wins()
        {
            var basePack = Packs.Base(Packs.Item("wood"));
            var a = Packs.Addon("alpha"); a.Items.Add(Packs.Item("alpha:gear", 10));
            var b = Packs.Addon("beta"); b.Items.Add(Packs.Item("beta:gear", 20)); b.Items.Add(Packs.Item("beta:gear", 30));
            var (reg, report) = Packs.Load(basePack, a, b);
            Assert.That(report.Accepted, Does.Contain("alpha"));
            Assert.IsTrue(Packs.Rejected(report, "beta", ContentError.DuplicateItemId));
            Assert.IsFalse(reg.NamespaceLoaded("beta"));
            Assert.AreEqual(10, reg.Item(reg.Items.Parse("alpha:gear")).MaxStack);
        }

        [Test]
        public void test_child_tag_member_expected_in_parent()
        {
            var basePack = Packs.Base(Packs.Item("iron_ingot"), Packs.Item("copper_ingot"));
            basePack.Tags.Add(Packs.Tag("#ingot"));
            basePack.Tags.Add(Packs.Tag("#ingot/iron", add: new[] { "iron_ingot" }));
            var (reg, _) = Packs.Load(basePack);
            var iron = reg.Items.Parse("iron_ingot");
            Assert.IsTrue(reg.Tags.TryGet("equilibrium:ingot", out var ingot));
            Assert.IsTrue(reg.Tags.TryGet("equilibrium:ingot/iron", out var ingotIron));
            Assert.IsTrue(reg.InTag(iron, ingot));
            Assert.IsTrue(reg.InTag(iron, ingotIron));
            Assert.IsFalse(reg.InTag(reg.Items.Parse("copper_ingot"), ingot));
        }

        [Test]
        public void test_remove_from_parent_expected_removed_from_child_too()
        {
            var basePack = Packs.Base(Packs.Item("iron_ingot"));
            basePack.Tags.Add(Packs.Tag("#ingot/iron", add: new[] { "iron_ingot" }));
            var addon = Packs.Addon("purist", "equilibrium");
            addon.Manifest.Modifies.Add("equilibrium");
            addon.Tags.Add(Packs.Tag("#equilibrium:ingot", remove: new[] { "equilibrium:iron_ingot" }));
            var (reg, report) = Packs.Load(basePack, addon);
            Assert.That(report.Accepted, Does.Contain("purist"));
            var iron = reg.Items.Parse("iron_ingot");
            reg.Tags.TryGet("equilibrium:ingot", out var ingot);
            reg.Tags.TryGet("equilibrium:ingot/iron", out var ingotIron);
            Assert.IsFalse(reg.InTag(iron, ingot));
            Assert.IsFalse(reg.InTag(iron, ingotIron), "removal from parent cascades to child (spec s2A.5)");
        }

        [Test]
        public void test_remove_beats_add_expected_not_member()
        {
            var basePack = Packs.Base(Packs.Item("x"));
            basePack.Tags.Add(Packs.Tag("#t"));
            var adder = Packs.Addon("adder", "equilibrium"); adder.Manifest.Modifies.Add("equilibrium");
            adder.Tags.Add(Packs.Tag("#equilibrium:t", add: new[] { "equilibrium:x" }));
            var remover = Packs.Addon("remover", "equilibrium"); remover.Manifest.Modifies.Add("equilibrium");
            remover.Tags.Add(Packs.Tag("#equilibrium:t", remove: new[] { "equilibrium:x" }));
            var (reg, _) = Packs.Load(basePack, adder, remover);
            reg.Tags.TryGet("equilibrium:t", out var t);
            Assert.IsFalse(reg.InTag(reg.Items.Parse("x"), t));
        }

        [Test]
        public void test_reference_cycle_expected_tag_cycle_and_empty_membership()
        {
            var basePack = Packs.Base(Packs.Item("x"));
            basePack.Tags.Add(Packs.Tag("#a", add: new[] { "x" }, refs: new[] { "#b" }));
            basePack.Tags.Add(Packs.Tag("#b", refs: new[] { "#a" }));
            var (reg, report) = Packs.Load(basePack);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Error == ContentError.TagCycle));
            reg.Tags.TryGet("equilibrium:a", out var a);
            reg.Tags.TryGet("equilibrium:b", out var b);
            Assert.IsFalse(reg.InTag(reg.Items.Parse("x"), a));
            Assert.IsFalse(reg.InTag(reg.Items.Parse("x"), b));
        }

        [Test]
        public void test_shorthand_tag_expected_default_namespace()
        {
            Assert.IsTrue(TagGraph.TryCanonicalize("#ingot", out var c, out _));
            Assert.AreEqual("equilibrium:ingot", c);
            Assert.IsTrue(TagGraph.TryCanonicalize("ingot/iron", out var c2, out _));
            Assert.AreEqual("equilibrium:ingot/iron", c2);
        }

        [Test]
        public void test_max_stack_zero_expected_invalid_max_stack()
        {
            var (reg, report) = Packs.Load(Packs.Base(Packs.Item("bad", maxStack: 0)));
            Assert.IsTrue(Packs.Rejected(report, "equilibrium", ContentError.InvalidMaxStack));
        }

        [Test]
        public void test_missing_display_name_expected_warning_and_fallback()
        {
            var item = Packs.Item("wood"); item.DisplayNameKey = null;
            var (reg, report) = Packs.Load(Packs.Base(item));
            Assert.IsFalse(report.BaseRejected);
            Assert.IsTrue(report.Diagnostics.Any(d => d.IsWarning && d.Error == ContentError.MissingDisplayName));
            Assert.AreEqual("equilibrium:wood", reg.Item(reg.Items.Parse("wood")).DisplayNameKey);
        }

        [Test]
        public void test_reference_expands_members_expected_union()
        {
            var basePack = Packs.Base(Packs.Item("a1"), Packs.Item("b1"));
            basePack.Tags.Add(Packs.Tag("#a", add: new[] { "a1" }));
            basePack.Tags.Add(Packs.Tag("#b", add: new[] { "b1" }, refs: new[] { "#a" }));
            var (reg, _) = Packs.Load(basePack);
            reg.Tags.TryGet("equilibrium:b", out var b);
            CollectionAssert.AreEquivalent(new[] { "equilibrium:a1", "equilibrium:b1" }, reg.Tags.Members(b).Select(i => i.Canonical));
        }

        [Test]
        public void test_depth_seventeen_expected_depth_exceeded_diagnostic()
        {
            var basePack = Packs.Base(Packs.Item("x"));
            string deep = "#d" + string.Concat(Enumerable.Repeat("/s", 16));
            basePack.Tags.Add(Packs.Tag(deep, add: new[] { "x" }));
            var (reg, report) = Packs.Load(basePack);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Error == ContentError.TagDepthExceeded));
        }
    }
}

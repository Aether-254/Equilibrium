using Equilibrium.Content;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.ItemRegistry
{
    /// <summary>Story item-registry-001 (TR-item-001).</summary>
    public class ItemIdTests
    {
        [Test]
        public void test_default_namespace_omitted_expected_canonical_equilibrium_wood()
        {
            var table = new ItemIdTable();
            Assert.IsTrue(table.TryParse("wood", out var id, out _));
            Assert.AreEqual("equilibrium:wood", id.Canonical);
            Assert.AreEqual("equilibrium", id.Namespace);
            Assert.AreEqual("wood", id.Path);
        }

        [TestCase("Iron_Ingot:foo")]
        [TestCase("a b:c")]
        [TestCase("a:")]
        [TestCase(":c")]
        [TestCase("a:/c")]
        [TestCase("a//b:c")]
        [TestCase("a:b//c")]
        [TestCase("a:b/")]
        [TestCase("")]
        [TestCase(null)]
        public void test_invalid_inputs_expected_invalid_item_id(string raw)
        {
            var table = new ItemIdTable();
            Assert.IsFalse(table.TryParse(raw, out _, out var err));
            Assert.AreEqual(ContentError.InvalidItemId, err);
        }

        [Test]
        public void test_non_default_namespace_expected_unchanged()
        {
            var table = new ItemIdTable();
            Assert.IsTrue(table.TryParse("farmer_addon:seed", out var id, out _));
            Assert.AreEqual("farmer_addon:seed", id.Canonical);
        }

        [Test]
        public void test_same_canonical_expected_same_handle_and_equality()
        {
            var table = new ItemIdTable();
            var a = table.Parse("wood");
            var b = table.Parse("equilibrium:wood");
            var c = table.Parse("stone");
            Assert.AreEqual(a.Handle, b.Handle);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a.Handle, c.Handle);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void test_ten_thousand_ids_expected_no_collisions()
        {
            var table = new ItemIdTable();
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 10000; i++)
            {
                var id = table.Parse($"ns{i % 7}:item_{i}");
                Assert.IsTrue(seen.Add(id.Handle), "handle collision");
            }
            Assert.AreEqual(10000, table.Count);
        }

        [Test]
        public void test_path_with_slash_expected_valid()
        {
            var table = new ItemIdTable();
            Assert.IsTrue(table.TryParse("equilibrium:ingot/iron", out var id, out _));
            Assert.AreEqual("ingot/iron", id.Path);
        }
    }
}

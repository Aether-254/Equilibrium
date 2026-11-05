using System.Linq;
using Equilibrium.Content;
using Equilibrium.Content.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Equilibrium.Tests.PlayMode.ItemRegistry
{
    /// <summary>Story item-registry-004 Unity-side: catalog export parity and Resources load.</summary>
    public class ContentCatalogTests
    {
        [Test]
        public void test_catalog_in_resources_expected_loads_and_matches_tier1()
        {
            var catalog = Resources.Load<ContentCatalog>(ContentCatalog.ResourcesPath);
            if (catalog == null) Assert.Ignore("Run Equilibrium/Content/Generate Tier 1 Assets first.");

            var fromAssets = catalog.Export();
            var fromCode = Tier1Content.Build();
            Assert.AreEqual(fromCode.Items.Count, fromAssets.Items.Count);
            CollectionAssert.AreEquivalent(fromCode.Items.Select(i => i.Id), fromAssets.Items.Select(i => i.Id));
            Assert.AreEqual(fromCode.Recipes.Count, fromAssets.Recipes.Count);

            var reg = new ContentRegistry();
            var report = new ContentLoader().Load(reg, fromAssets, System.Array.Empty<ContentPack>());
            Assert.IsFalse(report.BaseRejected, string.Join("; ", report.Diagnostics));
            Assert.AreEqual(13, reg.ItemCount);
        }
    }
}

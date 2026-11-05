using System.Collections.Generic;
using Equilibrium.Content;

namespace Equilibrium.Tests.EditMode.ItemRegistry
{
    /// <summary>In-memory pack builder for tests; mirrors what SO export and JSON parse produce.</summary>
    public static class Packs
    {
        public static ContentPack Base(params ItemDefData[] items)
        {
            var p = new ContentPack();
            p.Manifest.Namespace = ItemId.DefaultNamespace;
            p.Manifest.IsBase = true;
            p.Items.AddRange(items);
            return p;
        }

        public static ContentPack Addon(string ns, params string[] dependsOn)
        {
            var p = new ContentPack();
            p.Manifest.Namespace = ns;
            p.Manifest.DependsOn.AddRange(dependsOn);
            return p;
        }

        public static ItemDefData Item(string id, int maxStack = 64, string display = null)
            => new ItemDefData { Id = id, MaxStack = maxStack, DisplayNameKey = display ?? id };

        public static TagDefData Tag(string name, string[] add = null, string[] remove = null, string[] refs = null)
        {
            var t = new TagDefData { Name = name };
            if (add != null) t.Add.AddRange(add);
            if (remove != null) t.Remove.AddRange(remove);
            if (refs != null) t.References.AddRange(refs);
            return t;
        }

        public static (ContentRegistry reg, LoadReport report) Load(ContentPack basePack, params ContentPack[] addons)
        {
            var reg = new ContentRegistry();
            var report = new ContentLoader().Load(reg, basePack, addons);
            return (reg, report);
        }

        public static bool Rejected(LoadReport r, string ns, ContentError code)
        {
            foreach (var (n, c) in r.Rejected) if (n == ns && c == code) return true;
            return false;
        }
    }
}

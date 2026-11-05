using System;
using System.Collections.Generic;

namespace Equilibrium.Content
{
    /// <summary>Produces a raw <see cref="ContentPack"/> from some storage (ScriptableObjects, JSON, in-memory).</summary>
    public interface IContentSource
    {
        ContentPack Load();
    }

    public sealed class LoadReport
    {
        public readonly List<string> Accepted = new List<string>();
        public readonly List<(string ns, ContentError code)> Rejected = new List<(string, ContentError)>();
        public readonly List<ContentDiagnostic> Diagnostics = new List<ContentDiagnostic>();
        public bool BaseRejected;
    }

    /// <summary>
    /// ADR-0003 loader: orders packs by the addon dependency/modification graph (spec s2B.2),
    /// validates each with the shared rules, interns ids, and fills one <see cref="ContentRegistry"/>.
    /// Rejection codes: NamespaceMissing, NamespaceReserved, AddonCycle, DependencyRejected,
    /// UndeclaredModification, DuplicateItemId, TagCycle, InvalidItemId, UnlockWithoutDepth, NoNonTaskPath, ForbiddenRewardType.
    /// </summary>
    public sealed class ContentLoader
    {
        public LoadReport Load(ContentRegistry registry, ContentPack basePack, IEnumerable<ContentPack> addons)
        {
            var report = new LoadReport();
            registry.State = RegistryState.LoadingBase;

            basePack.Manifest.IsBase = true;
            basePack.Manifest.Namespace = ItemId.DefaultNamespace;
            var basePass = new PackPass(registry, basePack, report);
            if (!basePass.ValidateAndStage())
            {
                report.BaseRejected = true;
                registry.State = RegistryState.Fatal;
                return report;
            }
            basePass.Commit();
            report.Accepted.Add(ItemId.DefaultNamespace);

            registry.State = RegistryState.LoadingAddons;
            var order = OrderAddons(addons, report, registry);
            var rejected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (ns, code) in report.Rejected) rejected.Add(ns);

            foreach (var pack in order)
            {
                string ns = pack.Manifest.Namespace;
                bool blocked = false;
                foreach (var dep in pack.Manifest.DependsOn)
                    if (rejected.Contains(dep) || !registry.NamespaceLoaded(dep)) { blocked = true; break; }
                if (!blocked)
                    foreach (var mod in pack.Manifest.Modifies)
                        if (rejected.Contains(mod) || !registry.NamespaceLoaded(mod)) { blocked = true; break; }
                if (blocked)
                {
                    report.Rejected.Add((ns, ContentError.DependencyRejected));
                    rejected.Add(ns);
                    continue;
                }
                var pass = new PackPass(registry, pack, report);
                if (!pass.ValidateAndStage())
                {
                    rejected.Add(ns);
                    continue;
                }
                pass.Commit();
                report.Accepted.Add(ns);
            }

            foreach (var d in registry.Tags.Build())
            {
                report.Diagnostics.Add(d);
                if (!d.IsWarning && d.Error == ContentError.TagCycle) report.Rejected.Add((d.Namespace, ContentError.TagCycle));
            }
            registry.State = RegistryState.Ready;
            return report;
        }

        private static List<ContentPack> OrderAddons(IEnumerable<ContentPack> addons, LoadReport report, ContentRegistry registry)
        {
            var packs = new List<ContentPack>();
            var byNs = new Dictionary<string, ContentPack>(StringComparer.Ordinal);
            foreach (var p in addons)
            {
                string ns = p.Manifest?.Namespace;
                if (string.IsNullOrEmpty(ns)) { report.Rejected.Add(("<unnamed>", ContentError.NamespaceMissing)); continue; }
                if (ns == ItemId.DefaultNamespace) { report.Rejected.Add((ns, ContentError.NamespaceReserved)); continue; }
                if (byNs.ContainsKey(ns)) { report.Rejected.Add((ns, ContentError.DuplicateItemId)); continue; }
                byNs[ns] = p;
                packs.Add(p);
            }

            // Edges: dependency/modified -> dependent. Missing targets are handled at load as DependencyRejected.
            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var p in packs) { indegree[p.Manifest.Namespace] = 0; dependents[p.Manifest.Namespace] = new List<string>(); }
            foreach (var p in packs)
            {
                string ns = p.Manifest.Namespace;
                foreach (var target in Targets(p.Manifest))
                {
                    if (!byNs.ContainsKey(target)) continue; // base or absent: no ordering edge needed
                    dependents[target].Add(ns);
                    indegree[ns]++;
                }
            }
            var queue = new Queue<string>();
            foreach (var p in packs) if (indegree[p.Manifest.Namespace] == 0) queue.Enqueue(p.Manifest.Namespace);
            var order = new List<ContentPack>();
            while (queue.Count > 0)
            {
                string v = queue.Dequeue();
                order.Add(byNs[v]);
                foreach (var d in dependents[v]) if (--indegree[d] == 0) queue.Enqueue(d);
            }
            foreach (var p in packs)
                if (indegree[p.Manifest.Namespace] > 0) report.Rejected.Add((p.Manifest.Namespace, ContentError.AddonCycle));
            return order;
        }

        private static IEnumerable<string> Targets(AddonManifest m)
        {
            foreach (var d in m.DependsOn) yield return d;
            foreach (var d in m.Modifies) yield return d;
        }
    }
}

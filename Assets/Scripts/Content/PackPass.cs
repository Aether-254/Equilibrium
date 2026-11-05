using System;
using System.Collections.Generic;

namespace Equilibrium.Content
{
    /// <summary>
    /// Validates one pack against the shared rules and stages its records; <see cref="Commit"/>
    /// writes them into the registry. Used identically for the base pack and every addon, so the
    /// ScriptableObject and JSON paths produce the same error codes (ADR-0003).
    /// </summary>
    internal sealed class PackPass
    {
        private static readonly HashSet<string> DepthTags = new HashSet<string>(StringComparer.Ordinal)
            { "stateVariable", "measurement", "process", "control", "opportunity" };
        private static readonly HashSet<string> ForbiddenRewardKinds = new HashSet<string>(StringComparer.Ordinal)
            { "multiplier", "efficiency_bonus", "currency_repeatable" };

        private readonly ContentRegistry _reg;
        private readonly ContentPack _pack;
        private readonly LoadReport _report;
        private readonly string _ns;

        private readonly List<ItemDef> _items = new List<ItemDef>();
        private readonly List<(TagId tag, ItemId item, bool remove)> _tagEdits = new List<(TagId, ItemId, bool)>();
        private readonly List<(TagId tag, TagId reference)> _tagRefs = new List<(TagId, TagId)>();
        private readonly List<RecipeDef> _recipes = new List<RecipeDef>();
        private readonly List<MachineDef> _machines = new List<MachineDef>();
        private readonly List<MarketItemDef> _market = new List<MarketItemDef>();
        private readonly List<UnlockNodeDef> _unlocks = new List<UnlockNodeDef>();
        private bool _failed;

        public PackPass(ContentRegistry reg, ContentPack pack, LoadReport report)
        {
            _reg = reg; _pack = pack; _report = report; _ns = pack.Manifest.Namespace;
        }

        private void Error(ContentError e, string subject)
        {
            _report.Diagnostics.Add(new ContentDiagnostic(e, _ns, subject));
            if (!_failed) _report.Rejected.Add((_ns, e));
            _failed = true;
        }

        private void Warn(ContentError e, string subject) => _report.Diagnostics.Add(new ContentDiagnostic(e, _ns, subject, isWarning: true));

        private bool OwnsOrMayModify(string targetNs)
        {
            if (targetNs == _ns) return true;
            if (_pack.Manifest.IsBase) return false;
            return _pack.Manifest.Modifies.Contains(targetNs);
        }

        public bool ValidateAndStage()
        {
            var seenInPack = new HashSet<string>(StringComparer.Ordinal);

            foreach (var d in _pack.Items)
            {
                if (!ItemId.TryCanonicalize(d.Id, out var canonical, out var err)) { Error(err, d.Id ?? "<null>"); continue; }
                string itemNs = canonical.Substring(0, canonical.IndexOf(':'));
                if (itemNs != _ns) { Error(ContentError.UndeclaredModification, canonical); continue; }
                if (!seenInPack.Add(canonical)) { Error(ContentError.DuplicateItemId, canonical); continue; }
                if (_reg.Items.TryGet(canonical, out var existing) && _reg.TryGetItem(existing, out var existingDef) && !existingDef.IsUnknown)
                { Error(ContentError.DuplicateItemId, canonical); continue; }
                if (d.MaxStack < 1) { Error(ContentError.InvalidMaxStack, canonical); continue; }
                string display = d.DisplayNameKey;
                if (string.IsNullOrEmpty(display)) { Warn(ContentError.MissingDisplayName, canonical); display = canonical; }
                var id = _reg.Items.Intern(canonical);
                _items.Add(new ItemDef(id, d.MaxStack, display, d.Icon, d.Tradeable, d.EnergyJ, d.BlockDef, false, d.Extras));
            }

            foreach (var t in _pack.Tags)
            {
                if (!TagGraph.TryCanonicalize(t.Name, out var tagCanonical, out var terr)) { Error(terr, t.Name ?? "<null>"); continue; }
                string tagNs = tagCanonical.Substring(0, tagCanonical.IndexOf(':'));
                if (tagNs == "common" && _pack.Manifest.IsBase) { Error(ContentError.InvalidTagName, "#" + tagCanonical); continue; }
                if (!OwnsOrMayModify(tagNs) && tagNs != "common") { Error(ContentError.UndeclaredModification, "#" + tagCanonical); continue; }
                string path = tagCanonical.Substring(tagCanonical.IndexOf(':') + 1);
                if (path.EndsWith("s", StringComparison.Ordinal) && !path.EndsWith("ss", StringComparison.Ordinal))
                    Warn(ContentError.InvalidTagName, "#" + tagCanonical + " (plural form)");
                var tag = _reg.Tags.Intern(tagCanonical, _ns);
                foreach (var a in t.Add)
                {
                    if (!ItemId.TryCanonicalize(a, out var ac, out var aerr)) { Error(aerr, a); continue; }
                    _tagEdits.Add((tag, _reg.Items.Intern(ac), false));
                }
                foreach (var r in t.Remove)
                {
                    if (!ItemId.TryCanonicalize(r, out var rc, out var rerr)) { Error(rerr, r); continue; }
                    _tagEdits.Add((tag, _reg.Items.Intern(rc), true));
                }
                foreach (var refName in t.References)
                {
                    if (!TagGraph.TryCanonicalize(refName, out var rcanon, out var rerr)) { Error(rerr, refName); continue; }
                    _tagRefs.Add((tag, _reg.Tags.Intern(rcanon, _ns)));
                }
            }

            foreach (var m in _pack.Machines)
            {
                if (string.IsNullOrEmpty(m.Id)) { Error(ContentError.RecipeInvalid, "<machine without id>"); continue; }
                var tags = new List<TagId>();
                foreach (var tn in m.Tags)
                {
                    if (!TagGraph.TryCanonicalize(tn, out var tc, out var terr)) { Error(terr, tn); continue; }
                    tags.Add(_reg.Tags.Intern(tc, _ns));
                }
                _machines.Add(new MachineDef(m.Id, Math.Max(0f, m.TauReq), tags));
            }

            foreach (var r in _pack.Recipes)
            {
                if (string.IsNullOrEmpty(r.Id) || r.Inputs.Count == 0 || r.Outputs.Count == 0 || r.WorkRequired < 0)
                { Error(ContentError.RecipeInvalid, r.Id ?? "<recipe without id>"); continue; }
                if (r.WorkRequired == 0) Warn(ContentError.RecipeInvalid, r.Id + " (workRequired 0)");
                string machine = null; TagId machineTag = TagId.None;
                if (!string.IsNullOrEmpty(r.Machine))
                {
                    if (r.Machine[0] == '#')
                    {
                        if (!TagGraph.TryCanonicalize(r.Machine, out var mc, out var merr)) { Error(merr, r.Machine); continue; }
                        machineTag = _reg.Tags.Intern(mc, _ns);
                    }
                    else machine = r.Machine;
                }
                if (!TryIo(r.Inputs, out var inputs) || !TryIo(r.Outputs, out var outputs) || !TryIo(r.Byproducts, out var byproducts))
                { Error(ContentError.RecipeInvalid, r.Id); continue; }
                _recipes.Add(new RecipeDef(r.Id, machine, machineTag, inputs, outputs, byproducts, r.WorkRequired, r.AllowMixedTagInputs, r.Irreversible, r.AcceptWhileWorking));
            }

            foreach (var m in _pack.Market)
            {
                if (!ItemId.TryCanonicalize(m.Item, out var mc, out var merr)) { Error(merr, m.Item ?? "<null>"); continue; }
                if (m.BasePrice <= 0 || m.SpreadFraction < 0 || m.SpreadFraction > 0.5f || m.MaxSellVolumePerWindow <= 0 || m.MaxBuyVolumePerWindow <= 0 || m.WindowTicks <= 0)
                { Error(ContentError.MarketEntryInvalid, mc); continue; }
                _market.Add(new MarketItemDef(_reg.Items.Intern(mc), m.BasePrice, m.SpreadFraction, m.MaxSellVolumePerWindow, m.MaxBuyVolumePerWindow, m.WindowTicks));
            }

            foreach (var u in _pack.Unlocks)
            {
                if (string.IsNullOrEmpty(u.Id)) { Error(ContentError.UnlockWithoutDepth, "<unlock without id>"); continue; }
                if (string.IsNullOrEmpty(u.DepthTag) || !DepthTags.Contains(u.DepthTag)) { Error(ContentError.UnlockWithoutDepth, u.Id); continue; }
                bool anyNonTask = false;
                foreach (var g in u.ConditionGroups)
                {
                    bool groupHasTask = false;
                    foreach (var c in g.All) if (c.Kind == "task") groupHasTask = true;
                    if (!groupHasTask) anyNonTask = true;
                }
                if (u.ConditionGroups.Count > 0 && !anyNonTask) { Error(ContentError.NoNonTaskPath, u.Id); continue; }
                bool forbidden = false;
                foreach (var g in u.Grants) if (ForbiddenRewardKinds.Contains(g.Split(':')[0])) forbidden = true;
                if (forbidden) { Error(ContentError.ForbiddenRewardType, u.Id); continue; }
                _unlocks.Add(new UnlockNodeDef(u.Id, u.Tier, u.DepthTag, u.Grants, u.ConditionGroups));
            }

            return !_failed;
        }

        private bool TryIo(List<RecipeIoData> src, out List<RecipeIo> dst)
        {
            dst = new List<RecipeIo>(src.Count);
            foreach (var io in src)
            {
                if (io.Count <= 0 || string.IsNullOrEmpty(io.Item)) return false;
                float p = io.Probability <= 0 ? 1f : Math.Min(1f, io.Probability);
                if (io.Item[0] == '#')
                {
                    if (!TagGraph.TryCanonicalize(io.Item, out var tc, out _)) return false;
                    dst.Add(new RecipeIo(ItemId.None, _reg.Tags.Intern(tc, _ns), io.Count, p));
                }
                else
                {
                    if (!ItemId.TryCanonicalize(io.Item, out var ic, out _)) return false;
                    dst.Add(new RecipeIo(_reg.Items.Intern(ic), TagId.None, io.Count, p));
                }
            }
            return true;
        }

        public void Commit()
        {
            foreach (var d in _items) _reg.TryAddItem(d);
            foreach (var (tag, item, remove) in _tagEdits)
            {
                if (remove) _reg.Tags.RemoveMember(tag, item); else _reg.Tags.AddMember(tag, item);
            }
            foreach (var (tag, reference) in _tagRefs) _reg.Tags.AddReference(tag, reference);
            foreach (var m in _machines) _reg.AddMachine(m);
            foreach (var r in _recipes) _reg.AddRecipe(r);
            foreach (var m in _market) _reg.AddMarket(m);
            foreach (var u in _unlocks) _reg.AddUnlock(u);
            _reg.MarkNamespaceLoaded(_ns);
        }
    }
}

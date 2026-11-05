using System.Collections.Generic;

namespace Equilibrium.Content
{
    /// <summary>
    /// Tier 1 base content as an in-memory pack (item-registry story 004, mechanical-power /
    /// machine-processing / market-interface tuning tables). Authoritative values live in the
    /// GDD tuning knobs and are provisional (registry entities marked tuning: true). This pack is
    /// what the ScriptableObject export must match and what the reference addon JSON is generated from.
    /// </summary>
    public static class Tier1Content
    {
        public static ContentPack Build()
        {
            var p = new ContentPack();
            p.Manifest.Namespace = ItemId.DefaultNamespace;
            p.Manifest.Version = "0.1.0";
            p.Manifest.IsBase = true;

            p.Items.AddRange(new[]
            {
                Item("log", 64, "item.log", tradeable: true),
                Item("plank", 64, "item.plank", tradeable: true),
                Item("wood", 64, "item.wood", tradeable: true, energyJ: 8000f),
                Item("engine", 1, "block.engine", blockDef: "engine"),
                Item("shaft", 16, "block.shaft", blockDef: "shaft"),
                Item("gear8", 16, "block.gear8", blockDef: "gear8"),
                Item("gear16", 16, "block.gear16", blockDef: "gear16"),
                Item("gear32", 16, "block.gear32", blockDef: "gear32"),
                Item("saw", 1, "block.saw", blockDef: "saw"),
                Item("chest", 4, "block.chest", blockDef: "chest"),
                Item("belt", 64, "block.belt", blockDef: "belt"),
                Item("accessor", 16, "block.accessor", blockDef: "accessor"),
                Item("blocker", 16, "block.blocker", blockDef: "blocker"),
            });

            p.Tags.Add(new TagDefData { Name = "#fuel", Add = { "wood" } });
            p.Tags.Add(new TagDefData { Name = "#gear", Add = { "gear8", "gear16", "gear32" } });
            p.Tags.Add(new TagDefData { Name = "#machine", Add = { "saw" } });
            p.Tags.Add(new TagDefData { Name = "#machine/saw", Add = { "saw" } });

            p.Machines.Add(new MachineDefData { Id = "saw", TauReq = 30f, Tags = { "#machine/saw" } });
            p.Machines.Add(new MachineDefData { Id = "engine", TauReq = 0f });

            // Mechanical values are content, not a fixed processing speed in gameplay code.
            p.Items.Find(i => i.Id == "engine").Extras["omega_src"] = "10";
            p.Items.Find(i => i.Id == "engine").Extras["tau_max"] = "40";
            p.Items.Find(i => i.Id == "engine").Extras["eta_engine"] = "0.25";
            p.Items.Find(i => i.Id == "shaft").Extras["eta_shaft"] = "0.99";

            p.Recipes.Add(new RecipeDefData
            {
                Id = "log_to_plank",
                Machine = "saw",
                Inputs = { new RecipeIoData { Item = "log", Count = 1 } },
                Outputs = { new RecipeIoData { Item = "plank", Count = 2 } },
                WorkRequired = 1350f,
            });

            p.Market.AddRange(new[]
            {
                new MarketItemDefData { Item = "plank", BasePrice = 10f, SpreadFraction = 0.2f, MaxSellVolumePerWindow = 200, MaxBuyVolumePerWindow = 500, WindowTicks = 60 },
                new MarketItemDefData { Item = "log", BasePrice = 5f, SpreadFraction = 0.2f, MaxSellVolumePerWindow = 200, MaxBuyVolumePerWindow = 500, WindowTicks = 60 },
                new MarketItemDefData { Item = "wood", BasePrice = 2f, SpreadFraction = 0.2f, MaxSellVolumePerWindow = 500, MaxBuyVolumePerWindow = 1000, WindowTicks = 60 },
            });

            // Tier 1 unlock set (tech-unlock GDD rule 9). Start-available content has no node.
            p.Unlocks.Add(Node("gear32", "process", new[] { "item:gear32" },
                Group(Cond("phenomenon", "Stalled", "first", 0, 0)),
                Group(Cond("license", "lic_gearing", "eq", 1, 0))));
            p.Unlocks.Add(Node("accessor_keep_mode", "control", new[] { "capability:accessor.keep" },
                Group(Cond("metric", "sold.plank.session", "gte", 200, 0)),
                Group(Cond("task", "t_keep", "eq", 1, 0))));
            p.Unlocks.Add(Node("mechanics_trend", "measurement", new[] { "capability:inspection.mechanics.trend" },
                Group(Cond("metric", "load_pct.any", "gte", 60, 120), Cond("metric", "load_pct.any", "lte", 95, 120))));
            p.Unlocks.Add(Node("blocker", "control", new[] { "item:blocker" },
                Group(Cond("phenomenon", "CrossFeedDetected", "first", 0, 0)),
                Group(Cond("phenomenon", "OpenEndIgnored60s", "first", 0, 0))));
            p.Unlocks.Add(Node("carry_mode", "control", new[] { "capability:editing.carry" },
                Group(Cond("metric", "machine_replaced.count", "gte", 5, 0))));
            return p;
        }

        private static ItemDefData Item(string id, int maxStack, string display, bool tradeable = false, float energyJ = 0f, string blockDef = null)
            => new ItemDefData { Id = id, MaxStack = maxStack, DisplayNameKey = display, Tradeable = tradeable, EnergyJ = energyJ, BlockDef = blockDef, Icon = "icon." + id };

        private static UnlockNodeDefData Node(string id, string depth, string[] grants, params UnlockConditionGroupData[] groups)
        {
            var n = new UnlockNodeDefData { Id = id, Tier = 1, DepthTag = depth };
            n.Grants.AddRange(grants);
            n.ConditionGroups.AddRange(groups);
            return n;
        }

        private static UnlockConditionGroupData Group(params UnlockConditionData[] all)
        {
            var g = new UnlockConditionGroupData();
            g.All.AddRange(all);
            return g;
        }

        private static UnlockConditionData Cond(string kind, string subject, string op, double value, int window)
            => new UnlockConditionData { Kind = kind, Subject = subject, Op = op, Value = value, WindowTicks = window };
    }
}

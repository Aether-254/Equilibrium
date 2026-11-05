using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Equilibrium.Content
{
    /// <summary>
    /// Parses an addon directory into a <see cref="ContentPack"/>:
    /// addon.json (manifest) + items/*.json, tags/*.json, recipes/*.json, machines/*.json,
    /// market/*.json, unlocks/*.json. Each file holds a JSON array of records. Unknown item fields
    /// are preserved in <see cref="ItemDefData.Extras"/>. Pure C#; Newtonsoft.Json is shipped with
    /// Unity (com.unity.nuget.newtonsoft-json) and is not a UnityEngine dependency.
    /// </summary>
    public sealed class JsonContentSource : IContentSource
    {
        private readonly string _root;
        private readonly Func<string, string> _readText;
        private readonly Func<string, IEnumerable<string>> _listJson;

        public JsonContentSource(string rootDirectory)
            : this(rootDirectory, File.ReadAllText, dir => Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json") : Array.Empty<string>()) { }

        /// <summary>Injectable IO for tests and non-filesystem hosts.</summary>
        public JsonContentSource(string rootDirectory, Func<string, string> readText, Func<string, IEnumerable<string>> listJsonFiles)
        {
            _root = rootDirectory; _readText = readText; _listJson = listJsonFiles;
        }

        public ContentPack Load()
        {
            var pack = new ContentPack();
            string manifestPath = Path.Combine(_root, "addon.json");
            string manifestText = SafeRead(manifestPath);
            if (manifestText != null) pack.Manifest = ParseManifest(manifestText);

            foreach (var f in Sorted(_listJson(Path.Combine(_root, "items")))) pack.Items.AddRange(ParseItems(_readText(f)));
            foreach (var f in Sorted(_listJson(Path.Combine(_root, "tags")))) pack.Tags.AddRange(Deserialize<List<TagDefData>>(_readText(f)));
            foreach (var f in Sorted(_listJson(Path.Combine(_root, "recipes")))) pack.Recipes.AddRange(Deserialize<List<RecipeDefData>>(_readText(f)));
            foreach (var f in Sorted(_listJson(Path.Combine(_root, "machines")))) pack.Machines.AddRange(Deserialize<List<MachineDefData>>(_readText(f)));
            foreach (var f in Sorted(_listJson(Path.Combine(_root, "market")))) pack.Market.AddRange(Deserialize<List<MarketItemDefData>>(_readText(f)));
            foreach (var f in Sorted(_listJson(Path.Combine(_root, "unlocks")))) pack.Unlocks.AddRange(Deserialize<List<UnlockNodeDefData>>(_readText(f)));
            return pack;
        }

        public static AddonManifest ParseManifest(string json)
        {
            var m = Deserialize<AddonManifest>(json) ?? new AddonManifest();
            m.DependsOn ??= new List<string>();
            m.Modifies ??= new List<string>();
            return m;
        }

        /// <summary>Items are parsed via JObject so unknown properties land in Extras (forward compatibility).</summary>
        public static List<ItemDefData> ParseItems(string json)
        {
            var result = new List<ItemDefData>();
            var arr = JArray.Parse(json);
            foreach (var tok in arr)
            {
                if (!(tok is JObject o)) continue;
                var d = new ItemDefData
                {
                    Id = o.Value<string>("Id") ?? o.Value<string>("id"),
                    MaxStack = o.Value<int?>("MaxStack") ?? o.Value<int?>("maxStack") ?? 64,
                    DisplayNameKey = o.Value<string>("DisplayNameKey") ?? o.Value<string>("displayNameKey"),
                    Icon = o.Value<string>("Icon") ?? o.Value<string>("icon"),
                    Tradeable = o.Value<bool?>("Tradeable") ?? o.Value<bool?>("tradeable") ?? false,
                    EnergyJ = o.Value<float?>("EnergyJ") ?? o.Value<float?>("energyJ") ?? 0f,
                    BlockDef = o.Value<string>("BlockDef") ?? o.Value<string>("blockDef"),
                };
                foreach (var prop in o.Properties())
                {
                    if (KnownItemKeys.Contains(prop.Name)) continue;
                    d.Extras[prop.Name] = prop.Value.Type == JTokenType.String ? prop.Value.Value<string>() : prop.Value.ToString(Formatting.None);
                }
                result.Add(d);
            }
            return result;
        }

        private static readonly HashSet<string> KnownItemKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "Id","id","MaxStack","maxStack","DisplayNameKey","displayNameKey","Icon","icon","Tradeable","tradeable","EnergyJ","energyJ","BlockDef","blockDef"
        };

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Formatting.Indented, Settings);

        private string SafeRead(string path)
        {
            try { return _readText(path); }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static IEnumerable<string> Sorted(IEnumerable<string> files)
        {
            var list = new List<string>(files);
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }

    /// <summary>Writes a <see cref="ContentPack"/> to the addon directory layout; used by the editor export and by tests.</summary>
    public static class JsonContentWriter
    {
        public static Dictionary<string, string> ToFiles(ContentPack pack)
        {
            var files = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["addon.json"] = JsonContentSource.Serialize(pack.Manifest),
                ["items/items.json"] = JsonContentSource.Serialize(ItemsWithExtras(pack.Items)),
                ["tags/tags.json"] = JsonContentSource.Serialize(pack.Tags),
                ["recipes/recipes.json"] = JsonContentSource.Serialize(pack.Recipes),
                ["machines/machines.json"] = JsonContentSource.Serialize(pack.Machines),
                ["market/market.json"] = JsonContentSource.Serialize(pack.Market),
                ["unlocks/unlocks.json"] = JsonContentSource.Serialize(pack.Unlocks),
            };
            return files;
        }

        private static JArray ItemsWithExtras(List<ItemDefData> items)
        {
            var arr = new JArray();
            foreach (var d in items)
            {
                var o = new JObject
                {
                    ["Id"] = d.Id, ["MaxStack"] = d.MaxStack, ["DisplayNameKey"] = d.DisplayNameKey, ["Icon"] = d.Icon,
                    ["Tradeable"] = d.Tradeable, ["EnergyJ"] = d.EnergyJ, ["BlockDef"] = d.BlockDef,
                };
                foreach (var kv in d.Extras) o[kv.Key] = kv.Value;
                arr.Add(o);
            }
            return arr;
        }

        public static void WriteTo(string rootDirectory, ContentPack pack)
        {
            foreach (var kv in ToFiles(pack))
            {
                string path = Path.Combine(rootDirectory, kv.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, kv.Value);
            }
        }
    }
}

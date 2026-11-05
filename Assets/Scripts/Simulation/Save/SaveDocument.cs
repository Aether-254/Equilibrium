using System;
using System.Collections.Generic;
using Equilibrium.Content;

namespace Equilibrium.Simulation.Save
{
    /// <summary>Unknown members retain their original map location and raw MessagePack value.</summary>
    public abstract class SaveObject
    {
        public IDictionary<string, object> Extras { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
        internal HashSet<string> InlineExtras { get; } = new HashSet<string>(StringComparer.Ordinal);
    }

    public sealed class SaveDocument : SaveObject
    {
        public const int CurrentSchemaVersion = 1;
        public SaveHeader Header { get; set; } = new SaveHeader();
        public SaveSection World { get; set; } = new SaveSection();
        public SaveSection Containers { get; set; } = new SaveSection();
        public SaveSection Mech { get; set; } = new SaveSection();
        public SaveSection Market { get; set; } = new SaveSection();
        public SaveSection Ledger { get; set; } = new SaveSection();
        public SaveSection Unlocks { get; set; } = new SaveSection();
        public SaveSection Tasks { get; set; } = new SaveSection();
    }

    public sealed class SaveHeader : SaveObject
    {
        public int SchemaVersion { get; set; } = SaveDocument.CurrentSchemaVersion;
        public string GameVersion { get; set; } = "0.0.0-dev";
        public List<AddonVersion> Addons { get; set; } = new List<AddonVersion>();
        public MarketBinding MarketBinding { get; set; } = new MarketBinding();
        public string LastSaveWallTime { get; set; }
        public ulong TickIndex { get; set; }
    }

    public sealed class AddonVersion : SaveObject
    {
        public string Namespace { get; set; }
        public string Version { get; set; }
    }

    public sealed class MarketBinding : SaveObject
    {
        public string Mode { get; set; } = "LocalNpc";
        public string ServerId { get; set; }
    }

    /// <summary>Versioned envelope; system-specific world/market hydration belongs to later stories.</summary>
    public sealed class SaveSection : SaveObject
    {
        public int Version { get; set; } = 1;
        public List<SaveRecord> Records { get; set; } = new List<SaveRecord>();
    }

    public sealed class SaveRecord : SaveObject
    {
        private string _itemId;
        public string Id { get; set; }
        public string ContainerKey { get; set; }
        public string SlotKey { get; set; }
        public string ItemId { get => _itemId; set => _itemId = SaveIdentity.CanonicalItem(value); }
        public long Count { get; set; }
        public List<SaveReservation> Reservations { get; set; } = new List<SaveReservation>();
    }

    public sealed class SaveReservation : SaveObject
    {
        private string _itemId;
        public string Id { get; set; }
        public string ContainerKey { get; set; }
        public string SlotKey { get; set; }
        public string ItemId { get => _itemId; set => _itemId = SaveIdentity.CanonicalItem(value); }
        public long Count { get; set; }

        public static SaveReservation FromResolved(string id, string containerKey, string slotKey, ItemId item, long count)
        {
            if (item.IsNone) throw new ArgumentException("Reservation requires a concrete ItemId", nameof(item));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            return new SaveReservation { Id = id, ContainerKey = containerKey, SlotKey = slotKey, ItemId = item.Canonical, Count = count };
        }
    }

    internal static class SaveIdentity
    {
        internal static string CanonicalItem(string value)
        {
            if (value == null) return null;
            if (!Content.ItemId.TryCanonicalize(value, out var canonical, out _))
                throw new ArgumentException("SaveItemIdInvalid: " + value);
            return canonical;
        }
    }
}

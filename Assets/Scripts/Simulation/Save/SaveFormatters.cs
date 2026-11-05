using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Equilibrium.Simulation.Save
{
    // Closed registration and explicit typed calls keep this path AOT-safe, including nested lists.
    internal sealed class SaveResolver : IFormatterResolver
    {
        internal static readonly SaveResolver Instance = new SaveResolver();
        private SaveResolver() { }
        public IMessagePackFormatter<T> GetFormatter<T>() => Cache<T>.Formatter;
        private static class Cache<T>
        {
            internal static readonly IMessagePackFormatter<T> Formatter = Create();
            private static IMessagePackFormatter<T> Create()
            {
                if (typeof(T) == typeof(SaveDocument)) return (IMessagePackFormatter<T>)(object)new SaveDocumentFormatter();
                if (typeof(T) == typeof(SaveHeader)) return (IMessagePackFormatter<T>)(object)new SaveHeaderFormatter();
                if (typeof(T) == typeof(AddonVersion)) return (IMessagePackFormatter<T>)(object)new AddonVersionFormatter();
                if (typeof(T) == typeof(MarketBinding)) return (IMessagePackFormatter<T>)(object)new MarketBindingFormatter();
                if (typeof(T) == typeof(SaveSection)) return (IMessagePackFormatter<T>)(object)new SaveSectionFormatter();
                if (typeof(T) == typeof(SaveRecord)) return (IMessagePackFormatter<T>)(object)new SaveRecordFormatter();
                if (typeof(T) == typeof(SaveReservation)) return (IMessagePackFormatter<T>)(object)new SaveReservationFormatter();
                return null;
            }
        }
    }

    internal sealed class SaveDocumentFormatter : SaveMapFormatter<SaveDocument>
    {
        protected override void CheckEnd(ref MessagePackReader reader)
        {
            // Also checks the decompressed sequence, which the library's outer bytesRead cannot see.
            if (!reader.End) throw new MessagePackSerializationException("Trailing document bytes");
        }
        protected override int FieldCount => 8;
        protected override bool IsKnown(string key) => key == "header" || key == "world" || key == "containers" || key == "mech" || key == "market" || key == "ledger" || key == "unlocks" || key == "tasks";
        protected override void CheckRequired(HashSet<string> keys)
        {
            if (!keys.Contains("header")) throw new MessagePackSerializationException("Required save member missing: header");
        }
        protected override void WriteFields(ref MessagePackWriter writer, SaveDocument value, MessagePackSerializerOptions options)
        {
            writer.Write("header");
            SaveWire.WriteRecord(ref writer, value.Header, options);
            writer.Write("world");
            SaveWire.WriteRecord(ref writer, value.World, options);
            writer.Write("containers");
            SaveWire.WriteRecord(ref writer, value.Containers, options);
            writer.Write("mech");
            SaveWire.WriteRecord(ref writer, value.Mech, options);
            writer.Write("market");
            SaveWire.WriteRecord(ref writer, value.Market, options);
            writer.Write("ledger");
            SaveWire.WriteRecord(ref writer, value.Ledger, options);
            writer.Write("unlocks");
            SaveWire.WriteRecord(ref writer, value.Unlocks, options);
            writer.Write("tasks");
            SaveWire.WriteRecord(ref writer, value.Tasks, options);
        }
        protected override bool ReadField(ref MessagePackReader reader, SaveDocument value, string key, MessagePackSerializerOptions options)
        {
            switch (key)
            {
                case "header": value.Header = SaveWire.ReadRecord<SaveHeader>(ref reader, options); return true;
                case "world": value.World = SaveWire.ReadRecord<SaveSection>(ref reader, options); return true;
                case "containers": value.Containers = SaveWire.ReadRecord<SaveSection>(ref reader, options); return true;
                case "mech": value.Mech = SaveWire.ReadRecord<SaveSection>(ref reader, options); return true;
                case "market": value.Market = SaveWire.ReadRecord<SaveSection>(ref reader, options); return true;
                case "ledger": value.Ledger = SaveWire.ReadRecord<SaveSection>(ref reader, options); return true;
                case "unlocks": value.Unlocks = SaveWire.ReadRecord<SaveSection>(ref reader, options); return true;
                case "tasks": value.Tasks = SaveWire.ReadRecord<SaveSection>(ref reader, options); return true;
                default: return false;
            }
        }
    }

    internal sealed class SaveHeaderFormatter : SaveMapFormatter<SaveHeader>
    {
        protected override int FieldCount => 6;
        protected override bool IsKnown(string key) => key == "schemaVersion" || key == "gameVersion" || key == "addons" || key == "marketBinding" || key == "lastSaveWallTime" || key == "tickIndex";
        protected override void CheckRequired(HashSet<string> keys)
        {
            if (!keys.Contains("schemaVersion")) throw new MessagePackSerializationException("Required save member missing: schemaVersion");
        }
        protected override void WriteFields(ref MessagePackWriter writer, SaveHeader value, MessagePackSerializerOptions options)
        {
            writer.Write("schemaVersion");
            writer.Write(value.SchemaVersion);
            writer.Write("gameVersion");
            writer.Write(value.GameVersion);
            writer.Write("addons");
            SaveWire.WriteList(ref writer, value.Addons, options);
            writer.Write("marketBinding");
            SaveWire.WriteRecord(ref writer, value.MarketBinding, options);
            writer.Write("lastSaveWallTime");
            writer.Write(value.LastSaveWallTime);
            writer.Write("tickIndex");
            writer.Write(value.TickIndex);
        }
        protected override bool ReadField(ref MessagePackReader reader, SaveHeader value, string key, MessagePackSerializerOptions options)
        {
            switch (key)
            {
                case "schemaVersion": value.SchemaVersion = reader.ReadInt32(); return true;
                case "gameVersion": value.GameVersion = reader.ReadString(); return true;
                case "addons": value.Addons = SaveWire.ReadList<AddonVersion>(ref reader, options); return true;
                case "marketBinding": value.MarketBinding = SaveWire.ReadRecord<MarketBinding>(ref reader, options); return true;
                case "lastSaveWallTime": value.LastSaveWallTime = reader.ReadString(); return true;
                case "tickIndex": value.TickIndex = reader.ReadUInt64(); return true;
                default: return false;
            }
        }
    }

    internal sealed class AddonVersionFormatter : SaveMapFormatter<AddonVersion>
    {
        protected override int FieldCount => 2;
        protected override bool IsKnown(string key) => key == "namespace" || key == "version";
        protected override void WriteFields(ref MessagePackWriter writer, AddonVersion value, MessagePackSerializerOptions options)
        {
            writer.Write("namespace");
            writer.Write(value.Namespace);
            writer.Write("version");
            writer.Write(value.Version);
        }
        protected override bool ReadField(ref MessagePackReader reader, AddonVersion value, string key, MessagePackSerializerOptions options)
        {
            switch (key)
            {
                case "namespace": value.Namespace = reader.ReadString(); return true;
                case "version": value.Version = reader.ReadString(); return true;
                default: return false;
            }
        }
    }

    internal sealed class MarketBindingFormatter : SaveMapFormatter<MarketBinding>
    {
        protected override int FieldCount => 2;
        protected override bool IsKnown(string key) => key == "mode" || key == "serverId";
        protected override void WriteFields(ref MessagePackWriter writer, MarketBinding value, MessagePackSerializerOptions options)
        {
            writer.Write("mode");
            writer.Write(value.Mode);
            writer.Write("serverId");
            writer.Write(value.ServerId);
        }
        protected override bool ReadField(ref MessagePackReader reader, MarketBinding value, string key, MessagePackSerializerOptions options)
        {
            switch (key)
            {
                case "mode": value.Mode = reader.ReadString(); return true;
                case "serverId": value.ServerId = reader.ReadString(); return true;
                default: return false;
            }
        }
    }

    internal sealed class SaveSectionFormatter : SaveMapFormatter<SaveSection>
    {
        protected override int FieldCount => 2;
        protected override bool IsKnown(string key) => key == "version" || key == "records";
        protected override void WriteFields(ref MessagePackWriter writer, SaveSection value, MessagePackSerializerOptions options)
        {
            writer.Write("version");
            writer.Write(value.Version);
            writer.Write("records");
            SaveWire.WriteList(ref writer, value.Records, options);
        }
        protected override bool ReadField(ref MessagePackReader reader, SaveSection value, string key, MessagePackSerializerOptions options)
        {
            switch (key)
            {
                case "version": value.Version = reader.ReadInt32(); return true;
                case "records": value.Records = SaveWire.ReadList<SaveRecord>(ref reader, options); return true;
                default: return false;
            }
        }
    }

    internal sealed class SaveRecordFormatter : SaveMapFormatter<SaveRecord>
    {
        protected override int FieldCount => 6;
        protected override bool IsKnown(string key) => key == "id" || key == "containerKey" || key == "slotKey" || key == "itemId" || key == "count" || key == "reservations";
        protected override void WriteFields(ref MessagePackWriter writer, SaveRecord value, MessagePackSerializerOptions options)
        {
            writer.Write("id");
            writer.Write(value.Id);
            writer.Write("containerKey");
            writer.Write(value.ContainerKey);
            writer.Write("slotKey");
            writer.Write(value.SlotKey);
            writer.Write("itemId");
            writer.Write(value.ItemId);
            writer.Write("count");
            writer.Write(value.Count);
            writer.Write("reservations");
            SaveWire.WriteList(ref writer, value.Reservations, options);
        }
        protected override bool ReadField(ref MessagePackReader reader, SaveRecord value, string key, MessagePackSerializerOptions options)
        {
            switch (key)
            {
                case "id": value.Id = reader.ReadString(); return true;
                case "containerKey": value.ContainerKey = reader.ReadString(); return true;
                case "slotKey": value.SlotKey = reader.ReadString(); return true;
                case "itemId": value.ItemId = reader.ReadString(); return true;
                case "count": value.Count = reader.ReadInt64(); return true;
                case "reservations": value.Reservations = SaveWire.ReadList<SaveReservation>(ref reader, options); return true;
                default: return false;
            }
        }
    }

    internal sealed class SaveReservationFormatter : SaveMapFormatter<SaveReservation>
    {
        protected override int FieldCount => 5;
        protected override bool IsKnown(string key) => key == "id" || key == "containerKey" || key == "slotKey" || key == "itemId" || key == "count";
        protected override void WriteFields(ref MessagePackWriter writer, SaveReservation value, MessagePackSerializerOptions options)
        {
            writer.Write("id");
            writer.Write(value.Id);
            writer.Write("containerKey");
            writer.Write(value.ContainerKey);
            writer.Write("slotKey");
            writer.Write(value.SlotKey);
            writer.Write("itemId");
            writer.Write(value.ItemId);
            writer.Write("count");
            writer.Write(value.Count);
        }
        protected override bool ReadField(ref MessagePackReader reader, SaveReservation value, string key, MessagePackSerializerOptions options)
        {
            switch (key)
            {
                case "id": value.Id = reader.ReadString(); return true;
                case "containerKey": value.ContainerKey = reader.ReadString(); return true;
                case "slotKey": value.SlotKey = reader.ReadString(); return true;
                case "itemId": value.ItemId = reader.ReadString(); return true;
                case "count": value.Count = reader.ReadInt64(); return true;
                default: return false;
            }
        }
    }
}

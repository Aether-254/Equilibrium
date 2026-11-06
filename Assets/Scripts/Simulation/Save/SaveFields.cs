using System;
using System.Collections.Generic;
using MessagePack;

namespace Equilibrium.Simulation.Save
{
    /// <summary>Typed access to system-owned extension fields using the existing closed save resolver.</summary>
    public static class SaveFields
    {
        public static SaveRecord Copy(SaveRecord source)
            => source == null ? new SaveRecord() : Decode<SaveRecord>(Encode(source));

        public static SaveSection Section(SaveObject owner, string key, bool optional = false)
        {
            if (optional && !owner.Extras.ContainsKey(key)) return new SaveSection();
            var section = Decode<SaveSection>(Raw(owner, key));
            if (section == null || section.Version != 1 || section.Records == null)
                throw Invalid(key + ": unsupported section");
            Index(section.Records);
            return section;
        }

        public static void SetSection(SaveObject owner, string key, SaveSection value)
            => owner.Extras[key] = Encode(value);

        public static Dictionary<string, SaveRecord> Index(IReadOnlyList<SaveRecord> records)
        {
            var result = new Dictionary<string, SaveRecord>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (record == null || string.IsNullOrEmpty(record.Id) || record.Count < 0 || record.Reservations == null || result.ContainsKey(record.Id))
                    throw Invalid("duplicate or invalid record");
                result.Add(record.Id, record);
            }
            return result;
        }

        public static string Text(SaveObject owner, string key, bool nullable = false)
        {
            var reader = Reader(owner, key);
            string value = reader.ReadString();
            if (!reader.End || (!nullable && string.IsNullOrEmpty(value))) throw Invalid(key);
            return value;
        }

        public static int Int(SaveObject owner, string key, int min, int max)
        {
            var reader = Reader(owner, key);
            long value = reader.ReadInt64();
            if (!reader.End || value < min || value > max) throw Invalid(key);
            return (int)value;
        }

        public static long Long(SaveObject owner, string key)
        {
            var reader = Reader(owner, key); long value = reader.ReadInt64();
            if (!reader.End || value < 0) throw Invalid(key);
            return value;
        }

        public static double Number(SaveObject owner, string key, double max = double.MaxValue)
        {
            var reader = Reader(owner, key); double value = reader.ReadDouble();
            if (!reader.End || double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > max) throw Invalid(key);
            return value;
        }

        public static bool Bool(SaveObject owner, string key)
        {
            var reader = Reader(owner, key); bool value = reader.ReadBoolean();
            if (!reader.End) throw Invalid(key);
            return value;
        }

        public static byte[] Bytes(SaveObject owner, string key)
        {
            var reader = Reader(owner, key); var value = reader.ReadBytes();
            if (!reader.End || !value.HasValue) throw Invalid(key);
            var result = new byte[value.Value.Length];
            System.Buffers.BuffersExtensions.CopyTo(value.Value, result);
            return result;
        }

        public static InvalidOperationException Invalid(string detail) => new InvalidOperationException("SaveInvalid: " + detail);

        private static RawSaveValue Encode<T>(T value)
            => RawSaveValue.FromBytes(MessagePackSerializer.Serialize(value, SaveDocumentCodec.UncompressedOptions));

        private static T Decode<T>(RawSaveValue value)
            => MessagePackSerializer.Deserialize<T>(value.CopyBytes(), SaveDocumentCodec.UncompressedOptions);

        private static MessagePackReader Reader(SaveObject owner, string key) => new MessagePackReader(Raw(owner, key).CopyBytes());

        private static RawSaveValue Raw(SaveObject owner, string key)
        {
            if (!owner.Extras.TryGetValue(key, out var value)) throw Invalid("missing " + key);
            if (value is RawSaveValue raw) return raw;
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            SaveWire.WriteExtra(ref writer, value); writer.Flush();
            return RawSaveValue.FromBytes(buffer.WrittenSpan.ToArray());
        }
    }
}

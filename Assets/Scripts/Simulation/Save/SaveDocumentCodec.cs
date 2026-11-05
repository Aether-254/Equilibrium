using System;
using MessagePack;
using MessagePack.Formatters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Equilibrium.Simulation.Save
{
    /// <summary>Only the closed, statically referenced save resolver is used; no dynamic/typeless fallback.</summary>
    public static class SaveDocumentCodec
    {
        public const int MaxBytes = 64 * 1024 * 1024;
        internal static readonly MessagePackSerializerOptions UncompressedOptions =
            new MessagePackSerializerOptions(SaveResolver.Instance)
                .WithSecurity(MessagePackSecurity.UntrustedData.WithMaximumObjectGraphDepth(64)
                    .WithMaximumDecompressedSize(MaxBytes));
        internal static readonly MessagePackSerializerOptions Options = UncompressedOptions
            .WithCompression(MessagePackCompression.Lz4BlockArray);

        public static byte[] Serialize(SaveDocument document)
        {
            Validate(document);
            // Check the decompressed limit too: a large, compressible factory must not produce a file
            // that this same codec would refuse to read. Compression writes these already encoded bytes.
            var raw = MessagePackSerializer.Serialize(document, UncompressedOptions);
            if (raw.Length > MaxBytes) throw new InvalidOperationException("SaveTooLarge");
            var bytes = MessagePackSerializer.Serialize(raw, Options.WithResolver(EncodedSaveResolver.Instance));
            if (bytes.Length > MaxBytes) throw new InvalidOperationException("SaveTooLarge");
            return bytes;
        }

        private sealed class EncodedSaveResolver : IFormatterResolver, IMessagePackFormatter<byte[]>
        {
            internal static readonly EncodedSaveResolver Instance = new EncodedSaveResolver();
            public IMessagePackFormatter<T> GetFormatter<T>() => typeof(T) == typeof(byte[]) ? (IMessagePackFormatter<T>)(object)this : null;
            public void Serialize(ref MessagePackWriter writer, byte[] value, MessagePackSerializerOptions options) => writer.WriteRaw(value);
            public byte[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new NotSupportedException();
        }

        public static SaveDocument Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes)
                throw new ArgumentException("Save data is empty or too large", nameof(bytes));
            var doc = MessagePackSerializer.Deserialize<SaveDocument>(bytes, Options, out int consumed);
            if (consumed != bytes.Length) throw new MessagePackSerializationException("Trailing save bytes");
            Validate(doc);
            return doc;
        }

        private static void Validate(SaveDocument doc)
        {
            if (doc?.Header == null) throw new InvalidOperationException("SaveHeaderMissing");
            if (doc.Header.SchemaVersion > SaveDocument.CurrentSchemaVersion)
                throw new InvalidOperationException("SaveFromNewerVersion");
            if (doc.Header.SchemaVersion < 1) throw new InvalidOperationException("SaveSchemaInvalid");
            if (doc.Header.Addons == null || doc.Header.MarketBinding == null)
                throw new InvalidOperationException("SaveHeaderInvalid");
            ValidateSection(doc.World); ValidateSection(doc.Containers); ValidateSection(doc.Mech);
            ValidateSection(doc.Market); ValidateSection(doc.Ledger); ValidateSection(doc.Unlocks); ValidateSection(doc.Tasks);
        }

        private static void ValidateSection(SaveSection section)
        {
            if (section == null || section.Version < 1 || section.Records == null)
                throw new InvalidOperationException("SaveSectionInvalid");
            foreach (var record in section.Records)
            {
                if (record == null || record.Count < 0 || record.Reservations == null)
                    throw new InvalidOperationException("SaveRecordInvalid");
                foreach (var reservation in record.Reservations)
                    if (reservation == null || reservation.ItemId == null || reservation.Count <= 0 ||
                        string.IsNullOrEmpty(reservation.Id) || string.IsNullOrEmpty(reservation.ContainerKey) ||
                        string.IsNullOrEmpty(reservation.SlotKey))
                        throw new InvalidOperationException("SaveReservationInvalid");
            }
        }
    }

    /// <summary>Inspection/interchange only. Binary saves retain raw values; JSON cannot retain their encoding.</summary>
    public static class SaveDocumentJson
    {
        public static string Serialize(SaveDocument document)
        {
            var bytes = SaveDocumentCodec.Serialize(document);
            string json = MessagePackSerializer.ConvertToJson(bytes, SaveDocumentCodec.Options);
            using (var text = new System.IO.StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
                return JToken.ReadFrom(reader).ToString(Formatting.Indented);
        }

        public static SaveDocument Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Save JSON is empty", nameof(json));
            if (json.Length > SaveDocumentCodec.MaxBytes) throw new ArgumentException("Save JSON is too large", nameof(json));
            // Validate depth and duplicate keys before the library's JSON conversion.
            using (var text = new System.IO.StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
            {
                JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new JsonSerializationException("Trailing JSON data");
            }
            return SaveDocumentCodec.Deserialize(MessagePackSerializer.ConvertFromJson(json, SaveDocumentCodec.UncompressedOptions));
        }
    }
}

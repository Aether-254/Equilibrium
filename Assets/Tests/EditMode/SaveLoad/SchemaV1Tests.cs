using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Equilibrium.Content;
using Equilibrium.Simulation.Save;
using Equilibrium.Tests.EditMode.ItemRegistry;
using MessagePack;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.SaveLoad
{
    public sealed class SchemaV1Tests
    {
        [Test]
        public void Lz4_RoundTripsAllSectionsAndUnsignedTickIndex()
        {
            var doc = new SaveDocument();
            doc.Header.TickIndex = ulong.MaxValue;
            doc.Header.LastSaveWallTime = "2026-11-04T12:00:00Z";
            doc.Header.Addons.Add(new AddonVersion { Namespace = "farmer_addon", Version = "1.0.0" });
            doc.Header.MarketBinding.Mode = "RemoteServer";
            doc.Header.MarketBinding.ServerId = "community-market";
            foreach (var section in Sections(doc))
                section.Records.Add(new SaveRecord { Id = "entry", ItemId = "wood", Count = 3000 });
            doc.Extras["padding"] = new string('x', 8192);
            byte[] packed = SaveDocumentCodec.Serialize(doc);
            Assert.That(packed.Length, Is.LessThan(8192), "LZ4 must actually be exercised");
            var loaded = SaveDocumentCodec.Deserialize(packed);
            Assert.That(loaded.Header.TickIndex, Is.EqualTo(ulong.MaxValue));
            Assert.That(loaded.Header.LastSaveWallTime, Is.EqualTo(doc.Header.LastSaveWallTime));
            Assert.That(loaded.Header.Addons[0].Namespace, Is.EqualTo("farmer_addon"));
            Assert.That(loaded.Header.MarketBinding.ServerId, Is.EqualTo("community-market"));
            foreach (var section in Sections(loaded))
            {
                Assert.That(section.Records[0].ItemId, Is.EqualTo("equilibrium:wood"));
                Assert.That(section.Records[0].Count, Is.EqualTo(3000));
            }
        }

        [Test]
        public void UnknownIncomingMembers_KeepExactBytesAndOriginalLocationAtEveryRecordLevel()
        {
            // Int16(42), float32 NaN payload, custom extension, nested map/array: normalization loses bytes.
            byte[] raw = { 0x94, 0xd1, 0x00, 0x2a, 0xca, 0x7f, 0xc0, 0x00, 0x01,
                0xd4, 0x21, 0xab, 0x81, 0xa1, 0x78, 0x91, 0xc0 };
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteMapHeader(3);
            writer.Write("header"); writer.WriteMapHeader(5);
            writer.Write("schemaVersion"); writer.Write(1);
            writer.Write("future"); writer.WriteRaw(raw);
            writer.Write("addons"); writer.WriteArrayHeader(1); writer.WriteMapHeader(3);
            writer.Write("namespace"); writer.Write("farmer_addon"); writer.Write("version"); writer.Write("1");
            writer.Write("future"); writer.WriteRaw(raw);
            writer.Write("marketBinding"); writer.WriteMapHeader(1); writer.Write("future"); writer.WriteRaw(raw);
            writer.Write("extras"); writer.WriteMapHeader(1); writer.Write("explicit"); writer.WriteRaw(raw);
            writer.Write("containers"); writer.WriteMapHeader(2);
            writer.Write("future"); writer.WriteRaw(raw);
            writer.Write("records"); writer.WriteArrayHeader(1); writer.WriteMapHeader(2);
            writer.Write("future"); writer.WriteRaw(raw);
            writer.Write("reservations"); writer.WriteArrayHeader(1); writer.WriteMapHeader(6);
            writer.Write("id"); writer.Write("job"); writer.Write("containerKey"); writer.Write("input");
            writer.Write("slotKey"); writer.Write("ore"); writer.Write("itemId"); writer.Write("equilibrium:iron_ingot");
            writer.Write("count"); writer.Write(1); writer.Write("future"); writer.WriteRaw(raw);
            writer.Write("future"); writer.WriteRaw(raw);
            writer.Flush();
            var loaded = SaveDocumentCodec.Deserialize(buffer.WrittenSpan.ToArray());
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var value in AllFixtureObjects(loaded))
                    CollectionAssert.AreEqual(raw, ((RawSaveValue)value.Extras["future"]).CopyBytes());
                CollectionAssert.AreEqual(raw, ((RawSaveValue)loaded.Header.Extras["explicit"]).CopyBytes());
                loaded = SaveDocumentCodec.Deserialize(SaveDocumentCodec.Serialize(loaded));
            }
            // Read actual wire map, independently of our formatter, to catch accidental nesting in Extras.
            byte[] root = ReadMapValue(SaveDocumentCodec.Serialize(loaded), "future", compressed: true);
            CollectionAssert.AreEqual(raw, root);
            byte[] header = ReadMapValue(SaveDocumentCodec.Serialize(loaded), "header", compressed: true);
            CollectionAssert.AreEqual(raw, ReadMapValue(header, "future"));
        }

        [Test]
        public void JsonIncomingUnknownMembers_AreRetainedInsteadOfIgnored()
        {
            const string input = "{\"header\":{\"schemaVersion\":1,\"futureHeader\":{\"x\":[42,true]}},\"world\":{\"futureWorld\":\"keep\"},\"futureRoot\":99}";
            var loaded = SaveDocumentJson.Deserialize(input);
            string json = SaveDocumentJson.Serialize(loaded);
            Assert.That(json, Does.Contain("\"futureHeader\""));
            Assert.That(json, Does.Contain("\"futureWorld\""));
            Assert.That(json, Does.Contain("\"futureRoot\""));
            Assert.That(SaveDocumentJson.Deserialize(json).Extras.ContainsKey("futureRoot"), Is.True);
        }

        [Test]
        public void TagSelectedReservation_StoresConcreteItemAndNamedBindings()
        {
            var pack = Packs.Base(Packs.Item("iron_ingot"));
            pack.Tags.Add(Packs.Tag("#ingot/iron", add: new[] { "iron_ingot" }));
            var (registry, _) = Packs.Load(pack);
            registry.Tags.TryGet("equilibrium:ingot", out var tag);
            ItemId chosen = registry.Tags.Members(tag).Single();
            var record = new SaveRecord { Id = "machine:2,4", ContainerKey = "input", SlotKey = "ore", ItemId = chosen.Canonical, Count = 8 };
            record.Reservations.Add(SaveReservation.FromResolved("job:7", "input", "ore", chosen, 3));
            var doc = new SaveDocument(); doc.Containers.Records.Add(record);
            var loaded = SaveDocumentCodec.Deserialize(SaveDocumentCodec.Serialize(doc));
            Assert.That(loaded.Containers.Records[0].Reservations[0].ItemId, Is.EqualTo("equilibrium:iron_ingot"));
            Assert.That(loaded.Containers.Records[0].Reservations[0].SlotKey, Is.EqualTo("ore"));
            string json = SaveDocumentJson.Serialize(loaded);
            Assert.That(json, Does.Not.Contain("#ingot"));
            Assert.That(json, Does.Not.Contain("Handle"));
        }

        [TestCase("#ingot")]
        [TestCase("equilibrium:Wood")]
        [TestCase("")]
        public void InvalidItemIdentity_IsRejected(string id)
            => Assert.Throws<ArgumentException>(() => new SaveRecord { ItemId = id });

        [Test]
        public void MissingAddonIdentity_DoesNotRequireAnInstalledRegistry()
        {
            var doc = new SaveDocument();
            doc.Containers.Records.Add(new SaveRecord { ItemId = "missing_addon:seed", Count = 17 });
            Assert.That(SaveDocumentCodec.Deserialize(SaveDocumentCodec.Serialize(doc)).Containers.Records[0].ItemId,
                Is.EqualTo("missing_addon:seed"));
        }

        [TestCase("{}")]
        [TestCase("{\"header\":{}}")]
        [TestCase("{\"header\":null}")]
        [TestCase("{\"header\":{\"schemaVersion\":0}}")]
        [TestCase("{\"header\":{\"schemaVersion\":1,\"schemaVersion\":2}}")]
        public void InvalidOrAmbiguousHeaders_AreRejected(string json)
            => Assert.That(() => SaveDocumentJson.Deserialize(json), Throws.Exception);

        [Test]
        public void TruncatedTrailingAndOversizedCollections_AreRejected()
        {
            var bytes = SaveDocumentCodec.Serialize(new SaveDocument());
            Assert.That(() => SaveDocumentCodec.Deserialize(bytes.Take(bytes.Length / 2).ToArray()), Throws.Exception);
            Assert.That(() => SaveDocumentCodec.Deserialize(bytes.Concat(new byte[] { 0xc0 }).ToArray()), Throws.Exception);
            Assert.That(() => RawSaveValue.FromBytes(new byte[] { 0xdd, 0xff, 0xff, 0xff, 0xff }), Throws.Exception);
        }

        [Test]
        public void DeepUnknownData_IsRejectedWithoutStackOverflow()
        {
            var bytes = Enumerable.Repeat((byte)0x91, 80).Concat(new byte[] { 0xc0 }).ToArray();
            Assert.That(() => RawSaveValue.FromBytes(bytes), Throws.Exception);
        }

        [Test]
        public void TrailingValueInsideCompressedEnvelope_IsRejected()
        {
            var bytes = MessagePackSerializer.ConvertFromJson("{\"header\":{\"schemaVersion\":1}}");
            var invalid = bytes.Concat(Enumerable.Repeat((byte)0xc0, 200)).ToArray();
            var packed = MessagePackSerializer.Serialize(invalid, new MessagePackSerializerOptions(new RawPayloadResolver())
                .WithCompression(MessagePackCompression.Lz4BlockArray));
            Assert.That(() => SaveDocumentCodec.Deserialize(packed), Throws.Exception);
        }

        [Test]
        public void RawValues_OwnTheirBytesAndRejectArbitraryObjectGraphs()
        {
            byte[] source = { 0xd1, 0x00, 0x2a };
            var raw = RawSaveValue.FromBytes(source);
            source[2] = 0;
            raw.CopyBytes()[2] = 0;
            Assert.That(raw.CopyBytes()[2], Is.EqualTo(42));
            var doc = new SaveDocument(); doc.Extras["object"] = new object();
            Assert.That(() => SaveDocumentCodec.Serialize(doc), Throws.Exception);
        }

        [Test]
        public void HighlyCompressibleOversizeDocument_IsRefusedBeforeWritingAnUnreadableSave()
        {
            var doc = new SaveDocument();
            doc.Extras["large"] = new byte[SaveDocumentCodec.MaxBytes];
            var error = Assert.Throws<InvalidOperationException>(() => SaveDocumentCodec.Serialize(doc));
            Assert.That(error.Message, Is.EqualTo("SaveTooLarge"));
        }

        [Test]
        public void DuplicateBinaryKeys_AndInlineExtrasCollisions_AreRejected()
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteMapHeader(2);
            writer.Write("header"); writer.WriteMapHeader(1); writer.Write("schemaVersion"); writer.Write(1);
            writer.Write("header"); writer.WriteMapHeader(1); writer.Write("schemaVersion"); writer.Write(1);
            writer.Flush();
            Assert.That(() => SaveDocumentCodec.Deserialize(buffer.WrittenSpan.ToArray()), Throws.Exception);

            const string collision = "{\"header\":{\"schemaVersion\":1},\"future\":42,\"extras\":{\"future\":99}}";
            Assert.That(() => SaveDocumentJson.Deserialize(collision), Throws.Exception);
        }

        private static SaveSection[] Sections(SaveDocument d) => new[] { d.World, d.Containers, d.Mech, d.Market, d.Ledger, d.Unlocks, d.Tasks };
        private static SaveObject[] AllFixtureObjects(SaveDocument d) => new SaveObject[]
            { d, d.Header, d.Header.Addons[0], d.Header.MarketBinding, d.Containers, d.Containers.Records[0], d.Containers.Records[0].Reservations[0] };

        private static byte[] ReadMapValue(byte[] bytes, string key, bool compressed = false)
        {
            if (compressed)
                return MessagePackSerializer.Deserialize<byte[]>(bytes, new MessagePackSerializerOptions(new RawMapResolver(key))
                    .WithCompression(MessagePackCompression.Lz4BlockArray));
            var reader = new MessagePackReader(bytes);
            return ReadMapValue(ref reader, key);
        }
        private static byte[] ReadMapValue(ref MessagePackReader reader, string key)
        {
            int count = reader.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                if (reader.ReadString() == key) return reader.ReadRaw().ToArray();
                reader.Skip();
            }
            throw new AssertionException("Missing wire key: " + key);
        }
        private sealed class RawMapResolver : IFormatterResolver, MessagePack.Formatters.IMessagePackFormatter<byte[]>
        {
            private readonly string _key;
            public RawMapResolver(string key) { _key = key; }
            public MessagePack.Formatters.IMessagePackFormatter<T> GetFormatter<T>() => (MessagePack.Formatters.IMessagePackFormatter<T>)(object)this;
            public byte[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                // Consume the entire map even after capturing a member.
                var probe = reader.CreatePeekReader();
                byte[] value = ReadMapValue(ref probe, _key);
                reader.Skip(); return value;
            }
            public void Serialize(ref MessagePackWriter writer, byte[] value, MessagePackSerializerOptions options) => throw new NotSupportedException();
        }
        private sealed class RawPayloadResolver : IFormatterResolver, MessagePack.Formatters.IMessagePackFormatter<byte[]>
        {
            public MessagePack.Formatters.IMessagePackFormatter<T> GetFormatter<T>() => (MessagePack.Formatters.IMessagePackFormatter<T>)(object)this;
            public byte[] Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new NotSupportedException();
            public void Serialize(ref MessagePackWriter writer, byte[] value, MessagePackSerializerOptions options) => writer.WriteRaw(value);
        }
    }
}

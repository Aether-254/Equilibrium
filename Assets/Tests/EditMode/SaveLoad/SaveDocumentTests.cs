using System;
using NUnit.Framework;
using Equilibrium.Simulation.Save;
using MessagePack;

namespace Equilibrium.Tests.EditMode.SaveLoad
{
    public sealed class SaveDocumentTests
    {
        [Test]
        public void SchemaV1_RoundTripsCanonicalItemAndUnknownExtras()
        {
            var doc = new SaveDocument();
            doc.Header.TickIndex = 42;
            doc.Containers.Records.Add(new SaveRecord { Id = "slot:0", ItemId = "equilibrium:wood", Count = 10 });
            doc.Containers.Records[0].Extras["future"] = 42;
            var json = SaveDocumentJson.Serialize(doc);
            var loaded = SaveDocumentJson.Deserialize(json);
            Assert.That(loaded.Header.SchemaVersion, Is.EqualTo(1));
            Assert.That(loaded.Containers.Records[0].ItemId, Is.EqualTo("equilibrium:wood"));
            var raw = (RawSaveValue)loaded.Containers.Records[0].Extras["future"];
            Assert.That(new MessagePackReader(raw.CopyBytes()).ReadInt32(), Is.EqualTo(42));
        }

        [Test]
        public void NewerSchema_IsRefused()
        {
            var json = "{\"header\":{\"schemaVersion\":99}}";
            var ex = Assert.Throws<InvalidOperationException>(() => SaveDocumentJson.Deserialize(json));
            Assert.That(ex.Message, Is.EqualTo("SaveFromNewerVersion"));
        }

        [Test]
        public void SaveRecords_HaveNoRngOrTimeControlFields()
        {
            foreach (var type in typeof(SaveDocument).Assembly.GetTypes())
            {
                if (!typeof(SaveObject).IsAssignableFrom(type)) continue;
                foreach (var field in type.GetProperties())
                    Assert.That(field.Name.ToLowerInvariant(), Does.Not.Match("rng|seed|timescale|speed|pause"));
            }
        }
    }
}

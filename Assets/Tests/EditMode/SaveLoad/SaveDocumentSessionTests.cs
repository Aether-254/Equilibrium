#if !EQ_PURE
using System;
using Equilibrium.Presentation;
using Equilibrium.Simulation.Save;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.SaveLoad
{
    public sealed class SaveDocumentSessionTests
    {
        [TearDown] public void Cleanup() => SaveDocumentSession.Clear();

        [Test]
        public void InspectionUsesDetachedCurrentDocument()
        {
            SaveDocumentSession.Clear();
            Assert.That(SaveDocumentSession.HasCurrent, Is.False);
            Assert.Throws<InvalidOperationException>(() => SaveDocumentSession.ReadCurrent());
            var source = new SaveDocument(); source.Header.TickIndex = 12;
            SaveDocumentSession.Publish(source);
            source.Header.TickIndex = 99;
            var first = SaveDocumentSession.ReadCurrent(); first.Header.TickIndex = 88;
            Assert.That(SaveDocumentSession.ReadCurrent().Header.TickIndex, Is.EqualTo(12UL));
            string json = SaveDocumentJson.Serialize(SaveDocumentSession.ReadCurrent());
            Assert.That(json, Does.Contain("\"tickIndex\": 12"));
        }
    }
}
#endif

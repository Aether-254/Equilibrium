#if EQ_SAVE_SPIKE
using System;
using System.IO;
using Equilibrium.Content;
using Equilibrium.Simulation.Save;
using UnityEngine;

namespace Equilibrium.Presentation
{
    /// <summary>Compiled only for the explicit compatibility build; absent from production players.</summary>
    internal static class SaveCodecPlayerProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Run()
        {
            string marker = Argument("-saveProbeResult");
            try
            {
                if (string.IsNullOrEmpty(marker)) throw new InvalidOperationException("Missing -saveProbeResult");
#if !ENABLE_IL2CPP
                throw new InvalidOperationException("Probe must run under IL2CPP");
#endif
                var table = new ItemIdTable();
                var doc = new SaveDocument();
                doc.Header.TickIndex = ulong.MaxValue;
                doc.Header.Addons.Add(new AddonVersion { Namespace = "farmer_addon", Version = "1" });
                var slot = new SaveRecord { Id = "machine:2,4", ItemId = "iron_ingot", Count = 12, ContainerKey = "input", SlotKey = "ore" };
                slot.Reservations.Add(SaveReservation.FromResolved("job:1", "input", "ore", table.Parse("iron_ingot"), 4));
                doc.Containers.Records.Add(slot);
                doc.Extras["padding"] = new string('x', 8192);
                byte[] raw = { 0xd4, 0x21, 0xab };
                doc.Extras["futureExtension"] = RawSaveValue.FromBytes(raw);
                byte[] bytes = SaveDocumentCodec.Serialize(doc);
                var copy = SaveDocumentCodec.Deserialize(bytes);
                Require(bytes.Length < 8192, "LZ4");
                Require(copy.Header.TickIndex == ulong.MaxValue, "tickIndex");
                Require(copy.Containers.Records[0].Reservations[0].ItemId == "equilibrium:iron_ingot", "canonical reservation");
                Require(Convert.ToBase64String(((RawSaveValue)copy.Extras["futureExtension"]).CopyBytes()) == Convert.ToBase64String(raw), "raw extension");
                var future = SaveDocumentJson.Deserialize("{\"header\":{\"schemaVersion\":1,\"futureHeader\":42},\"world\":{\"futureWorld\":true},\"futureRoot\":99}");
                var futureCopy = SaveDocumentCodec.Deserialize(SaveDocumentCodec.Serialize(future));
                Require(futureCopy.Extras.ContainsKey("futureRoot") && futureCopy.Header.Extras.ContainsKey("futureHeader") && futureCopy.World.Extras.ContainsKey("futureWorld"), "unknown members");
                SaveDocumentSession.Publish(copy);
                Require(SaveDocumentJson.Serialize(SaveDocumentSession.ReadCurrent()).Contains("equilibrium:iron_ingot"), "JSON export");
                File.WriteAllBytes(Path.ChangeExtension(marker, ".eqs"), bytes);
                File.WriteAllText(Path.ChangeExtension(marker, ".json"), SaveDocumentJson.Serialize(copy));
                File.WriteAllText(marker, "PASS backend=IL2CPP codec=MessagePack-3.1.12 compression=Lz4BlockArray canonical-reservation raw-extension unknown-members json-export bytes=" + bytes.Length);
                Debug.Log("EQ_SAVE_PROBE_PASS");
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(marker)) File.WriteAllText(marker, "FAIL " + ex);
                Debug.LogException(ex);
                Application.Quit(1);
            }
        }

        private static void Require(bool success, string label)
        {
            if (!success) throw new InvalidOperationException("Probe failed: " + label);
        }
        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
#endif

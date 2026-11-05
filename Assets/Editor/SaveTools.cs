using System.IO;
using System.Text;
using Equilibrium.Presentation;
using Equilibrium.Simulation.Save;
using UnityEditor;

namespace Equilibrium.Editor
{
    public static class SaveTools
    {
        [MenuItem("Equilibrium/Save/Export JSON")]
        public static void ExportJson()
        {
            if (!SaveDocumentSession.HasCurrent) return;
            string path = EditorUtility.SaveFilePanel("Export current save document", "", "equilibrium-save", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, SaveDocumentJson.Serialize(SaveDocumentSession.ReadCurrent()), new UTF8Encoding(false));
        }

        [MenuItem("Equilibrium/Save/Export JSON", true)]
        private static bool CanExportJson() => SaveDocumentSession.HasCurrent;

        [MenuItem("Equilibrium/Save/Inspect existing EQS")]
        public static void InspectSave()
        {
            string path = EditorUtility.OpenFilePanel("Inspect saved document", "", "eqs");
            if (string.IsNullOrEmpty(path)) return;
            // Inspection changes only this detached session, never the live factory or input file.
            SaveDocumentSession.Publish(SaveDocumentCodec.Deserialize(File.ReadAllBytes(path)));
        }
    }
}

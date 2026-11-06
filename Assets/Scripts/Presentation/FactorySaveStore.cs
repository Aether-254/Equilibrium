using System;
using System.IO;
using System.Threading.Tasks;
using Equilibrium.Simulation.Save;

namespace Equilibrium.Presentation
{
    /// <summary>One local save slot. Only the primary replacement is the commit point.</summary>
    public sealed class FactorySaveStore
    {
        public string DirectoryPath { get; }
        public string PrimaryPath => Path.Combine(DirectoryPath, "save.eqs");
        public bool HasAnySave => File.Exists(PrimaryPath) || File.Exists(PathFor(1)) || File.Exists(PathFor(2)) || File.Exists(PathFor(3));

        public FactorySaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is required.", nameof(directory));
            DirectoryPath = Path.GetFullPath(directory);
        }

        public string PathFor(int backup)
        {
            if (backup < 0 || backup > 3) throw new ArgumentOutOfRangeException(nameof(backup));
            return Path.Combine(DirectoryPath, backup == 0 ? "save.eqs" : "save." + backup + ".eqs");
        }

        public SaveDocument Read(int backup = 0)
        {
            using (var stream = new FileStream(PathFor(backup), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > SaveDocumentCodec.MaxBytes) throw new InvalidDataException("SaveTooLargeOrEmpty");
                var bytes = new byte[(int)stream.Length]; int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0) throw new EndOfStreamException("SaveCorrupt");
                    offset += read;
                }
                return SaveDocumentCodec.Deserialize(bytes);
            }
        }

        public Task WriteAsync(SaveDocument detached) => Task.Run(() => Write(detached));

        public void Write(SaveDocument detached)
        {
            byte[] bytes = SaveDocumentCodec.Serialize(detached);
            Directory.CreateDirectory(DirectoryPath);
            // Exclusive OS handle serializes writers even if two runtimes or processes select this slot.
            using (var lease = new FileStream(Path.Combine(DirectoryPath, "save.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                string temporary = Path.Combine(DirectoryPath, "save.tmp");
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    if (File.Exists(PrimaryPath))
                    {
                        // Copy, rather than move, old backups: an error before Replace never destroys the primary.
                        if (File.Exists(PathFor(2))) CopyBackup(PathFor(2), PathFor(3));
                        if (File.Exists(PathFor(1))) CopyBackup(PathFor(1), PathFor(2));
                        File.Replace(temporary, PrimaryPath, PathFor(1));
                    }
                    else File.Move(temporary, PrimaryPath);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        private static void CopyBackup(string source, string destination)
        {
            string temporary = destination + ".tmp";
            try
            {
                File.Copy(source, temporary, true);
                using (var stream = new FileStream(temporary, FileMode.Open, FileAccess.Write, FileShare.None)) stream.Flush(true);
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}

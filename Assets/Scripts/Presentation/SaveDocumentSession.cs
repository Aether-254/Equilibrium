using System;
using Equilibrium.Simulation.Save;

namespace Equilibrium.Presentation
{
    /// <summary>Latest captured/loaded document for debug inspection. Gameplay owns when to publish a boundary capture.</summary>
    public static class SaveDocumentSession
    {
        private static byte[] _current;
        public static bool HasCurrent => _current != null;

        public static void Publish(SaveDocument document)
        {
            // Own the bytes so later mutations to a caller's document cannot change the inspection snapshot.
            byte[] captured = SaveDocumentCodec.Serialize(document);
            _current = captured;
        }

        public static SaveDocument ReadCurrent()
        {
            if (_current == null) throw new InvalidOperationException("No captured or loaded save document");
            return SaveDocumentCodec.Deserialize(_current);
        }

        public static void Clear() => _current = null;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Clear();
    }
}

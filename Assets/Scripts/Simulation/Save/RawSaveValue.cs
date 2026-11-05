using System;
using System.Buffers;
using MessagePack;

namespace Equilibrium.Simulation.Save
{
    /// <summary>One validated MessagePack value. Never normalizes integer widths, floats or extension payloads.</summary>
    public sealed class RawSaveValue
    {
        private readonly byte[] _bytes;
        private RawSaveValue(byte[] bytes) { _bytes = bytes; }
        public byte[] CopyBytes() => (byte[])_bytes.Clone();

        public static RawSaveValue FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > SaveDocumentCodec.MaxBytes)
                throw new ArgumentException("Invalid raw save value", nameof(bytes));
            var reader = new MessagePackReader(bytes);
            var value = Read(ref reader, SaveDocumentCodec.UncompressedOptions);
            if (!reader.End) throw new MessagePackSerializationException("Trailing raw value bytes");
            return value;
        }

        internal void Write(ref MessagePackWriter writer) => writer.WriteRaw(_bytes);

        internal static RawSaveValue Read(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var probe = reader.CreatePeekReader();
            SkipBounded(ref probe, options);
            return new RawSaveValue(reader.ReadRaw(probe.Consumed - reader.Consumed).ToArray());
        }

        private static void SkipBounded(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            options.Security.DepthStep(ref reader);
            try
            {
                switch (reader.NextMessagePackType)
                {
                    case MessagePackType.Array:
                        int count = reader.ReadArrayHeader();
                        CheckCount(count, (reader.Sequence.Length - reader.Consumed));
                        for (int i = 0; i < count; i++) SkipBounded(ref reader, options);
                        break;
                    case MessagePackType.Map:
                        int pairs = reader.ReadMapHeader();
                        CheckCount(pairs, (reader.Sequence.Length - reader.Consumed) / 2);
                        for (int i = 0; i < pairs; i++)
                        {
                            SkipBounded(ref reader, options);
                            SkipBounded(ref reader, options);
                        }
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
            finally { reader.Depth--; }
        }

        internal static void CheckCount(int count, long available)
        {
            if (count < 0 || count > 1_000_000 || count > available)
                throw new MessagePackSerializationException("Save collection exceeds bounds");
        }
    }
}

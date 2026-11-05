using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Equilibrium.Simulation.Save
{
    internal abstract class SaveMapFormatter<T> : IMessagePackFormatter<T> where T : SaveObject, new()
    {
        protected abstract int FieldCount { get; }
        protected abstract bool IsKnown(string key);
        protected abstract void WriteFields(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options);
        protected abstract bool ReadField(ref MessagePackReader reader, T value, string key, MessagePackSerializerOptions options);
        protected virtual void CheckRequired(HashSet<string> keys) { }
        protected virtual void CheckEnd(ref MessagePackReader reader) { }

        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            int inlineCount = 0;
            foreach (var pair in value.Extras)
            {
                if (pair.Key == null) throw new MessagePackSerializationException("Null Extras key");
                if (value.InlineExtras.Contains(pair.Key))
                {
                    if (IsKnown(pair.Key) || pair.Key == "extras") throw new MessagePackSerializationException("Extras collides with known member");
                    inlineCount++;
                }
            }
            writer.WriteMapHeader(FieldCount + 1 + inlineCount);
            WriteFields(ref writer, value, options);
            writer.Write("extras");
            writer.WriteMapHeader(value.Extras.Count - inlineCount);
            foreach (var pair in value.Extras)
                if (!value.InlineExtras.Contains(pair.Key))
                {
                    writer.Write(pair.Key);
                    SaveWire.WriteExtra(ref writer, pair.Value);
                }
            foreach (var pair in value.Extras)
                if (value.InlineExtras.Contains(pair.Key))
                {
                    writer.Write(pair.Key);
                    SaveWire.WriteExtra(ref writer, pair.Value);
                }
        }

        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            options.Security.DepthStep(ref reader);
            try
            {
                int count = reader.ReadMapHeader();
                RawSaveValue.CheckCount(count, (reader.Sequence.Length - reader.Consumed) / 2);
                var result = new T();
                var keys = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    if (key == null || !keys.Add(key)) throw new MessagePackSerializationException("Duplicate or null save key");
                    if (key == "extras") ReadExtras(ref reader, result, options);
                    else if (!ReadField(ref reader, result, key, options))
                    {
                        AddExtra(result, key, RawSaveValue.Read(ref reader, options));
                        result.InlineExtras.Add(key);
                    }
                }
                CheckRequired(keys);
                CheckEnd(ref reader);
                return result;
            }
            finally { reader.Depth--; }
        }

        private static void ReadExtras(ref MessagePackReader reader, T value, MessagePackSerializerOptions options)
        {
            options.Security.DepthStep(ref reader);
            try
            {
                int count = reader.ReadMapHeader();
                RawSaveValue.CheckCount(count, (reader.Sequence.Length - reader.Consumed) / 2);
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    if (key == null) throw new MessagePackSerializationException("Null Extras key");
                    AddExtra(value, key, RawSaveValue.Read(ref reader, options));
                }
            }
            finally { reader.Depth--; }
        }

        private static void AddExtra(T value, string key, RawSaveValue raw)
        {
            if (value.Extras.ContainsKey(key))
                throw new MessagePackSerializationException("Ambiguous duplicate Extras member: " + key);
            value.Extras.Add(key, raw);
        }
    }

    internal static class SaveWire
    {
        internal static void WriteRecord<T>(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
            => options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value, options);
        internal static T ReadRecord<T>(ref MessagePackReader reader, MessagePackSerializerOptions options)
            => options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);

        internal static void WriteList<T>(ref MessagePackWriter writer, List<T> values, MessagePackSerializerOptions options)
        {
            if (values == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(values.Count);
            foreach (var value in values) WriteRecord(ref writer, value, options);
        }

        internal static List<T> ReadList<T>(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            options.Security.DepthStep(ref reader);
            try
            {
                int count = reader.ReadArrayHeader();
                RawSaveValue.CheckCount(count, (reader.Sequence.Length - reader.Consumed));
                var values = new List<T>(count);
                for (int i = 0; i < count; i++) values.Add(ReadRecord<T>(ref reader, options));
                return values;
            }
            finally { reader.Depth--; }
        }

        internal static void WriteExtra(ref MessagePackWriter writer, object value)
        {
            // A closed set prevents typeless deserialization and arbitrary runtime object graphs.
            switch (value)
            {
                case null: writer.WriteNil(); break;
                case RawSaveValue raw: raw.Write(ref writer); break;
                case bool v: writer.Write(v); break;
                case string v: writer.Write(v); break;
                case byte[] v: writer.Write(v); break;
                case byte v: writer.Write(v); break;
                case sbyte v: writer.Write(v); break;
                case short v: writer.Write(v); break;
                case ushort v: writer.Write(v); break;
                case int v: writer.Write(v); break;
                case uint v: writer.Write(v); break;
                case long v: writer.Write(v); break;
                case ulong v: writer.Write(v); break;
                case float v: writer.Write(v); break;
                case double v: writer.Write(v); break;
                default: throw new MessagePackSerializationException("Extras requires a primitive or RawSaveValue");
            }
        }
    }
}

using System;
using System.Security.Cryptography;

namespace Equilibrium.Simulation
{
    /// <summary>Abstracts the OS CSPRNG so tests can inject failure. Production uses <see cref="OsEntropyProvider"/>.</summary>
    public interface IEntropyProvider
    {
        void Fill(Span<byte> buffer);
    }

    public sealed class OsEntropyProvider : IEntropyProvider
    {
        public static readonly OsEntropyProvider Instance = new OsEntropyProvider();
        public void Fill(Span<byte> buffer) => RandomNumberGenerator.Fill(buffer);
    }

    /// <summary>Raised when the OS entropy source fails twice; the tick must not proceed (ADR-0002).</summary>
    public sealed class TieBreakEntropyUnavailableException : Exception
    {
        public TieBreakEntropyUnavailableException(Exception inner)
            : base("OS entropy unavailable; refusing to seed tie-break with a predictable value.", inner) { }
    }

    /// <summary>
    /// ADR-0002: per tick, 32 bytes of OS entropy are mixed with the tick index through the
    /// SplitMix64 finalizer to seed a Xoshiro256** stream used for that tick's tie-break keys.
    /// State is never persisted. Not for use outside Resolve (pathfinding uses deterministic comparators).
    /// </summary>
    public sealed class TieBreakSource : ITieBreakSource
    {
        private readonly IEntropyProvider _entropy;
        private readonly byte[] _bytes = new byte[32];
        private ulong _s0, _s1, _s2, _s3;

        public TieBreakSource(IEntropyProvider entropy = null)
        {
            _entropy = entropy ?? OsEntropyProvider.Instance;
        }

        public void BeginTick(ulong tickIndex)
        {
            try
            {
                _entropy.Fill(_bytes);
            }
            catch (Exception first)
            {
                try { _entropy.Fill(_bytes); }
                catch (Exception) { throw new TieBreakEntropyUnavailableException(first); }
            }

            ulong e0 = BitConverter.ToUInt64(_bytes, 0);
            ulong e1 = BitConverter.ToUInt64(_bytes, 8);
            ulong e2 = BitConverter.ToUInt64(_bytes, 16);
            ulong e3 = BitConverter.ToUInt64(_bytes, 24);
            ulong seed = SplitMix64.Finalize(e0 ^ e1 ^ e2 ^ e3 ^ tickIndex);

            // Expand the seed into four non-zero state words.
            ulong z = seed;
            _s0 = SplitMix64.Next(ref z);
            _s1 = SplitMix64.Next(ref z);
            _s2 = SplitMix64.Next(ref z);
            _s3 = SplitMix64.Next(ref z);
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s0 = 1;
        }

        public ulong NextKey()
        {
            // xoshiro256** (Blackman & Vigna)
            ulong result = RotL(_s1 * 5, 7) * 9;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotL(_s3, 45);
            return result;
        }

        private static ulong RotL(ulong x, int k) => (x << k) | (x >> (64 - k));
    }

    internal static class SplitMix64
    {
        public static ulong Finalize(ulong z)
        {
            z ^= z >> 30; z *= 0xBF58476D1CE4E5B9UL;
            z ^= z >> 27; z *= 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return z;
        }

        public static ulong Next(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            return Finalize(state);
        }
    }
}

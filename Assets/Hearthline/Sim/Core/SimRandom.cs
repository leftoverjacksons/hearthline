using System;
using System.Collections.Generic;

namespace Hearthline.Sim
{
    /// <summary>Serializable generator state. Lives inside <see cref="SimState"/> so saves resume the same sequence.</summary>
    public sealed class RngState
    {
        public ulong State;
        public ulong Inc;
    }

    /// <summary>
    /// The only source of randomness in the simulation (PCG32). It reads and writes an
    /// <see cref="RngState"/> owned by the sim state, so the sequence survives save and load.
    /// </summary>
    public sealed class SimRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private readonly RngState _s;

        public SimRandom(RngState state)
        {
            _s = state ?? throw new ArgumentNullException(nameof(state));
        }

        public static RngState Seed(ulong seed, ulong stream = 54UL)
        {
            var s = new RngState { State = 0UL, Inc = (stream << 1) | 1UL };
            var rng = new SimRandom(s);
            rng.NextUInt();
            unchecked { s.State += seed; }
            rng.NextUInt();
            return s;
        }

        public uint NextUInt()
        {
            unchecked
            {
                ulong old = _s.State;
                _s.State = old * Multiplier + _s.Inc;
                uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
                int rot = (int)(old >> 59);
                return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
            }
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive), without modulo bias.</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            uint bound = (uint)((long)maxExclusive - minInclusive);
            uint threshold = unchecked((uint)(-(int)bound)) % bound;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(minInclusive + (long)(r % bound));
            }
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble()
        {
            return NextUInt() * (1.0 / 4294967296.0);
        }

        public bool Chance(double p)
        {
            return NextDouble() < p;
        }

        /// <summary>Index chosen in proportion to its weight. Non-positive weights are never chosen.</summary>
        public int PickWeighted(IReadOnlyList<double> weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++) if (weights[i] > 0) total += weights[i];
            if (total <= 0) throw new ArgumentException("No positive weights.", nameof(weights));
            double roll = NextDouble() * total;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0) continue;
                last = i;
                roll -= weights[i];
                if (roll < 0) return i;
            }
            return last;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Hearthline.Sim
{
    /// <summary>How worn one square metre of ground is (docs/specs/trampled-grass.md).</summary>
    public sealed class WearCell
    {
        /// <summary>0 untouched .. 1 bare, packed dirt.</summary>
        public double W;
        /// <summary>The in-game minute (day * 1440 + minute of day) it was last trodden by someone walking.</summary>
        public long Stamp = long.MinValue;
    }

    /// <summary>
    /// Desire paths: where the ground has been worn by feet. Kept sparse: only worn cells are stored.
    /// Cells are whole metres of world x and z.
    /// </summary>
    public sealed class WearState
    {
        /// <summary>Cell key (<see cref="Key"/>) -> wear.</summary>
        public SortedDictionary<long, WearCell> Cells = new SortedDictionary<long, WearCell>();
        /// <summary>
        /// The household's daily routes at home, by job id (or <see cref="WearRules.IdleRoute"/>): world x, z
        /// pairs from the dwelling door to where the work is done. Registered by the Bridge from the scene.
        /// </summary>
        public SortedDictionary<string, List<double>> Routes = new SortedDictionary<string, List<double>>();

        public static long Key(int x, int z) => ((long)x << 32) | (uint)z;
        public static int KeyX(long key) => (int)(key >> 32);
        public static int KeyZ(long key) => (int)(uint)(key & 0xffffffffL);
        public static long KeyAt(double x, double z) => Key((int)Math.Floor(x), (int)Math.Floor(z));

        public double At(double x, double z) => Cells.TryGetValue(KeyAt(x, z), out WearCell c) ? c.W : 0;
    }

    /// <summary>Tuning for wear. Values from the spec.</summary>
    public sealed class WearRules
    {
        public const string IdleRoute = "idle";
        public const int MinutesPerDay = 24 * 60;

        /// <summary>Each pass: w += (1 - w) * GainPerPass.</summary>
        public double GainPerPass = 0.07;
        /// <summary>Daily: w *= 1 - decay, by season.</summary>
        public double DecaySpring = 0.03, DecaySummer = 0.03, DecayAutumn = 0.015, DecayWinter = 0.003;
        /// <summary>Cells below this are dropped.</summary>
        public double Prune = 0.02;
        /// <summary>Passes a household member makes along their route each day (there and back).</summary>
        public int RoutePassesPerDay = 2;
        /// <summary>Routes are walked in steps this long, in metres.</summary>
        public double RouteStep = 0.5;
        /// <summary>What shows (presentation reads these): short grass, dirt showing, bare path.</summary>
        public double ShortFrom = 0.25, DirtFrom = 0.55, BareFrom = 0.8;
        /// <summary>Most points one Tread command may carry, and most points in one route.</summary>
        public int MaxPoints = 4096;

        public double DecayFor(Season season) =>
            season == Season.Spring ? DecaySpring : season == Season.Summer ? DecaySummer : season == Season.Autumn ? DecayAutumn : DecayWinter;
    }
}

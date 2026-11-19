using System.Collections.Generic;

namespace Hearthline.Sim
{
    /// <summary>
    /// Someone walked over the ground (the Bridge sends one per metre or so the player walks on foot).
    /// Each cell counts at most once per in-game minute, so standing still wears no hole.
    /// </summary>
    public sealed class Tread : Command
    {
        /// <summary>World x, z pairs walked over.</summary>
        public List<double> Points = new List<double>();
        /// <summary>Minute of the in-game day, 0..1439.</summary>
        public int MinuteOfDay;
        /// <summary>Passes per cell. More than 1 only from the cheat panel.</summary>
        public int Passes = 1;

        internal override string Apply(SimContext ctx)
        {
            if (!WearSystem.ValidPoints(Points, ctx.Wear.MaxPoints)) return "tread needs x, z pairs";
            if (MinuteOfDay < 0 || MinuteOfDay >= WearRules.MinutesPerDay) return "minute of day must be 0..1439";
            if (Passes < 1 || Passes > 200) return "passes must be 1..200";
            long stamp = (long)ctx.State.Day * WearRules.MinutesPerDay + MinuteOfDay;
            bool any = false;
            var seen = new HashSet<long>();
            for (int i = 0; i + 1 < Points.Count; i += 2)
            {
                long key = WearState.KeyAt(Points[i], Points[i + 1]);
                if (seen.Add(key) && WearSystem.Pass(ctx, key, stamp, Passes)) any = true;
            }
            if (any) ctx.Emit(new WearChanged { Day = ctx.State.Day });
            return null;
        }
    }

    /// <summary>
    /// The household's routes at home (job id or "idle" -> x, z pairs from the dwelling door), found by the
    /// Bridge from the scene. Replaces the set; unchanged routes change nothing.
    /// </summary>
    public sealed class SetHomeRoutes : Command
    {
        public Dictionary<string, List<double>> Routes = new Dictionary<string, List<double>>();

        internal override string Apply(SimContext ctx)
        {
            if (Routes == null) return "no routes";
            foreach (KeyValuePair<string, List<double>> r in Routes)
            {
                if (string.IsNullOrEmpty(r.Key)) return "a route needs a job id";
                if (r.Key != WearRules.IdleRoute && !ctx.Content.Jobs.ContainsKey(r.Key)) return "no job '" + r.Key + "'";
                if (!WearSystem.ValidPoints(r.Value, ctx.Wear.MaxPoints)) return "route '" + r.Key + "' needs x, z pairs";
            }
            var routes = new SortedDictionary<string, List<double>>();
            foreach (KeyValuePair<string, List<double>> r in Routes) routes[r.Key] = new List<double>(r.Value);
            ctx.State.Wear.Routes = routes;
            return null;
        }
    }

    /// <summary>Cheat panel: grow the ground back as if days had passed, without passing them.</summary>
    public sealed class AgeWear : Command
    {
        public int Days = 30;

        internal override string Apply(SimContext ctx)
        {
            if (Days < 1) return "days must be at least 1";
            WearSystem.Decay(ctx, Days, new GameDate(ctx.State.Day).Season);
            ctx.Emit(new WearChanged { Day = ctx.State.Day });
            return null;
        }
    }

    /// <summary>Cheat panel: the land forgets every path.</summary>
    public sealed class ClearWear : Command
    {
        internal override string Apply(SimContext ctx)
        {
            ctx.State.Wear.Cells.Clear();
            ctx.Emit(new WearChanged { Day = ctx.State.Day });
            return null;
        }
    }
}

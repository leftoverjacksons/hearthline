using System;
using System.Collections.Generic;

namespace Hearthline.Sim
{
    /// <summary>Wear on the ground: treading, the household's daily walks, and growing back (docs/specs/trampled-grass.md).</summary>
    internal static class WearSystem
    {
        /// <summary>One pass over a cell. Returns false if it was already trodden in that same minute.</summary>
        public static bool Pass(SimContext ctx, long key, long stamp, int passes)
        {
            WearState wear = ctx.State.Wear;
            if (!wear.Cells.TryGetValue(key, out WearCell cell))
            {
                cell = new WearCell();
                wear.Cells[key] = cell;
            }
            else if (stamp != long.MinValue && cell.Stamp == stamp) return false;
            for (int i = 0; i < passes; i++) cell.W += (1 - cell.W) * ctx.Wear.GainPerPass;
            if (stamp != long.MinValue) cell.Stamp = stamp;
            return true;
        }

        /// <summary>The cells a polyline of x, z pairs crosses, in order, each once.</summary>
        public static List<long> CellsAlong(IList<double> points, double step)
        {
            var cells = new List<long>();
            var seen = new HashSet<long>();
            void Add(double x, double z) { long k = WearState.KeyAt(x, z); if (seen.Add(k)) cells.Add(k); }
            if (points.Count < 2) return cells;
            Add(points[0], points[1]);
            for (int i = 2; i + 1 < points.Count; i += 2)
            {
                double x0 = points[i - 2], z0 = points[i - 1], x1 = points[i], z1 = points[i + 1];
                double len = Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
                int n = Math.Max(1, (int)Math.Ceiling(len / step));
                for (int s = 1; s <= n; s++) Add(x0 + (x1 - x0) * s / n, z0 + (z1 - z0) * s / n);
            }
            return cells;
        }

        public static bool ValidPoints(IList<double> points, int max)
        {
            if (points == null || points.Count < 2 || points.Count % 2 != 0 || points.Count > max * 2) return false;
            foreach (double v in points) if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e6) return false;
            return true;
        }

        /// <summary>Each day: the household walks to their work and back, then the ground grows back a little.</summary>
        public static void DailyTick(SimContext ctx)
        {
            WearState wear = ctx.State.Wear;
            int changed = 0;
            foreach (Character c in HomeSystem.LivingMembers(ctx))
            {
                if (c.Id == ctx.State.Household.HeadId || c.Downed || TripSystem.InParty(ctx.State, c)) continue;
                if (!wear.Routes.TryGetValue(c.JobId ?? WearRules.IdleRoute, out List<double> route)
                    && !wear.Routes.TryGetValue(WearRules.IdleRoute, out route)) continue;
                foreach (long key in CellsAlong(route, ctx.Wear.RouteStep))
                {
                    Pass(ctx, key, long.MinValue, ctx.Wear.RoutePassesPerDay);
                    changed++;
                }
            }
            changed += Decay(ctx, 1, new GameDate(ctx.State.Day).Season);
            if (changed > 0) ctx.Emit(new WearChanged { Day = ctx.State.Day });
        }

        /// <summary>Grows the ground back by <paramref name="days"/> days of <paramref name="season"/>. Returns cells touched.</summary>
        public static int Decay(SimContext ctx, int days, Season season)
        {
            WearState wear = ctx.State.Wear;
            if (wear.Cells.Count == 0) return 0;
            double keep = Math.Pow(1 - ctx.Wear.DecayFor(season), days);
            var drop = new List<long>();
            foreach (KeyValuePair<long, WearCell> kv in wear.Cells)
            {
                kv.Value.W *= keep;
                if (kv.Value.W < ctx.Wear.Prune) drop.Add(kv.Key);
            }
            foreach (long k in drop) wear.Cells.Remove(k);
            return wear.Cells.Count + drop.Count;
        }
    }
}

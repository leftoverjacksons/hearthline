using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim.World
{
    /// <summary>A place in the generated land: which kind, where, and which way it faces.</summary>
    public sealed class PlacedPlace
    {
        /// <summary>Stable id: the kind for the first of a kind ("chapel"), then "chapel_2", ...</summary>
        public string Id;
        public string Kind;
        public Vec2 P;
        public double Facing;
    }

    /// <summary>
    /// Chooses a kind of place for each ruin site from content siting (docs/specs/ruins-and-places.md), and adds
    /// the Elder Works. Each kind is used once before any is used twice; within that, the best-fitting
    /// site and kind pair is taken first. Deterministic: it depends only on the map and the content.
    /// </summary>
    public static class PlaceLayout
    {
        public static List<PlacedPlace> Assign(WorldMap map, IEnumerable<PlaceDef> defs)
        {
            var ruinKinds = new List<PlaceDef>();
            PlaceDef elder = null;
            foreach (PlaceDef d in defs)
            {
                if (d.Siting.Fixed == null) ruinKinds.Add(d);
                else if (d.Siting.Fixed == "elder_works") elder = d;
            }
            ruinKinds.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            var placed = new List<PlacedPlace>();
            var used = new Dictionary<string, int>();
            var open = new List<RuinSite>(map.Sites);
            while (open.Count > 0 && ruinKinds.Count > 0)
            {
                var round = new List<PlaceDef>(ruinKinds);
                while (open.Count > 0 && round.Count > 0)
                {
                    double best = double.MaxValue;
                    RuinSite bestSite = null;
                    PlaceDef bestKind = null;
                    foreach (RuinSite site in open)
                        foreach (PlaceDef kind in round)
                        {
                            double score = Misfit(kind.Siting, site);
                            if (score < best) { best = score; bestSite = site; bestKind = kind; }
                        }
                    used.TryGetValue(bestKind.Id, out int n);
                    used[bestKind.Id] = ++n;
                    placed.Add(new PlacedPlace { Id = n == 1 ? bestKind.Id : bestKind.Id + "_" + n, Kind = bestKind.Id, P = bestSite.P, Facing = bestSite.Facing });
                    open.Remove(bestSite);
                    round.Remove(bestKind);
                }
            }
            placed.Sort((a, b) => map.RuinSites.FindIndex(r => r.X == a.P.X && r.Z == a.P.Z).CompareTo(map.RuinSites.FindIndex(r => r.X == b.P.X && r.Z == b.P.Z)));

            if (elder != null)
            {
                Vec2 e = map.ElderWorks;
                placed.Add(new PlacedPlace { Id = elder.Id, Kind = elder.Id, P = e, Facing = Math.Atan2(-e.X, -e.Z) * 180 / Math.PI });
            }
            return placed;
        }

        /// <summary>How badly a site breaks a kind's siting: 0 when it fits, more the further outside the limits.</summary>
        public static double Misfit(PlaceSiting s, RuinSite site)
        {
            double m = 0;
            if (s.SlopeMax.HasValue) m += Math.Max(0, site.Slope - s.SlopeMax.Value) * 200;
            if (s.RoadMin.HasValue) m += Math.Max(0, s.RoadMin.Value - site.Road);
            if (s.RoadMax.HasValue) m += Math.Max(0, site.Road - s.RoadMax.Value) + site.Road * 0.05;
            if (s.WaterMin.HasValue) m += Math.Max(0, s.WaterMin.Value - site.Water);
            if (s.WaterMax.HasValue) m += Math.Max(0, site.Water - s.WaterMax.Value) + site.Water * 0.05;
            if (s.HomeMin.HasValue) m += Math.Max(0, s.HomeMin.Value - site.Home);
            if (s.Rise) m += Math.Max(0, 3 - site.Rise) * 3;
            return m;
        }
    }
}

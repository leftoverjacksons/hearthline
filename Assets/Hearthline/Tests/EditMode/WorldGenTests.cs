using System.Linq;
using Hearthline.Sim.World;
using NUnit.Framework;

namespace Hearthline.Tests
{
    /// <summary>The world generator (docs/adr/0024-world-generator.md): the land, then its water, then roads and places.</summary>
    public class WorldGenTests
    {
        private static WorldMap _map;
        private static WorldMap Map => _map ??= WorldGen.Generate(new WorldSettings { Seed = 1 });

        [Test]
        public void TheSameSeedGivesTheSameWorld()
        {
            WorldMap again = WorldGen.Generate(new WorldSettings { Seed = 1 });
            Assert.That(again.Height, Is.EqualTo(Map.Height));
            Assert.That(again.Streams.Count, Is.EqualTo(Map.Streams.Count));
            Assert.That(again.Road.Count, Is.EqualTo(Map.Road.Count));
        }

        [Test]
        public void AMountainRisesToTheNorth()
        {
            double north = Map.HeightAt(0, 520), home = Map.HeightAt(0, 0), south = Map.HeightAt(0, -400);
            Assert.That(north - home, Is.GreaterThan(100), "The mountain stands well above the homestead.");
            Assert.That(south, Is.LessThan(home + 15), "The valley opens to the south.");
        }

        [Test]
        public void TheHomesteadSitsOnFlatGroundNearWater()
        {
            for (double a = 0; a < 6.28; a += 0.5)
            {
                double x = System.Math.Cos(a) * Map.Settings.HomeRadius * 0.8, z = System.Math.Sin(a) * Map.Settings.HomeRadius * 0.8;
                Assert.That(System.Math.Abs(Map.HeightAt(x, z) - Map.HomeHeight), Is.LessThan(0.6), "Flat bench at " + x + "," + z);
            }
            double water = Map.DistanceToWater(0, 0, out _);
            Assert.That(water, Is.InRange(Map.Settings.HomeWaterMin, Map.Settings.HomeWaterMax));
        }

        [Test]
        public void StreamsRunDownhillAndARiverForms()
        {
            Assert.That(Map.Streams.Count, Is.GreaterThan(3));
            Assert.That(Map.Streams.Any(s => s.IsRiver), Is.True);
            foreach (Waterway s in Map.Streams)
                for (int i = 1; i < s.Points.Count; i++)
                    Assert.That(s.Points[i].Surface, Is.LessThanOrEqualTo(s.Points[i - 1].Surface + 1e-6), "Water never runs uphill.");
        }

        [Test]
        public void TheRoadCrossesTheValleyAndBridgesItsWater()
        {
            Assert.That(Map.Road.First().P.X, Is.LessThan(-500));
            Assert.That(Map.Road.Last().P.X, Is.GreaterThan(500));
            Assert.That(Map.Bridges.Count, Is.GreaterThan(0), "The road meets water somewhere.");
            // Wherever the road is over water, a bridge spans it.
            foreach (PathPoint q in Map.Road)
            {
                if (Map.DistanceToWater(q.P.X, q.P.Z, out _) >= 1.0) continue;
                bool spanned = Map.Bridges.Any(b => WorldMap.DistanceToPath(new System.Collections.Generic.List<PathPoint> { new PathPoint { P = b.From }, new PathPoint { P = b.To } }, q.P.X, q.P.Z) < 3);
                Assert.That(spanned, Is.True, "Road over water at " + q.P.X + "," + q.P.Z + " has no bridge.");
            }
        }

        [Test]
        public void ATrackLeadsHomeAndRuinsStandApart()
        {
            Assert.That(Map.Track.Count, Is.GreaterThan(2));
            Assert.That(Map.Track.First().P.Length, Is.LessThan(Map.Settings.HomeRadius + 5));
            Assert.That(WorldMap.DistanceToPath(Map.Road, Map.Track.Last().P.X, Map.Track.Last().P.Z), Is.LessThan(8));
            Assert.That(Map.RuinSites.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void AreasComeFromWhatIsThere()
        {
            Assert.That(Map.AreaAt(0, 0), Is.EqualTo("homestead"));
            Assert.That(Map.AreaAt(Map.RuinSites[0].X, Map.RuinSites[0].Z), Is.EqualTo("ruin"));
            var seen = new System.Collections.Generic.HashSet<string>();
            for (double x = -290; x <= 290; x += 10)
                for (double z = -290; z <= 290; z += 10)
                    seen.Add(Map.AreaAt(x, z));
            Assert.That(seen, Has.Member("woods"));
            Assert.That(seen, Has.Member("field"));
            Assert.That(seen, Has.Member("road"));
            Assert.That(seen, Has.Member("creek"));
        }

        [Test]
        public void TheLandIsForestWithClearingsWherePeopleMadeThem()
        {
            // ADR 0027: southern Appalachia is woods almost everywhere.
            int cells = 0, wooded = 0, coves = 0;
            for (double x = -280; x <= 280; x += 10)
                for (double z = -280; z <= 280; z += 10)
                {
                    if (Map.DistanceToWater(x, z, out _) < 6 || WorldMap.DistanceToPath(Map.Road, x, z) < 12) continue;
                    cells++;
                    if (Map.ForestAt(x, z) > 0.5) wooded++;
                    if (Map.IsDeepCove(x, z)) coves++;
                }
            Assert.That(wooded / (double)cells, Is.GreaterThan(0.75), "Mostly under trees.");
            Assert.That(coves, Is.GreaterThan(0), "Some damp, deep coves.");
            Assert.That(coves / (double)cells, Is.LessThan(0.2), "But deep woods (Thin 1) stay rare.");
            Assert.That(Map.ForestAt(0, 0), Is.LessThan(0.1), "The homestead is a clearing.");
            foreach (Vec2 r in Map.RuinSites) Assert.That(Map.ForestAt(r.X, r.Z), Is.LessThan(0.35), "Old fields around a ruin.");
            Assert.That(Map.ForestAt(Map.ElderWorks.X, Map.ElderWorks.Z), Is.LessThan(0.2), "The Elder Works pad is open.");
            Assert.That(Map.ForestAt(0, 230), Is.GreaterThan(0.4).Or.LessThan(0.1), "Up the ridges: forest, or now and then a bald.");
        }
    }
}

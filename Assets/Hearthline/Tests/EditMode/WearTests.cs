using System.Collections.Generic;
using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    /// <summary>Desire paths (docs/specs/trampled-grass.md, ADR 0034).</summary>
    public class WearTests
    {
        private static void Tread(Simulation sim, double x, double z, int minute, int passes = 1) =>
            TestSupport.Ok(sim.Execute(new Tread { Points = new List<double> { x, z }, MinuteOfDay = minute, Passes = passes }));

        private static Character Join(Simulation sim, string name)
        {
            TestSupport.Ok(sim.Execute(new AddHouseholdMember { GivenName = name, Surname = "Pruitt", AgeYears = 30 }));
            return sim.State.Characters[sim.State.Characters.Count - 1];
        }

        private static Dictionary<string, List<double>> WoodpileRoute() =>
            new Dictionary<string, List<double>> { ["woodcutter"] = new List<double> { 0.5, 0.5, 10.5, 0.5 } };

        [Test]
        public void WearRisesWithPassesWithDiminishingReturns()
        {
            Simulation sim = TestSupport.NewSim();
            var seen = new List<double>();
            for (int m = 0; m < 40; m++) { Tread(sim, 3.2, 4.7, m); seen.Add(sim.WearAt(3.9, 4.1)); }
            Assert.That(seen[0], Is.EqualTo(0.07).Within(1e-9));
            for (int i = 1; i < seen.Count; i++)
            {
                Assert.That(seen[i], Is.GreaterThan(seen[i - 1]));
                Assert.That(seen[i] - seen[i - 1], Is.LessThan(seen[i - 1] - (i > 1 ? seen[i - 2] : 0)), "each pass adds less");
            }
            Assert.That(seen[14], Is.GreaterThan(sim.Wear.ShortFrom), "about 15 passes make a visible path");
            Assert.That(seen[39], Is.GreaterThan(sim.Wear.BareFrom), "about 40 make bare dirt");
            Assert.That(sim.WearAt(5.5, 4.5), Is.EqualTo(0), "the next cell is untouched");
        }

        [Test]
        public void TreadsInTheSameMinuteCountOnce()
        {
            Simulation sim = TestSupport.NewSim();
            Tread(sim, 1, 1, 600);
            Tread(sim, 1.4, 1.6, 600);
            Assert.That(sim.WearAt(1, 1), Is.EqualTo(0.07).Within(1e-9));
            Tread(sim, 1, 1, 601);
            Assert.That(sim.WearAt(1, 1), Is.GreaterThan(0.07));
        }

        [Test]
        public void TreadRejectsBadInput()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Rejected(sim.Execute(new Tread { Points = new List<double> { 1 } }), "x, z pairs");
            TestSupport.Rejected(sim.Execute(new Tread { Points = new List<double> { 1, 1 }, MinuteOfDay = 1440 }), "minute");
            TestSupport.Rejected(sim.Execute(new SetHomeRoutes { Routes = new Dictionary<string, List<double>> { ["juggler"] = new List<double> { 0, 0, 1, 1 } } }), "no job");
        }

        [Test]
        public void WearGrowsBackFastestInSummerAndBarelyInWinter()
        {
            double Left(int startDay)
            {
                Simulation sim = TestSupport.NewSim();
                sim.State.Day = startDay;
                Tread(sim, 0, 0, 0, 20);
                double before = sim.WearAt(0, 0);
                TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 10 }));
                return sim.WearAt(0, 0) / before;
            }
            double summer = Left(GameDate.DaysPerSeason + 2), autumn = Left(GameDate.DaysPerSeason * 2 + 2), winter = Left(GameDate.DaysPerSeason * 3 + 2);
            Assert.That(summer, Is.EqualTo(System.Math.Pow(0.97, 10)).Within(1e-9));
            Assert.That(autumn, Is.GreaterThan(summer));
            Assert.That(winter, Is.GreaterThan(autumn));
            Assert.That(winter, Is.GreaterThan(0.95));
        }

        [Test]
        public void SmallWearIsPruned()
        {
            Simulation sim = TestSupport.NewSim();
            Tread(sim, 0, 0, 0);
            TestSupport.Ok(sim.Execute(new AgeWear { Days = 60 }));
            Assert.That(sim.State.Wear.Cells.Count, Is.EqualTo(0));
        }

        [Test]
        public void HouseholdMembersWearTheirRouteEachDay()
        {
            Simulation sim = TestSupport.NewSim();
            Character hand = Join(sim, "Eli");
            TestSupport.Ok(sim.Execute(new SetHomeRoutes { Routes = WoodpileRoute() }));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 3 }));
            Assert.That(sim.WearAt(5, 0.5), Is.EqualTo(0), "no work, no idle route: no path");

            TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = hand.Id, JobId = "woodcutter" }));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 30 }));
            for (double x = 0.5; x <= 10.5; x += 1) Assert.That(sim.WearAt(x, 0.5), Is.GreaterThan(sim.Wear.BareFrom), "a bare path at " + x);
            Assert.That(sim.WearAt(5, 2.5), Is.EqualTo(0), "only along the route");

            // The work stops; the path grows back.
            TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = hand.Id, JobId = null }));
            double worn = sim.WearAt(5, 0.5);
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 20 }));
            Assert.That(sim.WearAt(5, 0.5), Is.LessThan(worn * 0.7));
        }

        [Test]
        public void TheHeadAndThePartyAwayWearNothingAtHome()
        {
            Simulation sim = TestSupport.NewSim();
            var routes = WoodpileRoute();
            routes[WearRules.IdleRoute] = new List<double> { 0.5, 0.5, 0.5, 10.5 };
            TestSupport.Ok(sim.Execute(new SetHomeRoutes { Routes = routes }));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 5 }));
            Assert.That(sim.State.Wear.Cells.Count, Is.EqualTo(0), "only the head lives here, and the head is the player");
            Join(sim, "Eli");
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 1 }));
            Assert.That(sim.WearAt(0.5, 5), Is.GreaterThan(0), "without work, the idle route");
        }

        [Test]
        public void SameCommandsGiveTheSameMapAndItSurvivesSaveAndLoad()
        {
            string Run()
            {
                Simulation sim = TestSupport.NewSim();
                Character hand = Join(sim, "Eli");
                TestSupport.Ok(sim.Execute(new SetHomeRoutes { Routes = WoodpileRoute() }));
                TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = hand.Id, JobId = "woodcutter" }));
                for (int m = 0; m < 10; m++) Tread(sim, -4 + m * 0.7, 3, m);
                TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 12 }));
                return sim.ToJson();
            }
            string a = Run();
            Assert.That(Run(), Is.EqualTo(a));
            Simulation loaded = Simulation.FromJson(a, TestSupport.Content);
            Assert.That(loaded.ToJson(), Is.EqualTo(a));
            Assert.That(loaded.WearAt(5, 0.5), Is.GreaterThan(0.5));
            Assert.That(loaded.State.Wear.Routes.ContainsKey("woodcutter"));
            Assert.That(loaded.WearAt(-4, 3), Is.GreaterThan(0), "negative cells keep their place");
        }

        [Test]
        public void CellKeysRoundTripNegativeCoordinates()
        {
            foreach (var (x, z) in new[] { (0, 0), (-1, -1), (-300, 299), (299, -300), (int.MaxValue, int.MinValue) })
            {
                long k = WearState.Key(x, z);
                Assert.That(WearState.KeyX(k), Is.EqualTo(x));
                Assert.That(WearState.KeyZ(k), Is.EqualTo(z));
            }
        }
    }
}

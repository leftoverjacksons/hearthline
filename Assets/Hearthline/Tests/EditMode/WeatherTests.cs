using System.Collections.Generic;
using System.Linq;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using NUnit.Framework;

namespace Hearthline.Tests
{
    /// <summary>The weather (docs/specs/life-and-weather.md): planned each day from a seed and the season, saved, and asked for by events.</summary>
    public class WeatherTests
    {
        private static string Describe(Simulation sim) =>
            string.Join(",", sim.State.Weather.Today.Select(s => s.FromHour + ":" + s.Kind));

        [Test]
        public void TheSameSeedGivesTheSameWeather()
        {
            Simulation a = TestSupport.NewSim(5), b = TestSupport.NewSim(5);
            var runA = new List<string>(); var runB = new List<string>();
            for (int d = 0; d < 60; d++)
            {
                runA.Add(Describe(a)); runB.Add(Describe(b));
                TestSupport.Ok(a.Execute(new AdvanceDays()));
                TestSupport.Ok(b.Execute(new AdvanceDays()));
            }
            Assert.That(runA, Is.EqualTo(runB));
            Assert.That(runA.Distinct().Count(), Is.GreaterThan(5), "It changes from day to day.");
        }

        // Fraction of the day's daylight hours (6 to 20) with each weather, over many days of one season.
        private static Dictionary<WeatherKind, double> Climate(Season season)
        {
            var def = TestSupport.Content.Weather;
            var hours = new Dictionary<WeatherKind, double>();
            WeatherKind yesterday = WeatherKind.Clear;
            int total = 0;
            for (int i = 0; i < 3000; i++)
            {
                int day = (int)season * GameDate.DaysPerSeason + i % GameDate.DaysPerSeason + (i / GameDate.DaysPerSeason) * GameDate.DaysPerYear;
                var weather = new WeatherState { Today = WeatherSystem.Plan(def, 99, day, yesterday) };
                for (int h = 6; h < 20; h++)
                {
                    WeatherKind k = WeatherSystem.At(weather, h);
                    hours[k] = (hours.TryGetValue(k, out double n) ? n : 0) + 1;
                    total++;
                }
                yesterday = WeatherSystem.Last(weather);
            }
            foreach (WeatherKind k in hours.Keys.ToList()) hours[k] /= total;
            return hours;
        }

        private static double Of(Dictionary<WeatherKind, double> c, params WeatherKind[] kinds) => kinds.Sum(k => c.TryGetValue(k, out double v) ? v : 0);

        [Test]
        public void EachSeasonHasItsOwnWeather()
        {
            var spring = Climate(Season.Spring); var summer = Climate(Season.Summer);
            var autumn = Climate(Season.Autumn); var winter = Climate(Season.Winter);
            Assert.That(Of(spring, WeatherKind.Rain), Is.GreaterThan(Of(summer, WeatherKind.Rain)), "Spring is wetter than summer.");
            Assert.That(Of(summer, WeatherKind.Clear), Is.GreaterThan(Of(winter, WeatherKind.Clear)), "Summer is clearer than winter.");
            Assert.That(Of(summer, WeatherKind.Storm), Is.GreaterThan(Of(winter, WeatherKind.Storm)), "Summer has the storms.");
            Assert.That(Of(autumn, WeatherKind.Fog), Is.GreaterThan(Of(summer, WeatherKind.Fog)), "Autumn has the fog.");
            Assert.That(Of(winter, WeatherKind.Overcast), Is.GreaterThan(Of(summer, WeatherKind.Overcast)), "Winter is grey.");
        }

        [Test]
        public void StormsBlowOverWithinHalfADay()
        {
            var def = TestSupport.Content.Weather;
            int storms = 0;
            WeatherKind yesterday = WeatherKind.Clear;
            for (int day = 0; day < 4000; day++)
            {
                List<WeatherSpell> today = WeatherSystem.Plan(def, 7, day, yesterday);
                Assert.That(today[0].FromHour, Is.EqualTo(0));
                for (int i = 0; i < today.Count; i++)
                {
                    if (i > 0) Assert.That(today[i].FromHour, Is.GreaterThan(today[i - 1].FromHour));
                    if (today[i].Kind != WeatherKind.Storm) continue;
                    storms++;
                    double end = i + 1 < today.Count ? today[i + 1].FromHour : 24;
                    Assert.That(end - today[i].FromHour, Is.LessThanOrEqualTo(12), "Day " + day + ": " + string.Join(",", today.Select(s => s.FromHour + ":" + s.Kind)));
                }
                if (yesterday == WeatherKind.Storm) Assert.That(today[0].Kind == WeatherKind.Storm && today.Count == 1, Is.False);
                yesterday = today[today.Count - 1].Kind;
            }
            Assert.That(storms, Is.GreaterThan(50));
        }

        [Test]
        public void WeatherSurvivesTheSave()
        {
            Simulation sim = TestSupport.NewSim(3);
            for (int d = 0; d < 9; d++) TestSupport.Ok(sim.Execute(new AdvanceDays()));
            Simulation loaded = Simulation.FromJson(sim.ToJson(), TestSupport.Content);
            Assert.That(Describe(loaded), Is.EqualTo(Describe(sim)));
            Assert.That(loaded.ToJson(), Is.EqualTo(sim.ToJson()));
            TestSupport.Ok(sim.Execute(new AdvanceDays()));
            TestSupport.Ok(loaded.Execute(new AdvanceDays()));
            Assert.That(Describe(loaded), Is.EqualTo(Describe(sim)), "Tomorrow comes out the same too.");
        }

        [Test]
        public void SettingTheWeatherHoldsFromThatHour()
        {
            Simulation sim = TestSupport.NewSim();
            CommandResult r = sim.Execute(new SetWeather { Kind = WeatherKind.Rain, FromHour = 14 });
            TestSupport.Ok(r);
            WeatherChanged changed = r.Events.OfType<WeatherChanged>().Single();
            Assert.That(changed.To, Is.EqualTo(WeatherKind.Rain));
            Assert.That(changed.Hour, Is.EqualTo(14));
            Assert.That(sim.WeatherAt(14), Is.EqualTo(WeatherKind.Rain));
            Assert.That(sim.WeatherAt(23.5), Is.EqualTo(WeatherKind.Rain));
            TestSupport.Ok(sim.Execute(new SetWeather { Kind = WeatherKind.Storm }));
            Assert.That(sim.WeatherAt(0), Is.EqualTo(WeatherKind.Storm));
            Assert.That(sim.WeatherAt(20), Is.EqualTo(WeatherKind.Storm));
            TestSupport.Rejected(sim.Execute(new SetWeather { Kind = WeatherKind.Clear, FromHour = 30 }), "hour");
        }

        [Test]
        public void ANewDayCanBringNewWeather()
        {
            Simulation sim = TestSupport.NewSim(11);
            int changes = 0;
            sim.EventRaised += e => { if (e is WeatherChanged) changes++; };
            for (int d = 0; d < 40; d++) TestSupport.Ok(sim.Execute(new AdvanceDays()));
            Assert.That(changes, Is.GreaterThan(5));
        }

        [Test]
        public void OnlyStiffJointsFeelTheWeatherComing()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new SetWeather { Kind = WeatherKind.Clear }));
            TestSupport.Ok(sim.Execute(new SetWeather { Kind = WeatherKind.Rain, FromHour = 15 }));
            Assert.That(sim.Forecast(9), Is.Null, "Young joints feel nothing.");
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "age_stiff", Region = BodyRegion.Torso, Origin = TestSupport.Boar }));
            WeatherSpell next = sim.Forecast(9);
            Assert.That(next, Is.Not.Null);
            Assert.That(next.Kind, Is.EqualTo(WeatherKind.Rain));
            Assert.That(next.FromHour, Is.EqualTo(15));
            Assert.That(sim.Forecast(16), Is.Null, "Nothing more today.");
        }

        [Test]
        public void EventsCanWaitForTheWeather()
        {
            // Every event made to need a storm: none fire in clear weather, some do in a storm.
            ContentDatabase db = ContentLoader.LoadDirectory(TestSupport.ContentRoot).Database;
            foreach (EventDef def in db.Events.Values) { def.When.Weather.Clear(); def.When.Weather.Add(WeatherKind.Storm); }
            var road = new EventContext { AreaTags = new List<string> { "road" }, TimeOfDay = TimeOfDay.Day, DistanceFromHome = 0.5, Hour = 13 };
            int Fired(WeatherKind weather)
            {
                int fired = 0;
                for (ulong seed = 1; seed <= 10; seed++)
                {
                    Simulation sim = Simulation.NewGame(db, new NewGameOptions { Seed = seed });
                    TestSupport.Ok(sim.Execute(new SetWeather { Kind = weather }));
                    for (int i = 0; i < 30 && sim.GetActiveEvent() == null; i++) TestSupport.Ok(sim.Execute(new StorytellerTick { Context = road }));
                    if (sim.GetActiveEvent() != null) fired++;
                }
                return fired;
            }
            Assert.That(Fired(WeatherKind.Clear), Is.EqualTo(0));
            Assert.That(Fired(WeatherKind.Storm), Is.GreaterThan(0));
        }

        // ---- Content -------------------------------------------------------------------------------

        private static void AssertWeatherError(string yaml, string expected)
        {
            ContentLoadResult result = ContentLoader.Load(new[]
            {
                new ContentFile { Path = "vocab.yaml", Text = "stats: []\nreaction_tags: []\nabilities: []\narea_tags: []\narchetypes: []\nvisuals: {}\n" },
                new ContentFile { Path = "weather.yaml", Text = yaml },
            });
            if (expected == null) { Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors)); return; }
            foreach (ContentError e in result.Errors) if (e.Message.Contains(expected)) return;
            Assert.Fail("Expected an error containing \"" + expected + "\" but got:\n" + string.Join("\n", result.Errors));
        }

        private const string Spring = "seasons:\n  spring:\n    morning: { clear: 1, rain: 2 }\n    afternoon: { storm: 1 }\n    persist: 0.5\n";

        [Test] public void WeatherContentBaselineIsValid() => AssertWeatherError(Spring, null);
        [Test] public void WeatherKindsMustExist() => AssertWeatherError(Spring.Replace("rain: 2", "hail: 2"), "unknown entry 'hail'");
        [Test] public void WeatherChancesAreChances() => AssertWeatherError(Spring.Replace("persist: 0.5", "persist: 1.5"), "chances from 0 to 1");
        [Test] public void WeatherNeedsSomethingToRoll() => AssertWeatherError(Spring.Replace("{ storm: 1 }", "{ storm: 0 }"), "at least one weather");
        [Test] public void TheRealClimateLoads() => Assert.That(TestSupport.Content.Weather.SourceFile, Is.EqualTo("weather.yaml"));
    }
}

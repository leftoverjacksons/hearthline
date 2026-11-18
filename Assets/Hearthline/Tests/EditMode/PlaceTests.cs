using System.Collections.Generic;
using System.Linq;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using Hearthline.Sim.World;
using NUnit.Framework;

namespace Hearthline.Tests
{
    /// <summary>Ruins and places (docs/specs/ruins-and-places.md): kinds sited by content, searching once, the find, rumours.</summary>
    public class PlaceTests
    {
        private static WorldMap _map;
        private static WorldMap Map => _map ??= WorldGen.Generate(new WorldSettings { Seed = 1 });
        private static List<PlacedPlace> Layout => PlaceLayout.Assign(Map, TestSupport.Content.Places.Values);

        private static Simulation Away()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new SetOut()));
            return sim;
        }

        // ---- Layout ---------------------------------------------------------------------------------

        [Test]
        public void TheSameSeedGivesTheSamePlaces()
        {
            string Describe(List<PlacedPlace> l) => string.Join(";", l.Select(p => p.Id + "@" + p.P.X.ToString("F1") + "," + p.P.Z.ToString("F1")));
            Assert.That(Describe(PlaceLayout.Assign(Map, TestSupport.Content.Places.Values.Reverse())), Is.EqualTo(Describe(Layout)), "Content order doesn't matter.");
            Assert.That(Layout.Count, Is.EqualTo(Map.RuinSites.Count + 1), "Every ruin site plus the Elder Works.");
        }

        [Test]
        public void EveryKindAppearsOnceBeforeAnyRepeats()
        {
            int ruinKinds = TestSupport.Content.Places.Values.Count(d => d.Siting.Fixed == null);
            var kinds = Layout.Where(p => p.Kind != "elder_works").Select(p => p.Kind).ToList();
            Assert.That(kinds.Distinct().Count(), Is.EqualTo(System.Math.Min(ruinKinds, kinds.Count)));
            Assert.That(Layout.Select(p => p.Id).Distinct().Count(), Is.EqualTo(Layout.Count), "Ids are unique.");
        }

        [Test]
        public void KindsGoWhereTheyBelong()
        {
            RuinSite SiteOf(string kind) => Map.Sites.First(s => Layout.Any(p => p.Kind == kind && p.P.X == s.P.X && p.P.Z == s.P.Z));
            if (Layout.Any(p => p.Kind == "filling_station"))
            {
                double limit = TestSupport.Content.Places["filling_station"].Siting.RoadMax.Value;
                if (Map.Sites.Any(s => s.Road <= limit)) Assert.That(SiteOf("filling_station").Road, Is.LessThanOrEqualTo(limit), "The station stands by the highway.");
            }
            foreach (PlacedPlace p in Layout.Where(p => p.Kind != "elder_works"))
            {
                RuinSite site = SiteOf(p.Kind);
                double fit = PlaceLayout.Misfit(TestSupport.Content.Places[p.Kind].Siting, site);
                Assert.That(fit, Is.LessThan(30), p.Kind + " fits its site well enough.");
            }
            PlacedPlace elder = Layout.Single(p => p.Kind == "elder_works");
            Assert.That(elder.P.Length, Is.GreaterThan(TestSupport.Content.Places["elder_works"].Siting.HomeMin.Value), "Far from home.");
            Assert.That(Map.HeightAt(elder.P.X, elder.P.Z) - Map.HomeHeight, Is.GreaterThan(20), "Up on the mountain front.");
            Assert.That(System.Math.Abs(elder.P.X), Is.LessThan(290));
            Assert.That(System.Math.Abs(elder.P.Z), Is.LessThan(290), "Inside the playable square.");
            Assert.That(Map.AreaAt(elder.P.X, elder.P.Z), Is.EqualTo("elder_works"));
        }

        [Test]
        public void TheElderWorksStandsOnLevelGround()
        {
            Vec2 e = Map.ElderWorks;
            for (double a = 0; a < 6.28; a += 0.5)
            {
                double x = e.X + System.Math.Cos(a) * WorldMap.ElderWorksRadius * 0.8, z = e.Z + System.Math.Sin(a) * WorldMap.ElderWorksRadius * 0.8;
                Assert.That(System.Math.Abs(Map.HeightAt(x, z) - Map.ElderWorksHeight), Is.LessThan(0.8));
            }
        }

        // ---- Discovery ------------------------------------------------------------------------------

        [Test]
        public void DiscoveringAPlaceHappensOnceAndIsRemembered()
        {
            Simulation sim = Away();
            CommandResult r = sim.Execute(new DiscoverPlace { PlaceId = "chapel", Kind = "chapel" });
            TestSupport.Ok(r);
            Assert.That(r.Events.OfType<PlaceDiscovered>().Single().Name, Is.EqualTo("the fallen chapel"));
            Assert.That(sim.IsDiscovered("chapel"), Is.True);
            Assert.That(sim.State.Chronicle.Last().Text, Does.Contain("first came to the fallen chapel"));
            TestSupport.Rejected(sim.Execute(new DiscoverPlace { PlaceId = "chapel", Kind = "chapel" }), "already known");
            TestSupport.Rejected(sim.Execute(new DiscoverPlace { PlaceId = "chapel", Kind = "schoolhouse" }), "not a schoolhouse");
            TestSupport.Rejected(sim.Execute(new DiscoverPlace { PlaceId = "x", Kind = "castle" }), "no kind of place");
        }

        // ---- Searching ------------------------------------------------------------------------------

        [Test]
        public void ASpotGivesItsSalvageOnce()
        {
            Simulation sim = Away();
            CommandResult r = sim.Execute(new SearchSpot { PlaceId = "filling_station", Kind = "filling_station", Spot = 1 });
            TestSupport.Ok(r);
            SpotSearched searched = r.Events.OfType<SpotSearched>().Single();
            Assert.That(searched.Items.Values.Sum(), Is.GreaterThan(0));
            Assert.That(searched.IntoPack, Is.True);
            foreach (var kv in searched.Items) Assert.That(sim.State.Trip.Pack[kv.Key], Is.GreaterThanOrEqualTo(kv.Value), "Into the pack.");
            Assert.That(sim.IsSearched("filling_station", 1), Is.True);
            Assert.That(sim.IsSearched("filling_station", 0), Is.False);
            TestSupport.Rejected(sim.Execute(new SearchSpot { PlaceId = "filling_station", Kind = "filling_station", Spot = 1 }), "already gone through");
            TestSupport.Rejected(sim.Execute(new SearchSpot { PlaceId = "filling_station", Kind = "filling_station", Spot = 9 }), "no search spot");
            TestSupport.Ok(sim.Execute(new ReturnHome()));
            for (int day = 0; day < 400; day += 40) TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 40 }));
            TestSupport.Rejected(sim.Execute(new SearchSpot { PlaceId = "filling_station", Kind = "filling_station", Spot = 1 }), "already gone through");
        }

        [Test]
        public void SearchedSpotsAndTakenFindsSurviveTheSave()
        {
            Simulation sim = Away();
            TestSupport.Ok(sim.Execute(new DiscoverPlace { PlaceId = "farmstead", Kind = "farmstead" }));
            TestSupport.Ok(sim.Execute(new SearchSpot { PlaceId = "farmstead", Kind = "farmstead", Spot = 0 }));
            TestSupport.Ok(sim.Execute(new TakeFind { PlaceId = "farmstead", Kind = "farmstead" }));
            Simulation loaded = Simulation.FromJson(sim.ToJson(), TestSupport.Content);
            Assert.That(loaded.IsDiscovered("farmstead"), Is.True);
            Assert.That(loaded.IsSearched("farmstead", 0), Is.True);
            Assert.That(loaded.IsFindTaken("farmstead"), Is.True);
            Assert.That(loaded.ToJson(), Is.EqualTo(sim.ToJson()));
        }

        // ---- The find -------------------------------------------------------------------------------

        [Test]
        public void AnUncheckedFindIsAlwaysThereOnce()
        {
            Simulation sim = Away();
            CommandResult r = sim.Execute(new TakeFind { PlaceId = "feed_store", Kind = "feed_store" });
            TestSupport.Ok(r);
            FindTaken taken = r.Events.OfType<FindTaken>().Single();
            Assert.That(taken.ItemId, Is.EqualTo("cartridges"));
            Assert.That(taken.Count, Is.InRange(8, 14));
            Assert.That(taken.Text, Does.Contain("crate of shells"));
            TestSupport.Rejected(sim.Execute(new TakeFind { PlaceId = "feed_store", Kind = "feed_store" }), "already taken");
            TestSupport.Rejected(sim.Execute(new TakeFind { PlaceId = "overpass", Kind = "overpass" }), "nothing is hidden");
        }

        [Test]
        public void ACheckedFindCanBeMissedAndTriedAgainAnotherDay()
        {
            Simulation odds = TestSupport.NewSim();
            Assert.That(odds.FindChance("chapel"), Is.EqualTo(0.25), "Tracking 2 needed: an untrained eye has a quarter chance.");
            odds.Head.BaseStats["tracking"] = 1;
            Assert.That(odds.FindChance("chapel"), Is.EqualTo(0.5));
            odds.Head.BaseStats["tracking"] = 2;
            Assert.That(odds.FindChance("chapel"), Is.EqualTo(1));
            Assert.That(odds.FindChance("feed_store"), Is.EqualTo(1), "No check.");
            int missed = 0, found = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Simulation sim = TestSupport.NewSim(seed);
                TestSupport.Ok(sim.Execute(new SetOut()));
                CommandResult r = sim.Execute(new TakeFind { PlaceId = "chapel", Kind = "chapel" });
                TestSupport.Ok(r);
                if (r.Events.OfType<FindMissed>().Any())
                {
                    missed++;
                    Assert.That(sim.IsFindTaken("chapel"), Is.False);
                    TestSupport.Rejected(sim.Execute(new TakeFind { PlaceId = "chapel", Kind = "chapel" }), "nothing more to see today");
                    TestSupport.Ok(sim.Execute(new AdvanceDays()));
                    sim.Head.BaseStats["tracking"] = 2;
                    CommandResult again = sim.Execute(new TakeFind { PlaceId = "chapel", Kind = "chapel" });
                    TestSupport.Ok(again);
                    Assert.That(again.Events.OfType<FindTaken>().Any(), Is.True, "A good tracker always finds it.");
                }
                else found++;
            }
            Assert.That(missed, Is.GreaterThan(0), "An untrained eye misses it sometimes.");
            Assert.That(found, Is.GreaterThan(0), "And finds it sometimes.");
        }

        [Test]
        public void AGearFindIsItsOwnThingWithAHistory()
        {
            Simulation sim = Away();
            sim.Head.BaseStats["smithing"] = 1;
            int before = sim.Head.Gear.Count;
            TestSupport.Ok(sim.Execute(new TakeFind { PlaceId = "filling_station", Kind = "filling_station" }));
            Assert.That(sim.Head.Gear.Count, Is.EqualTo(before + 1));
            GearItem gun = sim.Head.Gear.Last();
            Assert.That(gun.DefId, Is.EqualTo("revolver"));
            Assert.That(gun.Ledger.First().Text, Does.Contain("Found at the filling station"));
        }

        [Test]
        public void ResetMakesAPlaceNewAgain()
        {
            Simulation sim = Away();
            TestSupport.Ok(sim.Execute(new SearchSpot { PlaceId = "chapel", Kind = "chapel", Spot = 0 }));
            TestSupport.Ok(sim.Execute(new ResetPlace { PlaceId = "chapel" }));
            Assert.That(sim.IsSearched("chapel", 0), Is.False);
            TestSupport.Rejected(sim.Execute(new ResetPlace { PlaceId = "chapel" }), "nothing known");
        }

        // ---- Rumours --------------------------------------------------------------------------------

        [Test]
        public void ARumourLeadsToAFindAndThenIsGone()
        {
            Simulation sim = TestSupport.NewSim();
            Assert.That(sim.Rumours(), Has.Member("filling_station"));
            Assert.That(sim.Rumours(), Has.No.Member("feed_store"), "No rumour written about it.");
            TestSupport.Ok(sim.Execute(new FollowRumour { Kind = "filling_station" }));
            Assert.That(sim.Rumour.Kind, Is.EqualTo("filling_station"));
            TestSupport.Rejected(sim.Execute(new FollowRumour { Kind = "chapel" }), "already following");
            TestSupport.Ok(sim.Execute(new SetOut()));
            sim.Head.BaseStats["smithing"] = 1;
            CommandResult r = sim.Execute(new TakeFind { PlaceId = "filling_station", Kind = "filling_station" });
            TestSupport.Ok(r);
            Assert.That(r.Events.OfType<RumourEnded>().Single().Found, Is.True);
            Assert.That(sim.Rumour, Is.Null);
            Assert.That(sim.State.Chronicle.Last().Text, Does.Contain("ran down the rumour about the filling station"));
            Assert.That(sim.Rumours(), Has.No.Member("filling_station"), "Nothing left to tell.");
            TestSupport.Rejected(sim.Execute(new FollowRumour { Kind = "filling_station" }), "already found");
        }

        [Test]
        public void ARumourCanBeDropped()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new FollowRumour { Kind = "chapel" }));
            CommandResult r = sim.Execute(new DropRumour());
            TestSupport.Ok(r);
            Assert.That(r.Events.OfType<RumourEnded>().Single().Found, Is.False);
            Assert.That(sim.Rumours(), Has.Member("chapel"));
            TestSupport.Rejected(sim.Execute(new DropRumour()), "no rumour");
        }

        // ---- Events ---------------------------------------------------------------------------------

        [Test]
        public void EventsCanBelongToAKindOfPlace()
        {
            ContentDatabase db = ContentLoader.LoadDirectory(TestSupport.ContentRoot).Database;
            foreach (EventDef def in db.Events.Values) { def.When.PlaceAny.Clear(); def.When.PlaceAny.Add("chapel"); }
            int Fired(string kind)
            {
                int fired = 0;
                for (ulong seed = 1; seed <= 10; seed++)
                {
                    Simulation sim = Simulation.NewGame(db, new NewGameOptions { Seed = seed });
                    var here = new EventContext { AreaTags = new List<string> { "ruin", "road" }, TimeOfDay = TimeOfDay.Day, DistanceFromHome = 0.5, PlaceKind = kind };
                    for (int i = 0; i < 30 && sim.GetActiveEvent() == null; i++) TestSupport.Ok(sim.Execute(new StorytellerTick { Context = here }));
                    if (sim.GetActiveEvent() != null) fired++;
                }
                return fired;
            }
            Assert.That(Fired(null), Is.EqualTo(0), "Not out in the open.");
            Assert.That(Fired("schoolhouse"), Is.EqualTo(0), "Not at another kind of place.");
            Assert.That(Fired("chapel"), Is.GreaterThan(0));
        }

        // ---- Content --------------------------------------------------------------------------------

        private static void AssertPlaceError(string yaml, string expected)
        {
            ContentLoadResult result = ContentLoader.Load(new[]
            {
                new ContentFile { Path = "vocab.yaml", Text = "stats:\n  - { id: tracking, name: Tracking, base: 0 }\nreaction_tags: []\nabilities: []\narea_tags: [ruin]\narchetypes: []\nvisuals: {}\n" },
                new ContentFile { Path = "items/old_coin.yaml", Text = "id: old_coin\nname: Old coin\nweight: 0.05\n" },
                new ContentFile { Path = "loot/bits.yaml", Text = "id: bits\nentries:\n  - { item: old_coin }\n" },
                new ContentFile { Path = "places/shed.yaml", Text = yaml },
            });
            var errors = result.Errors.Where(e => e.File == "places/shed.yaml").ToList();
            if (expected == null) { Assert.That(errors, Is.Empty, string.Join("\n", errors)); return; }
            foreach (ContentError e in errors) if (e.Message.Contains(expected)) return;
            Assert.Fail("Expected an error containing \"" + expected + "\" but got:\n" + string.Join("\n", errors));
        }

        private const string Shed = "id: shed\nname: the shed\narea_tags: [ruin]\nsiting: { slope_max: 0.1 }\nsearch_spots: [1, 2]\nloot: bits\n"
            + "find: { item: old_coin, count: [1, 2], check: { stat: tracking, difficulty: 1 }, text: \"Coins.\" }\nrumour: \"They say.\"\n";

        [Test] public void PlaceBaselineIsValid() => AssertPlaceError(Shed, null);
        [Test] public void PlaceTagsComeFromTheVocab() => AssertPlaceError(Shed.Replace("[ruin]", "[castle]"), "no area tag 'castle'");
        [Test] public void PlaceLootMustExist() => AssertPlaceError(Shed.Replace("loot: bits", "loot: nope"), "no loot table 'nope'");
        [Test] public void PlaceFindMustExist() => AssertPlaceError(Shed.Replace("item: old_coin", "item: gold"), "no item 'gold'");
        [Test] public void PlaceCheckStatMustExist() => AssertPlaceError(Shed.Replace("stat: tracking", "stat: luck"), "no stat 'luck'");
        [Test] public void PlaceSearchSpotsAreSane() => AssertPlaceError(Shed.Replace("[1, 2]", "[3, 2]"), "search_spots");
        [Test] public void PlaceNamesReadMidSentence() => AssertPlaceError(Shed.Replace("the shed", "The shed"), "lower case");
        [Test] public void ARumourNeedsAFind() => AssertPlaceError(Shed.Replace("find: { item: old_coin, count: [1, 2], check: { stat: tracking, difficulty: 1 }, text: \"Coins.\" }\n", ""), "rumour needs a find");
        [Test] public void FixedSitingIsKnown() => AssertPlaceError(Shed.Replace("{ slope_max: 0.1 }", "{ fixed: moon }"), "'fixed'");

        [Test]
        public void EventsMayOnlyNameRealKindsOfPlace()
        {
            string root = TestSupport.ContentRoot;
            var files = new List<ContentFile>();
            foreach (string path in System.IO.Directory.GetFiles(root, "*.yaml", System.IO.SearchOption.AllDirectories))
                files.Add(new ContentFile { Path = path.Substring(root.Length + 1).Replace('\\', '/'), Text = System.IO.File.ReadAllText(path) });
            string fog = files.Single(f => f.Path == "events/fog_on_the_ridge.yaml").Text;
            files.Add(new ContentFile { Path = "events/fog_at_the_castle.yaml", Text = fog.Replace("id: fog_on_the_ridge", "id: fog_at_the_castle").Replace("  weather_any: [fog]", "  weather_any: [fog]\n  place_any: [castle]") });
            ContentLoadResult result = ContentLoader.Load(files);
            Assert.That(result.Errors.Any(e => e.File == "events/fog_at_the_castle.yaml" && e.Message.Contains("no kind of place 'castle'")), Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Errors.Count(e => e.File != "events/fog_at_the_castle.yaml"), Is.EqualTo(0));
        }
    }
}

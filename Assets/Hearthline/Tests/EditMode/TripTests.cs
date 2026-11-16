using System.Collections.Generic;
using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class TripTests
    {
        private static Character Join(Simulation sim, string name, int age = 30, HouseholdBond bond = HouseholdBond.Sworn)
        {
            TestSupport.Ok(sim.Execute(new AddHouseholdMember { GivenName = name, Surname = "Pruitt", AgeYears = age, Bond = bond }));
            return sim.State.Characters[sim.State.Characters.Count - 1];
        }

        private static void Give(Simulation sim, string item, int count) => TestSupport.Ok(sim.Execute(new GiveItem { ItemId = item, Count = count }));

        private static T Find<T>(CommandResult result) where T : DomainEvent
        {
            foreach (DomainEvent e in result.Events) if (e is T t) return t;
            return null;
        }

        private static EventContext Road => new EventContext { AreaTags = new List<string> { "road" }, TimeOfDay = TimeOfDay.Day, PlaceName = "the ford road" };

        [Test]
        public void SettingOutMovesThePackOutOfTheStoresAndComingHomeBringsItBack()
        {
            Simulation sim = TestSupport.NewSim();
            Give(sim, "jerky", 5); Give(sim, "old_coin", 4);
            TestSupport.Ok(sim.Execute(new SetOut { Pack = new Dictionary<string, int> { { "jerky", 2 }, { "old_coin", 3 } } }));
            Assert.That(sim.OnTrip, Is.True);
            Assert.That(sim.ItemCount("jerky"), Is.EqualTo(3));
            Assert.That(sim.PackCount("jerky"), Is.EqualTo(2));
            Assert.That(sim.PackWeight, Is.EqualTo(2 * 0.2 + 3 * 0.05).Within(1e-9));

            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 3 }));
            CommandResult result = sim.Execute(new ReturnHome());
            TestSupport.Ok(result);
            Assert.That(Find<TripEnded>(result).DaysAway, Is.EqualTo(3));
            Assert.That(sim.OnTrip, Is.False);
            Assert.That(sim.ItemCount("jerky"), Is.EqualTo(5));
            Assert.That(sim.ItemCount("old_coin"), Is.EqualTo(4));
            TestSupport.Rejected(sim.Execute(new ReturnHome()), "not away");
        }

        [Test]
        public void OnTheRoadYouOnlyHaveWhatYouCarried()
        {
            Simulation sim = TestSupport.NewSim();
            Give(sim, "old_coin", 5);
            TestSupport.Ok(sim.Execute(new SetOut { Pack = new Dictionary<string, int> { { "old_coin", 1 } } }));
            TestSupport.Ok(sim.Execute(new StartEvent { EventId = "peddler_on_the_road", Context = Road }));
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }));
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 })); // buy bandages: 1 coin
            Assert.That(sim.PackCount("old_coin"), Is.EqualTo(0));
            Assert.That(sim.PackCount("bandage"), Is.EqualTo(2));
            Assert.That(sim.ItemCount("old_coin"), Is.EqualTo(4), "the coin at home was not spent");

            // The coin at home does not help at the next peddler.
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 20 }));
            TestSupport.Ok(sim.Execute(new StartEvent { EventId = "peddler_on_the_road", Context = Road }));
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }));
            Assert.That(sim.GetActiveEvent().Choices[0].Available, Is.False);
        }

        [Test]
        public void APackCanOnlyBeAsHeavyAsThePartyCanCarry()
        {
            Simulation sim = TestSupport.NewSim();
            Character silas = Join(sim, "Silas");
            Give(sim, "lumber", 5);
            var pack = new Dictionary<string, int> { { "lumber", 4 } }; // 40 weight; one person carries 30
            TestSupport.Rejected(sim.Execute(new SetOut { Pack = pack }), "the party can carry 30");
            Assert.That(sim.ItemCount("lumber"), Is.EqualTo(5), "a refused departure takes nothing");
            TestSupport.Ok(sim.Execute(new SetOut { Pack = pack, CompanionIds = new List<int> { silas.Id } }));
            Assert.That(sim.CarryCapacity, Is.EqualTo(60));
            Assert.That(sim.Overloaded, Is.False);

            // Loot can push the party over; that is allowed, and it is a state presentation and the storyteller can see.
            Give(sim, "pelt_wolf", 6);
            Assert.That(sim.Overloaded, Is.True);
        }

        [Test]
        public void CompanionsMustBeAbleHouseholdMembersAndAtMostTwo()
        {
            Simulation sim = TestSupport.NewSim();
            Character a = Join(sim, "Silas"), b = Join(sim, "Opal"), c = Join(sim, "Wade"), kid = Join(sim, "June", 9, HouseholdBond.Ward);
            Character shopkeeper = sim.FindResident("shopkeeper");
            TestSupport.Rejected(sim.Execute(new SetOut { CompanionIds = new List<int> { a.Id, b.Id, c.Id } }), "at most 2");
            TestSupport.Rejected(sim.Execute(new SetOut { CompanionIds = new List<int> { kid.Id } }), "too young");
            TestSupport.Rejected(sim.Execute(new SetOut { CompanionIds = new List<int> { shopkeeper.Id } }), "not in the household");
            TestSupport.Rejected(sim.Execute(new SetOut { CompanionIds = new List<int> { a.Id, a.Id } }), "twice");
            TestSupport.Rejected(sim.Execute(new SetOut { Pack = new Dictionary<string, int> { { "jerky", 1 } } }), "only hold 0");
            TestSupport.Ok(sim.Execute(new SetOut { CompanionIds = new List<int> { a.Id, b.Id } }));
            TestSupport.Rejected(sim.Execute(new SetOut()), "already away");
        }

        [Test]
        public void HomeCarriesOnWhileYouAreAwayButCompanionsAreNotThereToWork()
        {
            Simulation sim = TestSupport.NewSim();
            Character farmer = Join(sim, "Levi"), hunter = Join(sim, "Opal");
            sim.State.Household.Structures["garden"] = 1; // test setup only
            TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = farmer.Id, JobId = "farmer" }));
            TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = hunter.Id, JobId = "hunter" }));
            TestSupport.Ok(sim.Execute(new SetOut { CompanionIds = new List<int> { hunter.Id } }));

            TestSupport.Rejected(sim.Execute(new PassSeason()), "come home first");
            TestSupport.Rejected(sim.Execute(new BuildStructure { StructureId = "stable" }), "the head is away");

            // The season turns while the party is still out.
            CommandResult result = sim.Execute(new AdvanceDays { Days = GameDate.DaysPerSeason });
            HomeReported report = Find<HomeReported>(result);
            Assert.That(report, Is.Not.Null);
            Assert.That(Find<SeasonPassed>(result), Is.Not.Null);
            Assert.That(report.Lines.Count, Is.EqualTo(1), "only the farmer was home to work");
            Assert.That(report.Lines[0].CharacterId, Is.EqualTo(farmer.Id));
            Assert.That(sim.ItemCount("cornmeal"), Is.EqualTo(report.Lines[0].Items["cornmeal"] - 2), "one mouth at home");
            Assert.That(sim.PackCount("cornmeal"), Is.EqualTo(0), "what home produces goes to the stores, not the pack");
            Assert.That(sim.State.Household.LastReport.Count, Is.EqualTo(1), "waiting for the head to come home and hear it");
        }

        [Test]
        public void MeatSpoilsOnTheRoadAndInStoresWithNoLarder()
        {
            Simulation sim = TestSupport.NewSim();
            Give(sim, "boar_meat", 10);
            TestSupport.Ok(sim.Execute(new SetOut { Pack = new Dictionary<string, int> { { "boar_meat", 3 } } }));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 2 }));
            Assert.That(sim.PackCount("boar_meat"), Is.EqualTo(1), "one a day from a stack of three");
            Assert.That(sim.ItemCount("boar_meat"), Is.EqualTo(7), "the stores do not spoil by the day");
            TestSupport.Ok(sim.Execute(new ReturnHome()));

            TestSupport.Ok(sim.Execute(new PassSeason()));
            Assert.That(sim.ItemCount("boar_meat"), Is.EqualTo(4), "half of 8 went off with nowhere to keep it");

            sim.State.Household.Structures["tannery"] = 1; // drying rack; test setup only
            TestSupport.Ok(sim.Execute(new PassSeason()));
            Assert.That(sim.ItemCount("boar_meat"), Is.EqualTo(4));
        }

        [Test]
        public void IfTheHeadDiesAloneOnTheRoadThePackIsLost()
        {
            Simulation sim = TestSupport.NewSim();
            Join(sim, "Eli", 20, HouseholdBond.Ward);
            Give(sim, "old_coin", 6);
            TestSupport.Ok(sim.Execute(new SetOut { Pack = new Dictionary<string, int> { { "old_coin", 6 } } }));
            CommandResult result = sim.Execute(new KillCharacter { CharacterId = sim.Head.Id, Cause = "shot by a Reaver" });
            Assert.That(Find<TripEnded>(result).PackLost, Is.True);
            Assert.That(sim.OnTrip, Is.False);
            Assert.That(sim.ItemCount("old_coin"), Is.EqualTo(0));
            Assert.That(sim.EligibleHeirs().Count, Is.EqualTo(1));
        }

        [Test]
        public void ACompanionCarriesThePackHomeIfTheHeadFalls()
        {
            Simulation sim = TestSupport.NewSim();
            Character eli = Join(sim, "Eli", 20, HouseholdBond.Ward);
            Give(sim, "old_coin", 6);
            TestSupport.Ok(sim.Execute(new SetOut { CompanionIds = new List<int> { eli.Id }, Pack = new Dictionary<string, int> { { "old_coin", 6 } } }));
            CommandResult result = sim.Execute(new KillCharacter { CharacterId = sim.Head.Id, Cause = "shot by a Reaver" });
            Assert.That(Find<TripEnded>(result).PackLost, Is.False);
            Assert.That(sim.ItemCount("old_coin"), Is.EqualTo(6));
            TestSupport.Ok(sim.Execute(new ChooseHeir { HeirId = eli.Id }));
        }

        [Test]
        public void ATripInProgressSurvivesSaveAndLoad()
        {
            Simulation sim = TestSupport.NewSim(15);
            Character silas = Join(sim, "Silas");
            Give(sim, "jerky", 6); Give(sim, "boar_meat", 4);
            TestSupport.Ok(sim.Execute(new SetOut { CompanionIds = new List<int> { silas.Id }, Pack = new Dictionary<string, int> { { "jerky", 3 }, { "boar_meat", 4 } } }));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 1 }));

            string json = sim.ToJson();
            Simulation loaded = Simulation.FromJson(json, TestSupport.Content);
            Assert.That(loaded.ToJson(), Is.EqualTo(json));
            Assert.That(loaded.OnTrip, Is.True);
            foreach (Simulation s in new[] { sim, loaded })
            {
                s.Execute(new AdvanceDays { Days = 40 });
                s.Execute(new ReturnHome());
                s.Execute(new PassSeason());
            }
            Assert.That(loaded.ToJson(), Is.EqualTo(sim.ToJson()));
        }

        /// <summary>The loop the game is built around, start to finish, with nothing but commands.</summary>
        [Test]
        public void HangOutAtHomeGoOutComeBackChanged()
        {
            Simulation sim = TestSupport.NewSim(7);
            Character ada = sim.Head;
            Character levi = Join(sim, "Levi", 30, HouseholdBond.Partner), opal = Join(sim, "Opal");

            // At home: build, give people work, pack.
            Give(sim, "lumber", 5); Give(sim, "bandage", 3); Give(sim, "jerky", 6);
            TestSupport.Ok(sim.Execute(new BuildStructure { StructureId = "garden" }));
            TestSupport.Ok(sim.Execute(new BuildStructure { StructureId = "infirmary" }));
            TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = levi.Id, JobId = "farmer" }));
            TestSupport.Ok(sim.Execute(new SetOut { CompanionIds = new List<int> { opal.Id }, Pack = new Dictionary<string, int> { { "bandage", 1 }, { "jerky", 2 } } }));

            // Out: a fight goes badly, then the road offers something.
            var ridge = new MarkOrigin { Cause = "a Reaver", Place = "the ridge" };
            TestSupport.Ok(sim.Execute(new InflictHit { CharacterId = ada.Id, Region = BodyRegion.ArmL, Damage = DamageType.Gunshot, Severity = 2, Origin = ridge }));
            TestSupport.Ok(sim.Execute(new FieldTreatWound { CharacterId = ada.Id, MarkId = ada.Marks[0].Id }));
            TestSupport.Ok(sim.Execute(new GiveItem { ItemId = "pelt_wolf", Count = 2 }));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 3 }));

            // Home: the pack is unloaded, the wound is seen to in the infirmary that was built.
            TestSupport.Ok(sim.Execute(new ReturnHome()));
            Assert.That(sim.ItemCount("pelt_wolf"), Is.EqualTo(2));
            TestSupport.Ok(sim.Execute(new TreatWoundAtHome { CharacterId = ada.Id, MarkId = ada.Marks[0].Id }));
            TestSupport.Ok(sim.Execute(new SellItems { ItemId = "pelt_wolf", Count = 2 }));

            // The season turns: the farmer reports, the town feels the trade, the wound becomes what it will be.
            CommandResult season = sim.Execute(new PassSeason());
            Assert.That(Find<HomeReported>(season).Lines[0].Items.ContainsKey("cornmeal"), Is.True);
            Assert.That(sim.FindSettlement().Values[SettlementValue.Prosperity], Is.EqualTo(37));
            Assert.That(ada.Marks[0].Category, Is.Not.EqualTo(MarkCategory.Wound).Or.Property("Stage").EqualTo(WoundStages.Infected));

            string all = string.Join("\n", sim.ChronicleOf(ada).ConvertAll(e => e.Text));
            Assert.That(all, Does.Contain("Shot by a Reaver at the ridge, spring of year 1."));
            Assert.That(Simulation.FromJson(sim.ToJson(), TestSupport.Content).ToJson(), Is.EqualTo(sim.ToJson()));
        }
    }
}

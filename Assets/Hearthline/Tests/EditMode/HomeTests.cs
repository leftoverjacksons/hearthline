using System.Collections.Generic;
using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class HomesteadTests
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

        [Test]
        public void ANewHomesteadIsATentAndAFirePit()
        {
            Simulation sim = TestSupport.NewSim();
            Assert.That(sim.StructureTier("dwelling"), Is.EqualTo(1));
            Assert.That(sim.StructureTier("hearth"), Is.EqualTo(1));
            Assert.That(sim.StructureTier("infirmary"), Is.EqualTo(0));
            Assert.That(sim.HomeStatOf(HomeStat.Capacity), Is.EqualTo(3));
            Assert.That(sim.HomeStatOf(HomeStat.Treatment), Is.EqualTo(0));
            Assert.That(sim.State.Household.Morale, Is.EqualTo(50));
        }

        [Test]
        public void BuildingCostsMaterialsAndGoesOneTierAtATime()
        {
            Simulation sim = TestSupport.NewSim();
            string before = sim.ToJson();
            TestSupport.Rejected(sim.Execute(new BuildStructure { StructureId = "infirmary" }), "Cot needs 2 bandage (the household has 0)");
            Give(sim, "lumber", 3);
            TestSupport.Rejected(sim.Execute(new BuildStructure { StructureId = "infirmary" }), "needs 2 bandage");
            Assert.That(sim.ItemCount("lumber"), Is.EqualTo(3), "a refused build takes nothing");
            Give(sim, "bandage", 2);

            CommandResult result = sim.Execute(new BuildStructure { StructureId = "infirmary" });
            TestSupport.Ok(result);
            Assert.That(Find<StructureChanged>(result).NewTier, Is.EqualTo(1));
            Assert.That(sim.ItemCount("lumber"), Is.EqualTo(0));
            Assert.That(sim.ItemCount("bandage"), Is.EqualTo(0));
            Assert.That(sim.HomeStatOf(HomeStat.Treatment), Is.EqualTo(1));

            TestSupport.Rejected(sim.Execute(new BuildStructure { StructureId = "castle" }), "no structure");
            Give(sim, "lumber", 40);
            TestSupport.Ok(sim.Execute(new BuildStructure { StructureId = "tannery" }));
            TestSupport.Ok(sim.Execute(new BuildStructure { StructureId = "tannery" }));
            TestSupport.Rejected(sim.Execute(new BuildStructure { StructureId = "tannery" }), "cannot be improved further");
        }

        [Test]
        public void WoundsAreTreatedInTheInfirmaryYouBuilt()
        {
            Simulation sim = TestSupport.NewSim();
            Character healer = Join(sim, "Opal");
            healer.BaseStats["medicine"] = 4; // test setup only
            TestSupport.Ok(sim.Execute(new InflictHit { CharacterId = sim.Head.Id, Region = BodyRegion.ArmL, Damage = DamageType.Blade, Severity = 1, Origin = TestSupport.Boar }));
            int woundId = sim.Head.Marks[0].Id;
            TestSupport.Rejected(sim.Execute(new TreatWoundAtHome { CharacterId = sim.Head.Id, MarkId = woundId }), "nowhere to treat");

            Give(sim, "lumber", 3); Give(sim, "bandage", 2);
            TestSupport.Ok(sim.Execute(new BuildStructure { StructureId = "infirmary" }));
            CommandResult result = sim.Execute(new TreatWoundAtHome { CharacterId = sim.Head.Id, MarkId = woundId });
            TestSupport.Ok(result);
            Assert.That(Find<WoundTreated>(result).AtInfirmary, Is.True);
        }

        [Test]
        public void ABetterInfirmaryAtHomeHealsCleaner()
        {
            int Clean(bool surgery)
            {
                int clean = 0;
                for (ulong seed = 1; seed <= 300; seed++)
                {
                    Simulation sim = TestSupport.NewSim(seed);
                    sim.State.Household.Structures["infirmary"] = surgery ? 3 : 1; // test setup only
                    TestSupport.Ok(sim.Execute(new InflictHit { CharacterId = sim.Head.Id, Region = BodyRegion.ArmL, Damage = DamageType.Blade, Severity = 1, Origin = TestSupport.Boar }));
                    CommandResult r = sim.Execute(new TreatWoundAtHome { CharacterId = sim.Head.Id, MarkId = sim.Head.Marks[0].Id });
                    if (Find<WoundTreated>(r).Outcome == WoundOutcomes.CleanScar) clean++;
                }
                return clean;
            }
            Assert.That(Clean(true), Is.GreaterThan(Clean(false) + 30));
        }

        [Test]
        public void JobsNeedTheRightPersonAndTheRightStructure()
        {
            Simulation sim = TestSupport.NewSim();
            Character levi = Join(sim, "Levi");
            Character child = Join(sim, "June", 8, HouseholdBond.Ward);
            TestSupport.Rejected(sim.Execute(new AssignJob { CharacterId = sim.Head.Id, JobId = "farmer" }), "the one who goes out");
            TestSupport.Rejected(sim.Execute(new AssignJob { CharacterId = child.Id, JobId = "farmer" }), "too young");
            TestSupport.Rejected(sim.Execute(new AssignJob { CharacterId = levi.Id, JobId = "astronaut" }), "no job");
            TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = levi.Id, JobId = "farmer" }));
            TestSupport.Rejected(sim.Execute(new AssignJob { CharacterId = levi.Id, JobId = "farmer" }), "already does that");

            // No garden yet: the season's report says why nothing came of it.
            CommandResult result = sim.Execute(new PassSeason());
            HomeReported report = Find<HomeReported>(result);
            Assert.That(report.Lines.Count, Is.EqualTo(1));
            Assert.That(report.Lines[0].Problem, Is.EqualTo("there is no garden"));
            Assert.That(sim.ItemCount("cornmeal"), Is.EqualTo(0));
        }

        [Test]
        public void AFarmerFeedsTheHouseAndGetsBetterAtIt()
        {
            Simulation sim = TestSupport.NewSim();
            Character levi = Join(sim, "Levi");
            Give(sim, "lumber", 2);
            TestSupport.Ok(sim.Execute(new BuildStructure { StructureId = "garden" }));
            TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = levi.Id, JobId = "farmer" }));

            HomeReported report = Find<HomeReported>(sim.Execute(new PassSeason()));
            int grown = report.Lines[0].Items["cornmeal"];
            Assert.That(grown, Is.InRange(3, 5));
            Assert.That(report.HungryMembers, Is.EqualTo(0));
            Assert.That(sim.ItemCount("cornmeal"), Is.EqualTo(grown - 2), "Levi eats two; the head eats on the road");
            Assert.That(sim.GetStat(levi, "husbandry"), Is.EqualTo(0.25));
            Assert.That(sim.State.Household.LastReport.Count, Is.EqualTo(1));

            for (int i = 0; i < 40; i++) sim.Execute(new PassSeason());
            Assert.That(sim.GetStat(levi, "husbandry"), Is.EqualTo(5.0), "skill from work is capped");
        }

        [Test]
        public void BetterFieldsAndMoreSkillYieldMore()
        {
            int Harvest(int gardenTier, double skill)
            {
                int total = 0;
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    Simulation sim = TestSupport.NewSim(seed);
                    Character levi = Join(sim, "Levi");
                    sim.State.Household.Structures["garden"] = gardenTier; // test setup only
                    levi.BaseStats["husbandry"] = skill;
                    TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = levi.Id, JobId = "farmer" }));
                    total += Find<HomeReported>(sim.Execute(new PassSeason())).Lines[0].Items["cornmeal"];
                }
                return total;
            }
            int plot = Harvest(1, 0), orchard = Harvest(3, 0), skilled = Harvest(1, 4.75);
            Assert.That(orchard, Is.GreaterThan(plot * 1.3));
            Assert.That(skilled, Is.GreaterThan(plot * 1.3));
        }

        [Test]
        public void AHungryCrowdedHouseLosesHeart()
        {
            Simulation sim = TestSupport.NewSim();
            foreach (string name in new[] { "Levi", "Opal", "Silas", "Wade" }) Join(sim, name, 30, HouseholdBond.Partner);
            HomeReported report = Find<HomeReported>(sim.Execute(new PassSeason()));
            Assert.That(report.HungryMembers, Is.EqualTo(4));
            Assert.That(sim.State.Household.Morale, Is.EqualTo(40), "morale falls by at most 10 a season");
            for (int i = 0; i < 6; i++) sim.Execute(new PassSeason());
            Assert.That(sim.State.Household.Morale, Is.EqualTo(5), "50 + 5 (fire pit) - 40 (hungry) - 10 (two over capacity)");
        }

        [Test]
        public void AFedHouseWithACookIsContent()
        {
            Simulation sim = TestSupport.NewSim();
            Character cook = Join(sim, "Opal");
            TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = cook.Id, JobId = "cook" }));
            Give(sim, "jerky", 40);
            for (int i = 0; i < 4; i++) sim.Execute(new PassSeason());
            Assert.That(sim.State.Household.Morale, Is.EqualTo(60), "50 + 5 (fire pit) + 5 (cook)");
            Assert.That(sim.ItemCount("jerky"), Is.EqualTo(32));
            for (int i = 0; i < 4; i++) sim.Execute(new PassSeason());
            Assert.That(cook.Opinions[sim.Head.Id], Is.GreaterThan(0), "people warm to the head of a happy house");
        }

        [Test]
        public void InAMiserableHouseTheSwornWalkOutButFamilyStays()
        {
            int left = 0, thefts = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                Simulation sim = TestSupport.NewSim(seed);
                Character partner = Join(sim, "Levi", 30, HouseholdBond.Partner);
                Character sworn = Join(sim, "Silas");
                Join(sim, "Opal", 30, HouseholdBond.Partner); Join(sim, "Wade", 30, HouseholdBond.Partner);
                Give(sim, "lumber", 5);
                var events = new List<DomainEvent>();
                sim.EventRaised += events.Add;
                for (int i = 0; i < 24; i++) sim.Execute(new PassSeason());

                Assert.That(sim.State.Household.MemberIds, Does.Contain(partner.Id), "partners do not walk out");
                if (sim.State.Household.MemberIds.Contains(sworn.Id)) continue;
                left++;
                Assert.That(sworn.Alive, Is.True);
                Assert.That(sworn.JobId, Is.Null);
                CharacterLeft e = (CharacterLeft)events.Find(x => x is CharacterLeft);
                if (e.StolenItemId != null) { thefts++; Assert.That(sim.ItemCount(e.StolenItemId), Is.EqualTo(0)); }
            }
            Assert.That(left, Is.GreaterThan(20));
            Assert.That(thefts, Is.InRange(3, left - 3));
        }

        [Test]
        public void SellingFeedsTheTownAndBuyingCostsMore()
        {
            Simulation sim = TestSupport.NewSim();
            Give(sim, "pelt_wolf", 3);
            TestSupport.Ok(sim.Execute(new SellItems { ItemId = "pelt_wolf", Count = 2 }));
            Assert.That(sim.ItemCount("old_coin"), Is.EqualTo(12));
            Assert.That(sim.ItemCount("pelt_wolf"), Is.EqualTo(1));
            Assert.That(sim.FindSettlement().Pending[SettlementValue.Prosperity], Is.EqualTo(2), "12 coin of trade at 5 coin per point");

            TestSupport.Ok(sim.Execute(new BuyItems { ItemId = "bandage", Count = 3 }));
            Assert.That(sim.ItemCount("old_coin"), Is.EqualTo(6));
            Assert.That(sim.ItemCount("bandage"), Is.EqualTo(3));

            TestSupport.Rejected(sim.Execute(new SellItems { ItemId = "pelt_wolf", Count = 5 }), "only has 1");
            TestSupport.Rejected(sim.Execute(new SellItems { ItemId = "humming_stone" }), "nobody trades");
            TestSupport.Rejected(sim.Execute(new BuyItems { ItemId = "medkit", Count = 1 }), "costs 12 coin");

            sim.Execute(new PassSeason());
            Assert.That(sim.FindSettlement().Values[SettlementValue.Prosperity], Is.EqualTo(38));
        }

        [Test]
        public void TradeNeedsAnOpenStore()
        {
            Simulation sim = TestSupport.NewSim();
            Give(sim, "jerky", 2);
            for (int i = 0; i < 4; i++) { sim.Execute(new InfluenceSettlement { Value = SettlementValue.Prosperity, Delta = -40 }); sim.Execute(new PassSeason()); }
            Assert.That(sim.ServiceIsOpen("general_store"), Is.False);
            TestSupport.Rejected(sim.Execute(new SellItems { ItemId = "jerky" }), "no store open");
        }

        [Test]
        public void AGuardChangesWhatHappensAtNight()
        {
            var night = new EventContext { AreaTags = new List<string> { "homestead" }, TimeOfDay = TimeOfDay.Night, AtHome = true };
            var day = new EventContext { AreaTags = new List<string> { "homestead" }, TimeOfDay = TimeOfDay.Day, AtHome = true };
            HashSet<string> Seen(bool guarded, EventContext where)
            {
                var seen = new HashSet<string>();
                for (ulong seed = 1; seed <= 15; seed++)
                {
                    Simulation sim = TestSupport.NewSim(seed);
                    Character silas = Join(sim, "Silas");
                    if (guarded) TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = silas.Id, JobId = "guard" }));
                    Assert.That(sim.HomeStatOf(HomeStat.Defense), Is.EqualTo(guarded ? 1 : 0));
                    for (int i = 0; i < 40; i++)
                    {
                        sim.Execute(new StorytellerTick { Context = where });
                        if (sim.GetActiveEvent() == null) continue;
                        seen.Add(sim.GetActiveEvent().EventId);
                        List<EventChoiceView> open = sim.GetActiveEvent().Choices.FindAll(c => c.Available);
                        sim.Execute(new ChooseEventOption { ChoiceIndex = open[open.Count - 1].Index });
                    }
                }
                return seen;
            }
            Assert.That(Seen(false, night), Does.Contain("night_raid"));
            Assert.That(Seen(true, night), Does.Not.Contain("night_raid"));
            Assert.That(Seen(true, day), Does.Contain("raid_turned_back"));
            Assert.That(Seen(false, day), Does.Not.Contain("raid_turned_back"));
        }

        [Test]
        public void ARaidCanSetTheHomesteadBack()
        {
            Simulation sim = TestSupport.NewSim();
            sim.State.Household.Structures["garden"] = 2; // test setup only
            Give(sim, "jerky", 6);
            TestSupport.Ok(sim.Execute(new StartEvent { EventId = "night_raid", Context = new EventContext { AtHome = true } }));
            CommandResult result = sim.Execute(new ChooseEventOption { ChoiceIndex = 1 });
            Assert.That(sim.StructureTier("garden"), Is.EqualTo(1));
            Assert.That(Find<StructureChanged>(result).OldTier, Is.EqualTo(2));
            Assert.That(sim.ItemCount("jerky"), Is.EqualTo(2));
            Assert.That(sim.State.Household.Morale, Is.EqualTo(40));
        }

        [Test]
        public void TheHomesteadSurvivesSaveAndLoad()
        {
            Simulation sim = TestSupport.NewSim(12);
            Character levi = Join(sim, "Levi"), opal = Join(sim, "Opal");
            Give(sim, "lumber", 10);
            TestSupport.Ok(sim.Execute(new BuildStructure { StructureId = "garden" }));
            TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = levi.Id, JobId = "farmer" }));
            TestSupport.Ok(sim.Execute(new AssignJob { CharacterId = opal.Id, JobId = "hunter" }));
            sim.Execute(new PassSeason());

            string json = sim.ToJson();
            Simulation loaded = Simulation.FromJson(json, TestSupport.Content);
            Assert.That(loaded.ToJson(), Is.EqualTo(json));
            Assert.That(loaded.State.Household.LastReport.Count, Is.EqualTo(2));
            foreach (Simulation s in new[] { sim, loaded }) for (int i = 0; i < 8; i++) s.Execute(new PassSeason());
            Assert.That(loaded.ToJson(), Is.EqualTo(sim.ToJson()));
        }
    }
}

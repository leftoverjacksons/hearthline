using System.Collections.Generic;
using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class SettlementTests
    {
        private static void Push(Simulation sim, SettlementValue value, int delta)
        {
            TestSupport.Ok(sim.Execute(new InfluenceSettlement { Value = value, Delta = delta }));
        }

        [Test]
        public void ANewGameHasCalvaryFordWithItsPeopleAndFirstServices()
        {
            Simulation sim = TestSupport.NewSim();
            SettlementState town = sim.FindSettlement();
            Assert.That(town.Id, Is.EqualTo("calvary_ford"));
            Assert.That(town.Values[SettlementValue.Prosperity], Is.EqualTo(35));
            Assert.That(town.OpenServices, Is.EquivalentTo(new[] { "general_store" }));

            Character shopkeeper = sim.FindResident("shopkeeper");
            Assert.That(shopkeeper, Is.Not.Null);
            Assert.That(sim.AgeOf(shopkeeper), Is.InRange(35, 55));
            Assert.That(sim.State.Household.MemberIds, Has.No.Member(shopkeeper.Id));
            Assert.That(sim.FindResident("preacher"), Is.Not.Null);
            Assert.That(sim.FindResident("blacksmith"), Is.Null);
        }

        [Test]
        public void InfluenceWaitsForTheSeasonToTurn()
        {
            Simulation sim = TestSupport.NewSim();
            Push(sim, SettlementValue.Prosperity, 8);
            Assert.That(sim.FindSettlement().Values[SettlementValue.Prosperity], Is.EqualTo(35), "you shape the town; you do not set it");

            CommandResult result = sim.Execute(new PassSeason());
            Assert.That(sim.FindSettlement().Values[SettlementValue.Prosperity], Is.EqualTo(43));
            Assert.That(sim.ServiceIsOpen("saloon"), Is.True, "saloon needs prosperity 40 and safety 35");
            Assert.That(new List<DomainEvent>(result.Events).Exists(e => e is ServiceOpened o && o.ServiceId == "saloon"), Is.True);
            TestSupport.Rejected(sim.Execute(new InfluenceSettlement { SettlementId = "atlantis", Value = SettlementValue.Safety, Delta = 1 }), "no settlement");
        }

        [Test]
        public void OneSeasonCanOnlyMoveATownSoFar()
        {
            Simulation sim = TestSupport.NewSim();
            Push(sim, SettlementValue.Prosperity, 500);
            sim.Execute(new PassSeason());
            Assert.That(sim.FindSettlement().Values[SettlementValue.Prosperity], Is.EqualTo(35 + sim.World.MaxChangePerSeason));
            sim.Execute(new PassSeason());
            Assert.That(sim.FindSettlement().Values[SettlementValue.Prosperity], Is.EqualTo(49), "the excess is not carried over; the town drifts back");
        }

        [Test]
        public void LeftAloneATownDriftsToItsBaselineAndStaysInRange()
        {
            Simulation sim = TestSupport.NewSim();
            for (int i = 0; i < 12; i++) sim.Execute(new PassSeason());
            SettlementState town = sim.FindSettlement();
            Assert.That(town.Values[SettlementValue.Prosperity], Is.EqualTo(30));
            Assert.That(town.Values[SettlementValue.Safety], Is.EqualTo(35));

            for (int i = 0; i < 12; i++) { Push(sim, SettlementValue.Safety, -50); sim.Execute(new PassSeason()); }
            Assert.That(town.Values[SettlementValue.Safety], Is.EqualTo(0));
        }

        [Test]
        public void ATownCanDeclineAndLoseWhatItHad()
        {
            Simulation sim = TestSupport.NewSim();
            Push(sim, SettlementValue.Prosperity, 10);
            sim.Execute(new PassSeason());
            Assert.That(sim.ServiceIsOpen("doctor"), Is.True, "prosperity 45");

            // A small dip does not close it: services do not flicker at the threshold.
            Push(sim, SettlementValue.Prosperity, -3);
            sim.Execute(new PassSeason());
            Assert.That(sim.FindSettlement().Values[SettlementValue.Prosperity], Is.EqualTo(42));
            Assert.That(sim.ServiceIsOpen("doctor"), Is.True);

            Push(sim, SettlementValue.Prosperity, -10);
            CommandResult result = sim.Execute(new PassSeason());
            Assert.That(sim.ServiceIsOpen("doctor"), Is.False);
            Assert.That(new List<DomainEvent>(result.Events).Exists(e => e is ServiceClosed c && c.ServiceId == "doctor"), Is.True);
        }

        [Test]
        public void AChangedPersonInTheHouseholdMakesTheTownStranger()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "exposure_glass_skin", Region = BodyRegion.HandL, Origin = new MarkOrigin { Cause = "the stone" } }));
            // Make sure the mark is in its visibly Changed stage whatever the seed rolls.
            sim.Head.Marks[0].Stage = "spread"; // test setup only
            for (int i = 0; i < 8; i++) sim.Execute(new PassSeason());
            Assert.That(sim.FindSettlement().Values[SettlementValue.Strangeness], Is.EqualTo(18));

            Simulation plain = TestSupport.NewSim();
            for (int i = 0; i < 8; i++) plain.Execute(new PassSeason());
            Assert.That(plain.FindSettlement().Values[SettlementValue.Strangeness], Is.EqualTo(10));
        }

        [Test]
        public void WhenTheShopkeeperDiesTheirChildTakesOverAndRemembers()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new StartEvent { EventId = "short_weight", Context = new EventContext { AreaTags = new List<string> { "town" } } }));
            Character old = sim.FindResident("shopkeeper");
            Assert.That(sim.GetActiveEvent().Cast["shopkeeper"], Is.EqualTo(old.Id));
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }));
            Assert.That(old.Memories[0].Text, Is.EqualTo("Ada Ward called me a cheat in my own store."));
            Assert.That(old.Opinions[sim.Head.Id], Is.EqualTo(-20));

            CommandResult result = sim.Execute(new KillCharacter { CharacterId = old.Id, Cause = "fever" });
            Character heir = sim.FindResident("shopkeeper");
            Assert.That(heir, Is.Not.SameAs(old));
            Assert.That(heir.Surname, Is.EqualTo(old.Surname));
            Assert.That(heir.ParentIds, Does.Contain(old.Id));
            Assert.That(heir.Memories[0].Text, Does.Contain("called me a cheat"), "the family's name outlives the person it was said to");
            Assert.That(new List<DomainEvent>(result.Events).Exists(e => e is ResidentSucceeded), Is.True);
            Assert.That(sim.Head, Is.Not.Null);
        }

        [Test]
        public void EventsPushOnTheTownAndTownStateGatesEvents()
        {
            Simulation sim = TestSupport.NewSim();
            var town = new EventContext { AreaTags = new List<string> { "town" }, TimeOfDay = TimeOfDay.Night };

            // No saloon yet, so the storyteller never offers the card game.
            for (int i = 0; i < 60; i++)
            {
                sim.Execute(new StorytellerTick { Context = town });
                Assert.That(sim.GetActiveEvent(), Is.Null);
            }
            Push(sim, SettlementValue.Prosperity, 10);
            sim.Execute(new PassSeason());
            bool offered = false;
            for (int i = 0; i < 60 && !offered; i++)
            {
                sim.Execute(new StorytellerTick { Context = town });
                offered = sim.GetActiveEvent() != null;
            }
            Assert.That(offered, Is.True);
            Assert.That(sim.GetActiveEvent().EventId, Is.EqualTo("cards_at_the_saloon"));
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 2 }));

            TestSupport.Ok(sim.Execute(new StartEvent { EventId = "road_work_party", Context = town }));
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }));
            int before = sim.FindSettlement().Values[SettlementValue.Prosperity];
            sim.Execute(new PassSeason());
            Assert.That(sim.FindSettlement().Values[SettlementValue.Prosperity], Is.EqualTo(before + 5));
        }

        [Test]
        public void SettlementsSurviveSaveAndLoad()
        {
            Simulation sim = TestSupport.NewSim(6);
            Push(sim, SettlementValue.Safety, 7);
            TestSupport.Ok(sim.Execute(new SetGrudge { FactionId = "reavers", Reason = "an old debt" }));
            string json = sim.ToJson();
            Simulation loaded = Simulation.FromJson(json, TestSupport.Content);
            Assert.That(loaded.ToJson(), Is.EqualTo(json));
            foreach (Simulation s in new[] { sim, loaded }) for (int i = 0; i < 6; i++) s.Execute(new PassSeason());
            Assert.That(loaded.ToJson(), Is.EqualTo(sim.ToJson()));
            Assert.That(loaded.FindSettlement().Values[SettlementValue.Safety], Is.EqualTo(42));
        }
    }

    public class ReputationTests
    {
        [Test]
        public void ReputationFadesTowardNothing()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new ChangeReputation { FactionId = "homesteaders", Delta = 3 }));
            TestSupport.Ok(sim.Execute(new ChangeReputation { FactionId = "continuity", Delta = -2 }));
            sim.Execute(new PassSeason());
            Assert.That(sim.ReputationWith("homesteaders"), Is.EqualTo(2));
            Assert.That(sim.ReputationWith("continuity"), Is.EqualTo(-1));
            for (int i = 0; i < 6; i++) sim.Execute(new PassSeason());
            Assert.That(sim.ReputationWith("homesteaders"), Is.EqualTo(0));
            Assert.That(sim.ReputationWith("continuity"), Is.EqualTo(0));
        }

        [Test]
        public void AGrudgeDoesNotFadeAndCapsWhatCanBeEarned()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new ChangeReputation { FactionId = "reavers", Delta = 10 }));
            TestSupport.Ok(sim.Execute(new SetGrudge { FactionId = "reavers", Reason = "the ridge camp" }));
            Assert.That(sim.HasGrudge("reavers"), Is.True);
            Assert.That(sim.ReputationWith("reavers"), Is.EqualTo(sim.World.GrudgeCeiling));

            for (int i = 0; i < 20; i++) sim.Execute(new PassSeason());
            Assert.That(sim.ReputationWith("reavers"), Is.EqualTo(sim.World.GrudgeCeiling), "a grudge does not decay");
            TestSupport.Ok(sim.Execute(new ChangeReputation { FactionId = "reavers", Delta = 50 }));
            Assert.That(sim.ReputationWith("reavers"), Is.EqualTo(sim.World.GrudgeCeiling));

            TestSupport.Rejected(sim.Execute(new SetGrudge { FactionId = "reavers", Reason = "again" }), "already exists");
            TestSupport.Ok(sim.Execute(new SetGrudge { FactionId = "reavers", Held = false }));
            sim.Execute(new PassSeason());
            Assert.That(sim.ReputationWith("reavers"), Is.EqualTo(sim.World.GrudgeCeiling + 1), "once settled, it starts to fade");
        }

        [Test]
        public void TheHeirInheritsTheGrudgeAndTheReaversComeForThem()
        {
            Simulation sim = TestSupport.NewSim(2);
            Character grandmother = sim.Head;
            var ridge = new EventContext { AreaTags = new List<string> { "ridge" }, TimeOfDay = TimeOfDay.Day };
            TestSupport.Ok(sim.Execute(new AddHouseholdMember { GivenName = "Eli", AgeYears = 20, Bond = HouseholdBond.Ward }));
            Character heir = sim.State.Characters[sim.State.Characters.Count - 1];

            // Rob the ridge camp: go around is choice 2; go up openly then take the sack needs luck, so creep is forced by skill.
            grandmother.BaseStats["stealth"] = 2; // test setup only
            TestSupport.Ok(sim.Execute(new StartEvent { EventId = "smoke_on_the_ridge", Context = ridge }));
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }));
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }));
            Assert.That(sim.HasGrudge("reavers"), Is.True);
            Assert.That(sim.State.Household.Grudges[0].Reason, Is.EqualTo("Ada Ward robbed the ridge camp"));

            TestSupport.Ok(sim.Execute(new KillCharacter { CharacterId = grandmother.Id, Cause = "old age" }));
            TestSupport.Ok(sim.Execute(new ChooseHeir { HeirId = heir.Id }));
            for (int i = 0; i < 40; i++) sim.Execute(new PassSeason());
            Assert.That(sim.HasGrudge("reavers"), Is.True, "ten years on, the grudge is still held against the name");

            var road = new EventContext { AreaTags = new List<string> { "river_crossing" }, TimeOfDay = TimeOfDay.Night };
            var seen = new HashSet<string>();
            for (int i = 0; i < 80; i++)
            {
                sim.Execute(new StorytellerTick { Context = road });
                if (sim.GetActiveEvent() == null) continue;
                seen.Add(sim.GetActiveEvent().EventId);
                List<EventChoiceView> open = sim.GetActiveEvent().Choices.FindAll(c => c.Available);
                TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = open[open.Count - 1].Index }));
                if (sim.Head == null || sim.Head.Downed) break;
                sim.Execute(new AdvanceDays { Days = 31 });
            }
            Assert.That(seen, Does.Contain("reavers_remember"));
        }

        [Test]
        public void WithoutAGrudgeTheReaversDoNotComeLooking()
        {
            Simulation sim = TestSupport.NewSim(2);
            TestSupport.Rejected(sim.Execute(new SetGrudge { FactionId = "reavers", Held = false }), "no such grudge");
            var road = new EventContext { AreaTags = new List<string> { "woods" }, TimeOfDay = TimeOfDay.Day };
            for (int i = 0; i < 80; i++)
            {
                sim.Execute(new StorytellerTick { Context = road });
                if (sim.GetActiveEvent() == null) continue;
                Assert.That(sim.GetActiveEvent().EventId, Is.Not.EqualTo("reavers_remember"));
                List<EventChoiceView> open = sim.GetActiveEvent().Choices.FindAll(c => c.Available);
                TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = open[open.Count - 1].Index }));
            }
        }
    }
}

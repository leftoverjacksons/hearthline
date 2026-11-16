using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class CommandTests
    {
        [Test]
        public void NewGameStartsWithAnAdultHeadOfHousehold()
        {
            Simulation sim = Simulation.NewGame(TestSupport.Content, new NewGameOptions { Seed = 5, Surname = "Ward", HeadGivenName = "Ada", HeadAgeYears = 28 });
            Assert.That(sim.Head.GivenName, Is.EqualTo("Ada"));
            Assert.That(sim.State.Household.Surname, Is.EqualTo("Ward"));
            Assert.That(sim.State.Household.MemberIds, Does.Contain(sim.Head.Id));
            Assert.That(sim.AgeOf(sim.Head), Is.EqualTo(28));
            Assert.That(sim.LifeStageOf(sim.Head), Is.EqualTo(LifeStage.Adult));
            Assert.That(sim.Date.Year, Is.EqualTo(1));
        }

        [Test]
        public void PassSeasonLandsOnTheFirstDayOfTheNextSeason()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 10 }));
            CommandResult result = sim.Execute(new PassSeason());
            TestSupport.Ok(result);
            Assert.That(sim.Date.Season, Is.EqualTo(Season.Summer));
            Assert.That(sim.Date.DayOfSeason, Is.EqualTo(1));

            SeasonPassed passed = null;
            foreach (DomainEvent e in result.Events) if (e is SeasonPassed p) passed = p;
            Assert.That(passed, Is.Not.Null);
            Assert.That(passed.Ended, Is.EqualTo(Season.Spring));
            Assert.That(passed.Began, Is.EqualTo(Season.Summer));

            for (int i = 0; i < 3; i++) TestSupport.Ok(sim.Execute(new PassSeason()));
            Assert.That(sim.Date.Year, Is.EqualTo(2));
            Assert.That(sim.Date.Season, Is.EqualTo(Season.Spring));
        }

        [Test]
        public void PeopleAgeAsYearsPass()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = GameDate.DaysPerYear - 1 }));
            Assert.That(sim.AgeOf(sim.Head), Is.EqualTo(28));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 1 }));
            Assert.That(sim.AgeOf(sim.Head), Is.EqualTo(29));
        }

        [TestCase(5, LifeStage.Child)]
        [TestCase(12, LifeStage.Child)]
        [TestCase(13, LifeStage.Youth)]
        [TestCase(18, LifeStage.Adult)]
        [TestCase(45, LifeStage.Adult)]
        [TestCase(46, LifeStage.Elder)]
        [TestCase(65, LifeStage.Elder)]
        [TestCase(66, LifeStage.Old)]
        public void SetAgeMovesACharacterThroughLifeStages(int age, LifeStage expected)
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new SetAge { CharacterId = sim.Head.Id, AgeYears = age }));
            Assert.That(sim.AgeOf(sim.Head), Is.EqualTo(age));
            Assert.That(sim.LifeStageOf(sim.Head), Is.EqualTo(expected));
        }

        [Test]
        public void KillingTheHeadLeavesTheHouseholdWithoutOne()
        {
            Simulation sim = TestSupport.NewSim();
            Character head = sim.Head;
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 3 }));
            CommandResult result = sim.Execute(new KillCharacter { CharacterId = head.Id, Cause = "shot by a Reaver" });
            TestSupport.Ok(result);

            Assert.That(head.Alive, Is.False);
            Assert.That(head.DeathDay, Is.EqualTo(3));
            Assert.That(sim.Head, Is.Null);
            Assert.That(((CharacterDied)result.Events[0]).WasHeadOfHousehold, Is.True);

            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 5 * GameDate.DaysPerYear }));
            Assert.That(sim.AgeOf(head), Is.EqualTo(28), "the dead stop aging");
        }

        [Test]
        public void TheDeadCannotBeActedOn()
        {
            Simulation sim = TestSupport.NewSim();
            int id = sim.Head.Id;
            TestSupport.Rejected(sim.Execute(new KillCharacter { CharacterId = id, Cause = " " }), "needs a cause");
            TestSupport.Ok(sim.Execute(new KillCharacter { CharacterId = id, Cause = "fever" }));
            TestSupport.Rejected(sim.Execute(new KillCharacter { CharacterId = id, Cause = "fever" }), "is dead");
            TestSupport.Rejected(sim.Execute(new AddMark { CharacterId = id, DefId = "scar_claw", Region = BodyRegion.Face, Origin = TestSupport.Boar }), "is dead");
            TestSupport.Rejected(sim.Execute(new SetAge { CharacterId = id, AgeYears = 30 }), "is dead");
        }

        [Test]
        public void AdvanceDaysRejectsNonPositive()
        {
            TestSupport.Rejected(TestSupport.NewSim().Execute(new AdvanceDays { Days = 0 }), "at least 1");
        }
    }
}

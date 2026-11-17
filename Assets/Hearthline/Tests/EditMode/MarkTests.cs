using System.Collections.Generic;
using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class MarkTests
    {
        [Test]
        public void AddedMarkIsNamedByItsOrigin()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 2 * GameDate.DaysPerYear }));
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Severity = 2, Origin = TestSupport.Boar }));

            Mark mark = sim.Head.Marks[0];
            Assert.That(mark.OriginText, Is.EqualTo("Scar from the boar at Miller's Ford, spring of year 3"));
            Assert.That(mark.Category, Is.EqualTo(MarkCategory.Scar));
            Assert.That(mark.Stage, Is.EqualTo("scarred"));
            Assert.That(mark.DayGained, Is.EqualTo(sim.State.Day));
        }

        [Test]
        public void AddMarkRaisesAnEventAfterStateHasChanged()
        {
            Simulation sim = TestSupport.NewSim();
            var seen = new List<DomainEvent>();
            int marksWhenRaised = -1;
            sim.EventRaised += e => { seen.Add(e); marksWhenRaised = sim.Head.Marks.Count; };

            CommandResult result = sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Origin = TestSupport.Boar });

            Assert.That(seen.Count, Is.EqualTo(1));
            Assert.That(seen[0], Is.InstanceOf<MarkAdded>());
            Assert.That(((MarkAdded)seen[0]).MarkId, Is.EqualTo(sim.Head.Marks[0].Id));
            Assert.That(marksWhenRaised, Is.EqualTo(1));
            Assert.That(result.Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void RejectedCommandChangesNothingAndRaisesNothing()
        {
            Simulation sim = TestSupport.NewSim();
            string before = sim.ToJson();
            int raised = 0;
            sim.EventRaised += e => raised++;

            TestSupport.Rejected(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "no_such_mark", Region = BodyRegion.Face, Origin = TestSupport.Boar }), "no mark definition");
            TestSupport.Rejected(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "loss_eye", Region = BodyRegion.LegL, Origin = TestSupport.Boar }), "cannot be on region");
            TestSupport.Rejected(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Severity = 4, Origin = TestSupport.Boar }), "severity");
            TestSupport.Rejected(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face }), "origin cause");
            TestSupport.Rejected(sim.Execute(new AddMark { CharacterId = 999, DefId = "scar_claw", Region = BodyRegion.Face, Origin = TestSupport.Boar }), "no character");

            Assert.That(sim.ToJson(), Is.EqualTo(before));
            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void MarkWithoutCauseTokenNeedsNoCause()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "age_grey", Region = BodyRegion.Head }));
            Assert.That(sim.Head.Marks[0].OriginText, Is.EqualTo("Went grey in the spring of year 1"));
        }

        [Test]
        public void MarksChangeStats()
        {
            Simulation sim = TestSupport.NewSim();
            Character head = sim.Head;
            Assert.That(sim.GetStat(head, "reload_speed"), Is.EqualTo(1.0));
            Assert.That(sim.GetStat(head, "tracking"), Is.EqualTo(0.0));

            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = head.Id, DefId = "loss_fingers", Region = BodyRegion.HandR, Origin = TestSupport.Boar }));
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Origin = TestSupport.Boar }));

            Assert.That(sim.GetStat(head, "reload_speed"), Is.EqualTo(0.7).Within(1e-9));
            Assert.That(sim.GetStat(head, "tracking"), Is.EqualTo(1.0));
            Assert.That(sim.ReactionTagsOf(head), Is.EquivalentTo(new[] { "maimed", "scarred" }));
        }

        [Test]
        public void RegionLimitedEffectsOnlyApplyOnThatRegion()
        {
            // The design doc's example: a burned hand slows reloads. A burned leg does not.
            Simulation sim = TestSupport.NewSim();
            Character head = sim.Head;
            var fire = new MarkOrigin { Cause = "the barn fire" };

            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = head.Id, DefId = "scar_burn", Region = BodyRegion.LegL, Origin = fire }));
            Assert.That(sim.GetStat(head, "reload_speed"), Is.EqualTo(1.0));

            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = head.Id, DefId = "scar_burn", Region = BodyRegion.HandR, Origin = fire }));
            Assert.That(sim.GetStat(head, "reload_speed"), Is.EqualTo(0.85).Within(1e-9));
        }

        [Test]
        public void BaseStatOverridesCombineWithMarks()
        {
            Simulation sim = TestSupport.NewSim();
            Character head = sim.Head;
            head.BaseStats["tracking"] = 4; // test setup only; game code changes state through commands
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Origin = TestSupport.Boar }));
            Assert.That(sim.GetStat(head, "tracking"), Is.EqualTo(5.0));
        }

        [Test]
        public void RemoveMarkRemovesItsEffects()
        {
            Simulation sim = TestSupport.NewSim();
            Character head = sim.Head;
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = head.Id, DefId = "loss_limp", Region = BodyRegion.LegL, Origin = TestSupport.Boar }));
            Assert.That(sim.GetStat(head, "move_speed"), Is.EqualTo(0.75).Within(1e-9));

            TestSupport.Ok(sim.Execute(new RemoveMark { CharacterId = head.Id, MarkId = head.Marks[0].Id }));
            Assert.That(head.Marks, Is.Empty);
            Assert.That(sim.GetStat(head, "move_speed"), Is.EqualTo(1.0));
            TestSupport.Rejected(sim.Execute(new RemoveMark { CharacterId = head.Id, MarkId = 12345 }), "no mark");
        }

        [Test]
        public void StagesProgressOverTime()
        {
            Simulation sim = TestSupport.NewSim();
            Character head = sim.Head;
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = head.Id, DefId = "knack_dowsing", Region = BodyRegion.Head, Origin = new MarkOrigin { Cause = "a night in the hollow" } }));
            Assert.That(sim.AbilitiesOf(head), Is.Empty);

            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 111 }));
            Assert.That(head.Marks[0].Stage, Is.EqualTo("latent"));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 1 }));
            Assert.That(head.Marks[0].Stage, Is.EqualTo("waking"));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 112 }));
            Assert.That(head.Marks[0].Stage, Is.EqualTo("settled"));
            Assert.That(sim.AbilitiesOf(head), Does.Contain("dowse_thin"));
            Assert.That(sim.GetStat(head, "knack"), Is.EqualTo(2.0));
        }

        [Test]
        public void ExposureCanDeepenOrSettleDependingOnTheSeed()
        {
            var endings = new HashSet<string>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Simulation sim = TestSupport.NewSim(seed);
                TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "exposure_pale_eyes", Region = BodyRegion.Face, Origin = new MarkOrigin { Cause = "the light in the trees" } }));
                TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 60 }));
                endings.Add(sim.Head.Marks[0].Stage);
            }
            Assert.That(endings, Is.EquivalentTo(new[] { "lit", "settled" }));
        }

        [Test]
        public void MarkViewsCarryVisualsForTheCurrentStage()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Severity = 2, Origin = TestSupport.Boar }));
            MarkView view = sim.MarkViewsOf(sim.Head)[0];
            Assert.That(view.Name, Is.EqualTo("Claw scar"));
            Assert.That(view.Visual.Decal, Is.EqualTo("scar_claw"));
            Assert.That(view.Region, Is.EqualTo(BodyRegion.Face));
            Assert.That(view.Orphaned, Is.False);
        }

        [Test]
        public void MarkViewsDescribeEffectsForTheirRegion()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_burn", Region = BodyRegion.HandL, Origin = TestSupport.Boar }));
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_burn", Region = BodyRegion.Face, Origin = TestSupport.Boar }));
            List<MarkView> views = sim.MarkViewsOf(sim.Head);

            Assert.That(views[0].Effects, Is.EqualTo(new[] { "reaction: scarred", "reload_speed ×0.85" }));
            Assert.That(views[1].Effects, Is.EqualTo(new[] { "reaction: scarred", "persuasion -1", "reaction: disfigured" }));
        }

        [Test]
        public void ClawScarOnTheFaceListsItsEffects()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Origin = TestSupport.Boar }));
            Assert.That(sim.MarkViewsOf(sim.Head)[0].Effects, Is.EqualTo(new[] { "reaction: scarred", "tracking +1" }));
        }

        [Test]
        public void ACharacterCanCarryTwelveMarks()
        {
            Simulation sim = TestSupport.NewSim();
            var regions = (BodyRegion[])System.Enum.GetValues(typeof(BodyRegion));
            for (int i = 0; i < 12; i++)
                TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_ragged", Region = regions[i % regions.Length], Origin = TestSupport.Boar }));
            Assert.That(sim.MarkViewsOf(sim.Head).Count, Is.EqualTo(12));
        }
    }
}

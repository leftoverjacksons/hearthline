using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class SaveLoadTests
    {
        private static Simulation Reload(Simulation sim) => Simulation.FromJson(sim.ToJson(), TestSupport.Content);

        [Test]
        public void AScarSurvivesSaveAndLoad()
        {
            // The simulation half of the M0 gate.
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Severity = 2, Origin = TestSupport.Boar }));

            Simulation loaded = Reload(sim);

            MarkView view = loaded.MarkViewsOf(loaded.Head)[0];
            Assert.That(view.DefId, Is.EqualTo("scar_claw"));
            Assert.That(view.Region, Is.EqualTo(BodyRegion.Face));
            Assert.That(view.Severity, Is.EqualTo(2));
            Assert.That(view.OriginText, Is.EqualTo("Scar from the boar at Miller's Ford, spring of year 1"));
            Assert.That(view.Visual.Decal, Is.EqualTo("scar_claw"));
            Assert.That(loaded.GetStat(loaded.Head, "tracking"), Is.EqualTo(1.0));
        }

        [Test]
        public void SaveLoadSaveIsIdentical()
        {
            Simulation sim = BusySim();
            string first = sim.ToJson();
            Assert.That(Reload(sim).ToJson(), Is.EqualTo(first));
        }

        [Test]
        public void ALoadedGameContinuesExactlyLikeTheOriginal()
        {
            Simulation original = BusySim();
            Simulation loaded = Reload(original);
            foreach (Simulation sim in new[] { original, loaded })
            {
                Mark wound = sim.Head.Marks[0];
                sim.Execute(new InflictHit { CharacterId = sim.Head.Id, Region = BodyRegion.LegR, Damage = DamageType.Blade, Origin = TestSupport.Boar });
                sim.Execute(new InfirmaryTreatWound { CharacterId = sim.Head.Id, MarkId = wound.Id, InfirmaryTier = 1 });
                sim.Execute(new AdvanceDays { Days = 200 });
            }
            Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
        }

        [Test]
        public void DeathAndAVacantHeadSurviveSaveAndLoad()
        {
            Simulation sim = TestSupport.NewSim();
            int id = sim.Head.Id;
            TestSupport.Ok(sim.Execute(new KillCharacter { CharacterId = id, Cause = "fever" }));
            Simulation loaded = Reload(sim);
            Assert.That(loaded.Head, Is.Null);
            Assert.That(loaded.FindCharacter(id).Alive, Is.False);
            Assert.That(loaded.FindCharacter(id).DeathCause, Is.EqualTo("fever"));
        }

        [Test]
        public void ASaveFromANewerBuildIsRefused()
        {
            string json = TestSupport.NewSim().ToJson().Replace("\"SaveVersion\": " + SaveFormat.CurrentVersion, "\"SaveVersion\": 999");
            var e = Assert.Throws<SaveFormatException>(() => SaveFormat.FromJson(json));
            Assert.That(e.Message, Does.Contain("newer"));
        }

        [Test]
        public void GarbageIsRefusedWithASaveFormatException()
        {
            Assert.Throws<SaveFormatException>(() => SaveFormat.FromJson("not json"));
            Assert.Throws<SaveFormatException>(() => SaveFormat.FromJson("{}"));
            Assert.Throws<SaveFormatException>(() => SaveFormat.FromJson("{ \"SaveVersion\": 1, \"Surprise\": true }"));
        }

        [Test]
        public void MarksWhoseDefinitionIsGoneAreReportedNotLost()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Origin = TestSupport.Boar }));
            Simulation loaded = Simulation.FromJson(sim.ToJson().Replace("\"scar_claw\"", "\"scar_retired\""), TestSupport.Content);

            Assert.That(loaded.FindOrphanedMarks().Count, Is.EqualTo(1));
            MarkView view = loaded.MarkViewsOf(loaded.Head)[0];
            Assert.That(view.Orphaned, Is.True);
            Assert.That(view.OriginText, Does.Contain("the boar"));
            TestSupport.Ok(loaded.Execute(new AdvanceDays { Days = 5 }));
            Assert.That(loaded.GetStat(loaded.Head, "tracking"), Is.EqualTo(0.0));
        }

        private static Simulation BusySim()
        {
            Simulation sim = TestSupport.NewSim(77);
            int head = sim.Head.Id;
            sim.Execute(new InflictHit { CharacterId = head, Region = BodyRegion.HandL, Damage = DamageType.Gunshot, Severity = 2, Origin = TestSupport.Boar });
            sim.Execute(new AddMark { CharacterId = head, DefId = "exposure_pale_eyes", Region = BodyRegion.Face, Origin = new MarkOrigin { EventId = "light_in_trees", Cause = "the light in the trees" } });
            sim.Execute(new AddMark { CharacterId = head, DefId = "knack_dowsing", Region = BodyRegion.Head, Origin = new MarkOrigin { Cause = "a night in the hollow" } });
            sim.Execute(new AdvanceDays { Days = 1 });
            sim.Execute(new SetAge { CharacterId = head, AgeYears = 40 });
            return sim;
        }
    }
}

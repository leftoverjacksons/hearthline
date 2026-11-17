using System.Collections.Generic;
using System.Linq;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using NUnit.Framework;

namespace Hearthline.Tests
{
    /// <summary>The hunt in the simulation (docs/specs/hunt.md): sign, the animal's escapes, the kill and what it gives.</summary>
    public class HuntTests
    {
        private static Simulation Hunting(out Character buck, ulong seed = 1)
        {
            Simulation sim = TestSupport.NewSim(seed);
            TestSupport.Ok(sim.Execute(new StartHunt { HuntId = "crookhorn" }));
            buck = sim.FindCharacter(sim.Hunt.AnimalId);
            return sim;
        }

        private static void ReadAll(Simulation sim)
        {
            for (int guard = 0; !sim.Hunt.Revealed && guard < 20; guard++)
                TestSupport.Ok(sim.Execute(new ReadSign { Index = sim.Hunt.Progress }));
        }

        private static void Down(Simulation sim, Character buck, int? gearId = null) =>
            TestSupport.Ok(sim.Execute(new ShotHit { TargetId = buck.Id, Region = BodyRegion.Head, ShooterId = sim.Head.Id, GearId = gearId, Distance = 30 }));

        [Test]
        public void TakingUpTheHuntCreatesTheAnimalOnce()
        {
            Simulation sim = Hunting(out Character buck);
            Assert.That(buck.GivenName, Is.EqualTo("Crookhorn"));
            Assert.That(buck.Archetype, Is.EqualTo("deer"));
            Assert.That(sim.Hunt.Progress, Is.EqualTo(0));
            Assert.That(sim.Hunt.Revealed, Is.False);
            TestSupport.Rejected(sim.Execute(new StartHunt { HuntId = "crookhorn" }), "already on");
            TestSupport.Rejected(TestSupport.NewSim().Execute(new StartHunt { HuntId = "nope" }), "no hunt");
        }

        [Test]
        public void ReadingSignInOrderLeadsToHim()
        {
            Simulation sim = Hunting(out _);
            int sign = sim.HuntDefOf("crookhorn").Sign;
            TestSupport.Rejected(sim.Execute(new ReadSign { Index = 3 }), "not the next one");
            int reads = 0;
            while (!sim.Hunt.Revealed)
            {
                CommandResult r = sim.Execute(new ReadSign { Index = sim.Hunt.Progress });
                TestSupport.Ok(r);
                reads++;
            }
            Assert.That(sim.Hunt.Progress, Is.EqualTo(sign));
            Assert.That(reads, Is.EqualTo(sign), "An untrained tracker reads every sign.");
            TestSupport.Rejected(sim.Execute(new ReadSign { Index = sign }), "already led");
        }

        [Test]
        public void AGoodTrackerSkipsAhead()
        {
            int totalReads = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Simulation sim = Hunting(out _, seed);
                sim.Head.BaseStats["tracking"] = 3;
                while (!sim.Hunt.Revealed) { TestSupport.Ok(sim.Execute(new ReadSign { Index = sim.Hunt.Progress })); totalReads++; }
            }
            Assert.That(totalReads / 20.0, Is.LessThan(sim_Sign - 1.0));
        }

        private const int sim_Sign = 6;

        [Test]
        public void ABoltColdensTheTrail()
        {
            Simulation sim = Hunting(out _);
            TestSupport.Rejected(sim.Execute(new ReportAnimalBolted()), "hasn't been found");
            ReadAll(sim);
            HuntDef def = sim.HuntDefOf("crookhorn");
            CommandResult r = sim.Execute(new ReportAnimalBolted());
            TestSupport.Ok(r);
            Assert.That(sim.Hunt.Revealed, Is.False);
            Assert.That(sim.Hunt.Progress, Is.EqualTo(def.Sign - def.BoltCost));
            Assert.That(sim.Hunt.Bolts, Is.EqualTo(1));
        }

        [Test]
        public void ButcheringNeedsHimDownAndGivesTheHideOnce()
        {
            Simulation sim = Hunting(out Character buck);
            TestSupport.Ok(sim.Execute(new SetOut()));
            ReadAll(sim);
            TestSupport.Rejected(sim.Execute(new ButcherAnimal()), "isn't down");
            Down(sim, buck);
            Assert.That(buck.Downed, Is.True);
            TestSupport.Ok(sim.Execute(new ButcherAnimal()));
            Assert.That(sim.PackCount("crookhorn_hide"), Is.EqualTo(1), "Into the pack on a trip.");
            Assert.That(sim.PackCount("crookhorn_antlers"), Is.EqualTo(1));
            Assert.That(sim.PackCount("venison"), Is.EqualTo(6), "No knife: the plain yield.");
            Assert.That(buck.Alive, Is.False);
            Assert.That(sim.Hunt, Is.Null);
            Assert.That(sim.HuntDone("crookhorn"), Is.True);
            Assert.That(sim.ChronicleOf(sim.Head).Last().Text, Does.Contain("Crookhorn"));
            TestSupport.Rejected(sim.Execute(new StartHunt { HuntId = "crookhorn" }), "already dead");
        }

        [Test]
        public void AKnifeGetsMoreMeatAndTheGunRemembersHim()
        {
            Simulation sim = Hunting(out Character buck);
            TestSupport.Ok(sim.Execute(new GiveGear { DefId = "hunting_knife", CharacterId = sim.Head.Id }));
            TestSupport.Ok(sim.Execute(new GiveGear { DefId = "lever_rifle", CharacterId = sim.Head.Id }));
            GearItem rifle = sim.Head.Gear.Single(g => g.DefId == "lever_rifle");
            ReadAll(sim);
            int before = sim.ItemCount("venison");
            Down(sim, buck, rifle.Id);
            TestSupport.Ok(sim.Execute(new ButcherAnimal()));
            Assert.That(sim.ItemCount("venison") - before, Is.EqualTo(10));
            Assert.That(rifle.Ledger.Last().Text, Does.Contain("Crookhorn"));
        }

        [Test]
        public void GivingUpEndsTheHuntButHeStillLives()
        {
            Simulation sim = Hunting(out Character buck);
            TestSupport.Ok(sim.Execute(new GiveUpHunt()));
            Assert.That(sim.Hunt, Is.Null);
            Assert.That(buck.Alive, Is.True);
            Assert.That(sim.HuntDone("crookhorn"), Is.False);
            TestSupport.Ok(sim.Execute(new StartHunt { HuntId = "crookhorn" }));
        }

        [Test]
        public void AHuntSurvivesTheSaveFormat()
        {
            Simulation sim = Hunting(out _);
            TestSupport.Ok(sim.Execute(new ReadSign { Index = 0 }));
            Simulation loaded = Simulation.FromJson(sim.ToJson(), TestSupport.Content);
            Assert.That(loaded.Hunt.Progress, Is.EqualTo(sim.Hunt.Progress));
            Assert.That(loaded.Hunt.Seed, Is.EqualTo(sim.Hunt.Seed));
            Assert.That(loaded.ToJson(), Is.EqualTo(sim.ToJson()));
        }

        // ---- Content -------------------------------------------------------------------------------

        private const string Vocab = @"
stats:
  - { id: tracking, name: Tracking, base: 0 }
reaction_tags: [guilty]
abilities: [night_sight]
area_tags: [woods]
archetypes: [deer]
visuals: {}
";
        private const string Hunt = @"
id: test_hunt
name: A test hunt
offer: A deer.
animal: { archetype: deer, name: Test }
sign: 3
area_tags_any: [woods]
yields:
  meat: { item: test_meat, count: 2, with_knife: 1 }
  items: { test_hide: 1 }
chronicle: ""{hero.name} got the deer.""
";

        private static void AssertHuntError(string hunt, string expected)
        {
            ContentLoadResult result = ContentLoader.Load(new[]
            {
                new ContentFile { Path = "vocab.yaml", Text = Vocab },
                new ContentFile { Path = "items/test_meat.yaml", Text = "id: test_meat\nname: Meat\nweight: 1\n" },
                new ContentFile { Path = "items/test_hide.yaml", Text = "id: test_hide\nname: Hide\nweight: 1\n" },
                new ContentFile { Path = "hunts/test_hunt.yaml", Text = hunt },
            });
            if (expected == null) { Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors)); return; }
            foreach (ContentError e in result.Errors) if (e.Message.Contains(expected)) return;
            Assert.Fail("Expected an error containing \"" + expected + "\" but got:\n" + string.Join("\n", result.Errors));
        }

        [Test] public void HuntContentBaselineIsValid() => AssertHuntError(Hunt, null);
        [Test] public void HuntAnimalMustBeAnArchetype() => AssertHuntError(Hunt.Replace("archetype: deer", "archetype: bear"), "no archetype 'bear'");
        [Test] public void HuntAreaTagsMustExist() => AssertHuntError(Hunt.Replace("[woods]", "[swamp]"), "no area tag 'swamp'");
        [Test] public void HuntYieldsMustBeItems() => AssertHuntError(Hunt.Replace("test_hide: 1", "nothing: 1"), "not a stackable item");
        [Test] public void HuntChronicleNamesTheHunter() => AssertHuntError(Hunt.Replace("{hero.name} got", "Someone got"), "{hero.name}");
        [Test] public void HuntNeedsSign() => AssertHuntError(Hunt.Replace("sign: 3", "sign: 0"), "'sign' must be at least 1");
    }
}

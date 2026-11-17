using System.Collections.Generic;
using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class GatherTests
    {
        private static Simulation Away()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new SetOut()));
            return sim;
        }

        [Test]
        public void GatheringOnATripFillsThePack()
        {
            Simulation sim = Away();
            CommandResult result = sim.Execute(new Gather { SpotId = "berries_001", ItemId = "wild_berries" });
            TestSupport.Ok(result);
            int count = sim.PackCount("wild_berries");
            Assert.That(count, Is.InRange(2, 5));
            Assert.That(sim.ItemCount("wild_berries"), Is.EqualTo(0));
            Gathered gathered = null;
            foreach (DomainEvent e in result.Events) if (e is Gathered g) gathered = g;
            Assert.That(gathered, Is.Not.Null);
            Assert.That(gathered.Count, Is.EqualTo(count));
            Assert.That(gathered.IntoPack, Is.True);
        }

        [Test]
        public void GatheringAtHomeFillsTheStores()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new Gather { SpotId = "deadfall_yard", ItemId = "firewood" }));
            Assert.That(sim.ItemCount("firewood"), Is.InRange(2, 4));
        }

        [Test]
        public void ASpotIsPickedOverUntilItRegrows()
        {
            Simulation sim = Away();
            TestSupport.Ok(sim.Execute(new Gather { SpotId = "berries_001", ItemId = "wild_berries" }));
            Assert.That(sim.DaysUntilRegrown("berries_001", "wild_berries"), Is.EqualTo(10));

            CommandResult again = sim.Execute(new Gather { SpotId = "berries_001", ItemId = "wild_berries" });
            Assert.That(again.Ok, Is.False);
            Assert.That(again.Error, Does.Contain("picked over"));
            // Another spot is fine.
            TestSupport.Ok(sim.Execute(new Gather { SpotId = "berries_002", ItemId = "wild_berries" }));

            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 10 }));
            Assert.That(sim.DaysUntilRegrown("berries_001", "wild_berries"), Is.EqualTo(0));
            TestSupport.Ok(sim.Execute(new Gather { SpotId = "berries_001", ItemId = "wild_berries" }));
        }

        [Test]
        public void GatheringRejectsBadRequests()
        {
            Simulation sim = TestSupport.NewSim();
            Assert.That(sim.Execute(new Gather { SpotId = "x", ItemId = "no_such_item" }).Ok, Is.False);
            Assert.That(sim.Execute(new Gather { SpotId = "x", ItemId = "jerky" }).Error, Does.Contain("cannot be gathered"));
            Assert.That(sim.Execute(new Gather { SpotId = "", ItemId = "wild_berries" }).Ok, Is.False);

            for (int i = 0; i < 10 && !sim.Head.Downed; i++)
                sim.Execute(new InflictHit { CharacterId = sim.Head.Id, Region = BodyRegion.Torso, Damage = DamageType.Gunshot, Severity = 3, Origin = TestSupport.Boar });
            Assert.That(sim.Execute(new Gather { SpotId = "x", ItemId = "wild_berries" }).Error, Does.Contain("no state to gather"));
        }

        [Test]
        public void PickedOverSpotsSurviveSaveAndLoad()
        {
            Simulation sim = Away();
            TestSupport.Ok(sim.Execute(new Gather { SpotId = "salvage_overpass", ItemId = "scrap_metal" }));
            Simulation loaded = Simulation.FromJson(sim.ToJson(), TestSupport.Content);
            Assert.That(loaded.DaysUntilRegrown("salvage_overpass", "scrap_metal"), Is.EqualTo(60));
            Assert.That(loaded.Execute(new Gather { SpotId = "salvage_overpass", ItemId = "scrap_metal" }).Ok, Is.False);
        }

        [Test]
        public void SameSeedGathersTheSameAmount()
        {
            int Yield()
            {
                Simulation sim = TestSupport.NewSim(seed: 42);
                TestSupport.Ok(sim.Execute(new Gather { SpotId = "herbs_007", ItemId = "medicinal_herbs" }));
                return sim.ItemCount("medicinal_herbs");
            }
            Assert.That(Yield(), Is.EqualTo(Yield()));
        }

        [Test]
        public void ANewGameCanStartWithSupplies()
        {
            var options = new NewGameOptions { Seed = 1 };
            options.StartingStores = new Dictionary<string, int> { { "jerky", 6 }, { "cartridges", 20 }, { "no_such_item", 3 }, { "revolver", 1 } };
            Simulation sim = Simulation.NewGame(TestSupport.Content, options);
            Assert.That(sim.ItemCount("jerky"), Is.EqualTo(6));
            Assert.That(sim.ItemCount("cartridges"), Is.EqualTo(20));
            Assert.That(sim.State.Household.Inventory.ContainsKey("no_such_item"), Is.False);
            Assert.That(sim.State.Household.Inventory.ContainsKey("revolver"), Is.False, "Gear is not a count in the stores.");
        }
    }
}

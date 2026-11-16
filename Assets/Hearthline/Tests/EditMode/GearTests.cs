using System.Collections.Generic;
using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class GearTests
    {
        private static GearItem Give(Simulation sim, string def, Character to)
        {
            TestSupport.Ok(sim.Execute(new GiveGear { DefId = def, CharacterId = to?.Id }));
            List<GearItem> list = to != null ? to.Gear : sim.State.Household.GearStore;
            return list[list.Count - 1];
        }

        private static Character Join(Simulation sim, string name, int age, HouseholdBond bond)
        {
            TestSupport.Ok(sim.Execute(new AddHouseholdMember { GivenName = name, Surname = "Pruitt", AgeYears = age, Bond = bond }));
            return sim.State.Characters[sim.State.Characters.Count - 1];
        }

        [Test]
        public void GearOnlyHelpsWhileItIsWorn()
        {
            Simulation sim = TestSupport.NewSim();
            GearItem rifle = Give(sim, "lever_rifle", sim.Head);
            Assert.That(sim.GetStat(sim.Head, "marksmanship"), Is.EqualTo(0.0));
            TestSupport.Ok(sim.Execute(new EquipGear { GearId = rifle.Id }));
            Assert.That(sim.GetStat(sim.Head, "marksmanship"), Is.EqualTo(1.0));
            Assert.That(sim.GetStat(sim.Head, "aim_steadiness"), Is.EqualTo(1.0), "no heirloom bonus for the first owner");

            GearView view = sim.GearOf(sim.Head)[0];
            Assert.That(view.Slot, Is.EqualTo(EquipSlot.LongGun));
            Assert.That(view.Visual, Is.EqualTo("rifle_lever"));
            Assert.That(view.Equipped, Is.True);
            Assert.That(view.Ledger[0].Text, Is.EqualTo("First carried by Ada Ward."));

            TestSupport.Ok(sim.Execute(new EquipGear { GearId = rifle.Id, Equipped = false }));
            Assert.That(sim.GetStat(sim.Head, "marksmanship"), Is.EqualTo(0.0));
            TestSupport.Rejected(sim.Execute(new EquipGear { GearId = rifle.Id, Equipped = false }), "not worn");
        }

        [Test]
        public void OneItemPerSlot()
        {
            Simulation sim = TestSupport.NewSim();
            GearItem first = Give(sim, "lever_rifle", sim.Head), second = Give(sim, "lever_rifle", sim.Head), hat = Give(sim, "brimmed_hat", sim.Head);
            TestSupport.Ok(sim.Execute(new EquipGear { GearId = first.Id }));
            TestSupport.Ok(sim.Execute(new EquipGear { GearId = hat.Id }));
            TestSupport.Ok(sim.Execute(new EquipGear { GearId = second.Id }));
            Assert.That(first.Equipped, Is.False, "the second rifle took the long gun slot");
            Assert.That(second.Equipped, Is.True);
            Assert.That(hat.Equipped, Is.True);
            Assert.That(sim.GetStat(sim.Head, "marksmanship"), Is.EqualTo(1.0));
        }

        [Test]
        public void GearIsUniqueNotACount()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Rejected(sim.Execute(new GiveItem { ItemId = "lever_rifle" }), "is gear");
            TestSupport.Rejected(sim.Execute(new GiveGear { DefId = "jerky" }), "no gear");
            TestSupport.Rejected(sim.Execute(new SellItems { ItemId = "lever_rifle" }), "nobody trades");
            GearItem inStores = Give(sim, "wool_coat", null);
            Assert.That(sim.GearInStores().Count, Is.EqualTo(1));
            TestSupport.Rejected(sim.Execute(new EquipGear { GearId = inStores.Id }), "taken up by someone first");
            TestSupport.Ok(sim.Execute(new TransferGear { GearId = inStores.Id, ToCharacterId = sim.Head.Id }));
            Assert.That(sim.GearInStores(), Is.Empty);
            Assert.That(inStores.Ledger[inStores.Ledger.Count - 1].Text, Is.EqualTo("Taken up by Ada Ward."));
        }

        [Test]
        public void ARifleBecomesAnHeirloomInTheHandsOfAGrandchild()
        {
            Simulation sim = TestSupport.NewSim();
            Character ada = sim.Head;
            GearItem rifle = Give(sim, "lever_rifle", ada);
            TestSupport.Ok(sim.Execute(new EquipGear { GearId = rifle.Id }));
            Character levi = Join(sim, "Levi", 30, HouseholdBond.Partner);
            TestSupport.Ok(sim.Execute(new BirthChild { ParentAId = ada.Id, ParentBId = levi.Id, GivenName = "Ruth" }));
            Character ruth = sim.State.Characters[sim.State.Characters.Count - 1];
            TestSupport.Ok(sim.Execute(new SetAge { CharacterId = ruth.Id, AgeYears = 25 }));
            Character wade = Join(sim, "Wade", 26, HouseholdBond.Partner);
            TestSupport.Ok(sim.Execute(new BirthChild { ParentAId = ruth.Id, ParentBId = wade.Id, GivenName = "Thea" }));
            Character thea = sim.State.Characters[sim.State.Characters.Count - 1];
            TestSupport.Ok(sim.Execute(new SetAge { CharacterId = thea.Id, AgeYears = 18 }));
            Character silas = Join(sim, "Silas", 30, HouseholdBond.Sworn);

            // The dead keep their gear until someone takes it up.
            TestSupport.Ok(sim.Execute(new KillCharacter { CharacterId = ada.Id, Cause = "old age" }));
            TestSupport.Ok(sim.Execute(new ChooseHeir { HeirId = ruth.Id }));
            Assert.That(ada.Gear, Does.Contain(rifle));

            // To someone outside the blood it is only a rifle.
            TestSupport.Ok(sim.Execute(new TransferGear { GearId = rifle.Id, ToCharacterId = silas.Id }));
            TestSupport.Ok(sim.Execute(new EquipGear { GearId = rifle.Id }));
            Assert.That(rifle.Heirloom, Is.False);
            Assert.That(sim.GetStat(silas, "aim_steadiness"), Is.EqualTo(1.0));
            Assert.That(sim.ChronicleOf(silas).Exists(e => e.Kind == ChronicleKind.Bequest && e.Text == "Silas took up Lever rifle, which was Ada's."), Is.True);

            // In a granddaughter's hands it is Grandmother's rifle.
            TestSupport.Ok(sim.Execute(new TransferGear { GearId = rifle.Id, ToCharacterId = thea.Id }));
            Assert.That(rifle.Equipped, Is.False, "changing hands takes it off");
            TestSupport.Ok(sim.Execute(new EquipGear { GearId = rifle.Id }));
            Assert.That(rifle.Heirloom, Is.True);
            Assert.That(sim.GetStat(thea, "aim_steadiness"), Is.EqualTo(1.08).Within(1e-9));
            Assert.That(sim.GearOf(thea)[0].Heirloom, Is.True);

            var history = rifle.Ledger.ConvertAll(e => e.Text);
            Assert.That(history, Is.EqualTo(new[]
            {
                "First carried by Ada Ward.",
                "Passed from Ada Ward to Silas Pruitt.",
                "Handed from Silas Pruitt to Thea Ward.",
            }));
        }

        [Test]
        public void GearWithEnoughHistoryCanBeNamedAtAWorkshop()
        {
            Simulation sim = TestSupport.NewSim();
            GearItem rifle = Give(sim, "lever_rifle", sim.Head);
            TestSupport.Rejected(sim.Execute(new NameGear { GearId = rifle.Id, Name = "Patience" }), "not seen enough");
            TestSupport.Ok(sim.Execute(new NoteOnGear { GearId = rifle.Id, Text = "Dropped the boar in the corn." }));
            TestSupport.Ok(sim.Execute(new NoteOnGear { GearId = rifle.Id, Text = "Took a bullet meant for Ada at Miller's Ford." }));
            TestSupport.Rejected(sim.Execute(new NameGear { GearId = rifle.Id, Name = "Patience" }), "needs a workshop");

            sim.State.Household.Structures["workshop"] = 1; // test setup only
            TestSupport.Ok(sim.Execute(new NameGear { GearId = rifle.Id, Name = "Patience" }));
            Assert.That(sim.GearOf(sim.Head)[0].Name, Is.EqualTo("Patience"));
            Assert.That(rifle.Ledger[rifle.Ledger.Count - 1].Text, Is.EqualTo("Named \"Patience\"."));
        }

        [Test]
        public void AnEventCanPutARifleInYourHandsWithItsStory()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new StartEvent { EventId = "rifle_in_the_wreck", Context = new EventContext { PlaceName = "the old interstate" } }));
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }));
            GearItem rifle = sim.Head.Gear[0];
            Assert.That(rifle.DefId, Is.EqualTo("lever_rifle"));
            Assert.That(rifle.Ledger.ConvertAll(e => e.Text), Is.EqualTo(new[] { "First carried by Ada Ward.", "Found in a wrecked truck by Ada Ward." }));
        }

        [Test]
        public void GearSurvivesSaveAndLoad()
        {
            Simulation sim = TestSupport.NewSim();
            GearItem rifle = Give(sim, "lever_rifle", sim.Head);
            Give(sim, "wool_coat", null);
            TestSupport.Ok(sim.Execute(new EquipGear { GearId = rifle.Id }));
            string json = sim.ToJson();
            Simulation loaded = Simulation.FromJson(json, TestSupport.Content);
            Assert.That(loaded.ToJson(), Is.EqualTo(json));
            Assert.That(loaded.GearOf(loaded.Head)[0].Equipped, Is.True);
            Assert.That(loaded.GetStat(loaded.Head, "marksmanship"), Is.EqualTo(1.0));
            Assert.That(loaded.GearInStores()[0].DefId, Is.EqualTo("wool_coat"));
        }
    }
}

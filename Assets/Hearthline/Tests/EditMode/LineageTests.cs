using System.Collections.Generic;
using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class HealthTests
    {
        private static readonly MarkOrigin Reaver = new MarkOrigin { Cause = "a Reaver" };

        private static void Hit(Simulation sim, int severity, BodyRegion region = BodyRegion.HandL)
        {
            TestSupport.Ok(sim.Execute(new InflictHit { CharacterId = sim.Head.Id, Region = region, Damage = DamageType.Gunshot, Severity = severity, Origin = Reaver }));
        }

        [Test]
        public void ThreeLightHitsOrOneHeavyHitPutYouDown()
        {
            Simulation sim = TestSupport.NewSim();
            Hit(sim, 1); Hit(sim, 1);
            Assert.That(sim.Head.Downed, Is.False);
            Assert.That(sim.Head.Health, Is.EqualTo(1));
            Hit(sim, 1);
            Assert.That(sim.Head.Downed, Is.True);
            Assert.That(sim.Head.Alive, Is.True, "downed is not dead");
            TestSupport.Rejected(sim.Execute(new InflictHit { CharacterId = sim.Head.Id, Region = BodyRegion.Torso, Damage = DamageType.Blade, Origin = Reaver }), "already down");

            Simulation other = TestSupport.NewSim();
            CommandResult result = other.Execute(new InflictHit { CharacterId = other.Head.Id, Region = BodyRegion.Torso, Damage = DamageType.Gunshot, Severity = 3, Origin = Reaver });
            Assert.That(other.Head.Downed, Is.True);
            Assert.That(result.Events[result.Events.Count - 1], Is.InstanceOf<CharacterDowned>());
        }

        [Test]
        public void ACompanionCanReviveTheDowned()
        {
            Simulation sim = TestSupport.NewSim();
            Hit(sim, 3);
            TestSupport.Ok(sim.Execute(new ReviveCharacter { CharacterId = sim.Head.Id }));
            Assert.That(sim.Head.Downed, Is.False);
            Assert.That(sim.Head.Health, Is.EqualTo(1));
            TestSupport.Rejected(sim.Execute(new ReviveCharacter { CharacterId = sim.Head.Id }), "not down");
        }

        [Test]
        public void LeftDownedYouDieOrLoseSomethingForGood()
        {
            int died = 0, maimed = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                Simulation sim = TestSupport.NewSim(seed);
                Character head = sim.Head;
                Hit(sim, 3);
                TestSupport.Ok(sim.Execute(new LeaveDowned { CharacterId = head.Id }));
                Assert.That(head.Downed, Is.False);
                if (!head.Alive)
                {
                    died++;
                    Assert.That(head.DeathCause, Is.EqualTo("shot by a Reaver, spring of year 1"));
                }
                else
                {
                    maimed++;
                    Assert.That(head.Marks[0].DefId, Is.EqualTo("loss_fingers"));
                    Assert.That(head.Health, Is.EqualTo(1));
                }
            }
            Assert.That(died, Is.GreaterThan(10));
            Assert.That(maimed, Is.GreaterThan(10));
        }

        [Test]
        public void ADayPassingResolvesTheDowned()
        {
            Simulation sim = TestSupport.NewSim();
            Character head = sim.Head;
            Hit(sim, 3);
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 1 }));
            Assert.That(head.Downed, Is.False);
        }

        [Test]
        public void HealthReturnsOnceWoundsAreNoLongerOpen()
        {
            Simulation sim = TestSupport.NewSim();
            Hit(sim, 2);
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 1 }));
            Assert.That(sim.Head.Health, Is.EqualTo(1), "a fresh wound does not let you recover");
            TestSupport.Ok(sim.Execute(new FieldTreatWound { CharacterId = sim.Head.Id, MarkId = sim.Head.Marks[0].Id }));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 2 }));
            Assert.That(sim.Head.Health, Is.EqualTo(3));
        }
    }

    public class LineageTests
    {
        private static Character Join(Simulation sim, string name, int age, HouseholdBond bond, params string[] traits)
        {
            TestSupport.Ok(sim.Execute(new AddHouseholdMember { GivenName = name, Surname = "Pruitt", AgeYears = age, Bond = bond, Traits = new List<string>(traits) }));
            return sim.State.Characters[sim.State.Characters.Count - 1];
        }

        private static Character Child(Simulation sim, Character a, Character b, string name = null)
        {
            TestSupport.Ok(sim.Execute(new BirthChild { ParentAId = a.Id, ParentBId = b.Id, GivenName = name }));
            return sim.State.Characters[sim.State.Characters.Count - 1];
        }

        [Test]
        public void TraitsChangeStats()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new SetTrait { CharacterId = sim.Head.Id, TraitId = "keen_eyed" }));
            Assert.That(sim.GetStat(sim.Head, "tracking"), Is.EqualTo(1.0));
            TestSupport.Rejected(sim.Execute(new SetTrait { CharacterId = sim.Head.Id, TraitId = "keen_eyed" }), "already has");
            TestSupport.Rejected(sim.Execute(new SetTrait { CharacterId = sim.Head.Id, TraitId = "nope" }), "no trait");
            TestSupport.Ok(sim.Execute(new SetTrait { CharacterId = sim.Head.Id, TraitId = "keen_eyed", Has = false }));
            Assert.That(sim.GetStat(sim.Head, "tracking"), Is.EqualTo(0.0));
        }

        [Test]
        public void AChildTakesTheHouseholdNameAndKnowsItsParents()
        {
            Simulation sim = TestSupport.NewSim();
            Character partner = Join(sim, "Levi", 30, HouseholdBond.Partner);
            TestSupport.Ok(sim.Execute(new SetPartners { CharacterAId = sim.Head.Id, CharacterBId = partner.Id }));
            Character child = Child(sim, sim.Head, partner, "Ruth");

            Assert.That(child.GivenName, Is.EqualTo("Ruth"));
            Assert.That(child.Surname, Is.EqualTo("Ward"));
            Assert.That(child.Bond, Is.EqualTo(HouseholdBond.Blood));
            Assert.That(child.ParentIds, Is.EquivalentTo(new[] { sim.Head.Id, partner.Id }));
            Assert.That(sim.AgeOf(child), Is.EqualTo(0));
            Assert.That(sim.LifeStageOf(child), Is.EqualTo(LifeStage.Child));
            Assert.That(sim.State.Household.MemberIds, Does.Contain(child.Id));
            Assert.That(sim.ChronicleOf(child)[0].Text, Is.EqualTo("Ruth Ward was born to Ada and Levi."));
        }

        [Test]
        public void UnnamedChildrenGetAnUnusedName()
        {
            Simulation sim = TestSupport.NewSim();
            Character partner = Join(sim, "Levi", 30, HouseholdBond.Partner);
            var names = new HashSet<string> { "Ada", "Levi" };
            for (int i = 0; i < 8; i++)
            {
                string name = Child(sim, sim.Head, partner).GivenName;
                Assert.That(TestSupport.Content.GivenNames, Does.Contain(name));
                Assert.That(names.Add(name), Is.True, name + " was used twice");
            }
        }

        [Test]
        public void ChildrenInheritTraitsSometimesAndMutateRarely()
        {
            int inherited = 0, mutated = 0, total = 200;
            for (ulong seed = 1; seed <= (ulong)total; seed++)
            {
                Simulation sim = TestSupport.NewSim(seed);
                TestSupport.Ok(sim.Execute(new SetTrait { CharacterId = sim.Head.Id, TraitId = "sturdy" }));
                Character partner = Join(sim, "Levi", 30, HouseholdBond.Partner);
                Character child = Child(sim, sim.Head, partner);
                if (child.Traits.Contains("sturdy")) inherited++;
                foreach (string t in child.Traits) if (t != "sturdy") { mutated++; Assert.That(TestSupport.Content.FindTrait(t).Innate, Is.True); }
            }
            Assert.That(inherited / (double)total, Is.InRange(0.3, 0.6), "sturdy has inherit_chance 0.45");
            Assert.That(mutated / (double)total, Is.InRange(0.05, 0.25));
        }

        [Test]
        public void TheKnackCanSurfaceSkipAGenerationOrDieOut()
        {
            int manifest = 0, carried = 0, grandchildManifest = 0, total = 300;
            for (ulong seed = 1; seed <= (ulong)total; seed++)
            {
                Simulation sim = TestSupport.NewSim(seed);
                TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "knack_dowsing", Region = BodyRegion.Head, Origin = new MarkOrigin { Cause = "a night in the hollow" } }));
                Character partner = Join(sim, "Levi", 30, HouseholdBond.Partner);
                Character child = Child(sim, sim.Head, partner, "Ruth");
                if (child.Marks.Count == 1)
                {
                    manifest++;
                    Assert.That(child.Marks[0].DefId, Is.EqualTo("knack_dowsing"));
                    Assert.That(child.Marks[0].Stage, Is.EqualTo("latent"));
                    Assert.That(child.Marks[0].OriginText, Is.EqualTo("Born with the Knack Ada carried, spring of year 1"));
                }
                else if (child.CarriedLatent.Contains("knack_dowsing"))
                {
                    carried++;
                    // The carrier shows nothing, but can pass it on.
                    TestSupport.Ok(sim.Execute(new SetAge { CharacterId = child.Id, AgeYears = 25 }));
                    Character spouse = Join(sim, "Wade", 25, HouseholdBond.Partner);
                    if (Child(sim, child, spouse).Marks.Count == 1) grandchildManifest++;
                }
            }
            Assert.That(manifest / (double)total, Is.InRange(0.25, 0.45));
            Assert.That(carried, Is.GreaterThan(50));
            Assert.That(grandchildManifest, Is.GreaterThan(10), "the Knack should sometimes skip a generation");
        }

        [Test]
        public void ExposureCanEchoInAChildButWoundsAndScarsDoNot()
        {
            int echoes = 0, total = 200;
            for (ulong seed = 1; seed <= (ulong)total; seed++)
            {
                Simulation sim = TestSupport.NewSim(seed);
                TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "exposure_pale_eyes", Region = BodyRegion.Face, Origin = new MarkOrigin { Cause = "the light in the trees" } }));
                TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Origin = TestSupport.Boar }));
                Character child = Child(sim, sim.Head, Join(sim, "Levi", 30, HouseholdBond.Partner));
                foreach (Mark m in child.Marks) Assert.That(m.DefId, Is.EqualTo("exposure_pale_eyes"));
                echoes += child.Marks.Count;
                Assert.That(child.CarriedLatent, Is.Empty, "echoes are not carried silently");
            }
            Assert.That(echoes / (double)total, Is.InRange(0.15, 0.35));
        }

        [Test]
        public void ParentsMustBeLivingAdultHouseholdMembers()
        {
            Simulation sim = TestSupport.NewSim();
            Character youth = Join(sim, "Eli", 15, HouseholdBond.Ward);
            Character elder = Join(sim, "Opal", 60, HouseholdBond.Sworn);
            TestSupport.Rejected(sim.Execute(new BirthChild { ParentAId = sim.Head.Id, ParentBId = youth.Id }), "not of an age");
            TestSupport.Rejected(sim.Execute(new BirthChild { ParentAId = sim.Head.Id, ParentBId = elder.Id }), "not of an age");
            TestSupport.Rejected(sim.Execute(new BirthChild { ParentAId = sim.Head.Id, ParentBId = sim.Head.Id }), "two parents");
            TestSupport.Rejected(sim.Execute(new SetPartners { CharacterAId = sim.Head.Id, CharacterBId = youth.Id }), "not an adult");
            TestSupport.Rejected(sim.Execute(new AddHouseholdMember { GivenName = "X", Bond = HouseholdBond.Blood }), "blood is born");
        }

        [Test]
        public void SeasonsAtHomeBringChildrenToPartners()
        {
            Simulation sim = TestSupport.NewSim(3);
            Character partner = Join(sim, "Levi", 30, HouseholdBond.Partner);
            TestSupport.Ok(sim.Execute(new SetPartners { CharacterAId = sim.Head.Id, CharacterBId = partner.Id }));
            int before = sim.State.Household.MemberIds.Count;
            for (int i = 0; i < 40; i++) TestSupport.Ok(sim.Execute(new PassSeason()));
            int children = sim.State.Household.MemberIds.Count - before;
            Assert.That(children, Is.InRange(2, 12), "0.15 per season over 40 seasons");

            Simulation alone = TestSupport.NewSim(3);
            for (int i = 0; i < 40; i++) alone.Execute(new PassSeason());
            Assert.That(alone.State.Household.MemberIds.Count, Is.EqualTo(1), "no partner, no children");
        }

        [Test]
        public void AgeLeavesItsMarksAndEventuallyKills()
        {
            Simulation sim = TestSupport.NewSim();
            Character head = sim.Head;
            TestSupport.Ok(sim.Execute(new SetAge { CharacterId = head.Id, AgeYears = 49 }));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = GameDate.DaysPerYear }));
            Assert.That(head.Marks.Count, Is.EqualTo(1));
            Assert.That(head.Marks[0].DefId, Is.EqualTo("age_grey"));
            Assert.That(head.Marks[0].OriginText, Is.EqualTo("Went grey in the spring of year 2"));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 5 }));
            Assert.That(head.Marks.Count, Is.EqualTo(1), "an age mark is gained once");

            TestSupport.Ok(sim.Execute(new SetAge { CharacterId = head.Id, AgeYears = 66 }));
            for (int i = 0; i < 400 && head.Alive; i++) sim.Execute(new PassSeason());
            Assert.That(head.Alive, Is.False);
            Assert.That(head.DeathCause, Is.EqualTo("old age"));
            Assert.That(sim.AgeOf(head), Is.GreaterThanOrEqualTo(66));
        }

        [Test]
        public void YoungAdultsDoNotDieOfOldAge()
        {
            Simulation sim = TestSupport.NewSim();
            for (int i = 0; i < 4 * 30; i++) sim.Execute(new PassSeason());
            Assert.That(sim.Head.Alive, Is.True);
            Assert.That(sim.AgeOf(sim.Head), Is.EqualTo(58));
        }
    }

    public class SuccessionTests
    {
        private static Character Join(Simulation sim, string name, int age, HouseholdBond bond)
        {
            TestSupport.Ok(sim.Execute(new AddHouseholdMember { GivenName = name, Surname = "Pruitt", AgeYears = age, Bond = bond }));
            return sim.State.Characters[sim.State.Characters.Count - 1];
        }

        private static Character GrownChild(Simulation sim, Character a, Character b, string name, int age)
        {
            TestSupport.Ok(sim.Execute(new BirthChild { ParentAId = a.Id, ParentBId = b.Id, GivenName = name }));
            Character child = sim.State.Characters[sim.State.Characters.Count - 1];
            TestSupport.Ok(sim.Execute(new SetAge { CharacterId = child.Id, AgeYears = age }));
            return child;
        }

        [Test]
        public void HeirsAreBloodThenWardsThenSwornAndOnlyAdults()
        {
            Simulation sim = TestSupport.NewSim();
            Character sworn = Join(sim, "Silas", 40, HouseholdBond.Sworn);
            Character ward = Join(sim, "Eli", 20, HouseholdBond.Ward);
            Character partner = Join(sim, "Levi", 30, HouseholdBond.Partner);
            Character daughter = GrownChild(sim, sim.Head, partner, "Ruth", 19);
            GrownChild(sim, sim.Head, partner, "Micah", 12);

            var heirs = sim.EligibleHeirs();
            Assert.That(heirs.ConvertAll(c => c.GivenName), Is.EqualTo(new[] { "Ruth", "Eli", "Silas" }));
            Assert.That(heirs, Does.Not.Contain(sim.Head));
            Assert.That(heirs, Does.Not.Contain(partner));
            Assert.That(heirs[0], Is.SameAs(daughter));
        }

        [Test]
        public void WhenTheHeadDiesAnHeirIsChosenAndTheWorldRemembers()
        {
            Simulation sim = TestSupport.NewSim();
            Character ada = sim.Head;
            Character partner = Join(sim, "Levi", 30, HouseholdBond.Partner);
            TestSupport.Ok(sim.Execute(new SetPartners { CharacterAId = ada.Id, CharacterBId = partner.Id }));
            Character ruth = GrownChild(sim, ada, partner, "Ruth", 20);
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = ada.Id, DefId = "scar_claw", Region = BodyRegion.Face, Origin = TestSupport.Boar }));

            TestSupport.Rejected(sim.Execute(new ChooseHeir { HeirId = ruth.Id }), "already has a head");
            TestSupport.Ok(sim.Execute(new KillCharacter { CharacterId = ada.Id, Cause = "shot by a Reaver at the river crossing" }));
            Assert.That(sim.Head, Is.Null);
            Assert.That(sim.State.Household.LineEnded, Is.False);
            Assert.That(partner.PartnerId, Is.Null, "the widowed are free to partner again");
            TestSupport.Rejected(sim.Execute(new ChooseHeir { HeirId = partner.Id }), "cannot inherit");

            CommandResult result = sim.Execute(new ChooseHeir { HeirId = ruth.Id });
            TestSupport.Ok(result);
            Assert.That(sim.Head, Is.SameAs(ruth));
            Assert.That(((HeadSucceeded)result.Events[0]).PreviousHeadId, Is.EqualTo(ada.Id));
            Assert.That(sim.State.Household.PastHeadIds, Is.EqualTo(new[] { ada.Id, ruth.Id }));

            // The dead keep their marks and their story.
            Assert.That(ada.Marks.Count, Is.EqualTo(1));
            var chronicle = sim.ChronicleOf(ada).ConvertAll(e => e.Text);
            Assert.That(chronicle, Does.Contain("Scar from the boar at Miller's Ford, spring of year 1."));
            Assert.That(chronicle, Does.Contain("Ada Ward died at 28: shot by a Reaver at the river crossing."));
        }

        [Test]
        public void WithNoHeirTheLineEnds()
        {
            Simulation sim = TestSupport.NewSim();
            Character partner = Join(sim, "Levi", 30, HouseholdBond.Partner);
            CommandResult result = sim.Execute(new KillCharacter { CharacterId = sim.State.Household.HeadId.Value, Cause = "fever" });
            Assert.That(sim.State.Household.LineEnded, Is.True);
            Assert.That(result.Events[result.Events.Count - 1], Is.InstanceOf<LineEnded>());
            TestSupport.Rejected(sim.Execute(new ChooseHeir { HeirId = partner.Id }), "line has ended");
        }

        [Test]
        public void AnElderHeadCanRetireAndStaysHome()
        {
            Simulation sim = TestSupport.NewSim();
            Character ada = sim.Head;
            Character ward = Join(sim, "Eli", 22, HouseholdBond.Ward);
            TestSupport.Rejected(sim.Execute(new RetireHead { HeirId = ward.Id }), "too young to retire");
            TestSupport.Ok(sim.Execute(new SetAge { CharacterId = ada.Id, AgeYears = 47 }));
            TestSupport.Ok(sim.Execute(new RetireHead { HeirId = ward.Id }));

            Assert.That(sim.Head, Is.SameAs(ward));
            Assert.That(ada.Alive, Is.True);
            Assert.That(ada.Retired, Is.True);
            Assert.That(sim.State.Household.MemberIds, Does.Contain(ada.Id));
            Assert.That(sim.EligibleHeirs(), Does.Not.Contain(ada), "the retired do not take the household back");
        }

        [Test]
        public void ThreeGenerationsSurviveSaveAndLoad()
        {
            Simulation sim = TestSupport.NewSim(9);
            Character ada = sim.Head;
            Character levi = Join(sim, "Levi", 30, HouseholdBond.Partner);
            Character ruth = GrownChild(sim, ada, levi, "Ruth", 25);
            Character wade = Join(sim, "Wade", 26, HouseholdBond.Partner);
            TestSupport.Ok(sim.Execute(new SetPartners { CharacterAId = ruth.Id, CharacterBId = wade.Id }));
            GrownChild(sim, ruth, wade, "Thea", 3);
            TestSupport.Ok(sim.Execute(new KillCharacter { CharacterId = ada.Id, Cause = "fever" }));
            TestSupport.Ok(sim.Execute(new ChooseHeir { HeirId = ruth.Id }));

            string json = sim.ToJson();
            Simulation loaded = Simulation.FromJson(json, TestSupport.Content);
            Assert.That(loaded.ToJson(), Is.EqualTo(json));
            Assert.That(loaded.Head.GivenName, Is.EqualTo("Ruth"));
            Assert.That(loaded.State.Chronicle.Count, Is.EqualTo(sim.State.Chronicle.Count));
            foreach (Simulation s in new[] { sim, loaded }) for (int i = 0; i < 12; i++) s.Execute(new PassSeason());
            Assert.That(loaded.ToJson(), Is.EqualTo(sim.ToJson()));
        }
    }
}

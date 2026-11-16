using Hearthline.Sim;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class WoundTests
    {
        private static readonly MarkOrigin Reaver = new MarkOrigin { Cause = "a Reaver", Place = "the river crossing" };

        private static Mark Hit(Simulation sim, BodyRegion region, DamageType damage = DamageType.Gunshot, int severity = 2)
        {
            TestSupport.Ok(sim.Execute(new InflictHit { CharacterId = sim.Head.Id, Region = region, Damage = damage, Severity = severity, Origin = Reaver }));
            return sim.Head.Marks[sim.Head.Marks.Count - 1];
        }

        [Test]
        public void AHitBecomesAFreshWoundOnThatRegion()
        {
            Simulation sim = TestSupport.NewSim();
            Mark wound = Hit(sim, BodyRegion.ArmL);
            Assert.That(wound.DefId, Is.EqualTo("wound_gunshot"));
            Assert.That(wound.Category, Is.EqualTo(MarkCategory.Wound));
            Assert.That(wound.Region, Is.EqualTo(BodyRegion.ArmL));
            Assert.That(wound.Stage, Is.EqualTo(WoundStages.Fresh));
            Assert.That(wound.OriginText, Is.EqualTo("Shot by a Reaver at the river crossing, spring of year 1"));
            Assert.That(sim.GetStat(sim.Head, "stamina"), Is.LessThan(1.0));
            Assert.That(sim.ReactionTagsOf(sim.Head), Does.Contain("bleeding"));
        }

        [TestCase(DamageType.Gunshot, "wound_gunshot")]
        [TestCase(DamageType.Blade, "wound_blade")]
        [TestCase(DamageType.Claw, "wound_claw")]
        [TestCase(DamageType.Burn, "wound_burn")]
        [TestCase(DamageType.Blunt, "wound_blunt")]
        public void EachDamageTypeHasItsWound(DamageType damage, string expectedDef)
        {
            Simulation sim = TestSupport.NewSim();
            Assert.That(Hit(sim, BodyRegion.Torso, damage).DefId, Is.EqualTo(expectedDef));
        }

        [Test]
        public void AnUntreatedWoundTurnsInfectedThenLeavesALossOrBadScar()
        {
            bool sawLoss = false, sawScar = false;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Simulation sim = TestSupport.NewSim(seed);
                Mark wound = Hit(sim, BodyRegion.HandR);
                int id = wound.Id, dayGained = wound.DayGained;

                TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 2 }));
                Assert.That(wound.Stage, Is.EqualTo(WoundStages.Infected));
                Assert.That(sim.ReactionTagsOf(sim.Head), Does.Contain("feverish"));

                TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 6 }));
                Assert.That(wound.Id, Is.EqualTo(id), "the mark keeps its identity");
                Assert.That(wound.DayGained, Is.EqualTo(dayGained), "the mark keeps the date of the event that caused it");
                Assert.That(wound.Origin.Cause, Is.EqualTo("a Reaver"));
                if (wound.DefId == "loss_fingers")
                {
                    sawLoss = true;
                    Assert.That(wound.Category, Is.EqualTo(MarkCategory.Loss));
                    Assert.That(wound.OriginText, Is.EqualTo("Fingers lost to a Reaver at the river crossing, spring of year 1"));
                }
                else
                {
                    sawScar = true;
                    Assert.That(wound.DefId, Is.EqualTo("scar_ragged"));
                    Assert.That(wound.Category, Is.EqualTo(MarkCategory.Scar));
                }
            }
            Assert.That(sawLoss && sawScar, Is.True, "both endings should occur across seeds");
        }

        [Test]
        public void ARegionWithNothingToLoseFallsBackToABadScar()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Simulation sim = TestSupport.NewSim(seed);
                Mark wound = Hit(sim, BodyRegion.Torso, severity: 3);
                TestSupport.Ok(sim.Execute(new ReviveCharacter { CharacterId = sim.Head.Id }));
                TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 30 }));
                Assert.That(wound.DefId, Is.EqualTo("scar_ragged"));
            }
        }

        [Test]
        public void FieldTreatmentStabilizesAndBuysTime()
        {
            Simulation sim = TestSupport.NewSim();
            Mark wound = Hit(sim, BodyRegion.LegL);
            TestSupport.Ok(sim.Execute(new FieldTreatWound { CharacterId = sim.Head.Id, MarkId = wound.Id }));
            Assert.That(wound.Stage, Is.EqualTo(WoundStages.Stabilized));

            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 7 }));
            Assert.That(wound.Stage, Is.EqualTo(WoundStages.Stabilized), "a fresh wound would be infected by now");
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 1 }));
            Assert.That(wound.Stage, Is.EqualTo(WoundStages.Infected), "stabilizing delays infection; it does not prevent it");

            TestSupport.Rejected(sim.Execute(new FieldTreatWound { CharacterId = sim.Head.Id, MarkId = wound.Id }), "only a fresh wound");
        }

        [Test]
        public void InfirmaryTreatmentDecidesTheOutcomeAndHealingDeliversIt()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Simulation sim = TestSupport.NewSim(seed);
                Mark wound = Hit(sim, BodyRegion.Face);
                CommandResult result = sim.Execute(new InfirmaryTreatWound { CharacterId = sim.Head.Id, MarkId = wound.Id, InfirmaryTier = 2, HealerSkill = 3 });
                TestSupport.Ok(result);

                string outcome = null;
                foreach (DomainEvent e in result.Events) if (e is WoundTreated t) outcome = t.Outcome;
                Assert.That(outcome, Is.Not.Null);

                if (outcome == WoundOutcomes.Infection)
                {
                    Assert.That(wound.Stage, Is.EqualTo(WoundStages.Infected));
                    continue;
                }
                Assert.That(wound.Stage, Is.EqualTo(WoundStages.Healing));
                TestSupport.Rejected(sim.Execute(new InfirmaryTreatWound { CharacterId = sim.Head.Id, MarkId = wound.Id, InfirmaryTier = 2 }), "already been treated");

                TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 15 }));
                Assert.That(wound.Stage, Is.EqualTo(WoundStages.Healing), "a gunshot wound heals for 16 days");
                TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 1 }));
                string expected = outcome == WoundOutcomes.CleanScar ? "scar_gunshot" : outcome == WoundOutcomes.BadScar ? "scar_ragged" : "loss_eye";
                Assert.That(wound.DefId, Is.EqualTo(expected));
                Assert.That(wound.PendingOutcome, Is.Null);
                Assert.That(wound.OriginText, Does.Contain("a Reaver at the river crossing, spring of year 1"));
            }
        }

        [Test]
        public void ABetterInfirmaryGivesMoreCleanScars()
        {
            int CleanCount(int tier, int skill)
            {
                int clean = 0;
                for (ulong seed = 1; seed <= 400; seed++)
                {
                    Simulation sim = TestSupport.NewSim(seed);
                    Mark wound = Hit(sim, BodyRegion.ArmR);
                    CommandResult r = sim.Execute(new InfirmaryTreatWound { CharacterId = sim.Head.Id, MarkId = wound.Id, InfirmaryTier = tier, HealerSkill = skill });
                    foreach (DomainEvent e in r.Events) if (e is WoundTreated t && t.Outcome == WoundOutcomes.CleanScar) clean++;
                }
                return clean;
            }

            int cot = CleanCount(1, 0), surgery = CleanCount(3, 0), skilledSurgery = CleanCount(3, 10);
            Assert.That(surgery, Is.GreaterThan(cot + 40));
            Assert.That(skilledSurgery, Is.GreaterThan(surgery + 20));
        }

        [Test]
        public void TreatmentRejectsBadInput()
        {
            Simulation sim = TestSupport.NewSim();
            Mark wound = Hit(sim, BodyRegion.ArmR);
            TestSupport.Ok(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Origin = TestSupport.Boar }));
            int scarId = sim.Head.Marks[1].Id;

            TestSupport.Rejected(sim.Execute(new InfirmaryTreatWound { CharacterId = sim.Head.Id, MarkId = wound.Id, InfirmaryTier = 0 }), "tier");
            TestSupport.Rejected(sim.Execute(new InfirmaryTreatWound { CharacterId = sim.Head.Id, MarkId = wound.Id, InfirmaryTier = 1, HealerSkill = 11 }), "skill");
            TestSupport.Rejected(sim.Execute(new InfirmaryTreatWound { CharacterId = sim.Head.Id, MarkId = scarId, InfirmaryTier = 1 }), "only wounds");
            TestSupport.Rejected(sim.Execute(new FieldTreatWound { CharacterId = sim.Head.Id, MarkId = scarId }), "only wounds");
        }

        [Test]
        public void SameSeedAndCommandsGiveTheSameStory()
        {
            string Run(ulong seed)
            {
                Simulation sim = TestSupport.NewSim(seed);
                Mark a = Hit(sim, BodyRegion.HandL), b = Hit(sim, BodyRegion.Face, DamageType.Claw, 3);
                sim.Execute(new InfirmaryTreatWound { CharacterId = sim.Head.Id, MarkId = a.Id, InfirmaryTier = 1 });
                sim.Execute(new AdvanceDays { Days = 60 });
                return sim.ToJson() + b.DefId;
            }
            Assert.That(Run(11), Is.EqualTo(Run(11)));
        }

        [Test]
        public void TheDeadDoNotHeal()
        {
            Simulation sim = TestSupport.NewSim();
            Character head = sim.Head;
            Mark wound = Hit(sim, BodyRegion.Torso);
            TestSupport.Ok(sim.Execute(new KillCharacter { CharacterId = head.Id, Cause = "shot by a Reaver" }));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 60 }));
            Assert.That(wound.Stage, Is.EqualTo(WoundStages.Fresh));
        }
    }
}

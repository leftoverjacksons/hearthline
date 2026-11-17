using System.Collections.Generic;
using System.Linq;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using NUnit.Framework;

namespace Hearthline.Tests
{
    /// <summary>Ranged combat in the simulation (docs/specs/combat-and-ambush.md, acceptance tests 1-8).</summary>
    public class CombatTests
    {
        private static Simulation SimWithRifle(out Character head, out GearItem rifle, ulong seed = 1, int cartridges = 20)
        {
            Simulation sim = TestSupport.NewSim(seed);
            head = sim.Head;
            TestSupport.Ok(sim.Execute(new GiveGear { DefId = "lever_rifle", CharacterId = head.Id }));
            rifle = head.Gear.Single(g => g.DefId == "lever_rifle");
            TestSupport.Ok(sim.Execute(new EquipGear { GearId = rifle.Id }));
            if (cartridges > 0) TestSupport.Ok(sim.Execute(new GiveItem { ItemId = "cartridges", Count = cartridges }));
            return sim;
        }

        private static Simulation InFight(out Character head, out GearItem rifle, ulong seed = 1, string encounter = "reavers_road")
        {
            Simulation sim = SimWithRifle(out head, out rifle, seed);
            TestSupport.Ok(sim.Execute(new StartEncounter { EncounterId = encounter, PlaceName = "Miller's Ford" }));
            return sim;
        }

        private static List<Character> Enemies(Simulation sim) =>
            sim.Encounter.Combatants.Select(x => sim.FindCharacter(x.CharacterId)).ToList();

        private static void Down(Simulation sim, Character head, GearItem rifle, Character enemy) =>
            TestSupport.Ok(sim.Execute(new ShotHit { TargetId = enemy.Id, Region = BodyRegion.Head, ShooterId = head.Id, GearId = rifle.Id, Distance = 20 }));

        // ---- 1. Starting ---------------------------------------------------------------------------

        [Test]
        public void StartingAFightGeneratesArmedEnemiesWithinTheDefinedRanges()
        {
            Simulation sim = InFight(out _, out _);
            EncounterDef def = sim.Content.Encounters["reavers_road"];
            int count = sim.Encounter.Combatants.Count;
            Assert.That(count, Is.InRange(def.Enemies.Sum(e => e.CountMin), def.Enemies.Sum(e => e.CountMax)));
            Assert.That(sim.Encounter.Combatants.Count(x => x.Leader), Is.EqualTo(1));
            foreach (Combatant x in sim.Encounter.Combatants)
            {
                Character c = sim.FindCharacter(x.CharacterId);
                Assert.That(c.Archetype, Is.EqualTo("reaver"));
                GearItem gun = c.Gear.Single(g => g.Id == x.GearId);
                WeaponDef weapon = sim.WeaponOf(gun.DefId);
                Assert.That(weapon, Is.Not.Null);
                Assert.That(gun.Equipped, Is.True);
                Assert.That(gun.Loaded, Is.EqualTo(weapon.Capacity));
                Assert.That(gun.Condition, Is.InRange(0.4, 0.9));
            }
        }

        [Test]
        public void TheSameSeedGivesTheSameFight()
        {
            string a = InFight(out _, out _, seed: 9).ToJson();
            string b = InFight(out _, out _, seed: 9).ToJson();
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void OnlyOneFightAtATimeAndUnstagedOnesAreRefused()
        {
            Simulation sim = InFight(out _, out _);
            TestSupport.Rejected(sim.Execute(new StartEncounter { EncounterId = "reavers_small" }), "already");
            Simulation other = TestSupport.NewSim();
            TestSupport.Rejected(other.Execute(new StartEncounter { EncounterId = "wolves_pack" }), "not staged");
            TestSupport.Rejected(other.Execute(new StartEncounter { EncounterId = "nope" }), "no encounter");
        }

        [Test]
        public void AReaverTheEventCastLeadsTheFight()
        {
            Simulation sim = SimWithRifle(out _, out _);
            var toll = new Character { Id = sim.State.NextCharacterId++, GivenName = "Silas", Surname = "Crane", Archetype = "reaver" };
            sim.State.Characters.Add(toll);
            TestSupport.Ok(sim.Execute(new StartEncounter { EncounterId = "reavers_road", LeaderId = toll.Id }));
            Combatant leader = sim.Encounter.Combatants.Single(x => x.Leader);
            Assert.That(leader.CharacterId, Is.EqualTo(toll.Id));
            Assert.That(toll.Gear.Any(g => g.Id == leader.GearId), Is.True, "The leader is handed their gun.");
        }

        // ---- 2. Firing and reloading ---------------------------------------------------------------

        [Test]
        public void FiringUsesARoundAndAnEmptyGunRefuses()
        {
            Simulation sim = SimWithRifle(out Character head, out GearItem rifle);
            TestSupport.Rejected(sim.Execute(new FireWeapon { CharacterId = head.Id, GearId = rifle.Id }), "empty");
            TestSupport.Ok(sim.Execute(new ReloadWeapon { CharacterId = head.Id, GearId = rifle.Id }));
            Assert.That(rifle.Loaded, Is.EqualTo(7));
            Assert.That(sim.ItemCount("cartridges"), Is.EqualTo(13), "Rounds come out of the stores at home.");
            rifle.Condition = 1;
            CommandResult shot = sim.Execute(new FireWeapon { CharacterId = head.Id, GearId = rifle.Id });
            TestSupport.Ok(shot);
            Assert.That(rifle.Loaded, Is.EqualTo(6));
            Assert.That(rifle.Condition, Is.LessThan(1));
        }

        [Test]
        public void ReloadingTakesFromThePackOnATripAndStopsWhenFull()
        {
            Simulation sim = SimWithRifle(out Character head, out GearItem rifle, cartridges: 3);
            TestSupport.Ok(sim.Execute(new SetOut { Pack = new Dictionary<string, int> { { "cartridges", 3 } } }));
            TestSupport.Ok(sim.Execute(new ReloadWeapon { CharacterId = head.Id, GearId = rifle.Id, Rounds = 2 }));
            Assert.That(rifle.Loaded, Is.EqualTo(2));
            Assert.That(sim.PackCount("cartridges"), Is.EqualTo(1));
            TestSupport.Ok(sim.Execute(new ReloadWeapon { CharacterId = head.Id, GearId = rifle.Id }));
            Assert.That(rifle.Loaded, Is.EqualTo(3));
            TestSupport.Rejected(sim.Execute(new ReloadWeapon { CharacterId = head.Id, GearId = rifle.Id }), "no rifle cartridges");
            rifle.Loaded = 7;
            TestSupport.Rejected(sim.Execute(new ReloadWeapon { CharacterId = head.Id, GearId = rifle.Id }), "already full");
        }

        [Test]
        public void EnemiesReloadFromTheirOwnSupply()
        {
            Simulation sim = InFight(out _, out _);
            Combatant x = sim.Encounter.Combatants.First(c => c.Ammo > 0);
            Character enemy = sim.FindCharacter(x.CharacterId);
            GearItem gun = enemy.Gear.Single(g => g.Id == x.GearId);
            gun.Loaded = 0;
            gun.Jammed = false;
            int spare = x.Ammo;
            TestSupport.Ok(sim.Execute(new ReloadWeapon { CharacterId = enemy.Id, GearId = gun.Id }));
            Assert.That(gun.Loaded + x.Ammo, Is.EqualTo(spare));
        }

        // ---- 3. Jams -------------------------------------------------------------------------------

        [Test]
        public void JamsHappenAtAboutTheGunsRateAndMoreOftenWhenWorn()
        {
            Simulation sim = SimWithRifle(out Character head, out GearItem rifle, cartridges: 0);
            WeaponDef weapon = sim.WeaponOf("lever_rifle");
            int Jams(double condition)
            {
                int jams = 0;
                for (int i = 0; i < 10000; i++)
                {
                    rifle.Loaded = 1; rifle.Jammed = false; rifle.Condition = condition;
                    CommandResult r = sim.Execute(new FireWeapon { CharacterId = head.Id, GearId = rifle.Id });
                    if (r.Events.OfType<WeaponJammed>().Any()) jams++;
                }
                return jams;
            }
            int clean = Jams(1.0), worn = Jams(0.0);
            Assert.That(clean / 10000.0, Is.EqualTo(weapon.JamChance).Within(weapon.JamChance * 0.5));
            Assert.That(worn, Is.GreaterThan(clean * 3));
        }

        [Test]
        public void AJammedGunWontFireOrReloadUntilCleared()
        {
            Simulation sim = SimWithRifle(out Character head, out GearItem rifle);
            rifle.Loaded = 3;
            rifle.Jammed = true;
            TestSupport.Rejected(sim.Execute(new FireWeapon { CharacterId = head.Id, GearId = rifle.Id }), "jammed");
            TestSupport.Rejected(sim.Execute(new ReloadWeapon { CharacterId = head.Id, GearId = rifle.Id }), "jam");
            TestSupport.Ok(sim.Execute(new ClearJam { CharacterId = head.Id, GearId = rifle.Id }));
            TestSupport.Ok(sim.Execute(new FireWeapon { CharacterId = head.Id, GearId = rifle.Id }));
        }

        // ---- 4. Hits -------------------------------------------------------------------------------

        [Test]
        public void ABodyHitWoundsAndAHeadHitDowns()
        {
            Simulation sim = InFight(out Character head, out GearItem rifle);
            List<Character> enemies = Enemies(sim);
            TestSupport.Ok(sim.Execute(new ShotHit { TargetId = enemies[0].Id, Region = BodyRegion.Torso, ShooterId = head.Id, GearId = rifle.Id, Distance = 20 }));
            Mark wound = enemies[0].Marks.Single();
            Assert.That(wound.DefId, Is.EqualTo("wound_gunshot"));
            Assert.That(wound.Severity, Is.EqualTo(2));
            Assert.That(enemies[0].Health, Is.EqualTo(LifeRules.MaxHealth - 2));
            Assert.That(enemies[0].Downed, Is.False);

            Down(sim, head, rifle, enemies[1]);
            Assert.That(enemies[1].Marks.Single().Severity, Is.EqualTo(3));
            Assert.That(enemies[1].Downed, Is.True);
            Assert.That(sim.Encounter.Combatants.Single(x => x.CharacterId == enemies[1].Id).State, Is.EqualTo(CombatantState.Down));
        }

        [Test]
        public void HandsAndLongShotsAreLessSevere()
        {
            Simulation sim = InFight(out Character head, out GearItem rifle);
            List<Character> enemies = Enemies(sim);
            TestSupport.Ok(sim.Execute(new ShotHit { TargetId = enemies[0].Id, Region = BodyRegion.HandL, ShooterId = head.Id, GearId = rifle.Id, Distance = 10 }));
            Assert.That(enemies[0].Marks.Single().Severity, Is.EqualTo(1));
            TestSupport.Ok(sim.Execute(new ShotHit { TargetId = enemies[1].Id, Region = BodyRegion.Torso, ShooterId = head.Id, GearId = rifle.Id, Distance = 90 }));
            Assert.That(enemies[1].Marks.Single().Severity, Is.EqualTo(1));
        }

        [Test]
        public void TheGunRemembersWhoItPutDown()
        {
            Simulation sim = InFight(out Character head, out GearItem rifle);
            Character enemy = Enemies(sim)[0];
            Down(sim, head, rifle, enemy);
            Assert.That(rifle.Ledger.Last().Text, Does.Contain(enemy.GivenName).And.Contain("Miller's Ford"));
        }

        [Test]
        public void AReaversShotWoundsThePlayerWithTheReaverAsTheCause()
        {
            Simulation sim = InFight(out Character head, out _);
            Combatant x = sim.Encounter.Combatants[0];
            TestSupport.Ok(sim.Execute(new ShotHit { TargetId = head.Id, Region = BodyRegion.ArmL, ShooterId = x.CharacterId, GearId = x.GearId, Distance = 10 }));
            Mark wound = head.Marks.Single();
            Assert.That(wound.OriginText, Does.Contain("reaver"));
            Assert.That(head.Health, Is.EqualTo(LifeRules.MaxHealth - 2));
        }

        // ---- 5. Morale -----------------------------------------------------------------------------

        [Test]
        public void DowningTheLeaderBreaksTheGroup()
        {
            Simulation sim = InFight(out Character head, out GearItem rifle);
            Character leader = sim.FindCharacter(sim.Encounter.Combatants.Single(x => x.Leader).CharacterId);
            CommandResult r = sim.Execute(new ShotHit { TargetId = leader.Id, Region = BodyRegion.Head, ShooterId = head.Id, GearId = rifle.Id, Distance = 20 });
            TestSupport.Ok(r);
            GroupBroke broke = r.Events.OfType<GroupBroke>().Single();
            Assert.That(sim.Encounter.Broke, Is.True);
            Assert.That(broke.Fleeing.Count + broke.Surrendering.Count, Is.EqualTo(sim.Encounter.Combatants.Count - 1));
            foreach (Combatant x in sim.Encounter.Combatants.Where(c => !c.Leader)) Assert.That(x.Intent, Is.Not.EqualTo(BreakIntent.None));
        }

        [Test]
        public void HalfTheGroupDownBreaksIt()
        {
            Simulation sim = InFight(out Character head, out GearItem rifle, encounter: "reavers_small");
            Assert.That(sim.Encounter.Combatants.Count, Is.EqualTo(2));
            Character first = Enemies(sim)[0];
            CommandResult r = sim.Execute(new ShotHit { TargetId = first.Id, Region = BodyRegion.Face, ShooterId = head.Id, GearId = rifle.Id, Distance = 20 });
            Assert.That(r.Events.OfType<GroupBroke>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void SurrenderAndFleeSplitAcrossManyFights()
        {
            int surrendered = 0, fled = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                Simulation sim = InFight(out Character head, out GearItem rifle, seed);
                Down(sim, head, rifle, sim.FindCharacter(sim.Encounter.Combatants.Single(x => x.Leader).CharacterId));
                surrendered += sim.Encounter.Combatants.Count(x => x.Intent == BreakIntent.Surrender);
                fled += sim.Encounter.Combatants.Count(x => x.Intent == BreakIntent.Flee);
            }
            double share = surrendered / (double)(surrendered + fled);
            Assert.That(share, Is.EqualTo(sim_SurrenderChance).Within(0.12));
        }

        private const double sim_SurrenderChance = 0.35;

        // ---- 6. Winning ----------------------------------------------------------------------------

        [Test]
        public void YouCannotWinWhileSomeoneIsStillFighting()
        {
            Simulation sim = InFight(out _, out _);
            TestSupport.Rejected(sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Won }), "still fighting");
        }

        [Test]
        public void WinningDropsGearAndLootRecordsTheGrudgeAndCanBePickedUp()
        {
            Simulation sim = InFight(out Character head, out GearItem rifle);
            List<Combatant> fighters = sim.Encounter.Combatants.ToList();
            foreach (Combatant x in fighters)
                if (x.State == CombatantState.Fighting) Down(sim, head, rifle, sim.FindCharacter(x.CharacterId));
            int reaverRep = sim.ReputationWith("reavers");
            CommandResult end = sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Won });
            TestSupport.Ok(end);
            Assert.That(sim.InEncounter, Is.False);
            Assert.That(sim.HasGrudge("reavers"), Is.True);
            Assert.That(sim.ReputationWith("reavers"), Is.LessThan(reaverRep));
            EncounterEnded ended = end.Events.OfType<EncounterEnded>().Single();
            Assert.That(ended.LootIds.Count, Is.EqualTo(fighters.Count));
            Assert.That(sim.State.Dropped.Sum(l => l.Gear.Count), Is.EqualTo(fighters.Count), "Every fallen reaver drops their gun.");
            Assert.That(sim.ChronicleOf(head).Last().Text, Does.Contain("fought off"));

            int gearBefore = head.Gear.Count;
            foreach (int id in ended.LootIds) TestSupport.Ok(sim.Execute(new PickUpLoot { LootId = id }));
            Assert.That(head.Gear.Count, Is.EqualTo(gearBefore + fighters.Count));
            Assert.That(sim.State.Dropped, Is.Empty);
            TestSupport.Rejected(sim.Execute(new PickUpLoot { LootId = ended.LootIds[0] }), "no dropped loot");
        }

        [Test]
        public void ShootingTheOnesWhoGaveUpCostsReputationAndLeavesAMark()
        {
            Simulation sim = InFight(out Character head, out GearItem rifle, seed: 3);
            List<Combatant> fighters = sim.Encounter.Combatants.ToList();
            Down(sim, head, rifle, sim.FindCharacter(fighters[0].CharacterId));
            foreach (Combatant x in fighters.Skip(1))
                if (x.State == CombatantState.Fighting) TestSupport.Ok(sim.Execute(new CombatantSurrenders { CharacterId = x.CharacterId }));
            int homesteaders = sim.ReputationWith("homesteaders");
            TestSupport.Ok(sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Won, Surrender = SurrenderChoice.Execute }));
            foreach (Combatant x in fighters.Skip(1)) Assert.That(sim.FindCharacter(x.CharacterId).Alive, Is.False);
            Assert.That(head.Marks.Any(m => m.DefId == "rite_guilt_shadow"), Is.True);
            Assert.That(sim.ReputationWith("homesteaders"), Is.LessThan(homesteaders + 3), "The surrender bonus is outweighed.");
        }

        [Test]
        public void LettingThemGoKeepsThemAliveAndTheyRemember()
        {
            Simulation sim = InFight(out Character head, out GearItem rifle, seed: 3);
            List<Combatant> fighters = sim.Encounter.Combatants.ToList();
            Down(sim, head, rifle, sim.FindCharacter(fighters[0].CharacterId));
            foreach (Combatant x in fighters.Skip(1))
                if (x.State == CombatantState.Fighting) TestSupport.Ok(sim.Execute(new CombatantSurrenders { CharacterId = x.CharacterId }));
            TestSupport.Ok(sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Won, Surrender = SurrenderChoice.LetGo }));
            Character spared = sim.FindCharacter(fighters[1].CharacterId);
            Assert.That(spared.Alive, Is.True);
            Assert.That(spared.Memories.Last().Text, Does.Contain("let us walk away"));
        }

        // ---- 7. Losing -----------------------------------------------------------------------------

        [Test]
        public void DownedOnATripTheyTakeThePackAndBleedOutRulesFollow()
        {
            Simulation sim = SimWithRifle(out Character head, out GearItem rifle);
            TestSupport.Ok(sim.Execute(new GiveItem { ItemId = "jerky", Count = 3 }));
            TestSupport.Ok(sim.Execute(new SetOut { Pack = new Dictionary<string, int> { { "jerky", 3 }, { "cartridges", 5 } } }));
            TestSupport.Ok(sim.Execute(new StartEncounter { EncounterId = "reavers_road" }));
            TestSupport.Rejected(sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Downed }), "not down");
            Combatant x = sim.Encounter.Combatants[0];
            TestSupport.Ok(sim.Execute(new ShotHit { TargetId = head.Id, Region = BodyRegion.Head, ShooterId = x.CharacterId, GearId = x.GearId, Distance = 20 }));
            Assert.That(head.Downed, Is.True);
            TestSupport.Ok(sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Downed }));
            Assert.That(sim.State.Trip.Pack, Is.Empty);
            Assert.That(head.Downed, Is.True, "Still down: the existing bleed-out rules decide what happens next.");
            Assert.That(sim.InEncounter, Is.False);
        }

        [Test]
        public void FleeingEndsTheFightAndTheyRememberYouRan()
        {
            Simulation sim = InFight(out Character head, out _);
            Character enemy = Enemies(sim)[0];
            TestSupport.Ok(sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Fled }));
            Assert.That(enemy.Memories.Last().Text, Does.Contain("ran from us"));
            Assert.That(sim.State.Dropped, Is.Empty);
        }

        // ---- 8. Saving -----------------------------------------------------------------------------

        [Test]
        public void NoSavingMidFightButAFightStillSurvivesTheSaveFormat()
        {
            Simulation sim = InFight(out _, out _);
            Assert.That(sim.WhyCannotSave(), Is.Not.Null);
            Simulation loaded = Simulation.FromJson(sim.ToJson(), TestSupport.Content);
            Assert.That(loaded.Encounter.Combatants.Count, Is.EqualTo(sim.Encounter.Combatants.Count));
            Assert.That(loaded.ToJson(), Is.EqualTo(sim.ToJson()));
            TestSupport.Ok(sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Fled }));
            Assert.That(sim.WhyCannotSave(), Is.Null);
        }

        // ---- Events and content --------------------------------------------------------------------

        [Test]
        public void AnAmbushOutcomeNamesTheReaverTheEventCastAsLeader()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new StartEvent { EventId = "reaver_toll" }));
            int reaverId = sim.State.ActiveEvent.Cast["reaver"];
            ActiveEventView view = sim.GetActiveEvent();
            int refuse = view.Choices.FindIndex(c => c.Label == "Refuse");
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = refuse }));
            CommandResult draw = sim.Execute(new ChooseEventOption { ChoiceIndex = sim.GetActiveEvent().Choices.FindIndex(c => c.Label == "Draw") });
            TestSupport.Ok(draw);
            AmbushRequested ambush = draw.Events.OfType<AmbushRequested>().Single();
            Assert.That(ambush.EncounterId, Is.EqualTo("reavers_road"));
            Assert.That(ambush.LeaderId, Is.EqualTo(reaverId));
        }

        [Test]
        public void EveryAmbushInTheEventsIsAnEncounterWithAFile()
        {
            foreach (string id in TestSupport.Content.Vocab.Encounters)
                Assert.That(TestSupport.Content.Encounters.ContainsKey(id), Is.True, id);
        }
    }
}

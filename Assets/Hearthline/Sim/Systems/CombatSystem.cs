using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Numbers the combat rules use that are not per weapon or per encounter.</summary>
    public sealed class CombatRules
    {
        /// <summary>A worn-out gun jams this many times more often than a clean one (scales linearly between).</summary>
        public double JamFromWear = 4;
        /// <summary>Hits to the head or face are this much more severe.</summary>
        public int HeadSeverityBonus = 1;
        /// <summary>Hits to the hands are this much less severe (but cost the hand).</summary>
        public int HandSeverityMalus = 1;
        /// <summary>Hits beyond the weapon's range are this much less severe.</summary>
        public int LongRangeMalus = 1;
        /// <summary>Reputation lost with homesteaders for shooting people who gave up.</summary>
        public int ExecuteReputation = -8;
        /// <summary>The mark the head carries after that, if content has it.</summary>
        public string GuiltMark = "rite_guilt_shadow";
    }

    public enum EncounterOutcome { Won, Fled, Downed }

    /// <summary>What happens to enemies who gave up, when a won fight ends.</summary>
    public enum SurrenderChoice { LetGo, TakeGuns, Execute }

    /// <summary>
    /// Ranged combat in the simulation (docs/specs/combat-and-ambush.md): who is in the fight, guns, rounds,
    /// jams and wear, hits becoming wounds, the enemy group breaking, and what a fight leaves behind. Aiming,
    /// movement and the AI are presentation; they report what happens through the commands below.
    /// </summary>
    internal static class CombatSystem
    {
        private static string FullName(Character c) => (c.GivenName + " " + c.Surname).Trim();

        /// <summary>A person the event cast who fits the encounter's leader (or first) group, or null.</summary>
        public static int? CastLeader(SimContext ctx, string encounterId, ActiveEvent active)
        {
            if (active == null || !ctx.Content.Encounters.TryGetValue(encounterId, out EncounterDef def) || def.Enemies.Count == 0) return null;
            EncounterEnemyDef group = def.Enemies.Find(e => e.Leader) ?? def.Enemies[0];
            foreach (var kv in active.Cast)
            {
                Character c = ctx.FindCharacter(kv.Value);
                if (c != null && c.Alive && !c.Downed && c.Archetype == group.Archetype && !LifeSystem.InHousehold(ctx, c)) return c.Id;
            }
            return null;
        }

        public static Combatant FindCombatant(SimContext ctx, int characterId) =>
            ctx.State.Encounter?.Combatants.Find(x => x.CharacterId == characterId);

        public static string Start(SimContext ctx, string encounterId, string eventId, string placeName, int? leaderId)
        {
            if (ctx.State.Encounter != null) return "a fight is already going on";
            if (!ctx.Content.Encounters.TryGetValue(encounterId ?? "", out EncounterDef def)) return "no encounter '" + encounterId + "'";
            if (!def.Staged) return "encounter '" + encounterId + "' is not staged yet";
            if (!ctx.State.Household.HeadId.HasValue) return "there is no head of household";

            Character castLeader = leaderId.HasValue ? ctx.FindCharacter(leaderId.Value) : null;
            if (castLeader != null && (!castLeader.Alive || castLeader.Downed || LifeSystem.InHousehold(ctx, castLeader))) castLeader = null;
            bool leaderGroupExists = def.Enemies.Exists(e => e.Leader);

            var encounter = new ActiveEncounter { EncounterId = def.Id, EventId = eventId, PlaceName = placeName, StartDay = ctx.State.Day };
            foreach (EncounterEnemyDef group in def.Enemies)
            {
                int count = ctx.Rng.NextInt(group.CountMin, group.CountMax + 1);
                for (int i = 0; i < count; i++)
                {
                    bool leader = group.Leader || (!leaderGroupExists && castLeader != null && encounter.Combatants.Count == 0);
                    Character c;
                    if (leader && castLeader != null) { c = castLeader; castLeader = null; }
                    else c = Generate(ctx, group);
                    ItemDef weaponDef = ctx.Content.Items[group.Weapon];
                    GearItem gun = GearSystem.Create(ctx, weaponDef, c, "Carried by");
                    gun.Equipped = true;
                    gun.Condition = Math.Round(group.ConditionMin + ctx.Rng.NextDouble() * (group.ConditionMax - group.ConditionMin), 3);
                    gun.Loaded = weaponDef.Weapon.Capacity;
                    encounter.Combatants.Add(new Combatant
                    {
                        CharacterId = c.Id, Leader = leader, GearId = gun.Id,
                        Ammo = ctx.Rng.NextInt(group.AmmoMin, group.AmmoMax + 1),
                    });
                }
            }
            if (encounter.Combatants.Count == 0) return "encounter '" + encounterId + "' rolled no enemies";
            ctx.State.Encounter = encounter;
            Combatant lead = encounter.Combatants.Find(x => x.Leader);
            ctx.Emit(new EncounterStarted
            {
                Day = ctx.State.Day, EncounterId = def.Id,
                EnemyIds = encounter.Combatants.ConvertAll(x => x.CharacterId), LeaderId = lead?.CharacterId,
            });
            return null;
        }

        private static Character Generate(SimContext ctx, EncounterEnemyDef group)
        {
            var c = new Character
            {
                Id = ctx.State.NextCharacterId++,
                GivenName = LifeSystem.PickGivenName(ctx),
                Surname = ctx.Content.Surnames.Count > 0 ? ctx.Content.Surnames[ctx.Rng.NextInt(0, ctx.Content.Surnames.Count)] : "",
                BirthDay = ctx.State.Day - ctx.Rng.NextInt(18, 46) * GameDate.DaysPerYear,
                Archetype = group.Archetype,
            };
            if (group.TraitsAny.Count > 0) c.Traits.Add(group.TraitsAny[ctx.Rng.NextInt(0, group.TraitsAny.Count)]);
            ctx.State.Characters.Add(c);
            return c;
        }

        /// <summary>The gun a character holds, with its weapon block. Returns why not, or null.</summary>
        public static string Gun(SimContext ctx, Character c, int gearId, out GearItem gun, out WeaponDef weapon)
        {
            gun = c.Gear.Find(g => g.Id == gearId);
            weapon = null;
            if (gun == null) return c.GivenName + " is not carrying gear " + gearId;
            if (!ctx.Content.Items.TryGetValue(gun.DefId, out ItemDef def) || def.Weapon == null) return GearSystem.DisplayName(ctx.Content, gun) + " is not a firearm";
            weapon = def.Weapon;
            return null;
        }

        public static void Fire(SimContext ctx, Character c, GearItem gun, WeaponDef weapon)
        {
            gun.Loaded--;
            double jam = weapon.JamChance * (1 + ctx.Combat.JamFromWear * (1 - gun.Condition));
            gun.Condition = Math.Max(0, Math.Round(gun.Condition - weapon.WearPerShot, 4));
            if (ctx.Rng.Chance(Math.Min(1, jam)))
            {
                gun.Jammed = true;
                ctx.Emit(new WeaponJammed { Day = ctx.State.Day, CharacterId = c.Id, GearId = gun.Id });
            }
            else ctx.Emit(new WeaponFired { Day = ctx.State.Day, CharacterId = c.Id, GearId = gun.Id, Loaded = gun.Loaded });
        }

        /// <summary>Loads rounds from the fighter's spare ammo, or from the pack (on a trip) or the stores. Returns why not, or null.</summary>
        public static string Reload(SimContext ctx, Character c, GearItem gun, WeaponDef weapon, int rounds)
        {
            int space = weapon.Capacity - gun.Loaded;
            if (space <= 0) return GearSystem.DisplayName(ctx.Content, gun) + " is already full";
            int want = rounds > 0 ? Math.Min(rounds, space) : space;
            Combatant fighter = FindCombatant(ctx, c.Id);
            int available;
            if (fighter != null) available = fighter.Ammo;
            else TripSystem.CurrentInventory(ctx.State).TryGetValue(weapon.Ammo, out available);
            int n = Math.Min(want, available);
            if (n <= 0) return "no " + (ctx.Content.Items.TryGetValue(weapon.Ammo, out ItemDef ammo) ? ammo.Name.ToLowerInvariant() : weapon.Ammo) + " to load";
            if (fighter != null) fighter.Ammo -= n;
            else EventSystem.ChangeItem(ctx, weapon.Ammo, -n);
            gun.Loaded += n;
            ctx.Emit(new WeaponReloaded { Day = ctx.State.Day, CharacterId = c.Id, GearId = gun.Id, Loaded = gun.Loaded });
            return null;
        }

        /// <summary>The wound severity a shot makes, from the gun, where it hit and how far it flew.</summary>
        public static int ShotSeverity(SimContext ctx, WeaponDef weapon, BodyRegion region, double distance)
        {
            int severity = weapon?.Severity ?? 2;
            if (region == BodyRegion.Head || region == BodyRegion.Face) severity += ctx.Combat.HeadSeverityBonus;
            if (region == BodyRegion.HandL || region == BodyRegion.HandR) severity -= ctx.Combat.HandSeverityMalus;
            if (weapon != null && distance > weapon.Range) severity -= ctx.Combat.LongRangeMalus;
            return Math.Max(MarkSystem.MinSeverity, Math.Min(MarkSystem.MaxSeverity, severity));
        }

        /// <summary>Called after a hit has been applied: a fighter who went down leaves the fight and the gun remembers it.</summary>
        public static void AfterHit(SimContext ctx, Character target, Character shooter, GearItem gun)
        {
            if (!target.Downed) return;
            Combatant fighter = FindCombatant(ctx, target.Id);
            if (fighter != null && fighter.State == CombatantState.Fighting)
            {
                fighter.State = CombatantState.Down;
                ctx.Emit(new CombatantDown { Day = ctx.State.Day, CharacterId = target.Id, ShooterId = shooter?.Id });
            }
            if (gun != null)
            {
                string place = ctx.State.Encounter?.PlaceName;
                GearSystem.Note(ctx, gun, "Put down " + FullName(target) + (place != null ? " at " + place : "") + ".");
            }
            CheckMorale(ctx);
        }

        public static void Leave(SimContext ctx, Combatant fighter, CombatantState state)
        {
            fighter.State = state;
            fighter.Intent = BreakIntent.None;
            ctx.Emit(new CombatantLeftFight { Day = ctx.State.Day, CharacterId = fighter.CharacterId, State = state });
            CheckMorale(ctx);
        }

        /// <summary>When enough of the group is out of the fight, or the leader is down, the rest break: each flees or gives up.</summary>
        public static void CheckMorale(SimContext ctx)
        {
            ActiveEncounter encounter = ctx.State.Encounter;
            if (encounter == null || encounter.Broke) return;
            EncounterDef def = ctx.Content.Encounters[encounter.EncounterId];
            int total = encounter.Combatants.Count, outOfIt = 0;
            bool leaderDown = false;
            foreach (Combatant x in encounter.Combatants)
            {
                if (x.State != CombatantState.Fighting) outOfIt++;
                if (x.Leader && x.State == CombatantState.Down) leaderDown = true;
            }
            if ((double)outOfIt / total < def.BreakWhenDown && !(def.BreakOnLeaderDown && leaderDown)) return;

            encounter.Broke = true;
            var fleeing = new List<int>();
            var surrendering = new List<int>();
            foreach (Combatant x in encounter.Combatants)
            {
                if (x.State != CombatantState.Fighting) continue;
                x.Intent = ctx.Rng.Chance(def.SurrenderChance) ? BreakIntent.Surrender : BreakIntent.Flee;
                (x.Intent == BreakIntent.Surrender ? surrendering : fleeing).Add(x.CharacterId);
            }
            ctx.Emit(new GroupBroke { Day = ctx.State.Day, Fleeing = fleeing, Surrendering = surrendering });
        }

        public static string End(SimContext ctx, EncounterOutcome outcome, SurrenderChoice choice)
        {
            ActiveEncounter encounter = ctx.State.Encounter;
            if (encounter == null) return "no fight is going on";
            Character head = ctx.State.Household.HeadId.HasValue ? ctx.FindCharacter(ctx.State.Household.HeadId.Value) : null;
            if (outcome == EncounterOutcome.Won && encounter.Combatants.Exists(x => x.State == CombatantState.Fighting))
                return "someone is still fighting";
            if (outcome == EncounterOutcome.Downed && (head == null || !head.Downed)) return "the head of household is not down";

            EncounterDef def = ctx.Content.Encounters[encounter.EncounterId];
            string place = encounter.PlaceName;
            string at = place != null ? " at " + place : "";
            var lootIds = new List<int>();
            string key = outcome == EncounterOutcome.Won ? "won" : outcome == EncounterOutcome.Fled ? "fled" : "downed";
            ApplyOutcome(ctx, def, key, (head != null ? "the " + head.Surname + "s fought " : "fought ") + def.Name.ToLowerInvariant() + at);

            switch (outcome)
            {
                case EncounterOutcome.Won:
                    foreach (Combatant x in encounter.Combatants)
                    {
                        Character enemy = ctx.FindCharacter(x.CharacterId);
                        if (enemy == null) continue;
                        if (x.State == CombatantState.Down || !enemy.Alive) lootIds.Add(DropFrom(ctx, def, enemy, x, rollLoot: true).Id);
                        else if (x.State == CombatantState.Fled && head != null)
                            WorldSystem.Remember(ctx, enemy, FullName(head) + " drove us off" + at + ".");
                    }
                    if (encounter.Combatants.Exists(x => x.State == CombatantState.Surrendered))
                    {
                        ApplyOutcome(ctx, def, "surrender", null);
                        Surrendered(ctx, def, encounter, head, choice, at, lootIds);
                    }
                    if (head != null) ctx.Chronicle(head, ChronicleKind.Deed, head.GivenName + " fought off " + def.Name.ToLowerInvariant() + at + ".");
                    break;
                case EncounterOutcome.Fled:
                    foreach (Combatant x in encounter.Combatants)
                    {
                        Character enemy = ctx.FindCharacter(x.CharacterId);
                        if (enemy != null && enemy.Alive && head != null) WorldSystem.Remember(ctx, enemy, FullName(head) + " ran from us" + at + ".");
                    }
                    break;
                case EncounterOutcome.Downed:
                    if (def.Outcomes.TryGetValue("downed", out EncounterOutcomeDef downed) && downed.Robbed && ctx.State.Trip != null)
                        foreach (var kv in new List<KeyValuePair<string, int>>(ctx.State.Trip.Pack)) EventSystem.ChangeItem(ctx, kv.Key, -kv.Value);
                    foreach (Combatant x in encounter.Combatants)
                    {
                        Character enemy = ctx.FindCharacter(x.CharacterId);
                        if (enemy != null && enemy.Alive && x.State == CombatantState.Fighting && head != null)
                            WorldSystem.Remember(ctx, enemy, "Left " + FullName(head) + " in the dirt" + at + ".");
                    }
                    break;
            }

            ctx.State.Encounter = null;
            ctx.Emit(new EncounterEnded { Day = ctx.State.Day, EncounterId = def.Id, Outcome = key, LootIds = lootIds });
            return null;
        }

        private static void ApplyOutcome(SimContext ctx, EncounterDef def, string key, string grudgeReason)
        {
            if (!def.Outcomes.TryGetValue(key, out EncounterOutcomeDef o)) return;
            var factions = new List<string>(o.Reputation.Keys);
            factions.Sort(StringComparer.Ordinal);
            foreach (string f in factions) WorldSystem.ChangeReputation(ctx, f, o.Reputation[f]);
            if (o.Grudge != null) WorldSystem.AddGrudge(ctx, o.Grudge, grudgeReason ?? "a fight");
        }

        private static void Surrendered(SimContext ctx, EncounterDef def, ActiveEncounter encounter, Character head, SurrenderChoice choice, string at, List<int> lootIds)
        {
            var gaveUp = new List<Character>();
            foreach (Combatant x in encounter.Combatants)
            {
                Character c = ctx.FindCharacter(x.CharacterId);
                if (x.State == CombatantState.Surrendered && c != null && c.Alive) gaveUp.Add(c);
            }
            string headName = head != null ? FullName(head) : "a homesteader";
            foreach (Character c in gaveUp)
            {
                Combatant x = encounter.Combatants.Find(f => f.CharacterId == c.Id);
                switch (choice)
                {
                    case SurrenderChoice.LetGo:
                        WorldSystem.Remember(ctx, c, headName + " let us walk away" + at + ".");
                        break;
                    case SurrenderChoice.TakeGuns:
                        lootIds.Add(DropFrom(ctx, def, c, x, rollLoot: false).Id);
                        WorldSystem.Remember(ctx, c, headName + " took our guns" + at + ".");
                        break;
                    case SurrenderChoice.Execute:
                        LifeSystem.Kill(ctx, c, "shot by " + headName + " after giving up" + at);
                        lootIds.Add(DropFrom(ctx, def, c, x, rollLoot: true).Id);
                        break;
                }
            }
            if (choice != SurrenderChoice.Execute || gaveUp.Count == 0 || head == null) return;
            WorldSystem.ChangeReputation(ctx, "homesteaders", ctx.Combat.ExecuteReputation);
            MarkDef guilt = ctx.Content.FindMark(ctx.Combat.GuiltMark);
            if (guilt != null && !head.Marks.Exists(m => m.DefId == guilt.Id))
            {
                var origin = new MarkOrigin { Cause = "the men who gave up", Place = encounter.PlaceName };
                if (MarkSystem.CheckCanAdd(guilt, guilt.Regions[0], 1, origin) == null) MarkSystem.Add(ctx, head, guilt, guilt.Regions[0], 1, origin);
            }
            WorldSystem.Remember(ctx, head, "Shot the ones who gave up" + at + ".");
        }

        /// <summary>Puts a fallen or disarmed enemy's gun, spare rounds and (optionally) the encounter's loot on the ground.</summary>
        private static DroppedLoot DropFrom(SimContext ctx, EncounterDef def, Character enemy, Combatant fighter, bool rollLoot)
        {
            var loot = new DroppedLoot { Id = ctx.State.NextLootId++, FromCharacterId = enemy.Id };
            foreach (GearItem g in new List<GearItem>(enemy.Gear))
            {
                enemy.Gear.Remove(g);
                g.Equipped = false;
                loot.Gear.Add(g);
                ctx.Emit(new GearChanged { Day = ctx.State.Day, GearId = g.Id, CharacterId = enemy.Id });
            }
            GearItem gun = loot.Gear.Find(g => g.Id == fighter.GearId);
            if (gun != null && fighter.Ammo > 0 && ctx.Content.Items.TryGetValue(gun.DefId, out ItemDef gunDef) && gunDef.Weapon != null)
                Add(loot.Items, gunDef.Weapon.Ammo, fighter.Ammo);
            fighter.Ammo = 0;
            if (rollLoot)
                foreach (EncounterLootDef l in def.Loot)
                    if (ctx.Rng.Chance(l.Chance)) Add(loot.Items, l.ItemId, ctx.Rng.NextInt(l.CountMin, l.CountMax + 1));
            ctx.State.Dropped.Add(loot);
            return loot;
        }

        private static void Add(SortedDictionary<string, int> items, string id, int count)
        {
            if (count <= 0) return;
            items.TryGetValue(id, out int old);
            items[id] = old + count;
        }

        /// <summary>The head picks up a dropped bundle: goods into the pack (or stores), gear into their hands.</summary>
        public static string PickUp(SimContext ctx, int lootId)
        {
            DroppedLoot loot = ctx.State.Dropped.Find(l => l.Id == lootId);
            if (loot == null) return "no dropped loot " + lootId;
            Character head = ctx.State.Household.HeadId.HasValue ? ctx.FindCharacter(ctx.State.Household.HeadId.Value) : null;
            if (head == null || !head.Alive || head.Downed) return "nobody can pick it up";
            foreach (var kv in loot.Items) EventSystem.ChangeItem(ctx, kv.Key, kv.Value);
            Character from = loot.FromCharacterId.HasValue ? ctx.FindCharacter(loot.FromCharacterId.Value) : null;
            foreach (GearItem g in loot.Gear)
            {
                head.Gear.Add(g);
                g.Heirloom = false;
                GearSystem.Note(ctx, g, "Taken by " + FullName(head) + (from != null ? " from " + FullName(from) : "") + ".");
                ctx.Emit(new GearChanged { Day = ctx.State.Day, GearId = g.Id, CharacterId = head.Id });
            }
            ctx.State.Dropped.Remove(loot);
            ctx.Emit(new LootPickedUp { Day = ctx.State.Day, LootId = lootId });
            return null;
        }
    }
}

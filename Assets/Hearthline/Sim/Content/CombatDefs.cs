using System;
using System.Collections.Generic;

namespace Hearthline.Sim.Content
{
    /// <summary>
    /// Makes a gear item a firearm (docs/specs/combat-and-ambush.md). The sim uses ammo, capacity, damage,
    /// severity, range, jam chance and wear. Fire interval, reload time and spread are for presentation to
    /// act out; they live here so each gun is described in one place.
    /// </summary>
    public sealed class WeaponDef
    {
        public string Ammo;
        public int Capacity;
        public DamageType Damage = DamageType.Gunshot;
        /// <summary>Wound severity of a body hit, before region and range adjustments.</summary>
        public int Severity;
        /// <summary>Metres. Beyond it a hit is one severity weaker.</summary>
        public double Range;
        public double FireInterval;
        public double ReloadPerRound;
        /// <summary>Degrees of spread at rest, before stats and movement.</summary>
        public double Spread;
        /// <summary>Chance per shot at full condition.</summary>
        public double JamChance;
        /// <summary>Condition lost per shot (condition runs 1 to 0).</summary>
        public double WearPerShot;
    }

    public sealed class EncounterEnemyDef
    {
        public string Archetype;
        public int CountMin, CountMax;
        /// <summary>Gear item id with a weapon block.</summary>
        public string Weapon;
        /// <summary>Spare rounds each carries, beyond a full gun.</summary>
        public int AmmoMin, AmmoMax;
        public double ConditionMin = 1, ConditionMax = 1;
        public readonly List<string> TraitsAny = new List<string>();
        public bool Leader;
    }

    public sealed class EncounterLootDef { public string ItemId; public int CountMin = 1, CountMax = 1; public double Chance = 1; }

    public sealed class EncounterOutcomeDef
    {
        public readonly Dictionary<string, int> Reputation = new Dictionary<string, int>();
        /// <summary>Faction that takes up a grudge against the surname, or null.</summary>
        public string Grudge;
        /// <summary>Downed only: the enemies take what the party was carrying.</summary>
        public bool Robbed;
    }

    /// <summary>A fight presentation can stage, requested by an event's <c>ambush:</c> outcome.</summary>
    public sealed class EncounterDef
    {
        public string Id;
        public string Name;
        /// <summary>False: the fight is only reported (no enemies defined yet, e.g. animals).</summary>
        public bool Staged = true;
        public readonly List<EncounterEnemyDef> Enemies = new List<EncounterEnemyDef>();
        public double SpawnDistanceMin = 20, SpawnDistanceMax = 35;
        public double SpreadDegrees = 120;
        /// <summary>The group breaks once this share of it is down, fled or surrendered.</summary>
        public double BreakWhenDown = 0.5;
        public bool BreakOnLeaderDown = true;
        /// <summary>When the group breaks, each fighter still standing surrenders with this chance; otherwise flees.</summary>
        public double SurrenderChance;
        public readonly List<EncounterLootDef> Loot = new List<EncounterLootDef>();
        /// <summary>Outcome key (won, fled, surrender, downed) -> what it does.</summary>
        public readonly Dictionary<string, EncounterOutcomeDef> Outcomes = new Dictionary<string, EncounterOutcomeDef>();
        public string SourceFile;

        public static readonly string[] OutcomeKeys = { "won", "fled", "surrender", "downed" };
    }

    /// <summary>Reads weapon blocks and encounter files, and checks them against the rest of the content.</summary>
    internal static class CombatReader
    {
        public static WeaponDef ReadWeapon(YamlMap root)
        {
            YamlMap w = root.Map("weapon", "weapon");
            if (w == null) return null;
            var def = new WeaponDef
            {
                Ammo = w.Str("ammo"),
                Capacity = w.Int("capacity", true) ?? 0,
                Damage = w.Enum<DamageType>("damage", false) ?? DamageType.Gunshot,
                Severity = w.Int("severity", true) ?? 0,
                Range = w.Num("range", true) ?? 0,
                FireInterval = w.Num("fire_interval", true) ?? 0,
                ReloadPerRound = w.Num("reload_per_round", true) ?? 0,
                Spread = w.Num("spread", true) ?? 0,
                JamChance = w.Num("jam_chance") ?? 0,
                WearPerShot = w.Num("wear_per_shot") ?? 0,
            };
            if (def.Capacity < 1) w.Error("'capacity' must be at least 1");
            if (def.Severity < 1) w.Error("'severity' must be at least 1");
            if (def.Range <= 0 || def.FireInterval <= 0 || def.ReloadPerRound <= 0) w.Error("'range', 'fire_interval' and 'reload_per_round' must be positive");
            if (def.Spread < 0) w.Error("'spread' must not be negative");
            if (def.JamChance < 0 || def.JamChance > 1) w.Error("'jam_chance' must be 0..1");
            if (def.WearPerShot < 0 || def.WearPerShot > 1) w.Error("'wear_per_shot' must be 0..1");
            w.Done();
            return def;
        }

        public static EncounterDef ReadEncounter(YamlMap root, string path)
        {
            var def = new EncounterDef { SourceFile = path, Id = root.Str("id"), Name = root.Str("name"), Staged = root.Bool("staged") ?? true };
            foreach (YamlMap e in root.MapList("enemies", "enemies", def.Staged))
            {
                var enemy = new EncounterEnemyDef { Archetype = e.Str("archetype"), Weapon = e.Str("weapon"), Leader = e.Str("role", false) == "leader" };
                (enemy.CountMin, enemy.CountMax) = IntRange(e, "count", true, 0);
                (enemy.AmmoMin, enemy.AmmoMax) = IntRange(e, "ammo", false, 0);
                (enemy.ConditionMin, enemy.ConditionMax) = NumRange(e, "condition", 1);
                enemy.TraitsAny.AddRange(e.StrList("traits_any"));
                if (e.Has("role") && !enemy.Leader) e.Error("'role' can only be 'leader'");
                e.Done();
                def.Enemies.Add(enemy);
            }
            YamlMap spawn = root.Map("spawn", "spawn");
            if (spawn != null)
            {
                (def.SpawnDistanceMin, def.SpawnDistanceMax) = NumRange(spawn, "distance", 0);
                def.SpreadDegrees = spawn.Num("spread_degrees") ?? def.SpreadDegrees;
                spawn.Done();
            }
            YamlMap morale = root.Map("morale", "morale", def.Staged);
            if (morale != null)
            {
                def.BreakWhenDown = morale.Num("break_when_down", true) ?? def.BreakWhenDown;
                def.BreakOnLeaderDown = morale.Bool("break_on_leader_down") ?? def.BreakOnLeaderDown;
                def.SurrenderChance = morale.Num("surrender_chance") ?? 0;
                if (def.BreakWhenDown <= 0 || def.BreakWhenDown > 1) morale.Error("'break_when_down' must be in (0, 1]");
                if (def.SurrenderChance < 0 || def.SurrenderChance > 1) morale.Error("'surrender_chance' must be 0..1");
                morale.Done();
            }
            foreach (YamlMap l in root.MapList("loot", "loot"))
            {
                var loot = new EncounterLootDef { ItemId = l.Str("item"), Chance = l.Num("chance") ?? 1 };
                (loot.CountMin, loot.CountMax) = IntRange(l, "count", false, 1);
                if (loot.Chance <= 0 || loot.Chance > 1) l.Error("'chance' must be in (0, 1]");
                l.Done();
                def.Loot.Add(loot);
            }
            YamlMap outcomes = root.Map("outcomes", "outcomes");
            if (outcomes != null)
            {
                foreach (string key in new List<string>(outcomes.Keys))
                {
                    YamlMap o = outcomes.Map(key, "outcomes." + key);
                    if (o == null) continue;
                    if (Array.IndexOf(EncounterDef.OutcomeKeys, key) < 0) o.Error("unknown outcome '" + key + "' (allowed: " + string.Join(", ", EncounterDef.OutcomeKeys) + ")");
                    var outcome = new EncounterOutcomeDef { Grudge = o.Str("grudge", false), Robbed = o.Bool("robbed") ?? false };
                    foreach (var kv in o.NumMap("reputation")) outcome.Reputation[kv.Key] = (int)kv.Value;
                    if (outcome.Robbed && key != "downed") o.Error("'robbed' only applies to the downed outcome");
                    o.Done();
                    def.Outcomes[key] = outcome;
                }
                outcomes.Done();
            }
            if (def.SpawnDistanceMin <= 0 || def.SpawnDistanceMax < def.SpawnDistanceMin) root.Error("spawn 'distance' must be [min, max] with 0 < min <= max");
            root.Done();
            return def;
        }

        private static (int, int) IntRange(YamlMap m, string key, bool required, int fallback)
        {
            if (!m.Has(key))
            {
                if (required) m.Error("missing required field '" + key + "'");
                m.StrList(key);
                return (fallback, fallback);
            }
            List<string> parts = m.StrList(key);
            if (parts.Count == 2 && int.TryParse(parts[0], out int lo) && int.TryParse(parts[1], out int hi) && lo >= 0 && hi >= lo) return (lo, hi);
            m.Error("'" + key + "' must be [min, max] whole numbers with 0 <= min <= max");
            return (fallback, fallback);
        }

        private static (double, double) NumRange(YamlMap m, string key, double fallback)
        {
            if (!m.Has(key)) { m.StrList(key); return (fallback, fallback); }
            List<string> parts = m.StrList(key);
            if (parts.Count == 2
                && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lo)
                && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hi)
                && hi >= lo) return (lo, hi);
            m.Error("'" + key + "' must be [min, max] with min <= max");
            return (fallback, fallback);
        }

        public static void Validate(ContentDatabase db, List<ContentError> errors)
        {
            var itemIds = new List<string>(db.Items.Keys);
            itemIds.Sort(StringComparer.Ordinal);
            foreach (string id in itemIds)
            {
                ItemDef item = db.Items[id];
                if (item.Weapon == null) continue;
                void Err(string message) => errors.Add(new ContentError { File = item.SourceFile, Message = message });
                if (item.Equip == null) Err("a weapon must be gear (needs an 'equip' block)");
                else if (item.Equip.Slot != EquipSlot.LongGun && item.Equip.Slot != EquipSlot.Sidearm) Err("a weapon goes in the long_gun or sidearm slot");
                if (item.Weapon.Ammo != null)
                {
                    if (!db.Items.TryGetValue(item.Weapon.Ammo, out ItemDef ammo)) Err("weapon ammo: no item '" + item.Weapon.Ammo + "'");
                    else if (ammo.Equip != null) Err("weapon ammo '" + ammo.Id + "' must be a stackable item, not gear");
                }
            }

            var encounterIds = new List<string>(db.Encounters.Keys);
            encounterIds.Sort(StringComparer.Ordinal);
            foreach (string id in encounterIds)
            {
                EncounterDef def = db.Encounters[id];
                void Err(string message) => errors.Add(new ContentError { File = def.SourceFile, Message = message });
                if (!db.Vocab.Encounters.Contains(def.Id)) Err("encounter '" + def.Id + "' is not listed under encounters in vocab.yaml");
                if (def.Staged && def.Enemies.Count == 0) Err("a staged encounter needs enemies");
                if (def.Staged && !def.Enemies.Exists(e => e.CountMin > 0)) Err("at least one enemy group must always appear (count min > 0)");
                if (def.Enemies.FindAll(e => e.Leader).Count > 1) Err("only one enemy group can be the leader");
                foreach (EncounterEnemyDef e in def.Enemies)
                {
                    if (e.Archetype != null && !db.Vocab.Archetypes.Contains(e.Archetype)) Err("no archetype '" + e.Archetype + "' in vocab.yaml");
                    if (e.Weapon != null && (!db.Items.TryGetValue(e.Weapon, out ItemDef w) || w.Weapon == null)) Err("enemy weapon '" + e.Weapon + "' is not an item with a weapon block");
                    if (e.Leader && e.CountMax > 1) Err("the leader group can have at most one enemy");
                    if (e.ConditionMin < 0 || e.ConditionMax > 1) Err("enemy 'condition' must be within 0..1");
                    foreach (string t in e.TraitsAny) if (db.FindTrait(t) == null) Err("no trait '" + t + "'");
                }
                foreach (EncounterLootDef l in def.Loot)
                {
                    if (!db.Items.TryGetValue(l.ItemId ?? "", out ItemDef item)) Err("loot: no item '" + l.ItemId + "'");
                    else if (item.Equip != null) Err("loot '" + l.ItemId + "' is gear; enemies drop their gear by themselves");
                }
                foreach (var kv in def.Outcomes)
                {
                    foreach (string faction in kv.Value.Reputation.Keys) if (!db.Factions.ContainsKey(faction)) Err("outcomes." + kv.Key + ": no faction '" + faction + "'");
                    if (kv.Value.Grudge != null && !db.Factions.ContainsKey(kv.Value.Grudge)) Err("outcomes." + kv.Key + ": no faction '" + kv.Value.Grudge + "' for the grudge");
                }
            }
            var vocabIds = new List<string>(db.Vocab.Encounters);
            vocabIds.Sort(StringComparer.Ordinal);
            foreach (string id in vocabIds)
                if (!db.Encounters.ContainsKey(id))
                    errors.Add(new ContentError { File = ContentLoader.VocabFile, Message = "encounter '" + id + "' has no file encounters/" + id + ".yaml" });
        }
    }
}

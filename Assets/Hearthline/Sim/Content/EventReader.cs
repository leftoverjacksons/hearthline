using System;
using System.Collections.Generic;

namespace Hearthline.Sim.Content
{
    /// <summary>Parses and validates factions, items, loot tables and events.</summary>
    internal static class EventReader
    {
        public static FactionDef ReadFaction(YamlMap root, string path)
        {
            var def = new FactionDef { SourceFile = path, Id = root.Str("id"), Name = root.Str("name"), Description = root.Str("description") };
            root.Done();
            return def;
        }

        public static ItemDef ReadItem(YamlMap root, string path)
        {
            var def = new ItemDef { SourceFile = path, Id = root.Str("id"), Name = root.Str("name"), Weight = root.Num("weight", true) ?? 0, Value = root.Int("value") ?? 0 };
            if (def.Value < 0) root.Error("'value' must not be negative");
            def.Tags.AddRange(root.StrList("tags"));
            if (def.Weight < 0) root.Error("'weight' must not be negative");
            root.Done();
            return def;
        }

        public static StructureDef ReadStructure(YamlMap root, string path)
        {
            var def = new StructureDef { SourceFile = path, Id = root.Str("id"), Name = root.Str("name"), StartsAt = root.Int("starts_at") ?? 0 };
            foreach (YamlMap t in root.MapList("tiers", "tiers", true))
            {
                var tier = new StructureTier { Name = t.Str("name") };
                ReadWholeNumbers(t, "cost", tier.Cost);
                foreach (var kv in t.EnumIntMap<HomeStat>("provides")) tier.Provides[kv.Key] = kv.Value;
                t.Done();
                def.Tiers.Add(tier);
            }
            root.Done();
            return def;
        }

        public static JobDef ReadJob(YamlMap root, string path)
        {
            var def = new JobDef { SourceFile = path, Id = root.Str("id"), Name = root.Str("name"), Skill = root.Str("skill") };
            def.Structure = root.Has("structure") ? root.Str("structure") : null;
            ReadEntries(root, "produces", false, def.Produces);
            foreach (var kv in root.EnumIntMap<HomeStat>("provides")) def.Provides[kv.Key] = kv.Value;
            root.Done();
            return def;
        }

        private static void ValidateHome(ContentDatabase db, List<ContentError> errors)
        {
            foreach (StructureDef def in Sorted(db.Structures))
            {
                void Err(string message) => errors.Add(new ContentError { File = def.SourceFile, Message = message });
                if (!Naming.IsSnakeId(def.Id)) Err("id '" + def.Id + "' must be snake_case");
                if (def.Tiers.Count == 0) Err("a structure needs at least one tier");
                if (def.StartsAt < 0 || def.StartsAt > def.Tiers.Count) Err("starts_at must be 0.." + def.Tiers.Count);
                foreach (StructureTier tier in def.Tiers)
                {
                    foreach (var kv in tier.Cost)
                    {
                        if (!db.Items.ContainsKey(kv.Key)) Err("tier '" + tier.Name + "': no item '" + kv.Key + "'");
                        if (kv.Value < 1) Err("tier '" + tier.Name + "': cost of " + kv.Key + " must be positive");
                    }
                }
            }
            foreach (JobDef def in Sorted(db.Jobs))
            {
                void Err(string message) => errors.Add(new ContentError { File = def.SourceFile, Message = message });
                if (!Naming.IsSnakeId(def.Id)) Err("id '" + def.Id + "' must be snake_case");
                if (def.Skill != null && !db.Vocab.Stats.ContainsKey(def.Skill)) Err("no stat '" + def.Skill + "'");
                if (def.Structure != null && !db.Structures.ContainsKey(def.Structure)) Err("no structure '" + def.Structure + "'");
                if (def.Produces.Count == 0 && def.Provides.Count == 0) Err("a job must produce or provide something");
                foreach (LootEntry e in def.Produces) if (!db.Items.ContainsKey(e.ItemId)) Err("no item '" + e.ItemId + "'");
            }
        }

        private static void ReadEntries(YamlMap root, string key, bool required, List<LootEntry> target)
        {
            foreach (YamlMap e in root.MapList(key, key, required))
            {
                var entry = new LootEntry { ItemId = e.Str("item"), Weight = e.Num("weight") ?? 1 };
                entry.CountMin = e.Int("count_min") ?? 1;
                entry.CountMax = e.Int("count_max") ?? entry.CountMin;
                if (entry.CountMin < 1 || entry.CountMax < entry.CountMin) e.Error("needs 1 <= count_min <= count_max");
                e.Done();
                if (entry.ItemId != null) target.Add(entry);
            }
        }

        public static LootTableDef ReadLoot(YamlMap root, string path)
        {
            var def = new LootTableDef { SourceFile = path, Id = root.Str("id"), Rolls = root.Int("rolls") ?? 1 };
            foreach (YamlMap e in root.MapList("entries", "entries", true))
            {
                var entry = new LootEntry { ItemId = e.Str("item"), Weight = e.Num("weight") ?? 1 };
                entry.CountMin = e.Int("count_min") ?? 1;
                entry.CountMax = e.Int("count_max") ?? entry.CountMin;
                if (entry.CountMin < 1 || entry.CountMax < entry.CountMin) e.Error("needs 1 <= count_min <= count_max");
                if (entry.Weight <= 0) e.Error("'weight' must be positive");
                e.Done();
                if (entry.ItemId != null) def.Entries.Add(entry);
            }
            if (def.Rolls < 1) root.Error("'rolls' must be at least 1");
            root.Done();
            return def;
        }

        public static SettlementDef ReadSettlement(YamlMap root, string path)
        {
            var def = new SettlementDef { SourceFile = path, Id = root.Str("id"), Name = root.Str("name"), FactionId = root.Str("faction"), Home = root.Bool("home") ?? false };
            foreach (var kv in root.EnumIntMap<SettlementValue>("values")) def.Start[kv.Key] = kv.Value;
            foreach (var kv in root.EnumIntMap<SettlementValue>("baseline")) def.Baseline[kv.Key] = kv.Value;
            foreach (YamlMap s in root.MapList("services", "services"))
            {
                var service = new ServiceDef { Id = s.Str("id"), Name = s.Str("name") };
                foreach (var kv in s.EnumIntMap<SettlementValue>("min")) service.Min[kv.Key] = kv.Value;
                foreach (var kv in s.EnumIntMap<SettlementValue>("max")) service.Max[kv.Key] = kv.Value;
                s.Done();
                if (service.Id != null) def.Services.Add(service);
            }
            foreach (YamlMap r in root.MapList("residents", "residents"))
            {
                var slot = new ResidentSlot { Role = r.Str("role"), Archetype = r.Str("archetype") };
                slot.AgeMin = r.Int("age_min") ?? slot.AgeMin;
                slot.AgeMax = r.Int("age_max") ?? slot.AgeMax;
                r.Done();
                if (slot.Role != null) def.Residents.Add(slot);
            }
            root.Done();
            return def;
        }

        private static void ValidateSettlement(ContentDatabase db, SettlementDef def, List<ContentError> errors)
        {
            void Err(string message) => errors.Add(new ContentError { File = def.SourceFile, Message = message });
            if (!Naming.IsSnakeId(def.Id)) Err("id '" + def.Id + "' must be snake_case");
            if (def.FactionId != null && !db.Factions.ContainsKey(def.FactionId)) Err("no faction '" + def.FactionId + "'");
            foreach (SettlementValue v in (SettlementValue[])Enum.GetValues(typeof(SettlementValue)))
            {
                if (!def.Start.ContainsKey(v)) Err("values needs '" + Naming.ToSnake(v) + "'");
                if (!def.Baseline.ContainsKey(v)) Err("baseline needs '" + Naming.ToSnake(v) + "'");
            }
            foreach (var map in new[] { def.Start, def.Baseline })
                foreach (var kv in map) if (kv.Value < 0 || kv.Value > 100) Err(Naming.ToSnake(kv.Key) + " must be 0..100");
            var seen = new HashSet<string>();
            foreach (ServiceDef s in def.Services)
            {
                if (!Naming.IsSnakeId(s.Id)) Err("service id '" + s.Id + "' must be snake_case");
                if (!seen.Add(s.Id)) Err("duplicate service '" + s.Id + "'");
                foreach (var map in new[] { s.Min, s.Max })
                    foreach (var kv in map) if (kv.Value < 0 || kv.Value > 100) Err("service '" + s.Id + "': thresholds must be 0..100");
            }
            seen.Clear();
            foreach (ResidentSlot r in def.Residents)
            {
                if (!Naming.IsSnakeId(r.Role)) Err("resident role '" + r.Role + "' must be snake_case");
                if (!seen.Add(r.Role)) Err("duplicate resident role '" + r.Role + "'");
                if (r.Archetype != null && !db.Vocab.Archetypes.Contains(r.Archetype)) Err("resident '" + r.Role + "': no archetype '" + r.Archetype + "'");
                if (r.AgeMin < LifeRules.AdultAge || r.AgeMax < r.AgeMin) Err("resident '" + r.Role + "': needs " + LifeRules.AdultAge + " <= age_min <= age_max");
            }
        }

        public static EventDef ReadEvent(YamlMap root, string path)
        {
            var def = new EventDef { SourceFile = path, Id = root.Str("id") };
            def.Tags.AddRange(root.StrList("tags", true));
            def.Weight = root.Num("weight") ?? 10;
            def.CooldownDays = root.Int("cooldown_days") ?? 0;
            def.Intensity = root.Int("intensity") ?? 1;

            YamlMap when = root.Map("when", "when");
            if (when != null)
            {
                def.When.AreaTagsAny.AddRange(when.StrList("area_tags_any"));
                def.When.AreaTagsAll.AddRange(when.StrList("area_tags_all"));
                def.When.ThinLevelMin = when.Int("thin_level_min");
                def.When.ThinLevelMax = when.Int("thin_level_max");
                def.When.TimeOfDay.AddRange(when.EnumList<TimeOfDay>("time_of_day"));
                def.When.AtHome = when.Bool("at_home");
                def.When.FlagsAll.AddRange(when.StrList("flags_all"));
                def.When.FlagsNone.AddRange(when.StrList("flags_none"));
                ReadWholeNumbers(when, "reputation_min", def.When.ReputationMin);
                ReadWholeNumbers(when, "reputation_max", def.When.ReputationMax);
                def.When.GrudgeAny.AddRange(when.StrList("grudge_any"));
                def.When.ServicesAny.AddRange(when.StrList("services_any"));
                foreach (var kv in when.EnumIntMap<SettlementValue>("settlement_min")) def.When.SettlementMin[kv.Key] = kv.Value;
                foreach (var kv in when.EnumIntMap<SettlementValue>("settlement_max")) def.When.SettlementMax[kv.Key] = kv.Value;
                foreach (var kv in when.EnumIntMap<HomeStat>("home_min")) def.When.HomeMin[kv.Key] = kv.Value;
                foreach (var kv in when.EnumIntMap<HomeStat>("home_max")) def.When.HomeMax[kv.Key] = kv.Value;
                def.When.MoraleMin = when.Int("morale_min");
                def.When.MoraleMax = when.Int("morale_max");
                when.Done();
            }

            YamlMap cast = root.Map("cast", "cast", true);
            if (cast != null)
            {
                foreach (string role in new List<string>(cast.Keys))
                {
                    YamlMap c = cast.Map(role, "cast." + role, true);
                    if (c == null) continue;
                    var slot = new CastSlot { Role = role };
                    slot.From = c.Has("from") ? c.Str("from") : null;
                    slot.Generate = c.Has("generate") ? c.Str("generate") : null;
                    if (c.Has("role")) slot.Pick = c.Str("role") ?? "any";
                    slot.AgeMin = c.Int("age_min") ?? slot.AgeMin;
                    slot.AgeMax = c.Int("age_max") ?? slot.AgeMax;
                    slot.Traits.AddRange(c.StrList(slot.Generate != null ? "traits" : "traits_any"));
                    slot.MarksAny.AddRange(c.StrList("marks_any"));
                    c.Done();
                    def.Cast.Add(slot);
                }
            }

            int n = 0;
            foreach (YamlMap b in root.MapList("beats", "beats", true))
            {
                var beat = new EventBeat { Id = b.Has("id") ? b.Str("id") : "beat_" + n, Text = b.Str("text") };
                n++;
                foreach (YamlMap ch in b.MapList("choices", "beat '" + beat.Id + "' choices", true))
                {
                    var choice = new EventChoice { Label = ch.Str("label") };
                    YamlMap req = ch.Map("requires", "choice '" + choice.Label + "' requires");
                    if (req != null)
                    {
                        var r = new ChoiceRequires { Who = req.Has("who") ? req.Str("who") : null };
                        r.ItemAny.AddRange(req.StrList("item_any"));
                        r.TraitAny.AddRange(req.StrList("trait_any"));
                        r.MarkAny.AddRange(req.StrList("mark_any"));
                        r.FlagsAll.AddRange(req.StrList("flags_all"));
                        foreach (var kv in req.NumMap("stat_min")) r.StatMin[kv.Key] = kv.Value;
                        req.Done();
                        choice.Requires = r;
                    }
                    YamlMap o = ch.Map("outcome", "choice '" + choice.Label + "' outcome", true);
                    if (o != null) choice.Outcome = ReadOutcome(o, true);
                    ch.Done();
                    beat.Choices.Add(choice);
                }
                b.Done();
                def.Beats.Add(beat);
            }
            root.Done();
            return def;
        }

        private static void ReadWholeNumbers(YamlMap o, string key, Dictionary<string, int> target)
        {
            foreach (var kv in o.NumMap(key))
            {
                if (kv.Value != Math.Floor(kv.Value)) o.Error("'" + key + "." + kv.Key + "' must be a whole number");
                target[kv.Key] = (int)kv.Value;
            }
        }

        private static void ReadInts(YamlMap o, string key, Dictionary<string, int> target)
        {
            foreach (var kv in o.NumMap(key))
            {
                if (kv.Value != Math.Floor(kv.Value) || kv.Value == 0) o.Error("'" + key + "." + kv.Key + "' must be a non-zero whole number");
                target[kv.Key] = (int)kv.Value;
            }
        }

        private static EventOutcome ReadOutcome(YamlMap o, bool allowChance)
        {
            var outcome = new EventOutcome();
            ReadInts(o, "relationship", outcome.Relationship);
            ReadInts(o, "reputation", outcome.Reputation);
            ReadInts(o, "item", outcome.Items);
            YamlMap m = o.Map("mark", "mark");
            if (m != null)
            {
                outcome.Mark = new MarkOutcome
                {
                    Target = m.Str("target"), DefId = m.Str("def"), Region = m.Has("region") ? m.Enum<BodyRegion>("region") : null,
                    Severity = m.Int("severity") ?? 1, Chance = m.Num("chance") ?? 1, Cause = m.Has("cause") ? m.Str("cause") : null,
                };
                if (outcome.Mark.Chance <= 0 || outcome.Mark.Chance > 1) m.Error("'chance' must be above 0 and at most 1");
                m.Done();
            }
            YamlMap h = o.Map("hit", "hit");
            if (h != null)
            {
                outcome.Hit = new HitOutcome
                {
                    Target = h.Str("target"), Damage = h.Enum<DamageType>("damage") ?? DamageType.Blunt,
                    Region = h.Has("region") ? h.Enum<BodyRegion>("region") : null, Severity = h.Int("severity") ?? 1, Cause = h.Str("cause"),
                };
                h.Done();
            }
            YamlMap t = o.Map("trait", "trait");
            if (t != null)
            {
                outcome.Trait = new TraitOutcome { Target = t.Str("target"), Add = t.Has("add") ? t.Str("add") : null, Remove = t.Has("remove") ? t.Str("remove") : null };
                if ((outcome.Trait.Add == null) == (outcome.Trait.Remove == null)) t.Error("needs exactly one of 'add' or 'remove'");
                t.Done();
            }
            outcome.LootTable = o.Has("loot_table") ? o.Str("loot_table") : null;
            outcome.FlagSet.AddRange(o.StrList("flag_set"));
            outcome.FlagClear.AddRange(o.StrList("flag_clear"));
            outcome.Recruit = o.Has("recruit") ? o.Str("recruit") : null;
            outcome.Ambush = o.Has("ambush") ? o.Str("ambush") : null;
            outcome.Chronicle = o.Has("chronicle") ? o.Str("chronicle") : null;
            foreach (var kv in o.EnumIntMap<SettlementValue>("settlement"))
            {
                if (kv.Value == 0) o.Error("'settlement." + Naming.ToSnake(kv.Key) + "' must not be zero");
                outcome.Settlement[kv.Key] = kv.Value;
            }
            YamlMap g = o.Map("grudge", "grudge");
            if (g != null)
            {
                outcome.Grudge = new GrudgeOutcome { FactionId = g.Str("faction"), Reason = g.Str("reason") };
                g.Done();
            }
            outcome.SettleGrudge = o.Has("settle_grudge") ? o.Str("settle_grudge") : null;
            YamlMap mem = o.Map("memory", "memory");
            if (mem != null)
            {
                outcome.Memory = new MemoryOutcome { Target = mem.Str("target"), Text = mem.Str("text") };
                mem.Done();
            }
            outcome.Morale = o.Int("morale") ?? 0;
            if (o.Has("morale") && outcome.Morale == 0) o.Error("'morale' must not be zero");
            outcome.Damage = o.Has("damage") ? o.Str("damage") : null;
            outcome.Goto = o.Has("goto") ? o.Str("goto") : null;
            if (o.Has("chance"))
            {
                if (!allowChance) o.Error("'chance' cannot be nested inside another chance");
                double total = 0;
                foreach (YamlMap b in o.MapList("chance", "chance"))
                {
                    double p = b.Num("p", true) ?? 0;
                    if (p <= 0 || p > 1) b.Error("'p' must be above 0 and at most 1");
                    total += p;
                    outcome.Chance.Add(new ChanceBranch { P = p, Outcome = ReadOutcome(b, false) });
                }
                if (total > 1.0001) o.Error("chance probabilities add up to more than 1");
            }
            o.Done();
            return outcome;
        }

        // ---- Validation ----------------------------------------------------------------------------

        public static void Validate(ContentDatabase db, List<ContentError> errors)
        {
            foreach (FactionDef f in Sorted(db.Factions))
                if (!Naming.IsSnakeId(f.Id)) errors.Add(new ContentError { File = f.SourceFile, Message = "id '" + f.Id + "' must be snake_case" });
            foreach (ItemDef i in Sorted(db.Items))
                if (!Naming.IsSnakeId(i.Id)) errors.Add(new ContentError { File = i.SourceFile, Message = "id '" + i.Id + "' must be snake_case" });
            foreach (LootTableDef l in Sorted(db.LootTables))
            {
                if (!Naming.IsSnakeId(l.Id)) errors.Add(new ContentError { File = l.SourceFile, Message = "id '" + l.Id + "' must be snake_case" });
                if (l.Entries.Count == 0) errors.Add(new ContentError { File = l.SourceFile, Message = "a loot table needs at least one entry" });
                foreach (LootEntry e in l.Entries)
                    if (!db.Items.ContainsKey(e.ItemId)) errors.Add(new ContentError { File = l.SourceFile, Message = "no item '" + e.ItemId + "'" });
            }
            ValidateHome(db, errors);
            int homes = 0;
            foreach (SettlementDef s in Sorted(db.Settlements))
            {
                ValidateSettlement(db, s, errors);
                if (s.Home) homes++;
            }
            if (db.Settlements.Count > 0 && homes != 1)
                errors.Add(new ContentError { File = "settlements/", Message = "exactly one settlement must have 'home: true' (found " + homes + ")" });
            foreach (EventDef e in Sorted(db.Events)) ValidateEvent(db, e, errors);
        }

        private static List<T> Sorted<T>(Dictionary<string, T> map)
        {
            var keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            var list = new List<T>();
            foreach (string k in keys) list.Add(map[k]);
            return list;
        }

        private static void ValidateEvent(ContentDatabase db, EventDef def, List<ContentError> errors)
        {
            void Err(string message) => errors.Add(new ContentError { File = def.SourceFile, Message = message });
            void Known(bool ok, string kind, string id, string where) { if (!ok) Err(where + ": no " + kind + " '" + id + "'"); }

            if (!Naming.IsSnakeId(def.Id)) Err("id '" + def.Id + "' must be snake_case");
            if (def.Weight <= 0) Err("weight must be positive");
            if (def.CooldownDays < 0) Err("cooldown_days must not be negative");
            if (def.Intensity < 1 || def.Intensity > 3) Err("intensity must be 1..3");
            foreach (string tag in def.When.AreaTagsAny) Known(db.Vocab.AreaTags.Contains(tag), "area tag", tag, "when");
            foreach (string tag in def.When.AreaTagsAll) Known(db.Vocab.AreaTags.Contains(tag), "area tag", tag, "when");

            foreach (string f in def.When.ReputationMin.Keys) Known(db.Factions.ContainsKey(f), "faction", f, "when");
            foreach (string f in def.When.ReputationMax.Keys) Known(db.Factions.ContainsKey(f), "faction", f, "when");
            foreach (string f in def.When.GrudgeAny) Known(db.Factions.ContainsKey(f), "faction", f, "when");
            bool ServiceExists(string id) { foreach (SettlementDef s in db.Settlements.Values) if (s.Services.Exists(x => x.Id == id)) return true; return false; }
            bool ResidentExists(string role) { foreach (SettlementDef s in db.Settlements.Values) if (s.Residents.Exists(x => x.Role == role)) return true; return false; }
            foreach (string s in def.When.ServicesAny) Known(ServiceExists(s), "service", s, "when");

            bool hasHead = false;
            foreach (CastSlot slot in def.Cast)
            {
                string where = "cast." + slot.Role;
                if (!Naming.IsSnakeId(slot.Role)) Err(where + ": role must be snake_case");
                if ((slot.From == null) == (slot.Generate == null)) { Err(where + ": needs exactly one of 'from' or 'generate'"); continue; }
                if (slot.From == "settlement") Known(ResidentExists(slot.Pick), "resident role", slot.Pick, where);
                else if (slot.From != null)
                {
                    if (slot.From != "party" && slot.From != "household") Err(where + ": from must be 'party', 'household' or 'settlement'");
                    if (slot.Pick != "head" && slot.Pick != "any") Err(where + ": role must be 'head' or 'any'");
                    if (slot.Pick == "head") hasHead = true;
                }
                else
                {
                    Known(db.Vocab.Archetypes.Contains(slot.Generate), "archetype", slot.Generate, where);
                    if (slot.AgeMin < 0 || slot.AgeMax < slot.AgeMin) Err(where + ": needs 0 <= age_min <= age_max");
                    if (slot.MarksAny.Count > 0) Err(where + ": marks_any only applies to existing people");
                }
                foreach (string t in slot.Traits) Known(db.Traits.ContainsKey(t), "trait", t, where);
                foreach (string m in slot.MarksAny) Known(db.Marks.ContainsKey(m), "mark", m, where);
            }
            if (!hasHead) Err("an event must cast the head of household ({ from: party, role: head })");

            if (def.Beats.Count == 0) { Err("an event needs at least one beat"); return; }
            var beatIds = new HashSet<string>();
            foreach (EventBeat b in def.Beats) if (!beatIds.Add(b.Id)) Err("duplicate beat id '" + b.Id + "'");

            var kinds = new HashSet<string>();
            var reached = new HashSet<string> { def.Beats[0].Id };
            var queue = new Queue<EventBeat>();
            queue.Enqueue(def.Beats[0]);

            void CheckText(string text, string where)
            {
                EventText.Replace(text, (role, prop) =>
                {
                    if (def.FindSlot(role) == null) Err(where + ": text uses {" + role + "...} but there is no such cast role");
                    else if (Array.IndexOf(EventText.Props, prop) < 0) Err(where + ": unknown token {" + role + "." + prop + "} (known: " + string.Join(", ", EventText.Props) + ")");
                    return "";
                });
            }
            void Role(string role, string where) { if (role != null && def.FindSlot(role) == null) Err(where + ": no cast role '" + role + "'"); }

            void CheckOutcome(EventOutcome o, string where)
            {
                foreach (string role in o.Relationship.Keys) Role(role, where + " relationship");
                foreach (string f in o.Reputation.Keys) Known(db.Factions.ContainsKey(f), "faction", f, where + " reputation");
                foreach (string i in o.Items.Keys) Known(db.Items.ContainsKey(i), "item", i, where + " item");
                if (o.LootTable != null) Known(db.LootTables.ContainsKey(o.LootTable), "loot table", o.LootTable, where);
                if (o.Ambush != null) Known(db.Vocab.Encounters.Contains(o.Ambush), "encounter", o.Ambush, where + " ambush");
                if (o.Recruit != null)
                {
                    Role(o.Recruit, where + " recruit");
                    CastSlot slot = def.FindSlot(o.Recruit);
                    if (slot != null && slot.Generate == null) Err(where + " recruit: only a generated person can be recruited");
                }
                if (o.Chronicle != null) CheckText(o.Chronicle, where + " chronicle");
                if (o.Goto != null && !beatIds.Contains(o.Goto)) Err(where + ": goto unknown beat '" + o.Goto + "'");
                if (o.Goto != null && beatIds.Contains(o.Goto) && reached.Add(o.Goto)) queue.Enqueue(def.FindBeat(o.Goto));
                foreach (string f in o.FlagSet) if (!Naming.IsSnakeId(f)) Err(where + ": flag '" + f + "' must be snake_case");
                foreach (string f in o.FlagClear) if (!Naming.IsSnakeId(f)) Err(where + ": flag '" + f + "' must be snake_case");
                if (o.Trait != null)
                {
                    Role(o.Trait.Target, where + " trait");
                    string id = o.Trait.Add ?? o.Trait.Remove;
                    if (id != null) Known(db.Traits.ContainsKey(id), "trait", id, where);
                }
                if (o.Mark != null)
                {
                    Role(o.Mark.Target, where + " mark");
                    MarkDef mark = db.FindMark(o.Mark.DefId);
                    if (mark == null) Err(where + ": no mark '" + o.Mark.DefId + "'");
                    else
                    {
                        if (mark.Category == MarkCategory.Wound) Err(where + ": use 'hit' to cause a wound, not 'mark'");
                        if (o.Mark.Region.HasValue && !mark.Regions.Contains(o.Mark.Region.Value)) Err(where + ": mark '" + mark.Id + "' cannot be on " + Naming.ToSnake(o.Mark.Region.Value));
                        if (o.Mark.Cause == null && mark.OriginText != null && OriginText.RequiresCause(mark.OriginText))
                            Err(where + ": mark '" + mark.Id + "' needs a 'cause' (every mark is named by where it came from)");
                    }
                    if (o.Mark.Severity < 1 || o.Mark.Severity > 3) Err(where + ": mark severity must be 1..3");
                    if (o.Mark.Cause != null) CheckText(o.Mark.Cause, where + " mark cause");
                }
                if (o.Hit != null)
                {
                    Role(o.Hit.Target, where + " hit");
                    if (o.Hit.Severity < 1 || o.Hit.Severity > 3) Err(where + ": hit severity must be 1..3");
                    if (o.Hit.Cause != null) CheckText(o.Hit.Cause, where + " hit cause");
                }
                if (o.Grudge != null)
                {
                    Known(db.Factions.ContainsKey(o.Grudge.FactionId ?? ""), "faction", o.Grudge.FactionId, where + " grudge");
                    if (o.Grudge.Reason != null) CheckText(o.Grudge.Reason, where + " grudge reason");
                }
                if (o.SettleGrudge != null) Known(db.Factions.ContainsKey(o.SettleGrudge), "faction", o.SettleGrudge, where + " settle_grudge");
                if (o.Memory != null)
                {
                    Role(o.Memory.Target, where + " memory");
                    if (o.Memory.Text != null) CheckText(o.Memory.Text, where + " memory");
                }
                if (o.Damage != null) Known(db.Structures.ContainsKey(o.Damage), "structure", o.Damage, where + " damage");
                if (o.Settlement.Count > 0 && db.Settlements.Count == 0) Err(where + ": settlement outcome but no settlement is defined");
                foreach (ChanceBranch b in o.Chance) CheckOutcome(b.Outcome, where + " chance");
            }

            while (queue.Count > 0)
            {
                EventBeat beat = queue.Dequeue();
                string bw = "beat '" + beat.Id + "'";
                if (beat.Text != null) CheckText(beat.Text, bw);
                if (beat.Choices.Count == 0) Err(bw + ": needs at least one choice");
                bool anyUnconditional = false;
                foreach (EventChoice choice in beat.Choices)
                {
                    string cw = bw + " choice '" + choice.Label + "'";
                    if (choice.Requires == null) anyUnconditional = true;
                    else
                    {
                        Role(choice.Requires.Who, cw + " requires");
                        foreach (string i in choice.Requires.ItemAny) Known(db.Items.ContainsKey(i), "item", i, cw + " requires");
                        foreach (string t in choice.Requires.TraitAny) Known(db.Traits.ContainsKey(t), "trait", t, cw + " requires");
                        foreach (string m in choice.Requires.MarkAny) Known(db.Marks.ContainsKey(m), "mark", m, cw + " requires");
                        foreach (string s in choice.Requires.StatMin.Keys) Known(db.Vocab.Stats.ContainsKey(s), "stat", s, cw + " requires");
                    }
                    choice.Outcome.CollectKinds(kinds);
                    CheckOutcome(choice.Outcome, cw);
                }
                if (!anyUnconditional) Err(bw + ": at least one choice must have no 'requires', so the player is never stuck");
            }
            foreach (EventBeat b in def.Beats)
                if (!reached.Contains(b.Id)) Err("beat '" + b.Id + "' can never be reached");

            kinds.Remove("goto");
            kinds.Remove("chance");
            if (kinds.Count == 0) Err("an event must change something (mark, item, relationship, reputation, flag, ...); pure flavor text is not an event");
        }
    }
}

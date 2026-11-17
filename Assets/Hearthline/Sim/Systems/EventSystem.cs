using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Where and when the party is, as presentation reports it. The storyteller picks events that fit.</summary>
    public sealed class EventContext
    {
        public List<string> AreaTags = new List<string>();
        /// <summary>0 near home, rising in the Thin.</summary>
        public int ThinLevel;
        public TimeOfDay TimeOfDay = TimeOfDay.Day;
        public bool AtHome;
        /// <summary>0 at the homestead, 1 at the far edge of the region.</summary>
        public double DistanceFromHome;
        /// <summary>Used in the names of marks gained here: "at Miller's Ford". Optional.</summary>
        public string PlaceName;
        /// <summary>The settlement the party is in or nearest to. Null means the home settlement.</summary>
        public string SettlementId;
        /// <summary>Who is on the trip. Empty means the head alone. The head is always included.</summary>
        public List<int> PartyIds = new List<int>();
    }

    /// <summary>Pacing numbers for the storyteller. First guesses; rules, not state.</summary>
    public sealed class StorytellerRules
    {
        public double BudgetPerTick = 1, BudgetPerThinLevel = 0.5, BudgetPerDistance = 1;
        /// <summary>Budget needed before anything can fire.</summary>
        public double Threshold = 3;
        /// <summary>Chance per tick to fire once over the threshold, so timing is not mechanical.</summary>
        public double FireChance = 0.5;
        /// <summary>After any event the budget restarts this far below zero, so events never come back to back.</summary>
        public double QuietAfterEvent = 1;
        /// <summary>Extra debt per point of intensity above 1. Big events buy a longer quiet.</summary>
        public double CostPerIntensity = 2;
        /// <summary>Weight multiplier for events tagged "danger" while someone in the party is hurt.</summary>
        public double DangerWeightWhenHurt = 0.3;
        public const string DangerTag = "danger";
    }

    public sealed class EventChoiceView
    {
        public int Index; public string Label; public bool Available;
        /// <summary>Why the choice is greyed out. Null when available.</summary>
        public string UnavailableReason;
    }

    public sealed class ActiveEventView
    {
        public string EventId; public string BeatId; public string Text;
        public List<EventChoiceView> Choices = new List<EventChoiceView>();
        /// <summary>Cast role -> character id.</summary>
        public Dictionary<string, int> Cast = new Dictionary<string, int>();
    }

    /// <summary>Casting, running and resolving content events, and the storyteller that paces them.</summary>
    internal static class EventSystem
    {
        // ---- Starting ------------------------------------------------------------------------------

        private static List<Character> Party(SimContext ctx, EventContext where)
        {
            var party = new List<Character>();
            Character head = ctx.State.Household.HeadId.HasValue ? ctx.FindCharacter(ctx.State.Household.HeadId.Value) : null;
            if (head != null) party.Add(head);
            // On a trip the party is whoever set out, unless presentation says otherwise.
            List<int> ids = where.PartyIds.Count == 0 && ctx.State.Trip != null ? ctx.State.Trip.PartyIds : where.PartyIds;
            foreach (int id in ids)
            {
                Character c = ctx.FindCharacter(id);
                if (c != null && c.Alive && !party.Contains(c) && LifeSystem.InHousehold(ctx, c)) party.Add(c);
            }
            return party;
        }

        /// <summary>Picks existing people for every non-generated role. Null if a role cannot be filled.</summary>
        private static Dictionary<string, Character> CastExisting(SimContext ctx, EventDef def, EventContext where)
        {
            var cast = new Dictionary<string, Character>();
            List<Character> party = Party(ctx, where);
            SettlementState town = WorldSystem.Resolve(ctx.State, ctx.Content, where.SettlementId);
            foreach (CastSlot slot in def.Cast)
            {
                if (slot.Generate != null) continue;
                var candidates = new List<Character>();
                if (slot.From == "settlement")
                {
                    Character resident = town == null ? null : ctx.State.Characters.Find(c => c.Alive && c.SettlementId == town.Id && c.Role == slot.Pick);
                    if (resident == null) return null;
                    cast[slot.Role] = resident;
                    continue;
                }
                IEnumerable<Character> pool = slot.From == "party" ? party : (IEnumerable<Character>)ctx.State.Characters;
                foreach (Character c in pool)
                {
                    if (!c.Alive || c.Downed || cast.ContainsValue(c) || !LifeSystem.InHousehold(ctx, c)) continue;
                    if (slot.Pick == "head" && c.Id != ctx.State.Household.HeadId) continue;
                    if (slot.Traits.Count > 0 && !slot.Traits.Exists(c.Traits.Contains)) continue;
                    if (slot.MarksAny.Count > 0 && !c.Marks.Exists(m => slot.MarksAny.Contains(m.DefId))) continue;
                    candidates.Add(c);
                }
                if (candidates.Count == 0) return null;
                cast[slot.Role] = candidates.Count == 1 ? candidates[0] : candidates[ctx.Rng.NextInt(0, candidates.Count)];
            }
            return cast;
        }

        private static bool WhenHolds(SimContext ctx, EventDef def, EventContext where)
        {
            EventWhen w = def.When;
            if (w.AreaTagsAny.Count > 0 && !w.AreaTagsAny.Exists(where.AreaTags.Contains)) return false;
            if (!w.AreaTagsAll.TrueForAll(where.AreaTags.Contains)) return false;
            if (w.ThinLevelMin.HasValue && where.ThinLevel < w.ThinLevelMin.Value) return false;
            if (w.ThinLevelMax.HasValue && where.ThinLevel > w.ThinLevelMax.Value) return false;
            if (w.TimeOfDay.Count > 0 && !w.TimeOfDay.Contains(where.TimeOfDay)) return false;
            if (w.AtHome.HasValue && w.AtHome.Value != where.AtHome) return false;
            if (!w.FlagsAll.TrueForAll(ctx.State.Flags.Contains)) return false;
            if (w.FlagsNone.Exists(ctx.State.Flags.Contains)) return false;
            IDictionary<string, int> rep = ctx.State.Household.Reputation;
            foreach (var kv in w.ReputationMin) { rep.TryGetValue(kv.Key, out int r); if (r < kv.Value) return false; }
            foreach (var kv in w.ReputationMax) { rep.TryGetValue(kv.Key, out int r); if (r > kv.Value) return false; }
            if (w.GrudgeAny.Count > 0 && !w.GrudgeAny.Exists(f => WorldSystem.HasGrudge(ctx.State, f))) return false;
            if (w.MoraleMin.HasValue && ctx.State.Household.Morale < w.MoraleMin.Value) return false;
            if (w.MoraleMax.HasValue && ctx.State.Household.Morale > w.MoraleMax.Value) return false;
            foreach (var kv in w.HomeMin) if (HomeSystem.Stat(ctx, kv.Key) < kv.Value) return false;
            foreach (var kv in w.HomeMax) if (HomeSystem.Stat(ctx, kv.Key) > kv.Value) return false;
            if (w.ServicesAny.Count > 0 || w.SettlementMin.Count > 0 || w.SettlementMax.Count > 0)
            {
                SettlementState town = WorldSystem.Resolve(ctx.State, ctx.Content, where.SettlementId);
                if (town == null) return false;
                if (w.ServicesAny.Count > 0 && !w.ServicesAny.Exists(town.OpenServices.Contains)) return false;
                foreach (var kv in w.SettlementMin) if (town.Values[kv.Key] < kv.Value) return false;
                foreach (var kv in w.SettlementMax) if (town.Values[kv.Key] > kv.Value) return false;
            }
            if (ctx.State.Storyteller.LastFiredDay.TryGetValue(def.Id, out int last) && ctx.State.Day < last + def.CooldownDays) return false;
            return true;
        }

        /// <summary>Returns why the event cannot start, or null once it has.</summary>
        public static string Start(SimContext ctx, EventDef def, EventContext where)
        {
            if (ctx.State.ActiveEvent != null) return "an event is already in progress";
            if (!ctx.State.Household.HeadId.HasValue) return "there is no head of household";
            Dictionary<string, Character> cast = CastExisting(ctx, def, where);
            if (cast == null) return "nobody fits the cast of '" + def.Id + "'";

            var active = new ActiveEvent
            {
                EventId = def.Id, BeatId = def.Beats[0].Id, PlaceName = where.PlaceName,
                SettlementId = WorldSystem.Resolve(ctx.State, ctx.Content, where.SettlementId)?.Id,
            };
            foreach (CastSlot slot in def.Cast)
            {
                Character c;
                if (slot.Generate == null) c = cast[slot.Role];
                else
                {
                    c = new Character
                    {
                        Id = ctx.State.NextCharacterId++,
                        GivenName = LifeSystem.PickGivenName(ctx),
                        Surname = ctx.Content.Surnames.Count > 0 ? ctx.Content.Surnames[ctx.Rng.NextInt(0, ctx.Content.Surnames.Count)] : "",
                        BirthDay = ctx.State.Day - ctx.Rng.NextInt(slot.AgeMin, slot.AgeMax + 1) * GameDate.DaysPerYear,
                        Archetype = slot.Generate,
                    };
                    c.Traits.AddRange(slot.Traits);
                    ctx.State.Characters.Add(c);
                }
                active.Cast[slot.Role] = c.Id;
            }
            ctx.State.ActiveEvent = active;
            ctx.State.Storyteller.LastFiredDay[def.Id] = ctx.State.Day;
            ctx.Emit(new EventStarted { Day = ctx.State.Day, EventId = def.Id });
            ctx.Emit(new EventBeatEntered { Day = ctx.State.Day, EventId = def.Id, BeatId = active.BeatId });
            return null;
        }

        /// <summary>One storyteller opportunity. May start an event; usually does not.</summary>
        public static void StorytellerTick(SimContext ctx, EventContext where)
        {
            StorytellerRules rules = ctx.Storyteller;
            StorytellerState st = ctx.State.Storyteller;
            st.Budget += rules.BudgetPerTick + rules.BudgetPerThinLevel * Math.Max(0, where.ThinLevel)
                + rules.BudgetPerDistance * Math.Max(0, Math.Min(1, where.DistanceFromHome));
            st.Budget = Math.Min(st.Budget, rules.Threshold * 2);
            if (st.Budget < rules.Threshold || !ctx.Rng.Chance(rules.FireChance)) return;

            bool hurt = Party(ctx, where).Exists(c => c.Health < LifeRules.MaxHealth);
            bool loaded = TripSystem.Overloaded(ctx);
            var ids = new List<string>(ctx.Content.Events.Keys);
            ids.Sort(StringComparer.Ordinal);
            var eligible = new List<EventDef>();
            var weights = new List<double>();
            foreach (string id in ids)
            {
                EventDef def = ctx.Content.Events[id];
                if (!WhenHolds(ctx, def, where)) continue;
                eligible.Add(def);
                bool danger = def.Tags.Contains(StorytellerRules.DangerTag);
                weights.Add(def.Weight * (hurt && danger ? rules.DangerWeightWhenHurt : 1) * (loaded && danger ? ctx.Trips.DangerWeightWhenOverloaded : 1));
            }
            // Casting can still fail (nobody with the needed trait); try the others before giving up.
            while (eligible.Count > 0)
            {
                int i = ctx.Rng.PickWeighted(weights);
                if (Start(ctx, eligible[i], where) == null)
                {
                    // Unspent budget is not banked: a long quiet does not earn a burst of events.
                    st.Budget = -rules.QuietAfterEvent - rules.CostPerIntensity * (eligible[i].Intensity - 1);
                    return;
                }
                eligible.RemoveAt(i);
                weights.RemoveAt(i);
            }
        }

        // ---- Running -------------------------------------------------------------------------------

        private static string Render(SimContext ctx, ActiveEvent active, string text)
        {
            return EventText.Replace(text, (role, prop) =>
            {
                if (prop == null || !active.Cast.TryGetValue(role, out int id)) return null;
                Character c = ctx.FindCharacter(id);
                if (c == null) return null;
                switch (prop)
                {
                    case "name": return c.GivenName;
                    case "surname": return c.Surname;
                    case "full_name": return (c.GivenName + " " + c.Surname).Trim();
                    default: return null;
                }
            });
        }

        private static Character Who(SimContext ctx, ActiveEvent active, string role)
        {
            if (role != null) return active.Cast.TryGetValue(role, out int id) ? ctx.FindCharacter(id) : null;
            return ctx.State.Household.HeadId.HasValue ? ctx.FindCharacter(ctx.State.Household.HeadId.Value) : null;
        }

        private static string Unmet(SimContext ctx, ActiveEvent active, ChoiceRequires r)
        {
            if (r == null) return null;
            Character who = Who(ctx, active, r.Who);
            if (who == null || !who.Alive) return "nobody to do it";
            IDictionary<string, int> inv = TripSystem.CurrentInventory(ctx.State);
            if (r.ItemAny.Count > 0 && !r.ItemAny.Exists(i => inv.TryGetValue(i, out int n) && n > 0))
                return "needs " + string.Join(" or ", r.ItemAny);
            if (r.TraitAny.Count > 0 && !r.TraitAny.Exists(who.Traits.Contains)) return "needs " + string.Join(" or ", r.TraitAny);
            if (r.MarkAny.Count > 0 && !who.Marks.Exists(m => r.MarkAny.Contains(m.DefId))) return "needs " + string.Join(" or ", r.MarkAny);
            foreach (var kv in r.StatMin)
                if (StatSystem.GetStat(ctx.Content, who, kv.Key) < kv.Value) return "needs " + kv.Key + " " + kv.Value;
            foreach (string flag in r.FlagsAll) if (!ctx.State.Flags.Contains(flag)) return "not yet";
            return null;
        }

        public static ActiveEventView View(SimContext ctx)
        {
            ActiveEvent active = ctx.State.ActiveEvent;
            EventBeat beat = active == null ? null : ctx.Content.Events.TryGetValue(active.EventId, out EventDef def) ? def.FindBeat(active.BeatId) : null;
            if (beat == null) return null;
            var view = new ActiveEventView { EventId = active.EventId, BeatId = beat.Id, Text = Render(ctx, active, beat.Text) };
            foreach (var kv in active.Cast) view.Cast[kv.Key] = kv.Value;
            for (int i = 0; i < beat.Choices.Count; i++)
            {
                string reason = Unmet(ctx, active, beat.Choices[i].Requires);
                view.Choices.Add(new EventChoiceView { Index = i, Label = Render(ctx, active, beat.Choices[i].Label), Available = reason == null, UnavailableReason = reason });
            }
            return view;
        }

        public static string Choose(SimContext ctx, int index)
        {
            ActiveEvent active = ctx.State.ActiveEvent;
            if (active == null) return "there is no event in progress";
            EventBeat beat = ctx.Content.Events.TryGetValue(active.EventId, out EventDef def) ? def.FindBeat(active.BeatId) : null;
            if (beat == null)
            {
                // The save refers to an event content no longer has. Drop it rather than trap the player.
                ctx.State.ActiveEvent = null;
                ctx.Emit(new EventEnded { Day = ctx.State.Day, EventId = active.EventId });
                return null;
            }
            if (index < 0 || index >= beat.Choices.Count) return "there is no choice " + index;
            string unmet = Unmet(ctx, active, beat.Choices[index].Requires);
            if (unmet != null) return "that choice is not available: " + unmet;

            int day = ctx.State.Day;
            ctx.Emit(new EventChoiceMade { Day = day, EventId = def.Id, BeatId = beat.Id, ChoiceIndex = index });
            string next = Apply(ctx, def, active, beat.Choices[index].Outcome);
            if (next != null && def.FindBeat(next) != null)
            {
                active.BeatId = next;
                ctx.Emit(new EventBeatEntered { Day = day, EventId = def.Id, BeatId = next });
            }
            else
            {
                ctx.State.ActiveEvent = null;
                ctx.Emit(new EventEnded { Day = day, EventId = def.Id });
            }
            return null;
        }

        // ---- Outcomes ------------------------------------------------------------------------------

        private static IEnumerable<KeyValuePair<string, int>> Ordered(Dictionary<string, int> map)
        {
            var keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string k in keys) yield return new KeyValuePair<string, int>(k, map[k]);
        }

        /// <summary>Applies an outcome and returns the beat to go to next, or null to end the event.</summary>
        private static string Apply(SimContext ctx, EventDef def, ActiveEvent active, EventOutcome o)
        {
            int day = ctx.State.Day;
            Character head = Who(ctx, active, null);

            foreach (var kv in Ordered(o.Items)) ChangeItem(ctx, kv.Key, kv.Value);
            if (o.LootTable != null && ctx.Content.LootTables.TryGetValue(o.LootTable, out LootTableDef table))
            {
                var weights = table.Entries.ConvertAll(e => e.Weight);
                for (int i = 0; i < table.Rolls; i++)
                {
                    LootEntry entry = table.Entries[ctx.Rng.PickWeighted(weights)];
                    ChangeItem(ctx, entry.ItemId, ctx.Rng.NextInt(entry.CountMin, entry.CountMax + 1));
                }
            }
            foreach (var kv in Ordered(o.Relationship))
            {
                Character c = Who(ctx, active, kv.Key);
                if (c == null || head == null || c.Id == head.Id) continue;
                c.Opinions.TryGetValue(head.Id, out int old);
                int value = Math.Max(-100, Math.Min(100, old + kv.Value));
                c.Opinions[head.Id] = value;
                ctx.Emit(new OpinionChanged { Day = day, CharacterId = c.Id, AboutCharacterId = head.Id, Delta = kv.Value, NewValue = value });
            }
            foreach (var kv in Ordered(o.Reputation)) WorldSystem.ChangeReputation(ctx, kv.Key, kv.Value);
            if (o.Grudge != null) WorldSystem.AddGrudge(ctx, o.Grudge.FactionId, Render(ctx, active, o.Grudge.Reason));
            if (o.SettleGrudge != null) WorldSystem.SettleGrudge(ctx, o.SettleGrudge);
            if (o.Settlement.Count > 0)
            {
                SettlementState town = WorldSystem.Find(ctx.State, active.SettlementId);
                if (town != null)
                    foreach (SettlementValue v in (SettlementValue[])Enum.GetValues(typeof(SettlementValue)))
                        if (o.Settlement.TryGetValue(v, out int push)) WorldSystem.Influence(ctx, town, v, push);
            }
            if (o.GearGiven != null && ctx.Content.Items.TryGetValue(o.GearGiven.DefId, out ItemDef gearDef) && gearDef.Equip != null)
            {
                Character to = Who(ctx, active, o.GearGiven.To);
                if (to != null && to.Alive)
                {
                    GearItem made = GearSystem.Create(ctx, gearDef, to, "First carried by");
                    if (o.GearGiven.Note != null) GearSystem.Note(ctx, made, Render(ctx, active, o.GearGiven.Note));
                }
            }
            if (o.Morale != 0) HomeSystem.ChangeMorale(ctx, o.Morale);
            if (o.Damage != null) HomeSystem.Damage(ctx, o.Damage);
            if (o.Memory != null)
            {
                Character who = Who(ctx, active, o.Memory.Target);
                if (who != null && who.Alive) WorldSystem.Remember(ctx, who, Render(ctx, active, o.Memory.Text));
            }

            if (o.Mark != null && ctx.Rng.Chance(o.Mark.Chance))
            {
                Character target = Who(ctx, active, o.Mark.Target);
                MarkDef mark = ctx.Content.FindMark(o.Mark.DefId);
                if (target != null && target.Alive && mark != null)
                {
                    BodyRegion region = o.Mark.Region ?? (mark.Regions.Count == 1 ? mark.Regions[0] : mark.Regions[ctx.Rng.NextInt(0, mark.Regions.Count)]);
                    var origin = new MarkOrigin { EventId = def.Id, Cause = Render(ctx, active, o.Mark.Cause), Place = active.PlaceName };
                    if (MarkSystem.CheckCanAdd(mark, region, o.Mark.Severity, origin) == null)
                        MarkSystem.Add(ctx, target, mark, region, o.Mark.Severity, origin);
                }
            }
            if (o.Hit != null)
            {
                Character target = Who(ctx, active, o.Hit.Target);
                if (target != null && target.Alive && !target.Downed)
                {
                    var regions = (BodyRegion[])Enum.GetValues(typeof(BodyRegion));
                    BodyRegion region = o.Hit.Region ?? regions[ctx.Rng.NextInt(0, regions.Length)];
                    MarkDef wound = MarkSystem.FindWoundFor(ctx.Content, o.Hit.Damage, region);
                    var origin = new MarkOrigin { EventId = def.Id, Cause = Render(ctx, active, o.Hit.Cause), Place = active.PlaceName };
                    if (wound != null && MarkSystem.CheckCanAdd(wound, region, o.Hit.Severity, origin) == null)
                    {
                        MarkSystem.Add(ctx, target, wound, region, o.Hit.Severity, origin);
                        LifeSystem.TakeDamage(ctx, target, o.Hit.Severity);
                    }
                }
            }
            if (o.Trait != null)
            {
                Character target = Who(ctx, active, o.Trait.Target);
                string id = o.Trait.Add ?? o.Trait.Remove;
                bool gain = o.Trait.Add != null;
                if (target != null && target.Alive && target.Traits.Contains(id) != gain)
                {
                    if (gain) target.Traits.Add(id); else target.Traits.Remove(id);
                    ctx.Emit(new TraitChanged { Day = day, CharacterId = target.Id, TraitId = id, Gained = gain });
                }
            }
            foreach (string flag in o.FlagSet) SetFlag(ctx, flag, true);
            foreach (string flag in o.FlagClear) SetFlag(ctx, flag, false);

            if (o.Recruit != null)
            {
                Character c = Who(ctx, active, o.Recruit);
                if (c != null && c.Alive && !LifeSystem.InHousehold(ctx, c))
                {
                    c.Bond = HouseholdBond.Sworn;
                    ctx.State.Household.MemberIds.Add(c.Id);
                    ctx.Emit(new CharacterJoined { Day = day, CharacterId = c.Id });
                    ctx.Chronicle(c, ChronicleKind.Joined, (c.GivenName + " " + c.Surname).Trim() + " joined the household.");
                }
            }
            if (o.Chronicle != null && head != null) ctx.Chronicle(head, ChronicleKind.Deed, Render(ctx, active, o.Chronicle));
            if (o.Ambush != null)
                ctx.Emit(new AmbushRequested { Day = day, EncounterId = o.Ambush, EventId = def.Id, PlaceName = active.PlaceName, LeaderId = CombatSystem.CastLeader(ctx, o.Ambush, active) });

            string next = o.Goto;
            if (o.Chance.Count > 0)
            {
                double roll = ctx.Rng.NextDouble();
                foreach (ChanceBranch branch in o.Chance)
                {
                    roll -= branch.P;
                    if (roll >= 0) continue;
                    next = Apply(ctx, def, active, branch.Outcome) ?? next;
                    break;
                }
            }
            return next;
        }

        /// <summary>
        /// Adds or removes items where the head is: the pack on a trip, the stores at home.
        /// Things that happen at the homestead itself pass <paramref name="inStores"/>.
        /// </summary>
        public static void ChangeItem(SimContext ctx, string itemId, int delta, bool inStores = false)
        {
            IDictionary<string, int> inv = inStores ? ctx.State.Household.Inventory : TripSystem.CurrentInventory(ctx.State);
            inv.TryGetValue(itemId, out int old);
            int count = Math.Max(0, old + delta);
            if (count == old) return;
            if (count == 0) inv.Remove(itemId); else inv[itemId] = count;
            ctx.Emit(new InventoryChanged { Day = ctx.State.Day, ItemId = itemId, Delta = count - old, NewCount = count });
        }

        public static void SetFlag(SimContext ctx, string flag, bool set)
        {
            List<string> flags = ctx.State.Flags;
            if (flags.Contains(flag) == set) return;
            if (set) { flags.Add(flag); flags.Sort(StringComparer.Ordinal); } else flags.Remove(flag);
            ctx.Emit(new FlagChanged { Day = ctx.State.Day, Flag = flag, Set = set });
        }
    }
}

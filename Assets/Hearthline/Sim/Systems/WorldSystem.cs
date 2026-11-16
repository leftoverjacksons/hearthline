using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Numbers for how the world moves between seasons. First guesses; rules, not state.</summary>
    public sealed class WorldRules
    {
        public const int MinValue = 0, MaxValue = 100;
        /// <summary>The most a settlement value can move in one season, however much the family did.</summary>
        public int MaxChangePerSeason = 15;
        /// <summary>How far each value drifts toward its baseline per season when nothing pushes it.</summary>
        public int DriftPerSeason = 1;
        /// <summary>A service that is open stays open until a value is this far past its threshold, so it does not flicker.</summary>
        public int ServiceHysteresis = 5;
        /// <summary>Reputation moves this far toward zero each season. People forget, slowly.</summary>
        public int ReputationDecayPerSeason = 1;
        /// <summary>While a faction holds a grudge, reputation with it cannot rise above this, and does not decay.</summary>
        public int GrudgeCeiling = -25;
        /// <summary>Strangeness the home settlement gains per season while a visibly Changed person lives in the household.</summary>
        public int ChangedStrangenessPerSeason = 1;
        public int MaxMemories = 8;
        public const string ChangedTag = "changed";
    }

    /// <summary>Settlements, their people, reputation over time, and grudges.</summary>
    internal static class WorldSystem
    {
        public static SettlementDef HomeDef(ContentDatabase content)
        {
            SettlementDef home = null;
            foreach (SettlementDef def in content.Settlements.Values)
                if (def.Home && (home == null || string.CompareOrdinal(def.Id, home.Id) < 0)) home = def;
            return home;
        }

        public static SettlementState Find(SimState state, string id)
        {
            foreach (SettlementState s in state.Settlements) if (s.Id == id) return s;
            return null;
        }

        /// <summary>The settlement an event or condition refers to: the one named, else the home settlement.</summary>
        public static SettlementState Resolve(SimState state, ContentDatabase content, string id)
        {
            return Find(state, id ?? HomeDef(content)?.Id);
        }

        /// <summary>Creates state for any settlement content defines that the state does not have yet.</summary>
        public static void EnsureSettlements(SimState state, ContentDatabase content, SimRandom rng)
        {
            var ids = new List<string>(content.Settlements.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (Find(state, id) != null) continue;
                SettlementDef def = content.Settlements[id];
                var s = new SettlementState { Id = id };
                foreach (SettlementValue v in (SettlementValue[])Enum.GetValues(typeof(SettlementValue)))
                {
                    s.Values[v] = def.Start.TryGetValue(v, out int start) ? start : 0;
                    s.Pending[v] = 0;
                }
                foreach (ServiceDef service in def.Services)
                    if (Meets(s, service, 0)) s.OpenServices.Add(service.Id);
                state.Settlements.Add(s);
                foreach (ResidentSlot slot in def.Residents) CreateResident(state, content, rng, def, slot, null);
            }
        }

        public static Character CreateResident(SimState state, ContentDatabase content, SimRandom rng, SettlementDef def, ResidentSlot slot, Character parent)
        {
            var free = new List<string>();
            foreach (string name in content.GivenNames)
                if (!state.Characters.Exists(c => c.Alive && c.GivenName == name)) free.Add(name);
            List<string> names = free.Count > 0 ? free : content.GivenNames;
            int age = parent == null ? rng.NextInt(slot.AgeMin, slot.AgeMax + 1) : rng.NextInt(LifeRules.AdultAge, 31);
            var c = new Character
            {
                Id = state.NextCharacterId++,
                GivenName = names.Count > 0 ? names[rng.NextInt(0, names.Count)] : "Someone",
                Surname = parent != null ? parent.Surname : content.Surnames.Count > 0 ? content.Surnames[rng.NextInt(0, content.Surnames.Count)] : "",
                BirthDay = state.Day - age * GameDate.DaysPerYear,
                Archetype = slot.Archetype,
                SettlementId = def.Id,
                Role = slot.Role,
            };
            if (parent != null) c.ParentIds.Add(parent.Id);
            state.Characters.Add(c);
            return c;
        }

        /// <summary>A role-holder died: their child takes over the work, and inherits what the parent remembered.</summary>
        public static void ReplaceResident(SimContext ctx, Character dead)
        {
            if (dead.SettlementId == null || dead.Role == null) return;
            if (!ctx.Content.Settlements.TryGetValue(dead.SettlementId, out SettlementDef def)) return;
            ResidentSlot slot = def.Residents.Find(r => r.Role == dead.Role);
            if (slot == null) return;
            Character heir = CreateResident(ctx.State, ctx.Content, ctx.Rng, def, slot, dead);
            heir.Memories.AddRange(dead.Memories);
            ctx.Emit(new ResidentSucceeded { Day = ctx.State.Day, SettlementId = def.Id, Role = slot.Role, PreviousId = dead.Id, NewId = heir.Id });
        }

        // ---- Influence -----------------------------------------------------------------------------

        /// <summary>Records something the family did. It takes effect when the season turns.</summary>
        public static void Influence(SimContext ctx, SettlementState s, SettlementValue value, int delta)
        {
            s.Pending[value] = s.Pending[value] + delta;
            ctx.Emit(new SettlementInfluenced { Day = ctx.State.Day, SettlementId = s.Id, Value = value, Delta = delta });
        }

        private static bool Meets(SettlementState s, ServiceDef service, int slack)
        {
            foreach (var kv in service.Min) if (s.Values[kv.Key] < kv.Value - slack) return false;
            foreach (var kv in service.Max) if (s.Values[kv.Key] > kv.Value + slack) return false;
            return true;
        }

        public static void RunSeason(SimContext ctx)
        {
            WorldRules rules = ctx.World;
            SimState state = ctx.State;
            int day = state.Day;

            // Visibly Changed people in the household make the home town stranger.
            SettlementState home = Resolve(state, ctx.Content, null);
            if (home != null)
                foreach (Character c in state.Characters)
                    if (c.Alive && LifeSystem.InHousehold(ctx, c) && StatSystem.Collect(ctx.Content, c, EffectKind.Reaction).Contains(WorldRules.ChangedTag))
                    {
                        home.Pending[SettlementValue.Strangeness] += rules.ChangedStrangenessPerSeason;
                        break;
                    }

            foreach (SettlementState s in state.Settlements)
            {
                if (!ctx.Content.Settlements.TryGetValue(s.Id, out SettlementDef def)) continue;
                foreach (SettlementValue v in (SettlementValue[])Enum.GetValues(typeof(SettlementValue)))
                {
                    int old = s.Values[v];
                    int change = Math.Max(-rules.MaxChangePerSeason, Math.Min(rules.MaxChangePerSeason, s.Pending[v]));
                    s.Pending[v] = 0;
                    if (change == 0 && def.Baseline.TryGetValue(v, out int baseline))
                        change = Math.Sign(baseline - old) * Math.Min(rules.DriftPerSeason, Math.Abs(baseline - old));
                    int now = Math.Max(WorldRules.MinValue, Math.Min(WorldRules.MaxValue, old + change));
                    if (now == old) continue;
                    s.Values[v] = now;
                    ctx.Emit(new SettlementValueChanged { Day = day, SettlementId = s.Id, Value = v, OldValue = old, NewValue = now });
                }
                foreach (ServiceDef service in def.Services)
                {
                    bool open = s.OpenServices.Contains(service.Id);
                    if (!open && Meets(s, service, 0))
                    {
                        s.OpenServices.Add(service.Id);
                        ctx.Emit(new ServiceOpened { Day = day, SettlementId = s.Id, ServiceId = service.Id });
                    }
                    else if (open && !Meets(s, service, rules.ServiceHysteresis))
                    {
                        s.OpenServices.Remove(service.Id);
                        ctx.Emit(new ServiceClosed { Day = day, SettlementId = s.Id, ServiceId = service.Id });
                    }
                }
            }

            // Reputation fades toward nothing, except where a grudge keeps it alive.
            var factions = new List<string>(state.Household.Reputation.Keys);
            factions.Sort(StringComparer.Ordinal);
            foreach (string faction in factions)
            {
                int value = state.Household.Reputation[faction];
                if (value == 0 || HasGrudge(state, faction)) continue;
                int now = value - Math.Sign(value) * Math.Min(rules.ReputationDecayPerSeason, Math.Abs(value));
                state.Household.Reputation[faction] = now;
                ctx.Emit(new ReputationChanged { Day = day, FactionId = faction, Delta = now - value, NewValue = now });
            }
        }

        // ---- Reputation and grudges ----------------------------------------------------------------

        public static bool HasGrudge(SimState state, string factionId) => state.Household.Grudges.Exists(g => g.FactionId == factionId);

        public static void ChangeReputation(SimContext ctx, string factionId, int delta)
        {
            Dictionary<string, int> rep = ctx.State.Household.Reputation;
            rep.TryGetValue(factionId, out int old);
            int value = Math.Max(-100, Math.Min(100, old + delta));
            if (HasGrudge(ctx.State, factionId)) value = Math.Min(value, ctx.World.GrudgeCeiling);
            if (value == old) return;
            rep[factionId] = value;
            ctx.Emit(new ReputationChanged { Day = ctx.State.Day, FactionId = factionId, Delta = value - old, NewValue = value });
        }

        public static void AddGrudge(SimContext ctx, string factionId, string reason)
        {
            if (HasGrudge(ctx.State, factionId)) return;
            ctx.State.Household.Grudges.Add(new Grudge { FactionId = factionId, Reason = reason, Day = ctx.State.Day });
            ctx.Emit(new GrudgeChanged { Day = ctx.State.Day, FactionId = factionId, Held = true, Reason = reason });
            ctx.State.Household.Reputation.TryGetValue(factionId, out int value);
            if (value > ctx.World.GrudgeCeiling) ChangeReputation(ctx, factionId, ctx.World.GrudgeCeiling - value);
        }

        public static void SettleGrudge(SimContext ctx, string factionId)
        {
            Grudge grudge = ctx.State.Household.Grudges.Find(g => g.FactionId == factionId);
            if (grudge == null) return;
            ctx.State.Household.Grudges.Remove(grudge);
            ctx.Emit(new GrudgeChanged { Day = ctx.State.Day, FactionId = factionId, Held = false, Reason = grudge.Reason });
        }

        public static void Remember(SimContext ctx, Character who, string text)
        {
            who.Memories.Add(new Memory { Day = ctx.State.Day, Text = text });
            while (who.Memories.Count > ctx.World.MaxMemories) who.Memories.RemoveAt(0);
        }
    }
}

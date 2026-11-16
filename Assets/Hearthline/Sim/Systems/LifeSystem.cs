using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Health and the downed state, aging, birth and inheritance, death, and succession.</summary>
    internal static class LifeSystem
    {
        public static int AgeOf(SimContext ctx, Character c) => GameDate.YearsBetween(c.BirthDay, c.DeathDay ?? ctx.State.Day);

        public static bool InHousehold(SimContext ctx, Character c) => ctx.State.Household.MemberIds.Contains(c.Id);

        // ---- Health --------------------------------------------------------------------------------

        public static void TakeDamage(SimContext ctx, Character c, int amount)
        {
            c.Health = Math.Max(0, c.Health - amount);
            if (c.Health > 0 || c.Downed) return;
            c.Downed = true;
            ctx.Emit(new CharacterDowned { Day = ctx.State.Day, CharacterId = c.Id });
        }

        public static void Revive(SimContext ctx, Character c)
        {
            c.Downed = false;
            c.Health = ctx.Life.HealthAfterRevive;
            ctx.Emit(new CharacterRevived { Day = ctx.State.Day, CharacterId = c.Id });
        }

        /// <summary>Nobody came. Either they die of their worst wound, or it costs them something for good.</summary>
        public static void ResolveDowned(SimContext ctx, Character c)
        {
            Mark worst = null;
            foreach (Mark m in c.Marks)
                if (m.Category == MarkCategory.Wound && (worst == null || m.Severity >= worst.Severity)) worst = m;

            if (ctx.Rng.Chance(ctx.Life.DownedDeathChance))
            {
                Kill(ctx, c, worst != null ? LowerFirst(worst.OriginText) : "left for dead");
                return;
            }
            if (worst != null) MarkSystem.ResolveWound(ctx, c, worst, WoundOutcomes.Loss, ctx.State.Day);
            Revive(ctx, c);
        }

        /// <summary>Once a day for each living person: the downed are resolved, the hale recover, the old are marked.</summary>
        public static void DailyTick(SimContext ctx, Character c)
        {
            if (!c.Alive) return;
            if (c.Downed)
            {
                ResolveDowned(ctx, c);
                if (!c.Alive) return;
            }
            else if (c.Health < LifeRules.MaxHealth)
            {
                bool open = false;
                foreach (Mark m in c.Marks)
                    if (m.Category == MarkCategory.Wound && (m.Stage == WoundStages.Fresh || m.Stage == WoundStages.Infected)) open = true;
                if (!open) c.Health++;
            }

            int age = AgeOf(ctx, c);
            foreach (MarkDef def in ctx.AgeMarks)
            {
                if (age < def.GainedAtAge.Value) continue;
                bool has = false;
                foreach (Mark m in c.Marks) if (m.DefId == def.Id) has = true;
                if (!has) MarkSystem.Add(ctx, c, def, def.Regions[0], 1, null);
            }
        }

        // ---- Household -----------------------------------------------------------------------------

        public static Character CreateCharacter(SimContext ctx, string givenName, string surname, int birthDay, HouseholdBond bond)
        {
            var c = new Character
            {
                Id = ctx.State.NextCharacterId++,
                GivenName = givenName,
                Surname = surname,
                BirthDay = birthDay,
                Bond = bond,
            };
            ctx.State.Characters.Add(c);
            ctx.State.Household.MemberIds.Add(c.Id);
            return c;
        }

        public static string PickGivenName(SimContext ctx)
        {
            var free = new List<string>();
            foreach (string name in ctx.Content.GivenNames)
            {
                bool taken = false;
                foreach (Character c in ctx.State.Characters) if (c.Alive && c.GivenName == name) taken = true;
                if (!taken) free.Add(name);
            }
            List<string> pool = free.Count > 0 ? free : ctx.Content.GivenNames;
            return pool.Count == 0 ? "Child" : pool[ctx.Rng.NextInt(0, pool.Count)];
        }

        /// <summary>Returns why these two cannot have a child together, or null.</summary>
        public static string CheckCanParent(SimContext ctx, Character a, Character b)
        {
            if (a.Id == b.Id) return "a child needs two parents";
            foreach (Character p in new[] { a, b })
            {
                if (!p.Alive) return p.GivenName + " is dead";
                if (!InHousehold(ctx, p)) return p.GivenName + " is not in the household";
                int age = AgeOf(ctx, p);
                if (age < LifeRules.AdultAge || age > ctx.Life.MaxParentAge)
                    return p.GivenName + " is not of an age to have children";
            }
            return null;
        }

        public static Character Birth(SimContext ctx, Character a, Character b, string givenName)
        {
            SimState state = ctx.State;
            Character child = CreateCharacter(ctx, givenName ?? PickGivenName(ctx), state.Household.Surname, state.Day, HouseholdBond.Blood);
            child.ParentIds.Add(a.Id);
            child.ParentIds.Add(b.Id);

            // Traits: each parent's trait passes on by its own chance; sometimes something new appears.
            foreach (Character parent in new[] { a, b })
                foreach (string traitId in parent.Traits)
                {
                    TraitDef trait = ctx.Content.FindTrait(traitId);
                    if (trait != null && !child.Traits.Contains(traitId) && ctx.Rng.Chance(trait.InheritChance)) child.Traits.Add(traitId);
                }
            if (ctx.Rng.Chance(ctx.Life.MutationChance))
            {
                var innate = new List<string>();
                foreach (TraitDef trait in ctx.Content.Traits.Values)
                    if (trait.Innate && !child.Traits.Contains(trait.Id)) innate.Add(trait.Id);
                innate.Sort(StringComparer.Ordinal);
                if (innate.Count > 0) child.Traits.Add(innate[ctx.Rng.NextInt(0, innate.Count)]);
            }

            ctx.Emit(new CharacterBorn { Day = state.Day, CharacterId = child.Id, ParentAId = a.Id, ParentBId = b.Id });
            ctx.Chronicle(child, ChronicleKind.Born, child.GivenName + " " + child.Surname + " was born to " + a.GivenName + " and " + b.GivenName + ".");

            // Marks: exposure can echo in a child; the Knack can surface, or be carried unseen.
            foreach (Character parent in new[] { a, b })
            {
                var heritable = new List<string>(parent.CarriedLatent);
                foreach (Mark m in parent.Marks) if (!heritable.Contains(m.DefId)) heritable.Add(m.DefId);
                foreach (string defId in heritable)
                {
                    MarkDef def = ctx.Content.FindMark(defId);
                    if (def == null || def.Heritable == Heritability.None) continue;
                    bool already = child.CarriedLatent.Contains(defId);
                    foreach (Mark m in child.Marks) if (m.DefId == defId) already = true;
                    if (already) continue;

                    bool latent = def.Heritable == Heritability.Latent;
                    if (ctx.Rng.Chance(latent ? ctx.Life.LatentManifestChance : ctx.Life.EchoChance))
                        MarkSystem.Add(ctx, child, def, def.Regions[0], 1, new MarkOrigin { Cause = parent.GivenName }, def.InheritedOriginText);
                    else if (latent && ctx.Rng.Chance(ctx.Life.LatentCarryChance))
                        child.CarriedLatent.Add(defId);
                }
            }
            return child;
        }

        // ---- Death and succession ------------------------------------------------------------------

        public static void Kill(SimContext ctx, Character c, string cause)
        {
            SimState state = ctx.State;
            bool wasHead = state.Household.HeadId == c.Id;
            c.Alive = false;
            c.Downed = false;
            c.DeathDay = state.Day;
            c.DeathCause = cause;
            if (c.PartnerId.HasValue)
            {
                Character partner = ctx.FindCharacter(c.PartnerId.Value);
                if (partner != null) partner.PartnerId = null;
            }
            ctx.Emit(new CharacterDied { Day = state.Day, CharacterId = c.Id, Cause = cause, WasHeadOfHousehold = wasHead });
            ctx.Chronicle(c, ChronicleKind.Died, c.GivenName + " " + c.Surname + " died at " + AgeOf(ctx, c) + ": " + cause + ".");
            WorldSystem.ReplaceResident(ctx, c);
            TripSystem.OnDeath(ctx, c, wasHead);
            if (!wasHead) return;

            state.Household.HeadId = null;
            if (EligibleHeirs(ctx).Count == 0)
            {
                state.Household.LineEnded = true;
                ctx.Emit(new LineEnded { Day = state.Day, LastHeadId = c.Id });
                ctx.Chronicle(c, ChronicleKind.LineEnded, "With " + c.GivenName + " the " + state.Household.Surname + " line ended.");
            }
        }

        /// <summary>
        /// Living adult household members who may take over, in the design's order of precedence:
        /// blood, then wards, then sworn members. Partners and the retired are not eligible.
        /// </summary>
        public static List<Character> EligibleHeirs(SimContext ctx)
        {
            var heirs = new List<Character>();
            foreach (HouseholdBond tier in new[] { HouseholdBond.Blood, HouseholdBond.Ward, HouseholdBond.Sworn })
                foreach (Character c in ctx.State.Characters)
                {
                    if (!c.Alive || c.Retired || c.Bond != tier || !InHousehold(ctx, c)) continue;
                    if (c.Id == ctx.State.Household.HeadId || AgeOf(ctx, c) < LifeRules.AdultAge) continue;
                    heirs.Add(c);
                }
            return heirs;
        }

        public static void Succeed(SimContext ctx, Character heir)
        {
            Household house = ctx.State.Household;
            int? previous = house.PastHeadIds.Count > 0 ? house.PastHeadIds[house.PastHeadIds.Count - 1] : (int?)null;
            house.HeadId = heir.Id;
            house.PastHeadIds.Add(heir.Id);
            ctx.Emit(new HeadSucceeded { Day = ctx.State.Day, NewHeadId = heir.Id, PreviousHeadId = previous });
            ctx.Chronicle(heir, ChronicleKind.Succeeded, heir.GivenName + " became head of the " + house.Surname + " household.");
        }

        /// <summary>What a season at home does on its own: children are born and the old may die.</summary>
        public static void RunInterlude(SimContext ctx)
        {
            foreach (Character a in ctx.State.Characters.ToArray())
            {
                if (!a.Alive || !a.PartnerId.HasValue || a.PartnerId.Value < a.Id) continue; // each couple once
                Character b = ctx.FindCharacter(a.PartnerId.Value);
                if (b == null || CheckCanParent(ctx, a, b) != null) continue;
                if (ctx.Rng.Chance(ctx.Life.BirthChancePerSeason)) Birth(ctx, a, b, null);
            }
            foreach (Character c in ctx.State.Characters.ToArray())
            {
                if (!c.Alive) continue;
                int age = AgeOf(ctx, c);
                if (age < LifeRules.OldAge) continue;
                if (ctx.Rng.Chance(ctx.Life.OldAgeDeathBase + ctx.Life.OldAgeDeathPerYear * (age - LifeRules.OldAge)))
                    Kill(ctx, c, "old age");
            }
        }

        private static string LowerFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}

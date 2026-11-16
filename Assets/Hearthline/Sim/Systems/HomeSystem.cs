using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Numbers for the homestead. First guesses; rules, not state.</summary>
    public sealed class HomeRules
    {
        public const int MinMorale = 0, MaxMorale = 100, StartMorale = 50;
        public const string FoodTag = "food";
        public const string Currency = "old_coin";

        /// <summary>Food items each member other than the head eats per season. The head eats on the road.</summary>
        public int FoodPerMemberPerSeason = 2;
        /// <summary>Morale settles toward a target each season, by at most this much.</summary>
        public int MoraleStepPerSeason = 10;
        public int MoraleBase = 50, MoralePerPoint = 5, MoralePerHungryMember = 10, MoralePerCrowdedMember = 5;
        public int MinWorkAge = 13;
        public double SkillGainPerSeason = 0.25, SkillCap = 5;
        /// <summary>Yield multiplier is 1 + SkillOutputBonus * skill + TierOutputBonus * (structure tier - 1).</summary>
        public double SkillOutputBonus = 0.1, TierOutputBonus = 0.25;
        /// <summary>Below this morale, members who think badly of the head may walk out.</summary>
        public int LeaveBelowMorale = 25;
        public double LeaveChance = 0.25, TheftChance = 0.5;
        /// <summary>Opinion of the head moves each season: up in a happy house, down in a miserable one.</summary>
        public int HappyAboveMorale = 60, OpinionGainWhenHappy = 1, OpinionLossWhenMiserable = 3;
        /// <summary>The store charges this multiple of an item's value.</summary>
        public int BuyMarkup = 2;
        /// <summary>Coins of trade it takes to push the town's prosperity by 1.</summary>
        public int CoinsPerProsperity = 5;
    }

    public sealed class HomeReportLine
    {
        public int CharacterId;
        public string JobId;
        /// <summary>What they brought in this season. Empty if the job yields no goods or could not be done.</summary>
        public Dictionary<string, int> Items = new Dictionary<string, int>();
        /// <summary>Why nothing was done, if so.</summary>
        public string Problem;
    }

    /// <summary>Structures, jobs, food, morale, trade with the town.</summary>
    internal static class HomeSystem
    {
        public static void EnsureHomestead(SimState state, ContentDatabase content)
        {
            foreach (StructureDef def in content.Structures.Values)
                if (!state.Household.Structures.ContainsKey(def.Id)) state.Household.Structures[def.Id] = def.StartsAt;
        }

        public static int TierOf(SimState state, string structureId) =>
            state.Household.Structures.TryGetValue(structureId, out int tier) ? tier : 0;

        private static bool CanWork(SimContext ctx, Character c) =>
            c.Alive && !c.Downed && c.JobId != null && LifeSystem.InHousehold(ctx, c) && !TripSystem.InParty(ctx.State, c)
            && c.Id != ctx.State.Household.HeadId && LifeSystem.AgeOf(ctx, c) >= ctx.Home.MinWorkAge;

        /// <summary>Why this person cannot do this job right now, or null.</summary>
        private static string JobProblem(SimContext ctx, Character c, JobDef job)
        {
            if (job.Structure != null && TierOf(ctx.State, job.Structure) < 1) return "there is no " + job.Structure;
            return null;
        }

        public static int Stat(SimContext ctx, HomeStat stat)
        {
            int total = 0;
            foreach (StructureDef def in ctx.Content.Structures.Values)
            {
                int tier = TierOf(ctx.State, def.Id);
                if (tier >= 1 && tier <= def.Tiers.Count && def.Tiers[tier - 1].Provides.TryGetValue(stat, out int v)) total += v;
            }
            foreach (Character c in ctx.State.Characters)
            {
                if (!CanWork(ctx, c) || !ctx.Content.Jobs.TryGetValue(c.JobId, out JobDef job)) continue;
                if (JobProblem(ctx, c, job) == null && job.Provides.TryGetValue(stat, out int v)) total += v;
            }
            return total;
        }

        public static List<Character> LivingMembers(SimContext ctx)
        {
            var members = new List<Character>();
            foreach (Character c in ctx.State.Characters) if (c.Alive && LifeSystem.InHousehold(ctx, c)) members.Add(c);
            return members;
        }

        // ---- A season at home ----------------------------------------------------------------------

        public static void RunSeason(SimContext ctx)
        {
            HomeRules rules = ctx.Home;
            Household house = ctx.State.Household;
            List<Character> members = LivingMembers(ctx);
            var report = new List<HomeReportLine>();

            // Work.
            foreach (Character c in members)
            {
                if (!CanWork(ctx, c) || !ctx.Content.Jobs.TryGetValue(c.JobId, out JobDef job)) continue;
                var line = new HomeReportLine { CharacterId = c.Id, JobId = job.Id, Problem = JobProblem(ctx, c, job) };
                report.Add(line);
                if (line.Problem != null) continue;

                double skill = StatSystem.GetStat(ctx.Content, c, job.Skill);
                int tier = job.Structure == null ? 1 : TierOf(ctx.State, job.Structure);
                double multiplier = Math.Max(0.25, 1 + rules.SkillOutputBonus * skill + rules.TierOutputBonus * (tier - 1));
                foreach (LootEntry entry in job.Produces)
                {
                    int count = (int)Math.Round(ctx.Rng.NextInt(entry.CountMin, entry.CountMax + 1) * multiplier);
                    if (count <= 0) continue;
                    line.Items[entry.ItemId] = count;
                    EventSystem.ChangeItem(ctx, entry.ItemId, count, inStores: true);
                }
                // Skills grow by use. The override holds the trained value; marks and traits still apply on top.
                double trained = c.BaseStats.TryGetValue(job.Skill, out double own) ? own
                    : ctx.Content.Vocab.Stats.TryGetValue(job.Skill, out StatDef stat) ? stat.Base : 0;
                if (trained < rules.SkillCap) c.BaseStats[job.Skill] = Math.Min(rules.SkillCap, trained + rules.SkillGainPerSeason);
            }

            // Food. Everyone but the head eats from the stores.
            // The head, and anyone travelling with them, eat on the road.
            int mouths = 0;
            foreach (Character c in members) if (c.Id != house.HeadId && !TripSystem.InParty(ctx.State, c)) mouths++;
            int needed = Math.Max(0, mouths) * rules.FoodPerMemberPerSeason;
            var foods = new List<string>();
            foreach (ItemDef item in ctx.Content.Items.Values) if (item.Tags.Contains(HomeRules.FoodTag)) foods.Add(item.Id);
            foods.Sort(StringComparer.Ordinal);
            foreach (string food in foods)
            {
                if (needed <= 0) break;
                house.Inventory.TryGetValue(food, out int have);
                int eaten = Math.Min(have, needed);
                if (eaten > 0) EventSystem.ChangeItem(ctx, food, -eaten, inStores: true);
                needed -= eaten;
            }
            int hungry = (needed + rules.FoodPerMemberPerSeason - 1) / rules.FoodPerMemberPerSeason;

            // Morale settles toward what the house offers.
            int crowded = Math.Max(0, members.Count - Stat(ctx, HomeStat.Capacity));
            int target = rules.MoraleBase + rules.MoralePerPoint * Stat(ctx, HomeStat.Morale)
                - rules.MoralePerHungryMember * hungry - rules.MoralePerCrowdedMember * crowded;
            target = Math.Max(HomeRules.MinMorale, Math.Min(HomeRules.MaxMorale, target));
            int step = Math.Max(-rules.MoraleStepPerSeason, Math.Min(rules.MoraleStepPerSeason, target - house.Morale));
            ChangeMorale(ctx, step);

            // What people think of the head follows the mood of the house; the unhappy may leave.
            Character head = house.HeadId.HasValue ? ctx.FindCharacter(house.HeadId.Value) : null;
            if (head != null)
                foreach (Character c in members)
                {
                    if (c.Id == head.Id || LifeSystem.AgeOf(ctx, c) < rules.MinWorkAge) continue;
                    int shift = house.Morale >= rules.HappyAboveMorale ? rules.OpinionGainWhenHappy
                        : house.Morale < rules.LeaveBelowMorale ? -rules.OpinionLossWhenMiserable : 0;
                    if (shift != 0) ChangeOpinion(ctx, c, head, shift);

                    bool free = c.Bond == HouseholdBond.Ward || c.Bond == HouseholdBond.Sworn;
                    c.Opinions.TryGetValue(head.Id, out int opinion);
                    if (!free || house.Morale >= rules.LeaveBelowMorale || opinion >= 0 || LifeSystem.AgeOf(ctx, c) < LifeRules.AdultAge) continue;
                    if (ctx.Rng.Chance(rules.LeaveChance)) Leave(ctx, c);
                }

            house.LastReport = report;
            ctx.Emit(new HomeReported { Day = ctx.State.Day, Lines = report, HungryMembers = hungry, Morale = house.Morale });
        }

        private static void Leave(SimContext ctx, Character c)
        {
            Household house = ctx.State.Household;
            string stolen = null;
            if (ctx.Rng.Chance(ctx.Home.TheftChance))
            {
                var items = new List<string>(house.Inventory.Keys);
                items.Sort(StringComparer.Ordinal);
                if (items.Count > 0)
                {
                    stolen = items[ctx.Rng.NextInt(0, items.Count)];
                    EventSystem.ChangeItem(ctx, stolen, -house.Inventory[stolen], inStores: true);
                }
            }
            ctx.Chronicle(c, ChronicleKind.Left, c.GivenName + " " + c.Surname + " left the household" + (stolen != null ? ", and took the " + stolen + " along." : "."));
            house.MemberIds.Remove(c.Id);
            c.JobId = null;
            if (c.PartnerId.HasValue)
            {
                Character partner = ctx.FindCharacter(c.PartnerId.Value);
                if (partner != null) partner.PartnerId = null;
                c.PartnerId = null;
            }
            ctx.Emit(new CharacterLeft { Day = ctx.State.Day, CharacterId = c.Id, StolenItemId = stolen });
        }

        public static void ChangeMorale(SimContext ctx, int delta)
        {
            Household house = ctx.State.Household;
            int now = Math.Max(HomeRules.MinMorale, Math.Min(HomeRules.MaxMorale, house.Morale + delta));
            if (now == house.Morale) return;
            int old = house.Morale;
            house.Morale = now;
            ctx.Emit(new MoraleChanged { Day = ctx.State.Day, OldValue = old, NewValue = now });
        }

        public static void ChangeOpinion(SimContext ctx, Character who, Character about, int delta)
        {
            who.Opinions.TryGetValue(about.Id, out int old);
            int value = Math.Max(-100, Math.Min(100, old + delta));
            if (value == old) return;
            who.Opinions[about.Id] = value;
            ctx.Emit(new OpinionChanged { Day = ctx.State.Day, CharacterId = who.Id, AboutCharacterId = about.Id, Delta = value - old, NewValue = value });
        }

        /// <summary>A structure loses a tier: fire, raid, storm. It can be rebuilt.</summary>
        public static void Damage(SimContext ctx, string structureId)
        {
            int tier = TierOf(ctx.State, structureId);
            if (tier < 1) return;
            ctx.State.Household.Structures[structureId] = tier - 1;
            ctx.Emit(new StructureChanged { Day = ctx.State.Day, StructureId = structureId, OldTier = tier, NewTier = tier - 1 });
        }
    }
}

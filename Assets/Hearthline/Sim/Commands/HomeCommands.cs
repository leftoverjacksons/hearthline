using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Builds the next tier of a homestead structure, paying its cost from the stores.</summary>
    public sealed class BuildStructure : Command
    {
        public string StructureId;

        internal override string Apply(SimContext ctx)
        {
            if (StructureId == null || !ctx.Content.Structures.TryGetValue(StructureId, out StructureDef def)) return "no structure '" + StructureId + "'";
            if (ctx.State.Trip != null) return "the head is away; building waits until they are home";
            int tier = HomeSystem.TierOf(ctx.State, def.Id);
            if (tier >= def.Tiers.Count) return def.Name + " cannot be improved further";
            StructureTier next = def.Tiers[tier];
            IDictionary<string, int> inv = ctx.State.Household.Inventory;
            var costs = new List<string>(next.Cost.Keys);
            costs.Sort(StringComparer.Ordinal);
            foreach (string item in costs)
            {
                inv.TryGetValue(item, out int have);
                if (have < next.Cost[item]) return next.Name + " needs " + next.Cost[item] + " " + item + " (the household has " + have + ")";
            }
            foreach (string item in costs) EventSystem.ChangeItem(ctx, item, -next.Cost[item]);
            ctx.State.Household.Structures[def.Id] = tier + 1;
            ctx.Emit(new StructureChanged { Day = ctx.State.Day, StructureId = def.Id, OldTier = tier, NewTier = tier + 1 });
            return null;
        }
    }

    /// <summary>Gives a household member work to do at home, or takes it away (JobId null).</summary>
    public sealed class AssignJob : Command
    {
        public int CharacterId;
        public string JobId;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character c);
            if (error != null) return error;
            if (!LifeSystem.InHousehold(ctx, c)) return c.GivenName + " is not in the household";
            if (JobId != null)
            {
                if (!ctx.Content.Jobs.ContainsKey(JobId)) return "no job '" + JobId + "'";
                if (c.Id == ctx.State.Household.HeadId) return "the head of household is the one who goes out; give the work to someone else";
                if (LifeSystem.AgeOf(ctx, c) < ctx.Home.MinWorkAge) return c.GivenName + " is too young to work";
            }
            if (c.JobId == JobId) return c.GivenName + (JobId == null ? " has no job" : " already does that");
            c.JobId = JobId;
            ctx.Emit(new JobAssigned { Day = ctx.State.Day, CharacterId = c.Id, JobId = JobId });
            return null;
        }
    }

    /// <summary>
    /// Treats a wound in the homestead's own infirmary. The tier comes from what has been built,
    /// and the healer is whoever at home knows the most medicine.
    /// </summary>
    public sealed class TreatWoundAtHome : Command
    {
        public int CharacterId;
        public int MarkId;

        internal override string Apply(SimContext ctx)
        {
            Character patient = ctx.FindCharacter(CharacterId);
            if (patient != null && TripSystem.InParty(ctx.State, patient)) return patient.GivenName + " is away from the homestead";
            int tier = Math.Min(TreatmentRules.MaxTier, HomeSystem.Stat(ctx, HomeStat.Treatment));
            if (tier < TreatmentRules.MinTier) return "the homestead has nowhere to treat a wound";
            double best = 0;
            foreach (Character c in HomeSystem.LivingMembers(ctx))
                if (!c.Downed && c.Id != CharacterId) best = Math.Max(best, StatSystem.GetStat(ctx.Content, c, "medicine"));
            int skill = (int)Math.Max(TreatmentRules.MinSkill, Math.Min(TreatmentRules.MaxSkill, Math.Floor(best)));
            return new InfirmaryTreatWound { CharacterId = CharacterId, MarkId = MarkId, InfirmaryTier = tier, HealerSkill = skill }.Apply(ctx);
        }
    }

    /// <summary>Sells goods at the store of the home settlement (or the one named). Trade is how the family feeds a town.</summary>
    public sealed class SellItems : Command
    {
        public string ItemId;
        public int Count = 1;
        public string SettlementId;

        internal override string Apply(SimContext ctx)
        {
            string error = TradeCheck(ctx, ItemId, Count, SettlementId, out ItemDef item, out SettlementState town);
            if (error != null) return error;
            TripSystem.CurrentInventory(ctx.State).TryGetValue(item.Id, out int have);
            if (have < Count) return "the household only has " + have + " of " + item.Id;
            int coins = item.Value * Count;
            EventSystem.ChangeItem(ctx, item.Id, -Count);
            EventSystem.ChangeItem(ctx, HomeRules.Currency, coins);
            int push = coins / ctx.Home.CoinsPerProsperity;
            if (push > 0) WorldSystem.Influence(ctx, town, SettlementValue.Prosperity, push);
            return null;
        }

        internal static string TradeCheck(SimContext ctx, string itemId, int count, string settlementId, out ItemDef item, out SettlementState town)
        {
            town = WorldSystem.Resolve(ctx.State, ctx.Content, settlementId);
            item = null;
            if (itemId == null || !ctx.Content.Items.TryGetValue(itemId, out item)) return "no item '" + itemId + "'";
            if (town == null) return "there is no settlement to trade with";
            if (!town.OpenServices.Contains("general_store")) return "there is no store open in " + town.Id;
            if (count < 1) return "count must be at least 1";
            if (item.Value < 1 || item.Id == HomeRules.Currency) return "nobody trades in " + item.Id;
            return null;
        }
    }

    public sealed class BuyItems : Command
    {
        public string ItemId;
        public int Count = 1;
        public string SettlementId;

        internal override string Apply(SimContext ctx)
        {
            string error = SellItems.TradeCheck(ctx, ItemId, Count, SettlementId, out ItemDef item, out SettlementState town);
            if (error != null) return error;
            int cost = item.Value * ctx.Home.BuyMarkup * Count;
            TripSystem.CurrentInventory(ctx.State).TryGetValue(HomeRules.Currency, out int coins);
            if (coins < cost) return "that costs " + cost + " coin (the household has " + coins + ")";
            EventSystem.ChangeItem(ctx, HomeRules.Currency, -cost);
            EventSystem.ChangeItem(ctx, item.Id, Count);
            int push = cost / ctx.Home.CoinsPerProsperity;
            if (push > 0) WorldSystem.Influence(ctx, town, SettlementValue.Prosperity, push);
            return null;
        }
    }
}

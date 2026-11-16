using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Numbers for trips. First guesses; rules, not state.</summary>
    public sealed class TripRules
    {
        public int MaxCompanions = 2;
        public int MinCompanionAge = 13;
        /// <summary>Weight one person with carry_capacity 1 can carry.</summary>
        public double CarryPerPerson = 30;
        /// <summary>Weight multiplier for "danger" events while the party is overloaded. Loaded travel draws ambushes.</summary>
        public double DangerWeightWhenOverloaded = 1.5;
        /// <summary>Share of each perishable stack in the pack that spoils per day on the road (at least one).</summary>
        public double PackSpoilPerDay = 0.25;
        /// <summary>Share of each perishable stack in the stores that spoils per season with nowhere to preserve it.</summary>
        public double StoreSpoilPerSeason = 0.5;
        public const string PerishableTag = "perishable";
    }

    /// <summary>Setting out, the pack, and coming home.</summary>
    internal static class TripSystem
    {
        /// <summary>Where gained and spent items go right now: the pack on a trip, the stores at home.</summary>
        public static SortedDictionary<string, int> CurrentInventory(SimState state) =>
            state.Trip != null ? state.Trip.Pack : state.Household.Inventory;

        public static bool InParty(SimState state, Character c) => state.Trip != null && state.Trip.PartyIds.Contains(c.Id);

        public static double Weight(ContentDatabase content, IDictionary<string, int> items)
        {
            double total = 0;
            foreach (var kv in items) if (content.Items.TryGetValue(kv.Key, out ItemDef item)) total += item.Weight * kv.Value;
            return total;
        }

        public static double Capacity(SimContext ctx, IEnumerable<int> partyIds)
        {
            double total = 0;
            foreach (int id in partyIds)
            {
                Character c = ctx.FindCharacter(id);
                if (c != null && c.Alive) total += ctx.Trips.CarryPerPerson * StatSystem.GetStat(ctx.Content, c, "carry_capacity");
            }
            return total;
        }

        public static bool Overloaded(SimContext ctx) =>
            ctx.State.Trip != null && Weight(ctx.Content, ctx.State.Trip.Pack) > Capacity(ctx, ctx.State.Trip.PartyIds) + 1e-9;

        public static void End(SimContext ctx, bool packLost)
        {
            Trip trip = ctx.State.Trip;
            if (trip == null) return;
            ctx.State.Trip = null; // from here on, items land in the stores
            if (!packLost)
                foreach (var kv in new List<KeyValuePair<string, int>>(trip.Pack)) EventSystem.ChangeItem(ctx, kv.Key, kv.Value);
            ctx.Emit(new TripEnded { Day = ctx.State.Day, DaysAway = ctx.State.Day - trip.StartDay, PackLost = packLost });
        }

        /// <summary>Someone on the trip died. If it was the head, the trip is over; the pack comes home only if someone is left to carry it.</summary>
        public static void OnDeath(SimContext ctx, Character dead, bool wasHead)
        {
            Trip trip = ctx.State.Trip;
            if (trip == null || !trip.PartyIds.Contains(dead.Id)) return;
            trip.PartyIds.Remove(dead.Id);
            if (wasHead) End(ctx, packLost: trip.PartyIds.Count == 0);
        }

        private static void Spoil(SimContext ctx, SortedDictionary<string, int> items, double share)
        {
            foreach (string id in new List<string>(items.Keys))
            {
                if (!ctx.Content.Items.TryGetValue(id, out ItemDef item) || !item.Tags.Contains(TripRules.PerishableTag)) continue;
                int lost = Math.Min(items[id], Math.Max(1, (int)Math.Floor(items[id] * share)));
                int left = items[id] - lost;
                if (left == 0) items.Remove(id); else items[id] = left;
                ctx.Emit(new ItemsSpoiled { Day = ctx.State.Day, ItemId = id, Count = lost, InPack = items != ctx.State.Household.Inventory });
            }
        }

        /// <summary>Meat in the pack does not keep.</summary>
        public static void DailyTick(SimContext ctx)
        {
            if (ctx.State.Trip != null) Spoil(ctx, ctx.State.Trip.Pack, ctx.Trips.PackSpoilPerDay);
        }

        /// <summary>Without a rack, larder or smokehouse, what is perishable in the stores goes off over a season.</summary>
        public static void SeasonTick(SimContext ctx)
        {
            if (HomeSystem.Stat(ctx, HomeStat.Preserving) < 1) Spoil(ctx, ctx.State.Household.Inventory, ctx.Trips.StoreSpoilPerSeason);
        }
    }

    /// <summary>The head leaves the homestead, with up to two companions and what they choose to carry.</summary>
    public sealed class SetOut : Command
    {
        public List<int> CompanionIds = new List<int>();
        /// <summary>Items taken from the stores into the pack: item id -> count.</summary>
        public Dictionary<string, int> Pack = new Dictionary<string, int>();

        internal override string Apply(SimContext ctx)
        {
            SimState state = ctx.State;
            if (state.Trip != null) return "the party is already away";
            if (state.ActiveEvent != null) return "finish the event in progress first";
            if (!state.Household.HeadId.HasValue) return "there is no head of household";
            Character head = ctx.FindCharacter(state.Household.HeadId.Value);
            if (head.Downed) return head.GivenName + " is in no state to travel";
            if (CompanionIds.Count > ctx.Trips.MaxCompanions) return "at most " + ctx.Trips.MaxCompanions + " companions can come";

            var party = new List<int> { head.Id };
            foreach (int id in CompanionIds)
            {
                string error = LivingCharacter(ctx, id, out Character c);
                if (error != null) return error;
                if (party.Contains(id)) return c.GivenName + " is listed twice";
                if (!LifeSystem.InHousehold(ctx, c)) return c.GivenName + " is not in the household";
                if (c.Downed) return c.GivenName + " is in no state to travel";
                if (LifeSystem.AgeOf(ctx, c) < ctx.Trips.MinCompanionAge) return c.GivenName + " is too young to come";
                party.Add(id);
            }
            var pack = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in Pack)
            {
                if (!ctx.Content.Items.ContainsKey(kv.Key)) return "no item '" + kv.Key + "'";
                if (kv.Value < 1) return "pack counts must be positive";
                state.Household.Inventory.TryGetValue(kv.Key, out int have);
                if (have < kv.Value) return "the stores only hold " + have + " of " + kv.Key;
                pack[kv.Key] = kv.Value;
            }
            double weight = TripSystem.Weight(ctx.Content, pack), capacity = TripSystem.Capacity(ctx, party);
            if (weight > capacity + 1e-9) return "that pack weighs " + weight + "; the party can carry " + capacity;

            foreach (var kv in pack) EventSystem.ChangeItem(ctx, kv.Key, -kv.Value);
            var trip = new Trip { StartDay = state.Day };
            trip.PartyIds.AddRange(party);
            foreach (var kv in pack) trip.Pack[kv.Key] = kv.Value;
            state.Trip = trip;
            ctx.Emit(new TripStarted { Day = state.Day, PartyIds = new List<int>(party) });
            return null;
        }
    }

    /// <summary>The party walks back through the gate. The pack goes into the stores; show the home report.</summary>
    public sealed class ReturnHome : Command
    {
        internal override string Apply(SimContext ctx)
        {
            if (ctx.State.Trip == null) return "the party is not away";
            if (ctx.State.ActiveEvent != null) return "finish the event in progress first";
            TripSystem.End(ctx, packLost: false);
            return null;
        }
    }
}

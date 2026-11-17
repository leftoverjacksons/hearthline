using System;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>A gathering spot was picked: berries, herbs, deadfall, salvage.</summary>
    public sealed class Gathered : DomainEvent { public string SpotId; public string ItemId; public int Count; public bool IntoPack; }

    /// <summary>
    /// Gathering from spots in the world. A spot is identified by presentation (a stable id per placed spot) and
    /// yields an item that has a <see cref="GatherDef"/>. Once picked, the spot is spent until the item's
    /// regrow_days have passed. What is gathered goes into the pack on a trip, the stores at home.
    /// </summary>
    internal static class GatherSystem
    {
        public static int DaysUntilRegrown(SimState state, ContentDatabase content, string spotId, string itemId)
        {
            if (spotId == null || !state.Gathered.TryGetValue(spotId, out int day)) return 0;
            int regrow = content.Items.TryGetValue(itemId ?? "", out ItemDef def) && def.Gather != null ? def.Gather.RegrowDays : 0;
            return Math.Max(0, day + regrow - state.Day);
        }
    }

    /// <summary>The head gathers from a spot in the world.</summary>
    public sealed class Gather : Command
    {
        /// <summary>Stable id of the spot, chosen by presentation ("berries_014").</summary>
        public string SpotId;
        public string ItemId;

        internal override string Apply(SimContext ctx)
        {
            if (string.IsNullOrEmpty(SpotId)) return "a gathering spot needs an id";
            if (ItemId == null || !ctx.Content.Items.TryGetValue(ItemId, out ItemDef def)) return "no item '" + ItemId + "'";
            if (def.Gather == null) return def.Name + " cannot be gathered";
            if (ctx.State.ActiveEvent != null) return "finish the event in progress first";
            if (!ctx.State.Household.HeadId.HasValue) return "there is no head of household";
            Character head = ctx.FindCharacter(ctx.State.Household.HeadId.Value);
            if (head.Downed) return head.GivenName + " is in no state to gather";
            int wait = GatherSystem.DaysUntilRegrown(ctx.State, ctx.Content, SpotId, ItemId);
            if (wait > 0) return "picked over; back in " + wait + (wait == 1 ? " day" : " days");

            int count = ctx.Rng.NextInt(def.Gather.CountMin, def.Gather.CountMax + 1);
            bool intoPack = ctx.State.Trip != null;
            EventSystem.ChangeItem(ctx, ItemId, count, inStores: !intoPack);
            ctx.State.Gathered[SpotId] = ctx.State.Day;
            ctx.Emit(new Gathered { Day = ctx.State.Day, SpotId = SpotId, ItemId = ItemId, Count = count, IntoPack = intoPack });
            return null;
        }
    }
}

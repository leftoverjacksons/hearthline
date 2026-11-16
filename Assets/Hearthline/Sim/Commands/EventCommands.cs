using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Starts a named event now, ignoring its conditions and cooldown. Debug, and scripted moments.</summary>
    public sealed class StartEvent : Command
    {
        public string EventId;
        public EventContext Context = new EventContext();

        internal override string Apply(SimContext ctx)
        {
            if (EventId == null || !ctx.Content.Events.TryGetValue(EventId, out EventDef def)) return "no event '" + EventId + "'";
            return EventSystem.Start(ctx, def, Context ?? new EventContext());
        }
    }

    /// <summary>
    /// Presentation calls this at natural opportunities (entering an area, cresting a ridge, an hour on the road).
    /// The storyteller decides whether anything happens. Check <see cref="Simulation.GetActiveEvent"/> afterwards.
    /// </summary>
    public sealed class StorytellerTick : Command
    {
        public EventContext Context = new EventContext();

        internal override string Apply(SimContext ctx)
        {
            if (ctx.State.ActiveEvent != null) return "an event is already in progress";
            if (!ctx.State.Household.HeadId.HasValue) return "there is no head of household";
            EventSystem.StorytellerTick(ctx, Context ?? new EventContext());
            return null;
        }
    }

    public sealed class ChooseEventOption : Command
    {
        public int ChoiceIndex;

        internal override string Apply(SimContext ctx) => EventSystem.Choose(ctx, ChoiceIndex);
    }

    /// <summary>Adds items to the household's stores, or removes them with a negative count.</summary>
    public sealed class GiveItem : Command
    {
        public string ItemId;
        public int Count = 1;

        internal override string Apply(SimContext ctx)
        {
            if (ItemId == null || !ctx.Content.Items.ContainsKey(ItemId)) return "no item '" + ItemId + "'";
            if (Count == 0) return "count must not be zero";
            TripSystem.CurrentInventory(ctx.State).TryGetValue(ItemId, out int have);
            if (have + Count < 0) return "the household only has " + have + " of " + ItemId;
            EventSystem.ChangeItem(ctx, ItemId, Count);
            return null;
        }
    }

    public sealed class ChangeReputation : Command
    {
        public string FactionId;
        public int Delta;

        internal override string Apply(SimContext ctx)
        {
            if (FactionId == null || !ctx.Content.Factions.ContainsKey(FactionId)) return "no faction '" + FactionId + "'";
            if (Delta == 0) return "delta must not be zero";
            WorldSystem.ChangeReputation(ctx, FactionId, Delta);
            return null;
        }
    }

    /// <summary>
    /// The family did something a town will feel: sold a season's pelts, drove off raiders, reported a neighbour.
    /// The push is stored and takes effect when the season turns. You shape the town; you do not set it.
    /// </summary>
    public sealed class InfluenceSettlement : Command
    {
        /// <summary>Null means the home settlement.</summary>
        public string SettlementId;
        public SettlementValue Value;
        public int Delta;

        internal override string Apply(SimContext ctx)
        {
            SettlementState town = WorldSystem.Resolve(ctx.State, ctx.Content, SettlementId);
            if (town == null) return "no settlement '" + SettlementId + "'";
            if (Delta == 0) return "delta must not be zero";
            WorldSystem.Influence(ctx, town, Value, Delta);
            return null;
        }
    }

    public sealed class SetGrudge : Command
    {
        public string FactionId;
        /// <summary>True starts a grudge (needs a reason); false settles it.</summary>
        public bool Held = true;
        public string Reason;

        internal override string Apply(SimContext ctx)
        {
            if (FactionId == null || !ctx.Content.Factions.ContainsKey(FactionId)) return "no faction '" + FactionId + "'";
            if (WorldSystem.HasGrudge(ctx.State, FactionId) == Held) return Held ? "that grudge already exists" : "there is no such grudge";
            if (Held && string.IsNullOrWhiteSpace(Reason)) return "a grudge needs a reason";
            if (Held) WorldSystem.AddGrudge(ctx, FactionId, Reason); else WorldSystem.SettleGrudge(ctx, FactionId);
            return null;
        }
    }
}

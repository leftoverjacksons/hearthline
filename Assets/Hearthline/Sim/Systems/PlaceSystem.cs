using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>What the family knows of one place, and what has been taken from it (docs/specs/ruins-and-places.md).</summary>
    public sealed class PlaceState
    {
        public string Kind;
        /// <summary>The day it was first found, or -1 if only searched by cheat.</summary>
        public int DiscoveredDay = -1;
        /// <summary>Search spots already gone through (indices, sorted). Salvage doesn't come back.</summary>
        public List<int> Searched = new List<int>();
        public bool FindTaken;
        /// <summary>The last day someone looked for the find and missed it, or -1.</summary>
        public int FindMissedDay = -1;
    }

    /// <summary>A rumour taken up at the job board: the trip's goal is that kind of place's find.</summary>
    public sealed class FollowedRumour
    {
        public string Kind;
        public int Day;
    }

    public sealed class PlaceDiscovered : DomainEvent { public string PlaceId; public string Kind; public string Name; }
    public sealed class SpotSearched : DomainEvent { public string PlaceId; public int Spot; public Dictionary<string, int> Items = new Dictionary<string, int>(); public bool IntoPack; }
    public sealed class FindTaken : DomainEvent { public string PlaceId; public string ItemId; public int Count; public string Text; }
    public sealed class FindMissed : DomainEvent { public string PlaceId; public string Stat; }
    public sealed class RumourTaken : DomainEvent { public string Kind; }
    public sealed class RumourEnded : DomainEvent { public string Kind; public bool Found; }

    internal static class PlaceSystem
    {
        /// <summary>Chance to find a checked find below the needed stat: 25% at nothing, rising toward 75%.</summary>
        public static double FindChance(double stat, int difficulty) =>
            stat >= difficulty ? 1 : 0.25 + 0.5 * Math.Max(0, stat) / difficulty;

        public static PlaceState Get(SimState state, string placeId) =>
            placeId != null && state.Places.TryGetValue(placeId, out PlaceState p) ? p : null;

        /// <summary>The place's state, made on first contact. Null with an error if the id and kind don't fit.</summary>
        public static PlaceState Touch(SimContext ctx, string placeId, string kind, out PlaceDef def, out string error)
        {
            def = null;
            error = null;
            if (string.IsNullOrEmpty(placeId)) { error = "a place needs an id"; return null; }
            if (!ctx.Content.Places.TryGetValue(kind ?? "", out def)) { error = "no kind of place '" + kind + "'"; return null; }
            if (ctx.State.Places.TryGetValue(placeId, out PlaceState p))
            {
                if (p.Kind != kind) { error = placeId + " is a " + p.Kind + ", not a " + kind; return null; }
                return p;
            }
            p = new PlaceState { Kind = kind };
            ctx.State.Places[placeId] = p;
            return p;
        }

        public static Character AbleHead(SimContext ctx, string doing, out string error)
        {
            error = null;
            if (ctx.State.ActiveEvent != null) { error = "finish the event in progress first"; return null; }
            Character head = ctx.State.Household.HeadId.HasValue ? ctx.FindCharacter(ctx.State.Household.HeadId.Value) : null;
            if (head == null || !head.Alive) { error = "there is no head of household"; return null; }
            if (head.Downed) { error = head.GivenName + " is in no state to " + doing; return null; }
            return head;
        }

        /// <summary>Rumours the job board can offer: kinds with a find nobody has taken, not already followed.</summary>
        public static List<string> Rumours(SimState state, ContentDatabase content)
        {
            var list = new List<string>();
            foreach (PlaceDef def in content.Places.Values)
            {
                if (def.Rumour == null || def.Find == null) continue;
                if (state.Rumour != null && state.Rumour.Kind == def.Id) continue;
                bool taken = false;
                foreach (PlaceState p in state.Places.Values) if (p.Kind == def.Id && p.FindTaken) taken = true;
                if (!taken) list.Add(def.Id);
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }

    /// <summary>The head walks into a place for the first time.</summary>
    public sealed class DiscoverPlace : Command
    {
        public string PlaceId, Kind;

        internal override string Apply(SimContext ctx)
        {
            Character head = PlaceSystem.AbleHead(ctx, "explore", out string error);
            if (head == null) return error;
            PlaceState p = PlaceSystem.Touch(ctx, PlaceId, Kind, out PlaceDef def, out error);
            if (p == null) return error;
            if (p.DiscoveredDay >= 0) return "already known";
            p.DiscoveredDay = ctx.State.Day;
            ctx.Chronicle(head, ChronicleKind.Deed, head.GivenName + " first came to " + def.Name + ".");
            ctx.Emit(new PlaceDiscovered { Day = ctx.State.Day, PlaceId = PlaceId, Kind = Kind, Name = def.Name });
            return null;
        }
    }

    /// <summary>The head goes through one search spot: a roll on the place's loot table, once.</summary>
    public sealed class SearchSpot : Command
    {
        public string PlaceId, Kind;
        public int Spot;

        internal override string Apply(SimContext ctx)
        {
            if (PlaceSystem.AbleHead(ctx, "search", out string error) == null) return error;
            PlaceState p = PlaceSystem.Touch(ctx, PlaceId, Kind, out PlaceDef def, out error);
            if (p == null) return error;
            if (Spot < 0 || Spot >= def.SearchMax) return "no search spot " + Spot + " at " + def.Name;
            if (p.Searched.Contains(Spot)) return "already gone through";
            var items = new Dictionary<string, int>();
            bool intoPack = ctx.State.Trip != null;
            if (def.Loot != null && ctx.Content.LootTables.TryGetValue(def.Loot, out LootTableDef table) && table.Entries.Count > 0)
            {
                var weights = table.Entries.ConvertAll(e => e.Weight);
                for (int i = 0; i < table.Rolls; i++)
                {
                    LootEntry entry = table.Entries[ctx.Rng.PickWeighted(weights)];
                    int n = ctx.Rng.NextInt(entry.CountMin, entry.CountMax + 1);
                    if (n <= 0) continue;
                    items.TryGetValue(entry.ItemId, out int had);
                    items[entry.ItemId] = had + n;
                }
            }
            var ids = new List<string>(items.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids) EventSystem.ChangeItem(ctx, id, items[id], inStores: !intoPack);
            p.Searched.Add(Spot);
            p.Searched.Sort();
            ctx.Emit(new SpotSearched { Day = ctx.State.Day, PlaceId = PlaceId, Spot = Spot, Items = items, IntoPack = intoPack });
            return null;
        }
    }

    /// <summary>The head looks for the place's find. With a check, a miss can be tried again another day.</summary>
    public sealed class TakeFind : Command
    {
        public string PlaceId, Kind;

        internal override string Apply(SimContext ctx)
        {
            Character head = PlaceSystem.AbleHead(ctx, "search", out string error);
            if (head == null) return error;
            PlaceState p = PlaceSystem.Touch(ctx, PlaceId, Kind, out PlaceDef def, out error);
            if (p == null) return error;
            if (def.Find == null) return "nothing is hidden at " + def.Name;
            if (p.FindTaken) return "already taken";
            if (p.FindMissedDay == ctx.State.Day) return "nothing more to see today";

            PlaceFind find = def.Find;
            if (find.CheckStat != null)
            {
                double stat = StatSystem.GetStat(ctx.Content, head, find.CheckStat);
                if (!ctx.Rng.Chance(PlaceSystem.FindChance(stat, find.CheckDifficulty)))
                {
                    p.FindMissedDay = ctx.State.Day;
                    ctx.Emit(new FindMissed { Day = ctx.State.Day, PlaceId = PlaceId, Stat = find.CheckStat });
                    return null;
                }
            }

            ItemDef item = ctx.Content.Items[find.Item];
            int count = ctx.Rng.NextInt(find.CountMin, find.CountMax + 1);
            if (item.Equip != null)
            {
                count = 1;
                GearSystem.Create(ctx, item, head, "Found at " + def.Name + " by");
            }
            else EventSystem.ChangeItem(ctx, find.Item, count, inStores: ctx.State.Trip == null);
            p.FindTaken = true;
            ctx.Emit(new FindTaken { Day = ctx.State.Day, PlaceId = PlaceId, ItemId = find.Item, Count = count, Text = find.Text });

            if (ctx.State.Rumour != null && ctx.State.Rumour.Kind == Kind)
            {
                ctx.Chronicle(head, ChronicleKind.Deed, head.GivenName + " ran down the rumour about " + def.Name + " and found " + (item.Equip != null ? "a " + item.Name.ToLowerInvariant() : item.Name.ToLowerInvariant()) + ".");
                ctx.State.Rumour = null;
                ctx.Emit(new RumourEnded { Day = ctx.State.Day, Kind = Kind, Found = true });
            }
            return null;
        }
    }

    /// <summary>Takes up a rumour from the job board: that place's find becomes the goal.</summary>
    public sealed class FollowRumour : Command
    {
        public string Kind;

        internal override string Apply(SimContext ctx)
        {
            if (!ctx.Content.Places.TryGetValue(Kind ?? "", out PlaceDef def) || def.Rumour == null) return "no rumour about '" + Kind + "'";
            if (ctx.State.Rumour != null) return "already following the rumour about " + ctx.Content.Places[ctx.State.Rumour.Kind].Name;
            if (!PlaceSystem.Rumours(ctx.State, ctx.Content).Contains(Kind)) return "someone already found what was at " + def.Name;
            ctx.State.Rumour = new FollowedRumour { Kind = Kind, Day = ctx.State.Day };
            ctx.Emit(new RumourTaken { Day = ctx.State.Day, Kind = Kind });
            return null;
        }
    }

    public sealed class DropRumour : Command
    {
        internal override string Apply(SimContext ctx)
        {
            if (ctx.State.Rumour == null) return "no rumour is being followed";
            string kind = ctx.State.Rumour.Kind;
            ctx.State.Rumour = null;
            ctx.Emit(new RumourEnded { Day = ctx.State.Day, Kind = kind, Found = false });
            return null;
        }
    }

    /// <summary>Cheat: the place as if nobody had been there.</summary>
    public sealed class ResetPlace : Command
    {
        public string PlaceId;

        internal override string Apply(SimContext ctx)
        {
            if (PlaceId == null || !ctx.State.Places.Remove(PlaceId)) return "nothing known about '" + PlaceId + "'";
            return null;
        }
    }
}

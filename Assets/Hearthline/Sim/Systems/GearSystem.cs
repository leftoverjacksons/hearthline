using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Numbers for gear. First guesses; rules, not state.</summary>
    public sealed class GearRules
    {
        /// <summary>Ledger entries an item needs before it can be given a name.</summary>
        public int LedgerEntriesToName = 3;
        /// <summary>Workshop total the homestead needs to engrave a name.</summary>
        public int WorkshopToName = 1;
    }

    /// <summary>Gear as presentation needs it: the instance joined with its definition.</summary>
    public sealed class GearView
    {
        public int GearId; public string DefId;
        /// <summary>The engraved name if it has one, else the item's ordinary name.</summary>
        public string Name;
        public EquipSlot Slot; public bool Equipped; public bool Heirloom;
        /// <summary>Asset key for the bridge.</summary>
        public string Visual;
        public List<LedgerEntry> Ledger;
    }

    /// <summary>Unique items: who carries them, what is worn, and the history each one gathers.</summary>
    internal static class GearSystem
    {
        public static GearItem Find(SimContext ctx, int gearId, out Character owner)
        {
            owner = null;
            foreach (GearItem g in ctx.State.Household.GearStore) if (g.Id == gearId) return g;
            foreach (Character c in ctx.State.Characters)
                foreach (GearItem g in c.Gear) if (g.Id == gearId) { owner = c; return g; }
            return null;
        }

        public static void Note(SimContext ctx, GearItem gear, string text) =>
            gear.Ledger.Add(new LedgerEntry { Day = ctx.State.Day, Text = text });

        public static string DisplayName(ContentDatabase content, GearItem gear) =>
            gear.Name ?? (content.Items.TryGetValue(gear.DefId, out ItemDef def) ? def.Name : gear.DefId);

        private static string FullName(Character c) => (c.GivenName + " " + c.Surname).Trim();

        public static GearItem Create(SimContext ctx, ItemDef def, Character owner, string how)
        {
            var gear = new GearItem { Id = ctx.State.NextGearId++, DefId = def.Id };
            if (owner != null)
            {
                gear.FirstOwnerId = owner.Id;
                owner.Gear.Add(gear);
                Note(ctx, gear, how + " " + FullName(owner) + ".");
            }
            else
            {
                ctx.State.Household.GearStore.Add(gear);
                Note(ctx, gear, "Came into the household's keeping.");
            }
            ctx.Emit(new GearChanged { Day = ctx.State.Day, GearId = gear.Id, CharacterId = owner?.Id });
            return gear;
        }

        /// <summary>True if <paramref name="c"/> descends by blood from the person with id <paramref name="ancestorId"/>.</summary>
        private static bool DescendsFrom(SimContext ctx, Character c, int ancestorId, int depth = 0)
        {
            if (depth > 12) return false;
            foreach (int parentId in c.ParentIds)
            {
                if (parentId == ancestorId) return true;
                Character parent = ctx.FindCharacter(parentId);
                if (parent != null && DescendsFrom(ctx, parent, ancestorId, depth + 1)) return true;
            }
            return false;
        }

        public static void Transfer(SimContext ctx, GearItem gear, Character from, Character to)
        {
            if (from != null) from.Gear.Remove(gear); else ctx.State.Household.GearStore.Remove(gear);
            gear.Equipped = false;
            if (to == null)
            {
                ctx.State.Household.GearStore.Add(gear);
                Note(ctx, gear, from != null ? "Put away by " + FullName(from) + "." : "Put away.");
            }
            else
            {
                to.Gear.Add(gear);
                if (!gear.FirstOwnerId.HasValue) gear.FirstOwnerId = to.Id;
                gear.Heirloom = gear.FirstOwnerId.HasValue && DescendsFrom(ctx, to, gear.FirstOwnerId.Value);
                bool bequest = from != null && !from.Alive;
                Note(ctx, gear, bequest ? "Passed from " + FullName(from) + " to " + FullName(to) + "."
                    : from != null ? "Handed from " + FullName(from) + " to " + FullName(to) + "." : "Taken up by " + FullName(to) + ".");
                if (bequest) ctx.Chronicle(to, ChronicleKind.Bequest, to.GivenName + " took up " + DisplayName(ctx.Content, gear) + ", which was " + from.GivenName + "'s.");
            }
            ctx.Emit(new GearChanged { Day = ctx.State.Day, GearId = gear.Id, CharacterId = to?.Id });
            if (from != null) ctx.Emit(new GearChanged { Day = ctx.State.Day, GearId = gear.Id, CharacterId = from.Id });
        }

        public static List<GearView> Views(ContentDatabase content, IEnumerable<GearItem> items)
        {
            var views = new List<GearView>();
            foreach (GearItem g in items)
            {
                EquipDef equip = content.Items.TryGetValue(g.DefId, out ItemDef def) ? def.Equip : null;
                views.Add(new GearView
                {
                    GearId = g.Id, DefId = g.DefId, Name = DisplayName(content, g), Equipped = g.Equipped, Heirloom = g.Heirloom,
                    Slot = equip?.Slot ?? EquipSlot.Accessory, Visual = equip?.Visual, Ledger = g.Ledger,
                });
            }
            return views;
        }
    }

    /// <summary>A unique item comes into being: found, made, bought. It goes to a person, or to the stores.</summary>
    public sealed class GiveGear : Command
    {
        public string DefId;
        /// <summary>Null puts it in the household's stores.</summary>
        public int? CharacterId;

        internal override string Apply(SimContext ctx)
        {
            if (DefId == null || !ctx.Content.Items.TryGetValue(DefId, out ItemDef def) || def.Equip == null) return "no gear '" + DefId + "'";
            Character owner = null;
            if (CharacterId.HasValue)
            {
                string error = LivingCharacter(ctx, CharacterId.Value, out owner);
                if (error != null) return error;
            }
            GearSystem.Create(ctx, def, owner, "First carried by");
            return null;
        }
    }

    /// <summary>Wears or wields a piece of gear the character is carrying. Whatever was in that slot comes off.</summary>
    public sealed class EquipGear : Command
    {
        public int GearId;
        /// <summary>False takes it off.</summary>
        public bool Equipped = true;

        internal override string Apply(SimContext ctx)
        {
            GearItem gear = GearSystem.Find(ctx, GearId, out Character owner);
            if (gear == null) return "no gear with id " + GearId;
            if (owner == null) return "gear in the stores must be taken up by someone first";
            if (!owner.Alive) return owner.GivenName + " is dead";
            if (gear.Equipped == Equipped) return Equipped ? "that is already worn" : "that is not worn";
            if (Equipped)
            {
                EquipSlot slot = ctx.Content.Items[gear.DefId].Equip.Slot;
                foreach (GearItem other in owner.Gear)
                    if (other.Equipped && ctx.Content.Items.TryGetValue(other.DefId, out ItemDef d) && d.Equip != null && d.Equip.Slot == slot) other.Equipped = false;
            }
            gear.Equipped = Equipped;
            ctx.Emit(new GearChanged { Day = ctx.State.Day, GearId = gear.Id, CharacterId = owner.Id });
            return null;
        }
    }

    /// <summary>
    /// Moves gear between people, or to and from the stores. Taking gear from the dead is how it is
    /// bequeathed: the chronicle records it, and in the hands of a descendant of its first owner it is an heirloom.
    /// </summary>
    public sealed class TransferGear : Command
    {
        public int GearId;
        /// <summary>Null puts it in the stores.</summary>
        public int? ToCharacterId;

        internal override string Apply(SimContext ctx)
        {
            GearItem gear = GearSystem.Find(ctx, GearId, out Character from);
            if (gear == null) return "no gear with id " + GearId;
            Character to = null;
            if (ToCharacterId.HasValue)
            {
                string error = LivingCharacter(ctx, ToCharacterId.Value, out to);
                if (error != null) return error;
            }
            if (from == to) return "it is already there";
            GearSystem.Transfer(ctx, gear, from, to);
            return null;
        }
    }

    /// <summary>Engraves a name on gear with enough history. Needs a workshop at home.</summary>
    public sealed class NameGear : Command
    {
        public int GearId;
        public string Name;

        internal override string Apply(SimContext ctx)
        {
            GearItem gear = GearSystem.Find(ctx, GearId, out Character owner);
            if (gear == null) return "no gear with id " + GearId;
            if (string.IsNullOrWhiteSpace(Name)) return "a name is needed";
            if (gear.Ledger.Count < ctx.Gear.LedgerEntriesToName) return "it has not seen enough yet to earn a name";
            if (HomeSystem.Stat(ctx, HomeStat.Workshop) < ctx.Gear.WorkshopToName) return "engraving needs a workshop";
            if (ctx.State.Trip != null) return "engraving waits until the head is home";
            gear.Name = Name.Trim();
            GearSystem.Note(ctx, gear, "Named \"" + gear.Name + "\".");
            ctx.Emit(new GearChanged { Day = ctx.State.Day, GearId = gear.Id, CharacterId = owner?.Id });
            return null;
        }
    }

    /// <summary>Writes a line into a piece of gear's history. For presentation: kills, close calls, repairs.</summary>
    public sealed class NoteOnGear : Command
    {
        public int GearId;
        public string Text;

        internal override string Apply(SimContext ctx)
        {
            GearItem gear = GearSystem.Find(ctx, GearId, out _);
            if (gear == null) return "no gear with id " + GearId;
            if (string.IsNullOrWhiteSpace(Text)) return "the note is empty";
            GearSystem.Note(ctx, gear, Text.Trim());
            return null;
        }
    }
}

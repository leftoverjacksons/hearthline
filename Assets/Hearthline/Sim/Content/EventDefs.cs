using System.Collections.Generic;

namespace Hearthline.Sim.Content
{
    public sealed class FactionDef { public string Id; public string Name; public string Description; public string SourceFile; }

    public sealed class ItemDef
    {
        public string Id; public string Name; public double Weight;
        /// <summary>What the store pays for one, in coin. 0 means nobody trades in it.</summary>
        public int Value;
        public readonly List<string> Tags = new List<string>();
        /// <summary>Null for ordinary stackable goods.</summary>
        public EquipDef Equip;
        /// <summary>Non-null when the item can be gathered from spots in the world (berries, deadfall, salvage).</summary>
        public GatherDef Gather;
        /// <summary>Non-null for firearms (gear only).</summary>
        public WeaponDef Weapon;
        public string SourceFile;
    }

    /// <summary>How much one gathering spot yields and how long it takes to come back.</summary>
    public sealed class GatherDef
    {
        public int CountMin = 1, CountMax = 1;
        /// <summary>Days before a picked-over spot can be gathered again.</summary>
        public int RegrowDays = 7;
    }

    /// <summary>Makes an item gear: a unique thing that is worn or wielded, not a count in the stores.</summary>
    public sealed class EquipDef
    {
        public EquipSlot Slot;
        /// <summary>Asset key for the bridge.</summary>
        public string Visual;
        public readonly List<MarkEffect> Effects = new List<MarkEffect>();
        /// <summary>Extra effects in the hands of a blood descendant of the first owner.</summary>
        public readonly List<MarkEffect> HeirloomEffects = new List<MarkEffect>();
    }

    public sealed class LootEntry { public string ItemId; public int CountMin = 1; public int CountMax = 1; public double Weight = 1; }

    public sealed class LootTableDef
    {
        public string Id; public int Rolls = 1;
        public readonly List<LootEntry> Entries = new List<LootEntry>();
        public string SourceFile;
    }

    /// <summary>When the storyteller may offer an event. Every condition given must hold.</summary>
    public sealed class EventWhen
    {
        public readonly List<string> AreaTagsAny = new List<string>();
        public readonly List<string> AreaTagsAll = new List<string>();
        public int? ThinLevelMin, ThinLevelMax;
        public readonly List<TimeOfDay> TimeOfDay = new List<TimeOfDay>();
        public bool? AtHome;
        public readonly List<string> FlagsAll = new List<string>();
        public readonly List<string> FlagsNone = new List<string>();
        /// <summary>Faction -> the household's reputation must be at least / at most this.</summary>
        public readonly Dictionary<string, int> ReputationMin = new Dictionary<string, int>();
        public readonly Dictionary<string, int> ReputationMax = new Dictionary<string, int>();
        /// <summary>One of these factions must hold a grudge against the family.</summary>
        public readonly List<string> GrudgeAny = new List<string>();
        /// <summary>Conditions on the settlement the party is in (or the home settlement).</summary>
        public readonly List<string> ServicesAny = new List<string>();
        public readonly Dictionary<SettlementValue, int> SettlementMin = new Dictionary<SettlementValue, int>();
        public readonly Dictionary<SettlementValue, int> SettlementMax = new Dictionary<SettlementValue, int>();
        public readonly Dictionary<HomeStat, int> HomeMin = new Dictionary<HomeStat, int>();
        public readonly Dictionary<HomeStat, int> HomeMax = new Dictionary<HomeStat, int>();
        public int? MoraleMin, MoraleMax;
    }

    public sealed class CastSlot
    {
        public string Role;
        /// <summary>"party", "household" or "settlement" for an existing person; null when <see cref="Generate"/> is set.</summary>
        public string From;
        /// <summary>"head" or "any"; for from: settlement, the resident's role (shopkeeper). Only with <see cref="From"/>.</summary>
        public string Pick = "any";
        /// <summary>Archetype of a new person to create for this event.</summary>
        public string Generate;
        public int AgeMin = 20, AgeMax = 50;
        /// <summary>Existing person: must have one of these. Generated person: gets all of these.</summary>
        public readonly List<string> Traits = new List<string>();
        public readonly List<string> MarksAny = new List<string>();
    }

    public sealed class ChoiceRequires
    {
        /// <summary>Cast role the requirement is checked on. Null means the head of household.</summary>
        public string Who;
        public readonly List<string> ItemAny = new List<string>();
        public readonly List<string> TraitAny = new List<string>();
        public readonly List<string> MarkAny = new List<string>();
        public readonly Dictionary<string, double> StatMin = new Dictionary<string, double>();
        public readonly List<string> FlagsAll = new List<string>();
    }

    public sealed class MarkOutcome
    {
        public string Target; public string DefId; public BodyRegion? Region; public int Severity = 1;
        public double Chance = 1; public string Cause;
    }

    public sealed class HitOutcome
    {
        public string Target; public DamageType Damage; public BodyRegion? Region; public int Severity = 1; public string Cause;
    }

    public sealed class TraitOutcome { public string Target; public string Add; public string Remove; }

    public sealed class GearOutcome { public string DefId; public string To; public string Note; }

    public sealed class GrudgeOutcome { public string FactionId; public string Reason; }

    public sealed class MemoryOutcome { public string Target; public string Text; }

    public sealed class ChanceBranch { public double P; public EventOutcome Outcome; }

    /// <summary>What a choice does. Everything set is applied; then one chance branch, if any, is rolled.</summary>
    public sealed class EventOutcome
    {
        /// <summary>Cast role -> change in that person's opinion of the head of household.</summary>
        public readonly Dictionary<string, int> Relationship = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Reputation = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Items = new Dictionary<string, int>();
        public MarkOutcome Mark;
        public HitOutcome Hit;
        public TraitOutcome Trait;
        public string LootTable;
        public readonly List<string> FlagSet = new List<string>();
        public readonly List<string> FlagClear = new List<string>();
        /// <summary>Cast role who joins the household.</summary>
        public string Recruit;
        /// <summary>Encounter id handed to presentation to stage a fight. The sim does not resolve it.</summary>
        public string Ambush;
        /// <summary>A deed written into the head's chronicle.</summary>
        public string Chronicle;
        /// <summary>Pushes on the event's settlement. Takes effect when the season turns.</summary>
        public readonly Dictionary<SettlementValue, int> Settlement = new Dictionary<SettlementValue, int>();
        public GrudgeOutcome Grudge;
        /// <summary>Faction whose grudge against the family ends.</summary>
        public string SettleGrudge;
        /// <summary>Something a cast member will remember about the family.</summary>
        public MemoryOutcome Memory;
        /// <summary>A unique item given to a cast member.</summary>
        public GearOutcome GearGiven;
        /// <summary>Change in household morale.</summary>
        public int Morale;
        /// <summary>Structure that loses a tier.</summary>
        public string Damage;
        public string Goto;
        public readonly List<ChanceBranch> Chance = new List<ChanceBranch>();

        /// <summary>Names of the outcome kinds this outcome uses, including inside chance branches.</summary>
        public void CollectKinds(HashSet<string> kinds)
        {
            if (Relationship.Count > 0) kinds.Add("relationship");
            if (Reputation.Count > 0) kinds.Add("reputation");
            if (Items.Count > 0) kinds.Add("item");
            if (Mark != null) kinds.Add("mark");
            if (Hit != null) kinds.Add("hit");
            if (Trait != null) kinds.Add("trait");
            if (LootTable != null) kinds.Add("loot_table");
            if (FlagSet.Count > 0 || FlagClear.Count > 0) kinds.Add("flag");
            if (Recruit != null) kinds.Add("recruit");
            if (Ambush != null) kinds.Add("ambush");
            if (Chronicle != null) kinds.Add("chronicle");
            if (Settlement.Count > 0) kinds.Add("settlement");
            if (Grudge != null || SettleGrudge != null) kinds.Add("grudge");
            if (Memory != null) kinds.Add("memory");
            if (GearGiven != null) kinds.Add("gear");
            if (Morale != 0) kinds.Add("morale");
            if (Damage != null) kinds.Add("damage");
            if (Goto != null) kinds.Add("goto");
            if (Chance.Count > 0) kinds.Add("chance");
            foreach (ChanceBranch b in Chance) b.Outcome.CollectKinds(kinds);
        }

        public static readonly string[] AllKinds =
            { "relationship", "reputation", "item", "mark", "hit", "trait", "loot_table", "flag", "recruit", "ambush", "chronicle", "settlement", "grudge", "memory", "morale", "damage", "gear", "goto", "chance" };
    }

    public sealed class EventChoice { public string Label; public ChoiceRequires Requires; public EventOutcome Outcome = new EventOutcome(); }

    public sealed class EventBeat
    {
        public string Id; public string Text;
        public readonly List<EventChoice> Choices = new List<EventChoice>();
    }

    public sealed class EventDef
    {
        public string Id;
        public readonly List<string> Tags = new List<string>();
        public double Weight = 10;
        public int CooldownDays;
        /// <summary>1 to 3. How much of the storyteller's budget the event spends; bigger events are followed by longer quiet.</summary>
        public int Intensity = 1;
        public EventWhen When = new EventWhen();
        public readonly List<CastSlot> Cast = new List<CastSlot>();
        public readonly List<EventBeat> Beats = new List<EventBeat>();
        public string SourceFile;

        public EventBeat FindBeat(string id)
        {
            foreach (EventBeat b in Beats) if (b.Id == id) return b;
            return null;
        }

        public CastSlot FindSlot(string role)
        {
            foreach (CastSlot s in Cast) if (s.Role == role) return s;
            return null;
        }
    }

    /// <summary>Fills {role.name}, {role.surname} and {role.full_name} in event text.</summary>
    public static class EventText
    {
        public static readonly string[] Props = { "name", "surname", "full_name" };

        /// <summary>Calls <paramref name="onToken"/> with (role, prop) for each token; a null return leaves it as is.</summary>
        public static string Replace(string text, System.Func<string, string, string> onToken)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                int close = open < 0 ? -1 : text.IndexOf('}', open);
                if (close < 0) { sb.Append(text, i, text.Length - i); break; }
                sb.Append(text, i, open - i);
                string token = text.Substring(open + 1, close - open - 1);
                int dot = token.IndexOf('.');
                string value = dot < 0 ? onToken(token, null) : onToken(token.Substring(0, dot), token.Substring(dot + 1));
                sb.Append(value ?? "{" + token + "}");
                i = close + 1;
            }
            return sb.ToString();
        }
    }
}

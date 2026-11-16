using System.Collections.Generic;

namespace Hearthline.Sim
{
    /// <summary>What caused a mark. Kept on the mark so its name can be rebuilt when it changes form.</summary>
    public sealed class MarkOrigin
    {
        /// <summary>Id of the content event that caused the mark, or null (combat hit, cheat panel).</summary>
        public string EventId;
        /// <summary>Noun phrase for who or what did it: "the boar", "a Reaver".</summary>
        public string Cause;
        /// <summary>Where it happened: "Miller's Ford". Optional.</summary>
        public string Place;

        public MarkOrigin Clone() => new MarkOrigin { EventId = EventId, Cause = Cause, Place = Place };
    }

    /// <summary>
    /// A mark instance on a character. Effects and visuals are not stored here; they are resolved from the
    /// definition (<see cref="DefId"/>) and the current <see cref="Stage"/>.
    /// </summary>
    public sealed class Mark
    {
        public int Id;
        public string DefId;
        public MarkCategory Category;
        public BodyRegion Region;
        public int Severity;
        public string Stage;
        public int StageEnteredDay;
        /// <summary>Day of the originating event. Unchanged when a wound becomes a scar or loss.</summary>
        public int DayGained;
        public MarkOrigin Origin;
        public string OriginText;
        /// <summary>For a healing wound: the outcome the infirmary treatment rolled.</summary>
        public string PendingOutcome;
    }

    public sealed class Character
    {
        public int Id;
        public string GivenName;
        public string Surname;
        public int BirthDay;
        public bool Alive = true;
        public int? DeathDay;
        public string DeathCause;
        /// <summary>Per-character overrides of a stat's base value. Missing stats use the content default.</summary>
        public SortedDictionary<string, double> BaseStats = new SortedDictionary<string, double>();
        public List<Mark> Marks = new List<Mark>();
        public List<string> Traits = new List<string>();
        /// <summary>For people the event engine generated: what kind of person they are (traveler, reaver).</summary>
        public string Archetype;
        /// <summary>For townsfolk: where they live and what they do there (shopkeeper, doctor).</summary>
        public string SettlementId;
        public string Role;
        /// <summary>Work this household member does at home. Null for none.</summary>
        public string JobId;
        /// <summary>Unique items this person carries. The dead keep theirs until someone takes them up.</summary>
        public List<GearItem> Gear = new List<GearItem>();
        /// <summary>Notable dealings with the family, oldest first. Passed on to whoever takes over their work.</summary>
        public List<Memory> Memories = new List<Memory>();
        /// <summary>Latent heritable marks this person carries without showing. They can surface in a child.</summary>
        public List<string> CarriedLatent = new List<string>();
        public HouseholdBond Bond = HouseholdBond.Sworn;
        public List<int> ParentIds = new List<int>();
        public int? PartnerId;
        /// <summary>Stepped down as head. Stays home as a mentor.</summary>
        public bool Retired;
        /// <summary>Hits a person can still take before going down.</summary>
        public int Health = LifeRules.MaxHealth;
        /// <summary>At zero health: alive, but out of the fight until revived. Left alone, it ends in death or a loss.</summary>
        public bool Downed;
        /// <summary>What this person thinks of others, by character id. -100..100.</summary>
        public SortedDictionary<int, int> Opinions = new SortedDictionary<int, int>();
    }

    public sealed class LedgerEntry { public int Day; public string Text; }

    /// <summary>One particular rifle, coat or knife. It keeps its history whoever holds it.</summary>
    public sealed class GearItem
    {
        public int Id;
        public string DefId;
        /// <summary>Engraved name, once it has earned one.</summary>
        public string Name;
        public bool Equipped;
        public int? FirstOwnerId;
        /// <summary>Held by a blood descendant of its first owner. Its heirloom effects apply.</summary>
        public bool Heirloom;
        public List<LedgerEntry> Ledger = new List<LedgerEntry>();
    }

    public sealed class Memory { public int Day; public string Text; }

    /// <summary>A faction's lasting grievance against the surname. Heirs inherit it.</summary>
    public sealed class Grudge { public string FactionId; public string Reason; public int Day; }

    public sealed class SettlementState
    {
        public string Id;
        public SortedDictionary<SettlementValue, int> Values = new SortedDictionary<SettlementValue, int>();
        /// <summary>What the family has done this season. Applied when the season turns.</summary>
        public SortedDictionary<SettlementValue, int> Pending = new SortedDictionary<SettlementValue, int>();
        public List<string> OpenServices = new List<string>();
    }

    /// <summary>One line of the family chronicle. Written by the sim as things happen; never edited.</summary>
    public sealed class ChronicleEntry
    {
        public int Day;
        public int CharacterId;
        public ChronicleKind Kind;
        public string Text;
    }

    public sealed class Household
    {
        public string Surname;
        /// <summary>Null when the head has died and no heir has taken over yet.</summary>
        public int? HeadId;
        public List<int> MemberIds = new List<int>();
        /// <summary>Every head so far, oldest first.</summary>
        public List<int> PastHeadIds = new List<int>();
        /// <summary>True once the head died with nobody eligible to follow. The campaign is over.</summary>
        public bool LineEnded;
        /// <summary>What the household owns, by item id.</summary>
        public SortedDictionary<string, int> Inventory = new SortedDictionary<string, int>();
        /// <summary>Standing with each faction, -100..100. It belongs to the surname, so heirs inherit it.</summary>
        public SortedDictionary<string, int> Reputation = new SortedDictionary<string, int>();
        public List<Grudge> Grudges = new List<Grudge>();
        /// <summary>Unique items nobody is carrying.</summary>
        public List<GearItem> GearStore = new List<GearItem>();
        /// <summary>Structure id -> tier built. 0 is not built.</summary>
        public SortedDictionary<string, int> Structures = new SortedDictionary<string, int>();
        /// <summary>How the house is holding together, 0..100.</summary>
        public int Morale = HomeRules.StartMorale;
        /// <summary>What everyone at home did last season, for the head's return.</summary>
        public List<HomeReportLine> LastReport = new List<HomeReportLine>();
    }

    /// <summary>The event the player is in the middle of. Saved, so a game can be saved mid-event.</summary>
    public sealed class ActiveEvent
    {
        public string EventId;
        public string BeatId;
        /// <summary>Cast role -> character id.</summary>
        public SortedDictionary<string, int> Cast = new SortedDictionary<string, int>();
        public string PlaceName;
        /// <summary>The settlement the event's settlement outcomes and townsfolk refer to.</summary>
        public string SettlementId;
    }

    /// <summary>The head and companions are away from the homestead. Null on <see cref="SimState.Trip"/> means everyone is home.</summary>
    public sealed class Trip
    {
        public int StartDay;
        /// <summary>The head first, then companions.</summary>
        public List<int> PartyIds = new List<int>();
        /// <summary>What the party carries. Loot found on the road lands here, and is lost if nobody comes back.</summary>
        public SortedDictionary<string, int> Pack = new SortedDictionary<string, int>();
    }

    public sealed class StorytellerState
    {
        /// <summary>Builds up while nothing happens; an event spends it.</summary>
        public double Budget;
        /// <summary>Event id -> the day it last fired, for cooldowns.</summary>
        public SortedDictionary<string, int> LastFiredDay = new SortedDictionary<string, int>();
    }

    /// <summary>
    /// The whole persistent simulation. Everything that must survive save and load is in here.
    /// Only the simulation mutates it; presentation reads it and sends commands.
    /// </summary>
    /// <remarks>
    /// Dictionaries in the state are sorted, so a save never depends on the order things happened to be
    /// inserted or removed, and two equal states always serialize to the same text.
    /// </remarks>
    public sealed class SimState
    {
        public int SaveVersion = SaveFormat.CurrentVersion;
        public int Day;
        public RngState Rng;
        public int NextCharacterId = 1;
        public int NextMarkId = 1;
        public int NextGearId = 1;
        public Household Household = new Household();
        public List<Character> Characters = new List<Character>();
        public List<ChronicleEntry> Chronicle = new List<ChronicleEntry>();
        /// <summary>Story flags set by events. Kept sorted.</summary>
        public List<string> Flags = new List<string>();
        public List<SettlementState> Settlements = new List<SettlementState>();
        public ActiveEvent ActiveEvent;
        public Trip Trip;
        public StorytellerState Storyteller = new StorytellerState();
    }
}

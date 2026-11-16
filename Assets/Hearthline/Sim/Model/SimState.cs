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
        public Dictionary<string, double> BaseStats = new Dictionary<string, double>();
        public List<Mark> Marks = new List<Mark>();
        public List<string> Traits = new List<string>();
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
        public Dictionary<int, int> Opinions = new Dictionary<int, int>();
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
    }

    /// <summary>
    /// The whole persistent simulation. Everything that must survive save and load is in here.
    /// Only the simulation mutates it; presentation reads it and sends commands.
    /// </summary>
    public sealed class SimState
    {
        public int SaveVersion = SaveFormat.CurrentVersion;
        public int Day;
        public RngState Rng;
        public int NextCharacterId = 1;
        public int NextMarkId = 1;
        public Household Household = new Household();
        public List<Character> Characters = new List<Character>();
        public List<ChronicleEntry> Chronicle = new List<ChronicleEntry>();
    }
}

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
    }

    public sealed class Household
    {
        public string Surname;
        /// <summary>Null when the head has died and no heir has taken over yet.</summary>
        public int? HeadId;
        public List<int> MemberIds = new List<int>();
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
    }
}

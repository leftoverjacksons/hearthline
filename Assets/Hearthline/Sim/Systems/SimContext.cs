using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>What a command or system needs while it runs.</summary>
    internal sealed class SimContext
    {
        public SimState State;
        public ContentDatabase Content;
        public SimRandom Rng;
        public TreatmentRules Treatment;
        public LifeRules Life;
        public StorytellerRules Storyteller;
        public WorldRules World;
        public HomeRules Home;
        public TripRules Trips;
        public GearRules Gear;
        /// <summary>Age marks that arrive on their own (gained_at_age), sorted by id.</summary>
        public List<MarkDef> AgeMarks;
        public readonly List<DomainEvent> Events = new List<DomainEvent>();

        public void Emit(DomainEvent e) => Events.Add(e);

        /// <summary>The chronicle is the family's. People met on the road are not written into it.</summary>
        public void Chronicle(Character character, ChronicleKind kind, string text)
        {
            if (!State.Household.MemberIds.Contains(character.Id)) return;
            State.Chronicle.Add(new ChronicleEntry { Day = State.Day, CharacterId = character.Id, Kind = kind, Text = text });
        }

        public Character FindCharacter(int id)
        {
            foreach (var c in State.Characters) if (c.Id == id) return c;
            return null;
        }
    }
}

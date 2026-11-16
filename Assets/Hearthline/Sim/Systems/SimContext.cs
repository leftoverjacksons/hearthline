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
        /// <summary>Age marks that arrive on their own (gained_at_age), sorted by id.</summary>
        public List<MarkDef> AgeMarks;
        public readonly List<DomainEvent> Events = new List<DomainEvent>();

        public void Emit(DomainEvent e) => Events.Add(e);

        public void Chronicle(Character character, ChronicleKind kind, string text)
        {
            State.Chronicle.Add(new ChronicleEntry { Day = State.Day, CharacterId = character.Id, Kind = kind, Text = text });
        }

        public Character FindCharacter(int id)
        {
            foreach (var c in State.Characters) if (c.Id == id) return c;
            return null;
        }
    }
}

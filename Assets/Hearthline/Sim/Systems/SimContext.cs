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
        public readonly List<DomainEvent> Events = new List<DomainEvent>();

        public void Emit(DomainEvent e) => Events.Add(e);

        public Character FindCharacter(int id)
        {
            foreach (var c in State.Characters) if (c.Id == id) return c;
            return null;
        }
    }
}

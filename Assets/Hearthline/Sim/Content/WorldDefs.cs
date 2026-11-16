using System.Collections.Generic;

namespace Hearthline.Sim.Content
{
    public sealed class ServiceDef
    {
        public string Id; public string Name;
        /// <summary>The service is open while every value is at least its minimum and at most its maximum.</summary>
        public readonly Dictionary<SettlementValue, int> Min = new Dictionary<SettlementValue, int>();
        public readonly Dictionary<SettlementValue, int> Max = new Dictionary<SettlementValue, int>();
    }

    public sealed class ResidentSlot { public string Role; public string Archetype; public int AgeMin = 25, AgeMax = 55; }

    public sealed class SettlementDef
    {
        public string Id; public string Name; public string FactionId;
        /// <summary>The settlement used when an event does not say where it is. Exactly one settlement is home.</summary>
        public bool Home;
        public readonly Dictionary<SettlementValue, int> Start = new Dictionary<SettlementValue, int>();
        /// <summary>Where each value drifts back to when the family leaves the town alone.</summary>
        public readonly Dictionary<SettlementValue, int> Baseline = new Dictionary<SettlementValue, int>();
        public readonly List<ServiceDef> Services = new List<ServiceDef>();
        public readonly List<ResidentSlot> Residents = new List<ResidentSlot>();
        public string SourceFile;
    }
}

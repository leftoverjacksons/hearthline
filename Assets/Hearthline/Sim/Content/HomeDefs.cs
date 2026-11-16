using System.Collections.Generic;

namespace Hearthline.Sim.Content
{
    public sealed class StructureTier
    {
        public string Name;
        /// <summary>Items paid to build this tier.</summary>
        public readonly Dictionary<string, int> Cost = new Dictionary<string, int>();
        /// <summary>What the homestead has while the structure is at this tier (not cumulative with lower tiers).</summary>
        public readonly Dictionary<HomeStat, int> Provides = new Dictionary<HomeStat, int>();
    }

    /// <summary>A fixed slot on the homestead that is built up through tiers. Tier 0 is "not built".</summary>
    public sealed class StructureDef
    {
        public string Id; public string Name;
        /// <summary>Tier a new homestead starts with.</summary>
        public int StartsAt;
        public readonly List<StructureTier> Tiers = new List<StructureTier>();
        public string SourceFile;
    }

    /// <summary>Work a household member does at home while the head is away.</summary>
    public sealed class JobDef
    {
        public string Id; public string Name;
        /// <summary>The stat that grows with the work and scales what it yields.</summary>
        public string Skill;
        /// <summary>Structure that must be built for the job to be done at all; higher tiers yield more. Optional.</summary>
        public string Structure;
        /// <summary>Yield per season, before skill and tier scaling.</summary>
        public readonly List<LootEntry> Produces = new List<LootEntry>();
        /// <summary>What the homestead has while someone holds the job.</summary>
        public readonly Dictionary<HomeStat, int> Provides = new Dictionary<HomeStat, int>();
        public string SourceFile;
    }
}

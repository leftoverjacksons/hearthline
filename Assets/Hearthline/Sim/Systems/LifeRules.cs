namespace Hearthline.Sim
{
    /// <summary>
    /// Numbers that govern health, birth, inheritance and death. First-guess balance values;
    /// they are rules, not state, and are not written to saves.
    /// </summary>
    public sealed class LifeRules
    {
        /// <summary>Design: you drop in 1 to 3 hits.</summary>
        public const int MaxHealth = 3;
        public const int AdultAge = 18;

        public int HealthAfterRevive = 1;
        /// <summary>Chance that someone left down dies. Otherwise their worst wound becomes a permanent loss.</summary>
        public double DownedDeathChance = 0.5;

        public int MaxParentAge = 45;
        public double BirthChancePerSeason = 0.15;
        /// <summary>Chance a newborn gets one innate trait neither parent has.</summary>
        public double MutationChance = 0.15;
        /// <summary>Chance a child shows a parent's exposure mark as an echo.</summary>
        public double EchoChance = 0.25;
        /// <summary>Chance a latent mark (the Knack) surfaces in a child of someone who has or carries it.</summary>
        public double LatentManifestChance = 0.35;
        /// <summary>If it does not surface: chance the child carries it silently, so it can skip a generation.</summary>
        public double LatentCarryChance = 0.5;

        /// <summary>Per-season chance of natural death at the first Old year, rising each year after.</summary>
        public double OldAgeDeathBase = 0.03;
        public double OldAgeDeathPerYear = 0.01;
        public const int OldAge = 66;
    }
}

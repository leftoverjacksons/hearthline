using System;

namespace Hearthline.Sim
{
    /// <summary>
    /// Weights for the infirmary treatment roll. These are balance values with first-guess defaults;
    /// they are rules, not state, and are not written to saves.
    /// </summary>
    public sealed class TreatmentRules
    {
        public const int MinTier = 1, MaxTier = 3;
        public const int MinSkill = 0, MaxSkill = 10;

        public double CleanBase = 2, CleanPerTier = 2, CleanPerSkill = 0.5;
        public double BadBase = 2, BadPerSeverity = 1;
        /// <summary>Infection weight is (InfectionTierCeiling - tier), plus the penalties below.</summary>
        public double InfectionTierCeiling = 4, InfectionIfUnstabilized = 1, InfectionIfAlreadyInfected = 2;
        public double LossIfSeverityThree = 1, LossPerSeverityWhenInfected = 1;

        /// <summary>Weights in the order: clean_scar, bad_scar, infection, loss.</summary>
        public double[] Weights(int tier, int healerSkill, int severity, string woundStage)
        {
            bool infected = woundStage == WoundStages.Infected;
            return new[]
            {
                CleanBase + CleanPerTier * tier + CleanPerSkill * healerSkill,
                BadBase + BadPerSeverity * severity,
                Math.Max(0, InfectionTierCeiling - tier)
                    + (woundStage == WoundStages.Fresh ? InfectionIfUnstabilized : 0)
                    + (infected ? InfectionIfAlreadyInfected : 0),
                (severity >= 3 ? LossIfSeverityThree : 0) + (infected ? LossPerSeverityWhenInfected * severity : 0),
            };
        }

        public static readonly string[] OutcomeOrder =
            { WoundOutcomes.CleanScar, WoundOutcomes.BadScar, WoundOutcomes.Infection, WoundOutcomes.Loss };
    }
}

namespace Hearthline.Sim
{
    public enum MarkCategory { Wound, Scar, Loss, Exposure, Knack, Rite, Age }

    public enum BodyRegion { Head, Face, Torso, ArmL, ArmR, HandL, HandR, LegL, LegR }

    public enum Heritability { None, Echo, Latent }

    public enum DamageType { Gunshot, Blade, Claw, Burn, Blunt }

    public enum LifeStage { Child, Youth, Adult, Elder, Old }

    /// <summary>Stage ids every wound definition must provide. The wound state machine is driven by the engine.</summary>
    public static class WoundStages
    {
        public const string Fresh = "fresh";
        public const string Stabilized = "stabilized";
        public const string Infected = "infected";
        public const string Healing = "healing";
        public static readonly string[] All = { Fresh, Stabilized, Infected, Healing };
    }

    /// <summary>Keys used in a wound definition's outcome table and by treatment rolls.</summary>
    public static class WoundOutcomes
    {
        public const string CleanScar = "clean_scar";
        public const string BadScar = "bad_scar";
        public const string Loss = "loss";
        /// <summary>Treatment result only: the wound turns infected instead of healing.</summary>
        public const string Infection = "infection";
        /// <summary>Transition keyword: resolve to whatever the infirmary treatment rolled.</summary>
        public const string Treated = "treated";
        public static readonly string[] TableKeys = { CleanScar, BadScar, Loss };
    }
}

namespace Hearthline.Sim
{
    public enum MarkCategory { Wound, Scar, Loss, Exposure, Knack, Rite, Age }

    public enum BodyRegion { Head, Face, Torso, ArmL, ArmR, HandL, HandR, LegL, LegR }

    public enum Heritability { None, Echo, Latent }

    public enum DamageType { Gunshot, Blade, Claw, Burn, Blunt }

    public enum LifeStage { Child, Youth, Adult, Elder, Old }

    /// <summary>How someone belongs to the household. Decides whether and when they can inherit it.</summary>
    public enum HouseholdBond { Founder, Blood, Partner, Ward, Sworn }

    /// <summary>The numbers a settlement is made of, each 0..100.</summary>
    public enum SettlementValue { Prosperity, Safety, Strangeness, Openness }

    /// <summary>What a homestead has, summed over its structures and the people working there.</summary>
    public enum HomeStat { Capacity, Morale, Treatment, Defense, Workshop, Preserving, Stabling, Hygiene, Warding }

    /// <summary>Where gear goes on a body. One item per slot.</summary>
    public enum EquipSlot { Base, Mid, Outer, Head, Hands, Feet, Accessory, LongGun, Sidearm, Knife }

    public enum TimeOfDay { Dawn, Day, Dusk, Night }

    /// <summary>The sky over the region (docs/specs/life-and-weather.md).</summary>
    public enum WeatherKind { Clear, Overcast, Fog, Rain, Storm }

    public enum ChronicleKind { Joined, Born, Mark, Partnered, Died, Succeeded, Retired, LineEnded, Deed, Left, Bequest }

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

using System;

namespace Hearthline.Sim
{
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>
    /// A calendar date derived from a day count. Day 0 is the first day of spring, year 1.
    /// Negative days are valid (births before the campaign starts).
    /// </summary>
    public readonly struct GameDate : IEquatable<GameDate>
    {
        public const int DaysPerSeason = 28;
        public const int SeasonsPerYear = 4;
        public const int DaysPerYear = DaysPerSeason * SeasonsPerYear;

        public readonly int TotalDays;

        public GameDate(int totalDays) { TotalDays = totalDays; }

        public int Year => FloorDiv(TotalDays, DaysPerYear) + 1;
        public int DayOfYear => FloorMod(TotalDays, DaysPerYear);
        public Season Season => (Season)(DayOfYear / DaysPerSeason);
        public int DayOfSeason => DayOfYear % DaysPerSeason + 1;
        public string SeasonName => Naming.ToSnake(Season.ToString());

        /// <summary>The first day of the season after this one.</summary>
        public GameDate NextSeasonStart => new GameDate(TotalDays + (DaysPerSeason - (DayOfSeason - 1)));

        public static int YearsBetween(int fromDay, int toDay) => FloorDiv(toDay - fromDay, DaysPerYear);

        public bool Equals(GameDate other) => TotalDays == other.TotalDays;
        public override bool Equals(object obj) => obj is GameDate d && Equals(d);
        public override int GetHashCode() => TotalDays;
        public override string ToString() => "day " + DayOfSeason + " of " + SeasonName + ", year " + Year;

        private static int FloorDiv(int a, int b) { int q = a / b; return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q; }
        private static int FloorMod(int a, int b) { int m = a % b; return m < 0 ? m + b : m; }
    }
}

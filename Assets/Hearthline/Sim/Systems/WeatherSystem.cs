using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>A stretch of one kind of weather, from an hour of the day until the next spell (or midnight).</summary>
    public sealed class WeatherSpell
    {
        public double FromHour;
        public WeatherKind Kind;
    }

    /// <summary>Today's weather, planned when the day begins (docs/specs/life-and-weather.md).</summary>
    public sealed class WeatherState
    {
        /// <summary>Its own random stream, so weather never reshuffles anything else in a seed.</summary>
        public ulong Seed;
        public int Day;
        /// <summary>In order of hour; the first starts at 0.</summary>
        public List<WeatherSpell> Today = new List<WeatherSpell>();
    }

    /// <summary>
    /// The weather (ADR 0025). The simulation keeps days, not hours, so each day's weather is planned as spells
    /// when the day begins: the morning (often yesterday's weather carrying on), fog lifting before noon, maybe
    /// one change in the afternoon, and a storm never lasting more than half a day. Presentation asks what the
    /// weather is at the hour its clock shows.
    /// </summary>
    public static class WeatherSystem
    {
        public const string SenseAbility = "weather_sense";

        public static void Begin(SimState state, ContentDatabase content, ulong seed)
        {
            state.Weather = new WeatherState { Seed = seed, Day = state.Day };
            state.Weather.Today = Plan(content.Weather, seed, state.Day, WeatherKind.Clear);
        }

        /// <summary>Called as each new day begins.</summary>
        internal static void NewDay(SimContext ctx)
        {
            SimState state = ctx.State;
            if (state.Weather == null)
            {
                Begin(state, ctx.Content, (ulong)state.Day * 7919UL + 17UL);
                return;
            }
            WeatherKind yesterday = Last(state.Weather);
            state.Weather.Day = state.Day;
            state.Weather.Today = Plan(ctx.Content.Weather, state.Weather.Seed, state.Day, yesterday);
            WeatherKind morning = state.Weather.Today[0].Kind;
            if (morning != yesterday) ctx.Emit(new WeatherChanged { Day = state.Day, From = yesterday, To = morning, Hour = 0 });
        }

        public static WeatherKind At(WeatherState weather, double hour)
        {
            if (weather == null || weather.Today.Count == 0) return WeatherKind.Clear;
            WeatherKind kind = weather.Today[0].Kind;
            foreach (WeatherSpell s in weather.Today) if (s.FromHour <= hour) kind = s.Kind;
            return kind;
        }

        public static WeatherKind Last(WeatherState weather) =>
            weather == null || weather.Today.Count == 0 ? WeatherKind.Clear : weather.Today[weather.Today.Count - 1].Kind;

        /// <summary>The next change after this hour today, or null if the weather holds till night.</summary>
        public static WeatherSpell NextChange(WeatherState weather, double hour)
        {
            if (weather == null) return null;
            foreach (WeatherSpell s in weather.Today) if (s.FromHour > hour) return s;
            return null;
        }

        /// <summary>The day's spells, from the seed, the day and yesterday's last weather.</summary>
        public static List<WeatherSpell> Plan(WeatherDef def, ulong seed, int day, WeatherKind yesterday)
        {
            var rng = new SimRandom(SimRandom.Seed(seed + (ulong)day * 0x9E3779B97F4A7C15UL, 61));
            Season season = new GameDate(day).Season;
            SeasonWeather sw = def.Seasons.TryGetValue(season, out SeasonWeather found) ? found : new WeatherDef().Seasons[season];
            var spells = new List<WeatherSpell>();
            void Add(double hour, WeatherKind kind)
            {
                if (hour >= 24) return;
                if (spells.Count > 0 && spells[spells.Count - 1].Kind == kind) return;
                spells.Add(new WeatherSpell { FromHour = System.Math.Round(hour, 2), Kind = kind });
            }

            // A storm doesn't carry over the night: it has blown through by morning.
            WeatherKind first = yesterday != WeatherKind.Storm && rng.Chance(sw.Persist) ? yesterday : Roll(rng, sw.Morning, null);
            Add(0, first);
            WeatherKind current = first;
            double t = 0;
            if (current == WeatherKind.Fog && rng.Chance(sw.FogLifts))
            {
                t = 8 + rng.NextDouble() * 3.5;
                current = Roll(rng, sw.Morning, k => k != WeatherKind.Fog && k != WeatherKind.Storm);
                Add(t, current);
            }
            if (current == WeatherKind.Storm) { t = StormEnds(rng, t, spells, Add, out current); }
            if (rng.Chance(sw.Change))
            {
                double at = System.Math.Max(t + 1, 12 + rng.NextDouble() * 5);
                WeatherKind was = current;
                current = Roll(rng, sw.Afternoon, k => k != was);
                Add(at, current);
                if (current == WeatherKind.Storm) StormEnds(rng, at, spells, Add, out current);
            }
            return spells;
        }

        private static double StormEnds(SimRandom rng, double from, List<WeatherSpell> spells, System.Action<double, WeatherKind> add, out WeatherKind after)
        {
            double end = from + 3 + rng.NextDouble() * 4; // at most 7 hours, well under half a day
            after = rng.Chance(0.6) ? WeatherKind.Rain : WeatherKind.Overcast;
            add(end, after);
            return end;
        }

        private static WeatherKind Roll(SimRandom rng, Dictionary<WeatherKind, double> weights, System.Func<WeatherKind, bool> allowed)
        {
            double total = 0;
            foreach (WeatherKind k in Kinds) if (Allowed(k)) total += weights.TryGetValue(k, out double w) ? w : 0;
            if (total <= 0) return WeatherKind.Overcast;
            double roll = rng.NextDouble() * total;
            WeatherKind last = WeatherKind.Overcast;
            foreach (WeatherKind k in Kinds)
            {
                if (!Allowed(k)) continue;
                double w = weights.TryGetValue(k, out double x) ? x : 0;
                if (w <= 0) continue;
                last = k;
                roll -= w;
                if (roll < 0) return k;
            }
            return last;
            bool Allowed(WeatherKind k) => allowed == null || allowed(k);
        }

        private static readonly WeatherKind[] Kinds = { WeatherKind.Clear, WeatherKind.Overcast, WeatherKind.Fog, WeatherKind.Rain, WeatherKind.Storm };

        /// <summary>The cheat: this weather from this hour for the rest of the day.</summary>
        internal static string Set(SimContext ctx, WeatherKind kind, double fromHour)
        {
            if (fromHour < 0 || fromHour >= 24) return "the hour must be 0 to 24";
            SimState state = ctx.State;
            if (state.Weather == null) Begin(state, ctx.Content, (ulong)state.Day * 7919UL + 17UL);
            WeatherKind before = At(state.Weather, fromHour);
            state.Weather.Today.RemoveAll(s => s.FromHour >= fromHour && s.FromHour > 0);
            if (fromHour <= 0) state.Weather.Today.Clear();
            state.Weather.Today.Add(new WeatherSpell { FromHour = fromHour, Kind = kind });
            ctx.Emit(new WeatherChanged { Day = state.Day, From = before, To = kind, Hour = fromHour });
            return null;
        }

        /// <summary>Hours that stand for each time of day, when presentation gives no hour.</summary>
        public static double HourOf(TimeOfDay time) => time == TimeOfDay.Dawn ? 6 : time == TimeOfDay.Dusk ? 19 : time == TimeOfDay.Night ? 23 : 13;
    }

    /// <summary>Cheat: this weather from this hour (0 = all day) until midnight.</summary>
    public sealed class SetWeather : Command
    {
        public WeatherKind Kind;
        public double FromHour;
        internal override string Apply(SimContext ctx) => WeatherSystem.Set(ctx, Kind, FromHour);
    }
}

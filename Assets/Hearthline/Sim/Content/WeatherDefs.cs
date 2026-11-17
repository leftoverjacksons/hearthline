using System;
using System.Collections.Generic;

namespace Hearthline.Sim.Content
{
    /// <summary>One season's weather (docs/specs/life-and-weather.md): what the day starts with, what may roll in after noon, and how settled it is.</summary>
    public sealed class SeasonWeather
    {
        /// <summary>Weights for the morning's weather when yesterday's doesn't carry over.</summary>
        public readonly Dictionary<WeatherKind, double> Morning = new Dictionary<WeatherKind, double>();
        /// <summary>Weights for a change in the afternoon.</summary>
        public readonly Dictionary<WeatherKind, double> Afternoon = new Dictionary<WeatherKind, double>();
        /// <summary>Chance yesterday's last weather carries into the morning.</summary>
        public double Persist = 0.5;
        /// <summary>Chance the weather changes once in the afternoon.</summary>
        public double Change = 0.35;
        /// <summary>Chance morning fog lifts by midday.</summary>
        public double FogLifts = 0.8;

        public SeasonWeather Weights(Dictionary<WeatherKind, double> into, double clear, double overcast, double fog, double rain, double storm)
        {
            into[WeatherKind.Clear] = clear; into[WeatherKind.Overcast] = overcast; into[WeatherKind.Fog] = fog;
            into[WeatherKind.Rain] = rain; into[WeatherKind.Storm] = storm;
            return this;
        }
    }

    /// <summary>The climate: <c>Content/weather.yaml</c>, or these defaults when the file is absent.</summary>
    public sealed class WeatherDef
    {
        public readonly Dictionary<Season, SeasonWeather> Seasons = new Dictionary<Season, SeasonWeather>();
        public string SourceFile;

        public WeatherDef()
        {
            SeasonWeather S(double persist, double change) => new SeasonWeather { Persist = persist, Change = change };
            var spring = S(0.55, 0.4); spring.Weights(spring.Morning, 3, 3, 2, 4, 0.5); spring.Weights(spring.Afternoon, 3, 3, 0, 4, 1);
            var summer = S(0.6, 0.35); summer.Weights(summer.Morning, 6, 2, 1, 1, 0); summer.Weights(summer.Afternoon, 3, 2, 0, 1, 3);
            var autumn = S(0.55, 0.3); autumn.Weights(autumn.Morning, 3, 3, 4, 2, 0.3); autumn.Weights(autumn.Afternoon, 3, 3, 0, 3, 0.5);
            var winter = S(0.65, 0.25); winter.Weights(winter.Morning, 2, 5, 2, 2, 0); winter.Weights(winter.Afternoon, 2, 4, 0, 3, 0.3);
            Seasons[Season.Spring] = spring; Seasons[Season.Summer] = summer; Seasons[Season.Autumn] = autumn; Seasons[Season.Winter] = winter;
        }
    }

    internal static class WeatherReader
    {
        public static void Read(YamlMap root, string path, WeatherDef def)
        {
            def.SourceFile = path;
            YamlMap seasons = root.Map("seasons", "seasons", true);
            if (seasons != null)
            {
                foreach (Season season in (Season[])Enum.GetValues(typeof(Season)))
                {
                    string key = Naming.ToSnake(season);
                    YamlMap m = seasons.Map(key, "seasons." + key);
                    if (m == null) continue;
                    var sw = new SeasonWeather
                    {
                        Persist = m.Num("persist") ?? 0.5, Change = m.Num("change") ?? 0.35, FogLifts = m.Num("fog_lifts") ?? 0.8,
                    };
                    foreach (var kv in m.EnumNumMap<WeatherKind>("morning")) sw.Morning[kv.Key] = kv.Value;
                    foreach (var kv in m.EnumNumMap<WeatherKind>("afternoon")) sw.Afternoon[kv.Key] = kv.Value;
                    foreach (double chance in new[] { sw.Persist, sw.Change, sw.FogLifts })
                        if (chance < 0 || chance > 1) m.Error("'persist', 'change' and 'fog_lifts' are chances from 0 to 1");
                    if (Total(sw.Morning) <= 0) m.Error("'morning' needs at least one weather with a weight above 0");
                    if (Total(sw.Afternoon) <= 0) m.Error("'afternoon' needs at least one weather with a weight above 0");
                    foreach (var kv in sw.Morning) if (kv.Value < 0) m.Error("weights must not be negative");
                    foreach (var kv in sw.Afternoon) if (kv.Value < 0) m.Error("weights must not be negative");
                    m.Done();
                    def.Seasons[season] = sw;
                }
                seasons.Done();
            }
            root.Done();
        }

        private static double Total(Dictionary<WeatherKind, double> weights)
        {
            double t = 0;
            foreach (double w in weights.Values) t += w;
            return t;
        }
    }
}

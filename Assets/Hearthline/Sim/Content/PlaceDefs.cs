using System;
using System.Collections.Generic;

namespace Hearthline.Sim.Content
{
    /// <summary>Where a kind of place belongs (docs/specs/ruins-and-places.md). Unset limits don't apply.</summary>
    public sealed class PlaceSiting
    {
        /// <summary>Set for kinds placed by their own rule rather than chosen among ruin sites: "overpass", "elder_works".</summary>
        public string Fixed;
        public double? SlopeMax, RoadMin, RoadMax, WaterMin, WaterMax, HomeMin;
        /// <summary>Prefers ground higher than its surroundings.</summary>
        public bool Rise;
    }

    /// <summary>The one better thing hidden at a place.</summary>
    public sealed class PlaceFind
    {
        public string Item;
        public int CountMin = 1, CountMax = 1;
        /// <summary>A stat the head needs to find it every time; below it, a seeded chance. Null: always found.</summary>
        public string CheckStat;
        public int CheckDifficulty;
        public string Text;
    }

    /// <summary>A kind of place: the fallen chapel, the filling station, the Elder Works.</summary>
    public sealed class PlaceDef
    {
        public string Id;
        /// <summary>Lower case, as it reads mid-sentence: "the fallen chapel".</summary>
        public string Name;
        public readonly List<string> AreaTags = new List<string>();
        public int ThinLevel;
        public PlaceSiting Siting = new PlaceSiting();
        public int SearchMin = 2, SearchMax = 4;
        public string Loot;
        public PlaceFind Find;
        /// <summary>What the job board's Rumours section says about the find. Null: no rumour.</summary>
        public string Rumour;
        public string SourceFile;
    }

    internal static class PlaceReader
    {
        public static PlaceDef Read(YamlMap root, string path)
        {
            var def = new PlaceDef
            {
                SourceFile = path, Id = root.Str("id"), Name = root.Str("name"), ThinLevel = root.Int("thin_level") ?? 0,
                Loot = root.Str("loot"), Rumour = root.Str("rumour", false),
            };
            def.AreaTags.AddRange(root.StrList("area_tags", true));
            if (Pair(root, "search_spots", out int lo, out int hi)) { def.SearchMin = lo; def.SearchMax = hi; }
            YamlMap siting = root.Map("siting", "siting", true);
            if (siting != null)
            {
                def.Siting.Fixed = siting.Str("fixed", false);
                def.Siting.SlopeMax = siting.Num("slope_max");
                def.Siting.RoadMin = siting.Num("road_min");
                def.Siting.RoadMax = siting.Num("road_max");
                def.Siting.WaterMin = siting.Num("water_min");
                def.Siting.WaterMax = siting.Num("water_max");
                def.Siting.HomeMin = siting.Num("home_min");
                def.Siting.Rise = siting.Bool("rise") ?? false;
                if (def.Siting.Fixed != null && def.Siting.Fixed != "overpass" && def.Siting.Fixed != "elder_works") siting.Error("'fixed' must be overpass or elder_works");
                siting.Done();
            }
            YamlMap find = root.Map("find", "find");
            if (find != null)
            {
                def.Find = new PlaceFind { Item = find.Str("item"), Text = find.Str("text") };
                if (find.Has("count"))
                {
                    if (Pair(find, "count", out int a, out int b)) { def.Find.CountMin = a; def.Find.CountMax = b; }
                }
                YamlMap check = find.Map("check", "find.check");
                if (check != null)
                {
                    def.Find.CheckStat = check.Str("stat");
                    def.Find.CheckDifficulty = check.Int("difficulty", true) ?? 1;
                    if (def.Find.CheckDifficulty < 1) check.Error("'difficulty' must be at least 1");
                    check.Done();
                }
                find.Done();
            }
            if (def.ThinLevel < 0 || def.ThinLevel > 3) root.Error("'thin_level' must be 0..3");
            if (def.SearchMin < 1 || def.SearchMax < def.SearchMin || def.SearchMax > 8) root.Error("'search_spots' must be [min, max] with 1 <= min <= max <= 8");
            if (def.Name != null && (def.Name.Length == 0 || char.IsUpper(def.Name[0]))) root.Error("'name' starts lower case, as it reads mid-sentence (\"the fallen chapel\")");
            root.Done();
            return def;
        }

        private static bool Pair(YamlMap map, string key, out int lo, out int hi)
        {
            lo = hi = 0;
            List<string> v = map.StrList(key, true);
            if (v.Count == 2 && int.TryParse(v[0], out lo) && int.TryParse(v[1], out hi)) return true;
            map.Error("'" + key + "' must be [min, max]");
            return false;
        }

        public static void Validate(ContentDatabase db, List<ContentError> errors)
        {
            var ids = new List<string>(db.Places.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                PlaceDef def = db.Places[id];
                void Err(string message) => errors.Add(new ContentError { File = def.SourceFile, Message = message });
                foreach (string tag in def.AreaTags) if (!db.Vocab.AreaTags.Contains(tag)) Err("no area tag '" + tag + "' in vocab.yaml");
                if (def.Loot != null && !db.LootTables.ContainsKey(def.Loot)) Err("no loot table '" + def.Loot + "'");
                if (def.Find != null)
                {
                    if (!db.Items.TryGetValue(def.Find.Item ?? "", out ItemDef item)) Err("no item '" + def.Find.Item + "' for the find");
                    else if (item.Equip != null && def.Find.CountMax > 1) Err("a gear find is one piece");
                    if (def.Find.CountMin < 1 || def.Find.CountMax < def.Find.CountMin) Err("find 'count' must be at least 1 and min <= max");
                    if (def.Find.CheckStat != null && !db.Vocab.Stats.ContainsKey(def.Find.CheckStat)) Err("no stat '" + def.Find.CheckStat + "' in vocab.yaml");
                }
                if (def.Rumour != null && def.Find == null) Err("a rumour needs a find to point at");
            }
        }
    }
}

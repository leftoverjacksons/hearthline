using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace Hearthline.Sim.Content
{
    /// <summary>
    /// Parses and validates the YAML in Content/. Errors are collected, not thrown, so one run reports
    /// every problem. A database with errors must not be used to run a simulation.
    /// </summary>
    public static class ContentLoader
    {
        public const string VocabFile = "vocab.yaml";
        /// <summary>Folders that will hold content later but have no schema yet. YAML found there is an error.</summary>
        private static readonly string[] FoldersWithoutSchema = { "events", "items", "factions" };
        public const string NamesFile = "names.yaml";

        public static ContentLoadResult LoadDirectory(string contentRoot)
        {
            var files = new List<ContentFile>();
            if (!Directory.Exists(contentRoot))
            {
                var missing = new ContentLoadResult();
                missing.Errors.Add(new ContentError { File = contentRoot, Message = "content folder not found" });
                return missing;
            }
            string[] paths = Directory.GetFiles(contentRoot, "*.yaml", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.Ordinal);
            foreach (string path in paths)
            {
                string relative = path.Substring(contentRoot.Length).TrimStart('/', '\\').Replace('\\', '/');
                files.Add(new ContentFile { Path = relative, Text = File.ReadAllText(path) });
            }
            return Load(files);
        }

        public static ContentLoadResult Load(IEnumerable<ContentFile> files)
        {
            var result = new ContentLoadResult();
            var db = result.Database;
            var sorted = new List<ContentFile>(files);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

            bool sawVocab = false;
            foreach (var file in sorted)
            {
                YamlMap root = ParseRoot(file, result.Errors);
                if (root == null) continue;

                string folder = file.Path.Contains("/") ? file.Path.Substring(0, file.Path.IndexOf('/')) : "";
                if (file.Path == VocabFile)
                {
                    sawVocab = true;
                    ReadVocab(root, db.Vocab);
                }
                else if (file.Path == NamesFile)
                {
                    foreach (string name in root.StrList("given_names", true))
                    {
                        if (db.GivenNames.Contains(name)) root.Error("given_names lists '" + name + "' twice");
                        else db.GivenNames.Add(name);
                    }
                    root.Done();
                }
                else if (folder == "traits")
                {
                    TraitDef def = ReadTrait(root, file.Path);
                    if (def.Id == null) continue;
                    string expected = "traits/" + def.Id + ".yaml";
                    if (file.Path != expected) root.Error("file must be named " + expected + " to match its id");
                    if (db.Traits.ContainsKey(def.Id)) root.Error("duplicate trait id '" + def.Id + "'");
                    else db.Traits[def.Id] = def;
                }
                else if (folder == "marks")
                {
                    MarkDef def = ReadMark(root, file.Path);
                    if (def.Id == null) continue;
                    string expected = "marks/" + def.Id + ".yaml";
                    if (file.Path != expected) root.Error("file must be named " + expected + " to match its id");
                    if (db.Marks.ContainsKey(def.Id)) root.Error("duplicate mark id '" + def.Id + "'");
                    else db.Marks[def.Id] = def;
                }
                else if (Array.IndexOf(FoldersWithoutSchema, folder) >= 0)
                {
                    root.Error("no schema exists yet for '" + folder + "/'; content there cannot be validated");
                }
                else
                {
                    root.Error("unexpected content file location");
                }
            }

            if (!sawVocab) result.Errors.Add(new ContentError { File = VocabFile, Message = "file is missing" });
            ContentValidator.Validate(db, result.Errors);
            return result;
        }

        private static YamlMap ParseRoot(ContentFile file, List<ContentError> errors)
        {
            try
            {
                var stream = new YamlStream();
                stream.Load(new StringReader(file.Text));
                if (stream.Documents.Count != 1)
                {
                    errors.Add(new ContentError { File = file.Path, Message = "expected exactly one YAML document" });
                    return null;
                }
                if (stream.Documents[0].RootNode is YamlMappingNode map)
                    return new YamlMap(map, file.Path, "", errors);
                errors.Add(new ContentError { File = file.Path, Message = "top level must be a mapping" });
            }
            catch (Exception e)
            {
                errors.Add(new ContentError { File = file.Path, Message = "YAML syntax error: " + e.Message });
            }
            return null;
        }

        private static void ReadVocab(YamlMap root, Vocab vocab)
        {
            foreach (YamlMap m in root.MapList("stats", "stats", true))
            {
                var stat = new StatDef { Id = m.Str("id"), Name = m.Str("name"), Base = m.Num("base", true) ?? 0 };
                m.Done();
                if (stat.Id == null) continue;
                if (!Naming.IsSnakeId(stat.Id)) m.Error("stat id '" + stat.Id + "' must be snake_case");
                if (vocab.Stats.ContainsKey(stat.Id)) m.Error("duplicate stat id '" + stat.Id + "'");
                else vocab.Stats[stat.Id] = stat;
            }
            AddIds(root, "reaction_tags", vocab.ReactionTags);
            AddIds(root, "abilities", vocab.Abilities);
            root.Done();
        }

        private static void AddIds(YamlMap root, string key, HashSet<string> target)
        {
            foreach (string id in root.StrList(key, true))
            {
                if (!Naming.IsSnakeId(id)) root.Error(key + " entry '" + id + "' must be snake_case");
                if (!target.Add(id)) root.Error(key + " lists '" + id + "' twice");
            }
        }

        private static MarkDef ReadMark(YamlMap root, string path)
        {
            var def = new MarkDef { SourceFile = path };
            def.Id = root.Str("id");
            def.Name = root.Str("name");
            def.Category = root.Enum<MarkCategory>("category") ?? MarkCategory.Scar;
            def.Regions.AddRange(root.EnumList<BodyRegion>("regions", true));
            def.Heritable = root.Enum<Heritability>("heritable") ?? Heritability.None;
            def.OriginText = root.Str("origin_text");
            def.InheritedOriginText = root.Has("inherited_origin_text") ? root.Str("inherited_origin_text") : null;
            def.GainedAtAge = root.Int("gained_at_age");
            def.InitialStage = root.Str("initial_stage");
            def.DamageTypes.AddRange(root.EnumList<DamageType>("damage_types"));

            foreach (YamlMap s in root.MapList("stages", "stages", true))
            {
                var stage = new MarkStageDef { Id = s.Str("id") };
                foreach (YamlMap e in s.MapList("effects", "stage '" + stage.Id + "' effects"))
                {
                    MarkEffect effect = ReadEffect(e);
                    if (effect != null) stage.Effects.Add(effect);
                }
                YamlMap v = s.Map("visual", "stage '" + stage.Id + "' visual");
                if (v != null)
                {
                    stage.Visual.Decal = v.Has("decal") ? v.Str("decal") : null;
                    stage.Visual.PartSwap = v.Has("part_swap") ? v.Str("part_swap") : null;
                    foreach (var kv in v.NumMap("blendshapes")) stage.Visual.Blendshapes[kv.Key] = kv.Value;
                    foreach (var kv in v.NumMap("material_params")) stage.Visual.MaterialParams[kv.Key] = kv.Value;
                    v.Done();
                }
                stage.AfterDays = s.Int("after_days");
                foreach (YamlMap t in s.MapList("then", "stage '" + stage.Id + "' then"))
                {
                    var transition = new StageTransition
                    {
                        To = t.Has("to") ? t.Str("to") : null,
                        Resolve = t.Has("resolve") ? t.Str("resolve") : null,
                        Weight = t.Num("weight") ?? 1.0,
                    };
                    if ((transition.To == null) == (transition.Resolve == null))
                        t.Error("needs exactly one of 'to' or 'resolve'");
                    else stage.Then.Add(transition);
                    if (transition.Weight <= 0) t.Error("'weight' must be positive");
                    t.Done();
                }
                s.Done();
                if (stage.Id != null) def.Stages.Add(stage);
            }

            YamlMap outcomes = root.Map("outcomes", "outcomes");
            if (outcomes != null)
            {
                foreach (string key in new List<string>(outcomes.Keys))
                {
                    var list = new List<OutcomeCandidate>();
                    foreach (YamlMap c in outcomes.MapList(key, "outcomes." + key, true))
                    {
                        var candidate = new OutcomeCandidate { DefId = c.Str("def"), SeverityDelta = c.Int("severity_delta") ?? 0 };
                        candidate.Regions.AddRange(c.EnumList<BodyRegion>("regions"));
                        c.Done();
                        if (candidate.DefId != null) list.Add(candidate);
                    }
                    def.Outcomes[key] = list;
                }
            }
            root.Done();
            return def;
        }

        private static TraitDef ReadTrait(YamlMap root, string path)
        {
            var def = new TraitDef { SourceFile = path };
            def.Id = root.Str("id");
            def.Name = root.Str("name");
            def.Description = root.Str("description");
            def.InheritChance = root.Num("inherit_chance", true) ?? 0;
            def.Innate = root.Bool("innate") ?? false;
            foreach (YamlMap e in root.MapList("effects", "effects", true))
            {
                MarkEffect effect = ReadEffect(e);
                if (effect != null) def.Effects.Add(effect);
            }
            root.Done();
            return def;
        }

        private static MarkEffect ReadEffect(YamlMap e)
        {
            int kinds = (e.Has("stat") ? 1 : 0) + (e.Has("reaction") ? 1 : 0) + (e.Has("ability") ? 1 : 0);
            if (kinds != 1)
            {
                e.Error("an effect needs exactly one of 'stat', 'reaction' or 'ability'");
                return null;
            }
            var effect = new MarkEffect();
            if (e.Has("stat"))
            {
                effect.Kind = EffectKind.Stat;
                effect.Target = e.Str("stat");
                double? add = e.Num("add"), mul = e.Num("mul");
                if (add == null && mul == null) e.Error("a stat effect needs 'add' or 'mul'");
                if (add == 0) e.Error("'add: 0' does nothing");
                if (mul == 1) e.Error("'mul: 1' does nothing");
                if (mul < 0) e.Error("'mul' must not be negative");
                effect.Add = add ?? 0;
                effect.Mul = mul ?? 1;
            }
            else if (e.Has("reaction")) { effect.Kind = EffectKind.Reaction; effect.Target = e.Str("reaction"); }
            else { effect.Kind = EffectKind.Ability; effect.Target = e.Str("ability"); }
            effect.Regions.AddRange(e.EnumList<BodyRegion>("regions"));
            e.Done();
            return effect.Target == null ? null : effect;
        }
    }
}

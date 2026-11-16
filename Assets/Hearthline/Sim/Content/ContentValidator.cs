using System;
using System.Collections.Generic;

namespace Hearthline.Sim.Content
{
    /// <summary>Cross-reference and rule checks that run after every file has been parsed.</summary>
    internal static class ContentValidator
    {
        public static void Validate(ContentDatabase db, List<ContentError> errors)
        {
            var ids = new List<string>(db.Marks.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids) ValidateMark(db, db.Marks[id], errors);
        }

        private static void ValidateMark(ContentDatabase db, MarkDef def, List<ContentError> errors)
        {
            void Err(string message) => errors.Add(new ContentError { File = def.SourceFile, Message = message });

            if (!Naming.IsSnakeId(def.Id)) Err("id '" + def.Id + "' must be snake_case");
            if (def.OriginText == null) Err("every mark needs an origin_text");
            else
            {
                string problem = OriginText.Validate(def.OriginText);
                if (problem != null) Err(problem);
            }

            var stageIds = new HashSet<string>();
            foreach (var stage in def.Stages)
                if (!stageIds.Add(stage.Id)) Err("duplicate stage id '" + stage.Id + "'");
            if (def.Stages.Count == 0) Err("a mark needs at least one stage");
            if (def.InitialStage != null && !stageIds.Contains(def.InitialStage))
                Err("initial_stage '" + def.InitialStage + "' is not a defined stage");

            bool isWound = def.Category == MarkCategory.Wound;
            foreach (var stage in def.Stages)
            {
                string where = "stage '" + stage.Id + "': ";
                foreach (var effect in stage.Effects)
                {
                    bool known =
                        effect.Kind == EffectKind.Stat ? db.Vocab.Stats.ContainsKey(effect.Target) :
                        effect.Kind == EffectKind.Reaction ? db.Vocab.ReactionTags.Contains(effect.Target) :
                        db.Vocab.Abilities.Contains(effect.Target);
                    if (!known)
                        Err(where + Naming.ToSnake(effect.Kind) + " '" + effect.Target + "' is not defined in " + ContentLoader.VocabFile);
                    foreach (var region in effect.Regions)
                        if (!def.Regions.Contains(region))
                            Err(where + "effect is limited to region '" + Naming.ToSnake(region) + "', which the mark does not allow");
                }
                // No cosmetic-only marks: every stage must do something wherever the mark can sit.
                foreach (var region in def.Regions)
                {
                    bool any = false;
                    foreach (var effect in stage.Effects) if (effect.AppliesTo(region)) { any = true; break; }
                    if (!any)
                    {
                        Err(where + "no mechanical effect applies on region '" + Naming.ToSnake(region) + "' (cosmetic-only marks are not allowed)");
                        break;
                    }
                }

                if (stage.AfterDays.HasValue && stage.AfterDays.Value < 1) Err(where + "after_days must be at least 1");
                if (stage.AfterDays.HasValue != (stage.Then.Count > 0)) Err(where + "after_days and then must be used together");
                foreach (var t in stage.Then)
                {
                    if (t.To != null)
                    {
                        if (!stageIds.Contains(t.To)) Err(where + "transition to unknown stage '" + t.To + "'");
                        else if (t.To == stage.Id) Err(where + "transition to itself");
                    }
                    else if (!isWound) Err(where + "'resolve' is only allowed on wounds");
                    else if (t.Resolve == WoundOutcomes.Treated)
                    {
                        if (stage.Id != WoundStages.Healing) Err(where + "'resolve: treated' is only allowed in the healing stage");
                    }
                    else if (Array.IndexOf(WoundOutcomes.TableKeys, t.Resolve) < 0)
                        Err(where + "unknown resolve '" + t.Resolve + "' (allowed: " + string.Join(", ", WoundOutcomes.TableKeys) + ", treated)");
                }
            }

            if (isWound) ValidateWound(db, def, stageIds, Err);
            else
            {
                if (def.DamageTypes.Count > 0) Err("damage_types is only allowed on wounds");
                if (def.Outcomes.Count > 0) Err("outcomes is only allowed on wounds");
                CheckReachable(def, Err);
            }
        }

        private static void ValidateWound(ContentDatabase db, MarkDef def, HashSet<string> stageIds, Action<string> err)
        {
            if (def.DamageTypes.Count == 0) err("a wound needs damage_types");
            if (def.InitialStage != null && def.InitialStage != WoundStages.Fresh) err("a wound's initial_stage must be 'fresh'");
            if (def.Heritable != Heritability.None) err("a wound cannot be heritable");
            foreach (string required in WoundStages.All)
                if (!stageIds.Contains(required)) err("a wound needs a '" + required + "' stage");
            foreach (var stage in def.Stages)
            {
                if (Array.IndexOf(WoundStages.All, stage.Id) < 0) err("wound stage '" + stage.Id + "' is not one of: " + string.Join(", ", WoundStages.All));
                else if (!stage.AfterDays.HasValue) err("wound stage '" + stage.Id + "' needs after_days and then (wounds never stay as they are)");
                if (stage.Id == WoundStages.Healing && !(stage.Then.Count == 1 && stage.Then[0].Resolve == WoundOutcomes.Treated))
                    err("the healing stage must have exactly: then: [{ resolve: treated }]");
            }

            foreach (string key in def.Outcomes.Keys)
                if (Array.IndexOf(WoundOutcomes.TableKeys, key) < 0)
                    err("unknown outcome '" + key + "' (allowed: " + string.Join(", ", WoundOutcomes.TableKeys) + ")");

            foreach (string key in new[] { WoundOutcomes.CleanScar, WoundOutcomes.BadScar })
            {
                bool hasFallback = false;
                if (def.Outcomes.TryGetValue(key, out var list))
                    foreach (var c in list) if (c.Regions.Count == 0) hasFallback = true;
                if (!hasFallback) err("outcome '" + key + "' needs a candidate without a regions filter, so every wound can resolve");
            }

            foreach (var kv in def.Outcomes)
            {
                foreach (var candidate in kv.Value)
                {
                    MarkDef target = db.FindMark(candidate.DefId);
                    string where = "outcome '" + kv.Key + "' -> '" + candidate.DefId + "': ";
                    if (target == null) { err(where + "no such mark"); continue; }
                    bool lossKey = kv.Key == WoundOutcomes.Loss;
                    if (lossKey && target.Category != MarkCategory.Loss) err(where + "must be a loss mark");
                    if (!lossKey && target.Category != MarkCategory.Scar) err(where + "must be a scar mark");
                    var reachable = candidate.Regions.Count > 0 ? candidate.Regions : def.Regions;
                    foreach (var region in reachable)
                    {
                        if (candidate.Regions.Count > 0 && !def.Regions.Contains(region))
                            err(where + "filters on region '" + Naming.ToSnake(region) + "', which the wound does not allow");
                        if (!target.Regions.Contains(region))
                            err(where + "target does not allow region '" + Naming.ToSnake(region) + "'");
                    }
                }
            }
        }

        private static void CheckReachable(MarkDef def, Action<string> err)
        {
            if (def.InitialStage == null || def.FindStage(def.InitialStage) == null) return;
            var seen = new HashSet<string> { def.InitialStage };
            var queue = new Queue<string>();
            queue.Enqueue(def.InitialStage);
            while (queue.Count > 0)
            {
                var stage = def.FindStage(queue.Dequeue());
                if (stage == null) continue;
                foreach (var t in stage.Then)
                    if (t.To != null && seen.Add(t.To)) queue.Enqueue(t.To);
            }
            foreach (var stage in def.Stages)
                if (!seen.Contains(stage.Id)) err("stage '" + stage.Id + "' can never be reached from initial_stage");
        }
    }
}

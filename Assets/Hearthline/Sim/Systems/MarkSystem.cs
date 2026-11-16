using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Adding marks, moving them through stages over time, and turning wounds into scars or losses.</summary>
    internal static class MarkSystem
    {
        public const int MinSeverity = 1, MaxSeverity = 3;

        /// <summary>Returns why the mark cannot be added, or null if it can.</summary>
        public static string CheckCanAdd(MarkDef def, BodyRegion region, int severity, MarkOrigin origin)
        {
            if (!def.Regions.Contains(region))
                return "mark '" + def.Id + "' cannot be on region '" + Naming.ToSnake(region) + "'";
            if (severity < MinSeverity || severity > MaxSeverity)
                return "severity must be " + MinSeverity + ".." + MaxSeverity;
            if (OriginText.RequiresCause(def.OriginText) && string.IsNullOrWhiteSpace(origin?.Cause))
                return "mark '" + def.Id + "' needs an origin cause; every mark is named by where it came from";
            return null;
        }

        public static Mark Add(SimContext ctx, Character character, MarkDef def, BodyRegion region, int severity, MarkOrigin origin)
        {
            int day = ctx.State.Day;
            origin = origin?.Clone() ?? new MarkOrigin();
            var mark = new Mark
            {
                Id = ctx.State.NextMarkId++,
                DefId = def.Id,
                Category = def.Category,
                Region = region,
                Severity = severity,
                Stage = def.InitialStage,
                StageEnteredDay = day,
                DayGained = day,
                Origin = origin,
                OriginText = OriginText.Format(def.OriginText, origin, day),
            };
            character.Marks.Add(mark);
            ctx.Emit(new MarkAdded { Day = day, CharacterId = character.Id, MarkId = mark.Id, DefId = def.Id });
            return mark;
        }

        public static void SetStage(SimContext ctx, Character character, Mark mark, string stage, int day)
        {
            string from = mark.Stage;
            mark.Stage = stage;
            mark.StageEnteredDay = day;
            ctx.Emit(new MarkStageChanged { Day = day, CharacterId = character.Id, MarkId = mark.Id, FromStage = from, ToStage = stage });
        }

        /// <summary>Fires every stage timer that has come due on or before the current day.</summary>
        public static void Tick(SimContext ctx, Character character)
        {
            if (!character.Alive) return;
            foreach (Mark mark in character.Marks.ToArray())
            {
                // A mark can pass through several stages in one tick after a long skip; the cap guards against bad data.
                for (int guard = 0; guard < 32; guard++)
                {
                    MarkStageDef stage = ctx.Content.FindMark(mark.DefId)?.FindStage(mark.Stage);
                    if (stage == null || !stage.AfterDays.HasValue || stage.Then.Count == 0) break;
                    int due = mark.StageEnteredDay + stage.AfterDays.Value;
                    if (due > ctx.State.Day) break;

                    var weights = new double[stage.Then.Count];
                    for (int i = 0; i < weights.Length; i++) weights[i] = stage.Then[i].Weight;
                    StageTransition chosen = stage.Then[weights.Length == 1 ? 0 : ctx.Rng.PickWeighted(weights)];

                    if (chosen.To != null) SetStage(ctx, character, mark, chosen.To, due);
                    else
                    {
                        string outcome = chosen.Resolve == WoundOutcomes.Treated
                            ? (mark.PendingOutcome ?? WoundOutcomes.BadScar)
                            : chosen.Resolve;
                        if (!ResolveWound(ctx, character, mark, outcome, due)) break;
                    }
                }
            }
        }

        /// <summary>Turns a wound into the scar or loss its outcome table names. The mark keeps its id, origin and date.</summary>
        public static bool ResolveWound(SimContext ctx, Character character, Mark mark, string outcome, int day)
        {
            MarkDef wound = ctx.Content.FindMark(mark.DefId);
            if (wound == null) return false;

            OutcomeCandidate candidate = FindCandidate(wound, outcome, mark.Region);
            if (candidate == null && outcome != WoundOutcomes.BadScar)
            {
                // e.g. a loss was rolled but nothing can be lost from this region.
                outcome = WoundOutcomes.BadScar;
                candidate = FindCandidate(wound, outcome, mark.Region);
            }
            MarkDef target = candidate == null ? null : ctx.Content.FindMark(candidate.DefId);
            if (target == null) return false;

            string fromDef = mark.DefId;
            mark.DefId = target.Id;
            mark.Category = target.Category;
            mark.Stage = target.InitialStage;
            mark.StageEnteredDay = day;
            mark.Severity = Math.Max(MinSeverity, Math.Min(MaxSeverity, mark.Severity + candidate.SeverityDelta));
            mark.PendingOutcome = null;
            mark.OriginText = OriginText.Format(target.OriginText, mark.Origin, mark.DayGained);
            ctx.Emit(new MarkTransformed { Day = day, CharacterId = character.Id, MarkId = mark.Id, FromDefId = fromDef, ToDefId = target.Id, Outcome = outcome });
            return true;
        }

        private static OutcomeCandidate FindCandidate(MarkDef wound, string outcome, BodyRegion region)
        {
            if (!wound.Outcomes.TryGetValue(outcome, out List<OutcomeCandidate> list)) return null;
            foreach (var c in list)
                if (c.Regions.Count == 0 || c.Regions.Contains(region)) return c;
            return null;
        }

        /// <summary>The wound a hit of this kind causes on this region, or null. Ties are broken by id for determinism.</summary>
        public static MarkDef FindWoundFor(ContentDatabase content, DamageType damage, BodyRegion region)
        {
            MarkDef best = null;
            foreach (MarkDef def in content.Marks.Values)
            {
                if (def.Category != MarkCategory.Wound || !def.DamageTypes.Contains(damage) || !def.Regions.Contains(region)) continue;
                if (best == null || string.CompareOrdinal(def.Id, best.Id) < 0) best = def;
            }
            return best;
        }
    }
}

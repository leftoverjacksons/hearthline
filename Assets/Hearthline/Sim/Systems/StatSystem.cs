using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>Derives what a character can do from their base stats and the marks they carry.</summary>
    internal static class StatSystem
    {
        public static IEnumerable<MarkEffect> ActiveEffects(ContentDatabase content, Character character)
        {
            foreach (string traitId in character.Traits)
            {
                TraitDef trait = content.FindTrait(traitId);
                if (trait == null) continue;
                foreach (MarkEffect effect in trait.Effects) yield return effect;
            }
            foreach (Mark mark in character.Marks)
            {
                MarkStageDef stage = content.FindMark(mark.DefId)?.FindStage(mark.Stage);
                if (stage == null) continue;
                foreach (MarkEffect effect in stage.Effects)
                    if (effect.AppliesTo(mark.Region)) yield return effect;
            }
        }

        /// <summary>(base + sum of adds) * product of muls.</summary>
        public static double GetStat(ContentDatabase content, Character character, string statId)
        {
            double value = character.BaseStats.TryGetValue(statId, out double own) ? own
                : content.Vocab.Stats.TryGetValue(statId, out StatDef def) ? def.Base : 0;
            double mul = 1;
            foreach (MarkEffect effect in ActiveEffects(content, character))
            {
                if (effect.Kind != EffectKind.Stat || effect.Target != statId) continue;
                value += effect.Add;
                mul *= effect.Mul;
            }
            return value * mul;
        }

        public static SortedSet<string> Collect(ContentDatabase content, Character character, EffectKind kind)
        {
            var result = new SortedSet<string>(System.StringComparer.Ordinal);
            foreach (MarkEffect effect in ActiveEffects(content, character))
                if (effect.Kind == kind) result.Add(effect.Target);
            return result;
        }
    }
}

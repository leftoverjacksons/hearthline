using System.Collections.Generic;

namespace Hearthline.Sim.Content
{
    public sealed class StatDef
    {
        public string Id;
        public string Name;
        public double Base;
    }

    /// <summary>The closed vocabularies content may reference (Content/vocab.yaml).</summary>
    public sealed class Vocab
    {
        public readonly Dictionary<string, StatDef> Stats = new Dictionary<string, StatDef>();
        public readonly HashSet<string> ReactionTags = new HashSet<string>();
        public readonly HashSet<string> Abilities = new HashSet<string>();
    }

    public enum EffectKind { Stat, Reaction, Ability }

    public sealed class MarkEffect
    {
        public EffectKind Kind;
        /// <summary>Stat id, reaction tag or ability id, depending on <see cref="Kind"/>.</summary>
        public string Target;
        public double Add;
        public double Mul = 1.0;
        /// <summary>If non-empty, the effect applies only when the mark is on one of these regions.</summary>
        public readonly List<BodyRegion> Regions = new List<BodyRegion>();

        public bool AppliesTo(BodyRegion region) => Regions.Count == 0 || Regions.Contains(region);
    }

    /// <summary>Asset keys for the bridge layer. The sim never interprets them.</summary>
    public sealed class MarkVisual
    {
        public string Decal;
        public string PartSwap;
        public readonly Dictionary<string, double> Blendshapes = new Dictionary<string, double>();
        public readonly Dictionary<string, double> MaterialParams = new Dictionary<string, double>();
    }

    public sealed class StageTransition
    {
        /// <summary>Stage to move to. Exactly one of <see cref="To"/> and <see cref="Resolve"/> is set.</summary>
        public string To;
        /// <summary>Wounds only: outcome key to resolve through the outcome table, or "treated".</summary>
        public string Resolve;
        public double Weight = 1.0;
    }

    public sealed class MarkStageDef
    {
        public string Id;
        public readonly List<MarkEffect> Effects = new List<MarkEffect>();
        public MarkVisual Visual = new MarkVisual();
        /// <summary>Days in this stage before one of <see cref="Then"/> fires. Null means the stage is stable.</summary>
        public int? AfterDays;
        public readonly List<StageTransition> Then = new List<StageTransition>();
    }

    public sealed class OutcomeCandidate
    {
        public string DefId;
        /// <summary>If non-empty, this candidate is used only for wounds on these regions.</summary>
        public readonly List<BodyRegion> Regions = new List<BodyRegion>();
        public int SeverityDelta;
    }

    public sealed class MarkDef
    {
        public string Id;
        public string Name;
        public MarkCategory Category;
        public readonly List<BodyRegion> Regions = new List<BodyRegion>();
        public Heritability Heritable;
        public string OriginText;
        public string InitialStage;
        public readonly List<MarkStageDef> Stages = new List<MarkStageDef>();
        /// <summary>Wounds only: the kinds of hit that cause this wound.</summary>
        public readonly List<DamageType> DamageTypes = new List<DamageType>();
        /// <summary>Wounds only: outcome key -> candidates, first region match wins.</summary>
        public readonly Dictionary<string, List<OutcomeCandidate>> Outcomes = new Dictionary<string, List<OutcomeCandidate>>();
        public string SourceFile;

        public MarkStageDef FindStage(string id)
        {
            foreach (var s in Stages) if (s.Id == id) return s;
            return null;
        }
    }

    public sealed class ContentDatabase
    {
        public readonly Vocab Vocab = new Vocab();
        public readonly Dictionary<string, MarkDef> Marks = new Dictionary<string, MarkDef>();

        public MarkDef FindMark(string id)
        {
            return id != null && Marks.TryGetValue(id, out var def) ? def : null;
        }
    }

    public sealed class ContentFile
    {
        /// <summary>Path relative to the Content folder, with forward slashes: "marks/scar_claw.yaml".</summary>
        public string Path;
        public string Text;
    }

    public sealed class ContentError
    {
        public string File;
        public string Message;
        public override string ToString() => File + ": " + Message;
    }

    public sealed class ContentLoadResult
    {
        public ContentDatabase Database = new ContentDatabase();
        public readonly List<ContentError> Errors = new List<ContentError>();
        public bool Ok => Errors.Count == 0;
    }
}

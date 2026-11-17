using System;
using System.Collections.Generic;

namespace Hearthline.Sim.Content
{
    /// <summary>How the hunted animal notices you (metres, degrees). Presentation values, kept with the animal.</summary>
    public sealed class HuntSenses
    {
        public double SightRange = 45, SightAngle = 140;
        public double HearCrouched = 5, HearWalking = 14, HearRunning = 28, HearGunshot = 70;
    }

    /// <summary>A hunt the job board offers (docs/specs/hunt.md): one particular animal, its trail and what it yields.</summary>
    public sealed class HuntDef
    {
        public string Id;
        public string Name;
        /// <summary>What the job board says about it.</summary>
        public string Offer;
        public string AnimalArchetype;
        public string AnimalName;
        /// <summary>Signs to read before the animal can be found.</summary>
        public int Sign;
        /// <summary>Signs lost when it bolts.</summary>
        public int BoltCost;
        public readonly List<string> AreaTagsAny = new List<string>();
        public string MeatItem;
        public int MeatCount, MeatWithKnife;
        public readonly Dictionary<string, int> Items = new Dictionary<string, int>();
        public string Chronicle;
        public HuntSenses Senses = new HuntSenses();
        public string SourceFile;
    }

    internal static class HuntReader
    {
        public static HuntDef Read(YamlMap root, string path)
        {
            var def = new HuntDef
            {
                SourceFile = path, Id = root.Str("id"), Name = root.Str("name"), Offer = root.Str("offer"),
                Sign = root.Int("sign", true) ?? 0, BoltCost = root.Int("bolt_cost") ?? 0, Chronicle = root.Str("chronicle"),
            };
            YamlMap animal = root.Map("animal", "animal", true);
            if (animal != null)
            {
                def.AnimalArchetype = animal.Str("archetype");
                def.AnimalName = animal.Str("name");
                animal.Done();
            }
            def.AreaTagsAny.AddRange(root.StrList("area_tags_any", true));
            YamlMap yields = root.Map("yields", "yields", true);
            if (yields != null)
            {
                YamlMap meat = yields.Map("meat", "yields.meat");
                if (meat != null)
                {
                    def.MeatItem = meat.Str("item");
                    def.MeatCount = meat.Int("count", true) ?? 0;
                    def.MeatWithKnife = meat.Int("with_knife") ?? 0;
                    meat.Done();
                }
                foreach (var kv in yields.NumMap("items")) def.Items[kv.Key] = (int)kv.Value;
                yields.Done();
            }
            YamlMap senses = root.Map("senses", "senses");
            if (senses != null)
            {
                def.Senses.SightRange = senses.Num("sight_range") ?? def.Senses.SightRange;
                def.Senses.SightAngle = senses.Num("sight_angle") ?? def.Senses.SightAngle;
                YamlMap hearing = senses.Map("hearing", "senses.hearing");
                if (hearing != null)
                {
                    def.Senses.HearCrouched = hearing.Num("crouched") ?? def.Senses.HearCrouched;
                    def.Senses.HearWalking = hearing.Num("walking") ?? def.Senses.HearWalking;
                    def.Senses.HearRunning = hearing.Num("running") ?? def.Senses.HearRunning;
                    def.Senses.HearGunshot = hearing.Num("gunshot") ?? def.Senses.HearGunshot;
                    hearing.Done();
                }
                senses.Done();
            }
            if (def.Sign < 1) root.Error("'sign' must be at least 1");
            if (def.BoltCost < 0) root.Error("'bolt_cost' must not be negative");
            if (def.MeatCount < 0 || def.MeatWithKnife < 0) root.Error("meat counts must not be negative");
            root.Done();
            return def;
        }

        public static void Validate(ContentDatabase db, List<ContentError> errors)
        {
            var ids = new List<string>(db.Hunts.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                HuntDef def = db.Hunts[id];
                void Err(string message) => errors.Add(new ContentError { File = def.SourceFile, Message = message });
                if (def.AnimalArchetype != null && !db.Vocab.Archetypes.Contains(def.AnimalArchetype)) Err("no archetype '" + def.AnimalArchetype + "' in vocab.yaml");
                foreach (string tag in def.AreaTagsAny) if (!db.Vocab.AreaTags.Contains(tag)) Err("no area tag '" + tag + "' in vocab.yaml");
                if (def.MeatItem != null && !IsGoods(db, def.MeatItem)) Err("meat '" + def.MeatItem + "' is not a stackable item");
                foreach (var kv in def.Items)
                {
                    if (!IsGoods(db, kv.Key)) Err("yield '" + kv.Key + "' is not a stackable item");
                    if (kv.Value < 1) Err("yield '" + kv.Key + "' must be at least 1");
                }
                if (def.MeatItem == null && def.Items.Count == 0) Err("a hunt must yield something");
                if (def.Chronicle != null && !def.Chronicle.Contains("{hero.name}")) Err("the chronicle line must name {hero.name}");
            }
        }

        private static bool IsGoods(ContentDatabase db, string id) => db.Items.TryGetValue(id, out ItemDef item) && item.Equip == null;
    }
}

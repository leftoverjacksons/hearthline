using System;
using System.Collections.Generic;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using NUnit.Framework;

namespace Hearthline.Tests.ContentValidation
{
    /// <summary>The real files in Content/ must load without a single error.</summary>
    public class ContentValidationTests
    {
        [Test]
        public void AllContentValidates()
        {
            ContentLoadResult result = ContentLoader.LoadDirectory(TestSupport.ContentRoot);
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
        }

        [Test]
        public void EveryListedVisualIsUsed()
        {
            // vocab.yaml's visuals are the art deliverables list; a key nothing uses is a stale deliverable.
            ContentDatabase db = ContentLoader.LoadDirectory(TestSupport.ContentRoot).Database;
            var used = new HashSet<string>();
            foreach (MarkDef mark in db.Marks.Values)
                foreach (MarkStageDef stage in mark.Stages)
                {
                    if (stage.Visual.Decal != null) used.Add("decal " + stage.Visual.Decal);
                    if (stage.Visual.PartSwap != null) used.Add("part_swap " + stage.Visual.PartSwap);
                    foreach (string k in stage.Visual.MaterialParams.Keys) used.Add("material_param " + k);
                    foreach (string k in stage.Visual.Blendshapes.Keys) used.Add("blendshape " + k);
                }
            foreach (ItemDef item in db.Items.Values)
                if (item.Equip?.Visual != null) used.Add("gear " + item.Equip.Visual);

            var unused = new List<string>();
            void Check(string kind, IEnumerable<string> keys) { foreach (string k in keys) if (!used.Contains(kind + " " + k)) unused.Add(kind + " " + k); }
            Check("decal", db.Vocab.Decals);
            Check("part_swap", db.Vocab.PartSwaps);
            Check("material_param", db.Vocab.MaterialParams);
            Check("blendshape", db.Vocab.Blendshapes);
            Check("gear", db.Vocab.GearVisuals);
            Assert.That(unused, Is.Empty, "Listed in vocab.yaml visuals but used by nothing");
        }

        [Test]
        public void EveryMarkCategoryHasAtLeastOneDefinition()
        {
            var seen = new HashSet<MarkCategory>();
            foreach (MarkDef def in TestSupport.Content.Marks.Values) seen.Add(def.Category);
            foreach (MarkCategory category in (MarkCategory[])Enum.GetValues(typeof(MarkCategory)))
                Assert.That(seen, Does.Contain(category));
        }

        [Test]
        public void EveryDamageTypeCanWoundEveryRegion()
        {
            foreach (DamageType damage in (DamageType[])Enum.GetValues(typeof(DamageType)))
                foreach (BodyRegion region in (BodyRegion[])Enum.GetValues(typeof(BodyRegion)))
                {
                    bool found = false;
                    foreach (MarkDef def in TestSupport.Content.Marks.Values)
                        if (def.Category == MarkCategory.Wound && def.DamageTypes.Contains(damage) && def.Regions.Contains(region)) found = true;
                    Assert.That(found, Is.True, "No wound for " + damage + " on " + region);
                }
        }

        [Test]
        public void EveryScarAndLossIsReachableFromSomeWound()
        {
            var produced = new HashSet<string>();
            foreach (MarkDef def in TestSupport.Content.Marks.Values)
                foreach (List<OutcomeCandidate> list in def.Outcomes.Values)
                    foreach (OutcomeCandidate c in list) produced.Add(c.DefId);
            foreach (MarkDef def in TestSupport.Content.Marks.Values)
                if (def.Category == MarkCategory.Scar || def.Category == MarkCategory.Loss)
                    Assert.That(produced, Does.Contain(def.Id), def.Id + " is never the outcome of any wound");
        }
    }
}

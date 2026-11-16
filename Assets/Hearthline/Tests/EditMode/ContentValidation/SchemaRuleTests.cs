using System.Collections.Generic;
using Hearthline.Sim.Content;
using NUnit.Framework;

namespace Hearthline.Tests.ContentValidation
{
    /// <summary>Each schema rule rejects the content it is meant to reject.</summary>
    public class SchemaRuleTests
    {
        private const string Vocab = @"
stats:
  - { id: stamina, name: Stamina, base: 1 }
reaction_tags: [scarred, bleeding]
abilities: [night_sight]
";

        private const string Scar = @"
id: scar_test
name: Test scar
category: scar
regions: [face, torso]
heritable: none
origin_text: ""Scar from {cause}""
initial_stage: scarred
stages:
  - id: scarred
    effects: [{ reaction: scarred }]
    visual: { decal: scar_test }
";

        private const string Wound = @"
id: wound_test
name: Test wound
category: wound
regions: [face, torso]
heritable: none
damage_types: [claw]
origin_text: ""Clawed by {cause}""
initial_stage: fresh
stages:
  - id: fresh
    effects: [{ reaction: bleeding }]
    after_days: 2
    then: [{ to: infected }]
  - id: stabilized
    effects: [{ reaction: bleeding }]
    after_days: 5
    then: [{ to: infected }]
  - id: infected
    effects: [{ reaction: bleeding }]
    after_days: 5
    then: [{ resolve: bad_scar }]
  - id: healing
    effects: [{ reaction: bleeding }]
    after_days: 5
    then: [{ resolve: treated }]
outcomes:
  clean_scar: [{ def: scar_test }]
  bad_scar: [{ def: scar_test }]
";

        private static ContentLoadResult Load(string scar = Scar, string wound = Wound, string scarPath = "marks/scar_test.yaml", string extraPath = null, string extraText = null)
        {
            var files = new List<ContentFile>
            {
                new ContentFile { Path = "vocab.yaml", Text = Vocab },
                new ContentFile { Path = scarPath, Text = scar },
            };
            if (wound != null) files.Add(new ContentFile { Path = "marks/wound_test.yaml", Text = wound });
            if (extraPath != null) files.Add(new ContentFile { Path = extraPath, Text = extraText });
            return ContentLoader.Load(files);
        }

        private static void AssertError(ContentLoadResult result, string expected)
        {
            foreach (ContentError e in result.Errors)
                if (e.Message.Contains(expected)) return;
            Assert.Fail("Expected an error containing \"" + expected + "\" but got:\n" + string.Join("\n", result.Errors));
        }

        [Test]
        public void BaselineFixtureIsValid()
        {
            ContentLoadResult result = Load();
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(result.Database.Marks.Count, Is.EqualTo(2));
        }

        [Test]
        public void UnknownFieldIsRejected() =>
            AssertError(Load(Scar + "flavor: gnarly\n"), "unknown field 'flavor'");

        [Test]
        public void UnknownFieldInsideAnEffectIsRejected() =>
            AssertError(Load(Scar.Replace("{ reaction: scarred }", "{ reaction: scarred, strength: 2 }")), "unknown field 'strength'");

        [Test]
        public void MissingOriginTextIsRejected() =>
            AssertError(Load(Scar.Replace("origin_text: \"Scar from {cause}\"", "")), "origin_text");

        [Test]
        public void CosmeticOnlyMarkIsRejected() =>
            AssertError(Load(Scar.Replace("effects: [{ reaction: scarred }]", "effects: []")), "cosmetic-only");

        [Test]
        public void EffectThatMissesAnAllowedRegionIsRejected() =>
            AssertError(Load(Scar.Replace("{ reaction: scarred }", "{ reaction: scarred, regions: [face] }")), "no mechanical effect applies on region 'torso'");

        [Test]
        public void UndefinedStatIsRejected() =>
            AssertError(Load(Scar.Replace("{ reaction: scarred }", "{ stat: grit, add: 1 }")), "stat 'grit' is not defined");

        [Test]
        public void UndefinedReactionTagIsRejected() =>
            AssertError(Load(Scar.Replace("reaction: scarred", "reaction: sparkly")), "reaction 'sparkly' is not defined");

        [Test]
        public void StatEffectThatDoesNothingIsRejected() =>
            AssertError(Load(Scar.Replace("{ reaction: scarred }", "{ stat: stamina, mul: 1 }")), "does nothing");

        [Test]
        public void UnknownRegionIsRejected() =>
            AssertError(Load(Scar.Replace("[face, torso]", "[face, tail]")), "unknown value 'tail'");

        [Test]
        public void UnknownCategoryIsRejected() =>
            AssertError(Load(Scar.Replace("category: scar", "category: tattoo")), "unknown value 'tattoo'");

        [Test]
        public void FileNameMustMatchId() =>
            AssertError(Load(scarPath: "marks/some_scar.yaml"), "file must be named marks/scar_test.yaml");

        [Test]
        public void DuplicateIdIsRejected() =>
            AssertError(Load(extraPath: "marks/scar_copy.yaml", extraText: Scar), "duplicate mark id");

        [Test]
        public void IdMustBeSnakeCase() =>
            AssertError(Load(Scar.Replace("id: scar_test", "id: ScarTest"), scarPath: "marks/ScarTest.yaml", wound: null), "snake_case");

        [Test]
        public void InitialStageMustExist() =>
            AssertError(Load(Scar.Replace("initial_stage: scarred", "initial_stage: fresh")), "initial_stage 'fresh' is not a defined stage");

        [Test]
        public void TransitionToUnknownStageIsRejected() =>
            AssertError(Load(Scar + "    after_days: 3\n    then: [{ to: nowhere }]\n"), "unknown stage 'nowhere'");

        [Test]
        public void UnreachableStageIsRejected() =>
            AssertError(Load(Scar + "  - id: orphan\n    effects: [{ reaction: scarred }]\n"), "can never be reached");

        [Test]
        public void ResolveOutsideAWoundIsRejected() =>
            AssertError(Load(Scar + "    after_days: 3\n    then: [{ resolve: bad_scar }]\n"), "only allowed on wounds");

        [Test]
        public void WoundMissingAStageIsRejected() =>
            AssertError(Load(wound: Wound.Replace("id: stabilized", "id: fresh2")), "a wound needs a 'stabilized' stage");

        [Test]
        public void WoundOutcomeMustPointAtAnExistingMark() =>
            AssertError(Load(wound: Wound.Replace("clean_scar: [{ def: scar_test }]", "clean_scar: [{ def: scar_missing }]")), "no such mark");

        [Test]
        public void WoundNeedsAnUnfilteredScarOutcome() =>
            AssertError(Load(wound: Wound.Replace("bad_scar: [{ def: scar_test }]", "bad_scar: [{ def: scar_test, regions: [face] }]")), "needs a candidate without a regions filter");

        [Test]
        public void WoundOutcomeTargetMustAllowTheRegion() =>
            AssertError(Load(Scar.Replace("[face, torso]", "[face]")), "target does not allow region 'torso'");

        [Test]
        public void LossOutcomeMustBeALossMark() =>
            AssertError(Load(wound: Wound + "  loss: [{ def: scar_test }]\n"), "must be a loss mark");

        [Test]
        public void YamlSyntaxErrorIsReportedNotThrown() =>
            AssertError(Load("id: [unclosed"), "YAML syntax error");

        [Test]
        public void ContentInAFolderWithoutASchemaIsRejected() =>
            AssertError(Load(extraPath: "events/some_event.yaml", extraText: "id: some_event\n"), "no schema exists yet");

        [Test]
        public void MissingVocabIsRejected()
        {
            ContentLoadResult result = ContentLoader.Load(new[] { new ContentFile { Path = "marks/scar_test.yaml", Text = Scar } });
            Assert.That(result.Ok, Is.False);
        }
    }
}

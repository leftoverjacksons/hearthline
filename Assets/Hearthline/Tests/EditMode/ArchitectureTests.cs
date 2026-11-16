using System.IO;
using NUnit.Framework;

namespace Hearthline.Tests
{
    /// <summary>Guards the architecture rules in CLAUDE.md that can be checked from source text.</summary>
    public class ArchitectureTests
    {
        private static readonly string SimFolder = Path.Combine(TestSupport.RepoRoot, "Assets", "Hearthline", "Sim");

        [TestCase("UnityEngine", "rule 1: the sim is pure C#")]
        [TestCase("UnityEditor", "rule 1: the sim is pure C#")]
        [TestCase("System.Random", "rule 5: randomness goes through SimRandom")]
        [TestCase("new Random(", "rule 5: randomness goes through SimRandom")]
        [TestCase("DateTime.Now", "the sim must be deterministic")]
        [TestCase("Guid.NewGuid", "the sim must be deterministic")]
        public void SimSourceNeverMentions(string forbidden, string why)
        {
            string[] files = Directory.GetFiles(SimFolder, "*.cs", SearchOption.AllDirectories);
            Assert.That(files, Is.Not.Empty);
            foreach (string file in files)
                Assert.That(File.ReadAllText(file), Does.Not.Contain(forbidden), Path.GetFileName(file) + " breaks " + why);
        }

        [Test]
        public void SimAssemblyDefinitionForbidsEngineReferences()
        {
            string asmdef = File.ReadAllText(Path.Combine(SimFolder, "Hearthline.Sim.asmdef"));
            Assert.That(asmdef.Replace(" ", ""), Does.Contain("\"noEngineReferences\":true"));
        }
    }
}

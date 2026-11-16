using System;
using System.IO;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using NUnit.Framework;

namespace Hearthline.Tests
{
    /// <summary>Shared helpers. These tests run both in Unity (EditMode) and headless through tools/headless.</summary>
    internal static class TestSupport
    {
        private static ContentDatabase _content;

        /// <summary>The repository root: the folder holding Content/vocab.yaml.</summary>
        public static string RepoRoot
        {
            get
            {
                foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                {
                    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                        if (File.Exists(Path.Combine(dir.FullName, "Content", "vocab.yaml"))) return dir.FullName;
                }
                throw new InvalidOperationException("Could not find the repository root (a folder containing Content/vocab.yaml).");
            }
        }

        public static string ContentRoot => Path.Combine(RepoRoot, "Content");

        /// <summary>The real game content. Fails the test if it does not validate.</summary>
        public static ContentDatabase Content
        {
            get
            {
                if (_content != null) return _content;
                ContentLoadResult result = ContentLoader.LoadDirectory(ContentRoot);
                Assert.That(result.Errors, Is.Empty, "Content must validate:\n" + string.Join("\n", result.Errors));
                return _content = result.Database;
            }
        }

        public static Simulation NewSim(ulong seed = 1)
        {
            return Simulation.NewGame(Content, new NewGameOptions { Seed = seed });
        }

        public static MarkOrigin Boar => new MarkOrigin { Cause = "the boar", Place = "Miller's Ford" };

        public static void Ok(CommandResult result)
        {
            Assert.That(result.Ok, Is.True, "Command was rejected: " + result.Error);
        }

        public static void Rejected(CommandResult result, string errorContains)
        {
            Assert.That(result.Ok, Is.False, "Command should have been rejected.");
            Assert.That(result.Error, Does.Contain(errorContains));
        }
    }
}

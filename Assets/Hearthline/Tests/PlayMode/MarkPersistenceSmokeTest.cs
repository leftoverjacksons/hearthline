using System.Collections;
using System.IO;
using Hearthline.Bridge;
using Hearthline.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>
    /// The M0 gate in the greybox scene: a scar added through a sim command is drawn on the character,
    /// disappears on a new game, and is back on the same body region after loading the save.
    /// </summary>
    public sealed class MarkPersistenceSmokeTest
    {
        private const string SceneName = "M0_Greybox";
        private SimHost _host;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            _host = SimHost.Instance;
            Assert.That(_host, Is.Not.Null, "The greybox scene has no SimHost.");
            // Never touch a real save.
            _host.SaveFileName = "smoketest_" + System.Guid.NewGuid().ToString("N") + ".json";
        }

        [UnityTearDown]
        public IEnumerator DeleteSave()
        {
            if (_host != null && File.Exists(_host.SavePath)) File.Delete(_host.SavePath);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScarOnFaceSurvivesSaveAndLoad()
        {
            Transform face = FindAnchor("MarkAnchor_Face");
            Assert.That(face, Is.Not.Null, "The player has no MarkAnchor_Face.");
            Assert.That(face.parent.name, Does.Contain("Head"), "The face anchor should ride on the head bone.");
            Assert.That(ScarsUnder(face), Is.EqualTo(0));

            CommandResult added = _host.Sim.Execute(new AddMark
            {
                CharacterId = _host.Sim.Head.Id,
                DefId = "scar_claw",
                Region = BodyRegion.Face,
                Severity = 2,
                Origin = new MarkOrigin { Cause = "the boar", Place = "Miller's Ford" },
            });
            Assert.That(added.Ok, Is.True, added.Error);
            yield return null;
            Assert.That(ScarsUnder(face), Is.EqualTo(1), "The scar is not drawn on the face.");

            _host.Save();
            _host.NewGame();
            yield return null; // Destroy is deferred to the end of the frame.
            Assert.That(ScarsUnder(face), Is.EqualTo(0), "A new game still shows the scar.");

            Assert.That(_host.Load(), Is.True, "Loading the save failed.");
            yield return null;
            Assert.That(ScarsUnder(face), Is.EqualTo(1), "The scar is not back on the face after loading.");
        }

        private static Transform FindAnchor(string name)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) return null;
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static int ScarsUnder(Transform anchor)
        {
            int count = 0;
            foreach (Transform child in anchor)
                if (child.name.Contains("scar_claw")) count++;
            return count;
        }
    }
}

using System.Collections;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>The greybox mirror: camera switch, mark list, region framing, highlight, and that it is read-only.</summary>
    public sealed class MirrorViewTests
    {
        private SimHost _host;
        private MirrorView _mirror;
        private CharacterMarksView _marks;
        private int _faceScar, _legScar;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M0_Greybox");
            yield return null;
            _host = SimHost.Instance;
            Assert.That(_host, Is.Not.Null, "The greybox scene has no SimHost.");
            _host.SaveFileName = "mirrortest_" + System.Guid.NewGuid().ToString("N") + ".json";
            _mirror = Object.FindAnyObjectByType<MirrorView>();
            Assert.That(_mirror, Is.Not.Null, "The greybox scene has no MirrorView.");
            _marks = Object.FindAnyObjectByType<CharacterMarksView>();

            var origin = new MarkOrigin { Cause = "the boar", Place = "Miller's Ford" };
            Simulation sim = _host.Sim;
            Assert.That(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_claw", Region = BodyRegion.Face, Origin = origin }).Ok, Is.True);
            Assert.That(sim.Execute(new AddMark { CharacterId = sim.Head.Id, DefId = "scar_burn", Region = BodyRegion.LegL, Origin = origin }).Ok, Is.True);
            _faceScar = sim.Head.Marks[0].Id;
            _legScar = sim.Head.Marks[1].Id;
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpeningSwitchesToTheMirrorCameraAndClosingSwitchesBack()
        {
            var brain = Camera.main.GetComponent<CinemachineBrain>();
            Assert.That(brain, Is.Not.Null);

            _mirror.SetOpen(true);
            yield return null;
            yield return null;
            Assert.That(_mirror.IsOpen, Is.True);
            Assert.That((Object)brain.ActiveVirtualCamera, Is.SameAs(_mirror.Camera));

            _mirror.SetOpen(false);
            yield return null;
            yield return null;
            Assert.That((Object)brain.ActiveVirtualCamera, Is.Not.SameAs(_mirror.Camera));
        }

        [UnityTest]
        public IEnumerator TheListShowsEveryMarkWithItsOriginAndEffects()
        {
            _mirror.SetOpen(true);
            yield return null;
            var listed = _mirror.CurrentMarks();
            Assert.That(listed.Count, Is.EqualTo(_host.Sim.Head.Marks.Count));
            foreach (MarkView mark in listed)
            {
                Assert.That(mark.OriginText, Does.Contain("the boar"), mark.DefId);
                Assert.That(mark.Effects, Is.Not.Empty, mark.DefId);
            }
        }

        [UnityTest]
        public IEnumerator SelectingAMarkFramesItsRegionAndHighlightsIt()
        {
            _mirror.SetOpen(true);
            yield return null;
            _mirror.FrameFor(out Vector3 bodyCamera, out _);

            _mirror.Select(_legScar);
            yield return null;
            yield return null;
            _mirror.FrameFor(out Vector3 legCamera, out Vector3 legLook);
            Assert.That(Vector3.Distance(legLook, _marks.AnchorFor(BodyRegion.LegL).position), Is.LessThan(0.001f));
            Assert.That(legCamera.y, Is.LessThan(bodyCamera.y), "The camera should drop to look at the leg.");
            Assert.That(_marks.HighlightedMarkId, Is.EqualTo(_legScar));

            Transform legMarker = _marks.MarkerFor(_legScar);
            Transform faceMarker = _marks.MarkerFor(_faceScar);
            Assert.That(legMarker.localScale.x, Is.GreaterThan(faceMarker.localScale.x), "The selected marker is not drawn larger.");

            _mirror.SetOpen(false);
            Assert.That(_marks.HighlightedMarkId, Is.Null, "Closing the mirror should clear the highlight.");
        }

        [UnityTest]
        public IEnumerator LookingInTheMirrorChangesNothingInTheSimulation()
        {
            string before = _host.Sim.ToJson();
            _mirror.SetOpen(true);
            yield return null;
            _mirror.Select(_faceScar);
            yield return null;
            _mirror.Select(null);
            _mirror.SetOpen(false);
            yield return null;
            Assert.That(_host.Sim.ToJson(), Is.EqualTo(before));
        }
    }
}

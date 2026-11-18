using System.Collections;
using System.Linq;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>
    /// The hunt in the region (docs/specs/hunt.md): the job board lays a trail of sign, reading it brings Crookhorn
    /// out, he notices someone standing sooner than someone crouching, a gunshot sends him off, and a clean shot
    /// lets you butcher him into the pack.
    /// </summary>
    public sealed class HuntPlayTests
    {
        private SimHost _host;
        private GameObject _player;
        private HuntDirector _director;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "hunttest_" + System.Guid.NewGuid().ToString("N") + ".json";
            Object.FindAnyObjectByType<TravelClock>().MinutesPerDay = 100000f;
            _player = GameObject.Find("Player");
            _director = Object.FindAnyObjectByType<HuntDirector>();
            Assert.That(_director, Is.Not.Null);
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        private IEnumerator FindHim()
        {
            Assert.That(Object.FindAnyObjectByType<JobBoard>().TakeHunt("crookhorn"), Is.Null);
            yield return null;
            for (int i = 0; i < 12 && _director.CurrentSign != null; i++) { _director.CurrentSign.Interact(); yield return null; }
            Assert.That(_director.Deer, Is.Not.Null, "Reading every sign brings him out.");
        }

        // Puts the player this far from the deer, in front of him, standing or crouched, and turns him to face them.
        private IEnumerator Face(float distance, bool crouched)
        {
            Transform deer = _director.Deer.transform;
            Vector3 spot = deer.position + deer.forward * distance;
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 6f, NavMesh.AllAreas)) spot = hit.position;
            // In the forest (ADR 0027) a trunk or thicket can stand between: look around for a clear line of sight.
            for (int turn = 0; turn < 24; turn++)
            {
                Vector3 dir = Quaternion.Euler(0f, turn * 15f, 0f) * deer.forward;
                Vector3 tryAt = deer.position + dir * distance;
                if (!NavMesh.SamplePosition(tryAt, out NavMeshHit near, 3f, NavMesh.AllAreas)) continue;
                Vector3 eye = deer.position + Vector3.up * 1.2f, head = near.position + Vector3.up * 1.4f;
                if (Physics.Linecast(eye, head, out RaycastHit block, ~0, QueryTriggerInteraction.Ignore) && block.transform.root != _player.transform.root && block.transform.root != deer.root) continue;
                spot = near.position;
                break;
            }
            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            _player.transform.position = spot + Vector3.up * 0.1f;
            body.enabled = true;
            _player.GetComponent<Crouch>().Set(crouched);
            yield return new WaitForSeconds(0.3f);
        }

        [UnityTest]
        public IEnumerator TakingUpTheHuntLaysATrailToFollow()
        {
            Assert.That(Object.FindAnyObjectByType<JobBoard>().TakeHunt("crookhorn"), Is.Null);
            yield return null;
            Assert.That(_director.CurrentSign, Is.Not.Null);
            Assert.That(_director.CurrentSign.Index, Is.EqualTo(0));
            Assert.That(_director.Message, Does.Contain("Start looking"));
            Assert.That(AreaVolume.All.Any(a => (a.AreaTags.Contains("woods") || a.AreaTags.Contains("deep_woods")) && a.Contains(_director.CurrentSign.transform.position + Vector3.up)), Is.True, "The trail starts in the woods.");
        }

        [UnityTest]
        public IEnumerator ReadingTheSignBringsHimOutGrazing()
        {
            yield return FindHim();
            DeerAI deer = _director.Deer.GetComponent<DeerAI>();
            Assert.That(deer.Current, Is.EqualTo(DeerAI.State.Graze));
            Assert.That(_host.Sim.Hunt.Revealed, Is.True);
        }

        [UnityTest]
        public IEnumerator HeNoticesSomeoneStandingSoonerThanSomeoneCrouching()
        {
            yield return FindHim();
            DeerAI deer = _director.Deer.GetComponent<DeerAI>();
            yield return Face(18f, crouched: true);
            // Hold him still and facing the player: this tests his eyes, not his wandering.
            var agent = deer.GetComponent<NavMeshAgent>();
            IEnumerator Watch(float seconds)
            {
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    agent.isStopped = true;
                    Vector3 look = _player.transform.position - deer.transform.position; look.y = 0f;
                    deer.transform.rotation = Quaternion.LookRotation(look);
                    yield return null;
                }
            }
            yield return Watch(2f);
            float crouchedAwareness = deer.Awareness;
            Assert.That(crouchedAwareness, Is.LessThan(0.1f), "Low and still at 18 m: he doesn't see you.");
            _player.GetComponent<Crouch>().Set(false);
            yield return Watch(2f);
            Assert.That(deer.Awareness, Is.GreaterThan(crouchedAwareness + 0.2f), "Standing at 18 m: he does.");
        }

        [UnityTest]
        public IEnumerator AGunshotSendsHimOff()
        {
            yield return FindHim();
            DeerAI deer = _director.Deer.GetComponent<DeerAI>();
            yield return Face(40f, crouched: true);
            var weapon = _player.GetComponent<PlayerWeapon>();
            weapon.ForceAim = true;
            float t = 0f;
            while (!weapon.Ready && t < 2f) { t += Time.deltaTime; yield return null; }
            weapon.FireAt(new Ray(_player.transform.position + Vector3.up * 1.5f, Vector3.up)); // a shot into the air
            yield return null;
            Assert.That(deer.Current, Is.EqualTo(DeerAI.State.Flee));
        }

        [UnityTest]
        public IEnumerator ACleanShotDropsHimAndHeCanBeButchered()
        {
            yield return FindHim();
            Assert.That(_host.Sim.Execute(new SetOut()).Ok, Is.True);
            DeerAI deer = _director.Deer.GetComponent<DeerAI>();
            HitRegion chest = deer.GetComponentsInChildren<HitRegion>().First(z => z.Region == BodyRegion.Torso);
            HitOutcome hit = chest.Receiver.ReceiveShot(BodyRegion.Torso, _host.Sim.Head.Id, null, 30);
            Assert.That(hit.Ok, Is.True, hit.Error);
            yield return null;
            Assert.That(deer.Current, Is.EqualTo(DeerAI.State.Down));
            ButcherSpot spot = deer.GetComponent<ButcherSpot>();
            Assert.That(spot, Is.Not.Null);
            string took = spot.Interact();
            Assert.That(took, Does.Contain("hide"));
            Assert.That(_host.Sim.PackCount("crookhorn_hide"), Is.EqualTo(1));
            Assert.That(_host.Sim.HuntDone("crookhorn"), Is.True);
        }
    }
}

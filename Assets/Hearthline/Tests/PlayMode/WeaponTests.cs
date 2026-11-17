using System.Collections;
using System.Linq;
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
    /// <summary>
    /// Combat step 2: the head starts with a loaded rifle, aiming moves the camera in and slows you, a shot
    /// goes where it is aimed and the simulation decides the rest, and reloading takes rounds from the stores.
    /// </summary>
    public sealed class WeaponTests
    {
        private SimHost _host;
        private GameObject _player;
        private PlayerWeapon _weapon;
        private AimController _aim;
        private PracticeTarget _target;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "weapontest_" + System.Guid.NewGuid().ToString("N") + ".json";
            Object.FindAnyObjectByType<TravelClock>().MinutesPerDay = 100000f;
            _player = GameObject.Find("Player");
            _weapon = _player.GetComponent<PlayerWeapon>();
            _aim = _player.GetComponent<AimController>();
            _target = Object.FindAnyObjectByType<PracticeTarget>();
            Assert.That(_weapon, Is.Not.Null);
            Assert.That(_target, Is.Not.Null);
            yield return new WaitForSeconds(0.3f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        private IEnumerator AimedIn()
        {
            _weapon.ForceAim = true;
            float t = 0f;
            while ((!_weapon.Ready || _aim.Amount < 1f) && t < 3f) { t += Time.deltaTime; yield return null; }
            Assert.That(_weapon.Ready, Is.True);
        }

        private Ray RayTo(BodyRegion region)
        {
            HitRegion zone = _target.GetComponentsInChildren<HitRegion>().First(z => z.Region == region);
            Vector3 from = Camera.main.transform.position;
            return new Ray(from, (zone.GetComponent<Collider>().bounds.center - from).normalized);
        }

        [UnityTest]
        public IEnumerator TheHeadStartsWithALoadedRifleOnTheirBack()
        {
            yield return null;
            GearItem rifle = _host.Sim.Head.Gear.Single(g => g.DefId == "lever_rifle");
            Assert.That(rifle.Equipped, Is.True);
            Assert.That(rifle.Loaded, Is.EqualTo(7));
            Assert.That(_player.transform.Find("LongGun (rifle_lever)"), Is.Not.Null, "The rifle is on the body.");
            Assert.That(_weapon.Drawn, Is.EqualTo(GunVisuals.Drawn.None));
        }

        [UnityTest]
        public IEnumerator AimingMovesTheCameraInAndSlowsYouDown()
        {
            var follow = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude).First(c => c.GetComponent<CinemachineThirdPersonFollow>() != null);
            float hipDistance = follow.GetComponent<CinemachineThirdPersonFollow>().CameraDistance;
            float hipFov = follow.Lens.FieldOfView;
            yield return AimedIn();
            Assert.That(_weapon.Drawn, Is.EqualTo(GunVisuals.Drawn.LongGun), "Aiming draws the long gun.");
            Assert.That(follow.GetComponent<CinemachineThirdPersonFollow>().CameraDistance, Is.LessThan(hipDistance));
            Assert.That(follow.Lens.FieldOfView, Is.LessThan(hipFov));
            PlayerSpeed speed = _player.GetComponent<PlayerSpeed>();
            Assert.That(speed.Factor, Is.LessThan(1f));
            Assert.That(speed.SprintBlocked, Is.True);

            _weapon.ForceAim = false;
            yield return new WaitForSeconds(0.5f);
            Assert.That(follow.GetComponent<CinemachineThirdPersonFollow>().CameraDistance, Is.EqualTo(hipDistance).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator AShotAtThePracticeTargetLandsWhereAimedAndNeverReachesTheSim()
        {
            yield return AimedIn();
            int marks = _host.Sim.State.Characters.Sum(c => c.Marks.Count);
            PlayerWeapon.ShotResult shot = _weapon.FireAt(RayTo(BodyRegion.Torso));
            Assert.That(shot.Kind, Is.EqualTo(PlayerWeapon.ShotKind.Fired));
            Assert.That(shot.Zone, Is.Not.Null);
            Assert.That(shot.Zone.Region, Is.EqualTo(BodyRegion.Torso));
            Assert.That(shot.Severity, Is.EqualTo(2));
            Assert.That(_target.Hits, Is.EqualTo(1));
            Assert.That(_weapon.DrawnGun.Loaded, Is.EqualTo(6));
            Assert.That(_host.Sim.State.Characters.Sum(c => c.Marks.Count), Is.EqualTo(marks), "Practice hurts nobody.");
        }

        [UnityTest]
        public IEnumerator AnEmptyGunClicksAndReloadingTakesFromTheStores()
        {
            yield return AimedIn();
            GearItem rifle = _weapon.DrawnGun;
            rifle.Loaded = 0;
            Assert.That(_weapon.FireAt(RayTo(BodyRegion.Torso)).Kind, Is.EqualTo(PlayerWeapon.ShotKind.Refused));
            int stores = _host.Sim.ItemCount("cartridges");
            Assert.That(_weapon.StartReload(), Is.True);
            float t = 0f;
            while (_weapon.Reloading && t < 10f) { t += Time.deltaTime; yield return null; }
            Assert.That(rifle.Loaded, Is.EqualTo(7));
            Assert.That(_host.Sim.ItemCount("cartridges"), Is.EqualTo(stores - 7));
        }

        [UnityTest]
        public IEnumerator AJammedGunMustBeCleared()
        {
            yield return AimedIn();
            GearItem rifle = _weapon.DrawnGun;
            rifle.Jammed = true;
            Assert.That(_weapon.FireAt(RayTo(BodyRegion.Torso)).Message, Does.Contain("Jammed"));
            Assert.That(_weapon.StartReload(), Is.True);
            float t = 0f;
            while (_weapon.Reloading && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.That(rifle.Jammed, Is.False);
        }

        [UnityTest]
        public IEnumerator AShotThatHitsAPersonWoundsThemInTheSim()
        {
            yield return null;
            HitReceiver receiver = _player.GetComponent<HitReceiver>();
            Character head = _host.Sim.Head;
            HitOutcome outcome = receiver.ReceiveShot(BodyRegion.LegL, null, null, 10);
            Assert.That(outcome.Ok, Is.True, outcome.Error);
            Assert.That(outcome.Severity, Is.EqualTo(2));
            Assert.That(head.Marks.Single().DefId, Is.EqualTo("wound_gunshot"));
        }

        [UnityTest]
        public IEnumerator YouCannotSaveDuringAFight()
        {
            yield return null;
            Assert.That(_host.Sim.Execute(new StartEncounter { EncounterId = "reavers_small" }).Ok, Is.True);
            Assert.That(_host.Save(), Does.Contain("fight"));
            Assert.That(System.IO.File.Exists(_host.SavePath), Is.False);
            Assert.That(_host.Sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Fled }).Ok, Is.True);
            Assert.That(_host.Save(), Is.Null);
        }
    }
}

using System.Collections;
using Hearthline.Bridge;
using Hearthline.Game;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>
    /// Crouching (ADR 0019): the player gets shorter and slower, can't sprint, the camera drops, the crouch
    /// pose plays, standing up waits for headroom, and crouch stacks with a heavy pack.
    /// </summary>
    public sealed class CrouchTests
    {
        private SimHost _host;
        private GameObject _player;
        private Crouch _crouch;
        private CharacterController _body;
        private ThirdPersonController _controller;
        private PlayerSpeed _speed;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "crouchtest_" + System.Guid.NewGuid().ToString("N") + ".json";
            Object.FindAnyObjectByType<TravelClock>().MinutesPerDay = 100000f;
            _player = GameObject.Find("Player");
            _crouch = _player.GetComponent<Crouch>();
            _body = _player.GetComponent<CharacterController>();
            _controller = _player.GetComponent<ThirdPersonController>();
            _speed = _player.GetComponent<PlayerSpeed>();
            Assert.That(_crouch, Is.Not.Null, "The player should have the Crouch component.");
            yield return new WaitForSeconds(0.3f); // settle on the ground
        }

        [UnityTearDown]
        public IEnumerator DeleteSave()
        {
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CrouchingMakesThePlayerShorterSlowerAndLower()
        {
            float standHeight = _body.height;
            float walk = _controller.MoveSpeed;
            Transform cameraRoot = _controller.CinemachineCameraTarget.transform;
            float cameraY = cameraRoot.localPosition.y;
            float feet = _player.transform.position.y + _body.center.y - _body.height / 2f;

            _crouch.Set(true);
            yield return new WaitForSeconds(0.4f);

            Assert.That(_crouch.Amount, Is.EqualTo(1f));
            Assert.That(_body.height, Is.EqualTo(_crouch.CrouchedHeight).Within(0.01f));
            Assert.That(_player.transform.position.y + _body.center.y - _body.height / 2f, Is.EqualTo(feet).Within(0.05f), "The capsule shrinks from the top; feet stay put.");
            Assert.That(_controller.MoveSpeed, Is.LessThan(walk));
            Assert.That(_controller.SprintSpeed, Is.EqualTo(_controller.MoveSpeed).Within(0.001f), "No sprinting while crouched.");
            Assert.That(cameraRoot.localPosition.y, Is.LessThan(cameraY - 0.2f));

            _crouch.Set(false);
            yield return new WaitForSeconds(0.4f);
            Assert.That(_body.height, Is.EqualTo(standHeight).Within(0.01f));
            Assert.That(_controller.MoveSpeed, Is.EqualTo(walk).Within(0.01f));
            Assert.That(cameraRoot.localPosition.y, Is.EqualTo(cameraY).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator TheCrouchPosePlays()
        {
            Animator animator = _player.GetComponent<Animator>();
            bool hasCrouch = false;
            foreach (AnimatorControllerParameter p in animator.parameters) hasCrouch |= p.name == Crouch.AnimatorParameter;
            if (!hasCrouch) Assert.Ignore("Mixamo crouch clips not imported; the player uses the plain Starter Assets animator.");

            _crouch.Set(true);
            yield return new WaitForSeconds(0.5f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Crouch"), Is.True);

            _crouch.Set(false);
            yield return new WaitForSeconds(0.5f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle Walk Run Blend"), Is.True);
        }

        [UnityTest]
        public IEnumerator YouCantStandUpUnderSomethingLow()
        {
            _crouch.Set(true);
            yield return new WaitForSeconds(0.4f);

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.position = _player.transform.position + Vector3.up * 1.45f;
            ceiling.transform.localScale = new Vector3(3f, 0.2f, 3f);
            yield return new WaitForFixedUpdate();

            _crouch.Set(false);
            yield return null;
            Assert.That(_crouch.IsCrouched, Is.True, "Standing up should wait for headroom.");

            Object.Destroy(ceiling);
            yield return new WaitForFixedUpdate();
            yield return null;
            _crouch.Set(false);
            yield return null;
            Assert.That(_crouch.IsCrouched, Is.False);
        }

        [UnityTest]
        public IEnumerator CrouchAndAHeavyPackBothSlowYou()
        {
            float walk = _controller.MoveSpeed;
            PackLoad pack = _player.GetComponent<PackLoad>();
            // Out of the yard (a trip starts there), then set out.
            GatherSpot spot = null;
            foreach (GatherSpot g in Object.FindObjectsByType<GatherSpot>(FindObjectsInactive.Exclude))
                if (g.SpotId.StartsWith("berries_") && (spot == null || g.transform.position.sqrMagnitude < spot.transform.position.sqrMagnitude)) spot = g;
            Vector3 p = spot.transform.position + new Vector3(0f, 0f, -1.4f);
            _body.enabled = false;
            if (Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.05f;
            _player.transform.position = p;
            _body.enabled = true;
            yield return null;
            yield return null;
            var trip = Object.FindAnyObjectByType<TripController>();
            if (trip.Current == TripController.Phase.Outfitting) Assert.That(trip.ConfirmSetOut(), Is.True, trip.Status);
            yield return null;
            Assert.That(_host.Sim.OnTrip, Is.True);
            Assert.That(_host.Sim.Execute(new Hearthline.Sim.GiveItem { ItemId = "firewood", Count = 13 }).Ok, Is.True);
            yield return null;
            Assert.That(pack.SpeedMultiplier, Is.LessThan(1f));
            float packed = _controller.MoveSpeed;
            Assert.That(packed, Is.EqualTo(walk * pack.SpeedMultiplier).Within(0.01f));

            _crouch.Set(true);
            yield return null;
            yield return null;
            Assert.That(_controller.MoveSpeed, Is.LessThan(packed), "Crouching while loaded is slower still.");
            Assert.That(_controller.MoveSpeed, Is.EqualTo(walk * _speed.Factor).Within(0.01f));
            Assert.That(_speed.Factor, Is.LessThan(pack.SpeedMultiplier));
        }
    }
}

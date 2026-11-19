using System.Collections;
using System.Linq;
using Hearthline.Bridge;
using Hearthline.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>
    /// Sticky cover (combat step 3): you press against cover, slide along it, stop at its edge, lean out to
    /// aim from high cover, rise over low cover, and the cover stops bullets while you're pressed in.
    /// </summary>
    public sealed class CoverTests
    {
        private SimHost _host;
        private GameObject _player;
        private PlayerCover _cover;
        private PlayerWeapon _weapon;
        private Crouch _crouch;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "covertest_" + System.Guid.NewGuid().ToString("N") + ".json";
            Object.FindAnyObjectByType<TravelClock>().MinutesPerDay = 100000f;
            _player = GameObject.Find("Player");
            _cover = _player.GetComponent<PlayerCover>();
            _weapon = _player.GetComponent<PlayerWeapon>();
            _crouch = _player.GetComponent<Crouch>();
            Assert.That(_cover, Is.Not.Null);
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        // A box cover piece near home: high or low, with a face at least this wide.
        private static CoverSurface Box(bool high, float minHalfWidth)
        {
            return Object.FindObjectsByType<CoverSurface>(FindObjectsInactive.Exclude)
                .Where(c => c.Kind == CoverSurface.Shape.Box && c.Low != high && Mathf.Max(c.HalfSize.x, c.HalfSize.y) >= minHalfWidth)
                .OrderBy(c => new Vector2(c.Centre.x, c.Centre.z).sqrMagnitude)
                .First();
        }

        // Stands the player in front of the middle of the cover's longer face, facing it, and takes cover.
        private IEnumerator TakeCover(CoverSurface surface)
        {
            Vector3 outward = Quaternion.Euler(0f, surface.Yaw, 0f) * (surface.HalfSize.x >= surface.HalfSize.y ? Vector3.forward : Vector3.right);
            CoverSurface.Spot spot = surface.Nearest(surface.Centre + outward * 20f);
            Vector3 p = spot.Point + spot.Normal * 1.0f;
            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            if (Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.05f;
            _player.transform.SetPositionAndRotation(p, Quaternion.LookRotation(-spot.Normal));
            body.enabled = true;
            yield return null;
            Assert.That(_cover.TryEnter(anyDirection: true), Is.True, "There should be cover in reach.");
            Assert.That(_cover.Surface, Is.SameAs(surface));
            yield return new WaitForSeconds(0.5f);
        }

        private float DistanceToCover() =>
            Vector3.Distance(Flat(_player.transform.position), Flat(_cover.Surface.Nearest(_player.transform.position).Point));

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        [UnityTest]
        public IEnumerator YouPressYourBackAgainstHighCover()
        {
            CoverSurface wall = Box(high: true, minHalfWidth: 1.2f);
            yield return TakeCover(wall);
            Assert.That(_cover.InCover, Is.True);
            Assert.That(_cover.LowCover, Is.False);
            Assert.That(DistanceToCover(), Is.LessThan(0.5f), "Pressed against it.");
            Assert.That(Vector3.Dot(_player.transform.forward, _cover.CoverNormal), Is.GreaterThan(0.9f), "Back to the cover (DESIGN: Cover).");
            Assert.That(_crouch.IsCrouched, Is.False);
        }

        [UnityTest]
        public IEnumerator SlidingStopsAtTheEdgeAndAimingThereLeansOut()
        {
            CoverSurface wall = Box(high: true, minHalfWidth: 1.2f);
            yield return TakeCover(wall);
            Vector3 start = _player.transform.position;
            _cover.ScriptedMove = Vector2.right;
            yield return new WaitForSeconds(4f);
            CoverSurface.Spot at = _cover.Surface.OnFace(_cover.Surface.Nearest(_player.transform.position).Face, _player.transform.position);
            string why = "moved " + Vector3.Distance(start, _player.transform.position).ToString("F2") + " m; to right edge " + at.ToRightEdge.ToString("F2")
                + ", left " + at.ToLeftEdge.ToString("F2") + "; half " + _cover.Surface.HalfSize + " at " + _cover.Surface.Centre + " '" + _cover.Surface.name + "'";
            _cover.ScriptedMove = Vector2.zero;
            Assert.That(_cover.InCover, Is.True, "Sliding never walks you off the cover.");
            Assert.That(_cover.Edge, Is.EqualTo(PlayerCover.Side.Right), why);
            Assert.That(_cover.Exposed, Is.False);

            Vector3 pressed = _player.transform.position;
            _weapon.ForceAim = true;
            yield return new WaitForSeconds(0.8f);
            Assert.That(_cover.Exposed, Is.True);
            Vector3 right = Vector3.Cross(Vector3.up, -_cover.CoverNormal);
            Assert.That(Vector3.Dot(_player.transform.position - pressed, right), Is.GreaterThan(0.35f), "Leans out past the edge.");
            Assert.That(_player.GetComponent<AimController>().RightShoulder, Is.True);

            _weapon.ForceAim = false;
            yield return new WaitForSeconds(0.6f);
            Assert.That(_cover.Exposed, Is.False);
            Assert.That(Vector3.Distance(Flat(_player.transform.position), Flat(pressed)), Is.LessThan(0.2f), "Back into cover.");
        }

        [UnityTest]
        public IEnumerator LowCoverIsCrouchedBehindAndRisenOverToAim()
        {
            CoverSurface low = Box(high: false, minHalfWidth: 0.8f);
            yield return TakeCover(low);
            Assert.That(_cover.LowCover, Is.True);
            Assert.That(_crouch.IsCrouched, Is.True);
            _weapon.ForceAim = true;
            yield return new WaitForSeconds(0.6f);
            Assert.That(_crouch.IsCrouched, Is.False, "Rises over it to aim.");
            Assert.That(_cover.Exposed, Is.True);
            _weapon.ForceAim = false;
            yield return new WaitForSeconds(0.4f);
            Assert.That(_crouch.IsCrouched, Is.True);
            _cover.Leave();
            yield return null;
            Assert.That(_cover.InCover, Is.False);
            Assert.That(_crouch.IsCrouched, Is.False, "Leaving cover puts you back how you were.");
        }

        [UnityTest]
        public IEnumerator HighCoverStopsAShotAtYouWhilePressedIn()
        {
            CoverSurface wall = Box(high: true, minHalfWidth: 1.2f);
            yield return TakeCover(wall);
            Vector3 chest = _player.transform.position + Vector3.up * 1.3f;
            Vector3 shooter = chest - _cover.CoverNormal * 15f;
            Assert.That(Physics.Raycast(shooter, (chest - shooter).normalized, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Collide), Is.True);
            Assert.That(hit.collider.GetComponent<HitRegion>(), Is.Null, "The bullet meets the cover, not you.");
        }

        [UnityTest]
        public IEnumerator CoverIsOnlyTakenWithinReach()
        {
            var body = _player.GetComponent<CharacterController>();
            CoverSurface nearest = Object.FindObjectsByType<CoverSurface>(FindObjectsInactive.Exclude).OrderBy(c => (c.Centre - _player.transform.position).sqrMagnitude).First();
            body.enabled = false;
            Vector3 far = nearest.Centre + Vector3.right * 400f;
            if (Physics.Raycast(new Vector3(far.x, 200f, far.z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) far.y = hit.point.y + 0.05f;
            _player.transform.position = far;
            body.enabled = true;
            yield return null;
            bool any = Physics.OverlapSphere(far + Vector3.up * 0.8f, 1.7f).Any(c => CoverSurface.Of(c) != null);
            Assert.That(_cover.TryEnter(anyDirection: true), Is.EqualTo(any));
        }
    }
}

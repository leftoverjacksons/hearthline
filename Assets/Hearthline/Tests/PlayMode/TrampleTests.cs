using System.Collections;
using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>Matted grass and desire paths in the region (docs/specs/trampled-grass.md, ADR 0034).</summary>
    public sealed class TrampleTests
    {
        private GameObject _player;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            yield return new WaitForSeconds(0.3f);
            _player = GameObject.Find("Player");
            // Walk the player by hand: the controller would otherwise put it back.
            foreach (MonoBehaviour b in _player.GetComponents<MonoBehaviour>())
                if (b.GetType().Name == "ThirdPersonController") b.enabled = false;
            var cc = _player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        private IEnumerator Walk(Vector3 from, Vector3 to, float speed = 4f)
        {
            _player.transform.position = from;
            yield return null;
            yield return null;
            float length = Vector3.Distance(from, to), t = 0f;
            while (t < length)
            {
                t = Mathf.Min(length, t + speed * Time.deltaTime);
                _player.transform.position = Vector3.Lerp(from, to, t / length);
                yield return null;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalkingMatsTheGrassAndItStandsBackUp()
        {
            var field = Object.FindAnyObjectByType<TrampleField>();
            Assert.That(field, Is.Not.Null, "the region has a trample field");
            Vector3 a = new Vector3(-10f, 0f, -2f), b = new Vector3(10f, 0f, -2f);
            a.y = RegionHeight(a); b.y = RegionHeight(b);
            yield return Walk(a, b);
            for (float x = -8f; x <= 8f; x += 2f)
                Assert.That(field.PressAt(new Vector3(x, 0f, -2f)), Is.GreaterThan(0.5f), "matted at " + x);
            Assert.That(field.PressAt(new Vector3(0f, 0f, 2f)), Is.EqualTo(0f), "untouched off the line");

            field.RecoverSeconds = 0.5f;
            yield return new WaitForSeconds(0.8f);
            Assert.That(field.PressAt(new Vector3(0f, 0f, -2f)), Is.EqualTo(0f), "stood back up");
        }

        [UnityTest]
        public IEnumerator WalkingWearsTheGroundAndTheCheatShowsOnTheWearMap()
        {
            Simulation sim = SimHost.Instance.Sim;
            var view = Object.FindAnyObjectByType<WearView>();
            Assert.That(view, Is.Not.Null, "the region has a wear view");
            Assert.That(sim.State.Wear.Routes.ContainsKey("woodcutter"), "household routes registered from the NavMesh");
            Assert.That(sim.State.Wear.Routes.ContainsKey(WearRules.IdleRoute));

            Vector3 a = new Vector3(-12f, 0f, -16f), b = new Vector3(12f, 0f, -16f);
            a.y = RegionHeight(a); b.y = RegionHeight(b);
            yield return Walk(a, b);
            Assert.That(sim.WearAt(0, -16), Is.GreaterThan(0), "a step counted in the sim");

            var route = new List<Vector3>();
            for (float x = -10f; x <= 10f; x += 1f) route.Add(new Vector3(x, 0f, -16f));
            view.Walked(route, 0, 20);
            view.Redraw();
            Texture2D map = view.Texture;
            Vector4 rect = Shader.GetGlobalVector("_HL_WearRect");
            foreach (Vector3 p in route)
            {
                Color c = map.GetPixel((int)(Mathf.Floor(p.x) - rect.x), (int)(Mathf.Floor(p.z) - rect.y));
                Assert.That(c.r, Is.GreaterThan(0.6f), "worn on the wear map at " + p.x);
            }
        }

        private static float RegionHeight(Vector3 p) =>
            Physics.Raycast(new Vector3(p.x, 500f, p.z), Vector3.down, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y + 0.05f : 0f;
    }
}

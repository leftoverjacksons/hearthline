using System.Collections;
using Hearthline.Game;
using Hearthline.Sim.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>
    /// The GPU grass (ADR 0033): it draws blades around the player, and the grass map keeps it off the road.
    /// </summary>
    public sealed class GrassTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            yield return new WaitForSeconds(0.3f);
        }

        [UnityTest]
        public IEnumerator DrawsBladesAroundThePlayer()
        {
            if (!SystemInfo.supportsComputeShaders) Assert.Ignore("No compute shaders: the grass is off by design.");
            var field = Object.FindAnyObjectByType<GrassField>();
            Assert.That(field, Is.Not.Null, "the region has a grass field");
            yield return null;
            yield return null;
            Assert.That(field.BladeCount(Camera.main), Is.GreaterThan(1000), "blades drawn for the main camera");
        }

        [Test]
        public void NoGrassOnTheRoad()
        {
            var field = Object.FindAnyObjectByType<GrassField>();
            // The region's world (RegionSceneBuilder.Seed); only the road's middle inside the grass map is checked.
            WorldMap map = WorldGen.Generate(new WorldSettings { Seed = 1 });
            int checkedPoints = 0;
            foreach (PathPoint p in map.Road)
            {
                float x = (float)p.P.X, z = (float)p.P.Z;
                if (Mathf.Abs(x) > 300f || Mathf.Abs(z) > 300f) continue;
                Assert.That(field.DensityAt(x, z), Is.LessThan(0.05f), "grass on the road at " + x + ", " + z);
                checkedPoints++;
            }
            Assert.That(checkedPoints, Is.GreaterThan(5));
            // And there is grass somewhere: the open ground by the homestead.
            float best = 0f;
            for (float a = 0f; a < 360f; a += 10f)
                best = Mathf.Max(best, field.DensityAt(Mathf.Cos(a * Mathf.Deg2Rad) * 20f, Mathf.Sin(a * Mathf.Deg2Rad) * 20f));
            Assert.That(best, Is.GreaterThan(0.5f), "meadow around the homestead");
        }
    }
}

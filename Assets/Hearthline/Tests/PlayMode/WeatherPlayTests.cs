using System.Collections;
using System.Linq;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>
    /// Weather and life in the region (docs/specs/life-and-weather.md): the sim's rain shows as rain and wet
    /// ground, birds flush when you walk up or fire, there are none in Thin places, and fireflies come out on
    /// summer evenings.
    /// </summary>
    public sealed class WeatherPlayTests
    {
        private SimHost _host;
        private GameObject _player;
        private TravelClock _clock;
        private WeatherView _weather;
        private AmbientLife _life;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "weathertest_" + System.Guid.NewGuid().ToString("N") + ".json";
            _clock = Object.FindAnyObjectByType<TravelClock>();
            _clock.MinutesPerDay = 100000f;
            _player = GameObject.Find("Player");
            _weather = Object.FindAnyObjectByType<WeatherView>();
            _life = Object.FindAnyObjectByType<AmbientLife>();
            Assert.That(_weather, Is.Not.Null);
            Assert.That(_life, Is.Not.Null);
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        private IEnumerator SetHour(float hour)
        {
            _clock.AdvanceHours(Mathf.Repeat(hour - _clock.Hour, 24f));
            yield return null;
        }

        private IEnumerator Weather(WeatherKind kind)
        {
            Assert.That(_host.Sim.Execute(new SetWeather { Kind = kind }).Ok, Is.True);
            yield return null;
            _weather.Snap();
            yield return null;
        }

        private IEnumerator MoveTo(Vector3 p)
        {
            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            if (Physics.Raycast(new Vector3(p.x, p.y + 100f, p.z), Vector3.down, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.05f;
            _player.transform.position = p;
            body.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator RainFromTheSimFallsAndWetsTheGround()
        {
            yield return SetHour(12f);
            yield return Weather(WeatherKind.Rain);
            Assert.That(_weather.Current, Is.EqualTo(WeatherKind.Rain));
            Assert.That(_weather.RainRate, Is.GreaterThan(500f));
            float wet = _weather.Wetness;
            yield return new WaitForSeconds(1f);
            Assert.That(_weather.Rain.particleCount, Is.GreaterThan(100), "Drops are falling.");
            Assert.That(_weather.Wetness, Is.GreaterThan(wet), "The ground is getting wetter.");
            Assert.That(_weather.RainVolume, Is.GreaterThan(0.3f), "And you hear it.");

            yield return Weather(WeatherKind.Clear);
            Assert.That(_weather.RainRate, Is.EqualTo(0f));
            Assert.That(_weather.RainVolume, Is.EqualTo(0f));
            wet = _weather.Wetness;
            yield return new WaitForSeconds(1f);
            Assert.That(_weather.Wetness, Is.LessThan(wet), "And it dries.");
        }

        [UnityTest]
        public IEnumerator AStormFlashes()
        {
            yield return Weather(WeatherKind.Storm);
            _weather.Strike();
            yield return null;
            Assert.That(_weather.FlashLevel, Is.GreaterThan(0.5f));
            yield return new WaitForSeconds(0.6f);
            Assert.That(_weather.FlashLevel, Is.LessThan(0.1f), "Just a flash.");
        }

        private IEnumerator SomePerchedBird()
        {
            yield return SetHour(10f);
            yield return Weather(WeatherKind.Clear);
            for (float t = 0f; t < 6f && _life.PerchedCount == 0; t += Time.deltaTime) yield return null;
            Assert.That(_life.PerchedCount, Is.GreaterThan(0), "Birds about on a clear morning.");
            for (float t = 0f; t < 3f && _life.DaySoundVolume < 0.2f; t += Time.deltaTime) yield return null;
            Assert.That(_life.DaySoundVolume, Is.GreaterThan(0.2f), "And singing.");
        }

        [UnityTest]
        public IEnumerator ABirdFlushesWhenYouWalkUp()
        {
            yield return SomePerchedBird();
            Bird bird = _life.Birds.First(b => b.Perched);
            Vector3 at = bird.transform.position;
            yield return MoveTo(at + new Vector3(4f, 0f, 0f));
            yield return null;
            Assert.That(bird == null || !bird.Perched, Is.True, "Off it goes.");
        }

        [UnityTest]
        public IEnumerator AGunshotFlushesTheBirdsAround()
        {
            yield return SomePerchedBird();
            Bird bird = _life.Birds.First(b => b.Perched);
            var weapon = _player.GetComponent<PlayerWeapon>();
            weapon.ForceAim = true;
            for (float t = 0f; !weapon.Ready && t < 2f; t += Time.deltaTime) yield return null;
            Vector3 to = bird.transform.position - _player.transform.position;
            to.y = 0f;
            if (to.magnitude > 50f) yield return MoveTo(bird.transform.position - to.normalized * 40f);
            Assert.That(bird.Perched, Is.True, "Still sitting before the shot.");
            weapon.FireAt(new Ray(_player.transform.position + Vector3.up * 1.5f, Vector3.up));
            yield return null;
            Assert.That(bird == null || !bird.Perched, Is.True);
        }

        [UnityTest]
        public IEnumerator ThinPlacesAreSilent()
        {
            yield return SomePerchedBird();
            AreaVolume thin = AreaVolume.All.First(a => a.ThinLevel > 0);
            yield return MoveTo(thin.transform.position);
            for (float t = 0f; t < 1f; t += Time.deltaTime) yield return null;
            Assert.That(Object.FindAnyObjectByType<PlayerLocation>().ThinLevel, Is.GreaterThan(0));
            Assert.That(_life.Density, Is.EqualTo(0f));
            Assert.That(_life.PerchedCount, Is.EqualTo(0), "Not a bird.");
            for (float t = 0f; t < 4f && _life.DaySoundVolume > 0f; t += Time.deltaTime) yield return null;
            Assert.That(_life.DaySoundVolume, Is.EqualTo(0f), "Not a sound.");
        }

        [UnityTest]
        public IEnumerator FirefliesComeOutOnSummerEvenings()
        {
            Assert.That(_host.Sim.Execute(new AdvanceDays { Days = GameDate.DaysPerSeason + 5 }).Ok, Is.True);
            Assert.That(_host.Sim.Date.Season, Is.EqualTo(Season.Summer));
            yield return SetHour(20.6f);
            yield return Weather(WeatherKind.Clear);
            yield return new WaitForSeconds(1f);
            Assert.That(_life.FirefliesOn, Is.True);
            Assert.That(_life.Fireflies.particleCount, Is.GreaterThan(3));
            Assert.That(_life.NightSoundVolume, Is.GreaterThan(0.1f), "Crickets.");
            yield return SetHour(12f);
            yield return Weather(WeatherKind.Clear);
            Assert.That(_life.FirefliesOn, Is.False, "Not at noon.");
        }
    }
}

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
    /// Combat step 4: a fight the simulation starts is staged around the player. Enemies spawn on the NavMesh at
    /// the encounter's distance, run to cover, shoot through the simulation, can be shot down, and break and
    /// run when the group breaks.
    /// </summary>
    public sealed class AmbushTests
    {
        private SimHost _host;
        private GameObject _player;
        private EnemySpawner _spawner;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "ambushtest_" + System.Guid.NewGuid().ToString("N") + ".json";
            Object.FindAnyObjectByType<TravelClock>().MinutesPerDay = 100000f;
            _player = GameObject.Find("Player");
            _spawner = Object.FindAnyObjectByType<EnemySpawner>();
            Assert.That(_spawner, Is.Not.Null);
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        private IEnumerator Fight(string encounter = "reavers_road")
        {
            CommandResult r = _host.Sim.Execute(new StartEncounter { EncounterId = encounter, PlaceName = "the homestead" });
            Assert.That(r.Ok, Is.True, r.Error);
            yield return null;
        }

        private EnemyAI[] Enemies => Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude);

        [UnityTest]
        public IEnumerator EachEnemyGetsABodyOnTheNavMeshAtTheEncountersDistance()
        {
            yield return Fight();
            Assert.That(_spawner.Bodies.Count, Is.EqualTo(_host.Sim.Encounter.Combatants.Count));
            var def = _host.Sim.Content.Encounters["reavers_road"];
            foreach (var kv in _spawner.Bodies)
            {
                Assert.That(kv.Value.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True, kv.Value.name);
                float d = Vector3.Distance(kv.Value.transform.position, _player.transform.position);
                Assert.That(d, Is.InRange((float)def.SpawnDistanceMin - 9f, (float)def.SpawnDistanceMax + 9f), kv.Value.name);
                Assert.That(kv.Value.GetComponent<SimCharacterLink>().BoundId, Is.EqualTo(kv.Key));
            }
        }

        [UnityTest]
        public IEnumerator EnemiesRunToCover()
        {
            yield return Fight();
            Vector3[] start = Enemies.Select(e => e.transform.position).ToArray();
            yield return new WaitForSeconds(10f);
            Assert.That(Enemies.Count(e => e.Current == EnemyAI.State.InCover), Is.GreaterThanOrEqualTo(Enemies.Length - 1), "Most should be in place.");
            Assert.That(Enemies.Count(e => e.Cover != null), Is.GreaterThan(0), "Some cover is in reach of the homestead.");
            int moved = 0;
            for (int i = 0; i < start.Length; i++) if (Vector3.Distance(start[i], Enemies[i].transform.position) > 2f) moved++;
            Assert.That(moved, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator AnEnemyShotGoesThroughTheSimulation()
        {
            yield return Fight();
            EnemyAI enemy = Enemies[0];
            Character me = _host.Sim.FindCharacter(enemy.CharacterId);
            GearItem gun = me.Gear.Single();
            int loaded = gun.Loaded;
            gun.Jammed = false;
            gun.Condition = 1;
            // Both on the open path in front of the house, six metres apart, and shoot with no spread.
            Vector3 Ground(Vector3 p) => Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out RaycastHit g, 400f, ~0, QueryTriggerInteraction.Ignore) ? g.point : p;
            enemy.GetComponent<NavMeshAgent>().Warp(Ground(new Vector3(0f, 0f, -2f)));
            enemy.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            _player.transform.position = Ground(new Vector3(0f, 0f, 4f)) + Vector3.up * 0.05f;
            body.enabled = true;
            yield return null;
            BodyRegion? hit = null;
            for (int i = 0; i < 3 && hit == null; i++)
            {
                gun.Jammed = false;
                hit = enemy.ShootAt(_player.transform.position + Vector3.up * 1.3f, 0f);
                yield return null;
            }
            string why = "loaded " + gun.Loaded + "/" + loaded + ", jammed " + gun.Jammed + ", head health " + _host.Sim.Head.Health
                + ", enemy " + enemy.Current + " at " + enemy.transform.position + ", player at " + _player.transform.position
                + ", muzzle " + (enemy.GetComponent<GunVisuals>().Muzzle != null ? enemy.GetComponent<GunVisuals>().Muzzle.position.ToString() : "none");
            var from = enemy.GetComponent<GunVisuals>().Muzzle != null ? enemy.GetComponent<GunVisuals>().Muzzle.position : enemy.transform.position + Vector3.up * 1.5f;
            var to = _player.transform.position + Vector3.up * 1.3f;
            foreach (var h in Physics.RaycastAll(from, (to - from).normalized, 20f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
                why += " | " + h.collider.name + "@" + h.distance.ToString("F1") + (h.collider.isTrigger ? "t" : "");
            Assert.That(hit, Is.Not.Null, "A point-blank shot hits. " + why);
            Assert.That(gun.Loaded, Is.LessThan(loaded));
            Assert.That(_host.Sim.Head.Marks.Any(m => m.DefId == "wound_gunshot"), Is.True);
        }

        [UnityTest]
        public IEnumerator ShootingAnEnemyInTheHeadPutsThemDown()
        {
            yield return Fight();
            EnemyAI enemy = Enemies[0];
            var weapon = _player.GetComponent<PlayerWeapon>();
            weapon.ForceAim = true;
            float t = 0f;
            while (!weapon.Ready && t < 2f) { t += Time.deltaTime; yield return null; }
            HitRegion head = enemy.GetComponentsInChildren<HitRegion>().First(z => z.Region == BodyRegion.Head);
            Vector3 from = Camera.main.transform.position;
            Vector3 aim = head.GetComponent<Collider>().bounds.center;
            // Shoot from just in front of the head so nothing in between can block the test.
            PlayerWeapon.ShotResult shot = weapon.FireAt(new Ray(aim - (aim - from).normalized * 1.5f, (aim - from).normalized));
            Assert.That(shot.Kind, Is.EqualTo(PlayerWeapon.ShotKind.Fired));
            yield return null;
            Character me = _host.Sim.FindCharacter(enemy.CharacterId);
            Assert.That(me.Downed, Is.True);
            Assert.That(enemy.Current, Is.EqualTo(EnemyAI.State.Down));
        }

        [UnityTest]
        public IEnumerator WhenTheGroupBreaksTheyRunOrGiveUp()
        {
            yield return Fight();
            yield return new WaitForSeconds(0.5f);
            Combatant leader = _host.Sim.Encounter.Combatants.Single(x => x.Leader);
            Assert.That(_host.Sim.Execute(new ShotHit { TargetId = leader.CharacterId, Region = BodyRegion.Head, ShooterId = _host.Sim.Head.Id, Distance = 20 }).Ok, Is.True);
            yield return new WaitForSeconds(1f);
            foreach (EnemyAI e in Enemies.Where(e => e.CharacterId != leader.CharacterId))
            {
                Combatant part = _host.Sim.Encounter?.Combatants.Find(x => x.CharacterId == e.CharacterId);
                if (part == null) continue;
                Assert.That(e.Current, Is.EqualTo(part.Intent == BreakIntent.Flee || part.State == CombatantState.Fled ? EnemyAI.State.Fleeing : EnemyAI.State.Surrendered), e.name);
            }
        }

        [UnityTest]
        public IEnumerator TheyStopShootingOnceYouAreDown()
        {
            yield return Fight();
            Character head = _host.Sim.Head;
            Assert.That(_host.Sim.Execute(new ShotHit { TargetId = head.Id, Region = BodyRegion.Head, Distance = 10 }).Ok, Is.True);
            Assert.That(head.Downed, Is.True);
            int[] ids = _host.Sim.Encounter.Combatants.Select(x => x.CharacterId).ToArray();
            int Rounds() => ids.Sum(id => _host.Sim.FindCharacter(id).Gear.Sum(g => g.Loaded));
            yield return new WaitForSeconds(1f);
            int before = Rounds();
            yield return new WaitForSeconds(6f);
            Assert.That(Rounds(), Is.EqualTo(before), "No rounds wasted on someone already down.");
        }
    }
}

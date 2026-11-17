using System.Collections.Generic;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;
using UnityEngine.AI;

namespace Hearthline.Bridge
{
    /// <summary>Something on a spawned enemy body that starts fighting once the body is placed (EnemyAI).</summary>
    public interface ISpawnedCombatant
    {
        void Begin(Transform player);
    }

    /// <summary>
    /// Stages fights (combat spec, step 4): when the simulation starts an encounter, spawns a body for each
    /// enemy in an arc ahead of the player at the encounter's spawn distance, bound to that enemy's sim
    /// character. Bodies of enemies who fled are removed; the fallen stay where they fell. A state loaded in
    /// the middle of a fight is re-staged around the player.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform player;
        [Tooltip("Seconds after a fight ends before the bodies of the ones who walked away are removed.")]
        [SerializeField] private float cleanupSeconds = 20f;

        private SimHost _host;
        private readonly Dictionary<int, GameObject> _bodies = new Dictionary<int, GameObject>();

        public IReadOnlyDictionary<int, GameObject> Bodies => _bodies;

        public void Configure(GameObject prefab, Transform playerTransform)
        {
            enemyPrefab = prefab;
            player = playerTransform;
        }

        private void OnEnable()
        {
            _host = SimHost.Instance;
            if (_host != null) _host.EventRaised += OnDomainEvent;
        }

        private void OnDisable()
        {
            if (_host != null) _host.EventRaised -= OnDomainEvent;
        }

        private void Start()
        {
            if (_host == null) { _host = SimHost.Instance; if (_host != null) _host.EventRaised += OnDomainEvent; }
            if (_host != null && _host.Sim.InEncounter && _bodies.Count == 0) Spawn(_host.Sim.Encounter);
            if (_host != null) foreach (DroppedLoot loot in _host.Sim.State.Dropped) PlaceLoot(loot.Id);
        }

        /// <summary>Puts a pickup for dropped loot by the body it came from (or by the player if that body is gone).</summary>
        public LootPickup PlaceLoot(int lootId)
        {
            foreach (LootPickup existing in FindObjectsByType<LootPickup>(FindObjectsInactive.Exclude))
                if (existing.LootId == lootId) return existing;
            DroppedLoot loot = _host.Sim.State.Dropped.Find(l => l.Id == lootId);
            if (loot == null || player == null) return null;
            Vector3 at = player.position + player.forward * 1.5f;
            if (loot.FromCharacterId.HasValue && _bodies.TryGetValue(loot.FromCharacterId.Value, out GameObject body) && body != null)
                at = body.transform.position;
            var pickup = new GameObject("Loot " + lootId).AddComponent<LootPickup>();
            pickup.transform.position = at;
            pickup.Configure(lootId);
            pickup.SetReach(2.4f);
            return pickup;
        }

        private void OnDomainEvent(DomainEvent e)
        {
            switch (e)
            {
                case EncounterStarted _:
                    Spawn(_host.Sim.Encounter);
                    break;
                case CombatantLeftFight left when left.State == CombatantState.Fled:
                    if (_bodies.TryGetValue(left.CharacterId, out GameObject gone)) { Destroy(gone); _bodies.Remove(left.CharacterId); }
                    break;
                case EncounterEnded ended:
                    foreach (int lootId in ended.LootIds) PlaceLoot(lootId);
                    var standing = new List<int>();
                    foreach (var kv in _bodies)
                    {
                        Character c = _host.Sim.FindCharacter(kv.Key);
                        if (c != null && c.Alive && !c.Downed) standing.Add(kv.Key);
                    }
                    foreach (int id in standing) { Destroy(_bodies[id], cleanupSeconds); _bodies.Remove(id); }
                    break;
            }
        }

        /// <summary>Spawns bodies for every enemy in the encounter that doesn't have one yet.</summary>
        public void Spawn(ActiveEncounter encounter)
        {
            if (encounter == null || enemyPrefab == null || player == null) return;
            EncounterDef def = _host.Sim.Content.Encounters.TryGetValue(encounter.EncounterId, out EncounterDef d) ? d : null;
            float minDist = def != null ? (float)def.SpawnDistanceMin : 20f, maxDist = def != null ? (float)def.SpawnDistanceMax : 35f;
            float spread = def != null ? (float)def.SpreadDegrees : 120f;
            Vector3 forward = player.forward;
            forward.y = 0f;
            forward.Normalize();
            var todo = encounter.Combatants.FindAll(x => !_bodies.ContainsKey(x.CharacterId) && x.State == CombatantState.Fighting);
            for (int i = 0; i < todo.Count; i++)
            {
                float angle = Mathf.Lerp(-spread / 2f, spread / 2f, (i + 0.5f) / todo.Count) + Random.Range(-8f, 8f);
                float distance = Random.Range(minDist, maxDist);
                Vector3 at = player.position + Quaternion.Euler(0f, angle, 0f) * forward * distance;
                if (NavMesh.SamplePosition(at, out NavMeshHit hit, 8f, NavMesh.AllAreas)) at = hit.position;
                Vector3 look = player.position - at;
                look.y = 0f;
                GameObject body = Instantiate(enemyPrefab, at, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
                Character c = _host.Sim.FindCharacter(todo[i].CharacterId);
                body.name = "Enemy " + (c != null ? (c.GivenName + " " + c.Surname).Trim() : todo[i].CharacterId.ToString());
                body.GetComponent<SimCharacterLink>().Bind(todo[i].CharacterId);
                _bodies[todo[i].CharacterId] = body;
                body.GetComponent<ISpawnedCombatant>()?.Begin(player);
            }
        }
    }
}

using System.Collections.Generic;
using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Stand-in figures for the household members who are at home (everyone but the head, minus anyone out on
    /// a trip), standing where their job is done: the woodcutter by the woodpile, the cook by the hearth.
    /// Rebuilt from the simulation whenever the household, jobs or trips change. Names show above them up close.
    /// </summary>
    public sealed class HouseholdFigures : MonoBehaviour
    {
        [SerializeField] private GameObject figure;
        [SerializeField] private RuntimeAnimatorController animator;
        [Tooltip("Where each job is done. Parallel to jobSpots.")]
        [SerializeField] private string[] jobIds = new string[0];
        [SerializeField] private Transform[] jobSpots = new Transform[0];
        [Tooltip("Where people without a job stand about.")]
        [SerializeField] private Transform[] idleSpots = new Transform[0];
        [SerializeField] private float nameDistance = 16f;

        private static readonly Color[] Tints =
        {
            new Color(0.75f, 0.55f, 0.4f), new Color(0.45f, 0.55f, 0.7f), new Color(0.6f, 0.65f, 0.45f),
            new Color(0.7f, 0.45f, 0.5f), new Color(0.55f, 0.5f, 0.65f), new Color(0.75f, 0.7f, 0.5f),
        };

        private readonly List<(GameObject go, string label)> _figures = new List<(GameObject, string)>();
        private SimHost _host;

        public int Count => _figures.Count;

        /// <summary>The figure standing for a character, or null.</summary>
        public GameObject FigureOf(int characterId)
        {
            foreach (var f in _figures) if (f.go.name == "Figure " + characterId) return f.go;
            return null;
        }

        /// <summary>Used by scene builders.</summary>
        public void Configure(GameObject figureModel, RuntimeAnimatorController controller, string[] jobs, Transform[] spots, Transform[] idle)
        {
            figure = figureModel;
            animator = controller;
            jobIds = jobs;
            jobSpots = spots;
            idleSpots = idle;
        }

        private void Start()
        {
            _host = SimHost.Instance;
            if (_host == null) return;
            _host.SimReplaced += Rebuild;
            _host.EventRaised += OnDomainEvent;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_host == null) return;
            _host.SimReplaced -= Rebuild;
            _host.EventRaised -= OnDomainEvent;
        }

        private void OnDomainEvent(DomainEvent e)
        {
            if (e is JobAssigned || e is CharacterJoined || e is CharacterDied || e is CharacterLeft || e is CharacterBorn
                || e is TripStarted || e is TripEnded || e is HeadSucceeded || e is CharacterDowned || e is CharacterRevived)
                Rebuild();
        }

        public void Rebuild()
        {
            foreach (var f in _figures) Destroy(f.go);
            _figures.Clear();
            Simulation sim = _host != null ? _host.Sim : null;
            if (sim == null || figure == null) return;

            var used = new Dictionary<Transform, int>();
            int idle = 0;
            foreach (int id in sim.State.Household.MemberIds)
            {
                Character c = sim.FindCharacter(id);
                if (c == null || !c.Alive || c == sim.Head) continue;
                if (sim.OnTrip && sim.State.Trip.PartyIds.Contains(c.Id)) continue;

                Transform spot = null;
                int j = c.JobId == null ? -1 : System.Array.IndexOf(jobIds, c.JobId);
                if (j >= 0 && j < jobSpots.Length) spot = jobSpots[j];
                if (spot == null && idleSpots.Length > 0) spot = idleSpots[idle++ % idleSpots.Length];
                if (spot == null) spot = transform;
                used.TryGetValue(spot, out int crowd);
                used[spot] = crowd + 1;

                Vector3 at = spot.position + spot.right * (crowd * 1.2f);
                GameObject go = Instantiate(figure, at, spot.rotation, transform);
                go.name = "Figure " + c.Id;
                var anim = go.GetComponentInChildren<Animator>();
                if (anim != null && animator != null) anim.runtimeAnimatorController = animator;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", Tints[c.Id % Tints.Length]);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
                string job = c.JobId != null && _host.Content.Jobs.TryGetValue(c.JobId, out var def) ? def.Name.ToLowerInvariant() : (c.Downed ? "laid up" : "no work");
                _figures.Add((go, c.GivenName + " · " + job));
            }
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            foreach (var f in _figures)
            {
                Vector3 head = f.go.transform.position + Vector3.up * 2.1f;
                if (Vector3.Distance(cam.transform.position, head) > nameDistance) continue;
                Vector3 s = cam.WorldToScreenPoint(head);
                if (s.z <= 0f) continue;
                GUI.Label(new Rect(s.x - 90f, Screen.height - s.y - 12f, 180f, 24f), f.label, style);
            }
        }
    }
}

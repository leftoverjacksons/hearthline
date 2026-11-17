using System.Collections.Generic;
using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// A straw-stuffed figure at the homestead for practice (combat spec, step 2). It has the same hit zones
    /// as a person, but hits on it are not people: nothing reaches the simulation. It shows where the last
    /// shots landed and how bad each would have been on a person (the simulation's own severity rule).
    /// </summary>
    public sealed class PracticeTarget : MonoBehaviour
    {
        public readonly struct Shot
        {
            public readonly BodyRegion Region; public readonly int Severity; public readonly float Time;
            public Shot(BodyRegion region, int severity, float time) { Region = region; Severity = severity; Time = time; }
        }

        [Tooltip("How long a shot's label stays up, seconds.")]
        [SerializeField] private float labelSeconds = 4f;

        private readonly List<Shot> _shots = new List<Shot>();

        public IReadOnlyList<Shot> Shots => _shots;
        public int Hits => _shots.Count;

        public void Register(BodyRegion region, int severity)
        {
            _shots.Add(new Shot(region, severity, Time.time));
            if (_shots.Count > 50) _shots.RemoveAt(0);
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null || _shots.Count == 0) return;
            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.1f);
            if (screen.z < 0f) return;
            float y = Screen.height - screen.y;
            int shown = 0;
            for (int i = _shots.Count - 1; i >= 0 && shown < 4; i--)
            {
                Shot s = _shots[i];
                if (Time.time - s.Time > labelSeconds) break;
                string label = Naming.ToSnake(s.Region).Replace('_', ' ') + "  (" + (s.Severity >= 3 ? "drops them" : "wound " + s.Severity) + ")";
                GUI.Label(new Rect(screen.x - 80f, y - 22f * shown, 200f, 22f), label);
                shown++;
            }
        }
    }
}

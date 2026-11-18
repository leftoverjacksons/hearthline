using Hearthline.Bridge;
using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Walking into a place for the first time (docs/specs/ruins-and-places.md): tells the simulation
    /// (<see cref="DiscoverPlace"/>, which writes the chronicle) and fades the place's name in at the top of the
    /// screen. Places already known stay quiet.
    /// </summary>
    public sealed class PlaceDiscovery : MonoBehaviour
    {
        [SerializeField] private PlayerLocation location;
        [Tooltip("Seconds the name takes to fade in, stays, and fades out.")]
        [SerializeField] private float fadeIn = 1.2f, hold = 3f, fadeOut = 1.5f;
        [SerializeField] private int fontSize = 30;

        private SimHost _host;
        private string _title = "";
        private float _shownAt = -100f;
        private GUIStyle _style;

        /// <summary>The name on screen now (empty when none), for tests.</summary>
        public string Title => Time.time - _shownAt < fadeIn + hold + fadeOut ? _title : "";

        public void Configure(PlayerLocation where) => location = where;

        private void Start() => _host = SimHost.Instance;

        private void Update()
        {
            if (_host == null || _host.Sim == null || location == null) return;
            Simulation sim = _host.Sim;
            string id = location.PlaceId;
            if (id.Length == 0 || sim.IsDiscovered(id) || sim.State.ActiveEvent != null) return;
            CommandResult r = sim.Execute(new DiscoverPlace { PlaceId = id, Kind = location.PlaceKind });
            if (!r.Ok) return;
            foreach (DomainEvent e in r.Events)
                if (e is PlaceDiscovered d) Show(d.Name);
        }

        public void Show(string name)
        {
            _title = name.Length > 0 ? char.ToUpperInvariant(name[0]) + name.Substring(1) : name;
            _shownAt = Time.time;
        }

        private void OnGUI()
        {
            float t = Time.time - _shownAt;
            if (t > fadeIn + hold + fadeOut || _title.Length == 0) return;
            float a = t < fadeIn ? t / fadeIn : t < fadeIn + hold ? 1f : 1f - (t - fadeIn - hold) / fadeOut;
            _style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
            _style.fontSize = fontSize;
            Color was = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, a * 0.6f);
            GUI.Label(new Rect(2f, Screen.height * 0.12f + 2f, Screen.width, 50f), _title, _style);
            GUI.color = new Color(0.95f, 0.92f, 0.85f, a);
            GUI.Label(new Rect(0f, Screen.height * 0.12f, Screen.width, 50f), _title, _style);
            GUI.color = was;
        }

        /// <summary>Where the nearest place of a kind is from a point, as "240 m north-east"; null if none.</summary>
        public static string Bearing(string kind, Vector3 from)
        {
            AreaVolume best = null;
            float bestDistance = float.MaxValue;
            foreach (AreaVolume v in AreaVolume.All)
            {
                if (v.PlaceKind != kind) continue;
                float d = Vector2.Distance(new Vector2(v.transform.position.x, v.transform.position.z), new Vector2(from.x, from.z));
                if (d < bestDistance) { bestDistance = d; best = v; }
            }
            if (best == null) return null;
            if (bestDistance < 25f) return "here";
            Vector3 to = best.transform.position - from;
            float angle = Mathf.Repeat(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + 22.5f, 360f);
            string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return (Mathf.Round(bestDistance / 10f) * 10f).ToString("0") + " m " + names[(int)(angle / 45f) % 8];
        }
    }
}

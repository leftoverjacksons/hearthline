using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;
using UnityEngine.AI;

namespace Hearthline.Game
{
    /// <summary>
    /// Stages the hunt (docs/specs/hunt.md):
    /// - lays the trail of sign through the hunt's area from the simulation's seed, the same after a load;
    /// - shows the next sign to read, with a line pointing to the one after it;
    /// - brings the animal out at the trail's end once it's found;
    /// - lays a fresh stretch when it bolts, and cleans up when the hunt ends.
    ///
    /// Positions are presentation; progress, bolts and the kill are simulation state.
    /// </summary>
    public sealed class HuntDirector : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameObject deerPrefab;
        [Tooltip("Distance between signs, m (min, max).")]
        [SerializeField] private Vector2 spacing = new Vector2(24f, 38f);

        private SimHost _host;
        private readonly List<Vector3> _trail = new List<Vector3>();
        private SignSpot _sign;
        private GameObject _deer;
        private string _message;
        private float _messageTimer;

        public IReadOnlyList<Vector3> Trail => _trail;
        public SignSpot CurrentSign => _sign;
        public GameObject Deer => _deer;
        public string Message => _messageTimer > 0f ? _message : null;

        public void Configure(Transform playerTransform, GameObject deer) { player = playerTransform; deerPrefab = deer; }

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
            if (_host?.Sim.Hunt != null) Stage(_host.Sim.Hunt, announce: false);
        }

        private void Update() => _messageTimer -= Time.unscaledDeltaTime;

        private void Say(string text, float seconds = 6f) { _message = text; _messageTimer = seconds; }

        private void OnDomainEvent(DomainEvent e)
        {
            Simulation sim = _host.Sim;
            switch (e)
            {
                case HuntStarted _:
                    Stage(sim.Hunt, announce: true);
                    break;
                case SignRead read:
                    if (read.Revealed)
                    {
                        ClearSign();
                        SpawnDeer(sim.Hunt);
                        Say("He's close. Fresh sign everywhere: slow down, stay low.");
                    }
                    else
                    {
                        PlaceSign(sim.Hunt);
                        Say(Hint(read.Index) + " " + (read.Remaining <= 2 ? "He can't be far." : ""));
                    }
                    break;
                case AnimalBolted _:
                    if (_deer != null) { Destroy(_deer, 1f); _deer = null; } // he's over the rise and gone
                    LayTrail(sim.Hunt);
                    PlaceSign(sim.Hunt);
                    Say("He's gone, white tail flagging. The trail picks up again " + Compass(player.position, _sign != null ? _sign.transform.position : player.position) + ".");
                    break;
                case HuntEnded ended:
                    ClearSign();
                    if (_deer != null && ended.Outcome != "won") Destroy(_deer);
                    _trail.Clear();
                    if (ended.Outcome == "won") Say("A fine animal. Get him home before the meat turns.");
                    break;
            }
        }

        /// <summary>Lays out the hunt as the simulation has it (also after a load).</summary>
        public void Stage(ActiveHunt hunt, bool announce)
        {
            if (hunt == null || player == null) return;
            LayTrail(hunt);
            if (hunt.Revealed) SpawnDeer(hunt);
            else PlaceSign(hunt);
            if (announce && _sign != null)
            {
                HuntDef def = _host.Sim.HuntDefOf(hunt.HuntId);
                Say((def != null ? def.Offer + " " : "") + "Start looking " + Compass(player.position, _sign.transform.position) + ", about " + Mathf.RoundToInt(Vector3.Distance(player.position, _sign.transform.position) / 10f) * 10 + " m off.", 9f);
            }
        }

        // The trail for this stretch: one point per sign still to read, then where the animal is.
        private void LayTrail(ActiveHunt hunt)
        {
            _trail.Clear();
            HuntDef def = _host.Sim.HuntDefOf(hunt.HuntId);
            var rnd = new System.Random(hunt.Seed + hunt.Bolts * 7919);
            AreaVolume area = PickArea(def, rnd);
            Vector3 home = Vector3.zero;
            foreach (AreaVolume a in AreaVolume.All) if (a.IsHomestead) home = a.transform.position;
            Vector3 start;
            if (area != null)
            {
                // Just inside the area's edge nearest home (first stretch) or somewhere deeper in (after a bolt).
                Bounds b = new Bounds(area.transform.position, Vector3.Scale(area.Size, area.transform.lossyScale));
                Vector3 edge = b.ClosestPoint(home);
                Vector3 inward = (b.center - edge); inward.y = 0f;
                start = edge + inward.normalized * (hunt.Bolts == 0 ? 18f : 40f + (float)rnd.NextDouble() * 50f)
                    + new Vector3((float)(rnd.NextDouble() - 0.5) * 60f, 0f, (float)(rnd.NextDouble() - 0.5) * 60f);
            }
            else start = player.position + new Vector3(40f, 0f, 40f);
            float heading = (float)rnd.NextDouble() * 360f;
            int points = (def != null ? def.Sign : 6) - hunt.Progress + 1;
            Vector3 p = Snap(start);
            for (int i = 0; i < points; i++)
            {
                _trail.Add(p);
                heading += (float)(rnd.NextDouble() - 0.5) * 80f;
                Vector3 next = p + Quaternion.Euler(0f, heading, 0f) * Vector3.forward * Mathf.Lerp(spacing.x, spacing.y, (float)rnd.NextDouble());
                if (area != null && !area.Contains(next)) { heading += 150f; next = p + Quaternion.Euler(0f, heading, 0f) * Vector3.forward * spacing.x; }
                p = Snap(next);
            }
        }

        private static AreaVolume PickArea(HuntDef def, System.Random rnd)
        {
            var fits = new List<AreaVolume>();
            foreach (AreaVolume a in AreaVolume.All)
                if (def != null && !a.IsHomestead && def.AreaTagsAny.Exists(t => System.Linq.Enumerable.Contains(a.AreaTags, t))) fits.Add(a);
            fits.Sort((x, y) => string.CompareOrdinal(x.PlaceName, y.PlaceName));
            return fits.Count > 0 ? fits[rnd.Next(fits.Count)] : null;
        }

        private static Vector3 Snap(Vector3 p)
        {
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 12f, NavMesh.AllAreas)) return hit.position;
            if (Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out RaycastHit g, 600f, ~0, QueryTriggerInteraction.Ignore)) return g.point;
            return p;
        }

        private void PlaceSign(ActiveHunt hunt)
        {
            ClearSign();
            int local = 0; // the trail starts at the current progress
            if (_trail.Count == 0) return;
            var go = new GameObject("Sign " + hunt.Progress);
            go.transform.position = _trail[local];
            _sign = go.AddComponent<SignSpot>();
            string kind = SignSpot.Kinds[(hunt.Seed + hunt.Progress) % SignSpot.Kinds.Length];
            _sign.Configure(hunt.Progress, kind);
            _sign.SetReach(2.5f);
            Marks(go.transform, kind);
            _trail.RemoveAt(0);
        }

        private void ClearSign()
        {
            if (_sign != null) Destroy(_sign.gameObject);
            _sign = null;
        }

        // Small marks on the ground so a sign can be spotted: dark hoofprints and a snapped twig.
        private static void Marks(Transform at, string kind)
        {
            var dark = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.12f, 0.09f, 0.06f) };
            for (int i = 0; i < 6; i++)
            {
                var print = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(print.GetComponent<Collider>());
                print.transform.SetParent(at, false);
                print.transform.localPosition = new Vector3((i % 2 == 0 ? -0.1f : 0.1f), 0.01f, i * 0.45f - 1.1f);
                print.transform.localScale = new Vector3(0.07f, 0.02f, 0.1f);
                print.GetComponent<Renderer>().sharedMaterial = dark;
            }
            var twig = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(twig.GetComponent<Collider>());
            twig.transform.SetParent(at, false);
            twig.transform.localPosition = new Vector3(0.4f, 0.05f, 0.2f);
            twig.transform.localRotation = Quaternion.Euler(80f, 30f, 0f);
            twig.transform.localScale = new Vector3(0.02f, 0.25f, 0.02f);
            twig.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.55f, 0.42f, 0.28f) };
        }

        private void SpawnDeer(ActiveHunt hunt)
        {
            if (_deer != null) Destroy(_deer);
            if (deerPrefab == null || _trail.Count == 0) return;
            Vector3 at = _trail[_trail.Count - 1];
            Vector3 face = player.position - at; face.y = 0f;
            _deer = Instantiate(deerPrefab, at, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? -face : Vector3.forward));
            _deer.name = "Crookhorn";
            _deer.GetComponent<SimCharacterLink>().Bind(hunt.AnimalId);
            _deer.GetComponent<ISpawnedCombatant>()?.Begin(player);
        }

        private string Hint(int index)
        {
            Vector3 next = _sign != null ? _sign.transform.position : player.position;
            string kind = _sign != null ? _sign.Kind.ToLowerInvariant() : "sign";
            return "The sign leads " + Compass(player.position, next) + ". Look for " + kind + ".";
        }

        private static string Compass(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; d.y = 0f;
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return names[((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8];
        }

        private void OnGUI()
        {
            if (Message == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = true };
            var rect = new Rect(Screen.width / 2f - 340f, Screen.height * 0.26f, 680f, 52f);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, Message, style);
        }
    }
}

using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Life around the player (docs/specs/life-and-weather.md), presentation only and never saved:
    /// <list type="bullet">
    /// <item>birds perched in the tree crowns and foraging in the grass, which flush when you come close, when a
    /// gun goes off, or when a deer bolts;</item>
    /// <item>flocks crossing high overhead now and then;</item>
    /// <item>gnats over the water by day;</item>
    /// <item>fireflies on summer evenings.</item>
    /// </list>
    /// It all thins out in the deep woods, keeps under cover in rain, and is gone in Thin places, where the
    /// silence is the tell. Every number is a serialized field for tuning.
    /// </summary>
    public sealed class AmbientLife : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private TravelClock clock;
        [SerializeField] private PlayerLocation location;

        [Header("Birds")]
        [SerializeField] private int maxBirds = 18;
        [Tooltip("Birds keep to a ring this far from the player (m): they never appear right beside you.")]
        [SerializeField] private Vector2 birdRing = new Vector2(28f, 75f);
        [SerializeField] private float flushDistance = 12f;
        [SerializeField] private float flushDistanceCrouched = 6f;
        [SerializeField] private float flushOnShot = 60f;
        [SerializeField] private float flushOnDeer = 25f;
        [Tooltip("Share of birds foraging on the ground rather than up in a tree.")]
        [SerializeField, Range(0f, 1f)] private float groundShare = 0.35f;

        [Header("Flocks")]
        [SerializeField] private Vector2 flockEvery = new Vector2(70f, 200f);
        [SerializeField] private Vector2Int flockSize = new Vector2Int(7, 18);
        [SerializeField] private float flockHeight = 55f;

        [Header("Insects")]
        [SerializeField] private float gnatsWithin = 45f;
        [SerializeField] private Vector2 gnatHours = new Vector2(7f, 19.5f);
        [SerializeField] private Vector2 fireflyHours = new Vector2(19.5f, 24f);
        [SerializeField] private float firefliesPerSecond = 14f;

        [Header("Sound (CC0, see Assets/Hearthline/Art/Audio/CREDITS.md)")]
        [Tooltip("Birdsong and breeze by day; follows how much life there is here.")]
        [SerializeField] private AudioClip dayLoop;
        [Tooltip("Crickets outside winter, from dusk.")]
        [SerializeField] private AudioClip nightLoop;
        [SerializeField, Range(0f, 1f)] private float dayVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float nightVolume = 0.45f;
        [SerializeField] private float soundFadePerSecond = 0.25f;

        public readonly List<Bird> Birds = new List<Bird>();
        public float DaySoundVolume => _day != null ? _day.volume : 0f;
        public float NightSoundVolume => _night != null ? _night.volume : 0f;
        private AudioSource _day, _night;

        /// <summary>Used by the scene builder.</summary>
        public void SetSounds(AudioClip day, AudioClip night)
        {
            dayLoop = day;
            nightLoop = night;
        }
        public int PerchedCount { get { int n = 0; foreach (Bird b in Birds) if (b.Perched) n++; return n; } }
        public float Density { get; private set; }
        public bool FirefliesOn { get; private set; }
        public bool GnatsOn { get; private set; }
        public ParticleSystem Fireflies => _fireflies;

        private readonly List<Bird> _flock = new List<Bird>();
        private float _nextFlock, _nextCheck;
        private ParticleSystem _fireflies;
        private readonly List<ParticleSystem> _gnats = new List<ParticleSystem>();
        private Crouch _crouch;
        private Collider[] _trunks = new Collider[0];
        private Material _birdMaterial, _glow, _gnatMaterial;
        private Mesh _birdBody, _birdWing;

        public void Configure(Transform playerTransform, TravelClock travelClock, PlayerLocation playerLocation)
        {
            player = playerTransform;
            clock = travelClock;
            location = playerLocation;
        }

        private void Start()
        {
            if (player != null) _crouch = player.GetComponent<Crouch>();
            var trunks = GameObject.Find("Tree Trunks");
            if (trunks != null) _trunks = trunks.GetComponentsInChildren<Collider>();
            _birdMaterial = Unlit(new Color(0.12f, 0.11f, 0.1f));
            _birdBody = BirdBody();
            _birdWing = BirdWing();
            _glow = Glow(new Color(0.75f, 1f, 0.35f));
            _gnatMaterial = Glow(new Color(0.25f, 0.25f, 0.2f), additive: false);
            _fireflies = BuildFireflies();
            for (int i = 0; i < 3; i++) _gnats.Add(BuildGnats(i));
            _nextFlock = Time.time + Random.Range(flockEvery.x * 0.3f, flockEvery.y * 0.5f);
            PlayerWeapon.ShotFired += OnShot;
            _day = Loop("Day Sound", dayLoop);
            _night = Loop("Night Sound", nightLoop);
        }

        private AudioSource Loop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.clip = clip;
            if (clip != null) s.Play();
            return s;
        }

        private void OnDestroy() => PlayerWeapon.ShotFired -= OnShot;

        private float Hour => clock != null ? clock.Hour : 12f;
        private static Simulation Sim => SimHost.Instance != null ? SimHost.Instance.Sim : null;

        private WeatherKind Weather => Sim != null ? Sim.WeatherAt(Hour) : WeatherKind.Clear;

        private bool Has(string tag)
        {
            if (location == null) return false;
            foreach (string t in location.AreaTags) if (t == tag) return true;
            return false;
        }

        /// <summary>How much life there is here and now, 0 to 1.</summary>
        private float LifeHere()
        {
            if (location != null && location.ThinLevel > 0) return 0f; // the silence is the tell
            float d = Has("deep_woods") || Has("hollow") ? 0.5f : 1f;
            WeatherKind w = Weather;
            if (w == WeatherKind.Storm) d *= 0.05f;
            else if (w == WeatherKind.Rain) d *= 0.25f;
            else if (w == WeatherKind.Fog) d *= 0.6f;
            float h = Hour;
            if (h < 5f || h > 21f) d *= 0.1f; // birds roost at night
            return d;
        }

        private void Update()
        {
            if (player == null) return;
            Density = LifeHere();
            UpdateBirds();
            UpdateFlocks();
            UpdateInsects();
            UpdateSound();
        }

        // Birdsong by day where there is life; crickets on evenings and nights outside winter; nothing in Thin places.
        private void UpdateSound()
        {
            float h = Hour;
            bool thin = location != null && location.ThinLevel > 0;
            float daylight = Mathf.Clamp01(Mathf.InverseLerp(5f, 7f, h)) * Mathf.Clamp01(Mathf.InverseLerp(20.5f, 19f, h));
            float dayTarget = thin ? 0f : dayVolume * Density * daylight;
            Simulation sim = Sim;
            GameDate date = sim != null ? sim.Date : new GameDate(GameDate.DaysPerSeason + 1);
            bool cold = date.Season == Season.Winter;
            bool dry = Weather != WeatherKind.Rain && Weather != WeatherKind.Storm;
            float night = Mathf.Max(Mathf.InverseLerp(18.5f, 20.5f, h), Mathf.InverseLerp(5.5f, 4f, h));
            float nightTarget = thin || cold || !dry ? 0f : nightVolume * night * (Has("deep_woods") ? 0.6f : 1f);
            float step = soundFadePerSecond * Time.deltaTime;
            if (_day != null) _day.volume = Mathf.MoveTowards(_day.volume, dayTarget, step);
            if (_night != null) _night.volume = Mathf.MoveTowards(_night.volume, nightTarget, step);
        }

        // ---- Birds -----------------------------------------------------------------------------------

        private void UpdateBirds()
        {
            Vector3 p = player.position;
            float flush = _crouch != null && _crouch.IsCrouched ? flushDistanceCrouched : flushDistance;
            for (int i = Birds.Count - 1; i >= 0; i--)
            {
                Bird b = Birds[i];
                if (b.Perched && Flat(b.transform.position - p) < flush) b.Flush(p);
                if (b.Gone || (b.Perched && Flat(b.transform.position - p) > birdRing.y * 1.4f) || (b.Perched && Density <= 0f))
                {
                    Destroy(b.gameObject);
                    Birds.RemoveAt(i);
                }
            }
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.5f;
            FlushNearDeer();
            int want = Mathf.RoundToInt(maxBirds * Density);
            for (int tries = 0; tries < 6 && PerchedCount < want; tries++) SpawnBird(p);
            // Too many for the weather or the hour (rain coming on, dusk): one at a time, they leave for cover.
            if (PerchedCount > want)
                foreach (Bird b in Birds) if (b.Perched) { b.Flush(b.transform.position + Random.insideUnitSphere); break; }
        }

        private void FlushNearDeer()
        {
            foreach (DeerAI deer in FindObjectsByType<DeerAI>(FindObjectsInactive.Exclude))
            {
                if (deer.Current != DeerAI.State.Flee) continue;
                foreach (Bird b in Birds) if (b.Perched && Vector3.Distance(b.transform.position, deer.transform.position) < flushOnDeer) b.Flush(deer.transform.position);
            }
        }

        private void OnShot(Vector3 from)
        {
            foreach (Bird b in Birds) if (b.Perched && Vector3.Distance(b.transform.position, from) < flushOnShot) b.Flush(from);
        }

        private void SpawnBird(Vector3 around)
        {
            bool ground = Random.value < groundShare || _trunks.Length == 0;
            Vector3 spot;
            if (!ground)
            {
                // A tree in the ring: perch in its crown.
                Collider trunk = _trunks[Random.Range(0, _trunks.Length)];
                float d = Flat(trunk.transform.position - around);
                if (d < birdRing.x || d > birdRing.y) return;
                Bounds b = trunk.bounds;
                spot = new Vector3(b.center.x, b.max.y + Random.Range(0.5f, 2.5f), b.center.z) + Random.insideUnitSphere * 1.2f;
            }
            else
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(birdRing.x, birdRing.y);
                Vector3 at = around + new Vector3(r.x, 0f, r.y);
                if (!Physics.Raycast(at + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) return;
                if (hit.collider.GetComponentInParent<CharacterController>() != null) return;
                spot = hit.point + Vector3.up * 0.05f;
            }
            Birds.Add(MakeBird(spot, perched: true));
        }

        private Bird MakeBird(Vector3 at, bool perched)
        {
            var go = new GameObject(perched ? "Bird" : "Flock Bird");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            float scale = Random.Range(0.8f, 1.2f);
            go.transform.localScale = Vector3.one * scale;
            Part(go.transform, "Body", _birdBody, Vector3.zero, Quaternion.identity);
            Transform left = Part(go.transform, "Wing L", _birdWing, new Vector3(-0.03f, 0.03f, 0f), Quaternion.identity);
            Transform right = Part(go.transform, "Wing R", _birdWing, new Vector3(0.03f, 0.03f, 0f), Quaternion.Euler(0f, 180f, 0f));
            var bird = go.AddComponent<Bird>();
            bird.Setup(left, right, perched);
            return bird;
        }

        private Transform Part(Transform parent, string name, Mesh mesh, Vector3 local, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _birdMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        // ---- Flocks ----------------------------------------------------------------------------------

        private void UpdateFlocks()
        {
            _flock.RemoveAll(b => b == null);
            if (Time.time < _nextFlock) return;
            _nextFlock = Time.time + Random.Range(flockEvery.x, flockEvery.y);
            if (Density < 0.4f || Hour < 6f || Hour > 20f) return;
            SendFlock();
        }

        /// <summary>A flock crossing overhead. Public for the cheat panel.</summary>
        public void SendFlock()
        {
            if (player == null) return;
            Vector2 dir2 = Random.insideUnitCircle.normalized;
            var dir = new Vector3(dir2.x, 0f, dir2.y);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 start = player.position - dir * 260f + side * Random.Range(-80f, 80f) + Vector3.up * (flockHeight + Random.Range(-10f, 20f));
            int n = Random.Range(flockSize.x, flockSize.y + 1);
            bool vee = Random.value < 0.5f;
            for (int i = 0; i < n; i++)
            {
                Vector3 offset = vee
                    ? -dir * (i / 2) * 3f + side * ((i % 2 == 0 ? 1 : -1) * (i / 2) * 2.5f)
                    : Random.insideUnitSphere * 9f;
                Bird b = MakeBird(start + offset, perched: false);
                b.transform.localScale *= 1.6f;
                b.Cross(dir, Random.Range(11f, 14f));
                _flock.Add(b);
            }
        }

        public int FlockCount => _flock.Count;

        // ---- Insects ---------------------------------------------------------------------------------

        private void UpdateInsects()
        {
            float h = Hour;
            Simulation sim = Sim;
            GameDate date = sim != null ? sim.Date : new GameDate(GameDate.DaysPerSeason + 1);
            bool warm = date.Season == Season.Summer || (date.Season == Season.Spring && date.DayOfSeason > GameDate.DaysPerSeason / 2);
            bool dry = Weather != WeatherKind.Rain && Weather != WeatherKind.Storm;
            bool open = !(location != null && location.ThinLevel > 0) && !Has("deep_woods");

            // Fireflies: dusk till midnight, in the warm part of the year, in meadows and at woods' edges.
            FirefliesOn = warm && dry && open && h >= fireflyHours.x && h <= fireflyHours.y;
            var emission = _fireflies.emission;
            emission.rateOverTime = FirefliesOn ? firefliesPerSecond : 0f;
            _fireflies.transform.position = player.position;

            // Gnats: over the water near you, by day, not in rain.
            GnatsOn = false;
            int used = 0;
            if (dry && Density > 0f && h >= gnatHours.x && h <= gnatHours.y && date.Season != Season.Winter)
            {
                foreach (AreaVolume a in AreaVolume.All)
                {
                    if (used >= _gnats.Count) break;
                    if (!Contains(a.AreaTags, "creek") || a.AreaTags.Count > 1) continue;
                    Vector3 c = a.transform.position;
                    if (Flat(c - player.position) > gnatsWithin) continue;
                    if (Physics.Raycast(new Vector3(c.x, player.position.y + 100f, c.z), Vector3.down, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                        c.y = hit.point.y + 1.1f;
                    ParticleSystem g = _gnats[used++];
                    if (Vector3.Distance(g.transform.position, c) > 3f) g.transform.position = c;
                    var e = g.emission;
                    e.rateOverTime = 25f;
                    GnatsOn = true;
                }
            }
            for (int i = used; i < _gnats.Count; i++) { var e = _gnats[i].emission; e.rateOverTime = 0f; }
        }

        private static bool Contains(IReadOnlyList<string> tags, string tag)
        {
            foreach (string t in tags) if (t == tag) return true;
            return false;
        }

        private ParticleSystem BuildFireflies()
        {
            ParticleSystem ps = Particles("Fireflies", _glow, 600);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.18f);
            main.startColor = new Color(0.8f, 1f, 0.4f, 1f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50f, 2.5f, 50f);
            shape.position = new Vector3(0f, 1.2f, 0f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.2f;
            // Each one blinks: on, off, on, fading.
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 0.3f), new GradientAlphaKey(0f, 0.5f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 0.8f) });
            colour.color = g;
            return ps;
        }

        private ParticleSystem BuildGnats(int i)
        {
            ParticleSystem ps = Particles("Gnats " + i, _gnatMaterial, 120);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.9f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.4f;
            noise.frequency = 1.5f;
            noise.scrollSpeed = 1.2f;
            return ps;
        }

        private ParticleSystem Particles(string name, Material material, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        // ---- Look ------------------------------------------------------------------------------------

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        private static Material Unlit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Bird" };
            m.SetColor("_BaseColor", c);
            return m;
        }

        private static Material Glow(Color c, bool additive = true)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = additive ? "Firefly" : "Gnat" };
            m.SetFloat("_Surface", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetColor("_BaseColor", additive ? c * 2.5f : c);
            return m;
        }

        // A songbird-sized body: a stretched diamond, about 15 cm long.
        private static Mesh BirdBody()
        {
            var m = new Mesh { name = "Bird Body" };
            m.vertices = new[]
            {
                new Vector3(0f, 0.02f, 0.09f), new Vector3(0f, 0.02f, -0.08f), new Vector3(-0.025f, 0.02f, 0f),
                new Vector3(0.025f, 0.02f, 0f), new Vector3(0f, 0.05f, 0.01f), new Vector3(0f, -0.005f, 0f),
            };
            m.triangles = new[] { 0, 4, 3, 0, 2, 4, 1, 3, 4, 1, 4, 2, 0, 3, 5, 0, 5, 2, 1, 5, 3, 1, 2, 5 };
            m.RecalculateNormals();
            return m;
        }

        // One wing: a thin triangle reaching out along -x (mirrored for the other side).
        private static Mesh BirdWing()
        {
            var m = new Mesh { name = "Bird Wing" };
            m.vertices = new[] { new Vector3(0f, 0f, 0.035f), new Vector3(0f, 0f, -0.03f), new Vector3(-0.13f, 0f, -0.02f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            m.RecalculateNormals();
            return m;
        }
    }

    /// <summary>One ambient bird: perched (pecking, looking about) until something flushes it, then away.</summary>
    public sealed class Bird : MonoBehaviour
    {
        private Transform _left, _right;
        private Vector3 _velocity;
        private float _flap, _life, _hop;
        private bool _flying;

        public bool Perched => !_flying;
        public bool Gone { get; private set; }

        public void Setup(Transform left, Transform right, bool perched)
        {
            _left = left;
            _right = right;
            _flying = !perched;
            _hop = Random.Range(1f, 4f);
            Fold(perched);
        }

        /// <summary>Up and away from the danger, climbing.</summary>
        public void Flush(Vector3 from)
        {
            if (_flying) return;
            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            _velocity = away.normalized * Random.Range(9f, 13f) + Vector3.up * Random.Range(4f, 7f);
            _flying = true;
            _life = 10f;
            Fold(false);
        }

        /// <summary>Flies straight across the sky (a flock).</summary>
        public void Cross(Vector3 dir, float speed)
        {
            _flying = true;
            _velocity = dir * speed;
            _life = 520f / speed;
            Fold(false);
        }

        private void Fold(bool folded)
        {
            if (_left == null) return;
            _left.localRotation = Quaternion.Euler(0f, 0f, folded ? -80f : 0f);
            _right.localRotation = Quaternion.Euler(0f, 180f, folded ? -80f : 0f);
        }

        private void Update()
        {
            if (!_flying)
            {
                // Hop and turn now and then.
                _hop -= Time.deltaTime;
                if (_hop <= 0f)
                {
                    _hop = Random.Range(1.5f, 5f);
                    transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + Random.Range(-90f, 90f), 0f);
                }
                return;
            }
            _life -= Time.deltaTime;
            if (_life <= 0f) { Gone = true; Destroy(gameObject); return; }
            _velocity.y = Mathf.MoveTowards(_velocity.y, 1f, Time.deltaTime * 2f);
            transform.position += _velocity * Time.deltaTime;
            if (_velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(_velocity);
            _flap += Time.deltaTime * 16f;
            float a = Mathf.Sin(_flap) * 55f;
            _left.localRotation = Quaternion.Euler(0f, 0f, a);
            _right.localRotation = Quaternion.Euler(0f, 180f, a);
        }
    }
}

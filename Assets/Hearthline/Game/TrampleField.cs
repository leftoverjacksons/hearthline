using System.Collections.Generic;
using Hearthline.Bridge;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Matted grass (docs/specs/trampled-grass.md, ADR 0034). A small map that follows the player, moving in
    /// whole texels: the player, deer and enemies press the grass at their feet into it, along the way they were
    /// moving, and it recovers over a few minutes. Presentation only, not saved. Published as
    /// <c>_HL_TrampleMap</c> for GrassBlade.shader.
    /// Also counts the player's steps and tells <see cref="WearView"/> where they walked, so paths wear in.
    /// </summary>
    public sealed class TrampleField : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private TravelClock clock;
        [SerializeField] private WearView wear;

        [Header("Matting")]
        [Tooltip("Side of the square map around the player, in metres.")]
        [SerializeField] private float size = 64f;
        [Tooltip("Metres per texel.")]
        [SerializeField] private float texel = 0.25f;
        [Tooltip("Seconds for fully matted grass to stand back up.")]
        [SerializeField] private float recoverSeconds = 180f;
        [Tooltip("Half-width of the pressed oval across the way of travel, in metres.")]
        [SerializeField] private float footWidth = 0.4f;
        [Tooltip("Half-length of the pressed oval along the way of travel, in metres.")]
        [SerializeField] private float footLength = 0.6f;
        [SerializeField, Range(0f, 1f)] private float playerPress = 1f;
        [SerializeField, Range(0f, 1f)] private float deerPress = 0.7f;
        [SerializeField, Range(0f, 1f)] private float enemyPress = 1f;

        [Header("Desire paths")]
        [Tooltip("The player's steps are counted toward wear every this many metres walked.")]
        [SerializeField] private float treadEvery = 1f;
        [Tooltip("Moves faster than this (metres per second) are not walking: teleports, cheats, riding.")]
        [SerializeField] private float maxWalkSpeed = 12f;

        private static readonly int MapId = Shader.PropertyToID("_HL_TrampleMap"), RectId = Shader.PropertyToID("_HL_TrampleRect");

        private int _res;
        private Color32[] _pixels;
        private Color32[] _scratch;
        private Texture2D _texture;
        private Vector2Int _origin; // in texels
        private float _recoverDebt;
        private float _findTimer;
        private readonly List<(Transform t, float press)> _stampers = new List<(Transform, float)>();
        private readonly Dictionary<Transform, Vector3> _last = new Dictionary<Transform, Vector3>();
        private readonly List<Vector3> _route = new List<Vector3>();
        private float _walked;
        private Vector3 _lastStep;
        private bool _hasStep;

        /// <summary>Seconds for matted grass to stand back up. Tests shorten it.</summary>
        public float RecoverSeconds { get => recoverSeconds; set => recoverSeconds = Mathf.Max(0.1f, value); }

        /// <summary>The last 100 m or so the player walked, oldest first (for the cheat panel).</summary>
        public IReadOnlyList<Vector3> RecentRoute => _route;

        public void Configure(Transform playerTransform, TravelClock travelClock, WearView wearView)
        {
            player = playerTransform;
            clock = travelClock;
            wear = wearView;
        }

        /// <summary>How hard the grass is pressed at a world position, 0..1.</summary>
        public float PressAt(Vector3 p)
        {
            if (_pixels == null) return 0f;
            int x = Mathf.FloorToInt(p.x / texel) - _origin.x, z = Mathf.FloorToInt(p.z / texel) - _origin.y;
            if (x < 0 || z < 0 || x >= _res || z >= _res) return 0f;
            return _pixels[z * _res + x].r / 255f;
        }

        private void Awake()
        {
            _res = Mathf.Clamp(Mathf.RoundToInt(size / texel), 16, 1024);
            _pixels = new Color32[_res * _res];
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = new Color32(0, 128, 128, 0);
            _texture = new Texture2D(_res, _res, TextureFormat.RGBA32, false, true)
            {
                name = "Trample Map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            _origin = OriginFor(player != null ? player.position : Vector3.zero);
            Shader.SetGlobalTexture(MapId, _texture);
        }

        private void OnDisable() => Shader.SetGlobalVector(RectId, Vector4.zero);

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        private Vector2Int OriginFor(Vector3 centre) =>
            new Vector2Int(Mathf.FloorToInt(centre.x / texel) - _res / 2, Mathf.FloorToInt(centre.z / texel) - _res / 2);

        private void Update()
        {
            if (player == null) return;
            Scroll(OriginFor(player.position));
            Recover(Time.deltaTime);

            _findTimer -= Time.deltaTime;
            if (_findTimer <= 0f) FindStampers();
            foreach (var (t, press) in _stampers)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                Vector3 now = t.position;
                Vector3 before = _last.TryGetValue(t, out Vector3 b) ? b : now;
                _last[t] = now;
                Vector3 move = now - before;
                move.y = 0f;
                if (Time.deltaTime > 0f && move.magnitude / Time.deltaTime > maxWalkSpeed) continue; // teleported
                StampAlong(before, now, move, press);
            }

            _texture.SetPixelData(_pixels, 0);
            _texture.Apply(false);
            Shader.SetGlobalTexture(MapId, _texture);
            Shader.SetGlobalVector(RectId, new Vector4(_origin.x * texel, _origin.y * texel, _res * texel, 0f));

            CountSteps();
        }

        private void FindStampers()
        {
            _findTimer = 0.5f;
            _stampers.Clear();
            _stampers.Add((player, playerPress));
            foreach (DeerAI d in FindObjectsByType<DeerAI>()) _stampers.Add((d.transform, deerPress));
            foreach (EnemyAI e in FindObjectsByType<EnemyAI>()) _stampers.Add((e.transform, enemyPress));
            var gone = new List<Transform>();
            foreach (Transform t in _last.Keys) if (t == null) gone.Add(t);
            foreach (Transform t in gone) _last.Remove(t);
        }

        private void Scroll(Vector2Int to)
        {
            Vector2Int shift = to - _origin;
            if (shift == Vector2Int.zero) return;
            var blank = new Color32(0, 128, 128, 0);
            if (Mathf.Abs(shift.x) >= _res || Mathf.Abs(shift.y) >= _res)
            {
                for (int i = 0; i < _pixels.Length; i++) _pixels[i] = blank;
            }
            else
            {
                if (_scratch == null || _scratch.Length != _pixels.Length) _scratch = new Color32[_pixels.Length];
                Color32[] moved = _scratch;
                for (int z = 0; z < _res; z++)
                {
                    int sz = z + shift.y;
                    for (int x = 0; x < _res; x++)
                    {
                        int sx = x + shift.x;
                        moved[z * _res + x] = sx >= 0 && sz >= 0 && sx < _res && sz < _res ? _pixels[sz * _res + sx] : blank;
                    }
                }
                _scratch = _pixels;
                _pixels = moved;
            }
            _origin = to;
        }

        private void Recover(float dt)
        {
            _recoverDebt += dt * 255f / Mathf.Max(0.1f, recoverSeconds);
            int step = Mathf.FloorToInt(_recoverDebt);
            if (step < 1) return;
            _recoverDebt -= step;
            for (int i = 0; i < _pixels.Length; i++)
            {
                byte r = _pixels[i].r;
                if (r == 0) continue;
                _pixels[i].r = (byte)Mathf.Max(0, r - step);
            }
        }

        private void StampAlong(Vector3 from, Vector3 to, Vector3 move, float press)
        {
            Vector2 dir = new Vector2(move.x, move.z);
            bool moving = dir.sqrMagnitude > 1e-6f;
            if (moving) dir.Normalize();
            float length = Vector3.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / (texel * 2f)));
            for (int s = 1; s <= steps; s++) Stamp(Vector3.Lerp(from, to, s / (float)steps), dir, moving, press);
        }

        private void Stamp(Vector3 at, Vector2 dir, bool moving, float press)
        {
            Vector2 along = moving ? dir : Vector2.up, across = new Vector2(along.y, -along.x);
            float reach = Mathf.Max(footWidth, footLength);
            int cx = Mathf.FloorToInt(at.x / texel) - _origin.x, cz = Mathf.FloorToInt(at.z / texel) - _origin.y;
            int r = Mathf.CeilToInt(reach / texel);
            byte g = (byte)Mathf.RoundToInt((along.x * 0.5f + 0.5f) * 255f), b = (byte)Mathf.RoundToInt((along.y * 0.5f + 0.5f) * 255f);
            for (int z = cz - r; z <= cz + r; z++)
            {
                if (z < 0 || z >= _res) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= _res) continue;
                    Vector2 d = new Vector2((x + _origin.x + 0.5f) * texel - at.x, (z + _origin.y + 0.5f) * texel - at.z);
                    float a = Vector2.Dot(d, along) / footLength, c = Vector2.Dot(d, across) / footWidth;
                    float f = 1f - (a * a + c * c);
                    if (f <= 0f) continue;
                    byte value = (byte)Mathf.RoundToInt(press * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(f * 2f)) * 255f);
                    int i = z * _res + x;
                    if (value <= _pixels[i].r) continue;
                    _pixels[i].r = value;
                    if (moving) { _pixels[i].g = g; _pixels[i].b = b; }
                }
            }
        }

        private void CountSteps()
        {
            Vector3 p = player.position;
            if (!_hasStep) { _lastStep = p; _hasStep = true; return; }
            Vector3 d = p - _lastStep;
            d.y = 0f;
            float m = d.magnitude;
            if (Time.deltaTime > 0f && m / Time.deltaTime > maxWalkSpeed) { _lastStep = p; return; }
            _walked += m;
            _lastStep = p;
            if (_walked < treadEvery) return;
            _walked = 0f;
            _route.Add(p);
            if (_route.Count > 100) _route.RemoveAt(0);
            if (wear == null) return;
            int minute = clock != null ? Mathf.FloorToInt(clock.Hour * 60f) % (24 * 60) : 12 * 60;
            wear.Walked(p, minute);
        }
    }
}

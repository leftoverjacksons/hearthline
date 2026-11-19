using System;
using System.Collections.Generic;
using Hearthline.Sim;
using UnityEngine;
using UnityEngine.AI;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Desire paths between the sim and the scene (docs/specs/trampled-grass.md, ADR 0034).
    /// In: footsteps become <see cref="Tread"/> commands, and the household's routes at home are found on the
    /// NavMesh and registered with <see cref="SetHomeRoutes"/>. Out: the sim's wear is drawn into a texture,
    /// one texel per metre, published as <c>_HL_WearMap</c> for the grass and the ground.
    /// </summary>
    public sealed class WearView : MonoBehaviour
    {
        [Tooltip("World x, z of the map's corner (whole metres) and its size in metres.")]
        [SerializeField] private Vector2Int origin = new Vector2Int(-360, -360);
        [SerializeField] private int size = 720;
        [Tooltip("The colour of trodden dirt, for the ground and the roots of grass on a path.")]
        [SerializeField] private Color dirtColor = new Color(0.36f, 0.29f, 0.21f);
        [Tooltip("Redraw at most this often (seconds). Wear changes slowly.")]
        [SerializeField] private float minRefreshSeconds = 1f;

        [Header("Household routes")]
        [Tooltip("Where everyone sets out from each day: the dwelling door.")]
        [SerializeField] private Transform door;
        [Tooltip("Where each job is done. Parallel to jobSpots.")]
        [SerializeField] private string[] jobIds = new string[0];
        [SerializeField] private Transform[] jobSpots = new Transform[0];
        [Tooltip("Where people without work go about.")]
        [SerializeField] private Transform idleSpot;

        private static readonly int MapId = Shader.PropertyToID("_HL_WearMap"), RectId = Shader.PropertyToID("_HL_WearRect");
        private static readonly int LevelsId = Shader.PropertyToID("_HL_WearLevels"), DirtId = Shader.PropertyToID("_HL_DirtColor");

        private SimHost _host;
        private Texture2D _texture;
        private byte[] _pixels;
        private bool _dirty = true;
        private float _lastDraw = -999f;

        public Texture2D Texture => _texture;
        public int Redraws { get; private set; }

        public void Configure(Vector2Int mapOrigin, int mapSize, Transform from, string[] jobs, Transform[] spots, Transform idle)
        {
            origin = mapOrigin;
            size = mapSize;
            door = from;
            jobIds = jobs;
            jobSpots = spots;
            idleSpot = idle;
        }

        private void Start()
        {
            _host = SimHost.Instance;
            EnsureTexture();
            if (_host == null) return;
            _host.SimReplaced += OnSimReplaced;
            _host.EventRaised += OnDomainEvent;
            OnSimReplaced();
        }

        private void OnDestroy()
        {
            Shader.SetGlobalVector(RectId, Vector4.zero);
            if (_texture != null) Destroy(_texture);
            if (_host == null) return;
            _host.SimReplaced -= OnSimReplaced;
            _host.EventRaised -= OnDomainEvent;
        }

        private void EnsureTexture()
        {
            if (_texture == null)
            {
                _texture = new Texture2D(size, size, TextureFormat.R8, true, true)
                {
                    name = "Wear Map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 0,
                    hideFlags = HideFlags.DontSave,
                };
                _pixels = new byte[size * size];
            }
            Shader.SetGlobalTexture(MapId, _texture);
            Shader.SetGlobalVector(RectId, new Vector4(origin.x, origin.y, size, 0f));
            Shader.SetGlobalColor(DirtId, dirtColor.linear);
        }

        /// <summary>
        /// Edit mode (style frames): registers the routes on a simulation built by the caller, so it can be run
        /// forward, and <see cref="Show"/> draws it.
        /// </summary>
        public void RegisterRoutes(Simulation sim)
        {
            if (sim == null || door == null) return;
            var routes = new Dictionary<string, List<double>>();
            for (int i = 0; i < jobIds.Length && i < jobSpots.Length; i++)
                if (jobSpots[i] != null && sim.Content.Jobs.ContainsKey(jobIds[i])) routes[jobIds[i]] = Route(door.position, jobSpots[i].position);
            if (idleSpot != null) routes[WearRules.IdleRoute] = Route(door.position, idleSpot.position);

            bool same = routes.Count == sim.State.Wear.Routes.Count;
            if (same)
                foreach (KeyValuePair<string, List<double>> r in routes)
                    if (!sim.State.Wear.Routes.TryGetValue(r.Key, out List<double> had) || !Same(had, r.Value)) { same = false; break; }
            if (!same) sim.Execute(new SetHomeRoutes { Routes = routes });
        }

        /// <summary>Draws a simulation's wear now (edit mode, style frames).</summary>
        public void Show(Simulation sim)
        {
            EnsureTexture();
            Draw(sim);
        }

        private void OnSimReplaced()
        {
            RegisterRoutes();
            _dirty = true;
        }

        private void OnDomainEvent(DomainEvent e)
        {
            if (e is WearChanged) _dirty = true;
        }

        private void Update()
        {
            if (_dirty && Time.unscaledTime - _lastDraw >= minRefreshSeconds) Redraw();
        }

        /// <summary>Someone walked over these points at this minute of the in-game day.</summary>
        public void Walked(IList<Vector3> points, int minuteOfDay, int passes = 1)
        {
            Simulation sim = _host != null ? _host.Sim : null;
            if (sim == null || points.Count == 0) return;
            var xz = new List<double>(points.Count * 2);
            foreach (Vector3 p in points) { xz.Add(p.x); xz.Add(p.z); }
            sim.Execute(new Tread { Points = xz, MinuteOfDay = Mathf.Clamp(minuteOfDay, 0, WearRules.MinutesPerDay - 1), Passes = passes });
        }

        public void Walked(Vector3 point, int minuteOfDay) => Walked(new[] { point }, minuteOfDay);

        /// <summary>Draws the sim's wear into the texture now.</summary>
        public void Redraw()
        {
            if (_texture == null) return;
            Draw(_host != null ? _host.Sim : null);
        }

        private void Draw(Simulation sim)
        {
            Array.Clear(_pixels, 0, _pixels.Length);
            if (sim != null)
            {
                foreach (KeyValuePair<long, WearCell> cell in sim.State.Wear.Cells)
                {
                    int x = WearState.KeyX(cell.Key) - origin.x, z = WearState.KeyZ(cell.Key) - origin.y;
                    if (x < 0 || z < 0 || x >= size || z >= size) continue;
                    _pixels[z * size + x] = (byte)Mathf.Clamp(Mathf.RoundToInt((float)cell.Value.W * 255f), 0, 255);
                }
                Shader.SetGlobalVector(LevelsId, new Vector4((float)sim.Wear.ShortFrom, (float)sim.Wear.DirtFrom, (float)sim.Wear.BareFrom, 0f));
            }
            _texture.SetPixelData(_pixels, 0);
            _texture.Apply(true);
            _dirty = false;
            _lastDraw = Time.unscaledTime;
            Redraws++;
        }

        /// <summary>Finds each job's route from the door on the NavMesh and tells the sim, if they changed.</summary>
        public void RegisterRoutes() => RegisterRoutes(_host != null ? _host.Sim : null);

        private static List<double> Route(Vector3 from, Vector3 to)
        {
            var points = new List<double>();
            var path = new NavMeshPath();
            bool found = NavMesh.SamplePosition(from, out NavMeshHit a, 3f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(to, out NavMeshHit b, 3f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2;
            Vector3[] corners = found ? path.corners : new[] { from, to };
            // Rounded to centimetres so the same scene always registers the same routes.
            foreach (Vector3 c in corners) { points.Add(Math.Round(c.x, 2)); points.Add(Math.Round(c.z, 2)); }
            return points;
        }

        private static bool Same(List<double> a, List<double> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (Math.Abs(a[i] - b[i]) > 0.011) return false;
            return true;
        }
    }
}

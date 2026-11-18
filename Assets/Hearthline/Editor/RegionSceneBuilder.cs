using System;
using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim.Content;
using Hearthline.Sim.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/M2_Region.unity from the generated world (ADR 0024): the land at the foot of a
    /// mountain, its streams and river, the old highway with its bridges, the track home, and ruin sites, all
    /// from <see cref="WorldGen"/> with <see cref="Seed"/>. The scene dresses that map: ground painted by slope,
    /// wetness and forest; water ribbons along every stream; trees, cover and rocks by terrain; the homestead
    /// on its bench; and <see cref="AreaVolume"/>s read off the map so the simulation knows where the party is.
    /// The middle ~600 m is playable; the rest, the mountain included, is the view.
    /// Menu: Hearthline → Rebuild M2 region scene. From the CLI:
    /// <c>unity command script eval --code "return Hearthline.EditorTools.RegionSceneBuilder.Build();" --timeout 600</c>
    /// </summary>
    public static class RegionSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/M2_Region.unity";
        private const string Generated = SceneBuildKit.GeneratedFolder;
        private const string GroundPath = Generated + "/M2_Region_Ground.asset";
        private const string GroundMapPath = Generated + "/M2_Region_GroundMap.png";
        private const string WaterPath = Generated + "/M2_Region_Water.asset";

        /// <summary>The world's seed. Change it for a different land.</summary>
        public const ulong Seed = 1;
        /// <summary>The generated square (m); the playable region is the middle.</summary>
        public const float WorldSize = 1200f;
        public const float Size = 600f;
        public const float RegionRadius = 300f;
        /// <summary>The fenced yard (the generator's bench is a little wider).</summary>
        public const float HomeRadius = 32f;

        private static WorldMap _map;
        private static Fields _fields;
        public static WorldMap Map => _map ??= WorldGen.Generate(new WorldSettings { Seed = Seed });
        private static Fields F => _fields ??= new Fields(Map);
        private static Vector3 _overpass;
        private static ContentDatabase _content;
        private static List<PlacedPlace> _places;
        /// <summary>The region's places (docs/specs/ruins-and-places.md): one per ruin site, plus the Elder Works.</summary>
        public static List<PlacedPlace> Places => _places ??= PlaceLayout.Assign(Map, Content.Places.Values);
        private static ContentDatabase Content => _content ??= ContentLoader.LoadDirectory(SimHost.ContentRoot).Database;

        [MenuItem("Hearthline/Rebuild M2 region scene")]
        private static void Menu() => Debug.Log(Build());

        public static string Build()
        {
            _map = null;
            _fields = null;
            _places = null;
            _content = null;
            GreyboxKit.Setup();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneBuildKit.EnsureFolder(Generated);

            Light sun = SceneBuildKit.CreateSun();
            Light moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 1f);
            moon.shadows = LightShadows.Soft;

            SceneBuildKit.BuildGround("Region Ground", GroundPath, WorldSize, Mathf.RoundToInt(WorldSize / (float)Map.Cell), HeightAt, GroundMaterial(), mapUv: true);
            BuildWater();

            var trees = new GameObject("Trees").AddComponent<InstancedScatter>();
            SetScatter(trees, drawDistance: 650f, shadows: true);
            var cover = new GameObject("Ground Cover").AddComponent<InstancedScatter>();
            SetScatter(cover, drawDistance: 130f, shadows: false);
            var grass = new GameObject("Grass").AddComponent<InstancedScatter>();
            SetScatter(grass, drawDistance: 85f, shadows: false);
            var props = new GameObject("Props").transform;

            _overpass = PickOverpass();
            BuildHomestead(props);
            BuildCrossings(props);
            BuildRuins(props);
            ScatterNature(trees, cover, props);
            ScatterGrass(grass);
            BuildGathering(props);
            BuildStores(props);
            Transform home = BuildAreas(out int areaCount);

            SceneBuildKit.PlayerRig rig = SceneBuildKit.BuildPlayerRig(new Vector3(0f, HeightAt(0f, 4f) + 0.1f, 4f), yaw: 180f);
            AddHauling(rig);
            var location = rig.Player.AddComponent<PlayerLocation>();
            SceneBuildKit.SetRef(location, "home", home);
            SceneBuildKit.SetFloat(location, "regionRadius", RegionRadius);

            var clock = new GameObject("Clock").AddComponent<TravelClock>();
            SceneBuildKit.SetRef(clock, "sun", sun);
            SceneBuildKit.SetRef(clock, "playerInput", rig.InputRelease);

            var trip = new GameObject("Trip").AddComponent<TripController>();
            SceneBuildKit.SetRef(trip, "location", location);
            SceneBuildKit.SetRef(trip, "clock", clock);

            var roadEvents = new GameObject("Road Events").AddComponent<RoadEvents>();
            SceneBuildKit.SetRef(roadEvents, "location", location);
            SceneBuildKit.SetRef(roadEvents, "clock", clock);
            BuildHomeUse(clock, roadEvents);
            BuildPracticeTarget(new Vector3(4f, 0f, -10f));

            BuildAtmosphere(sun, moon, clock, location);
            // Weather and the life around you (docs/specs/life-and-weather.md).
            AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Hearthline/Art/Audio/" + name);
            var weatherView = new GameObject("Weather").AddComponent<WeatherView>();
            weatherView.Configure(clock, Object.FindAnyObjectByType<Atmosphere>(), new Renderer[] { GameObject.Find("Region Ground").GetComponent<Renderer>() });
            weatherView.SetSounds(Clip("Rain_3.ogg"), Clip("Rain_2.ogg"),
                new[] { Clip("Wind_1.ogg"), Clip("Wind_2.ogg"), Clip("Wind_3.ogg") }, new[] { Clip("Thunder_Long.wav"), Clip("Thunder_Short.wav") });
            var life = new GameObject("Ambient Life").AddComponent<AmbientLife>();
            life.Configure(rig.Player.transform, clock, location);
            life.SetSounds(Clip("Birds_And_Wind.ogg"), Clip("Crickets.mp3"));
            new GameObject("Place Discovery").AddComponent<PlaceDiscovery>().Configure(location);
            BuildHomeLights(clock);
            new GameObject("Thin Aura").AddComponent<ThinAura>().Configure(location, weatherView, Clip("Hum_Elder.wav"));
            int covers = SceneBuildKit.AddCoverSurfaces(HeightAt);
            CombatSceneKit.BuildEnemyPrefab();
            CombatSceneKit.AddSpawner(rig.Player.transform);
            CombatSceneKit.AddHuntDirector(rig.Player.transform);
            new GameObject("Fight Director").AddComponent<FightDirector>().Configure(rig.Player.transform);
            CombatSceneKit.BakeNavMesh("Assets/Scenes/Generated/M2_Region_NavMesh.asset", new Bounds(new Vector3(0f, (float)Map.HomeHeight, 0f), new Vector3(Size + 80f, 400f, Size + 80f)));

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildKit.AddToBuild(ScenePath, first: false);
            AssetDatabase.SaveAssets();
            return "Saved " + ScenePath + " (seed " + Seed + "): " + Map.Streams.Count + " streams, " + Map.Bridges.Count + " bridges, " + covers + " cover pieces, "
                + trees.InstanceCount + " trees, " + cover.InstanceCount + " ground-cover instances, " + props.childCount + " prop groups, " + areaCount + " areas.";
        }

        // ---- Terrain --------------------------------------------------------------------------------

        /// <summary>Ground height. Public so tests and tools can place things on the ground.</summary>
        public static float HeightAt(float x, float z) => (float)Map.HeightAt(x, z);

        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
        private static bool InRegion(float x, float z, float margin = 0f) => Mathf.Abs(x) < Size / 2f + margin && Mathf.Abs(z) < Size / 2f + margin;

        /// <summary>Distances (m) to the road's and track's centre lines and to the water's edge, on a 1 m grid.</summary>
        private sealed class Fields
        {
            public const float Reach = 40f;
            private readonly int _n;
            private readonly float _origin;
            private readonly float[] _road, _track, _water;

            public Fields(WorldMap map)
            {
                _n = Mathf.RoundToInt(WorldSize) + 1;
                _origin = -WorldSize / 2f;
                _road = Blank(); _track = Blank(); _water = Blank();
                Stamp(_road, map.Road, edge: false);
                Stamp(_track, map.Track, edge: false);
                foreach (Waterway w in map.Streams) Stamp(_water, w.Points, edge: true);
            }

            private float[] Blank() { var f = new float[_n * _n]; for (int i = 0; i < f.Length; i++) f[i] = Reach; return f; }

            private void Stamp(float[] field, List<PathPoint> pts, bool edge)
            {
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    Vector2 a = new Vector2((float)pts[k].P.X, (float)pts[k].P.Z), b = new Vector2((float)pts[k + 1].P.X, (float)pts[k + 1].P.Z);
                    float half = edge ? (float)pts[k].Width / 2f : 0f;
                    int i0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - Reach - _origin)), i1 = Mathf.Min(_n - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + Reach - _origin));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - Reach - _origin)), j1 = Mathf.Min(_n - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + Reach - _origin));
                    Vector2 ab = b - a;
                    float len2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        var p = new Vector2(i + _origin, j + _origin);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                        float d = Mathf.Max(0f, Vector2.Distance(p, a + ab * t) - half);
                        int idx = j * _n + i;
                        if (d < field[idx]) field[idx] = d;
                    }
                }
            }

            private float At(float[] f, float x, float z)
            {
                int i = Mathf.RoundToInt(x - _origin), j = Mathf.RoundToInt(z - _origin);
                if (i < 0 || j < 0 || i >= _n || j >= _n) return Reach;
                return f[j * _n + i];
            }

            public float Road(float x, float z) => At(_road, x, z);
            public float Track(float x, float z) => At(_track, x, z);
            public float Water(float x, float z) => At(_water, x, z);
        }

        // The painted colour map: one texture over the whole ground, coloured by what each spot is.
        private static Material GroundMaterial()
        {
            const int res = 2048;
            var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
            var pixels = new Color[res * res];
            for (int j = 0; j < res; j++)
            for (int i = 0; i < res; i++)
            {
                float x = (i / (res - 1f) - 0.5f) * WorldSize, z = (j / (res - 1f) - 0.5f) * WorldSize;
                pixels[j * res + i] = GroundColour(x, z);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(GroundMapPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(GroundMapPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GroundMapPath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();

            Material m = SceneBuildKit.ColorMaterial("Region_Ground", Color.white);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GroundMapPath));
            m.SetFloat("_Smoothness", 0.05f);
            // Close-up detail over the painted map: grass blades and soil grain repeating every 2 m.
            m.SetTexture("_DetailAlbedoMap", TextureForge.Albedo("soil"));
            m.SetTexture("_DetailNormalMap", TextureForge.Normal("soil"));
            m.SetTextureScale("_DetailAlbedoMap", Vector2.one * (WorldSize / 2f));
            m.SetFloat("_DetailAlbedoMapScale", 0.55f);
            m.SetFloat("_DetailNormalMapScale", 0.5f);
            m.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Color GroundColour(float x, float z)
        {
            float n1 = Mathf.PerlinNoise(x * 0.03f + 5f, z * 0.03f + 9f);
            float n2 = Mathf.PerlinNoise(x * 0.11f + 1f, z * 0.11f + 3f);
            float fine = Mathf.PerlinNoise(x * 0.5f, z * 0.5f);
            float moist = (float)Map.MoistureAt(x, z), forest = (float)Map.ForestAt(x, z), slope = (float)Map.SlopeAt(x, z);
            float high = HeightAt(x, z) - (float)Map.HomeHeight;

            // Open ground (ADR 0030): Valheim-lush green where water gathers, gold where it's dry and on the balds.
            Color dry = Color.Lerp(new Color(0.47f, 0.45f, 0.21f), new Color(0.43f, 0.44f, 0.2f), n1);
            Color lush = Color.Lerp(new Color(0.3f, 0.44f, 0.12f), new Color(0.38f, 0.48f, 0.15f), n1);
            Color c = Color.Lerp(dry, lush, Smooth(0.2f, 0.6f, moist + (n2 - 0.5f) * 0.25f)) * (0.92f + fine * 0.16f);
            float bald = Smooth(130f, 175f, high) * (1f - Smooth(0.3f, 0.6f, forest));
            c = Color.Lerp(c, dry * 1.05f, bald);

            // The forest floor: warm leaf litter with patches of moss, rust needle litter on dry ridges where pines
            // grow, deep green moss in the damp coves.
            Color litter = Color.Lerp(new Color(0.4f, 0.29f, 0.15f), new Color(0.34f, 0.25f, 0.13f), n2);
            Color moss = Color.Lerp(new Color(0.22f, 0.32f, 0.11f), new Color(0.27f, 0.36f, 0.13f), n1);
            Color needles = new Color(0.44f, 0.26f, 0.13f);
            Color floor = Color.Lerp(litter, moss, Smooth(0.45f, 0.7f, n1 * 0.6f + moist * 0.6f));
            floor = Color.Lerp(floor, needles, Smooth(60f, 110f, high) * (1f - moist) * 0.7f);
            if (Map.IsDeepCove(x, z)) floor = Color.Lerp(floor, new Color(0.14f, 0.22f, 0.1f), 0.6f);
            c = Color.Lerp(c, floor * (0.9f + fine * 0.2f), Smooth(0.35f, 0.65f, forest));

            // Rock only where it's genuinely steep: Appalachian ridges are wooded to the top.
            float rock = Smooth(0.75f, 1.15f, slope);
            c = Color.Lerp(c, new Color(0.45f, 0.43f, 0.39f) * (0.8f + fine * 0.35f), rock);

            // The Elder Works' pad: the grass gone grey.
            float elder = Smooth(26f, 14f, Vector2.Distance(new Vector2(x, z), new Vector2((float)Map.ElderWorks.X, (float)Map.ElderWorks.Z)));
            c = Color.Lerp(c, new Color(c.grayscale, c.grayscale, c.grayscale * 1.05f) * 0.95f, elder * 0.75f);

            // Stream banks: mud, then gravel at and under the water.
            float water = F.Water(x, z);
            c = Color.Lerp(c, new Color(0.33f, 0.27f, 0.19f) * (0.9f + fine * 0.2f), Smooth(4.5f, 1.5f, water) * 0.85f);
            c = Color.Lerp(c, new Color(0.47f, 0.44f, 0.38f) * (0.75f + fine * 0.45f), Smooth(1.3f, 0.2f, water));

            // The old highway: gravel shoulders, cracked asphalt with grass in the cracks.
            float road = F.Road(x, z);
            c = Color.Lerp(c, new Color(0.45f, 0.42f, 0.35f) * (0.9f + fine * 0.2f), Smooth(5.2f, 4.2f, road));
            if (road < 3.6f)
            {
                float crack = Mathf.Abs(Mathf.PerlinNoise(x * 0.25f, z * 0.25f) - 0.5f);
                Color asphalt = new Color(0.2f, 0.2f, 0.2f) * (0.9f + fine * 0.2f);
                c = crack < 0.025f ? new Color(0.3f, 0.38f, 0.15f) : asphalt;
            }

            // The track home and the yard: trampled earth.
            Color dirt = new Color(0.42f, 0.33f, 0.22f) * (0.9f + fine * 0.2f);
            c = Color.Lerp(c, dirt, Smooth(1.9f, 0.9f, F.Track(x, z)) * 0.9f);
            float d = Mathf.Sqrt(x * x + z * z);
            c = Color.Lerp(c, dirt, Smooth(9f, 4f, d) * 0.8f);
            return c;
        }

        // Every stream and the river as a ribbon at its water surface, its ripples running downstream.
        private static void BuildWater()
        {
            var vertices = new List<Vector3>();
            var flow = new List<Vector2>();
            var triangles = new List<int>();
            foreach (Waterway w in Map.Streams)
            {
                List<PathPoint> pts = w.Points;
                if (pts.Count < 2) continue;
                int first = vertices.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    PathPoint a = pts[Mathf.Max(0, i - 1)], b = pts[Mathf.Min(pts.Count - 1, i + 1)];
                    var dir = new Vector2((float)(b.P.X - a.P.X), (float)(b.P.Z - a.P.Z)).normalized;
                    var side = new Vector2(-dir.y, dir.x);
                    float half = (float)pts[i].Width / 2f + 0.7f;
                    var c = new Vector2((float)pts[i].P.X, (float)pts[i].P.Z);
                    float y = (float)pts[i].Surface;
                    Vector2 l = c + side * half, r = c - side * half;
                    vertices.Add(new Vector3(l.x, y, l.y));
                    vertices.Add(new Vector3(r.x, y, r.y));
                    // Narrow streams run quicker than the broad river.
                    Vector2 f = dir * Mathf.Lerp(1.4f, 0.7f, Mathf.InverseLerp(2f, 12f, (float)pts[i].Width));
                    flow.Add(f); flow.Add(f);
                    if (i == 0) continue;
                    int v = first + i * 2;
                    // Both windings, so the ribbon shows from above whichever way the stream turns.
                    triangles.Add(v - 2); triangles.Add(v); triangles.Add(v - 1);
                    triangles.Add(v - 1); triangles.Add(v); triangles.Add(v + 1);
                    triangles.Add(v - 2); triangles.Add(v - 1); triangles.Add(v);
                    triangles.Add(v - 1); triangles.Add(v + 1); triangles.Add(v);
                }
            }
            var mesh = new Mesh { name = "Region Water", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(1, flow);
            mesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            AssetDatabase.DeleteAsset(WaterPath);
            AssetDatabase.CreateAsset(mesh, WaterPath);
            var water = new GameObject("Water");
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WaterMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            water.isStatic = true;
        }

        private static Material WaterMaterial()
        {
            // The water's own shader: depth colour, refraction, ripples, glints, foam (ADR 0018).
            string path = Generated + "/Region_Water.mat";
            AssetDatabase.DeleteAsset(path);
            var m = new Material(Shader.Find("Hearthline/Water")) { name = "Region_Water" };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---- Homestead and crossings ----------------------------------------------------------------

        private static Vector3 OnGround(float x, float z) => new Vector3(x, HeightAt(x, z), z);

        private static void BuildHomestead(Transform props)
        {
            var root = new GameObject("Homestead").transform;
            root.SetParent(props, false);
            // The three building slots show whatever tier the simulation says (ADR 0017). Their panels are
            // wired in BuildHomeUse once the clock and road events exist.
            Structure(root, "dwelling", new Vector3(0f, 0f, 16f), 180f, "Build_Site", "Dwelling_Tent", "Cabin", "Dwelling_Farmhouse");
            Structure(root, "hearth", new Vector3(-16f, 0f, 2f), 90f, "Build_Site", "Hearth_FirePit", "Hearth_Kitchen", "Hearth_Smokehouse");
            Structure(root, "infirmary", new Vector3(18f, 0f, 10f), -90f, "Build_Site", "Infirmary_Cot", "Infirmary_Sickroom", "Infirmary_Surgery");

            GreyboxKit.Place("Rain_Barrel", OnGround(6.6f, 18.5f), 0f, 1f, root, collider: true);
            GreyboxKit.Place("Clothesline", OnGround(14f, 22f), 15f, 1f, root, collider: false);
            GreyboxKit.Place("Woodpile", OnGround(-10f, 15f), 90f, 1f, root, collider: true);
            GreyboxKit.Place("Garden_Bed", OnGround(9f, 1f), 0f, 1f, root, collider: false);
            GreyboxKit.Place("Garden_Bed", OnGround(9f, -3.5f), 0f, 1f, root, collider: false);
            GreyboxKit.Place("Job_Board", OnGround(-5f, 8f), 200f, 1f, root, collider: true).name = "Job Board";

            // Split-rail fence round the clearing, open to the south where the path runs down to the creek.
            Bounds b = GreyboxKit.MeshOf("Fence_Rail").bounds;
            float runLength = Mathf.Max(b.size.x, b.size.z);
            const float radius = 29f;
            int segments = Mathf.CeilToInt(2f * Mathf.PI * radius / runLength);
            for (int s = 0; s < segments; s++)
            {
                float a0 = s * 2f * Mathf.PI / segments, a1 = (s + 1) * 2f * Mathf.PI / segments;
                Vector3 p0 = OnGround(Mathf.Sin(a0) * radius, Mathf.Cos(a0) * radius), p1 = OnGround(Mathf.Sin(a1) * radius, Mathf.Cos(a1) * radius);
                Vector3 mid = (p0 + p1) / 2f;
                if (mid.z < -18f && Mathf.Abs(mid.x) < 9f) continue; // the gate
                PlaceRun("Fence_Rail", b, p0, p1, root);
            }
        }

        // One slot: a StructureView holding a model per tier (index 0 = not built), all but one hidden.
        private static void Structure(Transform root, string id, Vector3 at, float yaw, params string[] tierModels)
        {
            var slot = new GameObject("Structure " + id);
            slot.transform.SetParent(root, false);
            slot.transform.SetPositionAndRotation(OnGround(at.x, at.z), Quaternion.Euler(0f, yaw, 0f));
            var tiers = new GameObject[tierModels.Length];
            for (int i = 0; i < tierModels.Length; i++)
            {
                tiers[i] = GreyboxKit.Place(tierModels[i], slot.transform.position, yaw, 1f, slot.transform, collider: true);
                tiers[i].SetActive(i == 1);
            }
            slot.AddComponent<StructureView>().Configure(id, tiers);
        }

        // What you press E on at each building, the job board, and the household figures.
        // Combat step 2: a sacking-dressed figure with real hit zones to practise on. Hits on it never reach the sim.
        private static void BuildPracticeTarget(Vector3 at)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/StarterAssets/Runtime/ThirdPersonController/Character/Models/Armature.fbx");
            var dummy = (GameObject)PrefabUtility.InstantiatePrefab(model);
            dummy.name = "Practice Target";
            dummy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // zones are placed facing +Z at the origin
            Animator animator = dummy.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorBuilder.StarterController);
            SceneBuildKit.AddHitZones(dummy, SceneBuildKit.EnsureHitboxLayer());
            dummy.AddComponent<PracticeTarget>();
            Material sacking = GreyboxKit.Mat("HL_CanvasTan");
            foreach (Renderer r in dummy.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = sacking;
                r.sharedMaterials = mats;
            }
            dummy.transform.SetPositionAndRotation(new Vector3(at.x, HeightAt(at.x, at.z), at.z), Quaternion.Euler(0f, 0f, 0f));
            // A post behind it, so it reads as a target and not a person.
            GreyboxKit.Place("Fence_Rail", dummy.transform.position + new Vector3(0f, 0f, -0.35f), 90f, 0.6f, dummy.transform, collider: false);
        }

        private static void BuildHomeUse(TravelClock clock, RoadEvents roadEvents)
        {
            Transform root = GameObject.Find("Homestead").transform;
            void Use(string id, HomeBuilding.Kind kind, Vector3 front)
            {
                var view = GameObject.Find("Structure " + id).GetComponent<StructureView>();
                var use = new GameObject("Use " + id);
                use.transform.SetParent(root, false);
                use.transform.position = OnGround(front.x, front.z);
                var building = use.AddComponent<HomeBuilding>();
                building.Configure(kind, view, clock, roadEvents);
                building.SetReach(4.5f);
            }
            Use("dwelling", HomeBuilding.Kind.Dwelling, new Vector3(0f, 0f, 10.5f));
            Use("hearth", HomeBuilding.Kind.Hearth, new Vector3(-12.5f, 0f, 2f));
            Use("infirmary", HomeBuilding.Kind.Infirmary, new Vector3(14.5f, 0f, 10f));
            GameObject.Find("Job Board").AddComponent<JobBoard>().SetReach(2.8f);

            // Where each job is done, and where people without one stand about; all facing the yard.
            Transform Spot(string name, float x, float z)
            {
                var t = new GameObject("Spot " + name).transform;
                t.SetParent(root, false);
                t.position = OnGround(x, z);
                Vector3 look = new Vector3(-x, 0f, -z);
                t.rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
                return t;
            }
            string[] jobs = { "woodcutter", "cook", "healer", "farmer", "hunter", "guard", "smith" };
            Transform[] spots =
            {
                Spot("woodcutter", -8f, 13f), Spot("cook", -13f, 5f), Spot("healer", 15.5f, 7.5f), Spot("farmer", 6.5f, -1f),
                Spot("hunter", -18f, -6f), Spot("guard", 3f, -24f), Spot("smith", -6f, -12f),
            };
            Transform[] idle = { Spot("idle 0", -3f, 9f), Spot("idle 1", 3f, 9f), Spot("idle 2", 2f, -6f) };
            var figures = new GameObject("Household Figures").AddComponent<HouseholdFigures>();
            figures.transform.SetParent(root, false);
            figures.Configure(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/StarterAssets/Runtime/ThirdPersonController/Character/Models/Armature.fbx"),
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ThirdParty/StarterAssets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller"),
                jobs, spots, idle);
        }

        // Places a model whose length runs along its local Z (or X) so it spans p0 to p1.
        private static void PlaceRun(string model, Bounds b, Vector3 p0, Vector3 p1, Transform parent)
        {
            bool alongZ = b.size.z >= b.size.x;
            Vector3 dir = p1 - p0;
            dir.y = 0f;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - (alongZ ? 0f : 90f);
            Vector3 centreOffset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(b.center.x, 0f, b.center.z);
            Vector3 mid = (p0 + p1) / 2f;
            GreyboxKit.Place(model, new Vector3(mid.x, Mathf.Min(p0.y, p1.y), mid.z) - centreOffset, yaw, 1f, parent, collider: true);
        }
        // ---- Crossings and ruins ----------------------------------------------------------------------

        private static Vector2 RoadDirection(int i)
        {
            List<PathPoint> road = Map.Road;
            PathPoint a = road[Mathf.Max(0, i - 2)], b = road[Mathf.Min(road.Count - 1, i + 2)];
            return new Vector2((float)(b.P.X - a.P.X), (float)(b.P.Z - a.P.Z)).normalized;
        }

        private static float Yaw(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        private static float DistanceToBridge(float x, float z)
        {
            float best = float.MaxValue;
            foreach (Hearthline.Sim.World.Bridge b in Map.Bridges)
                best = Mathf.Min(best, Vector2.Distance(new Vector2(x, z), new Vector2((float)(b.From.X + b.To.X) / 2f, (float)(b.From.Z + b.To.Z) / 2f)));
            return best;
        }

        // The fallen overpass: on the highway in the playable region, well away from home, bridges and ruins.
        private static Vector3 PickOverpass()
        {
            int best = -1; float score = float.MinValue;
            for (int i = 2; i < Map.Road.Count - 2; i++)
            {
                float x = (float)Map.Road[i].P.X, z = (float)Map.Road[i].P.Z;
                if (!InRegion(x, z, -50f) || Mathf.Sqrt(x * x + z * z) < 110f) continue;
                float ruin = float.MaxValue;
                foreach (Vec2 r in Map.RuinSites) ruin = Mathf.Min(ruin, Vector2.Distance(new Vector2(x, z), new Vector2((float)r.X, (float)r.Z)));
                float s = Mathf.Min(DistanceToBridge(x, z), 80f) + Mathf.Min(ruin, 80f) - (float)Map.SlopeAt(x, z) * 200f + Mathf.Abs(x) * 0.05f;
                if (s > score) { score = s; best = i; }
            }
            float bx = (float)Map.Road[best].P.X, bz = (float)Map.Road[best].P.Z;
            return new Vector3(bx, HeightAt(bx, bz), bz);
        }

        private static int NearestRoadIndex(Vector3 p)
        {
            int best = 0; float d = float.MaxValue;
            for (int i = 0; i < Map.Road.Count; i++)
            {
                float dd = Vector2.Distance(new Vector2(p.x, p.z), new Vector2((float)Map.Road[i].P.X, (float)Map.Road[i].P.Z));
                if (dd < d) { d = dd; best = i; }
            }
            return best;
        }

        private static void BuildCrossings(Transform props)
        {
            var root = new GameObject("Crossings").transform;
            root.SetParent(props, false);

            // A guardrail bridge wherever the highway meets water, stretched to span it, kudzu on the banks.
            Mesh bridgeMesh = GreyboxKit.MeshOf("Bridge_Guardrail");
            float modelLength = Mathf.Max(1f, bridgeMesh.bounds.size.z);
            int n = 0;
            foreach (Hearthline.Sim.World.Bridge b in Map.Bridges)
            {
                var from = new Vector2((float)b.From.X, (float)b.From.Z);
                var to = new Vector2((float)b.To.X, (float)b.To.Z);
                Vector2 mid = (from + to) / 2f, dir = (to - from).normalized;
                float length = Vector2.Distance(from, to) + 6f;
                float deck = (float)b.DeckHeight - 0.05f;
                GameObject bridge = GreyboxKit.Place("Bridge_Guardrail", new Vector3(mid.x, deck, mid.y), Yaw(dir), 1f, root, collider: true);
                bridge.name = "Bridge " + n++;
                bridge.transform.localScale = new Vector3(1f, 1f, Mathf.Max(1f, length / modelLength));
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 k1 = from - dir * 4f + side * 5f, k2 = to + dir * 4f - side * 5f;
                GreyboxKit.Place("Kudzu_Mound", new Vector3(k1.x, HeightAt(k1.x, k1.y), k1.y), 30f, 0.8f, root, collider: false);
                GreyboxKit.Place("Kudzu_Mound", new Vector3(k2.x, HeightAt(k2.x, k2.y), k2.y), 200f, 0.7f, root, collider: false);
            }

            // The fallen overpass, laid out with the road along its local X: pillars, a standing deck end, the
            // broken span down on the road.
            Vector3 o = _overpass;
            var overpass = new GameObject("Overpass").transform;
            overpass.SetParent(root, false);
            overpass.position = o;
            GreyboxKit.Place("Overpass_Pillar", o + new Vector3(0f, 0f, 14f), 0f, 1f, overpass, collider: true);
            GreyboxKit.Place("Overpass_Pillar", o + new Vector3(-7f, 0f, -16f), 8f, 1f, overpass, collider: true);
            GreyboxKit.Place("Overpass_Deck_Standing", o + new Vector3(0f, 9.3f, 26f), 0f, 1f, overpass, collider: true);
            GameObject fallen = GreyboxKit.Place("Overpass_Deck_Fallen", o + new Vector3(2f, 4.2f, 1f), 6f, 1f, overpass, collider: true);
            fallen.transform.rotation = Quaternion.Euler(-17f, 6f, 4f);
            GreyboxKit.Place("Rubble", o + new Vector3(-5f, 0.6f, -10f), 30f, 1f, overpass, collider: true);
            GreyboxKit.Place("Rubble", o + new Vector3(6f, 0.6f, -13f), 140f, 0.7f, overpass, collider: true);
            for (int i = 0; i < 4; i++)
            {
                GreyboxKit.Place("Kudzu_Drape", o + new Vector3(-5.6f, 8.8f, 18f + i * 5f), 90f, 1f, overpass, collider: false);
                GreyboxKit.Place("Kudzu_Drape", o + new Vector3(5.6f, 8.8f, 20f + i * 5f), 270f, 1f, overpass, collider: false);
            }
            GreyboxKit.Place("Kudzu_Mound", o + new Vector3(0f, 0.4f, 14f), 0f, 1.1f, overpass, collider: false);
            GreyboxKit.Place("Kudzu_Mound", o + new Vector3(-7f, 0.4f, -16f), 70f, 1f, overpass, collider: false);
            GreyboxKit.Place("Kudzu_Mound", o + new Vector3(4f, 3.5f, 6f), 20f, 0.8f, overpass, collider: false);
            overpass.rotation = Quaternion.Euler(0f, Yaw(RoadDirection(NearestRoadIndex(o))) - 90f, 0f);
            // Settle each piece onto the ground where it ended up.
            foreach (Transform piece in overpass)
            {
                Vector3 p = piece.position;
                float lift = p.y - o.y;
                piece.position = new Vector3(p.x, HeightAt(p.x, p.z) + lift, p.z);
            }

            // Jersey barriers left along the highway, some knocked askew.
            var rnd = new Random(404);
            float walked = 0f;
            for (int i = 1; i < Map.Road.Count; i++)
            {
                walked += Vector2.Distance(new Vector2((float)Map.Road[i].P.X, (float)Map.Road[i].P.Z), new Vector2((float)Map.Road[i - 1].P.X, (float)Map.Road[i - 1].P.Z));
                if (walked < 24f) continue;
                walked = 0f;
                float x = (float)Map.Road[i].P.X, z = (float)Map.Road[i].P.Z;
                if (rnd.NextDouble() < 0.45 || !InRegion(x, z, 60f) || DistanceToBridge(x, z) < 25f || Vector3.Distance(new Vector3(x, 0f, z), new Vector3(o.x, 0f, o.z)) < 30f || F.Track(x, z) < 12f) continue;
                Vector2 dir = RoadDirection(i), side = new Vector2(-dir.y, dir.x) * (rnd.NextDouble() < 0.5 ? -5.2f : 5.2f);
                GreyboxKit.Place("Jersey_Barrier", new Vector3(x + side.x, HeightAt(x + side.x, z + side.y), z + side.y), Yaw(dir) + (float)(rnd.NextDouble() - 0.5) * 30f, 1f, root, collider: true);
            }
        }

        // Each place built by its kind (RuinKit), with its search spots and find. The overpass's own spots go in
        // BuildGathering, where its structure is.
        private static void BuildRuins(Transform props)
        {
            var root = new GameObject("Places").transform;
            root.SetParent(props, false);
            foreach (PlacedPlace place in Places)
            {
                if (!InRegion((float)place.P.X, (float)place.P.Z, -10f)) continue;
                RuinKit.Build(place, Content.Places[place.Kind], root, HeightAt);
            }
        }

        // ---- Nature ---------------------------------------------------------------------------------

        private static bool NearRuin(float x, float z, float within)
        {
            foreach (PlacedPlace p in Places)
            {
                Vector2 size = RuinKit.Footprint(p.Kind);
                if (Vector2.Distance(new Vector2(x, z), new Vector2((float)p.P.X, (float)p.P.Z)) < within + Mathf.Max(size.x, size.y) * 0.5f - 12f) return true;
            }
            return false;
        }

        private static bool Clear(float x, float z, float margin)
        {
            if (Mathf.Sqrt(x * x + z * z) < HomeRadius + margin) return false;
            if (F.Road(x, z) < 7f + margin * 0.3f) return false;
            if (F.Track(x, z) < 2.5f + Mathf.Max(0f, margin) * 0.2f) return false;
            if (F.Water(x, z) < 1.2f + Mathf.Max(0f, margin) * 0.3f) return false;
            if (Vector3.Distance(new Vector3(x, 0f, z), new Vector3(_overpass.x, 0f, _overpass.z)) < 26f + margin) return false;
            if (DistanceToBridge(x, z) < 14f + margin) return false;
            if (NearRuin(x, z, 13f + margin * 0.5f)) return false;
            return Mathf.Abs(x) < WorldSize / 2f - 4f && Mathf.Abs(z) < WorldSize / 2f - 4f;
        }

        // Grass (ADR 0030): dense card clumps wherever the ground is open, lush where it's damp and gold where it's
        // dry or high, thinning into the forest's edge; a little under the trees. Not on the road, the track, the
        // water or the trampled middle of the yard.
        private static void ScatterGrass(InstancedScatter grass)
        {
            var rnd = new Random(2207);
            float R(double a, double b) => (float)(a + rnd.NextDouble() * (b - a));
            Mesh tall = GreyboxKit.MeshOf("Grass_Card_Tall"), shortGrass = GreyboxKit.MeshOf("Grass_Card_Short"), dry = GreyboxKit.MeshOf("Grass_Card_Dry");
            Material[] tallM = GreyboxKit.MaterialsOf("Grass_Card_Tall"), shortM = GreyboxKit.MaterialsOf("Grass_Card_Short"), dryM = GreyboxKit.MaterialsOf("Grass_Card_Dry");
            const float step = 1.0f;
            for (float gx = -Size / 2f - 15f; gx < Size / 2f + 15f; gx += step)
            for (float gz = -Size / 2f - 15f; gz < Size / 2f + 15f; gz += step)
            {
                float x = gx + R(0, step), z = gz + R(0, step);
                float forest = (float)Map.ForestAt(x, z), moist = (float)Map.MoistureAt(x, z);
                if (Map.SlopeAt(x, z) > 0.8 || F.Water(x, z) < 1.6f || F.Road(x, z) < 4.2f || F.Track(x, z) < 1.3f) continue;
                float d = Mathf.Sqrt(x * x + z * z);
                if (d < 9f) continue;
                float open = 1f - Smooth(0.3f, 0.7f, forest);
                double chance = open * 0.9 + 0.05;
                if (rnd.NextDouble() > chance) continue;
                float high = HeightAt(x, z) - (float)Map.HomeHeight;
                bool isDry = moist < 0.28f + (float)(rnd.NextDouble() * 0.12) || high > 130f;
                bool yard = d < HomeRadius;
                Mesh mesh; Material[] mats;
                if (isDry) { mesh = dry; mats = dryM; }
                else if (yard || forest > 0.45f) { mesh = shortGrass; mats = shortM; }
                else { mesh = tall; mats = tallM; }
                float y = HeightAt(x, z) - 0.02f;
                float scale = R(1.1, 1.6) * (yard ? 0.75f : 1f);
                grass.Add(mesh, mats, Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, R(0, 360), 0f), new Vector3(scale, R(0.55, 0.85) * scale, scale)), 25f);
            }
        }

        private static void ScatterNature(InstancedScatter trees, InstancedScatter cover, Transform props)
        {
            var rnd = new Random(1789);
            var trunks = new GameObject("Tree Trunks").transform;
            trunks.SetParent(props, false);
            float R(double a, double b) => (float)(a + rnd.NextDouble() * (b - a));
            string Pick(params (string name, double weight)[] options)
            {
                double total = 0;
                foreach (var o in options) total += o.weight;
                double roll = rnd.NextDouble() * total;
                foreach (var o in options) { roll -= o.weight; if (roll <= 0) return o.name; }
                return options[options.Length - 1].name;
            }

            // Trees are drawn instanced, each with its far version; in and near the playable region each gets an
            // invisible trunk collider.
            void Tree(string model, float x, float z, float scale)
            {
                float y = HeightAt(x, z) - 0.15f;
                trees.Add(GreyboxKit.MeshOf(model), GreyboxKit.MaterialsOf(model), Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, R(0, 360), 0f), Vector3.one * scale), 60f,
                    GreyboxKit.MeshOf(model + "_Far"), GreyboxKit.MaterialsOf(model + "_Far"));
                if (!InRegion(x, z, 40f)) return;
                var trunk = new GameObject("Trunk").AddComponent<CapsuleCollider>();
                trunk.transform.SetParent(trunks, false);
                trunk.transform.position = new Vector3(x, y + 2.5f * scale, z);
                trunk.radius = 0.45f * scale;
                trunk.height = 5f * scale;
                trunk.gameObject.isStatic = true;
            }
            void Cover(string model, float x, float z, float scale, float sink = 0.05f) =>
                cover.Add(GreyboxKit.MeshOf(model), GreyboxKit.MaterialsOf(model), Matrix4x4.TRS(new Vector3(x, HeightAt(x, z) - sink, z), Quaternion.Euler(0f, R(0, 360), 0f), Vector3.one * scale), 40f);

            // The forest (ADR 0027): southern Appalachian woods, closed canopy almost everywhere. Species follow the
            // ground: hemlock and poplar along the creeks, poplar and oak in the damp coves, oak and hickory on the
            // slopes, shortleaf pine on dry ridges and in old fields, dogwood at the edges. Close spacing in and
            // around the playable square, wider in the backdrop (seen far off, as far versions).
            string Species(float forest, float moist, float water, float high)
            {
                if (forest < 0.4f) return Pick(("Tree_ShortleafPine_A", 3), ("Tree_ShortleafPine_B", 2), ("Tree_Dogwood_A", 2), ("Tree_Hickory_B", 1));
                if (water < 16f) return Pick(("Tree_Hemlock_A", 5), ("Tree_Poplar_A", 2), ("Tree_Poplar_B", 2), ("Tree_WhiteOak_B", 1));
                if (moist > 0.5f) return Pick(("Tree_Poplar_A", 3), ("Tree_Poplar_B", 3), ("Tree_WhiteOak_A", 2), ("Tree_Hemlock_A", 1), ("Tree_Hickory_A", 1), ("Tree_Dogwood_A", 1));
                if (high > 70f || moist < 0.25f) return Pick(("Tree_ShortleafPine_A", 3), ("Tree_ShortleafPine_B", 3), ("Tree_WhiteOak_A", 2), ("Tree_Hickory_A", 2), ("Tree_Hickory_B", 1));
                return Pick(("Tree_WhiteOak_A", 3), ("Tree_WhiteOak_B", 3), ("Tree_Hickory_A", 2), ("Tree_Hickory_B", 2), ("Tree_Poplar_A", 1), ("Tree_ShortleafPine_A", 1), ("Tree_Dogwood_A", 1));
            }
            float near = Size / 2f + 60f;
            for (float gx = -WorldSize / 2f; gx < WorldSize / 2f; gx += 6f)
            for (float gz = -WorldSize / 2f; gz < WorldSize / 2f; gz += 6f)
            {
                bool backdrop = Mathf.Abs(gx) > near || Mathf.Abs(gz) > near;
                // In the backdrop, one cell in two (offset rows), and bigger trees, so it reads as forest from afar.
                if (backdrop && ((int)((gx + WorldSize) / 6f) + (int)((gz + WorldSize) / 6f)) % 2 == 1) continue;
                float x = gx + R(0, 6), z = gz + R(0, 6);
                float forest = (float)Map.ForestAt(x, z), slope = (float)Map.SlopeAt(x, z), high = HeightAt(x, z) - (float)Map.HomeHeight;
                if (slope > 1.25f) continue;
                if (rnd.NextDouble() > forest * 0.95 + 0.02 || !Clear(x, z, 6f)) continue;
                float moist = (float)Map.MoistureAt(x, z), water = F.Water(x, z);
                Tree(Species(forest, moist, water, high), x, z, R(0.85, 1.15) * (backdrop ? 1.15f : 1f));
            }
            // A big white oak shading the edge of the yard.
            Tree("Tree_WhiteOak_A", -24f, -12f, 1.15f);

            // The understory: rhododendron thickets along the creeks and in the coves (solid, like a real laurel
            // hell), mountain laurel on the drier slopes, saplings and stumps under the canopy.
            var thickets = new GameObject("Thickets").transform;
            thickets.SetParent(props, false);
            for (float gx = -Size / 2f - 30f; gx < Size / 2f + 30f; gx += 5f)
            for (float gz = -Size / 2f - 30f; gz < Size / 2f + 30f; gz += 5f)
            {
                float x = gx + R(0, 5), z = gz + R(0, 5);
                if (!Clear(x, z, 4f)) continue;
                float forest = (float)Map.ForestAt(x, z), moist = (float)Map.MoistureAt(x, z), water = F.Water(x, z);
                if (forest < 0.45f) continue;
                double roll = rnd.NextDouble();
                bool creek = water > 2.5f && water < 14f, cove = Map.IsDeepCove(x, z);
                if ((creek && roll < 0.42) || (cove && roll < 0.25))
                {
                    string model = rnd.NextDouble() < 0.5 ? "Rhododendron_A" : "Rhododendron_B";
                    float scale = R(0.8, 1.2);
                    Cover(model, x, z, scale, 0.25f);
                    if (InRegion(x, z, 10f))
                    {
                        var body = new GameObject("Thicket").AddComponent<CapsuleCollider>();
                        body.transform.SetParent(thickets, false);
                        body.transform.position = new Vector3(x, HeightAt(x, z) + 1.2f * scale, z);
                        body.radius = 1.5f * scale;
                        body.height = 2.6f * scale;
                        body.gameObject.isStatic = true;
                    }
                }
                else if (moist < 0.35f && roll < 0.16) Cover("Laurel_A", x, z, R(0.8, 1.2), 0.2f);
                else if (roll < 0.06) Cover("Sapling_A", x, z, R(0.7, 1.3), 0.1f);
                else if (roll < 0.075) Cover("Stump_A", x, z, R(0.8, 1.2), 0.15f);
            }

            // Grass, flowers and ferns in the playable region: dense, instanced, no colliders.
            const float coverStep = 2.1f;
            for (float gx = -Size / 2f - 20f; gx < Size / 2f + 20f; gx += coverStep)
            for (float gz = -Size / 2f - 20f; gz < Size / 2f + 20f; gz += coverStep)
            {
                float x = gx + R(0, coverStep), z = gz + R(0, coverStep);
                float d = Mathf.Sqrt(x * x + z * z);
                bool yard = d < HomeRadius;
                if (!yard && !Clear(x, z, 0f)) continue;
                if (yard && (d < 10f || F.Track(x, z) < 2f)) continue;
                float forest = (float)Map.ForestAt(x, z), moist = (float)Map.MoistureAt(x, z), water = F.Water(x, z);
                if (Map.SlopeAt(x, z) > 0.9) continue;
                double roll = rnd.NextDouble();
                if (yard) { }
                else if (forest > 0.72f) { if (roll < 0.2 + moist * 0.15) Cover("Fern", x, z, R(0.9, 1.5)); }
                else if (forest > 0.35f) { if (roll < 0.16) Cover("Fern", x, z, R(0.8, 1.3)); }
                else if (water < 6f) { if (roll < 0.1) Cover("Fern", x, z, R(0.8, 1.2)); }
                else if (roll < 0.07 + (1f - moist) * 0.05) Cover("Wildflowers", x, z, R(0.8, 1.2));
            }
            // Bushes along the forest's edge, kudzu swallowing edges and roadsides.
            for (float gx = -Size / 2f - 20f; gx < Size / 2f + 20f; gx += 7f)
            for (float gz = -Size / 2f - 20f; gz < Size / 2f + 20f; gz += 7f)
            {
                float x = gx + R(0, 7), z = gz + R(0, 7);
                if (!Clear(x, z, 3f)) continue;
                float forest = (float)Map.ForestAt(x, z), road = F.Road(x, z);
                bool edge = forest > 0.2f && forest < 0.5f, roadside = road < 20f, woods = forest >= 0.5f;
                double roll = rnd.NextDouble();
                if ((edge || roadside) && roll < 0.2) Cover("Kudzu_Mound", x, z, R(0.7, 1.3), 0.3f);
                else if (woods && roll < 0.06) Cover(rnd.NextDouble() < 0.5 ? "Bush_A" : "Bush_B", x, z, R(0.7, 1.3), 0.2f);
                else if (edge && roll < 0.35) Cover(rnd.NextDouble() < 0.5 ? "Bush_A" : "Bush_B", x, z, R(0.6, 1.1), 0.2f);
                else if (roll < 0.03) Cover(rnd.NextDouble() < 0.5 ? "Bush_A" : "Bush_B", x, z, R(0.6, 1.1), 0.2f);
            }

            // Rocks and logs are solid: stones on steep ground and along the streams, fallen logs in the woods.
            var solid = new GameObject("Rocks and Logs").transform;
            solid.SetParent(props, false);
            for (int i = 0; i < 6000; i++)
            {
                float x = R(-Size / 2f - 40f, Size / 2f + 40f), z = R(-Size / 2f - 40f, Size / 2f + 40f);
                float water = F.Water(x, z), slope = (float)Map.SlopeAt(x, z), forest = (float)Map.ForestAt(x, z);
                bool bank = water > 0.5f && water < 5f, stony = slope > 0.45f, woods = forest > 0.5f;
                double chance = bank ? 0.12 : stony ? 0.1 : woods ? 0.04 : 0.0;
                if (rnd.NextDouble() > chance) continue;
                if (!bank && !Clear(x, z, 0f)) continue;
                if (DistanceToBridge(x, z) < 16f || F.Road(x, z) < 8f || Mathf.Sqrt(x * x + z * z) < HomeRadius + 4f) continue;
                if (woods && !bank && rnd.NextDouble() < 0.45)
                    GreyboxKit.Place("Log_Fallen", new Vector3(x, HeightAt(x, z) - 0.15f, z), R(0, 360), R(0.8, 1.2), solid, collider: true);
                else
                    GreyboxKit.Place(rnd.NextDouble() < 0.6 ? "Rock_A" : "Rock_B", new Vector3(x, HeightAt(x, z) - 0.3f, z), R(0, 360), R(0.6, stony ? 2.0 : 1.2), solid, collider: true);
            }
        }

        // ---- Gathering and hauling --------------------------------------------------------------------

        // Spots to gather from, each with a stable id the simulation remembers ("berries_007").
        private static void BuildGathering(Transform props)
        {
            var root = new GameObject("Gather Spots").transform;
            root.SetParent(props, false);
            var rnd = new Random(2024);
            float R(double a, double b) => (float)(a + rnd.NextDouble() * (b - a));
            var counts = new Dictionary<string, int>();
            int Count(string kind) => counts.TryGetValue(kind, out int n) ? n : 0;

            void Spot(string kind, string item, string verb, string model, float x, float z, float yaw, string pickedModel = null)
            {
                int n = Count(kind);
                counts[kind] = n + 1;
                var spot = new GameObject("Gather " + kind + " " + n);
                spot.transform.SetParent(root, false);
                spot.transform.position = OnGround(x, z);
                GameObject visual = GreyboxKit.Place(model, spot.transform.position - Vector3.up * 0.05f, yaw, 1f, spot.transform, collider: false);
                GameObject picked = pickedModel == null ? null : GreyboxKit.Place(pickedModel, spot.transform.position - Vector3.up * 0.1f, yaw, 0.8f, spot.transform, collider: false);
                if (picked != null) picked.SetActive(false);
                spot.AddComponent<GatherSpot>().Configure(kind + "_" + n.ToString("000"), item, verb, visual, picked);
            }
            float Lim = Size / 2f - 20f;

            // Berries along the forest's edge; herbs on stream banks and in the woods; deadfall under the trees;
            // salvage along the highway, at the overpass and in the ruins.
            for (int i = 0; i < 6000 && Count("berries") < 22; i++)
            {
                float x = R(-Lim, Lim), z = R(-Lim, Lim), forest = (float)Map.ForestAt(x, z);
                if (forest > 0.2f && forest < 0.5f && Clear(x, z, 4f)) Spot("berries", "wild_berries", "Pick", "Berry_Bush", x, z, R(0, 360), "Bush_A");
            }
            for (int i = 0; i < 6000 && Count("herbs") < 16; i++)
            {
                float x = R(-Lim, Lim), z = R(-Lim, Lim), water = F.Water(x, z);
                bool bank = water > 2.5f && water < 10f;
                bool woods = Map.ForestAt(x, z) > 0.5 && rnd.NextDouble() < 0.4;
                if ((bank || woods) && Clear(x, z, 2f)) Spot("herbs", "medicinal_herbs", "Gather", "Herb_Patch", x, z, R(0, 360));
            }
            for (int i = 0; i < 6000 && Count("deadfall") < 18; i++)
            {
                float x = R(-Lim, Lim), z = R(-Lim, Lim);
                if (Map.ForestAt(x, z) > 0.45 && Clear(x, z, 3f)) Spot("deadfall", "firewood", "Gather", "Deadfall", x, z, R(0, 360));
            }
            Spot("deadfall", "firewood", "Gather", "Deadfall", -22f, -20f, 40f); // one just outside the yard
            Vector3 o = _overpass;
            Vector2 dir = RoadDirection(NearestRoadIndex(o)), side = new Vector2(-dir.y, dir.x);
            // The overpass is a place: its heaps are search spots that stay searched.
            var overpassSpots = new List<Vector3>();
            foreach (float along in new[] { -12f, 10f, -2f })
            {
                Vector2 p = new Vector2(o.x, o.z) + dir * along + side * (along < 0 ? 7f : -7f);
                overpassSpots.Add(new Vector3(p.x, 0f, p.y));
            }
            if (Content.Places.TryGetValue("overpass", out PlaceDef overpassDef))
                RuinKit.AddSpotsAt(new PlacedPlace { Id = "overpass", Kind = "overpass", P = new Vec2(o.x, o.z) }, overpassDef, props, HeightAt, overpassSpots, "the wreckage");
            float walked = 0f;
            for (int i = 1; i < Map.Road.Count; i++)
            {
                walked += Vector2.Distance(new Vector2((float)Map.Road[i].P.X, (float)Map.Road[i].P.Z), new Vector2((float)Map.Road[i - 1].P.X, (float)Map.Road[i - 1].P.Z));
                if (walked < 70f) continue;
                walked = 0f;
                float x = (float)Map.Road[i].P.X, z = (float)Map.Road[i].P.Z;
                if (!InRegion(x, z, -20f) || DistanceToBridge(x, z) < 20f || Vector3.Distance(new Vector3(x, 0f, z), new Vector3(o.x, 0f, o.z)) < 30f) continue;
                Vector2 s = new Vector2(-RoadDirection(i).y, RoadDirection(i).x) * (rnd.NextDouble() < 0.5 ? -6.5f : 6.5f);
                Spot("salvage", "scrap_metal", "Search", "Salvage_Pile", x + s.x, z + s.y, R(0, 360));
            }
        }


        // The stores shed: an open lean-to in the yard whose shelves and floor fill with what is in the stores.
        private static void BuildStores(Transform props)
        {
            GameObject shed = GreyboxKit.Place("Stores_Shed", OnGround(-15f, -10f), 90f, 1f, props, collider: true);
            shed.name = "Stores Shed";
            var slots = new List<Transform>();
            // Model space: floor spans x -2.5..2.5, back wall at -Z (open side +Z); shelves at 0.9 m and 1.7 m.
            void Slot(float x, float y, float z, float scale)
            {
                var t = new GameObject("Slot").transform;
                t.SetParent(shed.transform, false);
                t.localPosition = new Vector3(x, y, z);
                t.localRotation = Quaternion.Euler(0f, (slots.Count * 37) % 30 - 15, 0f);
                t.localScale = Vector3.one * scale;
                slots.Add(t);
            }
            for (int i = 0; i < 5; i++) Slot(-2f + i, 0.16f, -0.55f, 1f);   // floor, back row
            for (int i = 0; i < 5; i++) Slot(-2f + i, 0.16f, 0.45f, 1f);    // floor, front row
            for (int i = 0; i < 5; i++) Slot(-2f + i, 0.93f, -1.1f, 0.6f);  // lower shelf
            for (int i = 0; i < 5; i++) Slot(-2f + i, 1.73f, -1.1f, 0.6f);  // upper shelf

            StoresDisplay.Rule Rule(string itemOrTag, string model, int units, int max) =>
                new StoresDisplay.Rule { itemOrTag = itemOrTag, prop = GreyboxKit.Model(model), unitsPerProp = units, maxProps = max };
            shed.AddComponent<StoresDisplay>().Configure(new[]
            {
                Rule("firewood", "Stock_Firewood", 4, 4),
                Rule("wild_berries", "Stock_Basket", 4, 3),
                Rule("medicine", "Stock_Herbs", 3, 3),
                Rule("salvage", "Stock_Scrap", 3, 3),
                Rule("food", "Stock_Sack", 5, 4),
                Rule("trade", "Stock_Crate", 5, 3),
                Rule("building", "Stock_Crate", 5, 2),
            }, slots.ToArray());
        }

        // The pack on the player's back, the interact prompt, and the load's effect on speed.
        private static void AddHauling(SceneBuildKit.PlayerRig rig)
        {
            Transform chest = rig.Player.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Chest);
            GameObject Piece(string model, Vector3 local, float yaw)
            {
                // Placed in the player's own frame (facing +Z), then hung on the chest bone so it moves with the body.
                Vector3 world = rig.Player.transform.TransformPoint(local);
                Quaternion rot = rig.Player.transform.rotation * Quaternion.Euler(0f, yaw, 0f);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(GreyboxKit.Model(model));
                go.transform.SetPositionAndRotation(world, rot);
                go.transform.SetParent(chest, true);
                go.SetActive(false);
                return go;
            }
            GameObject pack = Piece("Pack_Base", new Vector3(0f, 0.98f, -0.2f), 180f);
            GameObject sack = Piece("Pack_Sack", new Vector3(0.2f, 1.08f, -0.24f), 0f);
            GameObject bundle = Piece("Pack_Bundle", new Vector3(0f, 1.5f, -0.22f), 0f);
            rig.Player.AddComponent<PackLoad>().Configure(pack, sack, bundle);
            rig.Player.AddComponent<Interactor>();
        }

        private static void SetScatter(InstancedScatter scatter, float drawDistance, bool shadows)
        {
            var so = new SerializedObject(scatter);
            so.FindProperty("drawDistance").floatValue = drawDistance;
            so.FindProperty("castShadows").boolValue = shadows;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Sky, fog, post-processing ---------------------------------------------------------------

        // The fire and lamps that come up at dusk (ADR 0028): the key image of coming home.
        private static void BuildHomeLights(TravelClock clock)
        {
            var root = new GameObject("Home Lights").transform;
            Light Point(string name, Vector3 at, Color colour, float range)
            {
                var l = new GameObject(name).AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = new Vector3(at.x, HeightAt(at.x, at.z) + at.y, at.z);
                l.type = LightType.Point;
                l.color = colour;
                l.range = range;
                l.shadows = LightShadows.Soft;
                l.intensity = 0f;
                l.enabled = false;
                return l;
            }
            StructureView View(string id) { GameObject s = GameObject.Find("Structure " + id); return s != null ? s.GetComponent<StructureView>() : null; }
            var fire = Point("Hearth Fire", new Vector3(-16f, 1.0f, 2f), new Color(1f, 0.55f, 0.22f), 20f);
            var dwelling = Point("Dwelling Lamp", new Vector3(0f, 2.2f, 13.5f), new Color(1f, 0.7f, 0.38f), 14f);
            var infirmary = Point("Infirmary Lamp", new Vector3(15.5f, 2.2f, 10f), new Color(1f, 0.72f, 0.42f), 11f);
            dwelling.shadows = LightShadows.None;
            infirmary.shadows = LightShadows.None;
            root.gameObject.AddComponent<HomeLights>().Configure(clock, fire, dwelling, infirmary, View("hearth"), View("dwelling"), View("infirmary"));
        }

        private static void BuildAtmosphere(Light sun, Light moon, TravelClock clock, PlayerLocation location)
        {
            string skyPath = Generated + "/M2_Region_Sky.mat";
            AssetDatabase.DeleteAsset(skyPath);
            // Hearthline/Sky: gradient, sunset glow, sun disc, drifting cumulus, stars (ADR 0018).
            var sky = new Material(Shader.Find("Hearthline/Sky"));
            AssetDatabase.CreateAsset(sky, skyPath);
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Trilight;

            LookKit.InstallHeightFog();
            var atmosphere = new GameObject("Atmosphere").AddComponent<Atmosphere>();
            // Fog lies on the valley floor and in the creek bottoms below it (ADR 0028).
            atmosphere.Configure((float)Map.HomeHeight - 4f, atmosphere.gameObject.AddComponent<ColourGrade>());
            SceneBuildKit.SetRef(atmosphere, "clock", clock);
            SceneBuildKit.SetRef(atmosphere, "sun", sun);
            SceneBuildKit.SetRef(atmosphere, "moon", moon);
            SceneBuildKit.SetRef(atmosphere, "location", location);
            atmosphere.Apply(9f, 1f);

            // Post-processing: a gentle grade toward the warm greens and golds of the references.
            string profilePath = Generated + "/M2_Region_Post.asset";
            AssetDatabase.DeleteAsset(profilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(0.25f); grade.contrast.Override(12f); grade.saturation.Override(8f);
            var white = profile.Add<WhiteBalance>(true); white.temperature.Override(6f); white.tint.Override(2f);
            var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.0f); bloom.intensity.Override(0.45f); bloom.scatter.Override(0.65f);
            var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.22f); vignette.smoothness.Override(0.45f);
            foreach (VolumeComponent c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);

            var volume = new GameObject("Post-processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            Camera main = Camera.main;
            if (main != null)
            {
                var data = main.GetComponent<UniversalAdditionalCameraData>();
                if (data == null) data = main.gameObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                main.farClipPlane = 900f;
            }
        }

        // ---- Areas ------------------------------------------------------------------------------------

        // The lay of the land in the playable region, from the map: ridge, deep woods, woods or field.
        private enum Ground { Field, Woods, DeepWoods, Ridge }

        private static Ground GroundAt(float x, float z)
        {
            if (HeightAt(x, z) > Map.HomeHeight + 55 || Map.SlopeAt(x, z) > 0.7) return Ground.Ridge;
            double f = Map.ForestAt(x, z);
            return Map.IsDeepCove(x, z) ? Ground.DeepWoods : f > 0.35 ? Ground.Woods : Ground.Field;
        }

        /// <summary>
        /// The areas are read off the map (ADR 0024). The land is cut into 20 m tiles by what grows there,
        /// joined into rectangles; the road, the water, the bridges, the overpass, the ruins and the homestead are
        /// laid over them as smaller boxes, so they name the place where they are.
        /// </summary>
        private static Transform BuildAreas(out int count)
        {
            var root = new GameObject("Areas").transform;
            int made = 0;
            float baseY = (float)Map.HomeHeight;
            AreaVolume Area(string name, string place, string[] tags, Vector3 centre, Vector3 size, float yaw = 0f, int thin = 0, bool homestead = false)
            {
                var go = new GameObject("Area " + name);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
                var volume = go.AddComponent<AreaVolume>();
                volume.Configure(place, tags, size, thin, homestead);
                made++;
                return volume;
            }

            // The land: tiles by majority of what grows there, merged into rectangles.
            const float tile = 20f;
            int n = Mathf.RoundToInt(Size / tile);
            var kind = new Ground[n, n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                var votes = new int[4];
                for (int b = 0; b < 3; b++)
                for (int a = 0; a < 3; a++)
                    votes[(int)GroundAt(-Size / 2f + (i + (a + 0.5f) / 3f) * tile, -Size / 2f + (j + (b + 0.5f) / 3f) * tile)]++;
                int best = 0;
                for (int k = 1; k < 4; k++) if (votes[k] > votes[best]) best = k;
                // Coves are narrow and damp; a third of the tile is enough to make it a hollow.
                if (votes[(int)Ground.DeepWoods] >= 3) best = (int)Ground.DeepWoods;
                kind[i, j] = (Ground)best;
            }
            var done = new bool[n, n];
            int[] counters = new int[4];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                if (done[i, j]) continue;
                Ground g = kind[i, j];
                int w = 1;
                while (i + w < n && !done[i + w, j] && kind[i + w, j] == g) w++;
                int h = 1;
                while (j + h < n)
                {
                    bool row = true;
                    for (int a = i; a < i + w; a++) if (done[a, j + h] || kind[a, j + h] != g) { row = false; break; }
                    if (!row) break;
                    h++;
                }
                for (int b = j; b < j + h; b++) for (int a = i; a < i + w; a++) done[a, b] = true;
                var centre = new Vector3(-Size / 2f + (i + w / 2f) * tile, baseY, -Size / 2f + (j + h / 2f) * tile);
                var size = new Vector3(w * tile, 800f, h * tile);
                string label = g + " " + counters[(int)g]++;
                switch (g)
                {
                    case Ground.Field: Area(label, "the valley meadows", new[] { "field" }, centre, size); break;
                    case Ground.Woods: Area(label, "Laurel Woods", new[] { "woods" }, centre, size); break;
                    case Ground.DeepWoods: Area(label, "the deep hollow", new[] { "deep_woods", "hollow" }, centre, size, thin: 1); break;
                    default: Area(label, "the mountain's foot", new[] { "ridge" }, centre, size); break;
                }
            }

            // Thin boxes along the water and the road, a few points at a time. The bridges and the overpass are
            // flatter still, so they are the smallest box where they stand and name the place.
            const float tall = 300f;
            void Along(List<PathPoint> pts, int stride, float extra, string name, string place, string[] tags)
            {
                for (int k = 0; k + stride < pts.Count; k += stride)
                {
                    var a = new Vector2((float)pts[k].P.X, (float)pts[k].P.Z);
                    var b = new Vector2((float)pts[k + stride].P.X, (float)pts[k + stride].P.Z);
                    Vector2 mid = (a + b) / 2f;
                    if (!InRegion(mid.x, mid.y, 20f)) continue;
                    float width = (float)pts[k].Width + extra;
                    Area(name + " " + k, place, tags, new Vector3(mid.x, baseY, mid.y), new Vector3(width, tall, Vector2.Distance(a, b) + 1f), Yaw(b - a));
                }
            }
            int s = 0;
            foreach (Waterway w in Map.Streams)
                Along(w.Points, 4, 10f, (w.IsRiver ? "River " : "Creek ") + s++, w.IsRiver ? "the river" : "the creek", new[] { "creek" });
            Along(Map.Road, 3, 7f, "Road", "the old highway", new[] { "road" });

            int bridgeCount = 0;
            foreach (Hearthline.Sim.World.Bridge b in Map.Bridges)
            {
                var from = new Vector2((float)b.From.X, (float)b.From.Z);
                var to = new Vector2((float)b.To.X, (float)b.To.Z);
                Vector2 mid = (from + to) / 2f;
                if (!InRegion(mid.x, mid.y, 20f)) continue;
                Area("Bridge " + bridgeCount++, "the guardrail bridge", new[] { "creek", "river_crossing" }, new Vector3(mid.x, HeightAt(mid.x, mid.y), mid.y), new Vector3(12f, 20f, Vector2.Distance(from, to) + 10f), Yaw(to - from));
            }
            if (Content.Places.TryGetValue("overpass", out PlaceDef overpassPlace))
                Area("Overpass", overpassPlace.Name, overpassPlace.AreaTags.ToArray(), _overpass + Vector3.up * 4f, new Vector3(40f, 20f, 50f), Yaw(RoadDirection(NearestRoadIndex(_overpass))) - 90f)
                    .SetPlace("overpass", "overpass");

            foreach (PlacedPlace place in Places)
            {
                if (!InRegion((float)place.P.X, (float)place.P.Z, -10f)) continue;
                PlaceDef def = Content.Places[place.Kind];
                Vector2 size = RuinKit.Footprint(place.Kind);
                float y = HeightAt((float)place.P.X, (float)place.P.Z);
                Area("Place " + place.Id, def.Name, def.AreaTags.ToArray(), new Vector3((float)place.P.X, y, (float)place.P.Z), new Vector3(size.x, 60f, size.y), (float)place.Facing, def.ThinLevel)
                    .SetPlace(place.Id, place.Kind);
            }

            Area("Homestead", "the homestead", new[] { "homestead" }, new Vector3(0f, baseY, 0f), new Vector3(HomeRadius * 2f, 60f, HomeRadius * 2f), homestead: true);

            var home = new GameObject("Homestead Centre").transform;
            home.position = new Vector3(0f, HeightAt(0f, 0f), 0f);
            count = made;
            return home;
        }
    }
}

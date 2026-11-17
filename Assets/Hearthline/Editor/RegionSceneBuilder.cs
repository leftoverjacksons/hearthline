using System;
using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Game;
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
    /// Builds Assets/Scenes/M2_Region.unity: a 600 m region for the fast loop, laid out after the homestead
    /// references in ArtReference/places: a cabin clearing on a meadow rise, a creek in front crossed by a
    /// guardrail bridge, the old highway beyond it with a fallen overpass, woods to the west turning deep, a
    /// meadow to the east, and a ridge to the north. Dressed with the greybox kit (ADR 0014): painted ground,
    /// low-poly trees and props, instanced ground cover, sky, fog and post-processing.
    /// Every part is an <see cref="AreaVolume"/> so the simulation knows where the party is.
    /// Menu: Hearthline → Rebuild M2 region scene. From the CLI:
    /// <c>unity command script eval --code "return Hearthline.EditorTools.RegionSceneBuilder.Build();" --timeout 300</c>
    /// </summary>
    public static class RegionSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/M2_Region.unity";
        private const string Generated = SceneBuildKit.GeneratedFolder;
        private const string GroundPath = Generated + "/M2_Region_Ground.asset";
        private const string GroundMapPath = Generated + "/M2_Region_GroundMap.png";
        public const float Size = 600f;
        public const float RegionRadius = 300f;

        // Layout (metres). +Z is north. The homestead is at the origin.
        public const float HomeRadius = 32f;
        public const float CreekZ = -48f;
        public const float RoadZ = -92f;
        public const float WoodsEdgeX = -70f;
        public const float DeepWoodsX = -210f;
        public const float RidgeZ = 150f;
        public const float BridgeDeckY = 1.75f;
        public const float WaterY = -0.3f;
        public static readonly Vector3 OverpassAt = new Vector3(170f, 0f, RoadZ);

        [MenuItem("Hearthline/Rebuild M2 region scene")]
        private static void Menu() => Debug.Log(Build());

        public static string Build()
        {
            GreyboxKit.Setup();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneBuildKit.EnsureFolder(Generated);

            Light sun = SceneBuildKit.CreateSun();
            Light moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 1f);
            moon.shadows = LightShadows.Soft;

            SceneBuildKit.BuildGround("Region Ground", GroundPath, Size, 200, HeightAt, GroundMaterial(), mapUv: true);
            var water = SceneBuildKit.Block(PrimitiveType.Plane, "Creek Water", new Vector3(0f, WaterY, 0f), new Vector3(Size / 10f, 1f, Size / 10f), WaterMaterial(), collider: false);
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var trees = new GameObject("Trees").AddComponent<InstancedScatter>();
            SetScatter(trees, drawDistance: 420f, shadows: true);
            var cover = new GameObject("Ground Cover").AddComponent<InstancedScatter>();
            SetScatter(cover, drawDistance: 130f, shadows: false);
            var props = new GameObject("Props").transform;

            BuildHomestead(props);
            BuildCrossings(props);
            ScatterNature(trees, cover, props);
            BuildGathering(props);
            BuildStores(props);
            Transform home = BuildAreas();

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
            int covers = SceneBuildKit.AddCoverSurfaces(HeightAt);
            CombatSceneKit.BuildEnemyPrefab();
            CombatSceneKit.AddSpawner(rig.Player.transform);
            new GameObject("Fight Director").AddComponent<FightDirector>().Configure(rig.Player.transform);
            CombatSceneKit.BakeNavMesh("Assets/Scenes/Generated/M2_Region_NavMesh.asset");

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildKit.AddToBuild(ScenePath, first: false);
            AssetDatabase.SaveAssets();
            return "Saved " + ScenePath + ": " + covers + " cover pieces, " + trees.InstanceCount + " trees, " + cover.InstanceCount + " ground-cover instances, "
                + props.childCount + " prop groups, " + Object.FindObjectsByType<AreaVolume>(FindObjectsInactive.Exclude).Length + " areas.";
        }

        // ---- Terrain --------------------------------------------------------------------------------

        private static float CreekCentre(float x) => CreekZ + Mathf.Sin(x * 0.02f) * 8f;

        /// <summary>Ground height. Public so tests and tools can place things on the ground.</summary>
        public static float HeightAt(float x, float z)
        {
            float hills = Mathf.PerlinNoise(x * 0.012f + 31.7f, z * 0.012f + 8.3f) * 6f
                        + Mathf.PerlinNoise(x * 0.05f + 2.9f, z * 0.05f + 19.1f) * 1.2f;

            // The ridge rises to the north.
            float ridge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RidgeZ - 40f, RidgeZ + 80f, z)) * 26f;

            // The homestead sits on a gentle, flat-topped rise.
            float d = Mathf.Sqrt(x * x + z * z);
            float h = Mathf.Lerp(2.5f, hills + ridge, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HomeRadius, HomeRadius + 30f, d)));

            // The old road is a flat strip.
            float roadBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 4f, Mathf.Abs(z - RoadZ)));
            h = Mathf.Lerp(h, 0.6f, roadBlend);

            // The creek is carved below water level along a gentle meander.
            float off = Mathf.Abs(z - CreekCentre(x));
            float creekBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 3f, off));
            h = Mathf.Lerp(Mathf.Min(h, 1.5f + off * 0.15f), -2f, creekBlend * creekBlend);
            float bankBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 14f, off));
            return Mathf.Lerp(h, Mathf.Min(h, 1.5f), bankBlend * (1f - creekBlend));
        }

        private static float Slope(float x, float z)
        {
            const float e = 1.5f;
            float dx = HeightAt(x + e, z) - HeightAt(x - e, z), dz = HeightAt(x, z + e) - HeightAt(x, z - e);
            return Mathf.Sqrt(dx * dx + dz * dz) / (2f * e);
        }

        // Dirt path from the cabin porch down to the bridge, and on from the bridge to the road.
        private static float PathDistance(float x, float z)
        {
            if (z > 13f || z < RoadZ + 4f) return 99f;
            return Mathf.Abs(x - Mathf.Sin(z * 0.15f) * 1.2f);
        }

        // The painted colour map: one texture over the whole ground, coloured by what each spot is.
        private static Material GroundMaterial()
        {
            const int res = 1024;
            var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
            var pixels = new Color[res * res];
            for (int j = 0; j < res; j++)
            for (int i = 0; i < res; i++)
            {
                float x = (i / (res - 1f) - 0.5f) * Size, z = (j / (res - 1f) - 0.5f) * Size;
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
            m.SetTexture("_DetailAlbedoMap", TextureForge.Albedo("grass"));
            m.SetTexture("_DetailNormalMap", TextureForge.Normal("grass"));
            m.SetTextureScale("_DetailAlbedoMap", Vector2.one * (Size / 2f));
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

            Color grass = Color.Lerp(new Color(0.33f, 0.42f, 0.16f), new Color(0.43f, 0.47f, 0.2f), n1);
            grass = Color.Lerp(grass, new Color(0.55f, 0.5f, 0.27f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.75f, n2)) * 0.7f);
            Color c = grass * (0.92f + fine * 0.16f);

            // Woods floor: leaf litter and moss, darker in the deep hollow.
            float woods = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WoodsEdgeX + 15f, WoodsEdgeX - 25f, x));
            Color floor = Color.Lerp(new Color(0.27f, 0.31f, 0.14f), new Color(0.33f, 0.28f, 0.16f), n2);
            c = Color.Lerp(c, floor * (0.9f + fine * 0.2f), woods);
            float deep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DeepWoodsX + 20f, DeepWoodsX - 20f, x));
            c = Color.Lerp(c, new Color(0.14f, 0.17f, 0.1f) * (0.9f + fine * 0.2f), deep);

            // Rock on steep ground (the ridge).
            float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.75f, Slope(x, z)));
            c = Color.Lerp(c, new Color(0.43f, 0.41f, 0.37f) * (0.85f + fine * 0.3f), rock);

            // Creek banks: mud, then gravel at the water.
            float off = Mathf.Abs(z - CreekCentre(x));
            c = Color.Lerp(c, new Color(0.33f, 0.27f, 0.19f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(11f, 6f, off)));
            c = Color.Lerp(c, new Color(0.47f, 0.44f, 0.38f) * (0.8f + fine * 0.4f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 4.5f, off)));

            // The old highway: gravel shoulders, cracked asphalt with grass in the cracks, a faded centre line.
            float road = Mathf.Abs(z - RoadZ);
            c = Color.Lerp(c, new Color(0.45f, 0.42f, 0.35f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(7f, 5.5f, road)));
            if (road < 4.6f)
            {
                float crack = Mathf.Abs(Mathf.PerlinNoise(x * 0.25f, z * 0.6f) - 0.5f);
                Color asphalt = new Color(0.2f, 0.2f, 0.2f) * (0.9f + fine * 0.2f);
                if (road < 0.22f && Mathf.Repeat(x, 6f) < 3f) asphalt = new Color(0.62f, 0.6f, 0.5f);
                c = crack < 0.025f ? new Color(0.3f, 0.38f, 0.15f) : asphalt;
            }

            // The homestead yard and its paths: trampled earth.
            float d = Mathf.Sqrt(x * x + z * z);
            Color dirt = new Color(0.42f, 0.33f, 0.22f) * (0.9f + fine * 0.2f);
            c = Color.Lerp(c, dirt, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9f, 4f, d)) * 0.8f);
            c = Color.Lerp(c, dirt, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.8f, 0.9f, PathDistance(x, z))));
            return c;
        }

        private static Material WaterMaterial()
        {
            // The creek's own shader: depth colour, refraction, ripples, glints, foam (ADR 0018).
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

        private static void BuildCrossings(Transform props)
        {
            var root = new GameObject("Crossings").transform;
            root.SetParent(props, false);

            // The guardrail bridge over the creek (its length runs along Z), kudzu creeping up the banks.
            GreyboxKit.Place("Bridge_Guardrail", new Vector3(0f, BridgeDeckY, CreekZ), 0f, 1f, root, collider: true);
            GreyboxKit.Place("Kudzu_Mound", OnGround(-4f, CreekZ + 14f), 30f, 0.8f, root, collider: false);
            GreyboxKit.Place("Kudzu_Mound", OnGround(4.5f, CreekZ - 15f), 200f, 0.7f, root, collider: false);

            // The fallen overpass: pillars, a standing deck end to the north, the broken span down on the road.
            Vector3 o = OverpassAt;
            GreyboxKit.Place("Overpass_Pillar", o + new Vector3(0f, 0f, 14f), 0f, 1f, root, collider: true);
            GreyboxKit.Place("Overpass_Pillar", o + new Vector3(-7f, 0f, -16f), 8f, 1f, root, collider: true);
            GreyboxKit.Place("Overpass_Deck_Standing", o + new Vector3(0f, 9.3f, 26f), 0f, 1f, root, collider: true);
            GameObject fallen = GreyboxKit.Place("Overpass_Deck_Fallen", o + new Vector3(2f, 4.2f, 1f), 6f, 1f, root, collider: true);
            fallen.transform.rotation = Quaternion.Euler(-17f, 6f, 4f);
            GreyboxKit.Place("Rubble", o + new Vector3(-5f, 0.6f, -10f), 30f, 1f, root, collider: true);
            GreyboxKit.Place("Rubble", o + new Vector3(6f, 0.6f, -13f), 140f, 0.7f, root, collider: true);
            // Kudzu hanging off the standing deck and swallowing the pillars.
            for (int i = 0; i < 4; i++)
            {
                GreyboxKit.Place("Kudzu_Drape", o + new Vector3(-5.6f, 8.8f, 18f + i * 5f), 90f, 1f, root, collider: false);
                GreyboxKit.Place("Kudzu_Drape", o + new Vector3(5.6f, 8.8f, 20f + i * 5f), 270f, 1f, root, collider: false);
            }
            GreyboxKit.Place("Kudzu_Mound", o + new Vector3(0f, 0.4f, 14f), 0f, 1.1f, root, collider: false);
            GreyboxKit.Place("Kudzu_Mound", o + new Vector3(-7f, 0.4f, -16f), 70f, 1f, root, collider: false);
            GreyboxKit.Place("Kudzu_Mound", o + new Vector3(4f, 3.5f, 6f), 20f, 0.8f, root, collider: false);

            // Jersey barriers left along the highway, some knocked askew.
            var rnd = new Random(404);
            for (float x = -Size / 2f + 20f; x < Size / 2f - 20f; x += 24f)
            {
                if (rnd.NextDouble() < 0.45 || Mathf.Abs(x) < 12f || Mathf.Abs(x - o.x) < 30f) continue;
                float side = rnd.NextDouble() < 0.5 ? -5.2f : 5.2f;
                GreyboxKit.Place("Jersey_Barrier", OnGround(x + (float)rnd.NextDouble() * 6f, RoadZ + side), 90f + (float)(rnd.NextDouble() - 0.5) * 30f, 1f, root, collider: true);
            }
        }

        // ---- Nature ---------------------------------------------------------------------------------

        private static bool Clear(float x, float z, float margin)
        {
            if (Mathf.Sqrt(x * x + z * z) < HomeRadius + margin) return false;
            if (Mathf.Abs(z - RoadZ) < 7f + margin * 0.3f) return false;
            if (Mathf.Abs(z - CreekCentre(x)) < 5.5f + margin * 0.3f) return false;
            if (Vector3.Distance(new Vector3(x, 0f, z), OverpassAt) < 26f + margin) return false;
            if (PathDistance(x, z) < 2.5f + Mathf.Max(0f, margin) * 0.2f) return false;
            return Mathf.Abs(x) < Size / 2f - 4f && Mathf.Abs(z) < Size / 2f - 4f;
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

            // Trees are drawn instanced; each gets an invisible trunk collider so you cannot walk through it.
            void Tree(string model, float x, float z, float scale)
            {
                float y = HeightAt(x, z) - 0.15f;
                trees.Add(GreyboxKit.MeshOf(model), GreyboxKit.MaterialsOf(model), Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, R(0, 360), 0f), Vector3.one * scale), 60f);
                var trunk = new GameObject("Trunk").AddComponent<CapsuleCollider>();
                trunk.transform.SetParent(trunks, false);
                trunk.transform.position = new Vector3(x, y + 2.5f * scale, z);
                trunk.radius = 0.45f * scale;
                trunk.height = 5f * scale;
                trunk.gameObject.isStatic = true;
            }
            void Cover(string model, float x, float z, float scale, float sink = 0.05f) =>
                cover.Add(GreyboxKit.MeshOf(model), GreyboxKit.MaterialsOf(model), Matrix4x4.TRS(new Vector3(x, HeightAt(x, z) - sink, z), Quaternion.Euler(0f, R(0, 360), 0f), Vector3.one * scale), 40f);

            // Trees on a jittered grid: dense and tall in the woods, pines on the ridge, solitary in the meadows.
            for (float gx = -Size / 2f; gx < Size / 2f; gx += 8f)
            for (float gz = -Size / 2f; gz < Size / 2f; gz += 8f)
            {
                float x = gx + R(0, 8), z = gz + R(0, 8);
                bool deep = x < DeepWoodsX, woods = x < WoodsEdgeX + R(-15, 15), ridge = z > RidgeZ - 15f;
                double chance = deep ? 0.8 : woods ? 0.6 : ridge ? 0.28 : 0.035;
                if (rnd.NextDouble() > chance || !Clear(x, z, 8f)) continue;
                if (deep) Tree(Pick(("Tree_Tall_Hollow", 3), ("Tree_Pine", 2), ("Tree_Broadleaf_B", 1)), x, z, R(0.9, 1.3));
                else if (woods) Tree(Pick(("Tree_Oak", 3), ("Tree_Broadleaf_B", 3), ("Tree_Broadleaf_C", 2), ("Tree_Pine", 2)), x, z, R(0.8, 1.2));
                else if (ridge) Tree(Pick(("Tree_Pine", 3), ("Tree_Oak", 1), ("Tree_Broadleaf_C", 1)), x, z, R(0.8, 1.15));
                else Tree(Pick(("Tree_Oak", 2), ("Tree_Broadleaf_B", 2), ("Tree_Broadleaf_C", 1)), x, z, R(1.0, 1.35));
            }
            // A big shade tree at the edge of the yard, as in the references.
            Tree("Tree_Broadleaf_B", -24f, -12f, 1.35f);

            // Grass, flowers and ferns: dense, instanced, no colliders.
            const float coverStep = 2.1f;
            for (float gx = -Size / 2f; gx < Size / 2f; gx += coverStep)
            for (float gz = -Size / 2f; gz < Size / 2f; gz += coverStep)
            {
                float x = gx + R(0, coverStep), z = gz + R(0, coverStep);
                bool yard = Mathf.Sqrt(x * x + z * z) < HomeRadius;
                if (!yard && !Clear(x, z, 0f)) continue;
                if (yard && (Mathf.Sqrt(x * x + z * z) < 10f || PathDistance(x, z) < 2f)) continue;
                bool deep = x < DeepWoodsX, woods = x < WoodsEdgeX;
                double roll = rnd.NextDouble();
                if (deep) { if (roll < 0.18) Cover("Fern", x, z, R(0.9, 1.5)); }
                else if (woods) { if (roll < 0.14) Cover("Fern", x, z, R(0.8, 1.3)); else if (roll < 0.2) Cover("Grass_Clump", x, z, R(0.8, 1.2)); }
                else if (yard) { if (roll < 0.08) Cover("Grass_Clump", x, z, R(0.6, 0.9)); }
                else
                {
                    bool meadow = x > 40f && z > -30f && z < 120f;
                    if (roll < 0.34) Cover("Grass_Clump", x, z, R(0.8, 1.4));
                    else if (roll < (meadow ? 0.43 : 0.36)) Cover("Wildflowers", x, z, R(0.8, 1.2));
                }
            }
            // Bushes in the woods, kudzu swallowing the woods' edge and the roadside.
            for (float gx = -Size / 2f; gx < Size / 2f; gx += 7f)
            for (float gz = -Size / 2f; gz < Size / 2f; gz += 7f)
            {
                float x = gx + R(0, 7), z = gz + R(0, 7);
                if (!Clear(x, z, 3f)) continue;
                bool woods = x < WoodsEdgeX + 10f, edge = Mathf.Abs(x - WoodsEdgeX) < 18f, roadside = Mathf.Abs(z - RoadZ) < 20f;
                double roll = rnd.NextDouble();
                if ((edge || roadside) && roll < 0.22) Cover("Kudzu_Mound", x, z, R(0.7, 1.3), 0.3f);
                else if (woods && roll < 0.3) Cover(rnd.NextDouble() < 0.5 ? "Bush_A" : "Bush_B", x, z, R(0.7, 1.3), 0.2f);
                else if (!woods && roll < 0.05) Cover(rnd.NextDouble() < 0.5 ? "Bush_A" : "Bush_B", x, z, R(0.6, 1.1), 0.2f);
            }

            // Rocks and logs are solid: real objects with colliders.
            var solid = new GameObject("Rocks and Logs").transform;
            solid.SetParent(props, false);
            for (int i = 0; i < 900; i++)
            {
                float x = R(-Size / 2f + 8f, Size / 2f - 8f), z = R(-Size / 2f + 8f, Size / 2f - 8f);
                float creekOff = Mathf.Abs(z - CreekCentre(x));
                bool bank = creekOff > 5f && creekOff < 12f;
                bool ridge = z > RidgeZ;
                bool woods = x < WoodsEdgeX;
                if (!bank && !ridge && !woods) continue;
                if (rnd.NextDouble() > (bank ? 0.5 : 0.18)) continue;
                if (!bank && !Clear(x, z, 0f)) continue;
                if (Vector3.Distance(new Vector3(x, 0f, z), new Vector3(0f, 0f, CreekZ)) < 16f) continue; // keep the bridge clear
                if (woods && rnd.NextDouble() < 0.3)
                    GreyboxKit.Place("Log_Fallen", OnGround(x, z) - Vector3.up * 0.15f, R(0, 360), R(0.8, 1.2), solid, collider: true);
                else
                    GreyboxKit.Place(rnd.NextDouble() < 0.6 ? "Rock_A" : "Rock_B", OnGround(x, z) - Vector3.up * 0.3f, R(0, 360), R(0.6, ridge ? 2.2 : 1.3), solid, collider: true);
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

            // Berries along the woods' edge and round the meadow; herbs in the woods and on the creek banks;
            // deadfall under the trees; salvage on the highway and at the overpass.
            for (int i = 0; i < 2000 && Count("berries") < 22; i++)
            {
                float x = R(-120, 280), z = R(-80, 160);
                bool edge = Mathf.Abs(x - WoodsEdgeX) < 20f || (x > 40f && Mathf.Abs(z - 40f) > 60f);
                if (edge && Clear(x, z, 4f)) Spot("berries", "wild_berries", "Pick", "Berry_Bush", x, z, R(0, 360), "Bush_A");
            }
            for (int i = 0; i < 2000 && Count("herbs") < 16; i++)
            {
                float x = R(-280, 280), z = R(-280, 280);
                float creekOff = Mathf.Abs(z - CreekCentre(x));
                bool bank = creekOff > 6f && creekOff < 12f && Mathf.Abs(x) > 20f;
                bool woods = x < WoodsEdgeX && Clear(x, z, 2f);
                if (bank || (woods && rnd.NextDouble() < 0.5)) Spot("herbs", "medicinal_herbs", "Gather", "Herb_Patch", x, z, R(0, 360));
            }
            for (int i = 0; i < 2000 && Count("deadfall") < 18; i++)
            {
                float x = R(-280, WoodsEdgeX + 10f), z = R(-280, 280);
                if (Clear(x, z, 3f)) Spot("deadfall", "firewood", "Gather", "Deadfall", x, z, R(0, 360));
            }
            Spot("deadfall", "firewood", "Gather", "Deadfall", -22f, -20f, 40f); // one just outside the yard
            Vector3 o = OverpassAt;
            Spot("salvage", "scrap_metal", "Search", "Salvage_Pile", o.x - 9f, o.z - 6f, 20f);
            Spot("salvage", "scrap_metal", "Search", "Salvage_Pile", o.x + 8f, o.z + 4f, 160f);
            Spot("salvage", "scrap_metal", "Search", "Salvage_Pile", o.x - 3f, o.z + 30f, 95f);
            for (float x = -260f; x < 260f; x += 70f)
                if (Mathf.Abs(x) > 20f && Mathf.Abs(x - o.x) > 30f)
                    Spot("salvage", "scrap_metal", "Search", "Salvage_Pile", x + R(-10, 10), RoadZ + (rnd.NextDouble() < 0.5 ? -6.5f : 6.5f), R(0, 360));
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

            var atmosphere = new GameObject("Atmosphere").AddComponent<Atmosphere>();
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

        private static Transform BuildAreas()
        {
            var root = new GameObject("Areas").transform;
            void Area(string name, string place, string[] tags, Vector3 centre, Vector3 size, int thin = 0, bool homestead = false)
            {
                var go = new GameObject("Area " + name);
                go.transform.SetParent(root, false);
                go.transform.position = centre;
                go.AddComponent<AreaVolume>().Configure(place, tags, size, thin, homestead);
            }

            const float tall = 120f;
            Area("Homestead", "the homestead", new[] { "homestead" }, Vector3.zero, new Vector3(HomeRadius * 2f, tall, HomeRadius * 2f), homestead: true);
            Area("Creek", "the creek", new[] { "creek" }, new Vector3(0f, 0f, CreekZ), new Vector3(Size, tall, 30f));
            Area("Bridge", "the guardrail bridge", new[] { "creek", "river_crossing" }, new Vector3(0f, 0f, CreekZ), new Vector3(10f, tall, 30f));
            Area("Road", "the old highway", new[] { "road" }, new Vector3(0f, 0f, RoadZ), new Vector3(Size, tall, 16f));
            Area("Overpass", "the fallen overpass", new[] { "road", "ruin" }, OverpassAt, new Vector3(40f, tall, 50f));
            Area("Meadow", "the east meadow", new[] { "field" }, new Vector3(160f, 0f, 40f), new Vector3(260f, tall, 160f));
            Area("Woods", "Laurel Woods", new[] { "woods" }, new Vector3((WoodsEdgeX - Size / 2f) / 2f, 0f, 0f), new Vector3(Size / 2f + WoodsEdgeX, tall, Size));
            Area("Deep Woods", "the deep hollow", new[] { "deep_woods", "hollow" }, new Vector3((DeepWoodsX - Size / 2f) / 2f, 0f, 0f), new Vector3(Size / 2f + DeepWoodsX, tall, Size), thin: 1);
            Area("Ridge", "Buck Ridge", new[] { "ridge" }, new Vector3(0f, 0f, (RidgeZ + Size / 2f) / 2f), new Vector3(Size, tall * 2f, Size / 2f - RidgeZ));

            var home = new GameObject("Homestead Centre").transform;
            home.position = new Vector3(0f, HeightAt(0f, 0f), 0f);
            return home;
        }
    }
}

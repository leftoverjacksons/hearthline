using Hearthline.Bridge;
using Hearthline.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/M2_Region.unity: a 600 m greybox region for the fast loop. Laid out after the
    /// homestead references in ArtReference/places: a cabin clearing on a meadow rise, a creek in front crossed
    /// by a guardrail bridge, the old highway beyond it with a fallen overpass, woods to the west turning deep,
    /// a meadow to the east, and a ridge to the north. Every part is an <see cref="AreaVolume"/> so the
    /// simulation knows where the party is.
    /// Menu: Hearthline → Rebuild M2 region scene. From the CLI:
    /// <c>unity command script eval --code "return Hearthline.EditorTools.RegionSceneBuilder.Build();"</c>
    /// </summary>
    public static class RegionSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/M2_Region.unity";
        private const string GroundPath = "Assets/Scenes/Generated/M2_Region_Ground.asset";
        public const float Size = 600f;
        public const float RegionRadius = 300f;

        // Layout (metres). +Z is north. The homestead is at the origin.
        public const float HomeRadius = 32f;
        public const float CreekZ = -48f;
        public const float RoadZ = -92f;
        public const float WoodsEdgeX = -70f;
        public const float DeepWoodsX = -210f;
        public const float RidgeZ = 150f;
        public static readonly Vector3 OverpassAt = new Vector3(170f, 0f, RoadZ);

        [MenuItem("Hearthline/Rebuild M2 region scene")]
        private static void Menu() => Debug.Log(Build());

        public static string Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneBuildKit.EnsureFolder(SceneBuildKit.GeneratedFolder);

            Material grass = SceneBuildKit.ColorMaterial("Greybox_Grass", new Color(0.42f, 0.5f, 0.33f));
            Material water = SceneBuildKit.ColorMaterial("Greybox_Water", new Color(0.25f, 0.4f, 0.55f));
            Material wood = SceneBuildKit.ColorMaterial("Greybox_Wood", new Color(0.45f, 0.33f, 0.22f));
            Material leaves = SceneBuildKit.ColorMaterial("Greybox_Leaves", new Color(0.24f, 0.38f, 0.2f));
            Material deepLeaves = SceneBuildKit.ColorMaterial("Greybox_DeepLeaves", new Color(0.13f, 0.22f, 0.14f));
            Material concrete = SceneBuildKit.ColorMaterial("Greybox_Concrete", new Color(0.55f, 0.55f, 0.52f));
            Material road = SceneBuildKit.ColorMaterial("Greybox_Road", new Color(0.36f, 0.33f, 0.3f));
            Material slot = SceneBuildKit.ColorMaterial("Greybox_Slot", new Color(0.8f, 0.7f, 0.45f));
            Material steel = SceneBuildKit.ColorMaterial("Greybox_Steel", new Color(0.7f, 0.72f, 0.75f));

            Light sun = SceneBuildKit.CreateSun();
            SceneBuildKit.BuildGround("Region Ground", GroundPath, Size, 200, HeightAt, grass);

            // Creek water: one plane below ground level, visible only where the creek is carved.
            GameObject creek = SceneBuildKit.Block(PrimitiveType.Plane, "Creek Water", new Vector3(0f, -0.9f, 0f), new Vector3(Size / 10f, 1f, Size / 10f), water, collider: false);
            creek.transform.SetParent(null);

            BuildHomestead(wood, slot);
            BuildBridge(wood, steel);
            BuildRoad(road, concrete);
            BuildTrees(wood, leaves, deepLeaves);
            Transform home = BuildAreas();

            SceneBuildKit.PlayerRig rig = SceneBuildKit.BuildPlayerRig(new Vector3(0f, HeightAt(0f, 8f) + 0.1f, 8f), yaw: 180f);
            var location = rig.Player.AddComponent<PlayerLocation>();
            SceneBuildKit.SetRef(location, "home", home);
            SceneBuildKit.SetFloat(location, "regionRadius", RegionRadius);

            var clock = new GameObject("Clock").AddComponent<TravelClock>();
            SceneBuildKit.SetRef(clock, "sun", sun);
            SceneBuildKit.SetRef(clock, "playerInput", rig.InputRelease);

            var trip = new GameObject("Trip").AddComponent<TripController>();
            SceneBuildKit.SetRef(trip, "location", location);
            SceneBuildKit.SetRef(trip, "clock", clock);

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildKit.AddToBuild(ScenePath, first: false);
            AssetDatabase.SaveAssets();
            return "Saved " + ScenePath + ": " + Size + " m region, " + Object.FindObjectsByType<AreaVolume>(FindObjectsInactive.Exclude).Length + " areas, player at the homestead.";
        }

        /// <summary>Ground height. Public so tests and tools can place things on the ground.</summary>
        public static float HeightAt(float x, float z)
        {
            float hills = Mathf.PerlinNoise(x * 0.012f + 31.7f, z * 0.012f + 8.3f) * 6f
                        + Mathf.PerlinNoise(x * 0.05f + 2.9f, z * 0.05f + 19.1f) * 1.2f;

            // The ridge rises to the north.
            float ridge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RidgeZ - 40f, RidgeZ + 80f, z)) * 26f;

            // The homestead sits on a gentle, flat-topped rise.
            float d = Mathf.Sqrt(x * x + z * z);
            float rise = 2.5f;
            float h = Mathf.Lerp(rise, hills + ridge, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HomeRadius, HomeRadius + 30f, d)));

            // The old road is a flat strip.
            float roadBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 4f, Mathf.Abs(z - RoadZ)));
            h = Mathf.Lerp(h, 0.6f, roadBlend);

            // The creek is carved below water level along a gentle meander.
            float creekCentre = CreekZ + Mathf.Sin(x * 0.02f) * 8f;
            float creekBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 3f, Mathf.Abs(z - creekCentre)));
            h = Mathf.Lerp(Mathf.Min(h, 1.5f + Mathf.Abs(z - creekCentre) * 0.15f), -2f, creekBlend * creekBlend);
            float bankBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 14f, Mathf.Abs(z - creekCentre)));
            return Mathf.Lerp(h, Mathf.Min(h, 1.5f), bankBlend * (1f - creekBlend));
        }

        private static void BuildHomestead(Material wood, Material slot)
        {
            var root = new GameObject("Homestead").transform;
            float y = HeightAt(0f, 0f);
            // Three building slots (M2 step 6 fills them): dwelling north, hearth west, infirmary east.
            (string id, Vector3 at)[] slots =
            {
                ("dwelling", new Vector3(0f, y, 14f)),
                ("hearth", new Vector3(-14f, y, 2f)),
                ("infirmary", new Vector3(14f, y, 2f)),
            };
            foreach (var s in slots)
                SceneBuildKit.Block(PrimitiveType.Cube, "Slot_" + s.id, s.at + Vector3.up * 0.05f, new Vector3(8f, 0.1f, 6f), slot, root);
            // A stand-in cabin on the dwelling slot so home reads as home.
            SceneBuildKit.Block(PrimitiveType.Cube, "Cabin (stand-in)", new Vector3(0f, y + 1.6f, 15f), new Vector3(7f, 3.2f, 5f), wood, root);
            // Clothesline posts, as in the references.
            SceneBuildKit.Block(PrimitiveType.Cylinder, "Clothesline Post", new Vector3(8f, y + 1f, 20f), new Vector3(0.15f, 1f, 0.15f), wood, root);
            SceneBuildKit.Block(PrimitiveType.Cylinder, "Clothesline Post", new Vector3(16f, y + 1f, 20f), new Vector3(0.15f, 1f, 0.15f), wood, root);
        }

        private static void BuildBridge(Material wood, Material steel)
        {
            var root = new GameObject("Guardrail Bridge").transform;
            float zc = CreekZ;
            float deckY = 1.7f;
            SceneBuildKit.Block(PrimitiveType.Cube, "Deck", new Vector3(0f, deckY, zc), new Vector3(3.2f, 0.25f, 26f), wood, root);
            SceneBuildKit.Block(PrimitiveType.Cube, "Guardrail East", new Vector3(1.6f, deckY + 0.6f, zc), new Vector3(0.12f, 0.35f, 24f), steel, root);
            SceneBuildKit.Block(PrimitiveType.Cube, "Guardrail West", new Vector3(-1.6f, deckY + 0.6f, zc), new Vector3(0.12f, 0.35f, 24f), steel, root);
        }

        private static void BuildRoad(Material road, Material concrete)
        {
            var root = new GameObject("Old Highway").transform;
            SceneBuildKit.Block(PrimitiveType.Cube, "Road Surface", new Vector3(0f, 0.62f, RoadZ), new Vector3(Size, 0.05f, 9f), road, root, collider: false);
            // The fallen overpass: two standing pillars and a deck slab broken down onto the road.
            Vector3 o = OverpassAt;
            SceneBuildKit.Block(PrimitiveType.Cube, "Pillar North", o + new Vector3(0f, 4f, 14f), new Vector3(2f, 8f, 2f), concrete, root);
            SceneBuildKit.Block(PrimitiveType.Cube, "Pillar South", o + new Vector3(-6f, 4f, -14f), new Vector3(2f, 8f, 2f), concrete, root);
            SceneBuildKit.Block(PrimitiveType.Cube, "Deck Standing", o + new Vector3(0f, 8.5f, 26f), new Vector3(10f, 1f, 24f), concrete, root);
            SceneBuildKit.Block(PrimitiveType.Cube, "Deck Fallen", o + new Vector3(2f, 4f, 2f), new Vector3(10f, 1f, 26f), concrete, root, rotation: Quaternion.Euler(18f, 0f, 6f));
            SceneBuildKit.Block(PrimitiveType.Cube, "Rubble", o + new Vector3(-4f, 0.9f, -6f), new Vector3(3f, 1.4f, 2.5f), concrete, root, rotation: Quaternion.Euler(0f, 30f, 8f));
        }

        // Greybox trees from a fixed seed (editor-only randomness; not simulation state).
        private static void BuildTrees(Material wood, Material leaves, Material deepLeaves)
        {
            var root = new GameObject("Trees").transform;
            var random = new System.Random(1789);
            int placed = 0;
            for (int attempt = 0; attempt < 4000 && placed < 520; attempt++)
            {
                float x = (float)(random.NextDouble() - 0.5) * (Size - 20f);
                float z = (float)(random.NextDouble() - 0.5) * (Size - 20f);
                bool deep = x < DeepWoodsX;
                bool woods = x < WoodsEdgeX;
                // Woods are dense, the rest is scattered; keep the clearing, creek and road open.
                double chance = deep ? 0.95 : woods ? 0.7 : z > RidgeZ ? 0.35 : 0.06;
                if (random.NextDouble() > chance) continue;
                if (Mathf.Sqrt(x * x + z * z) < HomeRadius + 12f) continue;
                if (Mathf.Abs(z - RoadZ) < 9f || Mathf.Abs(z - (CreekZ + Mathf.Sin(x * 0.02f) * 8f)) < 9f) continue;
                if (Vector3.Distance(new Vector3(x, 0f, z), OverpassAt) < 25f) continue;

                float y = HeightAt(x, z);
                float height = deep ? 9f + (float)random.NextDouble() * 5f : 5f + (float)random.NextDouble() * 4f;
                SceneBuildKit.Block(PrimitiveType.Cylinder, "Trunk", new Vector3(x, y + height / 2f, z), new Vector3(0.5f, height / 2f, 0.5f), wood, root);
                float crown = deep ? 4.5f : 3.2f + (float)random.NextDouble();
                SceneBuildKit.Block(PrimitiveType.Sphere, "Crown", new Vector3(x, y + height, z), Vector3.one * crown * 2f, deep ? deepLeaves : leaves, root, collider: false);
                placed++;
            }
        }

        private static Transform BuildAreas()
        {
            var root = new GameObject("Areas").transform;
            AreaVolume Area(string name, string place, string[] tags, Vector3 centre, Vector3 size, int thin = 0, bool homestead = false)
            {
                var go = new GameObject("Area " + name);
                go.transform.SetParent(root, false);
                go.transform.position = centre;
                var area = go.AddComponent<AreaVolume>();
                area.Configure(place, tags, size, thin, homestead);
                return area;
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

using System;
using System.Collections.Generic;
using Hearthline.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// The style test (ADR 0022): one corner of the homestead built toward the target look instead of greybox.
    /// It has photo-sourced ground and rocks (Rocks FREE pack, wrapped in our own URP materials), leaf-card
    /// oaks, the cabin, the MPFB homesteader, and two moods: golden hour and foggy morning. Generated: change
    /// this builder, then rebuild. Needs the git-ignored Rocks FREE pack in Assets/ThirdParty/RockFREE.
    /// </summary>
    public static class StyleTestSceneBuilder
    {
        public enum Mood { GoldenHour, FoggyMorning }

        private const string Folder = "Assets/Hearthline/Art/StyleTest";
        private const string Rocks = "Assets/ThirdParty/RockFREE";
        private const float Size = 90f;

        [MenuItem("Hearthline/Rebuild style test scenes")]
        private static void Menu() => Debug.Log(BuildBoth());

        public static string BuildBoth() => Build(Mood.GoldenHour) + " | " + Build(Mood.FoggyMorning);

        public static string ScenePath(Mood mood) => "Assets/Scenes/StyleTest_" + mood + ".unity";

        public static string Build(Mood mood)
        {
            if (!AssetDatabase.IsValidFolder(Rocks)) throw new InvalidOperationException("The Rocks FREE pack is not in " + Rocks + " (see docs/SETUP.md).");
            SceneBuildKit.EnsureFolder(Folder);
            GreyboxKit.Setup();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rnd = new Random(22);

            // ---- Light, sky, fog -----------------------------------------------------------------------
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            if (mood == Mood.GoldenHour)
            {
                sun.transform.rotation = Quaternion.Euler(12f, 70f, 0f); // low and from the side: long shadows, lit faces
                sun.color = new Color(1f, 0.74f, 0.5f);
                sun.intensity = 1.9f;
            }
            else
            {
                sun.transform.rotation = Quaternion.Euler(7f, 115f, 0f);
                sun.color = new Color(0.95f, 0.88f, 0.8f);
                sun.intensity = 0.9f;
            }
            new GameObject("Sky Globals").AddComponent<SkyGlobals>().Configure(sun);
            var sky = new Material(Shader.Find("Hearthline/Sky"));
            sky.SetFloat("_CloudCoverage", mood == Mood.GoldenHour ? 0.42f : 0.7f);
            sky.SetFloat("_Exposure", mood == Mood.GoldenHour ? 1.0f : 0.85f);
            SaveAsset(sky, Folder + "/Sky_" + mood + ".mat");
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = mood == Mood.GoldenHour ? new Color(0.55f, 0.6f, 0.72f) : new Color(0.62f, 0.66f, 0.7f);
            RenderSettings.ambientEquatorColor = mood == Mood.GoldenHour ? new Color(0.62f, 0.52f, 0.4f) : new Color(0.55f, 0.57f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = mood == Mood.GoldenHour ? new Color(0.86f, 0.72f, 0.55f) : new Color(0.72f, 0.75f, 0.76f);
            RenderSettings.fogDensity = mood == Mood.GoldenHour ? 0.012f : 0.045f;

            // ---- Ground: grass worn to dirt in the yard and along the path ------------------------------
            var ground = new GameObject("Ground");
            ground.AddComponent<MeshFilter>().sharedMesh = GroundMesh(rnd);
            ground.AddComponent<MeshRenderer>().sharedMaterial = GroundMaterial(mood);
            ground.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            ground.isStatic = true;

            // ---- The homestead corner -------------------------------------------------------------------
            Transform props = new GameObject("Props").transform;
            GreyboxKit.Place("Cabin", new Vector3(0f, 0f, 7f), 180f, 1f, props, collider: true);
            GreyboxKit.Place("Woodpile", new Vector3(-5f, 0f, 6.5f), 90f, 1f, props, collider: true);
            GreyboxKit.Place("Rain_Barrel", new Vector3(3.8f, 0f, 4.6f), 0f, 1f, props, collider: true);
            // Sizes are the rock's longest side in metres (the pack's rocks are cliff-sized at scale 1).
            PlaceRock(1, new Vector3(-7f, -0.15f, -3f), 30f, 2.4f, props);
            PlaceRock(3, new Vector3(8f, -0.2f, -5f), 200f, 3.2f, props);
            PlaceRock(2, new Vector3(5f, -0.1f, -12f), 140f, 1.4f, props);
            PlaceRock(1, new Vector3(-13f, -0.2f, 5f), 110f, 1.6f, props);
            PlaceRock(5, new Vector3(-38f, -1.5f, 40f), 80f, 30f, props);   // a ridge of rock behind the trees
            PlaceRock(6, new Vector3(36f, -2f, 42f), 10f, 16f, props);      // a crag
            foreach (var t in new[] { (-12f, 13f, 1.1f), (12f, 11f, 1.25f), (-20f, -4f, 1.0f), (19f, -8f, 1.15f), (-6f, 26f, 1.3f) })
                GreyboxKit.Place("Tree_Oak", new Vector3(t.Item1, -0.1f, t.Item2), (float)rnd.NextDouble() * 360f, t.Item3, props, collider: false);
            foreach (var t in new[] { (6f, 30f, 1.3f), (17f, 27f, 1.5f), (-19f, 29f, 1.4f), (27f, 17f, 1.2f), (-28f, 15f, 1.3f), (2f, 40f, 1.6f) })
                GreyboxKit.Place("Tree_Oak", new Vector3(t.Item1, Height(t.Item1, t.Item2) - 0.1f, t.Item2), (float)rnd.NextDouble() * 360f, t.Item3, props, collider: false);

            // Ground cover on the grass, not on the worn dirt.
            var cover = new GameObject("Ground Cover").AddComponent<InstancedScatter>();
            Mesh grassCard = GrassCardMesh();
            Material grassMat = CardMaterial("Grass", "grass.tga", new Color(1.35f, 1.45f, 0.95f));
            Material flowerMat = CardMaterial("Flowers", "flower_red.tga", new Color(1f, 0.92f, 0.85f));
            for (float x = -Size / 2f + 2f; x < Size / 2f - 2f; x += 0.55f)
            for (float z = -Size / 2f + 2f; z < Size / 2f - 2f; z += 0.55f)
            {
                float px = x + (float)(rnd.NextDouble() - 0.5) * 0.5f, pz = z + (float)(rnd.NextDouble() - 0.5) * 0.5f;
                float dirt = DirtMask(px, pz);
                if (dirt > 0.35f || rnd.NextDouble() < dirt * 2f) continue;
                bool flower = rnd.NextDouble() < 0.04;
                float s2 = 0.7f + (float)rnd.NextDouble() * 0.6f;
                cover.Add(grassCard, new[] { flower ? flowerMat : grassMat },
                    Matrix4x4.TRS(new Vector3(px, Height(px, pz) - 0.03f, pz), Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f), new Vector3(s2, s2 * (flower ? 0.8f : 1f), s2)), 25f);
            }

            // ---- The homesteader, standing in the yard --------------------------------------------------
            GameObject model = CharacterKit.SetUpHomesteader();
            var person = (GameObject)PrefabUtility.InstantiatePrefab(model);
            person.name = "Homesteader";
            person.transform.SetPositionAndRotation(new Vector3(1.6f, Height(1.6f, 0.5f), 0.5f), Quaternion.Euler(0f, 205f, 0f));
            Animator animator = person.GetComponent<Animator>();
            if (animator == null) animator = person.AddComponent<Animator>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(CharacterKit.HomesteaderModel))
                if (o is Avatar avatar) animator.avatar = avatar;
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorBuilder.Path);
            animator.applyRootMotion = false;

            // ---- Foggy morning: low mist lying in the grass ---------------------------------------------
            if (mood == Mood.FoggyMorning)
            {
                Material mist = MistMaterial();
                for (int i = 0; i < 26; i++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = 10f + (float)rnd.NextDouble() * 30f;
                    var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    card.name = "Mist";
                    Object.DestroyImmediate(card.GetComponent<Collider>());
                    float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d + 6f;
                    card.transform.position = new Vector3(x, Height(x, z) + 0.9f, z);
                    card.transform.rotation = Quaternion.LookRotation(new Vector3(x, 0f, z - 6f) - new Vector3(7f, 0f, -14f)) ;
                    card.transform.localScale = new Vector3(14f + (float)rnd.NextDouble() * 10f, 2.4f, 1f);
                    card.GetComponent<MeshRenderer>().sharedMaterial = mist;
                    card.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            // ---- Camera and grade -----------------------------------------------------------------------
            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.transform.position = new Vector3(6.5f, 1.75f, -9.5f);
            camera.transform.LookAt(new Vector3(0.5f, 1.4f, 3f));
            camera.fieldOfView = 45f;
            camera.farClipPlane = 600f;
            var data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var volume = new GameObject("Post-processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Grade(mood);

            string path = ScenePath(mood);
            EditorSceneManager.SaveScene(scene, path);
            return "Saved " + path + ": " + cover.InstanceCount + " plants";
        }

        // ---- Ground ---------------------------------------------------------------------------------------

        private static float Height(float x, float z) =>
            0.18f * Mathf.PerlinNoise(x * 0.06f + 3f, z * 0.06f + 7f) + 0.08f * Mathf.PerlinNoise(x * 0.2f, z * 0.2f) - 0.12f
            + Mathf.Max(0f, (z - 22f) * 0.06f); // the land rises behind the cabin

        // 0 grass .. 1 bare dirt: the yard in front of the cabin and the path out toward the camera.
        private static float DirtMask(float x, float z)
        {
            float yard = 1f - Mathf.Clamp01((new Vector2(x * 0.8f, z - 2.5f).magnitude - 4.5f) / 3f);
            float pathCentre = Mathf.Sin(z * 0.12f) * 1.8f;
            float path = z < 2f ? 1f - Mathf.Clamp01((Mathf.Abs(x - pathCentre) - 0.9f) / 1.2f) : 0f;
            float wear = Mathf.PerlinNoise(x * 0.35f + 11f, z * 0.35f + 5f) * 0.5f;
            return Mathf.Clamp01(Mathf.Max(yard, path) * (0.75f + wear));
        }

        private static Mesh GroundMesh(Random rnd)
        {
            const int n = 180;
            var verts = new Vector3[(n + 1) * (n + 1)];
            var colors = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = -Size / 2f + Size * i / n, z = -Size / 2f + Size * j / n;
                int k = j * (n + 1) + i;
                verts[k] = new Vector3(x, Height(x, z), z);
                colors[k] = new Color(DirtMask(x, z), 0f, 0f, 1f);
                uvs[k] = new Vector2(x, z);
            }
            var tris = new List<int>();
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
            var mesh = new Mesh { name = "StyleTest_Ground", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            SaveAsset(mesh, Folder + "/StyleTest_Ground.asset");
            return mesh;
        }

        private static Material GroundMaterial(Mood mood)
        {
            string t = Rocks + "/Textures/Terrain_Textures/";
            var m = new Material(Shader.Find("Hearthline/GroundBlend"));
            m.SetTexture("_GrassMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "Ground_Textures/Terr_Grass_Diffuse.psd"));
            m.SetTexture("_GrassNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "Ground_Textures/Terr_Grass_Normal.psd"));
            m.SetTexture("_DirtMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "Terr_Dirt_Diffuse.psd"));
            m.SetTexture("_DirtNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "Terr_Dirt_Normal.psd"));
            // Toward the references: deep, slightly warm greens; dry, reddish Southeastern dirt.
            m.SetColor("_GrassTint", mood == Mood.GoldenHour ? new Color(0.85f, 0.92f, 0.62f) : new Color(0.75f, 0.85f, 0.65f));
            m.SetColor("_DirtTint", new Color(0.95f, 0.82f, 0.7f));
            m.SetFloat("_Tiling", 0.22f);
            return SaveAsset(m, Folder + "/Ground_" + mood + ".mat");
        }

        // ---- Grass cards: three crossed cut-out quads showing the pack's photo grass ----------------------

        private static Mesh GrassCardMesh()
        {
            string path = Folder + "/GrassCard.asset";
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.45f;
                int b = verts.Count;
                verts.AddRange(new[] { -d, d, d + Vector3.up * 0.75f, -d + Vector3.up * 0.75f });
                uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                // Normals lean up so the cards light like the ground they grow from, not like walls.
                Vector3 n = (Vector3.Cross(d, Vector3.up).normalized * 0.3f + Vector3.up).normalized;
                normals.AddRange(new[] { n, n, n, n });
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = "GrassCard" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetNormals(normals); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return SaveAsset(mesh, path);
        }

        private static Material CardMaterial(string name, string texture, Color tint)
        {
            var m = new Material(Shader.Find("Hearthline/Foliage"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Rocks + "/Textures/Terrain_Textures/Grass_Textures/" + texture));
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_AlphaClip", 1f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", 0.45f);
            m.SetFloat("_WindStrength", 0.18f); m.SetFloat("_HeightStart", 0f); m.SetFloat("_Flutter", 0.3f);
            m.SetFloat("_Wrap", 0.6f); m.SetFloat("_Translucency", 0.45f);
            m.enableInstancing = true;
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return SaveAsset(m, Folder + "/" + name + ".mat");
        }

        // ---- Rocks: the pack's meshes with our URP materials from its textures ----------------------------

        private static void PlaceRock(int index, Vector3 at, float yaw, float size, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Rocks + "/LODGroups/rock" + index + "_LOD0.prefab");
            if (prefab == null) return;
            var rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            rock.transform.SetPositionAndRotation(new Vector3(at.x, Height(at.x, at.z) + at.y, at.z), Quaternion.Euler(0f, yaw, 0f));
            var b = new Bounds(rock.transform.position, Vector3.zero);
            foreach (Renderer r in rock.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            float longest = Mathf.Max(b.size.x, b.size.z);
            rock.transform.localScale = Vector3.one * (longest > 0.01f ? size / longest : 1f);
            Material m = RockMaterial(index);
            foreach (Renderer r in rock.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats; // a scene override; the pack itself is untouched
            }
            foreach (MeshFilter mf in rock.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null && mf.name.EndsWith("LOD0")) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        private static Material RockMaterial(int index)
        {
            string path = Folder + "/Rock" + index + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            string t = Rocks + "/Textures/rock" + index;
            m = new Material(SceneBuildKit.DefaultLitMaterial());
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "_Albedo.png"));
            m.SetColor("_BaseColor", new Color(0.92f, 0.9f, 0.85f));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "_Normal.png"));
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "_Occlusion.png"));
            m.EnableKeyword("_OCCLUSIONMAP");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "_Metallic.tga"));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", 0.35f);
            return SaveAsset(m, path);
        }

        // ---- Mist and grade ---------------------------------------------------------------------------------

        private static Material MistMaterial()
        {
            string texPath = Folder + "/mist.png";
            const int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s, v = y / (float)s;
                float edge = Mathf.Sin(Mathf.PI * u) * Mathf.Pow(Mathf.Sin(Mathf.PI * v), 1.5f);
                float n = 0.55f + 0.45f * Mathf.PerlinNoise(u * 5f + 3f, v * 3f + 1f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(edge * n * 0.55f)));
            }
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetColor("_BaseColor", new Color(0.88f, 0.9f, 0.9f, 0.6f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return SaveAsset(m, Folder + "/Mist.mat");
        }

        private static VolumeProfile Grade(Mood mood)
        {
            string path = Folder + "/Grade_" + mood + ".asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var grade = profile.Add<ColorAdjustments>(true);
            var white = profile.Add<WhiteBalance>(true);
            var bloom = profile.Add<Bloom>(true);
            var vignette = profile.Add<Vignette>(true);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            if (mood == Mood.GoldenHour)
            {
                grade.postExposure.Override(0.35f); grade.contrast.Override(14f); grade.saturation.Override(4f);
                white.temperature.Override(14f); white.tint.Override(3f);
                bloom.threshold.Override(0.95f); bloom.intensity.Override(0.6f); bloom.scatter.Override(0.7f);
                smh.shadows.Override(new Vector4(0.9f, 0.95f, 1.1f, 0f)); smh.highlights.Override(new Vector4(1.1f, 1.0f, 0.88f, 0f));
            }
            else
            {
                grade.postExposure.Override(0.45f); grade.contrast.Override(6f); grade.saturation.Override(-14f);
                white.temperature.Override(-6f); white.tint.Override(0f);
                bloom.threshold.Override(0.9f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.8f);
                smh.shadows.Override(new Vector4(0.95f, 1.0f, 1.05f, 0f)); smh.highlights.Override(new Vector4(1f, 1f, 1f, 0f));
            }
            vignette.intensity.Override(0.26f); vignette.smoothness.Override(0.5f);
            foreach (VolumeComponent c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T SaveAsset<T>(T asset, string path) where T : Object
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}

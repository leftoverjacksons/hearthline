using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// The greybox world kit: low-poly models made in Blender (ArtSource/Greybox/region_kit.blend, exported to
    /// Assets/Hearthline/Art/Greybox) and one URP material per palette colour. palette.json, written by the
    /// Blender export, is the single source of the colours; every model's materials are remapped to the shared
    /// HL_* materials so a colour changes everywhere at once. A stand-in style, not the final art (ADR 0014).
    /// </summary>
    public static class GreyboxKit
    {
        public const string Folder = "Assets/Hearthline/Art/Greybox";
        public const string MaterialFolder = Folder + "/Materials";
        public const string LeafCardMaterial = "HL_LeafCard";
        public const string NeedleCardMaterial = "HL_NeedleCard";
        /// <summary>Leaf and needle card materials: HL_LeafCard, its Light and Dark tints, and HL_NeedleCard.</summary>
        public static bool IsCard(string name) => name.StartsWith(LeafCardMaterial) || name == NeedleCardMaterial || name.StartsWith("HL_GrassCard");

        [Serializable] private sealed class Rgb { public float r, g, b; }

        private static Dictionary<string, Material> _materials;

        /// <summary>Creates or updates the HL_* materials and remaps every kit model to them.</summary>
        public static Dictionary<string, Material> Setup()
        {
            SceneBuildKit.EnsureFolder(MaterialFolder);
            _materials = new Dictionary<string, Material>();
            foreach (KeyValuePair<string, Color> entry in ReadPalette())
                _materials[entry.Key] = MakeMaterial(entry.Key, entry.Value);
            AssetDatabase.SaveAssets();

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                bool changed = importer.animationType != ModelImporterAnimationType.None || importer.importAnimation;
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(sub is Material embedded) || !_materials.TryGetValue(embedded.name, out Material ours)) continue;
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), ours);
                    changed = true;
                }
                if (changed) importer.SaveAndReimport();
            }
            return _materials;
        }

        public static Material Mat(string name)
        {
            if (_materials == null) Setup();
            return _materials[name];
        }

        public static GameObject Model(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
            if (model == null) throw new InvalidOperationException("No greybox model " + name + " in " + Folder);
            return model;
        }

        public static Mesh MeshOf(string name) => Model(name).GetComponentInChildren<MeshFilter>().sharedMesh;

        public static Material[] MaterialsOf(string name) => Model(name).GetComponentInChildren<MeshRenderer>().sharedMaterials;

        /// <summary>Places a kit model in the scene as a prefab-linked instance.</summary>
        public static GameObject Place(string name, Vector3 position, float yaw, float scale, Transform parent, bool collider)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Model(name), parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            go.isStatic = true;
            if (collider)
            {
                MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
            return go;
        }

        private static Dictionary<string, Color> ReadPalette()
        {
            string json = File.ReadAllText(Path.Combine(Folder, "palette.json"));
            // { "linear": true, "colors": { "HL_Bark": {"r":..,"g":..,"b":..}, ... } } -- parsed by hand to keep
            // the editor free of extra JSON dependencies.
            var colors = new Dictionary<string, Color>();
            int start = json.IndexOf("\"colors\"", StringComparison.Ordinal);
            int i = json.IndexOf('{', start) + 1;
            while (true)
            {
                int q1 = json.IndexOf('"', i);
                if (q1 < 0) break;
                int q2 = json.IndexOf('"', q1 + 1);
                string key = json.Substring(q1 + 1, q2 - q1 - 1);
                int open = json.IndexOf('{', q2), close = json.IndexOf('}', open);
                Rgb rgb = JsonUtility.FromJson<Rgb>(json.Substring(open, close - open + 1));
                // Blender colours are linear; Unity's Color API takes sRGB and converts in a linear project.
                colors[key] = new Color(rgb.r, rgb.g, rgb.b).gamma;
                i = close + 1;
                int next = json.IndexOf('"', i), end = json.IndexOf('}', i);
                if (next < 0 || end < next) break;
            }
            return colors;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(SceneBuildKit.DefaultLitMaterial()) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.color = color;
            bool metal = name == "HL_Steel" || name == "HL_Tin";
            material.SetFloat("_Smoothness", metal ? 0.35f : name == "HL_BarrelBlue" ? 0.3f : 0.04f); // painted, matte (ADR 0029)
            material.SetFloat("_Metallic", metal ? 0.6f : 0f);
            material.enableInstancing = true;
            if (name == "HL_WindowGlow" || name == "HL_Fire")
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.2f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            // Thin leaves and grass blades are single quads; draw both sides.
            bool thin = name.StartsWith("HL_Grass") || name.StartsWith("HL_Flower") || name.StartsWith("HL_Cloth") || name == "HL_LeavesLight" || name == "HL_TentCanvas";
            material.SetFloat("_Cull", thin ? 0f : 2f);

            // Surface detail (ADR 0018): a near-white tiling map multiplies the palette colour, plus relief.
            (string surface, float tiling) = SurfaceOf(name);
            Texture2D albedo = surface == null ? null : TextureForge.Albedo(surface);
            Texture2D normal = surface == null ? null : TextureForge.Normal(surface);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetTextureScale("_BaseMap", Vector2.one * tiling);
            material.SetFloat("_BumpScale", 0.7f);
            if (normal != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
            ApplyWind(material, name);
            if (IsCard(name))
            {
                // Leaf cards (ADR 0020, 0027): a cut-out spray of leaves or needles per quad, crown shading baked
                // into the mesh.
                albedo = name == NeedleCardMaterial ? TextureForge.NeedleCluster() : name.StartsWith("HL_GrassCard") ? TextureForge.GrassBlades() : TextureForge.LeafCluster();
                material.SetTexture("_BaseMap", albedo);
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetFloat("_Cull", 0f);
                material.SetFloat("_AlphaClip", 1f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cutoff", TextureForge.LeafCutoff);
                material.SetFloat("_VertexAO", 1f);
                material.SetFloat("_AOStrength", 0.55f);
                material.SetFloat("_ShadowStrength", 0.55f);
                material.EnableKeyword("_VERTEX_AO");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            if (albedo != null)
            {
                // The detail maps average about 0.8; lift the colour so the palette's brightness holds.
                Color lifted = new Color(Mathf.Min(1f, color.r * 1.22f), Mathf.Min(1f, color.g * 1.22f), Mathf.Min(1f, color.b * 1.22f));
                material.SetColor("_BaseColor", lifted);
                material.color = lifted;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        // Leaves, grass and flowers use the swaying foliage shader; everything else stays URP Lit. The
        // values are starting points for tuning by eye on each material.
        private static void ApplyWind(Material material, string name)
        {
            (float strength, float start, float flutter) wind;
            switch (name)
            {
                case "HL_Grass": case "HL_GrassDry": case "HL_Flower": case "HL_FlowerPink": wind = (0.22f, 0f, 0.35f); break;
                case "HL_Leaves": case "HL_LeavesLight": case "HL_LeavesDark": case "HL_Berry": wind = (0.03f, 0.3f, 0.25f); break;
                case "HL_Pine": wind = (0.02f, 1f, 0.1f); break;
                case LeafCardMaterial: case "HL_LeafCardLight": case "HL_LeafCardDark": wind = (0.018f, 2.5f, 0.3f); break;
                case NeedleCardMaterial: wind = (0.012f, 3f, 0.15f); break;
                case "HL_GrassCard": case "HL_GrassCardDry": wind = (0.2f, 0f, 0.3f); break;
                case "HL_Kudzu": wind = (0.025f, 0.2f, 0.3f); break;
                default:
                    if (material.shader.name == "Hearthline/Foliage") material.shader = SceneBuildKit.DefaultLitMaterial().shader;
                    return;
            }
            Shader foliage = Shader.Find("Hearthline/Foliage");
            if (foliage == null) return;
            if (material.shader != foliage) material.shader = foliage;
            material.SetFloat("_WindStrength", wind.strength);
            material.SetFloat("_HeightStart", wind.start);
            material.SetFloat("_Flutter", wind.flutter);
            material.SetFloat("_Wrap", 0.65f);
            // Light through the leaves is what makes a sunlit canopy glow (ADR 0027, the Valheim reference).
            material.SetFloat("_Translucency", IsCard(name) ? 0.6f : 0.35f);
        }

        // Which generated surface a palette material wears, and how many times it repeats per metre.
        private static (string surface, float tiling) SurfaceOf(string name)
        {
            switch (name)
            {
                case "HL_Bark": case "HL_BarkLight": return ("bark", 1f);
                case "HL_WoodWeathered": case "HL_WoodDark": return ("planks", 0.55f);
                case "HL_Stone": return ("stone", 0.8f);
                case "HL_Rock": case "HL_RockMoss": return ("rock", 0.6f);
                case "HL_Concrete": case "HL_Asphalt": return ("concrete", 0.35f);
                case "HL_TinRust": case "HL_Tin": return ("tin", 0.6f);
                case "HL_Steel": case "HL_Rebar": case "HL_Rubber": return ("rust", 1f);
                case "HL_Canvas": case "HL_CanvasTan": case "HL_TentCanvas": case "HL_Blanket": case "HL_Sheet": case "HL_Grain":
                case "HL_ClothWhite": case "HL_ClothRed": case "HL_ClothBlue": return ("canvas", 2f);
                case "HL_Leaves": case "HL_LeavesLight": case "HL_LeavesDark": case "HL_Pine": case "HL_Kudzu": return ("leaves", 0.5f);
                case "HL_Soil": return ("soil", 1f);
                default: return (null, 1f);
            }
        }
    }
}

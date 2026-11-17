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
            material.SetFloat("_Smoothness", metal ? 0.55f : name == "HL_BarrelBlue" ? 0.4f : 0.12f);
            material.SetFloat("_Metallic", metal ? 0.6f : 0f);
            material.enableInstancing = true;
            if (name == "HL_WindowGlow")
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.2f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            // Thin leaves and grass blades are single quads; draw both sides.
            bool thin = name.StartsWith("HL_Grass") || name.StartsWith("HL_Flower") || name.StartsWith("HL_Cloth") || name == "HL_LeavesLight";
            material.SetFloat("_Cull", thin ? 0f : 2f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}

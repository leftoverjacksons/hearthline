using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Sets up characters exported from Blender with MPFB's Mixamo-named rig (style test, ADR 0022): imports
    /// them as Humanoid with an explicit bone map (Unity's automatic mapping misses this rig's feet), and
    /// builds URP materials from the textures baked next to the FBX. Generated: change this, then re-run.
    /// </summary>
    public static class CharacterKit
    {
        public const string HomesteaderFolder = "Assets/Hearthline/Art/Characters/Homesteader";
        public const string HomesteaderModel = HomesteaderFolder + "/Homesteader.fbx";
        public const string CharactersFolder = "Assets/Hearthline/Art/Characters";

        /// <summary>A character exported by ArtSource/Characters/export_tripo_character.py: Name/Name.fbx with
        /// Name_BaseColor, Name_Normal and Name_Mask (R metallic, A smoothness) beside it.</summary>
        public static string TripoModel(string name) => CharactersFolder + "/" + name + "/" + name + ".fbx";

        // Unity human bone -> Mixamo bone name (without the "mixamorig:" prefix).
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            { "Hips", "Hips" }, { "Spine", "Spine" }, { "Chest", "Spine1" }, { "UpperChest", "Spine2" },
            { "Neck", "Neck" }, { "Head", "Head" },
            { "LeftShoulder", "LeftShoulder" }, { "LeftUpperArm", "LeftArm" }, { "LeftLowerArm", "LeftForeArm" }, { "LeftHand", "LeftHand" },
            { "RightShoulder", "RightShoulder" }, { "RightUpperArm", "RightArm" }, { "RightLowerArm", "RightForeArm" }, { "RightHand", "RightHand" },
            { "LeftUpperLeg", "LeftUpLeg" }, { "LeftLowerLeg", "LeftLeg" }, { "LeftFoot", "LeftFoot" }, { "LeftToes", "LeftToeBase" },
            { "RightUpperLeg", "RightUpLeg" }, { "RightLowerLeg", "RightLeg" }, { "RightFoot", "RightFoot" }, { "RightToes", "RightToeBase" },
            { "Left Thumb Proximal", "LeftHandThumb1" }, { "Left Thumb Intermediate", "LeftHandThumb2" }, { "Left Thumb Distal", "LeftHandThumb3" },
            { "Left Index Proximal", "LeftHandIndex1" }, { "Left Index Intermediate", "LeftHandIndex2" }, { "Left Index Distal", "LeftHandIndex3" },
            { "Left Middle Proximal", "LeftHandMiddle1" }, { "Left Middle Intermediate", "LeftHandMiddle2" }, { "Left Middle Distal", "LeftHandMiddle3" },
            { "Left Ring Proximal", "LeftHandRing1" }, { "Left Ring Intermediate", "LeftHandRing2" }, { "Left Ring Distal", "LeftHandRing3" },
            { "Left Little Proximal", "LeftHandPinky1" }, { "Left Little Intermediate", "LeftHandPinky2" }, { "Left Little Distal", "LeftHandPinky3" },
            { "Right Thumb Proximal", "RightHandThumb1" }, { "Right Thumb Intermediate", "RightHandThumb2" }, { "Right Thumb Distal", "RightHandThumb3" },
            { "Right Index Proximal", "RightHandIndex1" }, { "Right Index Intermediate", "RightHandIndex2" }, { "Right Index Distal", "RightHandIndex3" },
            { "Right Middle Proximal", "RightHandMiddle1" }, { "Right Middle Intermediate", "RightHandMiddle2" }, { "Right Middle Distal", "RightHandMiddle3" },
            { "Right Ring Proximal", "RightHandRing1" }, { "Right Ring Intermediate", "RightHandRing2" }, { "Right Ring Distal", "RightHandRing3" },
            { "Right Little Proximal", "RightHandPinky1" }, { "Right Little Intermediate", "RightHandPinky2" }, { "Right Little Distal", "RightHandPinky3" },
        };

        // FBX material name -> (albedo texture, normal texture or null, smoothness, cut-out).
        private static readonly Dictionary<string, (string albedo, string normal, float smooth, bool cutout)> Looks =
            new Dictionary<string, (string, string, float, bool)>
            {
                { "Homesteader.body", ("Homesteader_Skin", null, 0.38f, false) },
                { "Homesteader.ears", ("Homesteader_Skin", null, 0.38f, false) },
                { "Homesteader.lips", ("Homesteader_Skin", null, 0.45f, false) },
                { "Homesteader.fingernails", ("Homesteader_Skin", null, 0.5f, false) },
                { "Homesteader.high-poly", ("Homesteader_Eyes", null, 0.55f, false) },
                { "Homesteader.male_casualsuit05", ("Homesteader_Jacket", "Homesteader_Suit_Normal", 0.18f, false) },
                { "Homesteader.trousers", ("Homesteader_Trousers", "Homesteader_Suit_Normal", 0.15f, false) },
                { "Homesteader.shoes03", ("Homesteader_Boots", null, 0.32f, false) },
                { "Homesteader.short02", ("Homesteader_Hair", null, 0.25f, true) },
                { "Homesteader.eyebrow002", ("Homesteader_Eyebrows", null, 0.2f, true) },
            };

        /// <summary>Imports a Tripo character as Humanoid with one URP Lit material from its textures. Null if it isn't there.</summary>
        public static GameObject SetUpTripo(string name)
        {
            string path = TripoModel(name);
            if (AssetImporter.GetAtPath(path) as ModelImporter == null) return null;
            // Textures and material first: reimporting them refreshes the asset database, so the model's
            // importer is fetched after, or its settings are lost.
            string folder = CharactersFolder + "/" + name;
            string matPath = folder + "/" + name + "_Body.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null)
            {
                m = new Material(SceneBuildKit.DefaultLitMaterial());
                AssetDatabase.CreateAsset(m, matPath);
            }
            m.SetTexture("_BaseMap", TripoTexture(folder + "/" + name + "_BaseColor.png", TextureKind.Colour));
            m.SetColor("_BaseColor", Color.white);
            Texture2D normal = TripoTexture(folder + "/" + name + "_Normal.png", TextureKind.Normal);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            Texture2D mask = TripoTexture(folder + "/" + name + "_Mask.png", TextureKind.Data);
            if (mask != null)
            {
                m.SetTexture("_MetallicGlossMap", mask);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 1f); // scales the mask's smoothness
                m.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            else m.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();

            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            Humanoid(importer, AssetDatabase.LoadAssetAtPath<GameObject>(path));
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (o is Material embedded) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), m);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name + "_Body"), m);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private enum TextureKind { Colour, Normal, Data }

        private static Texture2D TripoTexture(string path, TextureKind kind)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool changed = false;
            if (kind == TextureKind.Normal && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; changed = true; }
            if (kind == TextureKind.Data && importer.sRGBTexture) { importer.sRGBTexture = false; changed = true; }
            if (importer.maxTextureSize != 4096) { importer.maxTextureSize = 4096; changed = true; }
            if (changed) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Humanoid with an explicit Mixamo bone map, and the skeleton as it rests in the file (a T-pose).</summary>
        private static void Humanoid(ModelImporter importer, GameObject model)
        {

            // Humanoid with an explicit bone map, and the skeleton as it rests in the file.
            var human = new List<HumanBone>();
            var skeleton = new List<SkeletonBone>();
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                byName[t.name] = t;
                skeleton.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            }
            foreach (var kv in Map)
                if (byName.ContainsKey("mixamorig:" + kv.Value))
                    human.Add(new HumanBone { humanName = kv.Key, boneName = "mixamorig:" + kv.Value, limit = new HumanLimit { useDefaultValues = true } });
            HumanDescription description = importer.humanDescription;
            description.human = human.ToArray();
            description.skeleton = skeleton.ToArray();
            importer.humanDescription = description;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
        }

        public static GameObject SetUpHomesteader()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(HomesteaderModel);
            Humanoid(importer, AssetDatabase.LoadAssetAtPath<GameObject>(HomesteaderModel));

            // Materials: URP Lit from the baked textures, remapped by name.
            string matFolder = HomesteaderFolder + "/Materials";
            SceneBuildKit.EnsureFolder(matFolder);
            foreach (var kv in Looks)
            {
                Material m = MakeMaterial(matFolder, kv.Key, kv.Value.albedo, kv.Value.normal, kv.Value.smooth, kv.Value.cutout);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), m);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(HomesteaderModel);
        }

        public static bool AvatarIsValid() => HumanAvatar(HomesteaderModel) != null;

        /// <summary>The model's valid humanoid avatar, or null.</summary>
        public static Avatar HumanAvatar(string modelPath)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (o is Avatar a && a.isValid && a.isHuman) return a;
            return null;
        }

        private static Texture2D Tex(string name, bool normal)
        {
            string path = HomesteaderFolder + "/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                bool changed = false;
                if (normal && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; changed = true; }
                if (!normal && !importer.alphaIsTransparency) { importer.alphaIsTransparency = true; importer.mipMapsPreserveCoverage = true; importer.alphaTestReferenceValue = 0.4f; changed = true; }
                if (changed) importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material MakeMaterial(string folder, string name, string albedo, string normal, float smooth, bool cutout)
        {
            string path = folder + "/" + name.Replace("Homesteader.", "HS_") + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(SceneBuildKit.DefaultLitMaterial());
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", Tex(albedo, false));
            m.SetColor("_BaseColor", name.EndsWith("high-poly") ? new Color(0.62f, 0.6f, 0.58f) : Color.white); // eyes: no glow
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", Tex(normal, true));
                m.EnableKeyword("_NORMALMAP");
            }
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cutoff", 0.4f);
                m.SetFloat("_Cull", 0f);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}

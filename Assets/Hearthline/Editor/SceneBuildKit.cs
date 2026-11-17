using System;
using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Shared pieces for the scene builders: the player rig (Starter Assets armature, marks, hit zones,
    /// cameras, mirror, cheat panel), ground meshes, materials and build settings. Scenes are generated, not
    /// hand-edited: change a builder and rebuild.
    /// </summary>
    public static class SceneBuildKit
    {
        private const string PrefabFolder = "Assets/ThirdParty/StarterAssets/Runtime/ThirdPersonController/Prefabs/";
        public const string GeneratedFolder = "Assets/Scenes/Generated";

        // Layer 8 is taken: the Starter Assets player prefabs sit on it (as "Player"), so it is named that and
        // left colliding normally. Hit zones go on the next free layer.
        private const int StarterAssetsPlayerLayer = 8;

        // Where each mark region sits: a humanoid bone, an optional second bone to sit halfway towards,
        // and a world-space nudge (the character faces +Z when built) so markers sit on the skin.
        private static readonly (BodyRegion region, HumanBodyBones bone, HumanBodyBones towards, Vector3 nudge)[] Anchors =
        {
            (BodyRegion.Head, HumanBodyBones.Head, HumanBodyBones.LastBone, new Vector3(0f, 0.2f, 0f)),
            (BodyRegion.Face, HumanBodyBones.Head, HumanBodyBones.LastBone, new Vector3(0f, 0.09f, 0.11f)),
            (BodyRegion.Torso, HumanBodyBones.Chest, HumanBodyBones.LastBone, new Vector3(0f, 0f, 0.14f)),
            (BodyRegion.ArmL, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(0f, 0f, 0.06f)),
            (BodyRegion.ArmR, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(0f, 0f, 0.06f)),
            (BodyRegion.HandL, HumanBodyBones.LeftHand, HumanBodyBones.LastBone, new Vector3(0f, 0f, 0.04f)),
            (BodyRegion.HandR, HumanBodyBones.RightHand, HumanBodyBones.LastBone, new Vector3(0f, 0f, 0.04f)),
            (BodyRegion.LegL, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, new Vector3(0f, 0f, 0.1f)),
            (BodyRegion.LegR, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, new Vector3(0f, 0f, 0.1f)),
        };

        /// <summary>What <see cref="BuildPlayerRig"/> made.</summary>
        public sealed class PlayerRig
        {
            public GameObject Player;
            public CheatPanel CheatPanel;
            public MirrorView Mirror;
            public HitReceiver Receiver;
            public PlayerInputRelease InputRelease;
            public int HitZones;
        }

        public static Light CreateSun()
        {
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return sun;
        }

        /// <summary>
        /// SimHost, cheat panel, the player (marks, hit zones, input release), debug hitter, follow camera and
        /// mirror. The player stands at <paramref name="position"/>, built facing +Z and then turned by yaw.
        /// </summary>
        public static PlayerRig BuildPlayerRig(Vector3 position, float yaw = 0f)
        {
            var rig = new PlayerRig();
            new GameObject("SimHost", typeof(SimHost));
            rig.CheatPanel = new GameObject("CheatPanel", typeof(CheatPanel)).GetComponent<CheatPanel>();

            GameObject player = Spawn("PlayerArmature");
            player.name = "Player";
            player.transform.position = Vector3.zero; // anchors and zones are placed facing +Z at the origin
            rig.Player = player;
            rig.InputRelease = player.AddComponent<PlayerInputRelease>();
            AddMarksView(player);
            int hitboxLayer = EnsureHitboxLayer();
            rig.Receiver = player.AddComponent<HitReceiver>();
            rig.HitZones = AddHitZones(player, hitboxLayer);
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var hitter = new GameObject("DebugHitter").AddComponent<DebugHitter>();
            SetRef(hitter, "target", rig.Receiver);

            Spawn("MainCamera");
            GameObject followCamera = Spawn("PlayerFollowCamera");
            followCamera.GetComponent<CinemachineCamera>().Follow = FindChild(player.transform, "PlayerCameraRoot");

            // The mirror: its own Cinemachine camera, above the follow camera's priority, switched on only
            // while the mirror is open. MirrorView moves it; the brain blends to it and back.
            var mirrorCamera = new GameObject("MirrorCamera").AddComponent<CinemachineCamera>();
            mirrorCamera.Priority = 100;
            mirrorCamera.Lens = LensSettings.Default;
            mirrorCamera.Lens.FieldOfView = 35f;
            rig.Mirror = new GameObject("Mirror").AddComponent<MirrorView>();
            SetRef(rig.Mirror, "character", player.GetComponent<CharacterMarksView>());
            SetRef(rig.Mirror, "mirrorCamera", mirrorCamera);
            mirrorCamera.gameObject.SetActive(false);
            return rig;
        }

        /// <summary>Sets a serialized object reference on a component (private [SerializeField]s included).</summary>
        public static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) throw new InvalidOperationException(target.GetType().Name + " has no field " + field);
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void AddToBuild(string scenePath, bool first)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            var entry = new EditorBuildSettingsScene(scenePath, true);
            if (first) scenes.Insert(0, entry); else scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---- Ground and materials ------------------------------------------------------------------

        /// <summary>
        /// A square ground mesh centred on the origin, heights from <paramref name="height"/>(x, z), with a
        /// collider. A plain mesh rather than a Unity Terrain: the project does not include the terrain
        /// modules, and without them Terrain and TerrainCollider are stripped in Play Mode (ADR 0010).
        /// </summary>
        public static GameObject BuildGround(string name, string assetPath, float size, int cells, Func<float, float, float> height, Material material)
        {
            int res = cells + 1;
            var vertices = new Vector3[res * res];
            var uvs = new Vector2[res * res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = (x / (float)cells - 0.5f) * size;
                float wz = (z / (float)cells - 0.5f) * size;
                vertices[z * res + x] = new Vector3(wx, height(wx, wz), wz);
                uvs[z * res + x] = new Vector2(wx, wz) * 0.25f;
            }
            var triangles = new int[cells * cells * 6];
            int t = 0;
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                int i = z * res + x;
                triangles[t++] = i; triangles[t++] = i + res; triangles[t++] = i + 1;
                triangles[t++] = i + 1; triangles[t++] = i + res; triangles[t++] = i + res + 1;
            }
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(mesh, assetPath);

            var ground = new GameObject(name);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = material != null ? material : DefaultLitMaterial();
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            ground.isStatic = true;
            return ground;
        }

        /// <summary>The render pipeline's default lit material, the same one primitives get.</summary>
        public static Material DefaultLitMaterial()
        {
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Material material = probe.GetComponent<MeshRenderer>().sharedMaterial;
            Object.DestroyImmediate(probe);
            return material;
        }

        /// <summary>A flat-coloured copy of the default lit material, saved under Scenes/Generated.</summary>
        public static Material ColorMaterial(string name, Color color)
        {
            EnsureFolder(GeneratedFolder);
            string path = GeneratedFolder + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            var material = new Material(DefaultLitMaterial()) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.color = color;
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        /// <summary>A primitive with a material, optionally without its collider.</summary>
        public static GameObject Block(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent = null, bool collider = true, Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        // ---- Player rig pieces -----------------------------------------------------------------------

        private static GameObject Spawn(string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + prefabName + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing Starter Assets prefab " + prefabName + " (see docs/SETUP.md)");
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        public static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            throw new InvalidOperationException("No child named " + name + " under " + root.name);
        }

        private static void AddMarksView(GameObject player)
        {
            Animator animator = player.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) throw new InvalidOperationException("The player has no humanoid Animator.");

            var view = player.AddComponent<CharacterMarksView>();
            var serialized = new SerializedObject(view);
            SerializedProperty anchors = serialized.FindProperty("anchors");
            anchors.arraySize = Anchors.Length;
            for (int i = 0; i < Anchors.Length; i++)
            {
                var a = Anchors[i];
                Transform bone = animator.GetBoneTransform(a.bone);
                Vector3 position = bone.position;
                if (a.towards != HumanBodyBones.LastBone) position = Vector3.Lerp(position, animator.GetBoneTransform(a.towards).position, 0.5f);

                var anchor = new GameObject("MarkAnchor_" + a.region).transform;
                anchor.SetParent(bone, false);
                anchor.SetPositionAndRotation(position + a.nudge, Quaternion.identity);

                SerializedProperty element = anchors.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("region").enumValueIndex = (int)a.region;
                element.FindPropertyRelative("anchor").objectReferenceValue = anchor;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Puts "Hitbox" on the first free user layer (or finds it) and stops it colliding with anything:
        // hit zones are only for raycasts, never for movement or physics contacts.
        private static int EnsureHitboxLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty playerSlot = layers.GetArrayElementAtIndex(StarterAssetsPlayerLayer);
            if (string.IsNullOrEmpty(playerSlot.stringValue) || playerSlot.stringValue == HitReceiver.LayerName)
            {
                playerSlot.stringValue = "Player";
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }
            for (int other = 0; other < 32; other++) Physics.IgnoreLayerCollision(StarterAssetsPlayerLayer, other, false);

            int layer = LayerMask.NameToLayer(HitReceiver.LayerName);
            if (layer < 0)
            {
                for (int i = StarterAssetsPlayerLayer + 1; i < 32 && layer < 0; i++)
                {
                    SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.stringValue))
                    {
                        slot.stringValue = HitReceiver.LayerName;
                        layer = i;
                    }
                }
                if (layer < 0) throw new InvalidOperationException("No free user layer for " + HitReceiver.LayerName);
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }
            for (int other = 0; other < 32; other++) Physics.IgnoreLayerCollision(layer, other, true);
            return layer;
        }

        // Trigger zones on the humanoid bones (ADR 0011). Capsules run from one bone towards the next, so each
        // zone follows its bone when the body animates. The face is a thin box in front of the head.
        private static int AddHitZones(GameObject player, int layer)
        {
            Animator a = player.GetComponent<Animator>();
            Transform B(HumanBodyBones bone) => a.GetBoneTransform(bone);
            int count = 0;

            void Capsule(BodyRegion region, HumanBodyBones from, Vector3 to, float radius)
            {
                Transform bone = B(from);
                Vector3 start = bone.position;
                var capsule = NewZone<CapsuleCollider>(region, bone, layer, (start + to) / 2f, Quaternion.FromToRotation(Vector3.up, to - start));
                capsule.isTrigger = true;
                capsule.direction = 1;
                capsule.radius = radius;
                capsule.height = Vector3.Distance(start, to) + radius * 2f;
                count++;
            }

            Transform head = B(HumanBodyBones.Head);
            var skull = NewZone<SphereCollider>(BodyRegion.Head, head, layer, head.position + new Vector3(0f, 0.1f, -0.01f), Quaternion.identity);
            skull.isTrigger = true;
            skull.radius = 0.12f;
            count++;
            var face = NewZone<BoxCollider>(BodyRegion.Face, head, layer, head.position + new Vector3(0f, 0.08f, 0.1f), Quaternion.identity);
            face.isTrigger = true;
            face.size = new Vector3(0.17f, 0.2f, 0.06f);
            count++;

            Capsule(BodyRegion.Torso, HumanBodyBones.Hips, B(HumanBodyBones.Chest).position, 0.15f);
            Capsule(BodyRegion.Torso, HumanBodyBones.Chest, B(HumanBodyBones.Neck).position, 0.16f);

            foreach (bool left in new[] { true, false })
            {
                BodyRegion arm = left ? BodyRegion.ArmL : BodyRegion.ArmR;
                BodyRegion hand = left ? BodyRegion.HandL : BodyRegion.HandR;
                BodyRegion leg = left ? BodyRegion.LegL : BodyRegion.LegR;
                HumanBodyBones upperArm = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
                HumanBodyBones lowerArm = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
                HumanBodyBones handBone = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                HumanBodyBones finger = left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal;
                HumanBodyBones upperLeg = left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
                HumanBodyBones lowerLeg = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
                HumanBodyBones foot = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                HumanBodyBones toes = left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;

                Capsule(arm, upperArm, B(lowerArm).position, 0.06f);
                Capsule(arm, lowerArm, B(handBone).position, 0.05f);
                Transform fingerBone = B(finger);
                Vector3 handEnd = fingerBone != null ? fingerBone.position : B(handBone).position + (B(handBone).position - B(lowerArm).position).normalized * 0.08f;
                Capsule(hand, handBone, handEnd, 0.05f);
                Capsule(leg, upperLeg, B(lowerLeg).position, 0.09f);
                Capsule(leg, lowerLeg, B(foot).position, 0.07f);
                Transform toeBone = B(toes);
                if (toeBone != null) Capsule(leg, foot, toeBone.position, 0.06f);
            }
            return count;
        }

        private static T NewZone<T>(BodyRegion region, Transform bone, int layer, Vector3 position, Quaternion rotation) where T : Collider
        {
            var zone = new GameObject("HitZone_" + region);
            zone.layer = layer;
            zone.transform.SetParent(bone, false);
            zone.transform.SetPositionAndRotation(position, rotation);
            T collider = zone.AddComponent<T>(); // before HitRegion, which requires a collider
            zone.AddComponent<HitRegion>().Configure(region);
            return collider;
        }
    }
}

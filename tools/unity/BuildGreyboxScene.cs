// Builds Assets/Scenes/M0_Greybox.unity from scratch. Lives outside Assets/ so it is not part of any
// assembly; run it in the open Editor with:
//   unity command script run --file tools/unity/BuildGreyboxScene.cs --entry BuildGreyboxScene.Main
// Re-running it overwrites the scene and its terrain, so make scene changes here rather than by hand.
//
// The player is the Starter Assets third-person armature (Assets/ThirdParty/StarterAssets, untouched).
// Everything Hearthline adds to it lives on the scene instance, not in the prefab.
using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildGreyboxScene
{
    public const string ScenePath = "Assets/Scenes/M0_Greybox.unity";
    private const string TerrainDataPath = "Assets/Scenes/M0_Greybox_Ground.asset";
    private const string PrefabFolder = "Assets/ThirdParty/StarterAssets/Runtime/ThirdPersonController/Prefabs/";

    private const int TerrainSize = 200;
    private const float TerrainHeight = 12f;
    private const float FlatRadius = 12f; // flat ground around the spawn point

    // Where each mark region sits: a humanoid bone, an optional second bone to sit halfway towards,
    // and a world-space nudge (the character faces +Z when the scene is built) so markers sit on the skin.
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

    public static string Main()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        BuildTerrain();

        new GameObject("SimHost", typeof(SimHost));
        CheatPanel panel = new GameObject("CheatPanel", typeof(CheatPanel)).GetComponent<CheatPanel>();

        GameObject player = Spawn("PlayerArmature");
        player.name = "Player";
        player.transform.position = Vector3.zero;
        player.AddComponent<CheatPanelInputRelease>();
        var release = new SerializedObject(player.GetComponent<CheatPanelInputRelease>());
        release.FindProperty("panel").objectReferenceValue = panel;
        release.ApplyModifiedPropertiesWithoutUndo();
        AddMarksView(player);

        Spawn("MainCamera");
        GameObject followCamera = Spawn("PlayerFollowCamera");
        Transform cameraRoot = FindChild(player.transform, "PlayerCameraRoot");
        followCamera.GetComponent<CinemachineCamera>().Follow = cameraRoot;

        EditorSceneManager.SaveScene(scene, ScenePath);

        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!buildScenes.Exists(s => s.path == ScenePath))
        {
            buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }
        AssetDatabase.SaveAssets();
        return "Saved " + ScenePath + ": Starter Assets player, follow camera, " + TerrainSize + " m terrain, " + Anchors.Length + " mark anchors on bones.";
    }

    private static GameObject Spawn(string prefabName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + prefabName + ".prefab");
        if (prefab == null) throw new System.InvalidOperationException("Missing Starter Assets prefab " + prefabName);
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    private static void AddMarksView(GameObject player)
    {
        Animator animator = player.GetComponent<Animator>();
        if (animator == null || !animator.isHuman) throw new System.InvalidOperationException("The player has no humanoid Animator.");

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

    // Gentle rolling hills from fixed Perlin noise, flat around the spawn point at the centre.
    // A plain mesh rather than a Unity Terrain: the project does not include the terrain modules, and
    // without them Terrain and TerrainCollider are stripped in Play Mode.
    private static void BuildTerrain()
    {
        const int cells = 128;
        int res = cells + 1;
        var vertices = new Vector3[res * res];
        var uvs = new Vector2[res * res];
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            float wx = (x / (float)cells - 0.5f) * TerrainSize;
            float wz = (z / (float)cells - 0.5f) * TerrainSize;
            float noise = Mathf.PerlinNoise(wx * 0.02f + 17.3f, wz * 0.02f + 4.1f) * 0.75f
                        + Mathf.PerlinNoise(wx * 0.07f + 3.7f, wz * 0.07f + 11.9f) * 0.25f;
            float distance = Mathf.Sqrt(wx * wx + wz * wz);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FlatRadius, FlatRadius * 2.5f, distance));
            vertices[z * res + x] = new Vector3(wx, noise * rise * TerrainHeight, wz);
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
        var mesh = new Mesh { name = "M0_Greybox_Ground", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        AssetDatabase.DeleteAsset(TerrainDataPath);
        AssetDatabase.CreateAsset(mesh, TerrainDataPath);

        var ground = new GameObject("Greybox Terrain");
        ground.AddComponent<MeshFilter>().sharedMesh = mesh;
        ground.AddComponent<MeshRenderer>().sharedMaterial = DefaultLitMaterial();
        ground.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // The render pipeline's default lit material, the same one primitives get.
    private static Material DefaultLitMaterial()
    {
        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Material material = probe.GetComponent<MeshRenderer>().sharedMaterial;
        Object.DestroyImmediate(probe);
        return material;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        throw new System.InvalidOperationException("No child named " + name + " under " + root.name);
    }
}

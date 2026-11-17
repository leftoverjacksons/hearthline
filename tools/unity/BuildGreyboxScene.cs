// Builds Assets/Scenes/M0_Greybox.unity from scratch. Lives outside Assets/ so it is not part of any
// assembly; run it in the open Editor with:
//   unity command script run --file tools/unity/BuildGreyboxScene.cs --entry BuildGreyboxScene.Main
// Re-running it overwrites the scene, so make scene changes here rather than by hand.
using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildGreyboxScene
{
    public const string ScenePath = "Assets/Scenes/M0_Greybox.unity";

    // Anchor positions on a 2 m capsule (radius 0.5) standing at the origin and facing +Z.
    private static readonly (BodyRegion region, Vector3 position)[] Anchors =
    {
        (BodyRegion.Head, new Vector3(0f, 1.95f, 0.15f)),
        (BodyRegion.Face, new Vector3(0f, 1.6f, 0.5f)),
        (BodyRegion.Torso, new Vector3(0f, 1.15f, 0.5f)),
        (BodyRegion.ArmL, new Vector3(-0.5f, 1.35f, 0.1f)),
        (BodyRegion.ArmR, new Vector3(0.5f, 1.35f, 0.1f)),
        (BodyRegion.HandL, new Vector3(-0.5f, 0.9f, 0.1f)),
        (BodyRegion.HandR, new Vector3(0.5f, 0.9f, 0.1f)),
        (BodyRegion.LegL, new Vector3(-0.2f, 0.4f, 0.45f)),
        (BodyRegion.LegR, new Vector3(0.2f, 0.4f, 0.45f)),
    };

    public static string Main()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera camera = Object.FindAnyObjectByType<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 3.2f), Quaternion.Euler(5f, 180f, 0f));

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10f, 1f, 10f);

        new GameObject("SimHost", typeof(SimHost));
        new GameObject("CheatPanel", typeof(CheatPanel));

        var character = new GameObject("Character");
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body (stand-in)";
        body.transform.SetParent(character.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);

        var view = character.AddComponent<CharacterMarksView>();
        var serialized = new SerializedObject(view);
        SerializedProperty anchors = serialized.FindProperty("anchors");
        anchors.arraySize = Anchors.Length;
        for (int i = 0; i < Anchors.Length; i++)
        {
            var anchor = new GameObject("Anchor_" + Anchors[i].region).transform;
            anchor.SetParent(character.transform, false);
            anchor.localPosition = Anchors[i].position;
            SerializedProperty element = anchors.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("region").enumValueIndex = (int)Anchors[i].region;
            element.FindPropertyRelative("anchor").objectReferenceValue = anchor;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);

        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!buildScenes.Exists(s => s.path == ScenePath))
        {
            buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }
        return "Saved " + ScenePath + " with " + Anchors.Length + " region anchors.";
    }
}

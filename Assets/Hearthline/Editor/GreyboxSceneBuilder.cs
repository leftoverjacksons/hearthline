using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/M0_Greybox.unity, the test bench for marks, hits and the mirror: rolling ground
    /// and the player rig. Menu: Hearthline → Rebuild M0 greybox scene. From the CLI:
    /// <c>unity command script eval --code "return Hearthline.EditorTools.GreyboxSceneBuilder.Build();"</c>
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/M0_Greybox.unity";
        private const string GroundPath = "Assets/Scenes/M0_Greybox_Ground.asset";
        private const float Size = 200f;
        private const float Height = 12f;
        private const float FlatRadius = 12f; // flat ground around the spawn point

        [MenuItem("Hearthline/Rebuild M0 greybox scene")]
        private static void Menu() => Debug.Log(Build());

        public static string Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneBuildKit.CreateSun();
            SceneBuildKit.BuildGround("Greybox Terrain", GroundPath, Size, 128, HeightAt, null);
            SceneBuildKit.PlayerRig rig = SceneBuildKit.BuildPlayerRig(Vector3.zero);

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildKit.AddToBuild(ScenePath, first: true);
            AssetDatabase.SaveAssets();
            return "Saved " + ScenePath + ": player rig with " + rig.HitZones + " hit zones, " + Size + " m ground.";
        }

        // Gentle rolling hills from fixed Perlin noise, flat around the spawn point at the centre.
        private static float HeightAt(float x, float z)
        {
            float noise = Mathf.PerlinNoise(x * 0.02f + 17.3f, z * 0.02f + 4.1f) * 0.75f
                        + Mathf.PerlinNoise(x * 0.07f + 3.7f, z * 0.07f + 11.9f) * 0.25f;
            float distance = Mathf.Sqrt(x * x + z * z);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FlatRadius, FlatRadius * 2.5f, distance));
            return noise * rise * Height;
        }
    }
}

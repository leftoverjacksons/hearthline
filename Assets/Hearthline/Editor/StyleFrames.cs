using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// The look pass's fixed screenshots (docs/specs/look-pass.md, ADR 0027): the same eight shots of the region,
    /// same hour, fog and camera every time, so each step can be judged as before and after without playing.
    /// Saved as JPEGs to docs/style-frames/&lt;label&gt;/ with a report of render times.
    /// </summary>
    public static class StyleFrames
    {
        public const int Width = 1280, Height = 720;
        public const string Folder = "docs/style-frames";

        private sealed class Shot
        {
            public string Name, Caption;
            public float Hour, Fog = 1f;
            public Vector3 Eye, Target, PlayerAt;
            public float Fov = 55f;
        }

        [MenuItem("Hearthline/Capture style frames")]
        private static void Menu() => UnityEngine.Debug.Log(Capture("manual-" + DateTime.Now.ToString("yyyyMMdd-HHmm")));

        /// <summary>Opens the region scene if needed and captures every shot into docs/style-frames/label.</summary>
        public static string Capture(string label)
        {
            if (EditorApplication.isPlaying) return "Stop Play Mode first.";
            if (EditorSceneManager.GetActiveScene().path != RegionSceneBuilder.ScenePath)
                EditorSceneManager.OpenScene(RegionSceneBuilder.ScenePath, OpenSceneMode.Single);

            var atmosphere = Object.FindAnyObjectByType<Atmosphere>();
            GameObject player = GameObject.Find("Player");
            var location = player != null ? player.GetComponent<PlayerLocation>() : null;
            Vector3 playerWas = player != null ? player.transform.position : Vector3.zero;
            Quaternion playerTurn = player != null ? player.transform.rotation : Quaternion.identity;

            string dir = Path.Combine(Folder, label);
            Directory.CreateDirectory(dir);
            var camGo = new GameObject("Style Frame Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 1500f;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var report = new StringBuilder("# Style frames: " + label + "\n\nCaptured " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ". Render time is one editor render of the shot, a rough guide to cost, not a frame rate.\n\n| Shot | Hour | Fog | Render ms |\n| --- | --- | --- | --- |\n");
            try
            {
                foreach (Shot shot in Shots())
                {
                    if (player != null)
                    {
                        player.transform.position = Ground(shot.PlayerAt) + Vector3.up * 0.05f;
                        Vector3 look = shot.Eye - shot.PlayerAt;
                        look.y = 0f;
                        if (look.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(look.normalized);
                    }
                    if (location != null) location.Refresh();
                    if (atmosphere != null)
                    {
                        atmosphere.WeatherFog = shot.Fog;
                        atmosphere.Apply(shot.Hour, 100f);
                    }
                    foreach (HomeLights lights in Object.FindObjectsByType<HomeLights>(FindObjectsInactive.Exclude)) lights.Preview(shot.Hour);
                    cam.fieldOfView = shot.Fov;
                    cam.transform.position = shot.Eye;
                    cam.transform.LookAt(shot.Target);
                    cam.targetTexture = rt;
                    var watch = Stopwatch.StartNew();
                    foreach (InstancedScatter scatter in Object.FindObjectsByType<InstancedScatter>(FindObjectsInactive.Exclude)) scatter.Draw(cam, true);
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    tex.Apply();
                    watch.Stop();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(dir, shot.Name + ".jpg"), tex.EncodeToJPG(85));
                    report.Append("| " + shot.Name + " — " + shot.Caption + " | " + shot.Hour.ToString("0.#") + " | " + shot.Fog.ToString("0.#") + " | " + watch.ElapsedMilliseconds + " |\n");
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                if (player != null) player.transform.SetPositionAndRotation(playerWas, playerTurn);
                if (location != null) location.Refresh();
                if (atmosphere != null) { atmosphere.WeatherFog = 1f; atmosphere.Apply(9f, 100f); }
            }
            File.WriteAllText(Path.Combine(dir, "README.md"), report.ToString());
            return "Saved " + Shots().Count + " style frames to " + dir;
        }

        private static Vector3 Ground(Vector3 p) => new Vector3(p.x, RegionSceneBuilder.HeightAt(p.x, p.z), p.z);
        private static Vector3 Up(float x, float z, float above) => new Vector3(x, RegionSceneBuilder.HeightAt(x, z) + above, z);

        private static List<Shot> Shots()
        {
            WorldMap map = RegionSceneBuilder.Map;
            var shots = new List<Shot>
            {
                new Shot { Name = "1_homestead_dusk", Caption = "the homestead at dusk", Hour = 20.0f, Eye = Up(20f, -26f, 5f), Target = Up(0f, 0f, 2f), PlayerAt = new Vector3(4f, 0f, -8f) },
                new Shot { Name = "2_homestead_noon", Caption = "the homestead at noon", Hour = 12.5f, Eye = Up(-24f, 20f, 6f), Target = Up(0f, 0f, 1.5f), PlayerAt = new Vector3(-6f, 0f, 6f) },
            };

            // A ruin in the morning: the first place that isn't the Elder Works, from its road side.
            foreach (PlacedPlace p in RegionSceneBuilder.Places)
            {
                if (p.Kind == "elder_works") continue;
                Vector3 c = Up((float)p.P.X, (float)p.P.Z, 0f);
                Vector3 front = Quaternion.Euler(0f, (float)p.Facing + 25f, 0f) * Vector3.forward;
                Vector3 eye = c + front * 26f;
                shots.Add(new Shot { Name = "3_ruin_morning", Caption = "the " + p.Kind.Replace('_', ' ') + " in the morning", Hour = 8.5f, Eye = Up(eye.x, eye.z, 6f), Target = c + Vector3.up * 2f, PlayerAt = c + front * 12f });
                break;
            }

            // The creek in morning fog: the stream point nearest home, looking along the water.
            PathPoint best = default;
            Vector2 along = Vector2.up;
            float bestD = float.MaxValue;
            foreach (Waterway w in map.Streams)
                for (int i = 1; i < w.Points.Count; i++)
                {
                    float d = (float)w.Points[i].P.Length;
                    if (d < bestD && d > 45f) { bestD = d; best = w.Points[i]; along = new Vector2((float)(w.Points[i].P.X - w.Points[i - 1].P.X), (float)(w.Points[i].P.Z - w.Points[i - 1].P.Z)).normalized; }
                }
            Vector2 side = new Vector2(-along.y, along.x);
            Vector2 bank = new Vector2((float)best.P.X, (float)best.P.Z) + side * ((float)best.Width * 0.5f + 3f) - along * 10f;
            Vector2 look = new Vector2((float)best.P.X, (float)best.P.Z) + along * 15f;
            shots.Add(new Shot { Name = "4_creek_fog", Caption = "the creek in morning fog", Hour = 6.6f, Fog = 2.5f, Eye = Up(bank.x, bank.y, 1.8f), Target = Up(look.x, look.y, 0.5f), PlayerAt = new Vector3(bank.x + along.x * 4f, 0f, bank.y + along.y * 4f) });

            // The deep woods: the most forested spot within 200 m of home, at eye height.
            Vector2 woods = Vector2.zero;
            double most = -1;
            for (float x = -200f; x <= 200f; x += 10f)
                for (float z = -200f; z <= 200f; z += 10f)
                {
                    if (new Vector2(x, z).magnitude < 70f) continue;
                    double f = map.ForestAt(x, z);
                    if (f > most) { most = f; woods = new Vector2(x, z); }
                }
            shots.Add(new Shot { Name = "5_deep_woods", Caption = "the deep woods in the afternoon", Hour = 15f, Eye = Up(woods.x, woods.y, 1.7f), Target = Up(woods.x + 20f, woods.y + 8f, 1.5f), PlayerAt = new Vector3(woods.x + 5f, 0f, woods.y + 2f) });

            // The Elder Works in fog, from its doorway side.
            foreach (PlacedPlace p in RegionSceneBuilder.Places)
            {
                if (p.Kind != "elder_works") continue;
                Vector3 c = Up((float)p.P.X, (float)p.P.Z, 0f);
                Vector3 front = Quaternion.Euler(0f, (float)p.Facing + 20f, 0f) * Vector3.forward;
                Vector3 eye = c + front * 24f;
                shots.Add(new Shot { Name = "6_elder_works_fog", Caption = "the Elder Works in fog", Hour = 7f, Fog = 2f, Eye = Up(eye.x, eye.z, 5f), Target = c + Vector3.up * 2f, PlayerAt = c + front * 14f });
            }

            shots.Add(new Shot { Name = "7_overview", Caption = "the valley toward the mountain, late afternoon", Hour = 17f, Eye = Up(0f, -280f, 70f), Target = Up(0f, 150f, 40f), PlayerAt = new Vector3(0f, 0f, 4f), Fov = 60f });
            shots.Add(new Shot { Name = "8_player_close", Caption = "the player, over the shoulder", Hour = 10f, Eye = Up(1.2f, 7.5f, 1.8f), Target = Up(-0.5f, 2f, 1.3f), PlayerAt = new Vector3(0f, 0f, 4f), Fov = 45f });
            return shots;
        }
    }
}

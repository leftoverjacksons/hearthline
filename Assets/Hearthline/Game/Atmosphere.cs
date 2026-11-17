using Hearthline.Bridge;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Light and air through the day: sun and moon colour and strength, ambient light, sky exposure and fog,
    /// all read from the <see cref="TravelClock"/>'s hour. Fog thickens in the deep woods and Thin places.
    /// Every value here is a starting point for tuning by eye (feel values are Travis's), so they are curves
    /// and gradients in the Inspector, not constants. Presentation only; nothing here is saved.
    /// </summary>
    [ExecuteAlways]
    public sealed class Atmosphere : MonoBehaviour
    {
        [SerializeField] private TravelClock clock;
        [SerializeField] private Light sun;
        [SerializeField] private Light moon;
        [SerializeField] private PlayerLocation location;
        [Tooltip("Hour used in the editor and when there is no clock.")]
        [SerializeField, Range(0f, 24f)] private float previewHour = 9f;

        [Header("Sun and moon (x axis: hour 0-24)")]
        [SerializeField] private Gradient sunColour = Keys(
            (0f, new Color(0.35f, 0.4f, 0.65f)), (5.5f, new Color(1f, 0.55f, 0.35f)), (8f, new Color(1f, 0.93f, 0.82f)),
            (16.5f, new Color(1f, 0.9f, 0.76f)), (19.5f, new Color(1f, 0.5f, 0.25f)), (21f, new Color(0.35f, 0.4f, 0.65f)));
        [SerializeField] private AnimationCurve sunIntensity = Curve((0f, 0f), (5f, 0f), (6.5f, 0.7f), (9f, 1.35f), (16f, 1.35f), (19f, 0.7f), (20.5f, 0f), (24f, 0f));
        [SerializeField] private AnimationCurve moonIntensity = Curve((0f, 0.3f), (5f, 0.24f), (6.5f, 0f), (19.5f, 0f), (21f, 0.24f), (24f, 0.3f));

        [Header("Ambient and sky")]
        [SerializeField] private Gradient skyAmbient = Keys(
            (0f, new Color(0.11f, 0.13f, 0.22f)), (6f, new Color(0.5f, 0.45f, 0.48f)), (9f, new Color(0.58f, 0.66f, 0.78f)),
            (17f, new Color(0.58f, 0.64f, 0.74f)), (19.5f, new Color(0.6f, 0.45f, 0.4f)), (22f, new Color(0.11f, 0.13f, 0.22f)));
        [SerializeField] private Gradient groundAmbient = Keys(
            (0f, new Color(0.03f, 0.04f, 0.05f)), (8f, new Color(0.36f, 0.37f, 0.28f)), (18f, new Color(0.36f, 0.34f, 0.26f)), (22f, new Color(0.03f, 0.04f, 0.05f)));
        [SerializeField] private AnimationCurve skyExposure = Curve((0f, 0.8f), (5.5f, 0.9f), (8f, 1.05f), (17f, 1.05f), (20f, 0.95f), (22f, 0.8f), (24f, 0.8f));

        [Header("Fog")]
        [SerializeField] private Gradient fogColour = Keys(
            (0f, new Color(0.06f, 0.07f, 0.1f)), (6f, new Color(0.78f, 0.7f, 0.64f)), (9f, new Color(0.7f, 0.77f, 0.84f)),
            (17f, new Color(0.72f, 0.76f, 0.8f)), (19.5f, new Color(0.82f, 0.6f, 0.45f)), (22f, new Color(0.06f, 0.07f, 0.1f)));
        [Tooltip("Base fog density (exponential squared). Thinned for the 1.2 km generated land so the mountain shows (ADR 0024); tune freely.")]
        [SerializeField] private float fogDensity = 0.0018f;
        [Tooltip("Multiplier on fog density by hour: morning haze, clear days.")]
        [SerializeField] private AnimationCurve fogByHour = Curve((0f, 1.4f), (5f, 2.4f), (8f, 2f), (11f, 1f), (18f, 1f), (21f, 1.3f), (24f, 1.4f));
        [Tooltip("Fog multiplier in the deep woods, hollows and Thin places.")]
        [SerializeField] private float hollowFog = 2.6f;
        [SerializeField] private float fogBlendSpeed = 0.5f;

        private float _fogMultiplier = 1f;
        private static readonly int SunDirectionId = Shader.PropertyToID("_HL_SunDirection");
        private static readonly int SunColorId = Shader.PropertyToID("_HL_SunColor");

        private void OnEnable()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            if (sun != null) RenderSettings.sun = sun;
        }

        private void LateUpdate()
        {
            float hour = clock != null && Application.isPlaying ? clock.Hour : previewHour;
            Apply(hour, Application.isPlaying ? Time.deltaTime : 1f);
        }

        public void Apply(float hour, float deltaTime)
        {
            float t = Mathf.Repeat(hour, 24f) / 24f;
            if (sun != null)
            {
                // The sky shader reads the true sun (not URP's main light, which is the moon at night).
                Shader.SetGlobalVector(SunDirectionId, -sun.transform.forward);
                Color sc = sunColour.Evaluate(t);
                Shader.SetGlobalVector(SunColorId, new Vector4(sc.r, sc.g, sc.b, 1f));
                sun.color = sunColour.Evaluate(t);
                sun.intensity = sunIntensity.Evaluate(hour);
                sun.enabled = sun.intensity > 0.001f;
            }
            if (moon != null)
            {
                if (sun != null) moon.transform.rotation = sun.transform.rotation * Quaternion.Euler(180f, 0f, 0f);
                moon.intensity = moonIntensity.Evaluate(hour);
                moon.enabled = moon.intensity > 0.001f;
            }

            Color sky = skyAmbient.Evaluate(t), ground = groundAmbient.Evaluate(t);
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = Color.Lerp(sky, ground, 0.5f);
            RenderSettings.ambientGroundColor = ground;
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
                RenderSettings.skybox.SetFloat("_Exposure", skyExposure.Evaluate(hour));

            float target = 1f;
            if (location != null && (location.ThinLevel > 0 || Contains(location, "deep_woods") || Contains(location, "hollow"))) target = hollowFog;
            _fogMultiplier = Mathf.MoveTowards(_fogMultiplier, target, fogBlendSpeed * deltaTime * hollowFog);
            RenderSettings.fogColor = fogColour.Evaluate(t);
            RenderSettings.fogDensity = fogDensity * fogByHour.Evaluate(hour) * _fogMultiplier;
        }

        private static bool Contains(PlayerLocation l, string tag)
        {
            foreach (string s in l.AreaTags) if (s == tag) return true;
            return false;
        }

        private static Gradient Keys(params (float hour, Color colour)[] keys)
        {
            var colours = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) colours[i] = new GradientColorKey(keys[i].colour, keys[i].hour / 24f);
            var g = new Gradient();
            g.SetKeys(colours, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        private static AnimationCurve Curve(params (float hour, float value)[] keys)
        {
            var c = new AnimationCurve();
            foreach (var k in keys) c.AddKey(k.hour, k.value);
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }
    }
}

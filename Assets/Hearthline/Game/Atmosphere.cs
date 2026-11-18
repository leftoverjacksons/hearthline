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
            (0f, new Color(0.11f, 0.13f, 0.22f)), (6f, new Color(0.5f, 0.45f, 0.48f)), (9f, new Color(0.7f, 0.79f, 0.9f)),
            (17f, new Color(0.7f, 0.77f, 0.86f)), (19.2f, new Color(0.4f, 0.4f, 0.56f)), (20.5f, new Color(0.18f, 0.2f, 0.34f)), (22f, new Color(0.11f, 0.13f, 0.22f)));
        [SerializeField] private Gradient groundAmbient = Keys(
            (0f, new Color(0.03f, 0.04f, 0.05f)), (8f, new Color(0.4f, 0.45f, 0.3f)), (18f, new Color(0.38f, 0.4f, 0.28f)), (20f, new Color(0.08f, 0.09f, 0.12f)), (22f, new Color(0.03f, 0.04f, 0.05f)));
        [SerializeField] private AnimationCurve skyExposure = Curve((0f, 0.8f), (5.5f, 0.9f), (8f, 1.05f), (17f, 1.05f), (20f, 0.95f), (22f, 0.8f), (24f, 0.8f));

        [Header("Fog")]
        [SerializeField] private Gradient fogColour = Keys(
            (0f, new Color(0.06f, 0.07f, 0.1f)), (6f, new Color(0.78f, 0.7f, 0.64f)), (9f, new Color(0.7f, 0.77f, 0.84f)),
            (17f, new Color(0.72f, 0.76f, 0.8f)), (19.5f, new Color(0.66f, 0.54f, 0.52f)), (22f, new Color(0.06f, 0.07f, 0.1f)));
        [Tooltip("Base fog density (exponential squared). Thinned for the 1.2 km generated land so the mountain shows (ADR 0024); tune freely.")]
        [SerializeField] private float fogDensity = 0.0018f;
        [Tooltip("Multiplier on fog density by hour: morning haze, clear days.")]
        [SerializeField] private AnimationCurve fogByHour = Curve((0f, 1.4f), (5f, 2.4f), (8f, 2f), (11f, 1f), (18f, 1f), (21f, 1.3f), (24f, 1.4f));
        [Tooltip("Fog multiplier in the deep woods, hollows and Thin places.")]
        [SerializeField] private float hollowFog = 2.6f;
        [SerializeField] private float fogBlendSpeed = 0.5f;

        [Header("Height fog and light shafts (ADR 0028)")]
        [Tooltip("World height of the valley floor: height fog is densest here and thins above it.")]
        [SerializeField] private float heightFogBase;
        [Tooltip("Fog density per metre at the base, by hour: thick at dawn, thin by midday.")]
        [SerializeField] private AnimationCurve heightFogDensity = Curve((0f, 0.006f), (5f, 0.011f), (7f, 0.013f), (9.5f, 0.006f), (12f, 0.0025f), (17f, 0.0025f), (19.5f, 0.005f), (22f, 0.006f), (24f, 0.006f));
        [Tooltip("How fast the fog thins with height (per metre): 0.075 halves it every 9 m or so.")]
        [SerializeField] private float heightFogFalloff = 0.075f;
        [SerializeField, Range(0f, 1f)] private float heightFogMaxOpacity = 0.72f;
        [Tooltip("How much the sun's colour glows through the fog when looking toward it.")]
        [SerializeField] private float fogSunGlow = 0.9f;
        [Tooltip("Light shaft strength by hour: strongest with a low sun.")]
        [SerializeField] private AnimationCurve shaftStrength = Curve((0f, 0f), (5.5f, 0f), (6.5f, 0.55f), (9f, 0.35f), (12f, 0.22f), (16f, 0.3f), (19f, 0.6f), (20.3f, 0f), (24f, 0f));
        [SerializeField, Range(0f, 1f)] private float shaftReach = 0.6f;
        [SerializeField, Range(0.8f, 1f)] private float shaftDecay = 0.95f;
        [SerializeField] private ColourGrade grade;

        private float _fogMultiplier = 1f, _thin;
        private static readonly int HeightFogId = Shader.PropertyToID("_HL_HeightFog");
        private static readonly int HeightFogColorId = Shader.PropertyToID("_HL_HeightFogColor");
        private static readonly int ShaftsId = Shader.PropertyToID("_HL_Shafts");

        /// <summary>Used by scene builders.</summary>
        public void Configure(float valleyFloor, ColourGrade colourGrade)
        {
            heightFogBase = valleyFloor;
            grade = colourGrade;
        }

        /// <summary>Set by <see cref="WeatherView"/>: how much the weather dims the sun, the sky light, and thickens the fog.</summary>
        public float WeatherSun { get; set; } = 1f;
        public float WeatherAmbient { get; set; } = 1f;
        public float WeatherFog { get; set; } = 1f;
        /// <summary>A brief flash of light (lightning), 0 to 1.</summary>
        public float Flash { get; set; }
        private static readonly int SunDirectionId = Shader.PropertyToID("_HL_SunDirection");
        private static readonly int SunColorId = Shader.PropertyToID("_HL_SunColor");

        private void OnDisable()
        {
            // Other scenes share the renderer: leave the full-screen pass doing nothing.
            Shader.SetGlobalVector(HeightFogId, Vector4.zero);
            Shader.SetGlobalVector(ShaftsId, Vector4.zero);
        }

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
                sun.intensity = sunIntensity.Evaluate(hour) * WeatherSun + Flash * 2f;
                sun.enabled = sun.intensity > 0.001f;
            }
            if (moon != null)
            {
                if (sun != null) moon.transform.rotation = sun.transform.rotation * Quaternion.Euler(180f, 0f, 0f);
                moon.intensity = moonIntensity.Evaluate(hour);
                moon.enabled = moon.intensity > 0.001f;
            }

            Color sky = skyAmbient.Evaluate(t) * WeatherAmbient, ground = groundAmbient.Evaluate(t) * WeatherAmbient;
            sky = Color.Lerp(sky, Color.white, Flash * 0.6f);
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = Color.Lerp(sky, ground, 0.5f);
            RenderSettings.ambientGroundColor = ground;
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
                RenderSettings.skybox.SetFloat("_Exposure", skyExposure.Evaluate(hour) * Mathf.Lerp(1f, WeatherAmbient, 0.7f) + Flash * 1.5f);

            float target = 1f;
            if (location != null && (location.ThinLevel > 0 || Contains(location, "deep_woods") || Contains(location, "hollow"))) target = hollowFog;
            _fogMultiplier = Mathf.MoveTowards(_fogMultiplier, target, fogBlendSpeed * deltaTime * hollowFog);
            Color fog = fogColour.Evaluate(t);
            // Grey weather greys the air.
            float grey = Mathf.Clamp01(1f - WeatherAmbient) * 0.8f;
            fog = Color.Lerp(fog, new Color(fog.grayscale, fog.grayscale, fog.grayscale) * 0.9f, grey);
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity * fogByHour.Evaluate(hour) * _fogMultiplier * WeatherFog;

            // Height fog pools in the valley and the hollows; it follows the same colour, weather and hollows.
            float hush = 1f;
            var weather = WeatherView.Active;
            if (weather != null) hush += weather.Hush * 0.6f;
            float density = heightFogDensity.Evaluate(hour) * Mathf.Lerp(1f, _fogMultiplier, 0.6f) * WeatherFog * hush;
            Shader.SetGlobalVector(HeightFogId, new Vector4(density, heightFogBase, heightFogFalloff, heightFogMaxOpacity));
            float glow = fogSunGlow * Mathf.Clamp01(sun != null && sun.enabled ? sun.intensity : 0f) * (1f - grey);
            Shader.SetGlobalVector(HeightFogColorId, new Vector4(fog.r, fog.g, fog.b, glow));
            Shader.SetGlobalVector(ShaftsId, new Vector4(shaftStrength.Evaluate(hour) * WeatherSun * (1f - grey), shaftReach, shaftDecay, 0f));

            // The colour of the hour, the weather and the Thin.
            int thinLevel = location != null ? location.ThinLevel : 0;
            float thinTarget = thinLevel >= 2 ? 1f : thinLevel == 1 ? 0.35f : 0f;
            _thin = Mathf.MoveTowards(_thin, thinTarget, deltaTime * 0.4f);
            if (grade != null) grade.Apply(hour, _thin, Mathf.Clamp01(1f - WeatherAmbient) * 2f);
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

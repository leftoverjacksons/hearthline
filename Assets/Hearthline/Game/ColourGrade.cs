using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hearthline.Game
{
    /// <summary>
    /// The colour of the day (ADR 0028), Valheim-like: saturated with a warm sun and cool teal shadows by day,
    /// gold in the evening, cold blue and grey at night. Thin places drain the colour toward grey with one pale
    /// blue-violet accent (DESIGN, *Palette*). Drives its own volume with an in-memory profile, layered over the
    /// scene's base grade, so neither play nor the style frames ever edit an asset.
    /// Every value is a starting point for Travis to tune in the Inspector (x axis: hour 0-24).
    /// </summary>
    [ExecuteAlways]
    public sealed class ColourGrade : MonoBehaviour
    {
        [Header("By hour")]
        [SerializeField] private AnimationCurve temperature = Curve((0f, -28f), (5.5f, -6f), (7f, 14f), (10f, 4f), (16f, 6f), (18.5f, 16f), (20f, -2f), (21.5f, -26f), (24f, -28f));
        [SerializeField] private AnimationCurve saturation = Curve((0f, -38f), (5.5f, -10f), (8f, 16f), (16f, 18f), (19f, 22f), (21f, -18f), (24f, -38f));
        [SerializeField] private AnimationCurve contrast = Curve((0f, 6f), (7f, 14f), (12f, 20f), (18f, 22f), (21f, 10f), (24f, 6f));
        [SerializeField] private AnimationCurve exposure = Curve((0f, -0.35f), (6f, 0.05f), (12f, 0.15f), (18f, 0.1f), (21f, -0.2f), (24f, -0.35f));
        [Tooltip("Shadows tint: teal by day (Valheim), deep blue at night.")]
        [SerializeField] private Gradient shadowTint = Keys((0f, new Color(0.85f, 0.95f, 1.2f)), (8f, new Color(0.9f, 1.05f, 1.08f)), (17f, new Color(0.92f, 1.0f, 1.08f)), (20f, new Color(0.95f, 0.9f, 1.12f)), (22f, new Color(0.85f, 0.95f, 1.2f)));
        [Tooltip("Highlights tint: warm sunlight.")]
        [SerializeField] private Gradient highlightTint = Keys((0f, new Color(0.95f, 1f, 1.08f)), (7f, new Color(1.1f, 1.02f, 0.92f)), (12f, new Color(1.04f, 1.02f, 0.96f)), (18.5f, new Color(1.14f, 1.0f, 0.86f)), (21f, new Color(0.95f, 1f, 1.08f)));

        [Header("Thin places")]
        [SerializeField, Range(-100f, 0f)] private float thinSaturation = -45f;
        [SerializeField, Range(-50f, 50f)] private float thinTemperature = -18f;
        [Tooltip("The Thin's one accent: pale blue-violet in the midtones.")]
        [SerializeField] private Color thinAccent = new Color(0.97f, 0.95f, 1.12f);

        [Header("Weather")]
        [Tooltip("Saturation lost under a grey sky (overcast, rain).")]
        [SerializeField] private float greySaturation = -22f;

        private Volume _volume;
        private VolumeProfile _profile;
        private WhiteBalance _white;
        private ColorAdjustments _adjust;
        private ShadowsMidtonesHighlights _smh;

        private void OnEnable()
        {
            if (_volume == null)
            {
                var go = new GameObject("Colour Grade (runtime)") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                _volume = go.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 10f;
            }
            if (_profile == null)
            {
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.hideFlags = HideFlags.HideAndDontSave;
                _white = _profile.Add<WhiteBalance>(true);
                _adjust = _profile.Add<ColorAdjustments>(true);
                _smh = _profile.Add<ShadowsMidtonesHighlights>(true);
            }
            _volume.sharedProfile = _profile;
        }

        private void OnDisable()
        {
            if (_volume != null) DestroyImmediate(_volume.gameObject);
            if (_profile != null) DestroyImmediate(_profile);
            _volume = null;
            _profile = null;
        }

        /// <summary>Called by <see cref="Atmosphere"/> each frame.</summary>
        /// <param name="thin">0 in ordinary land; 1 at full Thin.</param>
        /// <param name="grey">0 under a clear sky; 1 under heavy cloud or rain.</param>
        public void Apply(float hour, float thin, float grey)
        {
            if (_profile == null) OnEnable();
            float t = Mathf.Repeat(hour, 24f) / 24f;
            _white.temperature.Override(Mathf.Lerp(temperature.Evaluate(hour), thinTemperature, thin));
            _white.tint.Override(Mathf.Lerp(0f, 6f, thin));
            _adjust.saturation.Override(saturation.Evaluate(hour) + greySaturation * grey + thinSaturation * thin);
            _adjust.contrast.Override(contrast.Evaluate(hour) * (1f - 0.4f * grey));
            _adjust.postExposure.Override(exposure.Evaluate(hour));
            _smh.shadows.Override(ToVector(shadowTint.Evaluate(t)));
            _smh.midtones.Override(ToVector(Color.Lerp(Color.white, thinAccent, thin)));
            _smh.highlights.Override(ToVector(highlightTint.Evaluate(t)));
        }

        // ShadowsMidtonesHighlights takes a colour in xyz (1 = neutral) and an offset in w.
        private static Vector4 ToVector(Color c) => new Vector4(c.r, c.g, c.b, 0f);

        private static Gradient Keys(params (float hour, Color colour)[] keys)
        {
            var colours = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) colours[i] = new GradientColorKey(keys[i].colour, keys[i].hour / 24f);
            var g = new Gradient { mode = GradientMode.Blend };
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

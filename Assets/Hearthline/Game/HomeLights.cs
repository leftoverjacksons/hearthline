using Hearthline.Bridge;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Coming home at dusk to lit windows is the key image (DESIGN, *Lighting*; ADR 0028): the hearth's fire, a
    /// lamp at the dwelling and one at the infirmary come up as the light goes, but only once something is built
    /// in that slot. The fire flickers. Presentation only. Brightness and colours are Travis's to tune.
    /// </summary>
    [ExecuteAlways]
    public sealed class HomeLights : MonoBehaviour
    {
        [SerializeField] private TravelClock clock;
        [SerializeField] private Light fire, dwellingLamp, infirmaryLamp;
        [SerializeField] private StructureView hearth, dwelling, infirmary;
        [SerializeField] private float fireIntensity = 6f, lampIntensity = 4f;
        [Tooltip("Hours when the lights come up (dusk) and go down (dawn).")]
        [SerializeField] private Vector2 dusk = new Vector2(18.3f, 20.2f), dawn = new Vector2(5.2f, 7f);
        [SerializeField] private float flicker = 0.25f;
        [Tooltip("Hour used in the editor (style frames set it).")]
        [SerializeField, Range(0f, 24f)] private float previewHour = 9f;

        /// <summary>0 by day, 1 at night.</summary>
        public float Night { get; private set; }

        public void Configure(TravelClock travelClock, Light fireLight, Light dwellingLight, Light infirmaryLight, StructureView hearthView, StructureView dwellingView, StructureView infirmaryView)
        {
            clock = travelClock;
            fire = fireLight; dwellingLamp = dwellingLight; infirmaryLamp = infirmaryLight;
            hearth = hearthView; dwelling = dwellingView; infirmary = infirmaryView;
        }

        /// <summary>For the style frames: show the lights as at this hour.</summary>
        public void Preview(float hour)
        {
            previewHour = hour;
            Apply(hour);
        }

        private void LateUpdate() => Apply(clock != null && Application.isPlaying ? clock.Hour : previewHour);

        private void Apply(float hour)
        {
            float evening = Mathf.InverseLerp(dusk.x, dusk.y, hour), morning = 1f - Mathf.InverseLerp(dawn.x, dawn.y, hour);
            Night = hour >= 12f ? evening : morning;
            float wobble = 1f + (Mathf.PerlinNoise(Time.realtimeSinceStartup * 6f, 0.3f) - 0.5f) * 2f * flicker;
            Set(fire, hearth, fireIntensity * Night * wobble);
            Set(dwellingLamp, dwelling, lampIntensity * Night);
            Set(infirmaryLamp, infirmary, lampIntensity * 0.8f * Night);
        }

        private static void Set(Light light, StructureView slot, float intensity)
        {
            if (light == null) return;
            bool built = slot == null || slot.ShowsBuilt;
            light.intensity = built ? intensity : 0f;
            light.enabled = light.intensity > 0.01f;
        }
    }
}

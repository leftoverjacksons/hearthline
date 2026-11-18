using Hearthline.Bridge;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// The feel of a Thin place (docs/specs/ruins-and-places.md; DESIGN, *Art*: wrongness, not monsters). The
    /// deeper the Thin, the more the land holds its breath: the wind drops, the fog thickens (through
    /// <see cref="WeatherView.Hush"/>), and at Thin 2 or more a low hum comes up. Birds are already gone there
    /// (<see cref="AmbientLife"/>).
    /// </summary>
    public sealed class ThinAura : MonoBehaviour
    {
        [SerializeField] private PlayerLocation location;
        [SerializeField] private WeatherView weather;
        [SerializeField] private AudioClip hum;
        [Tooltip("Hush at Thin 1, and at Thin 2 or more.")]
        [SerializeField, Range(0f, 1f)] private float hushThin1 = 0.3f, hushThin2 = 1f;
        [SerializeField, Range(0f, 1f)] private float humVolume = 0.35f;
        [Tooltip("Seconds to come and go.")]
        [SerializeField] private float fadeSeconds = 3f;

        private AudioSource _hum;

        /// <summary>How hushed it is here now, 0 to 1.</summary>
        public float Hush { get; private set; }
        public float HumVolume => _hum != null ? _hum.volume : 0f;

        public void Configure(PlayerLocation where, WeatherView weatherView, AudioClip humClip)
        {
            location = where;
            weather = weatherView;
            hum = humClip;
        }

        private void Start()
        {
            _hum = gameObject.AddComponent<AudioSource>();
            _hum.loop = true;
            _hum.spatialBlend = 0f;
            _hum.volume = 0f;
            _hum.clip = hum;
            if (hum != null) _hum.Play();
        }

        private void Update()
        {
            int thin = location != null ? location.ThinLevel : 0;
            float target = thin >= 2 ? hushThin2 : thin == 1 ? hushThin1 : 0f;
            float step = Time.deltaTime / Mathf.Max(0.01f, fadeSeconds);
            Hush = Mathf.MoveTowards(Hush, target, step);
            if (weather != null) weather.Hush = Hush;
            if (_hum != null) _hum.volume = Mathf.MoveTowards(_hum.volume, thin >= 2 ? humVolume : 0f, step * humVolume);
        }
    }
}

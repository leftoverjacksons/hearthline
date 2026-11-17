using Hearthline.Bridge;
using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Shows the simulation's weather (docs/specs/life-and-weather.md): it reads <c>WeatherAt(hour)</c> from the sim
    /// every frame and blends into it. The sun and sky light dim, the clouds close in, the fog thickens, the trees
    /// bend harder, rain falls around the camera and the land gets wet (and dries after), and storms flash and
    /// thunder. Presentation only: the weather itself lives in the sim. Every value is for tuning by eye.
    /// </summary>
    public sealed class WeatherView : MonoBehaviour
    {
        [System.Serializable]
        public struct Look
        {
            [Tooltip("Sun strength multiplier.")] public float sun;
            [Tooltip("Sky and ambient light multiplier.")] public float ambient;
            [Tooltip("Fog density multiplier.")] public float fog;
            [Tooltip("Sky cloud coverage, 0 to 1.")] public float clouds;
            [Tooltip("Extra wind on the trees (0 = as authored).")] public float wind;
            [Tooltip("Rain drops per second around the camera.")] public float rain;
            public Look(float sun, float ambient, float fog, float clouds, float wind, float rain)
            { this.sun = sun; this.ambient = ambient; this.fog = fog; this.clouds = clouds; this.wind = wind; this.rain = rain; }
        }

        [SerializeField] private TravelClock clock;
        [SerializeField] private Atmosphere atmosphere;
        [Tooltip("Renderers whose material darkens and shines when wet (the ground). Each gets its own material copy.")]
        [SerializeField] private Renderer[] wettable = new Renderer[0];

        [Header("Looks")]
        [SerializeField] private Look clear = new Look(1f, 1f, 1f, 0.42f, 0f, 0f);
        [SerializeField] private Look overcast = new Look(0.4f, 0.8f, 1.3f, 0.95f, 0.4f, 0f);
        [SerializeField] private Look fog = new Look(0.45f, 0.8f, 4f, 0.85f, 0f, 0f);
        [SerializeField] private Look rain = new Look(0.25f, 0.62f, 1.9f, 1f, 0.8f, 2500f);
        [SerializeField] private Look storm = new Look(0.12f, 0.45f, 2.3f, 1f, 2.2f, 6000f);
        [Tooltip("Seconds (real time) to blend from one weather to the next.")]
        [SerializeField] private float blendSeconds = 20f;

        [Header("Wet ground")]
        [Tooltip("Wetness gained per second in rain (storms twice as fast).")]
        [SerializeField] private float wetPerSecond = 0.02f;
        [Tooltip("Wetness lost per second once it stops (faster under a clear sky).")]
        [SerializeField] private float dryPerSecond = 0.004f;
        [SerializeField, Range(0f, 1f)] private float wetDarken = 0.35f;
        [SerializeField, Range(0f, 1f)] private float wetSmoothness = 0.32f;

        [Header("Lightning (storms)")]
        [SerializeField] private Vector2 lightningEvery = new Vector2(8f, 25f);
        [Tooltip("Seconds from flash to thunder (distance).")]
        [SerializeField] private Vector2 thunderDelay = new Vector2(0.6f, 4f);

        [Header("Sound (no clips yet: see docs/NEEDS.md)")]
        [SerializeField] private AudioClip rainLoop;
        [SerializeField] private AudioClip windLoop;
        [SerializeField] private AudioClip[] thunder = new AudioClip[0];

        public WeatherKind Current { get; private set; } = WeatherKind.Clear;
        public float Wetness { get; private set; }
        /// <summary>Rain drops per second right now (after blending).</summary>
        public float RainRate { get; private set; }
        public float WindBoost { get; private set; }
        public ParticleSystem Rain => _rain;

        private Look _look;
        private ParticleSystem _rain, _splash;
        private Material _sky;
        private float _skyShadow;
        private Material[] _wetMaterials = new Material[0];
        private Color[] _dryColours = new Color[0];
        private float[] _drySmoothness = new float[0];
        private float _nextLightning = 10f, _flash, _thunderAt = -1f;
        private AudioSource _rainAudio, _windAudio, _thunderAudio;
        private static readonly int WindId = Shader.PropertyToID("_HL_WindBoost");
        private static readonly int WetId = Shader.PropertyToID("_HL_Wetness");

        public void Configure(TravelClock travelClock, Atmosphere atmo, Renderer[] wet)
        {
            clock = travelClock;
            atmosphere = atmo;
            wettable = wet;
        }

        private void Start()
        {
            _look = LookOf(Sim != null ? Sim.WeatherAt(Hour) : WeatherKind.Clear);
            // The sky is an asset: change a copy so play never edits the file.
            if (RenderSettings.skybox != null)
            {
                _sky = new Material(RenderSettings.skybox) { name = RenderSettings.skybox.name + " (weather)" };
                RenderSettings.skybox = _sky;
                _skyShadow = _sky.HasProperty("_CloudShadow") ? _sky.GetFloat("_CloudShadow") : 0.45f;
            }
            _wetMaterials = new Material[wettable.Length];
            _dryColours = new Color[wettable.Length];
            _drySmoothness = new float[wettable.Length];
            for (int i = 0; i < wettable.Length; i++)
            {
                if (wettable[i] == null) continue;
                _wetMaterials[i] = wettable[i].material; // a copy
                _dryColours[i] = _wetMaterials[i].HasProperty("_BaseColor") ? _wetMaterials[i].GetColor("_BaseColor") : Color.white;
                _drySmoothness[i] = _wetMaterials[i].HasProperty("_Smoothness") ? _wetMaterials[i].GetFloat("_Smoothness") : 0f;
            }
            _rain = BuildRain();
            _splash = BuildSplash();
            _rainAudio = Loop("Rain Sound", rainLoop);
            _windAudio = Loop("Wind Sound", windLoop);
            _thunderAudio = gameObject.AddComponent<AudioSource>();
            _thunderAudio.playOnAwake = false;
            _thunderAudio.spatialBlend = 0f;
        }

        /// <summary>Jumps straight to the current weather, skipping the blend (after a load, and for tests).</summary>
        public void Snap()
        {
            if (Sim != null) Current = Sim.WeatherAt(Hour);
            _look = LookOf(Current);
            if (Current == WeatherKind.Rain || Current == WeatherKind.Storm) Wetness = Mathf.Max(Wetness, 0.5f);
        }

        private static Simulation Sim => SimHost.Instance != null ? SimHost.Instance.Sim : null;
        private float Hour => clock != null ? clock.Hour : 12f;

        private Look LookOf(WeatherKind kind) =>
            kind == WeatherKind.Overcast ? overcast : kind == WeatherKind.Fog ? fog : kind == WeatherKind.Rain ? rain : kind == WeatherKind.Storm ? storm : clear;

        private void Update()
        {
            Simulation sim = Sim;
            if (sim != null) Current = sim.WeatherAt(Hour);
            Look target = LookOf(Current);
            float k = blendSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime * 3f / blendSeconds);
            _look.sun = Mathf.Lerp(_look.sun, target.sun, k);
            _look.ambient = Mathf.Lerp(_look.ambient, target.ambient, k);
            _look.fog = Mathf.Lerp(_look.fog, target.fog, k);
            _look.clouds = Mathf.Lerp(_look.clouds, target.clouds, k);
            _look.wind = Mathf.Lerp(_look.wind, target.wind, k);
            _look.rain = Mathf.Lerp(_look.rain, target.rain, k);
            if (Mathf.Abs(_look.rain - target.rain) < 5f) _look.rain = target.rain;
            RainRate = _look.rain;
            WindBoost = _look.wind;

            bool raining = Current == WeatherKind.Rain || Current == WeatherKind.Storm;
            float wetRate = Current == WeatherKind.Storm ? wetPerSecond * 2f : wetPerSecond;
            float dryRate = Current == WeatherKind.Clear ? dryPerSecond * 2f : Current == WeatherKind.Fog ? dryPerSecond * 0.3f : dryPerSecond;
            Wetness = Mathf.Clamp01(Wetness + (raining ? wetRate : -dryRate) * Time.deltaTime);

            Lightning(Current == WeatherKind.Storm);
            Apply();
        }

        private void Apply()
        {
            if (atmosphere != null)
            {
                atmosphere.WeatherSun = _look.sun;
                atmosphere.WeatherAmbient = _look.ambient;
                atmosphere.WeatherFog = _look.fog;
                atmosphere.Flash = _flash;
            }
            if (_sky != null)
            {
                if (_sky.HasProperty("_CloudCoverage")) _sky.SetFloat("_CloudCoverage", _look.clouds);
                if (_sky.HasProperty("_CloudShadow")) _sky.SetFloat("_CloudShadow", Mathf.Lerp(_skyShadow, 0.85f, Mathf.InverseLerp(0.6f, 1f, _look.clouds)));
            }
            Shader.SetGlobalFloat(WindId, _look.wind);
            Shader.SetGlobalFloat(WetId, Wetness);
            for (int i = 0; i < _wetMaterials.Length; i++)
            {
                Material m = _wetMaterials[i];
                if (m == null) continue;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", _dryColours[i] * (1f - wetDarken * Wetness));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Lerp(_drySmoothness[i], wetSmoothness, Wetness));
            }

            // Rain falls around the camera.
            Camera cam = Camera.main;
            if (_rain != null)
            {
                if (cam != null)
                {
                    _rain.transform.position = cam.transform.position + Vector3.up * 12f;
                    _splash.transform.position = cam.transform.position;
                }
                var emission = _rain.emission;
                emission.rateOverTime = RainRate;
                var splashEmission = _splash.emission;
                splashEmission.rateOverTime = RainRate * 0.12f;
                var velocity = _rain.velocityOverLifetime;
                velocity.x = new ParticleSystem.MinMaxCurve(_look.wind * 2.5f);
            }
            float rainLevel = Mathf.InverseLerp(0f, storm.rain, RainRate);
            if (_rainAudio != null) _rainAudio.volume = rainLevel;
            if (_windAudio != null) _windAudio.volume = Mathf.Clamp01(0.15f + _look.wind * 0.35f);
        }

        private void Lightning(bool storming)
        {
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 6f);
            if (_thunderAt >= 0f && Time.time >= _thunderAt)
            {
                _thunderAt = -1f;
                if (thunder.Length > 0 && _thunderAudio != null) _thunderAudio.PlayOneShot(thunder[Random.Range(0, thunder.Length)]);
            }
            if (!storming) { _nextLightning = Time.time + Random.Range(2f, lightningEvery.x); return; }
            if (Time.time < _nextLightning) return;
            _nextLightning = Time.time + Random.Range(lightningEvery.x, lightningEvery.y);
            Strike();
        }

        /// <summary>A flash now, and thunder after it. Public for the cheat panel and tests.</summary>
        public void Strike()
        {
            _flash = 1f;
            _thunderAt = Time.time + Random.Range(thunderDelay.x, thunderDelay.y);
        }

        public float FlashLevel => _flash;

        private AudioSource Loop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.clip = clip;
            if (clip != null) source.Play();
            return source;
        }

        private ParticleSystem BuildRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.maxParticles = 12000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.75f, 0.8f, 0.88f, 0.45f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-14f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 1f, 36f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1f;
            renderer.sharedMaterial = ParticleMaterial("Rain");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        // Small rings of spray where drops land: in a circle round the camera, at the ground under it.
        private ParticleSystem BuildSplash()
        {
            var go = new GameObject("Rain Splashes");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 0.25f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.gravityModifier = 0.6f;
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.8f, 0.85f, 0.9f, 0.5f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 14f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial("Splash");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        // Puts each splash particle on the ground below where it was born (the circle is at camera height).
        private void LateUpdate()
        {
            if (_splash == null || _splash.particleCount == 0) return;
            if (_particles == null || _particles.Length < _splash.main.maxParticles) _particles = new ParticleSystem.Particle[_splash.main.maxParticles];
            int n = _splash.GetParticles(_particles);
            for (int i = 0; i < n; i++)
            {
                if (_particles[i].remainingLifetime < _particles[i].startLifetime - Time.deltaTime * 1.5f) continue; // only new ones
                Vector3 p = _particles[i].position;
                if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.02f;
                _particles[i].position = p;
            }
            _splash.SetParticles(_particles, n);
        }

        private ParticleSystem.Particle[] _particles;

        private static Material ParticleMaterial(string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(shader) { name = name };
            m.SetFloat("_Surface", 1f); // transparent
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetColor("_BaseColor", Color.white);
            return m;
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(WindId, 0f);
            Shader.SetGlobalFloat(WetId, 0f);
        }
    }
}

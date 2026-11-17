using Hearthline.Bridge;
using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Time of day while playing. The simulation counts whole days; this clock runs the hours between them,
    /// turns the sun, and sends <see cref="AdvanceDays"/> when midnight passes or the party makes camp.
    /// The clock stops while a panel holds the player still. The hour is not saved: after a load the day
    /// starts at <see cref="startHour"/>.
    /// </summary>
    public sealed class TravelClock : MonoBehaviour
    {
        [Header("Pace (tune by feel)")]
        [Tooltip("Real minutes for one game day while walking about.")]
        [SerializeField, Min(0.1f)] private float minutesPerDay = 20f;
        [SerializeField, Range(0f, 24f)] private float startHour = 8f;
        [Tooltip("The hour making camp skips to.")]
        [SerializeField, Range(0f, 24f)] private float wakeHour = 6f;

        [Header("Sun")]
        [SerializeField] private Light sun;
        [SerializeField] private float sunYaw = -30f;

        [SerializeField] private PlayerInputRelease playerInput;

        private int _owedDays;

        public float Hour { get; private set; }
        public float MinutesPerDay { get => minutesPerDay; set => minutesPerDay = Mathf.Max(0.1f, value); }

        public TimeOfDay TimeOfDay =>
            Hour >= 5f && Hour < 8f ? TimeOfDay.Dawn
            : Hour >= 8f && Hour < 18f ? TimeOfDay.Day
            : Hour >= 18f && Hour < 21f ? TimeOfDay.Dusk
            : TimeOfDay.Night;

        public string HourText
        {
            get
            {
                int minutes = Mathf.FloorToInt(Hour * 60f) % (24 * 60);
                return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
            }
        }

        private void Awake()
        {
            Hour = startHour;
            UpdateSun();
        }

        private void Update()
        {
            if (playerInput != null && playerInput.Released) return;
            AdvanceHours(Time.deltaTime / (minutesPerDay * 60f) * 24f);
            if (_owedDays > 0) PayOwedDays();
        }

        /// <summary>Moves the clock forward; every midnight crossed advances the simulation one day.</summary>
        public void AdvanceHours(float hours)
        {
            if (hours <= 0f) return;
            Hour += hours;
            while (Hour >= 24f)
            {
                Hour -= 24f;
                _owedDays++;
            }
            PayOwedDays();
            UpdateSun();
        }

        /// <summary>Sleep until the next <see cref="wakeHour"/>. Returns a line for the HUD.</summary>
        public string MakeCamp()
        {
            Simulation sim = SimHost.Instance != null ? SimHost.Instance.Sim : null;
            if (sim != null && sim.State.ActiveEvent != null) return "Not now; something is happening.";
            float hours = wakeHour - Hour;
            if (hours <= 0f) hours += 24f;
            AdvanceHours(hours);
            return _owedDays > 0 ? "Could not rest." : "Slept until " + HourText + ".";
        }

        // The simulation refuses to advance during an event; the days are kept and paid once it ends.
        private void PayOwedDays()
        {
            Simulation sim = SimHost.Instance != null ? SimHost.Instance.Sim : null;
            if (sim == null || _owedDays == 0) return;
            if (sim.Execute(new AdvanceDays { Days = _owedDays }).Ok) _owedDays = 0;
        }

        private void UpdateSun()
        {
            if (sun == null) return;
            // Noon overhead, midnight underneath.
            sun.transform.rotation = Quaternion.Euler(Hour / 24f * 360f - 90f, sunYaw, 0f);
        }
    }
}

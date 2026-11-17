using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Feeds the sky shader (Hearthline/Sky) the sun's direction and colour from a fixed light, for scenes
    /// without the day cycle (the style test). The region's Atmosphere does this itself.
    /// </summary>
    [ExecuteAlways]
    public sealed class SkyGlobals : MonoBehaviour
    {
        [SerializeField] private Light sun;

        public void Configure(Light light) => sun = light;

        private void Update()
        {
            if (sun == null) return;
            Shader.SetGlobalVector("_HL_SunDirection", -sun.transform.forward);
            Shader.SetGlobalColor("_HL_SunColor", sun.color * sun.intensity);
        }
    }
}

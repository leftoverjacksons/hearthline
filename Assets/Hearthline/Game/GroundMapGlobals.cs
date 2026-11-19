using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Publishes the region's painted ground map to shaders (ADR 0033), so grass can take the colour of the ground
    /// it grows from. Presentation only; set up by the region builder.
    /// </summary>
    [ExecuteAlways]
    public sealed class GroundMapGlobals : MonoBehaviour
    {
        [SerializeField] private Texture2D groundMap;
        [Tooltip("World x and z of the map's lower-left corner, and its size in metres.")]
        [SerializeField] private Vector3 originAndSize = new Vector3(-600f, -600f, 1200f);

        private static readonly int MapId = Shader.PropertyToID("_HL_GroundMap");
        private static readonly int RectId = Shader.PropertyToID("_HL_GroundMapRect");

        public void Configure(Texture2D map, float originX, float originZ, float size)
        {
            groundMap = map;
            originAndSize = new Vector3(originX, originZ, size);
            Publish();
        }

        private void OnEnable() => Publish();
        private void Update() => Publish();

        private void OnDisable() => Shader.SetGlobalVector(RectId, Vector4.zero);

        private void Publish()
        {
            if (groundMap == null) return;
            Shader.SetGlobalTexture(MapId, groundMap);
            Shader.SetGlobalVector(RectId, new Vector4(originAndSize.x, originAndSize.y, originAndSize.z, 1f));
        }
    }
}

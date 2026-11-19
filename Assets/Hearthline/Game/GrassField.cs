using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hearthline.Game
{
    /// <summary>
    /// Grass made on the GPU around each camera, every frame (ADR 0033): a compute shader places blades from the
    /// grass map (where grass grows, how tall, how dry) and the ground's heights, and GrassBlade.shader builds,
    /// sways and lights them. Nothing is baked into the scene. Presentation only; the region builder sets the
    /// data, and every look and feel value below is for tuning by eye.
    /// Without compute shaders no grass is drawn, and the painted ground carries the look.
    /// </summary>
    [ExecuteAlways]
    public sealed class GrassField : MonoBehaviour
    {
        [Header("Data (set by the region builder)")]
        [SerializeField] private Texture2D grassMap;
        [Tooltip("World x and z of the grass map's lower-left corner, and its size in metres.")]
        [SerializeField] private Vector3 grassRect;
        [SerializeField] private Texture2D heightMap;
        [Tooltip("World x and z of the height map's first vertex, and the spacing between vertices in metres.")]
        [SerializeField] private Vector3 heightRect;
        [SerializeField] private ComputeShader placer;
        [SerializeField] private Shader bladeShader;
        [Tooltip("Blades bend away from this (the player).")]
        [SerializeField] private Transform pusher;

        [Header("Field")]
        [Tooltip("One candidate blade per cell of this size (metres). Smaller is denser and costlier.")]
        [SerializeField] private float spacing = 0.085f;
        [SerializeField, Range(0f, 1f)] private float density = 1f;
        [Tooltip("How far from the camera blades are drawn (metres).")]
        [SerializeField] private float radius = 55f;
        [Tooltip("Blades start thinning out past this distance (metres).")]
        [SerializeField] private float fullDensityDistance = 10f;
        [Tooltip("How fast blades thin out past the full-density distance.")]
        [SerializeField] private float thinning = 1.5f;
        [Tooltip("How much wider the remaining blades get as they thin out (0 none, 1 keeps the cover).")]
        [SerializeField, Range(0f, 1f)] private float widthGrowth = 0.55f;
        [Tooltip("Most blades drawn per camera.")]
        [SerializeField] private int capacity = 400000;
        [SerializeField] private bool castShadows;

        [Header("Blades")]
        [SerializeField] private float minHeight = 0.12f;
        [SerializeField] private float maxHeight = 0.95f;
        [SerializeField] private float bladeWidth = 0.035f;
        [Tooltip("Size of the patches that share height, lean and colour (metres).")]
        [SerializeField] private float clumpSize = 1.3f;
        [Tooltip("How much an edge-on blade turns toward the camera.")]
        [SerializeField, Range(0f, 1f)] private float cameraFacing = 0.6f;
        [SerializeField, Range(0f, 1f)] private float roundness = 0.4f;

        [Header("Colour")]
        [Tooltip("Tip brightness over the ground's colour.")]
        [SerializeField] private float tipLift = 1.3f;
        [Tooltip("Root brightness over the ground's colour (1 matches the ground).")]
        [SerializeField] private float rootShade = 1f;
        [SerializeField] private Color dryTip = new Color(0.66f, 0.6f, 0.3f);
        [SerializeField, Range(0f, 1f)] private float dryAmount = 0.75f;
        [SerializeField, Range(0f, 1f)] private float clumpVariation = 0.2f;
        [SerializeField, Range(0f, 1f)] private float bladeVariation = 0.12f;
        [SerializeField, Range(0f, 2f)] private float translucency = 0.5f;
        [Tooltip("Shade at the root, up close.")]
        [SerializeField, Range(0f, 1f)] private float rootAO = 0.9f;
        [Tooltip("Root shade fades out by this distance (metres).")]
        [SerializeField] private float aoFadeDistance = 25f;

        [Header("Light")]
        [Tooltip("How much of the ground's normal a near blade uses (1 lights it exactly like the ground).")]
        [SerializeField, Range(0f, 1f)] private float groundNormalNear = 0.5f;
        [Tooltip("By this distance blades are lit exactly like the ground (metres).")]
        [SerializeField] private float groundNormalFar = 30f;

        [Header("Wind and the player")]
        [SerializeField] private float windStrength = 0.22f;
        [SerializeField] private float windSpeed = 1.3f;
        [SerializeField] private float windScale = 0.15f;
        [SerializeField] private float gustStrength = 0.8f;
        [Tooltip("Blades within this distance of the player bend away (metres).")]
        [SerializeField] private float pushRadius = 0.9f;

        private const int VertsPerBlade = 24; // 4 segments of 2 triangles; the last narrows to the tip
        private const int BladeStride = 48;

        private sealed class PerCamera
        {
            public GraphicsBuffer Blades, Args;
            public MaterialPropertyBlock Props;
        }

        private readonly Dictionary<Camera, PerCamera> _cameras = new Dictionary<Camera, PerCamera>();
        private readonly Plane[] _planes = new Plane[6];
        private readonly Vector4[] _planeVectors = new Vector4[6];
        private Material _material;
        private int _reset, _place, _finish;

        private static class Ids
        {
            public static readonly int Blades = Shader.PropertyToID("_Blades"), Args = Shader.PropertyToID("_Args");
            public static readonly int GrassMap = Shader.PropertyToID("_GrassMap"), HeightMap = Shader.PropertyToID("_HeightMap");
            public static readonly int GrassRect = Shader.PropertyToID("_GrassRect"), HeightRect = Shader.PropertyToID("_HeightRect");
            public static readonly int Eye = Shader.PropertyToID("_Eye"), Grid = Shader.PropertyToID("_Grid"), Shape = Shader.PropertyToID("_Shape");
            public static readonly int Distance = Shader.PropertyToID("_Distance"), Planes = Shader.PropertyToID("_Planes");
            public static readonly int Density = Shader.PropertyToID("_Density"), Capacity = Shader.PropertyToID("_Capacity");
            public static readonly int VertsPerBlade = Shader.PropertyToID("_VertsPerBlade");
        }

        public void Configure(Texture2D grass, Vector3 grassOriginAndSize, Texture2D heights, Vector3 heightOriginAndCell,
            ComputeShader compute, Shader shader, Transform player)
        {
            grassMap = grass;
            grassRect = grassOriginAndSize;
            heightMap = heights;
            heightRect = heightOriginAndCell;
            placer = compute;
            bladeShader = shader;
            pusher = player;
        }

        public void SetPusher(Transform player) => pusher = player;

        /// <summary>Where grass grows (0 to 1) at a world spot, from the grass map. The map must be readable.</summary>
        public float DensityAt(float x, float z)
        {
            if (grassMap == null || !grassMap.isReadable) return 0f;
            return grassMap.GetPixelBilinear((x - grassRect.x) / grassRect.z, (z - grassRect.y) / grassRect.z).r;
        }

        /// <summary>How many blades were drawn for a camera last time it rendered (reads back from the GPU; for tests).</summary>
        public int BladeCount(Camera cam)
        {
            if (!_cameras.TryGetValue(cam, out PerCamera pc)) return 0;
            var args = new int[4];
            pc.Args.GetData(args);
            return args[1];
        }

        private bool Ready => grassMap != null && heightMap != null && placer != null && bladeShader != null && SystemInfo.supportsComputeShaders;

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            foreach (PerCamera pc in _cameras.Values) { pc.Blades?.Release(); pc.Args?.Release(); }
            _cameras.Clear();
            if (_material != null) DestroyImmediate(_material);
            _material = null;
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (!Ready || cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            if (_material == null)
            {
                _material = new Material(bladeShader) { name = "Grass Blades", hideFlags = HideFlags.DontSave };
                _reset = placer.FindKernel("Reset");
                _place = placer.FindKernel("Place");
                _finish = placer.FindKernel("Finish");
            }
            PerCamera pc = Buffers(cam);

            Vector3 eye = cam.transform.position;
            float step = Mathf.Max(0.03f, spacing);
            int cells = Mathf.CeilToInt(2f * radius / step);
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            for (int i = 0; i < 6; i++) _planeVectors[i] = new Vector4(_planes[i].normal.x, _planes[i].normal.y, _planes[i].normal.z, _planes[i].distance);

            placer.SetBuffer(_reset, Ids.Args, pc.Args);
            placer.SetBuffer(_place, Ids.Args, pc.Args);
            placer.SetBuffer(_place, Ids.Blades, pc.Blades);
            placer.SetBuffer(_finish, Ids.Args, pc.Args);
            placer.SetTexture(_place, Ids.GrassMap, grassMap);
            placer.SetTexture(_place, Ids.HeightMap, heightMap);
            placer.SetVector(Ids.GrassRect, grassRect);
            placer.SetVector(Ids.HeightRect, new Vector4(heightRect.x, heightRect.y, heightRect.z, heightMap.width));
            placer.SetVector(Ids.Eye, eye);
            placer.SetVector(Ids.Grid, new Vector4(Mathf.Floor((eye.x - radius) / step), Mathf.Floor((eye.z - radius) / step), cells, step));
            placer.SetVector(Ids.Shape, new Vector4(minHeight, maxHeight, bladeWidth, Mathf.Max(0.2f, clumpSize)));
            placer.SetVector(Ids.Distance, new Vector4(Mathf.Max(0.5f, fullDensityDistance), radius, thinning, widthGrowth));
            placer.SetVectorArray(Ids.Planes, _planeVectors);
            placer.SetFloat(Ids.Density, density);
            placer.SetInt(Ids.Capacity, capacity);
            placer.SetInt(Ids.VertsPerBlade, VertsPerBlade);
            placer.Dispatch(_reset, 1, 1, 1);
            int groups = Mathf.CeilToInt(cells / 8f);
            placer.Dispatch(_place, groups, groups, 1);
            placer.Dispatch(_finish, 1, 1, 1);

            UpdateMaterial();
            pc.Props.SetBuffer(Ids.Blades, pc.Blades);
            var rp = new RenderParams(_material)
            {
                camera = cam,
                matProps = pc.Props,
                worldBounds = new Bounds(eye, Vector3.one * (radius * 2f + 20f)),
                shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer,
            };
            Graphics.RenderPrimitivesIndirect(rp, MeshTopology.Triangles, pc.Args);
        }

        private PerCamera Buffers(Camera cam)
        {
            int cap = Mathf.Max(1000, capacity);
            if (_cameras.TryGetValue(cam, out PerCamera pc) && pc.Blades != null && pc.Blades.count == cap) return pc;
            // A new camera: drop the buffers of any that have been destroyed.
            var gone = new List<Camera>();
            foreach (Camera c in _cameras.Keys) if (c == null) gone.Add(c);
            foreach (Camera c in gone) { _cameras[c].Blades?.Release(); _cameras[c].Args?.Release(); _cameras.Remove(c); }
            if (pc != null) { pc.Blades?.Release(); pc.Args?.Release(); }
            pc = new PerCamera
            {
                Blades = new GraphicsBuffer(GraphicsBuffer.Target.Structured, cap, BladeStride),
                Args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 4, sizeof(int)),
                Props = new MaterialPropertyBlock(),
            };
            _cameras[cam] = pc;
            return pc;
        }

        private void UpdateMaterial()
        {
            _material.SetColor("_DryTip", dryTip);
            _material.SetFloat("_TipLift", tipLift);
            _material.SetFloat("_RootShade", rootShade);
            _material.SetFloat("_DryAmount", dryAmount);
            _material.SetFloat("_ClumpVariation", clumpVariation);
            _material.SetFloat("_BladeVariation", bladeVariation);
            _material.SetFloat("_Translucency", translucency);
            _material.SetFloat("_RootAO", rootAO);
            _material.SetFloat("_AOFadeDistance", aoFadeDistance);
            _material.SetFloat("_GroundNormalNear", groundNormalNear);
            _material.SetFloat("_GroundNormalFar", groundNormalFar);
            _material.SetFloat("_CameraFacing", cameraFacing);
            _material.SetFloat("_Roundness", roundness);
            _material.SetFloat("_Radius", radius);
            _material.SetFloat("_WindStrength", windStrength);
            _material.SetFloat("_WindSpeed", windSpeed);
            _material.SetFloat("_WindScale", windScale);
            _material.SetFloat("_GustStrength", gustStrength);
            Vector3 p = pusher != null ? pusher.position : new Vector3(0f, -10000f, 0f);
            _material.SetVector("_Pusher", new Vector4(p.x, p.y, p.z, pushRadius));
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hearthline.Game
{
    /// <summary>
    /// Draws thousands of small things (grass clumps, flowers, ferns) with GPU instancing instead of one
    /// GameObject each. Scene builders fill it; it has no colliders and holds no game state.
    /// Groups beyond <see cref="drawDistance"/> from the camera are skipped in cells. A group can have a far
    /// version (fewer faces) drawn past <see cref="farDistance"/>, so a whole forest stays cheap (ADR 0027).
    /// </summary>
    [ExecuteAlways]
    public sealed class InstancedScatter : MonoBehaviour
    {
        [Serializable]
        public sealed class Group
        {
            public Mesh mesh;
            public Material[] materials = new Material[0];
            /// <summary>Drawn instead of <see cref="mesh"/> past the far distance. Optional.</summary>
            public Mesh farMesh;
            public Material[] farMaterials = new Material[0];
            public Vector3 cellCentre;
            /// <summary>Encloses every instance (with room for its size), for culling against the camera.</summary>
            public Bounds bounds;
            public List<Matrix4x4> matrices = new List<Matrix4x4>();
        }

        [Tooltip("Cells further than this from the camera are not drawn (metres).")]
        [SerializeField] private float drawDistance = 140f;
        [SerializeField] private bool castShadows;
        [Tooltip("Cells further than this use their far version, if they have one (metres).")]
        [SerializeField] private float farDistance = 120f;
        [Tooltip("Cells further than this cast no shadows (metres). Match the pipeline's shadow distance.")]
        [SerializeField] private float shadowDistance = 110f;
        [SerializeField] private List<Group> groups = new List<Group>();

        private readonly Matrix4x4[] _batch = new Matrix4x4[1023];
        private readonly Plane[] _planes = new Plane[6];

        public int InstanceCount
        {
            get { int n = 0; foreach (Group g in groups) n += g.matrices.Count; return n; }
        }

        /// <summary>Used by scene builders. Instances are bucketed into cells of <paramref name="cellSize"/> metres.</summary>
        public void Add(Mesh mesh, Material[] materials, Matrix4x4 matrix, float cellSize = 50f, Mesh farMesh = null, Material[] farMaterials = null)
        {
            Vector3 p = matrix.GetColumn(3);
            var cell = new Vector3(Mathf.Floor(p.x / cellSize) * cellSize + cellSize / 2f, 0f, Mathf.Floor(p.z / cellSize) * cellSize + cellSize / 2f);
            if (_index == null)
            {
                _index = new Dictionary<(Mesh, Vector3), Group>();
                foreach (Group g in groups) _index[(g.mesh, g.cellCentre)] = g;
            }
            if (!_index.TryGetValue((mesh, cell), out Group group))
            {
                group = new Group { mesh = mesh, materials = materials, cellCentre = cell, farMesh = farMesh, farMaterials = farMaterials ?? new Material[0], bounds = new Bounds(p, Vector3.zero) };
                groups.Add(group);
                _index[(mesh, cell)] = group;
            }
            Bounds b = mesh.bounds;
            Vector3 size = matrix.lossyScale;
            group.bounds.Encapsulate(new Bounds(matrix.MultiplyPoint3x4(b.center), Vector3.Scale(b.size, size) + Vector3.one));
            group.matrices.Add(matrix);
        }

        public void Clear()
        {
            groups.Clear();
            _index = null;
        }

        [NonSerialized] private Dictionary<(Mesh, Vector3), Group> _index;

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Draw(cam, false);
        }

        /// <summary>
        /// Queues this frame's instances around a camera. With <paramref name="onlyFor"/>, they're drawn for that
        /// camera alone, so a camera rendered by hand (style frames, captures) gets them on its next Render().
        /// </summary>
        public void Draw(Camera cam, bool onlyFor)
        {
            Vector3 eye = cam.transform.position;
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            float limit = drawDistance * drawDistance;
            ShadowCastingMode shadows = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (Group group in groups)
            {
                if (group.mesh == null || group.matrices.Count == 0) continue;
                Vector3 d = group.cellCentre - eye;
                d.y = 0f;
                if (d.sqrMagnitude > limit) continue;
                // Off screen: skip. (Groups built before bounds existed have none and are always drawn.)
                if (group.bounds.size != Vector3.zero && !GeometryUtility.TestPlanesAABB(_planes, group.bounds)) continue;
                bool far = group.farMesh != null && d.sqrMagnitude > farDistance * farDistance;
                Mesh mesh = far ? group.farMesh : group.mesh;
                Material[] materials = far ? group.farMaterials : group.materials;
                for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
                {
                    ShadowCastingMode cast = shadows == ShadowCastingMode.On && d.sqrMagnitude < shadowDistance * shadowDistance ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    var rp = new RenderParams(materials[sub]) { shadowCastingMode = cast, receiveShadows = true, camera = onlyFor ? cam : null };
                    for (int start = 0; start < group.matrices.Count; start += _batch.Length)
                    {
                        int count = Mathf.Min(_batch.Length, group.matrices.Count - start);
                        group.matrices.CopyTo(start, _batch, 0, count);
                        Graphics.RenderMeshInstanced(rp, mesh, sub, _batch, count);
                    }
                }
            }
        }
    }
}

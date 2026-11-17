using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hearthline.Game
{
    /// <summary>
    /// Draws thousands of small things (grass clumps, flowers, ferns) with GPU instancing instead of one
    /// GameObject each. Scene builders fill it; it has no colliders and holds no game state.
    /// Groups beyond <see cref="drawDistance"/> from the camera are skipped in cells.
    /// </summary>
    [ExecuteAlways]
    public sealed class InstancedScatter : MonoBehaviour
    {
        [Serializable]
        public sealed class Group
        {
            public Mesh mesh;
            public Material[] materials = new Material[0];
            public Vector3 cellCentre;
            public List<Matrix4x4> matrices = new List<Matrix4x4>();
        }

        [Tooltip("Cells further than this from the camera are not drawn (metres).")]
        [SerializeField] private float drawDistance = 140f;
        [SerializeField] private bool castShadows;
        [SerializeField] private List<Group> groups = new List<Group>();

        private readonly Matrix4x4[] _batch = new Matrix4x4[1023];

        public int InstanceCount
        {
            get { int n = 0; foreach (Group g in groups) n += g.matrices.Count; return n; }
        }

        /// <summary>Used by scene builders. Instances are bucketed into cells of <paramref name="cellSize"/> metres.</summary>
        public void Add(Mesh mesh, Material[] materials, Matrix4x4 matrix, float cellSize = 50f)
        {
            Vector3 p = matrix.GetColumn(3);
            var cell = new Vector3(Mathf.Floor(p.x / cellSize) * cellSize + cellSize / 2f, 0f, Mathf.Floor(p.z / cellSize) * cellSize + cellSize / 2f);
            Group group = groups.Find(g => g.mesh == mesh && g.cellCentre == cell);
            if (group == null)
            {
                group = new Group { mesh = mesh, materials = materials, cellCentre = cell };
                groups.Add(group);
            }
            group.matrices.Add(matrix);
        }

        public void Clear() => groups.Clear();

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 eye = cam.transform.position;
            float limit = drawDistance * drawDistance;
            ShadowCastingMode shadows = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (Group group in groups)
            {
                if (group.mesh == null || group.matrices.Count == 0) continue;
                Vector3 d = group.cellCentre - eye;
                d.y = 0f;
                if (d.sqrMagnitude > limit) continue;
                for (int sub = 0; sub < group.mesh.subMeshCount && sub < group.materials.Length; sub++)
                {
                    var rp = new RenderParams(group.materials[sub]) { shadowCastingMode = shadows, receiveShadows = true };
                    for (int start = 0; start < group.matrices.Count; start += _batch.Length)
                    {
                        int count = Mathf.Min(_batch.Length, group.matrices.Count - start);
                        group.matrices.CopyTo(start, _batch, 0, count);
                        Graphics.RenderMeshInstanced(rp, group.mesh, sub, _batch, count);
                    }
                }
            }
        }
    }
}

using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Something you can take cover behind (combat spec, sticky cover). The scene builder puts one on each
    /// solid prop and tree trunk that is tall enough, next to its collider. The shape is a footprint on the
    /// ground with a height:
    /// - a **box** (walls, barriers, wrecks, logs, rocks, buildings): its faces are what you press against,
    ///   its corners are where you lean out;
    /// - a **ring** (tree trunks, barrels): you can lean out on either side anywhere around it.
    ///
    /// Low cover (under <see cref="HighCoverHeight"/>) is crouched behind and risen over. High cover is stood
    /// behind and leaned around. The player and the enemies use the same surfaces.
    /// </summary>
    public sealed class CoverSurface : MonoBehaviour
    {
        public enum Shape { Box, Ring }

        /// <summary>Cover shorter than this is crouched behind; taller is stood behind.</summary>
        public const float HighCoverHeight = 1.35f;
        /// <summary>Anything lower doesn't hide a crouching person.</summary>
        public const float MinCoverHeight = 0.6f;

        [SerializeField] private Shape shape;
        [SerializeField] private Vector3 centre;     // world, at ground level
        [SerializeField] private float yaw;          // degrees, box only
        [SerializeField] private Vector2 halfSize;   // box: half width (local x) and half depth (local z)
        [SerializeField] private float radius;       // ring only
        [SerializeField] private float height;

        public Shape Kind => shape;
        public float Height => height;
        public bool Low => height < HighCoverHeight;
        public Vector3 Centre => centre;
        /// <summary>Box: half width and half depth. Ring: the radius twice.</summary>
        public Vector2 HalfSize => shape == Shape.Box ? halfSize : new Vector2(radius, radius);
        public float Yaw => yaw;

        public void ConfigureBox(Vector3 groundCentre, float yawDegrees, Vector2 halfExtents, float coverHeight)
        {
            shape = Shape.Box; centre = groundCentre; yaw = yawDegrees; halfSize = halfExtents; height = coverHeight;
        }

        public void ConfigureRing(Vector3 groundCentre, float ringRadius, float coverHeight)
        {
            shape = Shape.Ring; centre = groundCentre; radius = ringRadius; height = coverHeight;
        }

        /// <summary>A place on the cover's outline, seen from one side.</summary>
        public struct Spot
        {
            /// <summary>On the outline, at ground height of the cover.</summary>
            public Vector3 Point;
            /// <summary>Horizontal, pointing away from the cover (toward whoever is behind it).</summary>
            public Vector3 Normal;
            /// <summary>Box: which face (0..3). Ring: -1.</summary>
            public int Face;
            /// <summary>Box: distance from this spot to the face's left and right corners, as seen facing the cover.</summary>
            public float ToLeftEdge, ToRightEdge;
        }

        /// <summary>The point on the outline nearest to <paramref name="from"/> (on its side of the cover).</summary>
        public Spot Nearest(Vector3 from)
        {
            Vector3 d = from - centre;
            d.y = 0f;
            if (shape == Shape.Ring)
            {
                Vector3 n = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
                return new Spot { Point = centre + n * radius, Normal = n, Face = -1, ToLeftEdge = float.MaxValue, ToRightEdge = float.MaxValue };
            }
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 local = Quaternion.Inverse(rot) * d;
            // Which face: the one the point is furthest beyond, in units of each half-size.
            float ox = Mathf.Abs(local.x) - halfSize.x, oz = Mathf.Abs(local.z) - halfSize.y;
            int face;
            if (ox > oz) face = local.x >= 0f ? 0 : 2; else face = local.z >= 0f ? 1 : 3;
            return OnFace(face, local, rot);
        }

        /// <summary>The nearest point on one face (box only), for sliding along it without rounding the corner.</summary>
        public Spot OnFace(int face, Vector3 from)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 d = from - centre;
            d.y = 0f;
            return OnFace(face, Quaternion.Inverse(rot) * d, rot);
        }

        private Spot OnFace(int face, Vector3 local, Quaternion rot)
        {
            bool sideFace = face == 0 || face == 2;              // faces across x; they run along z
            float half = sideFace ? halfSize.y : halfSize.x;
            float along = Mathf.Clamp(sideFace ? local.z : local.x, -half, half);
            Vector3 n = face == 0 ? Vector3.right : face == 2 ? Vector3.left : face == 1 ? Vector3.forward : Vector3.back;
            Vector3 tangent = sideFace ? Vector3.forward : Vector3.right;
            Vector3 onFace = n * (sideFace ? halfSize.x : halfSize.y);
            Vector3 worldN = rot * n;
            Vector3 worldP = centre + rot * (onFace + tangent * along);
            // Facing the cover means facing -n, so the right-hand direction is cross(up, -n).
            Vector3 right = Vector3.Cross(Vector3.up, -worldN);
            bool plusIsRight = Vector3.Dot(rot * tangent, right) > 0f;
            float toPlus = half - along, toMinus = half + along;
            return new Spot
            {
                Point = worldP, Normal = worldN, Face = face,
                ToRightEdge = plusIsRight ? toPlus : toMinus,
                ToLeftEdge = plusIsRight ? toMinus : toPlus,
            };
        }

        /// <summary>The cover piece a collider belongs to, if it is cover.</summary>
        public static CoverSurface Of(Collider c) => c == null ? null : c.GetComponent<CoverSurface>() ?? c.GetComponentInParent<CoverSurface>();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Low ? Color.yellow : Color.cyan;
            if (shape == Shape.Ring) { Gizmos.DrawWireSphere(centre + Vector3.up * height / 2f, radius); return; }
            Gizmos.matrix = Matrix4x4.TRS(centre + Vector3.up * height / 2f, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfSize.x * 2f, height, halfSize.y * 2f));
        }
    }
}

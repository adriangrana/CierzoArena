using UnityEngine;

namespace CierzoArena.CameraSystem
{
    /// <summary>
    /// Rectangular play-area bounds on the world XZ plane, authored in the inspector
    /// (Milestone 4.2). It is a plain, self-contained MonoBehaviour: it knows nothing
    /// about the camera, the NavMesh, selection, combat or networking, and never runs
    /// global scene searches. The MOBA camera reads these bounds to keep the whole
    /// visible ground region inside the allowed area.
    ///
    /// Internal representation is a single source of truth: the min/max edges on X and
    /// Z. The exposed <see cref="MinX"/> / <see cref="MaxX"/> / <see cref="MinZ"/> /
    /// <see cref="MaxZ"/> are always returned normalized (min &lt;= max) so inverted or
    /// swapped inspector values can never leak out. Center and size are derived on
    /// demand, never stored, so there is no duplicated state to keep in sync.
    ///
    /// Deliberately not included yet: any scene-specific wiring (that belongs to M4.4)
    /// and any coupling to the NavMesh walls (the play area is authored explicitly, not
    /// inferred from navigation geometry).
    /// </summary>
    public sealed class CameraWorldBounds : MonoBehaviour
    {
        [Header("World XZ bounds")]
        [SerializeField] private float minX = -90f;
        [SerializeField] private float maxX = 90f;
        [SerializeField] private float minZ = -90f;
        [SerializeField] private float maxZ = 90f;

        [Header("Gizmos")]
        [Tooltip("Draw the bounds rectangle when the object is selected.")]
        [SerializeField] private bool drawGizmos = true;
        [Tooltip("Height at which the bounds gizmo is drawn (visual only).")]
        [SerializeField] private float gizmoY = 0f;

        /// <summary>Left edge on X, always &lt;= <see cref="MaxX"/>.</summary>
        public float MinX => Mathf.Min(minX, maxX);

        /// <summary>Right edge on X, always &gt;= <see cref="MinX"/>.</summary>
        public float MaxX => Mathf.Max(minX, maxX);

        /// <summary>Near edge on Z, always &lt;= <see cref="MaxZ"/>.</summary>
        public float MinZ => Mathf.Min(minZ, maxZ);

        /// <summary>Far edge on Z, always &gt;= <see cref="MinZ"/>.</summary>
        public float MaxZ => Mathf.Max(minZ, maxZ);

        /// <summary>Center of the rectangle on the XZ plane (x = X, y = Z).</summary>
        public Vector2 Center => new Vector2((MinX + MaxX) * 0.5f, (MinZ + MaxZ) * 0.5f);

        /// <summary>Size of the rectangle on the XZ plane (x = width, y = depth).</summary>
        public Vector2 Size => new Vector2(MaxX - MinX, MaxZ - MinZ);

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.color = new Color(0.24f, 0.86f, 0.95f, 0.6f);
            Vector3 center = new Vector3(Center.x, gizmoY, Center.y);
            Vector3 size = new Vector3(Size.x, 0.1f, Size.y);
            Gizmos.DrawWireCube(center, size);
        }
    }
}

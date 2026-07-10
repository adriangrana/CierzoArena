using UnityEngine;

namespace CierzoArena.CameraSystem
{
    /// <summary>
    /// Visible ground rectangle projected by the camera onto a horizontal plane,
    /// expressed as offsets relative to the camera pivot on the XZ plane. Because the
    /// camera is tilted, the region is not symmetric around the pivot: it reaches much
    /// farther forward (+Z) than backward, so the four edges are stored independently.
    /// </summary>
    public readonly struct CameraGroundOffsets
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float MinZ;
        public readonly float MaxZ;

        public CameraGroundOffsets(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        /// <summary>Midpoint of the visible region on X, relative to the pivot.</summary>
        public float CenterX => (MinX + MaxX) * 0.5f;

        /// <summary>Midpoint of the visible region on Z, relative to the pivot.</summary>
        public float CenterZ => (MinZ + MaxZ) * 0.5f;
    }

    /// <summary>
    /// MOBA camera controller. Milestone 4.1 added free movement (keyboard pan and
    /// edge scrolling); Milestone 4.2 adds limited orthographic zoom and real map
    /// bounds whose clamp accounts for zoom, aspect ratio and the camera's tilt.
    ///
    /// Responsibilities in this slice:
    /// - Keyboard panning on the world XZ plane (WASD / arrows), normalized so a
    ///   diagonal is never faster than a straight movement.
    /// - Optional edge scrolling with a configurable pixel border.
    /// - Limited orthographic zoom driven by the mouse wheel.
    /// - Clamping the pivot so the whole visible ground region stays inside a
    ///   <see cref="CameraWorldBounds"/> rectangle, re-applied every frame after both
    ///   movement and zoom (even with no input).
    /// - Never touches Y or rotation: it only translates on the XZ plane and only
    ///   changes the orthographic size.
    ///
    /// Combination semantics: keyboard and edge scrolling are resolved as two
    /// independent deltas, each with its own configurable speed, and then summed.
    /// Each source direction is independently clamped to magnitude <= 1, so a diagonal
    /// is not faster than a cardinal move. When keyboard and edge point the same way
    /// the two configurable speeds add up on purpose; this is not the accidental
    /// double-speed you would get from summing directions and applying one speed.
    ///
    /// The visible ground region is computed from the camera's real orientation by
    /// intersecting the four viewport-corner rays with a configurable horizontal plane,
    /// so it stays correct across zoom, aspect ratio and small future pitch changes
    /// (no hard-coded tilt angle). The geometry is extracted to pure static functions
    /// (<see cref="ComputeVisibleGroundOffsets"/>, <see cref="ClampPivotToBounds"/>,
    /// <see cref="ClampZoom"/>) that are unit-testable without a running camera or a
    /// real screen resolution.
    ///
    /// Deliberately out of scope for M4.2 (implemented in later slices): local-hero
    /// follow and recenter (M4.3), scene integration and multi-resolution validation
    /// (M4.4), smoothing, and any networking. This controller is a purely local client
    /// concern: a plain MonoBehaviour, no RPCs, not a NetworkBehaviour, never influences
    /// the simulation. It leaves <see cref="IsometricCameraRig"/> untouched.
    /// </summary>
    public sealed class MobaCameraController : MonoBehaviour
    {
        [Header("Keyboard pan")]
        [Tooltip("World units per second for keyboard panning.")]
        [SerializeField, Min(0f)] private float keyboardPanSpeed = 40f;

        [Header("Edge scrolling")]
        [Tooltip("Enable moving the camera when the cursor nears a screen edge.")]
        [SerializeField] private bool edgeScrollingEnabled = true;
        [Tooltip("World units per second for edge scrolling.")]
        [SerializeField, Min(0f)] private float edgePanSpeed = 40f;
        [Tooltip("Thickness in pixels of the active edge-scroll border. 0 disables it.")]
        [SerializeField, Min(0)] private int edgeBorderPixels = 12;

        [Header("Zoom (orthographic size)")]
        [Tooltip("Orthographic-size change per mouse-wheel notch.")]
        [SerializeField, Min(0f)] private float zoomSpeed = 4f;
        [Tooltip("Closest zoom (smallest orthographic size).")]
        [SerializeField, Min(0f)] private float minOrthographicSize = 12f;
        [Tooltip("Farthest zoom (largest orthographic size).")]
        [SerializeField, Min(0f)] private float maxOrthographicSize = 60f;

        [Header("Bounds")]
        [Tooltip("Orthographic camera driven by this controller. Auto-resolved from this GameObject if left empty.")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("Play-area rectangle the visible region must stay inside. Bounds are skipped while empty.")]
        [SerializeField] private CameraWorldBounds worldBounds;
        [Tooltip("World Y of the horizontal plane the visible region is projected onto.")]
        [SerializeField] private float groundPlaneY = 0f;

        private readonly MobaCameraInput input = new MobaCameraInput();

        // Input captured in Update and consumed in LateUpdate (never re-read late).
        private Vector2 frameKeyboardDirection;
        private Vector2 frameEdgeDirection;
        private float frameZoomDelta;

        private void Awake()
        {
            if (targetCamera == null)
            {
                // Local component lookup only (not a global scene search): the
                // controller normally lives on the camera it drives.
                targetCamera = GetComponent<Camera>();
            }

            // Bring a possibly out-of-range serialized zoom into the valid interval up
            // front so the very first frame already shows a limited zoom.
            if (targetCamera != null && targetCamera.orthographic)
            {
                targetCamera.orthographicSize = ClampZoom(targetCamera.orthographicSize, minOrthographicSize, maxOrthographicSize);
            }
        }

        private void Update()
        {
            frameKeyboardDirection = input.ReadKeyboardDirection();
            frameEdgeDirection = edgeScrollingEnabled
                ? input.ReadEdgeDirection(edgeBorderPixels)
                : Vector2.zero;
            frameZoomDelta = input.ReadScrollDelta();
        }

        private void LateUpdate()
        {
            Pan(frameKeyboardDirection, frameEdgeDirection, Time.deltaTime);
            ApplyZoomDelta(frameZoomDelta);
            ClampToBounds();
        }

        /// <summary>
        /// Applies a pan for the given already-captured directions and delta time.
        /// Used by <see cref="LateUpdate"/> with the frame's captured input, and as a
        /// deterministic seam for play-mode validation (no real keyboard/mouse needed).
        /// Only the XZ position changes; Y and rotation are preserved. Bounds are not
        /// applied here so callers can pan then clamp explicitly.
        /// </summary>
        public void Pan(Vector2 keyboardDirection, Vector2 edgeDirection, float deltaTime)
        {
            Vector3 delta = ComputePanDelta(keyboardDirection, keyboardPanSpeed, edgeDirection, edgePanSpeed, deltaTime);
            if (delta != Vector3.zero)
            {
                transform.position += delta;
            }
        }

        /// <summary>
        /// Applies a mouse-wheel impulse to the orthographic size, clamped to the
        /// configured range. Convention (matching the M3A rig): scrolling up (positive
        /// delta) zooms in by reducing the orthographic size; scrolling down zooms out.
        /// No-ops when there is no orthographic camera. Deterministic seam for tests.
        /// </summary>
        public void ApplyZoomDelta(float scrollDelta)
        {
            if (targetCamera == null || !targetCamera.orthographic)
            {
                return;
            }

            float next = targetCamera.orthographicSize - scrollDelta * Mathf.Max(0f, zoomSpeed);
            targetCamera.orthographicSize = ClampZoom(next, minOrthographicSize, maxOrthographicSize);
        }

        /// <summary>
        /// Clamps the pivot on XZ so the whole visible ground region stays inside the
        /// world bounds, taking the current zoom, aspect ratio and camera tilt into
        /// account. Preserves Y and rotation. No-ops when no bounds or no orthographic
        /// camera are wired. Deterministic seam for tests; also called every frame.
        /// </summary>
        public void ClampToBounds()
        {
            if (worldBounds == null || targetCamera == null || !targetCamera.orthographic)
            {
                return;
            }

            float cameraHeight = transform.position.y - groundPlaneY;
            CameraGroundOffsets offsets = ComputeVisibleGroundOffsets(
                transform.rotation,
                targetCamera.orthographicSize,
                targetCamera.aspect,
                cameraHeight);

            transform.position = ClampPivotToBounds(
                transform.position,
                offsets,
                worldBounds.MinX,
                worldBounds.MaxX,
                worldBounds.MinZ,
                worldBounds.MaxZ);
        }

        // ----- Pure logic (unit-testable, no Unity runtime required) ----------

        /// <summary>
        /// Computes the world-space XZ displacement for a frame. Keyboard and edge
        /// contributions are scaled by their own speeds and summed; Y is always zero.
        /// Negative speeds are floored to zero so bad inspector values can never
        /// produce inverted movement. Zero speed, zero delta time or zero input all
        /// yield a zero delta.
        /// </summary>
        public static Vector3 ComputePanDelta(Vector2 keyboardDirection, float keyboardSpeed, Vector2 edgeDirection, float edgeSpeed, float deltaTime)
        {
            Vector2 keyboardDelta = keyboardDirection * (Mathf.Max(0f, keyboardSpeed) * deltaTime);
            Vector2 edgeDelta = edgeDirection * (Mathf.Max(0f, edgeSpeed) * deltaTime);
            Vector2 combined = keyboardDelta + edgeDelta;
            return new Vector3(combined.x, 0f, combined.y);
        }

        /// <summary>
        /// Clamps an orthographic zoom size to the configured range, tolerating an
        /// inverted (min &gt; max) pair by normalizing it first.
        /// </summary>
        public static float ClampZoom(float size, float minSize, float maxSize)
        {
            float lo = Mathf.Min(minSize, maxSize);
            float hi = Mathf.Max(minSize, maxSize);
            return Mathf.Clamp(size, lo, hi);
        }

        /// <summary>
        /// Computes the visible ground rectangle as XZ offsets relative to the camera
        /// pivot, by intersecting the four viewport-corner rays of an orthographic
        /// camera with a horizontal plane at <paramref name="cameraHeight"/> below the
        /// pivot. Uses the camera's real <paramref name="rotation"/> (no hard-coded
        /// tilt), so it stays correct across zoom, aspect ratio and pitch changes.
        ///
        /// The returned Z offsets are asymmetric for a tilted camera (it sees farther
        /// forward than backward). Guards a near-horizontal/upward view, a non-positive
        /// height and NaN by falling back to a symmetric top-down box, so it never
        /// divides by ~0 nor returns NaN.
        /// </summary>
        public static CameraGroundOffsets ComputeVisibleGroundOffsets(Quaternion rotation, float orthographicSize, float aspect, float cameraHeight)
        {
            float halfHeight = Mathf.Max(0f, orthographicSize);
            float halfWidth = halfHeight * Mathf.Max(0f, aspect);

            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;

            // A valid projection needs a downward-looking camera above the plane.
            if (forward.y >= -1e-5f || cameraHeight <= 0f || float.IsNaN(cameraHeight))
            {
                return new CameraGroundOffsets(-halfWidth, halfWidth, -halfHeight, halfHeight);
            }

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    // Corner ray origin relative to the pivot (orthographic: all rays
                    // are parallel to 'forward', only laterally offset).
                    Vector3 origin = right * (sx * halfWidth) + up * (sy * halfHeight);

                    // Distance along 'forward' to reach the ground plane. The pivot is
                    // 'cameraHeight' above the plane; the corner adds 'origin.y'.
                    float t = (cameraHeight + origin.y) / -forward.y;

                    float x = origin.x + forward.x * t;
                    float z = origin.z + forward.z * t;

                    if (x < minX) { minX = x; }
                    if (x > maxX) { maxX = x; }
                    if (z < minZ) { minZ = z; }
                    if (z > maxZ) { maxZ = z; }
                }
            }

            return new CameraGroundOffsets(minX, maxX, minZ, maxZ);
        }

        /// <summary>
        /// Clamps the pivot on XZ so that, for the given visible offsets, the whole
        /// visible region stays inside the (possibly inverted) bounds. On an axis where
        /// the visible region is wider than the bounds, the region is centered on that
        /// axis instead of oscillating between the two edges. Y is preserved.
        /// </summary>
        public static Vector3 ClampPivotToBounds(Vector3 pivot, CameraGroundOffsets offsets, float boundsMinX, float boundsMaxX, float boundsMinZ, float boundsMaxZ)
        {
            pivot.x = ClampPivotAxis(pivot.x, offsets.MinX, offsets.MaxX, boundsMinX, boundsMaxX);
            pivot.z = ClampPivotAxis(pivot.z, offsets.MinZ, offsets.MaxZ, boundsMinZ, boundsMaxZ);
            return pivot;
        }

        private static float ClampPivotAxis(float pivot, float visibleMin, float visibleMax, float boundsMin, float boundsMax)
        {
            float bMin = Mathf.Min(boundsMin, boundsMax);
            float bMax = Mathf.Max(boundsMin, boundsMax);

            // pivot + visibleMin >= bMin  ->  pivot >= bMin - visibleMin (lower)
            // pivot + visibleMax <= bMax  ->  pivot <= bMax - visibleMax (upper)
            float lower = bMin - visibleMin;
            float upper = bMax - visibleMax;

            if (lower > upper)
            {
                // Visible region wider than the bounds on this axis: center it so both
                // overshoots are equal and the result never flips between the edges.
                float boundsCenter = (bMin + bMax) * 0.5f;
                float visibleCenter = (visibleMin + visibleMax) * 0.5f;
                return boundsCenter - visibleCenter;
            }

            return Mathf.Clamp(pivot, lower, upper);
        }
    }
}

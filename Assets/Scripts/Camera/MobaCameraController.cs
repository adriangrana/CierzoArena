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
    /// Camera tracking intent. This is the <em>desired</em> mode, not just the current
    /// behavior: <see cref="FollowHero"/> means "keep the local hero centered", and it
    /// stays the intent even while no hero exists yet, so the camera starts following
    /// automatically as soon as one is registered. <see cref="Free"/> means the player
    /// took manual control and the camera must not snap back on its own.
    /// </summary>
    public enum CameraTrackingMode
    {
        FollowHero,
        Free
    }

    /// <summary>
    /// MOBA camera controller. M4.1 added free movement (keyboard pan and edge
    /// scrolling); M4.2 added limited orthographic zoom and real map bounds whose clamp
    /// accounts for zoom, aspect ratio and the camera's tilt; M4.3 adds local-hero
    /// follow and a configurable recenter key, without the controller ever knowing about
    /// Netcode.
    ///
    /// Tracking semantics (M4.3):
    /// - The controller has two conceptual states, <see cref="CameraTrackingMode"/>.
    ///   It starts in <see cref="CameraTrackingMode.FollowHero"/>.
    /// - The local hero is read from a decoupled <see cref="LocalHeroProvider"/> (a
    ///   Runtime component), never from Netcode. The controller caches the hero via the
    ///   provider's change event, so it does no per-frame global searches.
    /// - In FollowHero, LateUpdate centers the pivot XZ on the hero (Y and rotation
    ///   preserved), then M4.2 bounds are applied, so the hero can sit near an edge and
    ///   the bounds win over perfect centering without jitter.
    /// - Real manual pan input (keyboard or edge, above a small threshold) switches to
    ///   Free immediately. Zoom, a temporarily missing hero, the bounds clamp and aspect
    ///   changes never switch to Free.
    /// - The recenter key (default Space; F is reserved for the M3A rig) switches back to
    ///   FollowHero and recenters this frame if a hero exists.
    /// - Late spawn: registering a hero after several frames starts following it
    ///   automatically (via the provider event), unless the player already chose Free.
    /// - Despawn: if the hero is unregistered or destroyed, the camera keeps its last
    ///   valid position, keeps the FollowHero intent, and resumes following when a new
    ///   local hero is registered. A despawn never forces Free.
    ///
    /// LateUpdate order (deterministic): resolve mode from this frame's input, then move
    /// (follow the hero in FollowHero, or apply pan in Free), then apply zoom, then apply
    /// bounds last so the clamp always uses the final zoom and the camera is never left
    /// outside for a frame.
    ///
    /// The visible ground region is computed from the camera's real orientation by
    /// intersecting the four viewport-corner rays with a configurable horizontal plane,
    /// so it stays correct across zoom, aspect ratio and small future pitch changes
    /// (no hard-coded tilt angle). The geometry and decisions are extracted to pure
    /// static functions (<see cref="ComputeVisibleGroundOffsets"/>,
    /// <see cref="ClampPivotToBounds"/>, <see cref="ClampZoom"/>,
    /// <see cref="ComputeFollowPosition"/>, <see cref="ResolveMode"/>,
    /// <see cref="HasManualPanInput"/>) that are unit-testable without a running camera
    /// or a real screen resolution.
    ///
    /// Deliberately out of scope for M4.3 (later slices): scene integration and
    /// multi-resolution validation (M4.4), smoothing, dead zones, spectator camera,
    /// selected-unit following, target switching and any networking. This controller is
    /// a purely local client concern: a plain MonoBehaviour, no RPCs, not a
    /// NetworkBehaviour, never influences the simulation. It leaves
    /// <see cref="IsometricCameraRig"/> untouched.
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

        [Header("Hero follow (M4.3)")]
        [Tooltip("Decoupled source of the local hero. Follow is skipped while empty.")]
        [SerializeField] private LocalHeroProvider heroProvider;
        [Tooltip("Key that recenters on the local hero and returns to follow. Space by default (F belongs to the M3A rig).")]
        [SerializeField] private KeyCode recenterKey = KeyCode.Space;
        [Tooltip("World XZ offset (x = X, y = Z) added to the hero when following, to frame it on screen. Zero puts the pivot exactly on the hero. For the tilted camera a negative Z pulls the pivot back so the hero appears centered instead of low.")]
        [SerializeField] private Vector2 followPlaneOffset = Vector2.zero;

        // Manual input below this magnitude is treated as noise and never switches to
        // Free, so zero or jittery input cannot drop follow.
        private const float ManualInputThreshold = 1e-3f;

        private readonly MobaCameraInput input = new MobaCameraInput();

        // Input captured in Update and consumed in LateUpdate (never re-read late).
        private Vector2 frameKeyboardDirection;
        private Vector2 frameEdgeDirection;
        private float frameZoomDelta;
        private bool frameRecenterPressed;

        // Desired tracking mode; starts following so a hero registered later is picked
        // up automatically. Cached hero comes from the provider's change event.
        private CameraTrackingMode mode = CameraTrackingMode.FollowHero;
        private Transform cachedHero;

        /// <summary>Current tracking intent, for tests and diagnostics.</summary>
        public CameraTrackingMode Mode => mode;

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

        private void OnEnable()
        {
            SubscribeToProvider(heroProvider);
        }

        private void OnDisable()
        {
            if (heroProvider != null)
            {
                heroProvider.HeroChanged -= OnHeroChanged;
            }
        }

        /// <summary>
        /// Swaps the local-hero provider at runtime (used by scene wiring and tests).
        /// Re-subscribes cleanly and seeds the cached hero from the new provider.
        /// </summary>
        public void SetHeroProvider(LocalHeroProvider provider)
        {
            if (heroProvider == provider)
            {
                return;
            }

            if (heroProvider != null)
            {
                heroProvider.HeroChanged -= OnHeroChanged;
            }

            heroProvider = provider;
            cachedHero = null;
            SubscribeToProvider(heroProvider);
        }

        private void SubscribeToProvider(LocalHeroProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            // Idempotent: safe whether called from OnEnable or SetHeroProvider.
            provider.HeroChanged -= OnHeroChanged;
            provider.HeroChanged += OnHeroChanged;
            cachedHero = provider.CurrentHero;
        }

        private void OnHeroChanged(Transform hero)
        {
            // Late spawn / respawn: caching here means FollowHero starts (or resumes)
            // following on the next LateUpdate without any global search.
            cachedHero = hero;
        }

        private void Update()
        {
            frameKeyboardDirection = input.ReadKeyboardDirection();
            frameEdgeDirection = edgeScrollingEnabled
                ? input.ReadEdgeDirection(edgeBorderPixels)
                : Vector2.zero;
            frameZoomDelta = input.ReadScrollDelta();
            frameRecenterPressed = input.ReadRecenterPressed(recenterKey);
        }

        private void LateUpdate()
        {
            // 1. Resolve the desired mode from this frame's input. Recenter wins;
            //    otherwise real manual pan switches to Free; otherwise mode is kept.
            bool hasManualInput = HasManualPanInput(frameKeyboardDirection, frameEdgeDirection, ManualInputThreshold);
            mode = ResolveMode(mode, hasManualInput, frameRecenterPressed);

            // 2. Move according to the resolved mode.
            if (mode == CameraTrackingMode.Free)
            {
                Pan(frameKeyboardDirection, frameEdgeDirection, Time.deltaTime);
            }
            else if (cachedHero != null)
            {
                // Follow after the hero has moved (this is LateUpdate). Missing hero:
                // keep the last position, do not touch a destroyed transform.
                transform.position = ComputeFollowPosition(transform.position, cachedHero.position, followPlaneOffset);
            }

            // 3. Zoom, then 4. bounds last so the clamp uses the final zoom.
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

        /// <summary>
        /// Switches to <see cref="CameraTrackingMode.FollowHero"/> and, if a local hero
        /// exists, recenters on it this call and re-applies bounds (Y and rotation
        /// preserved). With no hero it only sets the intent and never throws, so the
        /// camera starts following once a hero is registered later. Deterministic seam
        /// for the recenter key and for tests.
        /// </summary>
        public void RecenterOnHero()
        {
            mode = CameraTrackingMode.FollowHero;
            if (cachedHero != null)
            {
                transform.position = ComputeFollowPosition(transform.position, cachedHero.position, followPlaneOffset);
                ClampToBounds();
            }
        }

        /// <summary>
        /// Applies a deterministic manual pan: real input switches to
        /// <see cref="CameraTrackingMode.Free"/>, then the pan is applied and bounds are
        /// re-applied. Used by play-mode tests to simulate manual control without real
        /// keyboard/mouse. Zero input neither moves the camera nor changes the mode.
        /// </summary>
        public void ApplyManualPan(Vector2 keyboardDirection, Vector2 edgeDirection, float deltaTime)
        {
            if (HasManualPanInput(keyboardDirection, edgeDirection, ManualInputThreshold))
            {
                mode = CameraTrackingMode.Free;
            }

            Pan(keyboardDirection, edgeDirection, deltaTime);
            ClampToBounds();
        }

        // ----- Pure logic (unit-testable, no Unity runtime required) ----------

        /// <summary>
        /// True when the combined manual pan input exceeds the noise threshold. Keyboard
        /// and edge are summed first so opposite inputs that cancel out do not count as
        /// manual control.
        /// </summary>
        public static bool HasManualPanInput(Vector2 keyboardDirection, Vector2 edgeDirection, float threshold)
        {
            return (keyboardDirection + edgeDirection).sqrMagnitude > threshold * threshold;
        }

        /// <summary>
        /// Resolves the next tracking mode. Recenter always wins and forces
        /// <see cref="CameraTrackingMode.FollowHero"/>; otherwise real manual pan input
        /// forces <see cref="CameraTrackingMode.Free"/>; otherwise the mode is unchanged.
        /// Mirrors the M3A rig's mode resolution so behavior stays consistent.
        /// </summary>
        public static CameraTrackingMode ResolveMode(CameraTrackingMode current, bool hasManualInput, bool recenterPressed)
        {
            if (recenterPressed)
            {
                return CameraTrackingMode.FollowHero;
            }

            if (hasManualInput)
            {
                return CameraTrackingMode.Free;
            }

            return current;
        }

        /// <summary>
        /// Computes the follow pivot: the hero's XZ with the camera's own Y preserved.
        /// Rotation is never involved, so following can only translate on the plane.
        /// </summary>
        public static Vector3 ComputeFollowPosition(Vector3 pivot, Vector3 heroPosition)
        {
            return new Vector3(heroPosition.x, pivot.y, heroPosition.z);
        }

        /// <summary>
        /// Follow pivot with a framing offset on the XZ plane (x = X, y = Z) added to
        /// the hero, keeping the camera's own Y. A zero offset places the pivot exactly
        /// on the hero (the M4.3 behavior); a negative Z offset pulls the pivot back so
        /// the tilted camera frames the hero centered instead of near the bottom edge.
        /// </summary>
        public static Vector3 ComputeFollowPosition(Vector3 pivot, Vector3 heroPosition, Vector2 planeOffset)
        {
            Vector3 followed = ComputeFollowPosition(pivot, heroPosition);
            followed.x += planeOffset.x;
            followed.z += planeOffset.y;
            return followed;
        }

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

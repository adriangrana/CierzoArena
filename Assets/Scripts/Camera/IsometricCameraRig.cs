using UnityEngine;

namespace CierzoArena.CameraSystem
{
    public enum CameraFollowMode
    {
        Follow,
        Free
    }

    /// <summary>
    /// Technical isometric camera rig for prototyping a large MOBA map. It is
    /// intentionally self-contained: it knows nothing about selection, orders,
    /// combat, health or networking. It only reads input and moves/zooms an
    /// orthographic camera while keeping a fixed isometric angle.
    ///
    /// Behaviour:
    /// - Follow mode: the focus point tracks a target transform.
    /// - Free mode: the player pans the focus with WASD/arrows; this switches out of
    ///   follow so the two never fight.
    /// - Recenter key snaps back to the target and re-enables follow.
    /// - Mouse wheel zooms by changing the orthographic size (clamped), which keeps
    ///   the isometric angle and can never clip through the ground.
    /// - The focus point is clamped to a rectangular XZ bounds.
    ///
    /// The serialized field names <c>target</c>, <c>offset</c> and
    /// <c>followSharpness</c> are preserved from the previous simple rig so existing
    /// scenes keep their wiring without reserialization. The decision/clamp math is
    /// exposed as pure static methods for unit testing without a running camera.
    /// </summary>
    public sealed class IsometricCameraRig : MonoBehaviour
    {
        [Header("Target / framing")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -12f);
        [SerializeField] private float followSharpness = 8f;
        [SerializeField] private Vector3 isoEuler = new Vector3(55f, 0f, 0f);

        [Header("Free pan")]
        [SerializeField] private float panSpeed = 20f;
        [SerializeField] private float panInputThreshold = 0.01f;
        [SerializeField] private KeyCode recenterKey = KeyCode.F;

        [Header("Zoom (orthographic size)")]
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 18f;
        [SerializeField] private float zoomStep = 1.5f;

        [Header("Bounds (world XZ)")]
        [SerializeField] private float minX = -40f;
        [SerializeField] private float maxX = 40f;
        [SerializeField] private float minZ = -40f;
        [SerializeField] private float maxZ = 40f;

        [Header("Edge pan (optional)")]
        [SerializeField] private bool edgePanEnabled = false;
        [SerializeField] private float edgePanBorder = 12f;

        private Camera cameraComponent;
        private Vector3 focusPoint;
        private CameraFollowMode mode = CameraFollowMode.Follow;

        public CameraFollowMode Mode => mode;
        public Vector3 FocusPoint => focusPoint;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                focusPoint = ClampToBounds(target.position, minX, maxX, minZ, maxZ);
                mode = CameraFollowMode.Follow;
            }
        }

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();

            // Seed the focus point from the target if present, otherwise infer it from
            // the current camera placement so the view does not jump on start.
            focusPoint = target != null ? target.position : transform.position - offset;
            focusPoint = ClampToBounds(focusPoint, minX, maxX, minZ, maxZ);
            ApplyImmediate();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            Vector2 panInput = ReadPanInput();
            if (edgePanEnabled)
            {
                panInput += ReadEdgePanInput();
            }

            bool recenterPressed = Input.GetKeyDown(recenterKey);
            bool hasPanInput = panInput.sqrMagnitude > (panInputThreshold * panInputThreshold);

            mode = NextMode(mode, hasPanInput, recenterPressed);

            if (recenterPressed && target != null)
            {
                focusPoint = target.position;
            }

            if (mode == CameraFollowMode.Free || target == null)
            {
                // World XZ pan. With zero yaw the camera's right/forward map to world
                // +X/+Z, so this stays intuitive regardless of the pitch angle.
                Vector3 delta = new Vector3(panInput.x, 0f, panInput.y) * (panSpeed * dt);
                focusPoint += delta;
            }
            else
            {
                focusPoint = Vector3.Lerp(focusPoint, target.position, 1f - Mathf.Exp(-followSharpness * dt));
            }

            ApplyZoom(Input.mouseScrollDelta.y);

            focusPoint = ClampToBounds(focusPoint, minX, maxX, minZ, maxZ);
            ApplyImmediate();
        }

        private static Vector2 ReadPanInput()
        {
            // GetAxisRaw covers WASD and arrow keys via Unity's default input axes and
            // avoids smoothing; robust enough for a prototype.
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        private Vector2 ReadEdgePanInput()
        {
            Vector3 mouse = Input.mousePosition;
            float x = 0f;
            float y = 0f;

            if (mouse.x <= edgePanBorder) { x = -1f; }
            else if (mouse.x >= Screen.width - edgePanBorder) { x = 1f; }

            if (mouse.y <= edgePanBorder) { y = -1f; }
            else if (mouse.y >= Screen.height - edgePanBorder) { y = 1f; }

            return new Vector2(x, y);
        }

        private void ApplyZoom(float scrollDelta)
        {
            if (cameraComponent == null || !cameraComponent.orthographic || Mathf.Approximately(scrollDelta, 0f))
            {
                return;
            }

            // The wheel is a discrete impulse, so apply a fixed step per notch (no
            // Time.deltaTime) for consistent, perceptible zoom independent of framerate.
            float next = cameraComponent.orthographicSize - Mathf.Sign(scrollDelta) * zoomStep;
            cameraComponent.orthographicSize = ClampZoom(next, minZoom, maxZoom);
        }

        private void ApplyImmediate()
        {
            transform.rotation = Quaternion.Euler(isoEuler);
            transform.position = focusPoint + offset;
        }

        // ----- Pure logic (unit-testable, no Unity runtime required) ----------

        /// <summary>
        /// Clamps a focus point to the rectangular XZ bounds. Y is left untouched.
        /// </summary>
        public static Vector3 ClampToBounds(Vector3 focus, float minX, float maxX, float minZ, float maxZ)
        {
            focus.x = Mathf.Clamp(focus.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
            focus.z = Mathf.Clamp(focus.z, Mathf.Min(minZ, maxZ), Mathf.Max(minZ, maxZ));
            return focus;
        }

        /// <summary>
        /// Clamps an orthographic zoom size to the configured range.
        /// </summary>
        public static float ClampZoom(float size, float minZoom, float maxZoom)
        {
            return Mathf.Clamp(size, Mathf.Min(minZoom, maxZoom), Mathf.Max(minZoom, maxZoom));
        }

        /// <summary>
        /// Resolves the next follow/free mode. Recenter always wins and forces Follow;
        /// otherwise any manual pan switches to Free; otherwise the mode is unchanged.
        /// </summary>
        public static CameraFollowMode NextMode(CameraFollowMode current, bool hasPanInput, bool recenterPressed)
        {
            if (recenterPressed)
            {
                return CameraFollowMode.Follow;
            }

            if (hasPanInput)
            {
                return CameraFollowMode.Free;
            }

            return current;
        }

        private void OnDrawGizmosSelected()
        {
            // Cheap visual aid for authoring the play area bounds.
            Gizmos.color = new Color(0.95f, 0.86f, 0.24f, 0.6f);
            Vector3 center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            Vector3 size = new Vector3(Mathf.Abs(maxX - minX), 0.1f, Mathf.Abs(maxZ - minZ));
            Gizmos.DrawWireCube(center, size);
        }
    }
}

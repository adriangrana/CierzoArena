using UnityEngine;

namespace CierzoArena.CameraSystem
{
    /// <summary>
    /// Free-moving MOBA camera controller (Milestone 4.1 — free movement only).
    ///
    /// Responsibilities in this slice:
    /// - Keyboard panning on the world XZ plane (WASD / arrows), normalized so a
    ///   diagonal is never faster than a straight movement.
    /// - Optional edge scrolling with a configurable pixel border.
    /// - Frame-rate independent movement (scaled by <see cref="Time.deltaTime"/>).
    /// - Never touches Y or rotation: it only translates on the XZ plane.
    ///
    /// Combination semantics: keyboard and edge scrolling are resolved as two
    /// independent deltas, each with its own configurable speed, and then summed.
    /// Each source direction is independently clamped to magnitude <= 1, so a diagonal
    /// is not faster than a cardinal move. When keyboard and edge point the same way
    /// the two configurable speeds add up on purpose; this is not the accidental
    /// double-speed you would get from summing directions and applying one speed.
    ///
    /// Deliberately out of scope for M4.1 (implemented in later slices):
    /// - Map bounds/clamping. The real clamp must account for zoom, aspect ratio and
    ///   the camera's tilt, so it is implemented directly in M4.2 rather than as a
    ///   throwaway rectangle here.
    /// - Zoom (M4.2), local-hero follow and recenter (M4.3).
    ///
    /// This controller is a purely local client concern: it is a plain MonoBehaviour,
    /// sends no RPCs, is not a NetworkBehaviour and never influences the simulation.
    /// It leaves <see cref="IsometricCameraRig"/> untouched.
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

        private readonly MobaCameraInput input = new MobaCameraInput();

        // Input captured in Update and consumed in LateUpdate (never re-read late).
        private Vector2 frameKeyboardDirection;
        private Vector2 frameEdgeDirection;

        private void Update()
        {
            frameKeyboardDirection = input.ReadKeyboardDirection();
            frameEdgeDirection = edgeScrollingEnabled
                ? input.ReadEdgeDirection(edgeBorderPixels)
                : Vector2.zero;
        }

        private void LateUpdate()
        {
            Pan(frameKeyboardDirection, frameEdgeDirection, Time.deltaTime);
        }

        /// <summary>
        /// Applies a pan for the given already-captured directions and delta time.
        /// Used by <see cref="LateUpdate"/> with the frame's captured input, and as a
        /// deterministic seam for play-mode validation (no real keyboard/mouse needed).
        /// Only the XZ position changes; Y and rotation are preserved.
        /// </summary>
        public void Pan(Vector2 keyboardDirection, Vector2 edgeDirection, float deltaTime)
        {
            Vector3 delta = ComputePanDelta(keyboardDirection, keyboardPanSpeed, edgeDirection, edgePanSpeed, deltaTime);
            if (delta != Vector3.zero)
            {
                transform.position += delta;
            }
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
    }
}

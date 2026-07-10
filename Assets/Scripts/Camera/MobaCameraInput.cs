using UnityEngine;

namespace CierzoArena.CameraSystem
{
    /// <summary>
    /// Encapsulates the legacy Input Manager reads for the MOBA camera (M4.1) so the
    /// controller never touches <see cref="Input"/>, <see cref="Screen"/> or
    /// <see cref="Application"/> directly. The runtime-dependent reads are instance
    /// methods; the direction math is exposed as pure static functions that can be
    /// unit-tested without a running player.
    ///
    /// Keyboard uses the project's existing "Horizontal"/"Vertical" axes, which are
    /// already bound to both WASD (alt buttons a/d, s/w) and the arrow keys. GetAxisRaw
    /// returns a clean -1/0/1 with no smoothing, which is what a camera pan wants.
    /// </summary>
    public sealed class MobaCameraInput
    {
        /// <summary>
        /// Reads the raw keyboard pan direction, clamped so a diagonal is never faster
        /// than a straight movement (magnitude never exceeds 1).
        /// </summary>
        public Vector2 ReadKeyboardDirection()
        {
            Vector2 raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            return LimitMagnitude(raw, 1f);
        }

        /// <summary>
        /// Reads the edge-scroll direction from the mouse position. Returns zero when
        /// the application is not focused; the pure geometry (borders, off-screen
        /// cursor, corners) lives in <see cref="ComputeEdgeDirection"/>.
        /// </summary>
        public Vector2 ReadEdgeDirection(int borderPixels)
        {
            // Focus guard stays in the input layer: a non-focused window must never
            // scroll (e.g. cursor parked at a screen edge while alt-tabbed away).
            if (!Application.isFocused)
            {
                return Vector2.zero;
            }

            return ComputeEdgeDirection(Input.mousePosition, Screen.width, Screen.height, borderPixels);
        }

        /// <summary>
        /// Reads the mouse wheel delta for zooming. Positive means the wheel was
        /// scrolled up. Kept here so the controller never touches the legacy
        /// <see cref="Input"/> API directly. The wheel is a discrete impulse, so the
        /// controller applies it without <see cref="Time.deltaTime"/>.
        /// </summary>
        public float ReadScrollDelta()
        {
            return Input.mouseScrollDelta.y;
        }

        /// <summary>
        /// Returns true on the frame the recenter key is pressed. Kept here so the
        /// controller never touches the legacy <see cref="Input"/> API directly. The
        /// key is configurable (defaulting to Space at the controller); F is avoided
        /// because it belongs to the M3A technical camera.
        /// </summary>
        public bool ReadRecenterPressed(KeyCode recenterKey)
        {
            return Input.GetKeyDown(recenterKey);
        }

        // ----- Pure logic (unit-testable, no Unity runtime required) ----------

        /// <summary>
        /// Scales a vector down to <paramref name="maxMagnitude"/> if it exceeds it,
        /// otherwise returns it unchanged. A sub-maximum input is never amplified, and
        /// a zero input stays zero (no NaN from normalizing a zero vector).
        /// </summary>
        public static Vector2 LimitMagnitude(Vector2 value, float maxMagnitude)
        {
            if (maxMagnitude <= 0f)
            {
                return Vector2.zero;
            }

            float sqr = value.sqrMagnitude;
            if (sqr <= maxMagnitude * maxMagnitude)
            {
                return value;
            }

            return value.normalized * maxMagnitude;
        }

        /// <summary>
        /// Computes the edge-scroll direction for a mouse position within a screen of
        /// the given size. Returns zero when disabled by a non-positive border, an
        /// invalid screen size, or a cursor outside the [0,width] x [0,height] area.
        /// Corners return a normalized diagonal so they are not faster than an edge.
        /// The screen coordinate convention matches Unity: origin bottom-left, y up.
        /// </summary>
        public static Vector2 ComputeEdgeDirection(Vector2 mousePosition, int screenWidth, int screenHeight, int borderPixels)
        {
            if (borderPixels <= 0 || screenWidth <= 0 || screenHeight <= 0)
            {
                return Vector2.zero;
            }

            if (mousePosition.x < 0f || mousePosition.x > screenWidth ||
                mousePosition.y < 0f || mousePosition.y > screenHeight)
            {
                return Vector2.zero;
            }

            float x = 0f;
            float y = 0f;

            if (mousePosition.x <= borderPixels) { x = -1f; }
            else if (mousePosition.x >= screenWidth - borderPixels) { x = 1f; }

            if (mousePosition.y <= borderPixels) { y = -1f; }
            else if (mousePosition.y >= screenHeight - borderPixels) { y = 1f; }

            Vector2 direction = new Vector2(x, y);
            return LimitMagnitude(direction, 1f);
        }
    }
}

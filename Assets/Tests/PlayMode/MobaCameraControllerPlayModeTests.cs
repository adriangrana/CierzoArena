using System.Collections;
using System.Reflection;
using CierzoArena.CameraSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    /// <summary>
    /// Minimal runtime validation for the MOBA camera. M4.1 confirms free movement on
    /// XZ preserving Y and rotation; M4.2 confirms orthographic zoom stays within
    /// limits and that the bounds clamp keeps the whole visible region inside the play
    /// area across zoom and aspect-ratio changes. Movement, zoom and clamping are
    /// driven through the deterministic <see cref="MobaCameraController.Pan"/>,
    /// <see cref="MobaCameraController.ApplyZoomDelta"/> and
    /// <see cref="MobaCameraController.ClampToBounds"/> seams, so no real keyboard/mouse
    /// input is required. No persistent scene is modified.
    /// </summary>
    public sealed class MobaCameraControllerPlayModeTests
    {
        private GameObject cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (cameraObject != null)
            {
                Object.Destroy(cameraObject);
            }
        }

        [UnityTest]
        public IEnumerator ControllerInitializesAndPansOnXZPreservingYAndRotation()
        {
            cameraObject = new GameObject("MOBA Camera (test)");
            Vector3 startPosition = new Vector3(3f, 25f, -7f);
            Quaternion startRotation = Quaternion.Euler(55f, 0f, 0f);
            cameraObject.transform.SetPositionAndRotation(startPosition, startRotation);

            MobaCameraController controller = cameraObject.AddComponent<MobaCameraController>();

            // Let Awake/Update/LateUpdate run once with no input (no keys, edge off in
            // practice): the component must not move the camera or throw.
            yield return null;

            Assert.That(cameraObject.transform.position, Is.EqualTo(startPosition));
            Assert.That(Quaternion.Angle(cameraObject.transform.rotation, startRotation), Is.LessThan(0.01f));

            // Drive a deterministic pan: keyboard +X and edge +Z, one second.
            controller.Pan(new Vector2(1f, 0f), new Vector2(0f, 1f), 1f);

            Vector3 moved = cameraObject.transform.position;
            Assert.That(moved.x, Is.GreaterThan(startPosition.x), "Camera should move on +X.");
            Assert.That(moved.z, Is.GreaterThan(startPosition.z), "Camera should move on +Z.");
            Assert.That(moved.y, Is.EqualTo(startPosition.y), "Y must be preserved.");
            Assert.That(Quaternion.Angle(cameraObject.transform.rotation, startRotation), Is.LessThan(0.01f), "Rotation must be preserved.");
        }

        [UnityTest]
        public IEnumerator PanWithZeroDirectionsDoesNotMoveTheCamera()
        {
            cameraObject = new GameObject("MOBA Camera (test)");
            Vector3 startPosition = new Vector3(-10f, 30f, 12f);
            cameraObject.transform.position = startPosition;

            MobaCameraController controller = cameraObject.AddComponent<MobaCameraController>();
            yield return null;

            controller.Pan(Vector2.zero, Vector2.zero, 1f);

            Assert.That(cameraObject.transform.position, Is.EqualTo(startPosition));
        }

        // ----- M4.2: zoom limits and bounds clamp ----------------------------

        [UnityTest]
        public IEnumerator ZoomStaysWithinLimitsAndOutsidePositionIsClamped()
        {
            MobaCameraController controller = BuildRig(new Vector3(500f, 25f, 500f), 30f, 16f / 9f, 15f, 40f);
            Camera camera = cameraObject.GetComponent<Camera>();
            Quaternion startRotation = cameraObject.transform.rotation;

            // Zoom out hard: clamps to the maximum size.
            controller.ApplyZoomDelta(-1000f);
            Assert.That(camera.orthographicSize, Is.EqualTo(40f).Within(0.001f));

            // Zoom in hard: clamps to the minimum size.
            controller.ApplyZoomDelta(1000f);
            Assert.That(camera.orthographicSize, Is.EqualTo(15f).Within(0.001f));

            // Back to a mid size and clamp the far-outside position.
            camera.orthographicSize = 30f;
            controller.ApplyZoomDelta(0f);
            controller.ClampToBounds();

            Vector3 clamped = cameraObject.transform.position;
            Assert.That(clamped.x, Is.LessThan(500f), "An outside pivot must be pulled back inside.");
            Assert.That(clamped.y, Is.EqualTo(25f).Within(0.001f), "Y must be preserved.");
            Assert.That(Quaternion.Angle(cameraObject.transform.rotation, startRotation), Is.LessThan(0.01f), "Rotation must be preserved.");

            AssertVisibleRegionValidForBounds(controller, camera);
            yield return null; // A live frame must not throw without real input.
        }

        [UnityTest]
        public IEnumerator ChangingAspectOrSizeStillYieldsAValidClampedPosition()
        {
            MobaCameraController controller = BuildRig(new Vector3(400f, 25f, -400f), 30f, 16f / 9f, 15f, 60f);
            Camera camera = cameraObject.GetComponent<Camera>();

            controller.ClampToBounds();
            AssertVisibleRegionValidForBounds(controller, camera);

            // Ultrawide aspect: wider visible region, still fully inside the map.
            camera.aspect = 21f / 9f;
            cameraObject.transform.position = new Vector3(400f, 25f, -400f);
            controller.ClampToBounds();
            AssertVisibleRegionValidForBounds(controller, camera);

            // Larger orthographic size: the visible region becomes wider than the map
            // on X, so the axis must center instead of fitting inside (deterministic
            // viewport-larger-than-map behavior). The helper checks each axis case.
            camera.orthographicSize = 45f;
            cameraObject.transform.position = new Vector3(400f, 25f, 400f);
            controller.ClampToBounds();
            AssertVisibleRegionValidForBounds(controller, camera);

            yield return null; // No exceptions on a real frame without input.
        }

        // ----- Helpers -------------------------------------------------------

        private const float BoundsMin = -90f;
        private const float BoundsMax = 90f;

        private MobaCameraController BuildRig(Vector3 position, float orthographicSize, float aspect, float minZoom, float maxZoom)
        {
            cameraObject = new GameObject("MOBA Camera (test)");
            cameraObject.transform.SetPositionAndRotation(position, Quaternion.Euler(55f, 0f, 0f));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.aspect = aspect;

            CameraWorldBounds bounds = cameraObject.AddComponent<CameraWorldBounds>();
            SetPrivateField(bounds, "minX", BoundsMin);
            SetPrivateField(bounds, "maxX", BoundsMax);
            SetPrivateField(bounds, "minZ", BoundsMin);
            SetPrivateField(bounds, "maxZ", BoundsMax);

            MobaCameraController controller = cameraObject.AddComponent<MobaCameraController>();
            SetPrivateField(controller, "targetCamera", camera);
            SetPrivateField(controller, "worldBounds", bounds);
            SetPrivateField(controller, "groundPlaneY", 0f);
            SetPrivateField(controller, "minOrthographicSize", minZoom);
            SetPrivateField(controller, "maxOrthographicSize", maxZoom);
            SetPrivateField(controller, "zoomSpeed", 4f);
            SetPrivateField(controller, "edgeScrollingEnabled", false);
            return controller;
        }

        private static void AssertVisibleRegionValidForBounds(MobaCameraController controller, Camera camera)
        {
            Vector3 pivot = controller.transform.position;
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(
                controller.transform.rotation, camera.orthographicSize, camera.aspect, pivot.y);

            const float tolerance = 0.05f;
            float boundsWidth = BoundsMax - BoundsMin;
            float boundsDepth = BoundsMax - BoundsMin;
            float boundsCenter = (BoundsMin + BoundsMax) * 0.5f;

            // X axis: fits inside -> both edges within bounds; otherwise the region is
            // wider than the map and must be centered (deterministic behavior).
            if (offsets.MaxX - offsets.MinX <= boundsWidth + tolerance)
            {
                Assert.That(pivot.x + offsets.MinX, Is.GreaterThanOrEqualTo(BoundsMin - tolerance), "Left visible edge outside bounds.");
                Assert.That(pivot.x + offsets.MaxX, Is.LessThanOrEqualTo(BoundsMax + tolerance), "Right visible edge outside bounds.");
            }
            else
            {
                Assert.That(pivot.x + offsets.CenterX, Is.EqualTo(boundsCenter).Within(tolerance), "Region wider than map must center X.");
            }

            // Z axis: same fit-or-center logic.
            if (offsets.MaxZ - offsets.MinZ <= boundsDepth + tolerance)
            {
                Assert.That(pivot.z + offsets.MinZ, Is.GreaterThanOrEqualTo(BoundsMin - tolerance), "Near visible edge outside bounds.");
                Assert.That(pivot.z + offsets.MaxZ, Is.LessThanOrEqualTo(BoundsMax + tolerance), "Far visible edge outside bounds.");
            }
            else
            {
                Assert.That(pivot.z + offsets.CenterZ, Is.EqualTo(boundsCenter).Within(tolerance), "Region deeper than map must center Z.");
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Serialized field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}

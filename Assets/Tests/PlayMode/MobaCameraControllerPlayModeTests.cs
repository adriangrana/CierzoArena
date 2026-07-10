using System.Collections;
using CierzoArena.CameraSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    /// <summary>
    /// Minimal runtime validation for the MOBA camera (M4.1). It creates a bare
    /// GameObject, adds <see cref="MobaCameraController"/> and drives movement through
    /// the deterministic <see cref="MobaCameraController.Pan"/> seam, so no real
    /// keyboard/mouse input is required. It confirms the component initializes without
    /// exceptions, translates on XZ, preserves Y and rotation, works without a follow
    /// target and works with edge scrolling disabled.
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
    }
}

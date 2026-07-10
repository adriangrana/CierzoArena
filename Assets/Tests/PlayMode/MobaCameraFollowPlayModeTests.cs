using System.Collections;
using System.Reflection;
using CierzoArena.CameraSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    /// <summary>
    /// M4.3 runtime validation for local-hero follow and recenter. It builds a tilted
    /// orthographic camera with <see cref="MobaCameraController"/>,
    /// <see cref="CameraWorldBounds"/> and a <see cref="LocalHeroProvider"/>, plus a
    /// plain GameObject as the hero. Real per-frame follow is exercised through the
    /// component's own LateUpdate (real keyboard/mouse read zero in tests), while manual
    /// control and recenter use the deterministic
    /// <see cref="MobaCameraController.ApplyManualPan"/> and
    /// <see cref="MobaCameraController.RecenterOnHero"/> seams. No persistent scene is
    /// modified and no real input is simulated.
    /// </summary>
    public sealed class MobaCameraFollowPlayModeTests
    {
        private const float BoundsMin = -90f;
        private const float BoundsMax = 90f;

        private GameObject cameraObject;
        private GameObject providerObject;
        private GameObject heroObject;

        [TearDown]
        public void TearDown()
        {
            if (cameraObject != null) { Object.Destroy(cameraObject); }
            if (providerObject != null) { Object.Destroy(providerObject); }
            if (heroObject != null) { Object.Destroy(heroObject); }
        }

        [UnityTest]
        public IEnumerator CameraCreatedBeforeHeroDoesNotThrow()
        {
            MobaCameraController controller = BuildRig(new Vector3(0f, 25f, 0f));
            // Several frames with no hero and no input: must not throw and must not move.
            Vector3 start = controller.transform.position;
            yield return null;
            yield return null;
            Assert.That(controller.transform.position, Is.EqualTo(start));
        }

        [UnityTest]
        public IEnumerator RegisteringHeroLaterStartsFollowing()
        {
            MobaCameraController controller = BuildRig(new Vector3(0f, 25f, 0f));
            LocalHeroProvider provider = providerObject.GetComponent<LocalHeroProvider>();
            yield return null;

            heroObject = CreateHero(new Vector3(20f, 0f, -15f));
            provider.Register(heroObject.transform);
            yield return null; // LateUpdate should follow now.

            Assert.That(controller.transform.position.x, Is.EqualTo(20f).Within(0.01f));
            Assert.That(controller.transform.position.z, Is.EqualTo(-15f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator MovingHeroUpdatesCameraInLateUpdate()
        {
            MobaCameraController controller = BuildRigWithHero(new Vector3(0f, 25f, 0f), new Vector3(10f, 0f, 10f));
            yield return null;

            heroObject.transform.position = new Vector3(-30f, 0f, 25f);
            yield return null;

            Assert.That(controller.transform.position.x, Is.EqualTo(-30f).Within(0.01f));
            Assert.That(controller.transform.position.z, Is.EqualTo(25f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ManualMovementSwitchesToFree()
        {
            MobaCameraController controller = BuildRigWithHero(new Vector3(0f, 25f, 0f), new Vector3(10f, 0f, 10f));
            yield return null;

            controller.ApplyManualPan(new Vector2(1f, 0f), Vector2.zero, 1f);

            Assert.That(controller.Mode, Is.EqualTo(CameraTrackingMode.Free));
            yield return null; // A live frame in Free with zero real input must not throw.
        }

        [UnityTest]
        public IEnumerator InFreeModeMovingHeroDoesNotDragCamera()
        {
            MobaCameraController controller = BuildRigWithHero(new Vector3(0f, 25f, 0f), new Vector3(10f, 0f, 10f));
            yield return null;

            controller.ApplyManualPan(new Vector2(1f, 0f), Vector2.zero, 1f);
            Vector3 afterManual = controller.transform.position;

            heroObject.transform.position = new Vector3(-40f, 0f, -40f);
            yield return null; // Free: the camera must not jump to the hero.

            Assert.That(controller.transform.position, Is.EqualTo(afterManual));
        }

        [UnityTest]
        public IEnumerator RecenterReturnsToFollow()
        {
            MobaCameraController controller = BuildRigWithHero(new Vector3(0f, 25f, 0f), new Vector3(10f, 0f, 10f));
            yield return null;

            controller.ApplyManualPan(new Vector2(1f, 0f), Vector2.zero, 1f);
            Assert.That(controller.Mode, Is.EqualTo(CameraTrackingMode.Free));

            heroObject.transform.position = new Vector3(35f, 0f, -20f);
            controller.RecenterOnHero();

            Assert.That(controller.Mode, Is.EqualTo(CameraTrackingMode.FollowHero));
            Assert.That(controller.transform.position.x, Is.EqualTo(35f).Within(0.01f));
            Assert.That(controller.transform.position.z, Is.EqualTo(-20f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator DestroyingHeroKeepsLastPosition()
        {
            MobaCameraController controller = BuildRigWithHero(new Vector3(0f, 25f, 0f), new Vector3(18f, 0f, -22f));
            yield return null;
            Vector3 lastPosition = controller.transform.position;

            Object.Destroy(heroObject);
            heroObject = null;
            yield return null; // Destroyed transform must not be followed nor throw.

            Assert.That(controller.transform.position, Is.EqualTo(lastPosition));
        }

        [UnityTest]
        public IEnumerator RegisteringAnotherHeroFollowsTheNewTarget()
        {
            MobaCameraController controller = BuildRigWithHero(new Vector3(0f, 25f, 0f), new Vector3(18f, 0f, -22f));
            LocalHeroProvider provider = providerObject.GetComponent<LocalHeroProvider>();
            yield return null;

            GameObject secondHero = CreateHero(new Vector3(-45f, 0f, 40f));
            provider.Register(secondHero.transform);
            yield return null;

            Assert.That(controller.transform.position.x, Is.EqualTo(-45f).Within(0.01f));
            Assert.That(controller.transform.position.z, Is.EqualTo(40f).Within(0.01f));
            Object.Destroy(secondHero);
        }

        [UnityTest]
        public IEnumerator YAndRotationStayConstantThroughFollowAndRecenter()
        {
            MobaCameraController controller = BuildRigWithHero(new Vector3(0f, 25f, 0f), new Vector3(12f, 0f, 12f));
            Quaternion startRotation = controller.transform.rotation;
            yield return null;

            heroObject.transform.position = new Vector3(-20f, 0f, 30f);
            yield return null;
            controller.ApplyManualPan(new Vector2(0f, 1f), Vector2.zero, 1f);
            controller.RecenterOnHero();

            Assert.That(controller.transform.position.y, Is.EqualTo(25f).Within(0.001f));
            Assert.That(Quaternion.Angle(controller.transform.rotation, startRotation), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator BoundsApplyDuringFollowAndRecenter()
        {
            MobaCameraController controller = BuildRig(new Vector3(0f, 25f, 0f));
            LocalHeroProvider provider = providerObject.GetComponent<LocalHeroProvider>();
            Camera camera = cameraObject.GetComponent<Camera>();

            // Hero far past a corner: follow must clamp the visible region inside bounds.
            heroObject = CreateHero(new Vector3(500f, 0f, 500f));
            provider.Register(heroObject.transform);
            yield return null;
            AssertVisibleRegionInsideBounds(controller, camera);

            controller.RecenterOnHero();
            AssertVisibleRegionInsideBounds(controller, camera);
        }

        // ----- Helpers -------------------------------------------------------

        private MobaCameraController BuildRig(Vector3 position)
        {
            cameraObject = new GameObject("MOBA Camera (test)");
            cameraObject.transform.SetPositionAndRotation(position, Quaternion.Euler(55f, 0f, 0f));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.aspect = 16f / 9f;

            CameraWorldBounds bounds = cameraObject.AddComponent<CameraWorldBounds>();
            SetPrivateField(bounds, "minX", BoundsMin);
            SetPrivateField(bounds, "maxX", BoundsMax);
            SetPrivateField(bounds, "minZ", BoundsMin);
            SetPrivateField(bounds, "maxZ", BoundsMax);

            providerObject = new GameObject("LocalHeroProvider (test)");
            LocalHeroProvider provider = providerObject.AddComponent<LocalHeroProvider>();

            MobaCameraController controller = cameraObject.AddComponent<MobaCameraController>();
            SetPrivateField(controller, "targetCamera", camera);
            SetPrivateField(controller, "worldBounds", bounds);
            SetPrivateField(controller, "groundPlaneY", 0f);
            SetPrivateField(controller, "minOrthographicSize", 8f);
            SetPrivateField(controller, "maxOrthographicSize", 30f);
            SetPrivateField(controller, "edgeScrollingEnabled", false);
            controller.SetHeroProvider(provider);
            return controller;
        }

        private MobaCameraController BuildRigWithHero(Vector3 cameraPosition, Vector3 heroPosition)
        {
            MobaCameraController controller = BuildRig(cameraPosition);
            LocalHeroProvider provider = providerObject.GetComponent<LocalHeroProvider>();
            heroObject = CreateHero(heroPosition);
            provider.Register(heroObject.transform);
            return controller;
        }

        private static GameObject CreateHero(Vector3 position)
        {
            GameObject hero = new GameObject("Hero (test)");
            hero.transform.position = position;
            return hero;
        }

        private static void AssertVisibleRegionInsideBounds(MobaCameraController controller, Camera camera)
        {
            Vector3 pivot = controller.transform.position;
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(
                controller.transform.rotation, camera.orthographicSize, camera.aspect, pivot.y);

            const float tolerance = 0.05f;
            Assert.That(pivot.x + offsets.MinX, Is.GreaterThanOrEqualTo(BoundsMin - tolerance), "Left visible edge outside bounds.");
            Assert.That(pivot.x + offsets.MaxX, Is.LessThanOrEqualTo(BoundsMax + tolerance), "Right visible edge outside bounds.");
            Assert.That(pivot.z + offsets.MinZ, Is.GreaterThanOrEqualTo(BoundsMin - tolerance), "Near visible edge outside bounds.");
            Assert.That(pivot.z + offsets.MaxZ, Is.LessThanOrEqualTo(BoundsMax + tolerance), "Far visible edge outside bounds.");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Serialized field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}

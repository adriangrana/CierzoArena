using CierzoArena.CameraSystem;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    /// <summary>
    /// Pure-logic coverage for the technical isometric camera (M3A). Only the
    /// input-independent decision and clamp math is tested; real keyboard/mouse
    /// input and per-frame movement are validated manually in the editor.
    /// </summary>
    public sealed class IsometricCameraRigTests
    {
        [Test]
        public void ClampToBoundsKeepsFocusInsideRectangle()
        {
            Vector3 inside = IsometricCameraRig.ClampToBounds(new Vector3(5f, 3f, -5f), -10f, 10f, -10f, 10f);
            Assert.That(inside, Is.EqualTo(new Vector3(5f, 3f, -5f)));

            Vector3 clamped = IsometricCameraRig.ClampToBounds(new Vector3(50f, 3f, -50f), -10f, 10f, -10f, 10f);
            Assert.That(clamped.x, Is.EqualTo(10f));
            Assert.That(clamped.z, Is.EqualTo(-10f));

            // Y must never be altered by XZ bounds clamping.
            Assert.That(clamped.y, Is.EqualTo(3f));
        }

        [Test]
        public void ClampToBoundsToleratesInvertedBounds()
        {
            // Even if min/max are provided swapped, clamping stays well-defined.
            Vector3 clamped = IsometricCameraRig.ClampToBounds(new Vector3(999f, 0f, -999f), 10f, -10f, 10f, -10f);
            Assert.That(clamped.x, Is.EqualTo(10f));
            Assert.That(clamped.z, Is.EqualTo(-10f));
        }

        [Test]
        public void ClampZoomRespectsRange()
        {
            Assert.That(IsometricCameraRig.ClampZoom(9f, 5f, 18f), Is.EqualTo(9f));
            Assert.That(IsometricCameraRig.ClampZoom(2f, 5f, 18f), Is.EqualTo(5f));
            Assert.That(IsometricCameraRig.ClampZoom(99f, 5f, 18f), Is.EqualTo(18f));
        }

        [Test]
        public void NextModeRecenterAlwaysForcesFollow()
        {
            // Recenter wins even if the player is panning at the same time.
            Assert.That(IsometricCameraRig.NextMode(CameraFollowMode.Free, hasPanInput: true, recenterPressed: true),
                Is.EqualTo(CameraFollowMode.Follow));
        }

        [Test]
        public void NextModePanSwitchesToFree()
        {
            Assert.That(IsometricCameraRig.NextMode(CameraFollowMode.Follow, hasPanInput: true, recenterPressed: false),
                Is.EqualTo(CameraFollowMode.Free));
        }

        [Test]
        public void NextModeKeepsCurrentWhenIdle()
        {
            Assert.That(IsometricCameraRig.NextMode(CameraFollowMode.Follow, hasPanInput: false, recenterPressed: false),
                Is.EqualTo(CameraFollowMode.Follow));
            Assert.That(IsometricCameraRig.NextMode(CameraFollowMode.Free, hasPanInput: false, recenterPressed: false),
                Is.EqualTo(CameraFollowMode.Free));
        }
    }
}

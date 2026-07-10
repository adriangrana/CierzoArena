using CierzoArena.CameraSystem;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    /// <summary>
    /// Pure-logic coverage for the MOBA camera (M4.1 — free movement). Only the
    /// input-independent direction and displacement math is tested here; real
    /// keyboard/mouse input, focus and per-frame movement are validated in play mode
    /// and manually. Tests never depend on the real screen size or mouse position.
    /// </summary>
    public sealed class MobaCameraControllerTests
    {
        private const float Tolerance = 1e-4f;

        // ----- Direction normalization ---------------------------------------

        [Test]
        public void DiagonalKeyboardDirectionIsNormalizedToMagnitudeOne()
        {
            Vector2 limited = MobaCameraInput.LimitMagnitude(new Vector2(1f, 1f), 1f);
            Assert.That(limited.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void ZeroDirectionStaysZeroWithoutNaN()
        {
            Vector2 limited = MobaCameraInput.LimitMagnitude(Vector2.zero, 1f);
            Assert.That(limited, Is.EqualTo(Vector2.zero));
            Assert.That(float.IsNaN(limited.x), Is.False);
            Assert.That(float.IsNaN(limited.y), Is.False);
        }

        [Test]
        public void SubMaximumDirectionIsNotAmplified()
        {
            Vector2 input = new Vector2(0.3f, 0f);
            Vector2 limited = MobaCameraInput.LimitMagnitude(input, 1f);
            Assert.That(limited, Is.EqualTo(input));
        }

        // ----- Edge direction geometry ---------------------------------------

        [Test]
        public void LeftEdgeProducesNegativeX()
        {
            Vector2 dir = MobaCameraInput.ComputeEdgeDirection(new Vector2(5f, 400f), 1920, 1080, 12);
            Assert.That(dir.x, Is.LessThan(0f));
            Assert.That(dir.y, Is.EqualTo(0f));
        }

        [Test]
        public void RightEdgeProducesPositiveX()
        {
            Vector2 dir = MobaCameraInput.ComputeEdgeDirection(new Vector2(1915f, 400f), 1920, 1080, 12);
            Assert.That(dir.x, Is.GreaterThan(0f));
            Assert.That(dir.y, Is.EqualTo(0f));
        }

        [Test]
        public void BottomEdgeProducesNegativeY()
        {
            Vector2 dir = MobaCameraInput.ComputeEdgeDirection(new Vector2(960f, 5f), 1920, 1080, 12);
            Assert.That(dir.y, Is.LessThan(0f));
            Assert.That(dir.x, Is.EqualTo(0f));
        }

        [Test]
        public void TopEdgeProducesPositiveY()
        {
            Vector2 dir = MobaCameraInput.ComputeEdgeDirection(new Vector2(960f, 1075f), 1920, 1080, 12);
            Assert.That(dir.y, Is.GreaterThan(0f));
            Assert.That(dir.x, Is.EqualTo(0f));
        }

        [Test]
        public void CornerProducesNormalizedDiagonal()
        {
            Vector2 dir = MobaCameraInput.ComputeEdgeDirection(new Vector2(2f, 2f), 1920, 1080, 12);
            Assert.That(dir.x, Is.LessThan(0f));
            Assert.That(dir.y, Is.LessThan(0f));
            Assert.That(dir.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void CursorOutsideScreenProducesZero()
        {
            Vector2 outside = MobaCameraInput.ComputeEdgeDirection(new Vector2(-5f, 400f), 1920, 1080, 12);
            Assert.That(outside, Is.EqualTo(Vector2.zero));

            Vector2 above = MobaCameraInput.ComputeEdgeDirection(new Vector2(960f, 2000f), 1920, 1080, 12);
            Assert.That(above, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ZeroBorderProducesZero()
        {
            Vector2 dir = MobaCameraInput.ComputeEdgeDirection(new Vector2(0f, 0f), 1920, 1080, 0);
            Assert.That(dir, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void InvalidScreenSizeProducesZero()
        {
            Vector2 dir = MobaCameraInput.ComputeEdgeDirection(new Vector2(0f, 0f), 0, 0, 12);
            Assert.That(dir, Is.EqualTo(Vector2.zero));
        }

        // ----- Pan delta ------------------------------------------------------

        [Test]
        public void PanDeltaScalesLinearlyWithSpeedAndDeltaTime()
        {
            Vector3 baseDelta = MobaCameraController.ComputePanDelta(new Vector2(1f, 0f), 10f, Vector2.zero, 0f, 1f);
            Assert.That(baseDelta, Is.EqualTo(new Vector3(10f, 0f, 0f)));

            Vector3 doubleSpeed = MobaCameraController.ComputePanDelta(new Vector2(1f, 0f), 20f, Vector2.zero, 0f, 1f);
            Assert.That(doubleSpeed, Is.EqualTo(new Vector3(20f, 0f, 0f)));

            Vector3 doubleDt = MobaCameraController.ComputePanDelta(new Vector2(1f, 0f), 10f, Vector2.zero, 0f, 2f);
            Assert.That(doubleDt, Is.EqualTo(new Vector3(20f, 0f, 0f)));
        }

        [Test]
        public void PanDeltaZeroSpeedProducesZero()
        {
            Vector3 delta = MobaCameraController.ComputePanDelta(new Vector2(1f, 1f), 0f, new Vector2(1f, 1f), 0f, 1f);
            Assert.That(delta, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PanDeltaZeroDeltaTimeProducesZero()
        {
            Vector3 delta = MobaCameraController.ComputePanDelta(new Vector2(1f, 1f), 40f, new Vector2(1f, 1f), 40f, 0f);
            Assert.That(delta, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PanDeltaZeroInputProducesZero()
        {
            Vector3 delta = MobaCameraController.ComputePanDelta(Vector2.zero, 40f, Vector2.zero, 40f, 1f);
            Assert.That(delta, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PanDeltaKeepsYAtZeroAndMapsVerticalToWorldZ()
        {
            Vector3 delta = MobaCameraController.ComputePanDelta(new Vector2(0f, 1f), 10f, Vector2.zero, 0f, 1f);
            Assert.That(delta.x, Is.EqualTo(0f));
            Assert.That(delta.y, Is.EqualTo(0f));
            Assert.That(delta.z, Is.EqualTo(10f));
        }

        [Test]
        public void PanDeltaSumsKeyboardAndEdgeWithTheirOwnSpeeds()
        {
            // Both point -X; the two configurable speeds add up on purpose.
            Vector3 delta = MobaCameraController.ComputePanDelta(new Vector2(-1f, 0f), 10f, new Vector2(-1f, 0f), 5f, 1f);
            Assert.That(delta, Is.EqualTo(new Vector3(-15f, 0f, 0f)));
        }

        [Test]
        public void PanDeltaNegativeSpeedNeverInvertsMovement()
        {
            Vector3 delta = MobaCameraController.ComputePanDelta(new Vector2(1f, 0f), -50f, Vector2.zero, 0f, 1f);
            Assert.That(delta, Is.EqualTo(Vector3.zero));
        }
    }
}

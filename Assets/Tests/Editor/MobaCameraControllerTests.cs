using CierzoArena.CameraSystem;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    /// <summary>
    /// Pure-logic coverage for the MOBA camera. M4.1 covers free-movement direction
    /// and displacement math; M4.2 adds orthographic zoom clamping, the tilted-camera
    /// visible-ground geometry and the bounds clamp. Only input-independent math is
    /// tested here; real keyboard/mouse input, focus and per-frame movement are
    /// validated in play mode and manually. Tests never depend on the real screen size,
    /// mouse position or window resolution.
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

        // ----- Zoom clamp (M4.2) ---------------------------------------------

        [Test]
        public void ZoomBelowMinimumClampsToMinimum()
        {
            Assert.That(MobaCameraController.ClampZoom(5f, 12f, 60f), Is.EqualTo(12f).Within(Tolerance));
        }

        [Test]
        public void ZoomAboveMaximumClampsToMaximum()
        {
            Assert.That(MobaCameraController.ClampZoom(100f, 12f, 60f), Is.EqualTo(60f).Within(Tolerance));
        }

        [Test]
        public void InvertedZoomLimitsAreNormalized()
        {
            // min/max swapped: they must still behave as the same [12, 60] interval.
            Assert.That(MobaCameraController.ClampZoom(5f, 60f, 12f), Is.EqualTo(12f).Within(Tolerance));
            Assert.That(MobaCameraController.ClampZoom(100f, 60f, 12f), Is.EqualTo(60f).Within(Tolerance));
            Assert.That(MobaCameraController.ClampZoom(30f, 60f, 12f), Is.EqualTo(30f).Within(Tolerance));
        }

        [Test]
        public void ValidZoomIsUnchanged()
        {
            Assert.That(MobaCameraController.ClampZoom(30f, 12f, 60f), Is.EqualTo(30f).Within(Tolerance));
        }

        // ----- Visible ground geometry (M4.2) --------------------------------

        [Test]
        public void LargerAspectWidensHorizontalExtent()
        {
            CameraGroundOffsets narrow = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, 1f, 25f);
            CameraGroundOffsets wide = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, 2f, 25f);
            Assert.That(wide.MaxX - wide.MinX, Is.GreaterThan(narrow.MaxX - narrow.MinX));
        }

        [Test]
        public void LargerOrthographicSizeWidensAllExtents()
        {
            CameraGroundOffsets small = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            CameraGroundOffsets big = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 20f, Aspect, 25f);
            Assert.That(big.MaxX - big.MinX, Is.GreaterThan(small.MaxX - small.MinX));
            Assert.That(big.MaxZ - big.MinZ, Is.GreaterThan(small.MaxZ - small.MinZ));
        }

        [Test]
        public void TiltProducesAsymmetricZOffsets()
        {
            // For a downward tilt of angle t the Z offsets are
            //   MaxZ = h*cot(t) + size/sin(t),  MinZ = h*cot(t) - size/sin(t),
            // both shifted forward by h*cot(t) > 0, so the region is not symmetric
            // around the pivot: the camera sees farther forward than backward.
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            Assert.That(offsets.CenterZ, Is.GreaterThan(1f));
            Assert.That(Mathf.Abs(Mathf.Abs(offsets.MaxZ) - Mathf.Abs(offsets.MinZ)), Is.GreaterThan(GeomTolerance));
        }

        // ----- Clamp against bounds (M4.2) -----------------------------------

        [Test]
        public void ClampKeepsLeftVisibleEdgeInsideBounds()
        {
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            Vector3 clamped = ClampFull(new Vector3(-1000f, 25f, 0f), offsets);
            Assert.That(clamped.x + offsets.MinX, Is.EqualTo(BoundsMin).Within(GeomTolerance));
        }

        [Test]
        public void ClampKeepsRightVisibleEdgeInsideBounds()
        {
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            Vector3 clamped = ClampFull(new Vector3(1000f, 25f, 0f), offsets);
            Assert.That(clamped.x + offsets.MaxX, Is.EqualTo(BoundsMax).Within(GeomTolerance));
        }

        [Test]
        public void ClampKeepsBottomVisibleEdgeInsideBounds()
        {
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            Vector3 clamped = ClampFull(new Vector3(0f, 25f, -1000f), offsets);
            Assert.That(clamped.z + offsets.MinZ, Is.EqualTo(BoundsMin).Within(GeomTolerance));
        }

        [Test]
        public void ClampKeepsTopVisibleEdgeInsideBounds()
        {
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            Vector3 clamped = ClampFull(new Vector3(0f, 25f, 1000f), offsets);
            Assert.That(clamped.z + offsets.MaxZ, Is.EqualTo(BoundsMax).Within(GeomTolerance));
        }

        [Test]
        public void ClampWorksInACorner()
        {
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            Vector3 clamped = ClampFull(new Vector3(1000f, 25f, 1000f), offsets);
            Assert.That(clamped.x + offsets.MaxX, Is.LessThanOrEqualTo(BoundsMax + GeomTolerance));
            Assert.That(clamped.z + offsets.MaxZ, Is.LessThanOrEqualTo(BoundsMax + GeomTolerance));
            Assert.That(clamped.x + offsets.MinX, Is.GreaterThanOrEqualTo(BoundsMin - GeomTolerance));
            Assert.That(clamped.z + offsets.MinZ, Is.GreaterThanOrEqualTo(BoundsMin - GeomTolerance));
        }

        [Test]
        public void ViewportWiderThanMapCentersX()
        {
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            // Bounds much narrower on X than the visible width: X must center, not flip.
            Vector3 clamped = MobaCameraController.ClampPivotToBounds(new Vector3(100f, 25f, 0f), offsets, -5f, 5f, BoundsMin, BoundsMax);
            Assert.That(clamped.x + offsets.CenterX, Is.EqualTo(0f).Within(GeomTolerance));
        }

        [Test]
        public void ViewportDeeperThanMapCentersZ()
        {
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            // Bounds much shallower on Z than the visible depth: Z must center.
            Vector3 clamped = MobaCameraController.ClampPivotToBounds(new Vector3(0f, 25f, 100f), offsets, BoundsMin, BoundsMax, -5f, 5f);
            Assert.That(clamped.z + offsets.CenterZ, Is.EqualTo(0f).Within(GeomTolerance));
        }

        [Test]
        public void ClampPreservesAValidPosition()
        {
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            Vector3 valid = new Vector3(0f, 25f, 10f);
            Vector3 clamped = ClampFull(valid, offsets);
            Assert.That(clamped.x, Is.EqualTo(valid.x).Within(GeomTolerance));
            Assert.That(clamped.y, Is.EqualTo(valid.y).Within(GeomTolerance));
            Assert.That(clamped.z, Is.EqualTo(valid.z).Within(GeomTolerance));
        }

        [Test]
        public void InvalidInputsDoNotProduceNaN()
        {
            CameraGroundOffsets negativeHeight = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, -5f);
            AssertNoNaN(negativeHeight);

            CameraGroundOffsets nanHeight = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, float.NaN);
            AssertNoNaN(nanHeight);

            CameraGroundOffsets lookingUp = MobaCameraController.ComputeVisibleGroundOffsets(Quaternion.Euler(-55f, 0f, 0f), 10f, Aspect, 25f);
            AssertNoNaN(lookingUp);

            Vector3 clamped = ClampFull(new Vector3(1000f, 25f, 1000f), negativeHeight);
            Assert.That(float.IsNaN(clamped.x), Is.False);
            Assert.That(float.IsNaN(clamped.z), Is.False);
        }

        [Test]
        public void ChangingAspectChangesHorizontalClamp()
        {
            CameraGroundOffsets narrow = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, 1f, 25f);
            CameraGroundOffsets wide = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, 2f, 25f);
            Vector3 clampNarrow = ClampFull(new Vector3(1000f, 25f, 0f), narrow);
            Vector3 clampWide = ClampFull(new Vector3(1000f, 25f, 0f), wide);
            // A wider viewport must be pushed farther from the right edge.
            Assert.That(clampNarrow.x, Is.GreaterThan(clampWide.x + GeomTolerance));
        }

        [Test]
        public void ChangingZoomChangesAllowedPivotRange()
        {
            CameraGroundOffsets small = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 10f, Aspect, 25f);
            CameraGroundOffsets big = MobaCameraController.ComputeVisibleGroundOffsets(Tilt, 25f, Aspect, 25f);
            Vector3 clampSmall = ClampFull(new Vector3(1000f, 25f, 0f), small);
            Vector3 clampBig = ClampFull(new Vector3(1000f, 25f, 0f), big);
            // Zooming out (larger size) shrinks how far the pivot may travel.
            Assert.That(clampSmall.x, Is.GreaterThan(clampBig.x + GeomTolerance));
        }

        // ----- Shared test fixtures ------------------------------------------

        private static readonly Quaternion Tilt = Quaternion.Euler(55f, 0f, 0f);
        private const float Aspect = 16f / 9f;
        private const float BoundsMin = -90f;
        private const float BoundsMax = 90f;
        private const float GeomTolerance = 1e-2f;

        private static Vector3 ClampFull(Vector3 pivot, CameraGroundOffsets offsets)
        {
            return MobaCameraController.ClampPivotToBounds(pivot, offsets, BoundsMin, BoundsMax, BoundsMin, BoundsMax);
        }

        private static void AssertNoNaN(CameraGroundOffsets offsets)
        {
            Assert.That(float.IsNaN(offsets.MinX), Is.False);
            Assert.That(float.IsNaN(offsets.MaxX), Is.False);
            Assert.That(float.IsNaN(offsets.MinZ), Is.False);
            Assert.That(float.IsNaN(offsets.MaxZ), Is.False);
        }
    }
}

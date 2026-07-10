using CierzoArena.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    /// <summary>
    /// Pure-logic coverage for the M3B navigation instrumentation. Only the
    /// corner-summing path length helper is tested; live NavMesh pathfinding is
    /// validated manually in the scale-spike scene, not here.
    /// </summary>
    public sealed class NavPathProbeTests
    {
        [Test]
        public void PathLengthOfNullOrShortPathIsZero()
        {
            Assert.That(NavPathProbe.PathLength(null), Is.EqualTo(0f));
            Assert.That(NavPathProbe.PathLength(new Vector3[0]), Is.EqualTo(0f));
            Assert.That(NavPathProbe.PathLength(new[] { new Vector3(3f, 0f, 4f) }), Is.EqualTo(0f));
        }

        [Test]
        public void PathLengthSumsSegmentDistances()
        {
            Vector3[] corners =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(3f, 0f, 4f), // 5
                new Vector3(3f, 0f, 14f) // 10
            };

            Assert.That(NavPathProbe.PathLength(corners), Is.EqualTo(15f).Within(1e-4f));
        }

        [Test]
        public void PathLengthIgnoresVerticalConsistentlyWithCornerData()
        {
            // Corners can carry height; the helper measures true 3D segment length.
            Vector3[] corners =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 3f, 4f) // 3D distance 5
            };

            Assert.That(NavPathProbe.PathLength(corners), Is.EqualTo(5f).Within(1e-4f));
        }
    }
}

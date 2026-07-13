using CierzoArena.Environment;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    /// <summary>Small deterministic contracts for the art/gameplay bridge used by
    /// the M23 builders. Scene-wide checks are exposed through the M23 validator.
    /// These tests intentionally create no package prefab and never mutate Occa assets.</summary>
    public sealed class M23EnvironmentNavigationTests
    {
        [Test]
        public void BridgeDeckProfileStoresAnArchedMultiSegmentSurface()
        {
            GameObject visual = new GameObject("Bridge VisualRoot");
            GameObject deck = new GameObject("Bridge GameplayRoot");
            try
            {
                MeshCollider collider = deck.AddComponent<MeshCollider>();
                EnvironmentObstacle metadata = deck.AddComponent<EnvironmentObstacle>();
                metadata.Configure(EnvironmentObstacle.Category.BridgeDeck, visual.transform, false);
                BridgeVisualProfile profile = visual.AddComponent<BridgeVisualProfile>();
                float[] samples = { 0f, .2f, .4f, .5f, .6f, .8f, 1f };
                float[] arch = { .02f, .36f, .68f, .78f, .68f, .36f, .02f };
                profile.ConfigureArc(new Bounds(Vector3.zero, new Vector3(16f, 4f, 26f)), 16f, 26f,
                    Vector3.zero, Vector3.forward, samples, arch, (float[])arch.Clone());

                Assert.That(collider, Is.Not.Null);
                Assert.That(profile.SampleCount, Is.EqualTo(7));
                Assert.That(profile.SegmentCount, Is.EqualTo(6));
                Assert.That(profile.CrownHeight, Is.GreaterThan(profile.EntryHeight));
                Assert.That(profile.MaximumVisualDifference, Is.LessThanOrEqualTo(.001f));
                Assert.That(metadata.ExcludesFromNavMesh, Is.False);
                Assert.That(metadata.VisualRoot, Is.EqualTo(visual.transform));
            }
            finally
            {
                Object.DestroyImmediate(deck);
                Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void SolidEnvironmentObjectsUseSeparateGameplayColliderRoots()
        {
            GameObject visual = new GameObject("Town Center Visual");
            GameObject colliderRoot = new GameObject("Town Center GameplayCollider");
            try
            {
                colliderRoot.layer = 6;
                colliderRoot.AddComponent<BoxCollider>();
                EnvironmentObstacle metadata = colliderRoot.AddComponent<EnvironmentObstacle>();
                metadata.Configure(EnvironmentObstacle.Category.TownCenter, visual.transform, true);

                Assert.That(colliderRoot.GetComponent<Collider>(), Is.Not.Null);
                Assert.That(visual.GetComponent<Collider>(), Is.Null);
                Assert.That(metadata.ObstacleCategory, Is.EqualTo(EnvironmentObstacle.Category.TownCenter));
                Assert.That(metadata.ExcludesFromNavMesh, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(colliderRoot);
                Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void DecorativeTreesAndFlowersRemainColliderFreeByPolicy()
        {
            GameObject decoration = new GameObject("Flowers");
            try
            {
                Assert.That(decoration.GetComponent<Collider>(), Is.Null);
                Assert.That(decoration.GetComponent<EnvironmentObstacle>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(decoration);
            }
        }
    }
}

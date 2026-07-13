using System.Collections;
using CierzoArena.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    /// <summary>Scene-level smoke coverage for the static M23 bridge pairs. Network
    /// authority remains unaffected because this test exercises only scene geometry.</summary>
    public sealed class M23EnvironmentNavigationPlayModeTests
    {
        [UnityTest]
        public IEnumerator GeneratedArenaContainsThreeArchedBridgePairs()
        {
            SceneManager.LoadScene("MobaGreyboxArena");
            yield return null;

            BridgeVisualProfile[] profiles = Object.FindObjectsByType<BridgeVisualProfile>(FindObjectsInactive.Include);
            EnvironmentObstacle[] obstacles = Object.FindObjectsByType<EnvironmentObstacle>(FindObjectsInactive.Include);
            Assert.That(profiles, Has.Length.EqualTo(3));

            foreach (BridgeVisualProfile profile in profiles)
            {
                EnvironmentObstacle deck = System.Array.Find(obstacles, item =>
                    item.ObstacleCategory == EnvironmentObstacle.Category.BridgeDeck && item.VisualRoot == profile.transform);
                Assert.That(deck, Is.Not.Null, $"{profile.name} needs a gameplay deck.");
                Assert.That(deck.GetComponent<MeshCollider>(), Is.Not.Null, $"{profile.name} needs an arched mesh collider, not a flat box.");
                Assert.That(deck.GetComponent<BoxCollider>(), Is.Null, $"{profile.name} cannot keep the retired flat deck.");
                Assert.That(profile.SampleCount, Is.GreaterThanOrEqualTo(7));
                Assert.That(profile.SegmentCount, Is.InRange(6, 12));
                Assert.That(profile.CrownHeight, Is.GreaterThan(profile.EntryHeight + .05f));
                Assert.That(profile.MaximumVisualDifference, Is.LessThanOrEqualTo(.15f));
            }
        }

        [UnityTest]
        public IEnumerator ArchedBridgeDecksHaveCompletePathsInBothDirections()
        {
            SceneManager.LoadScene("MobaGreyboxArena");
            yield return null;
            yield return null;

            BridgeVisualProfile[] profiles = Object.FindObjectsByType<BridgeVisualProfile>(FindObjectsInactive.Include);
            Assert.That(profiles, Has.Length.EqualTo(3));
            foreach (BridgeVisualProfile profile in profiles)
            {
                Vector3 forward = profile.DeckForward;
                Vector3 entryRequested = profile.DeckCenter - forward * (profile.Length * .46f) + Vector3.up * 2f;
                Vector3 exitRequested = profile.DeckCenter + forward * (profile.Length * .46f) + Vector3.up * 2f;
                Assert.That(NavMesh.SamplePosition(entryRequested, out NavMeshHit entry, 3f, NavMesh.AllAreas), Is.True, $"{profile.name} entry must join its shore.");
                Assert.That(NavMesh.SamplePosition(exitRequested, out NavMeshHit exit, 3f, NavMesh.AllAreas), Is.True, $"{profile.name} exit must join its shore.");

                NavMeshPath forwardPath = new NavMeshPath();
                NavMeshPath reversePath = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(entry.position, exit.position, NavMesh.AllAreas, forwardPath), Is.True);
                Assert.That(NavMesh.CalculatePath(exit.position, entry.position, NavMesh.AllAreas, reversePath), Is.True);
                Assert.That(forwardPath.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(reversePath.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            }
        }
    }
}

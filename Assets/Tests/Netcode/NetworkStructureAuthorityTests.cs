using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using CierzoArena.Netcode;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode.Tests
{
    /// <summary>Fast authority regression checks without a live transport.</summary>
    public sealed class NetworkStructureAuthorityTests
    {
        [Test]
        public void NetworkStructureStartsWithTowerSimulationDisabledUntilServerSpawn()
        {
            GameObject structureObject = new GameObject("Network Tower");
            structureObject.AddComponent<NetworkObject>();
            TeamMember team = structureObject.AddComponent<TeamMember>();
            typeof(TeamMember).GetField("team", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(team, TeamId.Azure);
            structureObject.AddComponent<Health>();
            structureObject.AddComponent<StructureEntity>();
            TowerController tower = structureObject.AddComponent<TowerController>();
            structureObject.AddComponent<NetworkStructureController>();

            Assert.That(tower.SimulationEnabled, Is.False,
                "A tower must never begin client-side simulation before NGO establishes server authority.");

            Object.DestroyImmediate(structureObject);
        }

        [Test]
        public void NetworkMatchStateHasNoClientVictoryEntryPoint()
        {
            // The bridge exposes no RPC: only MatchStateController's server-side
            // core-destruction event writes its NetworkVariable.
            MethodInfo[] methods = typeof(NetworkMatchStateController).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (MethodInfo method in methods)
            {
                Assert.That(method.Name.EndsWith("Rpc"), Is.False,
                    $"Unexpected victory RPC: {method.Name}");
            }
        }

        [Test]
        public void NetworkCreepBridgesExposeNoClientAuthoritativeEntryPoint()
        {
            AssertHasNoRpc(typeof(NetworkCreepController));
            AssertHasNoRpc(typeof(NetworkCreepWaveSpawner));
        }

        private static void AssertHasNoRpc(System.Type type)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (MethodInfo method in methods)
            {
                Assert.That(method.Name.EndsWith("Rpc"), Is.False,
                    $"{type.Name} unexpectedly exposes a client authority RPC: {method.Name}");
            }
        }
    }
}

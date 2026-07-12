using System.Reflection;
using CierzoArena.Core;
using CierzoArena.Netcode;
using CierzoArena.Structures;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode.Tests
{
    public sealed class NetworkMatchStatisticsAuthorityTests
    {
        [Test]
        public void PreSpawnBridgeNeverGrantsStatisticsAuthority()
        {
            GameObject match=new GameObject("Network Match");match.AddComponent<NetworkObject>();match.AddComponent<MatchStateController>();MatchStatisticsController statistics=match.AddComponent<MatchStatisticsController>();statistics.SetAuthorityEnabled(false);match.AddComponent<NetworkMatchStatisticsController>();
            Assert.That(statistics.IsAuthoritative,Is.False);
            Object.DestroyImmediate(match);
        }
        [Test]
        public void NetworkStatisticsExposeNoClientStatisticRpc()
        {
            MethodInfo[] methods=typeof(NetworkMatchStatisticsController).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
            for(int i=0;i<methods.Length;i++)Assert.That(methods[i].Name.EndsWith("Rpc"),Is.False,$"Unexpected statistics client RPC: {methods[i].Name}");
        }
    }
}

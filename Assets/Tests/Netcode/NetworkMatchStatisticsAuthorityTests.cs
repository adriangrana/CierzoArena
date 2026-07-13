using System.Reflection;
using CierzoArena.Core;
using CierzoArena.Netcode;
using CierzoArena.Structures;
using CierzoArena.Units;
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
        public void NetworkStatisticsGoldReplicationIsPrivateAndServerOriginated()
        {
            MethodInfo[] methods=typeof(NetworkMatchStatisticsController).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
            for(int i=0;i<methods.Length;i++)
            {
                if(!methods[i].Name.EndsWith("Rpc"))continue;
                Assert.That(methods[i].Name,Is.EqualTo("ReceiveTeamGoldRpc"));
                Assert.That(methods[i].IsPrivate,Is.True,"The client must not be able to invoke a scoreboard gold endpoint.");
            }
            System.Type publicRow=typeof(NetworkMatchStatisticsController).GetNestedType("NetworkHeroStatistics",BindingFlags.NonPublic);
            Assert.That(publicRow.GetField("CurrentGold",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic),Is.Null);
            Assert.That(publicRow.GetField("GoldEarned",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic),Is.Null);
        }
        [Test]
        public void IndividualHeroGoldIsOwnerOnlyRatherThanPublic()
        {
            GameObject hero=new GameObject("Hero Economy");
            hero.AddComponent<HeroEconomy>();
            NetworkHeroEconomy networkEconomy=hero.AddComponent<NetworkHeroEconomy>();
            FieldInfo goldField=typeof(NetworkHeroEconomy).GetField("replicatedGold",BindingFlags.Instance|BindingFlags.NonPublic);
            NetworkVariableBase gold=(NetworkVariableBase)goldField.GetValue(networkEconomy);
            Assert.That(gold.ReadPerm,Is.EqualTo(NetworkVariableReadPermission.Owner));
            Object.DestroyImmediate(hero);
        }
    }
}

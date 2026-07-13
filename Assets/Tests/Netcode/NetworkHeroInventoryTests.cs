using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode.Tests
{
    public sealed class NetworkHeroInventoryTests
    {
        [Test]
        public void ClientBridgeStartsWithDirectInventoryAuthorityDisabled()
        {
            GameObject hero = new GameObject("Network Hero");
            hero.AddComponent<NetworkObject>();
            TeamMember team = hero.AddComponent<TeamMember>();
            Set(team, "team", TeamId.Azure);
            hero.AddComponent<HeroUnit>();
            hero.AddComponent<Health>();
            hero.AddComponent<HeroEconomy>();
            HeroInventory inventory = hero.AddComponent<HeroInventory>();
            hero.AddComponent<NetworkHeroInventory>();

            FieldInfo authority = typeof(HeroInventory).GetField("authorityEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((bool)authority.GetValue(inventory), Is.False);
            Assert.That(typeof(NetworkHeroInventory).GetInterface(nameof(IHeroInventoryRequestGateway)), Is.Not.Null);

            Object.DestroyImmediate(hero);
        }

        [Test]
        public void NetworkRequestsTransmitOnlyItemHashOrSlotNeverPriceOrStats()
        {
            MethodInfo buy = typeof(NetworkHeroInventory).GetMethod("RequestBuyRpc");
            MethodInfo sell = typeof(NetworkHeroInventory).GetMethod("RequestSellRpc");
            MethodInfo swap = typeof(NetworkHeroInventory).GetMethod("RequestSwapRpc");

            Assert.That(buy.GetParameters()[0].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(sell.GetParameters()[0].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(buy.GetParameters().Length, Is.EqualTo(2));
            Assert.That(sell.GetParameters().Length, Is.EqualTo(2));
            Assert.That(swap.GetParameters()[0].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(swap.GetParameters()[1].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(swap.GetParameters().Length, Is.EqualTo(3));
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}

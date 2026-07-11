using System.Reflection;
using CierzoArena.Netcode;
using NUnit.Framework;

namespace CierzoArena.Netcode.Tests
{
    public sealed class NetworkNeutralAuthorityTests
    {
        [Test]
        public void NeutralNetworkBridgesExposeNoClientAuthoritativeRpc()
        {
            AssertNoRpc(typeof(NetworkNeutralController));AssertNoRpc(typeof(NetworkNeutralCampSpawner));
        }
        private static void AssertNoRpc(System.Type type){foreach(MethodInfo method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))Assert.That(method.Name.EndsWith("Rpc"),Is.False,$"{type.Name} unexpectedly exposes {method.Name}");}
    }
}

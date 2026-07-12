using CierzoArena.Netcode;
using NUnit.Framework;

namespace CierzoArena.Netcode.Tests
{
    public sealed class MobaNetworkMatchBootstrapTests
    {
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(false, false, false)]
        [TestCase(true, true, false)]
        public void LocalAndNetworkModesAreMutuallyExclusive(bool networkMode, bool localActorsActive, bool expected)
        {
            Assert.That(MobaNetworkMatchBootstrap.AreModesExclusive(networkMode,localActorsActive),Is.EqualTo(expected));
        }

        [Test]
        public void NetworkBootstrapContainsNoClientGameplayRpc()
        {
            foreach(System.Reflection.MethodInfo method in typeof(MobaNetworkMatchBootstrap).GetMethods(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly))
                Assert.That(method.Name.EndsWith("Rpc"),Is.False,$"Unexpected client gameplay RPC: {method.Name}");
        }
    }
}

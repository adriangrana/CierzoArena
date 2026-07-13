using CierzoArena.Environment;
using NUnit.Framework;
using Unity.Netcode;

namespace CierzoArena.Netcode.Tests
{
    /// <summary>Static environment geometry is authored identically on every peer;
    /// it must not become a replicated actor or take ownership from gameplay units.</summary>
    public sealed class M23StaticEnvironmentNetcodeTests
    {
        [Test]
        public void StaticBridgeAndObstacleMetadataAreNotNetworkBehaviours()
        {
            Assert.That(typeof(NetworkBehaviour).IsAssignableFrom(typeof(BridgeVisualProfile)), Is.False);
            Assert.That(typeof(NetworkBehaviour).IsAssignableFrom(typeof(EnvironmentObstacle)), Is.False);
        }
    }
}

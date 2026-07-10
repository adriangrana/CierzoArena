using System.Collections;
using CierzoArena.CameraSystem;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Netcode.Tests
{
    /// <summary>
    /// M4.3 ownership integration for the MOBA camera. Following the existing Netcode
    /// test philosophy, these tests do not spin up a NetworkManager or simulated
    /// clients; they exercise the real registration wiring on
    /// <see cref="NetworkUnitController"/> in isolation. The ownership decision is
    /// extracted into <see cref="NetworkUnitController.ShouldRegisterAsLocalHero"/> and
    /// the effect is driven through the public
    /// <see cref="NetworkUnitController.RegisterAsLocalHeroIfOwner"/> /
    /// <see cref="NetworkUnitController.UnregisterAsLocalHero"/> methods, which
    /// <see cref="NetworkUnitController.OnNetworkSpawn"/> and
    /// <see cref="NetworkUnitController.OnNetworkDespawn"/> call with the real
    /// <c>IsOwner</c> and <see cref="LocalHeroProvider.Active"/>. The final NGO
    /// call-through (spawn/despawn firing these methods) is validated manually with two
    /// instances; simulating live ownership here would need disproportionate,
    /// flaky infrastructure.
    /// </summary>
    public sealed class NetworkUnitControllerLocalHeroTests
    {
        [Test]
        public void OwnershipDecisionRegistersOnlyForTheOwner()
        {
            Assert.That(NetworkUnitController.ShouldRegisterAsLocalHero(true), Is.True);
            Assert.That(NetworkUnitController.ShouldRegisterAsLocalHero(false), Is.False);
        }

        [UnityTest]
        public IEnumerator OwnerUnitRegistersItsTransform()
        {
            LocalHeroProvider provider = CreateProvider(out GameObject providerHost);
            GameObject unit = CreateNetworkUnit("Owned");
            NetworkUnitController controller = unit.GetComponent<NetworkUnitController>();
            yield return null;

            controller.RegisterAsLocalHeroIfOwner(provider, isOwner: true);

            Assert.That(provider.CurrentHero, Is.EqualTo(unit.transform));
            Object.Destroy(unit);
            Object.Destroy(providerHost);
        }

        [UnityTest]
        public IEnumerator NonOwnerUnitDoesNotRegister()
        {
            LocalHeroProvider provider = CreateProvider(out GameObject providerHost);
            GameObject unit = CreateNetworkUnit("Remote");
            NetworkUnitController controller = unit.GetComponent<NetworkUnitController>();
            yield return null;

            controller.RegisterAsLocalHeroIfOwner(provider, isOwner: false);

            Assert.That(provider.CurrentHero, Is.Null);
            Object.Destroy(unit);
            Object.Destroy(providerHost);
        }

        [UnityTest]
        public IEnumerator DespawnOfOwnerClearsTheReference()
        {
            LocalHeroProvider provider = CreateProvider(out GameObject providerHost);
            GameObject unit = CreateNetworkUnit("Owned");
            NetworkUnitController controller = unit.GetComponent<NetworkUnitController>();
            yield return null;

            controller.RegisterAsLocalHeroIfOwner(provider, isOwner: true);
            Assert.That(provider.CurrentHero, Is.EqualTo(unit.transform));

            controller.UnregisterAsLocalHero();

            Assert.That(provider.CurrentHero, Is.Null);
            Object.Destroy(unit);
            Object.Destroy(providerHost);
        }

        [UnityTest]
        public IEnumerator RemoteUnitDoesNotReplaceTheLocalHero()
        {
            LocalHeroProvider provider = CreateProvider(out GameObject providerHost);
            GameObject localUnit = CreateNetworkUnit("Local");
            GameObject remoteUnit = CreateNetworkUnit("Remote");
            NetworkUnitController local = localUnit.GetComponent<NetworkUnitController>();
            NetworkUnitController remote = remoteUnit.GetComponent<NetworkUnitController>();
            yield return null;

            local.RegisterAsLocalHeroIfOwner(provider, isOwner: true);
            remote.RegisterAsLocalHeroIfOwner(provider, isOwner: false);

            Assert.That(provider.CurrentHero, Is.EqualTo(localUnit.transform));
            Object.Destroy(localUnit);
            Object.Destroy(remoteUnit);
            Object.Destroy(providerHost);
        }

        private static LocalHeroProvider CreateProvider(out GameObject host)
        {
            host = new GameObject("LocalHeroProvider (test)");
            return host.AddComponent<LocalHeroProvider>();
        }

        private static GameObject CreateNetworkUnit(string name)
        {
            // Mirror the domain unit composition used by the existing Netcode tests, then
            // add the network bridge. No NetworkObject/NetworkManager is needed because
            // the registration methods use only the transform and the provider.
            GameObject unit = new GameObject(name);
            unit.AddComponent<TeamMember>();
            unit.AddComponent<Health>();
            unit.AddComponent<ClickMover>();
            unit.AddComponent<BasicAttack>();
            unit.AddComponent<UnitOrderController>();
            unit.AddComponent<NetworkUnitController>();
            return unit;
        }
    }
}

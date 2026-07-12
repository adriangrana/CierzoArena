using CierzoArena.Netcode;
using NUnit.Framework;

namespace CierzoArena.Netcode.Tests
{
    public sealed class NetworkHeroRosterAuthorityTests
    {
        [Test]
        public void ServerValidatesRequestedHeroIdAndNeverAcceptsPrefabData()
        {
            Assert.AreEqual("storm_warden",MobaNetworkMatchBootstrap.ValidateRequestedHeroId("storm_warden"));
            Assert.AreEqual("storm_warden",MobaNetworkMatchBootstrap.ValidateRequestedHeroId("client_supplied_prefab_or_stats"));
        }

        [Test]
        public void DevelopmentRosterAllowsDuplicateHeroIdsForSeparatePlayers()
        {
            string first=MobaNetworkMatchBootstrap.ValidateRequestedHeroId("rift_duelist");
            string second=MobaNetworkMatchBootstrap.ValidateRequestedHeroId("rift_duelist");
            Assert.AreEqual(first,second);
        }
    }
}

using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>Server-write-only gold replication bridge; no client economy RPC exists.</summary>
    [RequireComponent(typeof(HeroEconomy))]
    public sealed class NetworkHeroEconomy : NetworkBehaviour
    {
        private readonly NetworkVariable<int> replicatedGold = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private HeroEconomy economy;

        private void Awake()
        {
            economy = GetComponent<HeroEconomy>();
            economy.SetAuthorityEnabled(false);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                economy.SetAuthorityEnabled(true);
                economy.Changed += OnServerGoldChanged;
                replicatedGold.Value = economy.Gold;
            }
            else
            {
                replicatedGold.OnValueChanged += OnReplicatedGoldChanged;
                economy.ApplyAuthoritativeState(replicatedGold.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) economy.Changed -= OnServerGoldChanged;
            else replicatedGold.OnValueChanged -= OnReplicatedGoldChanged;
        }

        private void OnServerGoldChanged(HeroEconomy _, int gold) => replicatedGold.Value = gold;
        private void OnReplicatedGoldChanged(int _, int gold) => economy.ApplyAuthoritativeState(gold);
    }
}

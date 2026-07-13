using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>Server-write-only gold replication bridge; no client economy RPC exists.</summary>
    [RequireComponent(typeof(HeroEconomy))]
    public sealed class NetworkHeroEconomy : NetworkBehaviour
    {
        // Individual hero economy is private to its owner. Team scoreboard gold is
        // replicated separately by NetworkMatchStatisticsController to same-team
        // clients only, rather than exposing every hero's balance to every client.
        private readonly NetworkVariable<int> replicatedGold = new(0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
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
            else if (IsOwner)
            {
                replicatedGold.OnValueChanged += OnReplicatedGoldChanged;
                economy.ApplyAuthoritativeState(replicatedGold.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) economy.Changed -= OnServerGoldChanged;
            else if(IsOwner) replicatedGold.OnValueChanged -= OnReplicatedGoldChanged;
        }

        private void OnServerGoldChanged(HeroEconomy _, int gold) => replicatedGold.Value = gold;
        private void OnReplicatedGoldChanged(int _, int gold) => economy.ApplyAuthoritativeState(gold);
    }
}

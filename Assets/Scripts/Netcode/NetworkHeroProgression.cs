using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>Server-owned replication bridge for hero XP and levels; intentionally exposes no RPC.</summary>
    [RequireComponent(typeof(HeroProgression))]
    public sealed class NetworkHeroProgression : NetworkBehaviour
    {
        private readonly NetworkVariable<int> replicatedLevel = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> replicatedExperience = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> replicatedTotalExperience = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private HeroProgression progression;
        public void ReapplyReplicatedState() { if(IsSpawned&&!IsServer) ApplyReplicatedState(); }

        private void Awake()
        {
            progression = GetComponent<HeroProgression>();
            progression.SetAuthorityEnabled(false);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                progression.SetAuthorityEnabled(true);
                progression.Changed += PublishServerState;
                PublishServerState(progression);
            }
            else
            {
                replicatedLevel.OnValueChanged += OnReplicatedValueChanged;
                replicatedExperience.OnValueChanged += OnReplicatedValueChanged;
                replicatedTotalExperience.OnValueChanged += OnReplicatedValueChanged;
                ApplyReplicatedState();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                progression.Changed -= PublishServerState;
            }
            else
            {
                replicatedLevel.OnValueChanged -= OnReplicatedValueChanged;
                replicatedExperience.OnValueChanged -= OnReplicatedValueChanged;
                replicatedTotalExperience.OnValueChanged -= OnReplicatedValueChanged;
            }
        }

        private void PublishServerState(HeroProgression source)
        {
            replicatedLevel.Value = source.Level;
            replicatedExperience.Value = source.Experience;
            replicatedTotalExperience.Value = source.TotalExperience;
        }

        private void OnReplicatedValueChanged(int _, int __) => ApplyReplicatedState();

        private void ApplyReplicatedState()
        {
            progression.ApplyAuthoritativeState(replicatedLevel.Value, replicatedExperience.Value, replicatedTotalExperience.Value);
            if (TryGetComponent(out NetworkUnitController unit))
            {
                unit.ReapplyReplicatedHealth();
            }
        }
    }
}

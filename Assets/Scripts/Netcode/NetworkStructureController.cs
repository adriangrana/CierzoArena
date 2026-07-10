using CierzoArena.Combat;
using CierzoArena.Structures;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>
    /// Thin NGO bridge for a static structure. Runtime owns health/destruction and
    /// tower simulation; this component only selects server authority and mirrors the
    /// authoritative health state to observers. Structures deliberately have no
    /// player ownership and expose no damage RPC.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(StructureEntity))]
    public sealed class NetworkStructureController : NetworkBehaviour
    {
        private readonly NetworkVariable<float> replicatedHealth =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> replicatedDestroyed =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Health health;
        private StructureEntity structure;
        private TowerController tower;

        private void Awake()
        {
            health = GetComponent<Health>();
            structure = GetComponent<StructureEntity>();
            TryGetComponent(out tower);

            // Prevent a client from simulating before OnNetworkSpawn establishes its
            // role. The host/server explicitly enables the tower below.
            tower?.SetSimulationEnabled(false);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                replicatedHealth.Value = health.Current;
                replicatedDestroyed.Value = structure.IsDestroyed;
                health.Changed += OnServerHealthChanged;
                structure.Destroyed += OnServerStructureDestroyed;
                tower?.SetSimulationEnabled(true);
                return;
            }

            health.ApplyAuthoritativeState(replicatedHealth.Value);
            replicatedHealth.OnValueChanged += OnReplicatedHealthChanged;
            replicatedDestroyed.OnValueChanged += OnReplicatedDestroyedChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                health.Changed -= OnServerHealthChanged;
                structure.Destroyed -= OnServerStructureDestroyed;
            }
            else
            {
                replicatedHealth.OnValueChanged -= OnReplicatedHealthChanged;
                replicatedDestroyed.OnValueChanged -= OnReplicatedDestroyedChanged;
            }

            tower?.SetSimulationEnabled(false);
        }

        private void OnServerHealthChanged(Health _, float current, float __)
        {
            replicatedHealth.Value = current;
        }

        private void OnServerStructureDestroyed(StructureEntity _)
        {
            replicatedDestroyed.Value = true;
        }

        private void OnReplicatedHealthChanged(float _, float current)
        {
            health.ApplyAuthoritativeState(current);
        }

        private void OnReplicatedDestroyedChanged(bool _, bool destroyed)
        {
            // Health is the source of Runtime truth. The bool exists for an explicit
            // replicated destruction flag and late-join diagnostics; state normally
            // arrives with health = 0, so it intentionally has no client mutation.
            if (destroyed && health.Current > 0f)
            {
                health.ApplyAuthoritativeState(0f);
            }
        }
    }
}

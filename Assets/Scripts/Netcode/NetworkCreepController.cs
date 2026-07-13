using CierzoArena.Combat;
using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Netcode
{
    /// <summary>NGO bridge: server simulates a creep, observers only mirror health/transform.</summary>
    [RequireComponent(typeof(CreepController))]
    [RequireComponent(typeof(Health))]
    public sealed class NetworkCreepController : NetworkBehaviour
    {
        private readonly NetworkVariable<float> replicatedHealth = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private CreepController creep;
        private Health health;
        private BasicAttack attack;

        private void Awake()
        {
            creep = GetComponent<CreepController>();
            health = GetComponent<Health>();
            attack = GetComponent<BasicAttack>();
            creep.SetSimulationEnabled(false);
            creep.SetExternalDespawn(true);
        }

        public override void OnNetworkSpawn()
        {
            CreepGoblinPresentation.Ensure(gameObject);
            if (IsServer)
            {
                replicatedHealth.Value = health.Current;
                health.Changed += OnServerHealthChanged;
                creep.DespawnRequested += OnDespawnRequested;
                attack.SetProjectilePresentationEnabled(false);
                attack.ProjectileReleased += OnProjectileReleased;
                creep.SetSimulationEnabled(true);
                return;
            }

            creep.SetSimulationEnabled(false);
            attack.enabled = false;
            if (TryGetComponent(out ClickMover mover)) mover.enabled = false;
            if (TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
            health.ApplyAuthoritativeState(replicatedHealth.Value);
            replicatedHealth.OnValueChanged += OnReplicatedHealthChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                health.Changed -= OnServerHealthChanged;
                creep.DespawnRequested -= OnDespawnRequested;
                attack.ProjectileReleased -= OnProjectileReleased;
            }
            else
            {
                replicatedHealth.OnValueChanged -= OnReplicatedHealthChanged;
            }

            creep.SetSimulationEnabled(false);
        }

        private void OnServerHealthChanged(Health _, float current, float __) => replicatedHealth.Value = current;
        private void OnReplicatedHealthChanged(float _, float current) => health.ApplyAuthoritativeState(current);
        private void OnProjectileReleased(BasicAttack source, Health target) => NetworkProjectileSpawner.Active?.SpawnVisual(source, target);

        private void OnDespawnRequested(CreepController _)
        {
            if (IsServer && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
    }
}

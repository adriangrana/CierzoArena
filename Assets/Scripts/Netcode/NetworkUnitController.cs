using CierzoArena.Combat;
using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Netcode
{
    /// <summary>
    /// Network bridge for a single controllable unit. It is the authoritative
    /// reception point for order requests coming from the owning client and the
    /// replication point for the unit's minimal state (transform is handled by a
    /// separate NetworkTransform; health is replicated here).
    ///
    /// Design notes for the M2.5 spike:
    /// - The client only <b>requests</b>. All acceptance/validation happens on the
    ///   server, first via ownership (<see cref="AuthoritativeOrderProcessor"/>)
    ///   and then via the existing domain boundary (<see cref="UnitOrderController"/>).
    /// - Continuous simulation (chasing, range, cadence, damage, death) runs only on
    ///   the server. On non-server instances the simulation components are disabled
    ///   so the NavMeshAgent never fights the replicated transform.
    /// - This class intentionally stays small and easy to delete/refactor.
    /// </summary>
    [RequireComponent(typeof(UnitOrderController))]
    [RequireComponent(typeof(Health))]
    public sealed class NetworkUnitController : NetworkBehaviour
    {
        private readonly NetworkVariable<float> replicatedHealth =
            new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private UnitOrderController orderController;
        private Health health;
        private AuthoritativeOrderProcessor processor;

        private void Awake()
        {
            orderController = GetComponent<UnitOrderController>();
            health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                processor = new AuthoritativeOrderProcessor(orderController, OwnerClientId);
                replicatedHealth.Value = health.Current;
                health.Changed += OnServerHealthChanged;
            }
            else
            {
                // Non-authority instance: hand the transform over to replication and
                // stop the local simulation so it cannot diverge from the server.
                DisableServerOnlySimulation();
                DisableHitFeedback();

                // Apply the initial authoritative health exactly (explicit initial
                // path, independent of any later OnValueChanged).
                health.ApplyAuthoritativeState(replicatedHealth.Value);
                replicatedHealth.OnValueChanged += OnReplicatedHealthChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && health != null)
            {
                health.Changed -= OnServerHealthChanged;
            }
            else
            {
                replicatedHealth.OnValueChanged -= OnReplicatedHealthChanged;
            }
        }

        // ----- Client -> Server order requests -------------------------------

        [Rpc(SendTo.Server)]
        public void RequestMoveRpc(Vector3 destination, RpcParams rpcParams = default)
        {
            SyncOwnerToProcessor();
            OrderRequestResult result = processor.ProcessMove(rpcParams.Receive.SenderClientId, destination);
            LogResult("Move", rpcParams.Receive.SenderClientId, result);
        }

        [Rpc(SendTo.Server)]
        public void RequestAttackRpc(NetworkObjectReference targetReference, RpcParams rpcParams = default)
        {
            SyncOwnerToProcessor();
            Health resolvedTarget = ResolveTargetHealth(targetReference);
            OrderRequestResult result = processor.ProcessAttack(rpcParams.Receive.SenderClientId, resolvedTarget);
            LogResult("Attack", rpcParams.Receive.SenderClientId, result);
        }

        [Rpc(SendTo.Server)]
        public void RequestStopRpc(RpcParams rpcParams = default)
        {
            SyncOwnerToProcessor();
            OrderRequestResult result = processor.ProcessStop(rpcParams.Receive.SenderClientId);
            LogResult("Stop", rpcParams.Receive.SenderClientId, result);
        }

        // ----- Server internals ---------------------------------------------

        private void SyncOwnerToProcessor()
        {
            // Ownership can be (re)assigned by the server at runtime; keep the
            // authorization source of truth aligned with the NetworkObject owner.
            processor.SetOwningClient(OwnerClientId);
        }

        private static Health ResolveTargetHealth(NetworkObjectReference targetReference)
        {
            if (!targetReference.TryGet(out NetworkObject targetObject))
            {
                return null;
            }

            return targetObject.GetComponent<Health>();
        }

        private void OnServerHealthChanged(Health _, float current, float max)
        {
            replicatedHealth.Value = current;
        }

        private void LogResult(string order, ulong senderClientId, OrderRequestResult result)
        {
            if (result == OrderRequestResult.Accepted)
            {
                return;
            }

            Debug.Log($"[Spike] {order} request from client {senderClientId} on '{name}' (owner {OwnerClientId}) rejected: {result}");
        }

        // ----- Client internals ---------------------------------------------

        private void DisableServerOnlySimulation()
        {
            orderController.enabled = false;

            if (TryGetComponent(out BasicAttack basicAttack))
            {
                basicAttack.enabled = false;
            }

            if (TryGetComponent(out ClickMover mover))
            {
                mover.enabled = false;
            }

            if (TryGetComponent(out NavMeshAgent agent))
            {
                agent.enabled = false;
            }
        }

        private void DisableHitFeedback()
        {
            // Damage numbers and the damage flash are hit-event feedback derived from
            // Health.Changed. On a non-authority instance the only health signal is a
            // state sync, which is not a real hit; deriving hit feedback from it would
            // misfire (notably on late join). For the spike we disable them here and
            // leave replicated hit feedback as a documented limitation. The world
            // health bar stays enabled because it is a pure reflection of state.
            if (TryGetComponent(out DamageNumberSpawner damageNumbers))
            {
                damageNumbers.enabled = false;
            }

            if (TryGetComponent(out DamageFlash damageFlash))
            {
                damageFlash.enabled = false;
            }
        }

        private void OnReplicatedHealthChanged(float previous, float current)
        {
            // Mirror the authoritative value as exact state (not as a damage event) so
            // the client's Health, world health bar and death visibility stay in sync
            // without ever interpreting a state sync as a hit.
            health.ApplyAuthoritativeState(current);
        }
    }
}

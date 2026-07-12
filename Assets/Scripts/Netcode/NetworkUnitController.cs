using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using CierzoArena.CameraSystem;
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
        private BasicAttack attack;
        private AuthoritativeOrderProcessor processor;
        private LocalHeroProvider localHeroProvider;
        private float lastReplicatedHealth;

        private void Awake()
        {
            orderController = GetComponent<UnitOrderController>();
            health = GetComponent<Health>();
            attack = GetComponent<BasicAttack>();
        }

        public override void OnNetworkSpawn()
        {
            TeamId team=TryGetComponent(out TeamMember member)?member.Team:TeamId.Neutral;
            int entityId=TryGetComponent(out HeroMatchIdentity identity)?identity.HeroId:-1;
            ulong localClientId=NetworkManager.Singleton!=null?NetworkManager.Singleton.LocalClientId:ulong.MaxValue;
            Debug.Log($"[M18 Spawn] NetworkUnitController.OnNetworkSpawn object={name} IsServer={IsServer} IsClient={IsClient} IsOwner={IsOwner} OwnerClientId={OwnerClientId} LocalClientId={localClientId} team={team} EntityId={entityId} NetworkObjectId={NetworkObjectId}",this);
            if (IsServer)
            {
                processor = new AuthoritativeOrderProcessor(orderController, OwnerClientId);
                // A Runtime projectile remains the authoritative hit simulator. NGO
                // owns the visible twin, so hide the local mesh on the host/server.
                attack?.SetProjectilePresentationEnabled(false);
                replicatedHealth.Value = health.Current;
                health.Changed += OnServerHealthChanged;
                if (attack != null)
                {
                    attack.ProjectileReleased += OnServerProjectileReleased;
                }
            }
            else
            {
                // Non-authority instance: hand the transform over to replication and
                // stop the local simulation so it cannot diverge from the server.
                DisableServerOnlySimulation();
                DisableHitFeedback();

                // Apply the initial authoritative health exactly (explicit initial
                // path, independent of any later OnValueChanged).
                lastReplicatedHealth = replicatedHealth.Value;
                ReapplyReplicatedHealth();
                replicatedHealth.OnValueChanged += OnReplicatedHealthChanged;
            }

            // MOBA camera (M4.3): register this unit as the local hero only when this
            // instance owns it, so no camera ever follows a remote unit. Resolved once
            // here (not per frame) via the scene provider's small access point, keeping
            // Runtime Netcode-agnostic.
            RegisterAsLocalHeroIfOwner(LocalHeroProvider.Active, IsOwner);
            if(IsOwner&&TryGetComponent(out TeamMember ownerTeam))MobaNetworkMatchBootstrap.Active?.NotifyLocalOwner(ownerTeam.Team);
            if (IsOwner && TryGetComponent(out SelectableUnit selectable))
            {
                selectable.SetSelected(true);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && health != null)
            {
                health.Changed -= OnServerHealthChanged;
                if (attack != null)
                {
                    attack.ProjectileReleased -= OnServerProjectileReleased;
                }
            }
            else
            {
                replicatedHealth.OnValueChanged -= OnReplicatedHealthChanged;
            }

            // Clear the local-hero reference if this owned unit despawns, so the camera
            // stops touching a transform that is about to be destroyed.
            UnregisterAsLocalHero();
        }

        // ----- Local hero registration (M4.3) --------------------------------

        /// <summary>
        /// Pure decision: an instance registers a unit as its local hero exactly when it
        /// owns that unit. Extracted so the ownership rule is testable without spinning
        /// up a NetworkManager.
        /// </summary>
        public static bool ShouldRegisterAsLocalHero(bool isOwner)
        {
            return isOwner;
        }

        /// <summary>
        /// Registers this unit's transform as the local hero in <paramref name="provider"/>
        /// when <paramref name="isOwner"/> is true. No-ops on a null provider or a
        /// non-owned unit, so remote units never become the local hero. This is the real
        /// wiring called from <see cref="OnNetworkSpawn"/>; it is public so ownership
        /// registration can be validated deterministically without live networking.
        /// </summary>
        public void RegisterAsLocalHeroIfOwner(LocalHeroProvider provider, bool isOwner)
        {
            if (provider == null || !ShouldRegisterAsLocalHero(isOwner))
            {
                return;
            }

            localHeroProvider = provider;
            provider.Register(transform);
        }

        /// <summary>
        /// Removes this unit from the local-hero provider it registered with, if any.
        /// Safe to call when it never registered. Called from
        /// <see cref="OnNetworkDespawn"/>.
        /// </summary>
        public void UnregisterAsLocalHero()
        {
            if (localHeroProvider == null)
            {
                return;
            }

            localHeroProvider.Unregister(transform);
            localHeroProvider = null;
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

        private void OnServerProjectileReleased(BasicAttack source, Health target)
        {
            NetworkProjectileSpawner.Active?.SpawnVisual(source, target);
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
            lastReplicatedHealth = current;
            ReapplyReplicatedHealth();
        }

        /// <summary>
        /// Applies the most recent server current-health value after another
        /// replicated system (level or inventory) has changed the local maximum.
        /// This keeps ordering between independent NetworkVariables from turning a
        /// damaged hero's purchase into an accidental heal on a client.
        /// </summary>
        public void ReapplyReplicatedHealth()
        {
            if (IsSpawned && !IsServer)
            {
                health.ApplyAuthoritativeState(lastReplicatedHealth);
            }
        }
    }
}

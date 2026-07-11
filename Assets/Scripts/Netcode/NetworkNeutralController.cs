using CierzoArena.Combat;
using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Netcode
{
    /// <summary>Server authority bridge for a spawned neutral monster.</summary>
    [RequireComponent(typeof(NeutralUnitController))]
    [RequireComponent(typeof(Health))]
    public sealed class NetworkNeutralController : NetworkBehaviour
    {
        private readonly NetworkVariable<float> replicatedHealth=new(0f,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private NeutralUnitController neutral;private Health health;private BasicAttack attack;
        private void Awake(){neutral=GetComponent<NeutralUnitController>();health=GetComponent<Health>();attack=GetComponent<BasicAttack>();neutral.SetSimulationEnabled(false);neutral.SetExternalDespawn(true);}
        public override void OnNetworkSpawn()
        {
            if(IsServer){replicatedHealth.Value=health.Current;health.Changed+=OnHealthChanged;neutral.DespawnRequested+=OnDespawn;attack.SetProjectilePresentationEnabled(false);attack.ProjectileReleased+=OnProjectile;neutral.SetSimulationEnabled(true);return;}
            neutral.SetSimulationEnabled(false);attack.enabled=false;if(TryGetComponent(out ClickMover mover))mover.enabled=false;if(TryGetComponent(out NavMeshAgent agent))agent.enabled=false;health.ApplyAuthoritativeState(replicatedHealth.Value);replicatedHealth.OnValueChanged+=OnReplicatedHealth;
        }
        public override void OnNetworkDespawn(){if(IsServer){health.Changed-=OnHealthChanged;neutral.DespawnRequested-=OnDespawn;attack.ProjectileReleased-=OnProjectile;}else replicatedHealth.OnValueChanged-=OnReplicatedHealth;neutral.SetSimulationEnabled(false);}
        private void OnHealthChanged(Health _,float current,float __)=>replicatedHealth.Value=current;
        private void OnReplicatedHealth(float _,float current)=>health.ApplyAuthoritativeState(current);
        private void OnProjectile(BasicAttack source,Health target)=>NetworkProjectileSpawner.Active?.SpawnVisual(source,target);
        private void OnDespawn(NeutralUnitController _){if(IsServer&&NetworkObject.IsSpawned)NetworkObject.Despawn(true);}
    }
}

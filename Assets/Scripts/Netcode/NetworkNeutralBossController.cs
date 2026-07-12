using CierzoArena.Combat;
using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Netcode
{
    [RequireComponent(typeof(NeutralBossController))]
    [RequireComponent(typeof(Health))]
    public sealed class NetworkNeutralBossController : NetworkBehaviour
    {
        private readonly NetworkVariable<float> replicatedHealth=new(0f,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> replicatedState=new(0,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> telegraphActive=new(false,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private NeutralBossController boss;private Health health;private BasicAttack attack;
        private void Awake(){boss=GetComponent<NeutralBossController>();health=GetComponent<Health>();attack=GetComponent<BasicAttack>();boss.SetSimulationEnabled(false);}
        public override void OnNetworkSpawn(){if(IsServer){replicatedHealth.Value=health.Current;health.Changed+=OnHealth;boss.TelegraphChanged+=OnTelegraph;boss.RewardGranted+=OnReward;attack.SetProjectilePresentationEnabled(false);attack.ProjectileReleased+=OnProjectile;boss.SetSimulationEnabled(true);return;}boss.SetSimulationEnabled(false);attack.enabled=false;if(TryGetComponent(out ClickMover mover))mover.enabled=false;if(TryGetComponent(out NavMeshAgent agent))agent.enabled=false;health.ApplyAuthoritativeState(replicatedHealth.Value);replicatedHealth.OnValueChanged+=OnReplicatedHealth;telegraphActive.OnValueChanged+=OnReplicatedTelegraph;boss.SetReplicatedTelegraph(telegraphActive.Value);}
        public override void OnNetworkDespawn(){if(IsServer){health.Changed-=OnHealth;boss.TelegraphChanged-=OnTelegraph;boss.RewardGranted-=OnReward;attack.ProjectileReleased-=OnProjectile;}else{replicatedHealth.OnValueChanged-=OnReplicatedHealth;telegraphActive.OnValueChanged-=OnReplicatedTelegraph;}boss.SetSimulationEnabled(false);}
        private void Update(){if(IsServer&&IsSpawned)replicatedState.Value=(int)boss.State;}
        private void OnHealth(Health _,float current,float __)=>replicatedHealth.Value=current;
        private void OnReplicatedHealth(float _,float current)=>health.ApplyAuthoritativeState(current);
        private void OnTelegraph(NeutralBossController _,bool active)=>telegraphActive.Value=active;
        private void OnReward(NeutralBossController _,CierzoArena.Core.TeamId team)=>RewardAnnouncementRpc((int)team);
        // Host already receives the server-side announcement directly; NGO sends
        // this only to remote clients. `NotServer` is the portable NGO 2.x target.
        [Rpc(SendTo.NotServer)] private void RewardAnnouncementRpc(int team)=>BossAnnouncementFeedback.Show($"{(CierzoArena.Core.TeamId)team} ha derrotado al Guardián del Cierzo");
        private void OnReplicatedTelegraph(bool _,bool active)=>boss.SetReplicatedTelegraph(active);
        private void OnProjectile(BasicAttack source,Health target)=>NetworkProjectileSpawner.Active?.SpawnVisual(source,target);
    }
}

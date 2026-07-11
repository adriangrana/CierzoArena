using System;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Simple camp-bound neutral AI. It reuses M6 attack and M3 movement.</summary>
    [RequireComponent(typeof(TeamMember))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(BasicAttack))]
    [RequireComponent(typeof(ClickMover))]
    public sealed class NeutralUnitController : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float searchInterval=.2f;
        [SerializeField, Min(.1f)] private float deathCleanupDelay=3f;
        [SerializeField] private LayerMask targetMask=~0;
        private readonly Collider[] overlap=new Collider[32];
        private TeamMember team;private Health health;private BasicAttack attack;private ClickMover mover;private NeutralCamp camp;private Health target;private Vector3 home;private float searchElapsed;private float deathElapsed;private bool returning;private bool simulationEnabled=true;private bool externallyDespawned;
        public Health CurrentTarget=>target;public bool IsReturning=>returning;public bool IsInCombat=>target!=null||returning;public Vector3 HomePosition=>home;public event Action<NeutralUnitController> DespawnRequested;
        private void Awake(){Ensure();CombatEvents.DamageApplied+=OnDamageApplied;}
        private void OnDestroy(){CombatEvents.DamageApplied-=OnDamageApplied;}
        private void Ensure(){if(team==null)team=GetComponent<TeamMember>();if(health==null)health=GetComponent<Health>();if(attack==null)attack=GetComponent<BasicAttack>();if(mover==null)mover=GetComponent<ClickMover>();if(home==Vector3.zero)home=transform.position;}
        public void Configure(NeutralCamp owner,Vector3 origin){Ensure();camp=owner;home=origin;returning=false;target=null;}
        public void SetSimulationEnabled(bool enabled){simulationEnabled=enabled;if(!enabled){target=null;attack?.ClearTarget();mover?.Stop();}}
        public void SetExternalDespawn(bool enabled)=>externallyDespawned=enabled;
        public bool TryAssist(Health aggressor){if(returning||!IsValidTarget(aggressor)||!WithinAggro(aggressor))return false;target=aggressor;return true;}
        private void Update()=>Simulate(Time.deltaTime);
        public bool Simulate(float deltaTime)
        {
            Ensure();deltaTime=Mathf.Max(0f,deltaTime);if(!health.IsAlive)return SimulateDeath(deltaTime);if(!simulationEnabled||(MatchStateController.Active!=null&&!MatchStateController.Active.IsPlaying)){attack.ClearTarget();mover.Stop();return false;}
            if(returning)return SimulateReturn();
            if(OutsideLeash()){BeginReturn();return false;}
            if(!IsValidTarget(target)){searchElapsed+=deltaTime;if(searchElapsed>=searchInterval){searchElapsed=0f;target=FindTarget();}}
            if(target==null){attack.ClearTarget();mover.Stop();camp?.NotifyUnitIdle();return false;}
            attack.SetTarget(target);bool released=attack.Simulate(deltaTime);if(attack.NeedsApproach)mover.MoveTo(attack.GetApproachPosition(target));else mover.Stop();return released;
        }
        private bool SimulateReturn(){mover.MoveTo(home);Vector3 delta=transform.position-home;delta.y=0f;if(delta.sqrMagnitude>camp.ResetRadius*camp.ResetRadius)return false;mover.Stop();attack.ClearTarget();target=null;returning=false;health.RestoreFull();camp?.NotifyUnitIdle();return false;}
        private bool OutsideLeash(){Vector3 delta=transform.position-home;delta.y=0f;return camp!=null&&delta.sqrMagnitude>camp.LeashRange*camp.LeashRange;}
        private void BeginReturn(){returning=true;target=null;attack.ClearTarget();}
        private bool IsValidTarget(Health candidate){if(candidate==null||!candidate.IsAlive||!candidate.TryGetComponent(out TeamMember member)||!team.IsEnemy(member)||member.Team==TeamId.Neutral||candidate.TryGetComponent(out StructureEntity _))return false;return attack.CanAttack(candidate);}
        private bool WithinAggro(Health candidate){Vector3 d=candidate.transform.position-home;d.y=0f;return camp!=null&&d.sqrMagnitude<=camp.AggroRadius*camp.AggroRadius;}
        private Health FindTarget(){if(camp==null)return null;int count=Physics.OverlapSphereNonAlloc(transform.position,camp.AggroRadius,overlap,targetMask,QueryTriggerInteraction.Ignore);Health best=null;float bestDistance=float.PositiveInfinity;for(int i=0;i<count;i++){Collider hit=overlap[i];overlap[i]=null;Health candidate=hit!=null?hit.GetComponentInParent<Health>():null;if(!IsValidTarget(candidate)||!WithinAggro(candidate))continue;float d=(candidate.transform.position-transform.position).sqrMagnitude;if(d<bestDistance){best=candidate;bestDistance=d;}}return best;}
        private void OnDamageApplied(Health victim,DamageContext context){if(victim!=health||context.Attacker==null||returning)return;Health aggressor=context.Attacker.GetComponent<Health>();if(TryAssist(aggressor))camp?.NotifyEngaged(this,aggressor);}
        private bool SimulateDeath(float deltaTime){mover.Stop();attack.ClearTarget();deathElapsed+=deltaTime;if(deathElapsed<deathCleanupDelay)return false;DespawnRequested?.Invoke(this);if(!externallyDespawned){if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}return false;}
    }
}

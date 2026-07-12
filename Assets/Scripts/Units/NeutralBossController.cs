using System;
using System.Collections.Generic;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Units
{
    public enum NeutralBossState { Dormant, Alive, Engaged, Resetting, Dead, Respawning }

    /// <summary>Authoritative major neutral objective with threat, area strike and team bounty.</summary>
    [RequireComponent(typeof(TeamMember))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(BasicAttack))]
    [RequireComponent(typeof(ClickMover))]
    public sealed class NeutralBossController : MonoBehaviour
    {
        private static readonly List<HeroProgression> heroes=new();
        private readonly Dictionary<Health,float> threat=new();
        private readonly Collider[] overlap=new Collider[48];
        [SerializeField,Min(1f)] private float aggroRadius=11f;
        [SerializeField,Min(2f)] private float leashRange=20f;
        [SerializeField,Min(.1f)] private float resetRadius=1.5f;
        [SerializeField,Min(.1f)] private float respawnDelay=180f;
        [SerializeField,Min(.1f)] private float specialCooldown=12f;
        [SerializeField,Min(0f)] private float specialCastPoint=1.1f;
        [SerializeField,Min(.1f)] private float specialRadius=5f;
        [SerializeField,Min(0f)] private float specialDamage=85f;
        [SerializeField,Range(0f,1f)] private float specialSlow=.3f;
        [SerializeField,Min(.1f)] private float specialSlowDuration=2f;
        [SerializeField,Min(.1f)] private float teamBuffDuration=120f;
        [SerializeField,Min(0f)] private float teamDamageBonus=10f;
        [SerializeField,Min(0f)] private float teamMoveBonus=.3f;
        [SerializeField,Min(0)] private int teamGold=100;
        [SerializeField] private Renderer telegraphRenderer;
        [SerializeField] private Renderer[] presentationRenderers;
        [SerializeField] private Collider[] presentationColliders;
        [SerializeField] private LayerMask targetMask=~0;
        [SerializeField] private bool simulationEnabled=true;
        private TeamMember team;private Health health;private BasicAttack attack;private ClickMover mover;private StatusEffectController selfEffects;private Health target;private Vector3 home;private float specialCooldownRemaining;private float castElapsed;private float respawnElapsed;private float resetStuckElapsed;private bool casting;private bool rewardGranted;private bool initialized;private bool combatRegistered;
        public NeutralBossState State { get; private set; }=NeutralBossState.Dormant;
        public Health CurrentTarget=>target;public bool TelegraphActive=>casting;public bool SimulationEnabled=>simulationEnabled;
        public bool NavigationReady=>mover!=null&&mover.IsNavigationEnabled&&!mover.IsNavigationStopped;
        public bool HasMoveCommand=>mover!=null&&mover.HasMoveCommand;
        public event Action<NeutralBossController,bool> TelegraphChanged;
        public event Action<NeutralBossController,TeamId> RewardGranted;
        public static event Action<TeamId,string> Announced;
        private void Awake()=>Ensure();
        private void Start(){if(State==NeutralBossState.Dormant){home=transform.position;State=NeutralBossState.Alive;}}
        private void OnDestroy(){if(combatRegistered)CombatEvents.DamageApplied-=OnDamageApplied;if(health!=null)health.DiedWithContext-=OnDied;}
        private void Ensure(){if(initialized)return;team=GetComponent<TeamMember>();health=GetComponent<Health>();attack=GetComponent<BasicAttack>();mover=GetComponent<ClickMover>();selfEffects=GetComponent<StatusEffectController>();home=transform.position;health.DiedWithContext+=OnDied;CombatEvents.DamageApplied+=OnDamageApplied;combatRegistered=true;initialized=true;}
        public void Configure(Vector3 origin,float aggro,float leash,float reset,float respawn){Ensure();home=origin;aggroRadius=Mathf.Max(1f,aggro);leashRange=Mathf.Max(aggroRadius,leash);resetRadius=Mathf.Clamp(reset,.1f,leashRange);respawnDelay=Mathf.Max(.1f,respawn);State=NeutralBossState.Alive;}
        public void SetSimulationEnabled(bool enabled){simulationEnabled=enabled;if(!enabled)CancelCombat();}
        public void SetReplicatedTelegraph(bool active){if(IsDeadOrRespawning())return;SetTelegraph(active);}
        private void Update()=>Simulate(Time.deltaTime);
        public void Simulate(float deltaTime)
        {
            Ensure();deltaTime=Mathf.Max(0f,deltaTime);if(!simulationEnabled)return;if(MatchStateController.Active!=null&&!MatchStateController.Active.IsPlaying){CancelCombat();return;}
            if(!health.IsAlive){SimulateRespawn(deltaTime);return;}
            if(State==NeutralBossState.Dormant)State=NeutralBossState.Alive;
            if(State==NeutralBossState.Resetting){SimulateReset(deltaTime);return;}
            if(OutsideLeash() || TargetOutsideLeash(target)){BeginReset();return;}
            specialCooldownRemaining=Mathf.Max(0f,specialCooldownRemaining-deltaTime);
            if(casting){if((castElapsed+=deltaTime)>=specialCastPoint)ReleaseSpecial();return;}
            // Keep chasing the current attacker while it stays a valid target, even if it
            // leaves the aggro bubble, so the boss actually follows whoever is hitting it.
            // Only acquire a NEW target from within the aggro radius around the pit; when
            // the target is gone and the boss has stepped off the pit it walks back and
            // resets (leash below caps how far it can be pulled from the pit).
            if(!IsValidTarget(target)){target=SelectThreatTarget();if(target==null)target=FindTarget();}
            if(target==null){if(AwayFromHome()){BeginReset();return;}State=NeutralBossState.Alive;attack.ClearTarget();mover.ClearDestinationAndRemainReady();return;}
            State=NeutralBossState.Engaged;
            if(specialCooldownRemaining<=0f&&DistanceTo(target)<=specialRadius){BeginSpecial();return;}
            attack.SetTarget(target);
            attack.Simulate(deltaTime);
            // Movement follows actual range, not only BasicAttack's transient state.
            // A moving target can leave range during windup/backswing; the guardian
            // must keep following it until the leash rule starts a reset.
            if (!attack.IsInRange(target) || attack.NeedsApproach)
            {
                mover.MoveTo(attack.GetApproachPosition(target));
            }
            else
            {
                mover.Stop();
            }
        }
        private void OnDamageApplied(Health victim,DamageContext context)
        {
            if(victim!=health||context.Attacker==null||!context.Attacker.TryGetComponent(out Health attacker)||!IsValidTarget(attacker)||State==NeutralBossState.Resetting)return;
            threat.TryGetValue(attacker,out float current);threat[attacker]=current+Mathf.Max(0f,context.Amount);target=SelectThreatTarget();State=NeutralBossState.Engaged;
        }
        private Health SelectThreatTarget(){Health best=null;float high=float.NegativeInfinity;int bestId=int.MaxValue;List<Health> remove=null;foreach(KeyValuePair<Health,float> pair in threat){if(!IsValidTarget(pair.Key)||!WithinAggro(pair.Key)){(remove??=new List<Health>()).Add(pair.Key);continue;}int id=pair.Key.GetEntityId().GetHashCode();if(pair.Value>high||(Mathf.Approximately(pair.Value,high)&&id<bestId)){best=pair.Key;high=pair.Value;bestId=id;}}if(remove!=null)for(int i=0;i<remove.Count;i++)threat.Remove(remove[i]);return best;}
        private Health FindTarget()
        {
            // Heroes are registered by progression, so re-aggro does not depend on a
            // collider having updated after NavMesh return/warp. Creeps are still
            // collected with the reusable physics buffer below.
            HeroProgression.CopyActiveHeroesTo(heroes);
            Health best=null;float distance=float.PositiveInfinity;
            for(int i=0;i<heroes.Count;i++)
            {
                HeroProgression hero=heroes[i];
                Health candidate=hero!=null?hero.GetComponent<Health>():null;
                if(!IsValidTarget(candidate)||!WithinAggro(candidate))continue;
                float d=DistanceTo(candidate);if(d<distance){best=candidate;distance=d;}
            }
            int count=Physics.OverlapSphereNonAlloc(transform.position,aggroRadius,overlap,targetMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                Collider hit=overlap[i];overlap[i]=null;Health candidate=hit!=null?hit.GetComponentInParent<Health>():null;
                if(!IsValidTarget(candidate)||!WithinAggro(candidate))continue;
                float d=DistanceTo(candidate);if(d<distance){best=candidate;distance=d;}
            }
            return best;
        }
        private bool IsValidTarget(Health candidate){return candidate!=null&&candidate.IsAlive&&candidate.TryGetComponent(out TeamMember member)&&team.IsEnemy(member)&&member.Team!=TeamId.Neutral&&!candidate.TryGetComponent(out StructureEntity _)&&attack.CanAttack(candidate);}
        private bool WithinAggro(Health candidate){Vector3 d=candidate.transform.position-home;d.y=0f;return d.sqrMagnitude<=aggroRadius*aggroRadius;}
        private float DistanceTo(Health candidate){Vector3 d=candidate.transform.position-transform.position;d.y=0f;return d.magnitude;}
        private bool OutsideLeash(){Vector3 d=transform.position-home;d.y=0f;return d.sqrMagnitude>leashRange*leashRange;}
        private bool TargetOutsideLeash(Health candidate)
        {
            if(candidate==null)return false;
            Vector3 d=candidate.transform.position-home;d.y=0f;
            return d.sqrMagnitude>leashRange*leashRange;
        }
        private bool AwayFromHome(){Vector3 d=transform.position-home;d.y=0f;return d.sqrMagnitude>resetRadius*resetRadius;}
        private void BeginSpecial(){casting=true;castElapsed=0f;attack.ClearTarget();mover.Stop();SetTelegraph(true);}
        private void ReleaseSpecial()
        {
            if(!casting)return;
            SetTelegraph(false);casting=false;specialCooldownRemaining=specialCooldown;

            // The current target is already known to be inside the area when the cast
            // starts. Applying it explicitly makes the gameplay independent from a
            // collider's update timing (and avoids missing a target without one).
            ApplySpecialTo(target);
            int count=Physics.OverlapSphereNonAlloc(transform.position,specialRadius,overlap,targetMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                Collider hit=overlap[i];overlap[i]=null;
                Health candidate=hit!=null?hit.GetComponentInParent<Health>():null;
                if(candidate==target)continue;
                ApplySpecialTo(candidate);
            }
        }
        private void ApplySpecialTo(Health candidate)
        {
            if(!IsValidTarget(candidate)||DistanceTo(candidate)>specialRadius)return;
            candidate.ApplyDamage(new DamageContext(team,specialDamage,AttackDelivery.Melee));
            if(candidate.TryGetComponent(out StatusEffectController effects))effects.Apply(new StatusEffectSpec{Id="boss.cierzo.slow",Type=StatusEffectType.Slow,Duration=specialSlowDuration,Magnitude=specialSlow,StackRule=StatusStackRule.RefreshDuration,ClearOnDeath=true});
        }
        private void BeginReset(){State=NeutralBossState.Resetting;resetStuckElapsed=0f;threat.Clear();CancelCombat();}
        private void SimulateReset(float deltaTime)
        {
            Vector3 destination=ResolveHomeOnNavMesh();
            mover.MoveTo(destination);
            Vector3 d=transform.position-destination;d.y=0f;
            if(d.sqrMagnitude<=resetRadius*resetRadius){CompleteReset();return;}
            // A malformed or newly rebuilt NavMesh must not strand the objective
            // forever. The normal path remains movement; this is a bounded fallback.
            resetStuckElapsed+=deltaTime;
            if(resetStuckElapsed>=6f){mover.WarpTo(destination);CompleteReset();}
        }
        private Vector3 ResolveHomeOnNavMesh()
        {
            return NavMesh.SamplePosition(home,out NavMeshHit hit,8f,NavMesh.AllAreas)?hit.position:home;
        }
        private void CompleteReset()
        {
            // Clear the return route first, then explicitly re-arm the same agent for
            // the next engagement. Stop() is correct while returning ends, but it
            // leaves NavMeshAgent.isStopped true; without this transition a target in
            // range can still be attacked while a distant target is never pursued.
            mover.Stop();
            attack.ClearTarget();
            target=null;
            threat.Clear();
            casting=false;
            specialCooldownRemaining=0f;
            SetTelegraph(false);
            health.RestoreFull();
            selfEffects?.ClearAll();
            mover.RearmNavigation();
            State=NeutralBossState.Alive;
            resetStuckElapsed=0f;
        }
        private void OnDied(Health _,DamageContext context){if(State==NeutralBossState.Dead||State==NeutralBossState.Respawning)return;CancelCombat();State=NeutralBossState.Dead;SetPresentation(false);GrantReward(context.Attacker);State=NeutralBossState.Respawning;respawnElapsed=0f;}
        private void GrantReward(TeamMember attacker){if(rewardGranted||attacker==null||!attacker.TryGetComponent(out HeroUnit _)||attacker.Team==TeamId.Neutral||(MatchStateController.Active!=null&&!MatchStateController.Active.IsPlaying))return;rewardGranted=true;TeamId winner=attacker.Team;HeroProgression.CopyActiveHeroesTo(heroes);for(int i=0;i<heroes.Count;i++){HeroProgression hero=heroes[i];if(hero==null||!hero.TryGetComponent(out TeamMember member)||member.Team!=winner)continue;if(hero.TryGetComponent(out StatusEffectController effects)){effects.Apply(new StatusEffectSpec{Id="boss.cierzo.ascendant.damage",Type=StatusEffectType.DamageBuff,Duration=teamBuffDuration,Magnitude=teamDamageBonus,StackRule=StatusStackRule.RefreshDuration,ClearOnDeath=false});effects.Apply(new StatusEffectSpec{Id="boss.cierzo.ascendant.move",Type=StatusEffectType.MoveSpeedBuff,Duration=teamBuffDuration,Magnitude=teamMoveBonus,StackRule=StatusStackRule.RefreshDuration,ClearOnDeath=false});}if(hero.TryGetComponent(out HeroEconomy economy))economy.TryAddGold(teamGold);}string message=$"{winner} ha derrotado al Guardián del Cierzo";Announced?.Invoke(winner,message);BossAnnouncementFeedback.Show(message);RewardGranted?.Invoke(this,winner);}
        private void SimulateRespawn(float delta){if(State!=NeutralBossState.Respawning)return;if(MatchStateController.Active!=null&&!MatchStateController.Active.IsPlaying)return;respawnElapsed+=delta;if(respawnElapsed<respawnDelay)return;rewardGranted=false;threat.Clear();specialCooldownRemaining=0f;transform.position=home;health.RestoreFull();selfEffects?.ClearAll();SetPresentation(true);State=NeutralBossState.Alive;}
        private void CancelCombat(){target=null;attack?.ClearTarget();mover?.Stop();casting=false;SetTelegraph(false);}
        private void SetTelegraph(bool active){if(telegraphRenderer!=null)telegraphRenderer.enabled=active;TelegraphChanged?.Invoke(this,active);}
        private void SetPresentation(bool visible){if(presentationRenderers!=null)for(int i=0;i<presentationRenderers.Length;i++)if(presentationRenderers[i]!=null)presentationRenderers[i].enabled=visible;if(presentationColliders!=null)for(int i=0;i<presentationColliders.Length;i++)if(presentationColliders[i]!=null)presentationColliders[i].enabled=visible;}
        private bool IsDeadOrRespawning()=>State==NeutralBossState.Dead||State==NeutralBossState.Respawning;
    }
}

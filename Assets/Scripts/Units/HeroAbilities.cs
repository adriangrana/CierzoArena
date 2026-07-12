using System;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    public interface IHeroAbilityRequestGateway
    {
        bool IsReady { get; }
        void RequestUpgrade(int slot);
        void RequestCast(int slot, Health target, Vector3 point);
    }

    public enum AbilityCastState { Idle, Casting, Recovery }

    [RequireComponent(typeof(HeroUnit))]
    [RequireComponent(typeof(HeroMana))]
    [RequireComponent(typeof(HeroProgression))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(TeamMember))]
    public sealed class HeroAbilities : MonoBehaviour
    {
        [SerializeField] private AbilityDefinition[] abilities = new AbilityDefinition[4];
        [SerializeField, Min(0)] private int startingSkillPoints = 1;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.1f;
        [SerializeField] private bool authorityEnabled = true;
        private readonly int[] levels = new int[4];
        private readonly float[] cooldowns = new float[4];
        private HeroMana mana; private HeroProgression progression; private Health health; private TeamMember team; private ClickMover mover; private BasicAttack attack;
        private int skillPoints; private AbilityCastState castState; private int pendingSlot = -1; private Health pendingTarget; private Vector3 pendingPoint; private float stateTime; private float speedBonusUntil; private float speedBonus;
        private bool initialized;

        public AbilityCastState CastState => castState;
        public int SkillPoints { get { EnsureInitialized(); return skillPoints; } }
        public AbilityDefinition GetDefinition(int slot) => slot >= 0 && slot < abilities.Length ? abilities[slot] : null;
        public int GetLevel(int slot) => slot >= 0 && slot < levels.Length ? levels[slot] : 0;
        public float GetCooldown(int slot) => slot >= 0 && slot < cooldowns.Length ? Mathf.Max(0f, cooldowns[slot]) : 0f;
        public event Action<HeroAbilities> Changed;
        public event Action<HeroAbilities, Vector3, Health, float, float> ProjectileReleased;

        private void Awake() => EnsureInitialized();
        private void Update() => Simulate(Time.deltaTime);
        private void OnDestroy()
        {
            if (progression != null) progression.LevelUp -= OnLevelUp;
            if (health != null) health.Died -= OnDied;
        }

        public void EnsureInitialized()
        {
            if (initialized) return;
            mana = GetComponent<HeroMana>(); progression = GetComponent<HeroProgression>(); health = GetComponent<Health>(); team = GetComponent<TeamMember>();
            TryGetComponent(out mover); TryGetComponent(out attack);
            skillPoints = Mathf.Max(0, startingSkillPoints);
            progression.LevelUp += OnLevelUp;
            health.Died += OnDied;
            initialized = true;
        }

        public void SetAuthorityEnabled(bool enabled) => authorityEnabled = enabled;
        public void ConfigureHeroKit(AbilityDefinition[] definitionSet)
        {
            abilities=definitionSet!=null&&definitionSet.Length==4?definitionSet:new AbilityDefinition[4];
            for(int i=0;i<levels.Length;i++){levels[i]=0;cooldowns[i]=0f;}skillPoints=Mathf.Max(0,startingSkillPoints);ClearPending();Changed?.Invoke(this);
        }
        public bool TryUpgrade(int slot)
        {
            EnsureInitialized();
            AbilityDefinition definition = GetDefinition(slot);
            if (!authorityEnabled || definition == null || skillPoints <= 0 || levels[slot] >= definition.MaximumLevel || progression.Level < definition.RequiredHeroLevel(levels[slot] + 1)) return false;
            levels[slot]++; skillPoints--; Changed?.Invoke(this); return true;
        }

        public bool TryStartCast(int slot, Health target, Vector3 point)
        {
            EnsureInitialized();
            AbilityDefinition definition = GetDefinition(slot);
            if (!authorityEnabled || castState != AbilityCastState.Idle || definition == null || levels[slot] <= 0 || GetCooldown(slot) > 0f || !CanPlay() || !mana || mana.CurrentMana < definition.ManaCost(levels[slot]) || !IsValidTarget(definition, target, point)) return false;
            pendingSlot = slot; pendingTarget = target; pendingPoint = point; stateTime = 0f; castState = AbilityCastState.Casting;
            attack?.ClearTarget(); mover?.Stop(); Changed?.Invoke(this); return true;
        }

        public bool CancelBeforeRelease()
        {
            if (!authorityEnabled || castState != AbilityCastState.Casting) return false;
            ClearPending(); Changed?.Invoke(this); return true;
        }

        public void Simulate(float deltaTime)
        {
            EnsureInitialized();
            if (!authorityEnabled) return;
            if (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying)
            {
                CancelBeforeRelease(); return;
            }
            deltaTime = Mathf.Max(0f, deltaTime);
            bool changed = false;
            for (int i = 0; i < cooldowns.Length; i++) { float next = Mathf.Max(0f, cooldowns[i] - deltaTime); changed |= !Mathf.Approximately(next, cooldowns[i]); cooldowns[i] = next; }
            if (speedBonusUntil > 0f && Time.time >= speedBonusUntil) { speedBonusUntil = 0f; speedBonus = 0f; mover?.SetAbilityMoveSpeedBonus(0f); changed = true; }
            if (castState == AbilityCastState.Casting)
            {
                AbilityDefinition definition = GetDefinition(pendingSlot);
                if (definition == null || !CanPlay() || !IsValidTarget(definition, pendingTarget, pendingPoint)) { ClearPending(); changed = true; }
                else if ((stateTime += deltaTime) >= definition.CastPoint)
                {
                    if (mana.TrySpend(definition.ManaCost(levels[pendingSlot])))
                    {
                        cooldowns[pendingSlot] = definition.Cooldown(levels[pendingSlot]);
                        Execute(definition, levels[pendingSlot], pendingTarget, pendingPoint);
                        castState = AbilityCastState.Recovery; stateTime = 0f; pendingTarget = null; changed = true;
                    }
                    else { ClearPending(); changed = true; }
                }
            }
            else if (castState == AbilityCastState.Recovery && (stateTime += deltaTime) >= recoveryDuration) { ClearPending(); changed = true; }
            if (changed) Changed?.Invoke(this);
        }

        public void ApplyAuthoritativeState(int replicatedPoints, int[] replicatedLevels, float[] replicatedCooldowns)
        {
            EnsureInitialized(); authorityEnabled = false;
            skillPoints = Mathf.Max(0, replicatedPoints);
            for (int i = 0; i < levels.Length; i++) { levels[i] = Mathf.Max(0, replicatedLevels != null && i < replicatedLevels.Length ? replicatedLevels[i] : 0); cooldowns[i] = Mathf.Max(0f, replicatedCooldowns != null && i < replicatedCooldowns.Length ? replicatedCooldowns[i] : 0f); }
            Changed?.Invoke(this);
        }

        private bool CanPlay() => health != null && health.IsAlive && (MatchStateController.Active == null || MatchStateController.Active.IsPlaying) && (!TryGetComponent(out HeroLifeCycle life) || life.IsAliveForGameplay) && (!TryGetComponent(out StatusEffectController effects) || effects.CanCast);
        private bool IsValidTarget(AbilityDefinition definition, Health target, Vector3 point)
        {
            if (definition.Targeting == AbilityTargeting.NoTarget) return true;
            if (definition.Targeting == AbilityTargeting.UnitTarget)
            {
                return target != null && target.IsAlive && target != health && target.TryGetComponent(out TeamMember targetTeam) && targetTeam.Team != team.Team && !target.TryGetComponent(out StructureEntity _) && (!TryGetComponent(out VisionSource _) || VisionSource.IsVisible(team.Team, target.transform.position)) && Vector3.Distance(transform.position, target.transform.position) <= definition.Range;
            }
            return IsFinite(point) && Mathf.Abs(point.y - transform.position.y) <= 4f && Vector3.Distance(transform.position, point) <= definition.Range;
        }
        private void Execute(AbilityDefinition definition, int level, Health target, Vector3 point)
        {
            float value = definition.EffectValue(level);
            switch (definition.Effect)
            {
                case AbilityEffect.ProjectileDamage:
                    GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    projectile.name = "Ability Projectile"; projectile.transform.position = transform.position + Vector3.up * 1.1f; projectile.transform.localScale = Vector3.one * 0.28f;
                    Collider projectileCollider = projectile.GetComponent<Collider>();
                    if (Application.isPlaying) Destroy(projectileCollider);
                    else DestroyImmediate(projectileCollider);
                    projectile.AddComponent<VisionEffectVisibility>();
                    projectile.AddComponent<AbilityProjectile>().Configure(target, team, value, definition.ProjectileSpeed, 4f);
                    ProjectileReleased?.Invoke(this, projectile.transform.position, target, definition.ProjectileSpeed, 4f);
                    break;
                case AbilityEffect.AreaDamage: DamageArea(point, definition.AreaRadius, value); break;
                case AbilityEffect.StrongAreaDamage: DamageArea(point, definition.AreaRadius, value); break;
                case AbilityEffect.AreaSlow: ApplyAreaStatus(point, definition.AreaRadius, StatusEffectType.Slow, value, definition.Duration); break;
                case AbilityEffect.StrongAreaStun: DamageArea(point, definition.AreaRadius, value); ApplyAreaStatus(point, definition.AreaRadius, StatusEffectType.Stun, 0f, definition.Duration); break;
                case AbilityEffect.SelfShield: GetComponent<StatusEffectController>()?.Apply(new StatusEffectSpec { Id = definition.AbilityId, Type = StatusEffectType.Shield, Duration = definition.Duration, Magnitude = value, StackRule = StatusStackRule.RefreshDuration, ClearOnDeath = true }); break;
                case AbilityEffect.SelfMoveSpeed:
                    speedBonus = value; speedBonusUntil = Time.time + definition.Duration; mover?.SetAbilityMoveSpeedBonus(speedBonus); break;
            }
        }
        private void DamageArea(Vector3 center, float radius, float damage)
        {
            Collider[] hits = Physics.OverlapSphere(center, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Health candidate = hits[i].GetComponentInParent<Health>();
                if (candidate == null || !candidate.IsAlive || candidate == health || !candidate.TryGetComponent(out TeamMember candidateTeam) || candidateTeam.Team == team.Team) continue;
                candidate.ApplyDamage(new DamageContext(team, damage, AttackDelivery.Ranged));
            }
        }
        private void ApplyAreaStatus(Vector3 center, float radius, StatusEffectType type, float magnitude, float duration)
        {
            foreach (Collider hit in Physics.OverlapSphere(center, radius))
            {
                Health candidate = hit.GetComponentInParent<Health>();
                if (candidate == null || candidate == health || !candidate.TryGetComponent(out TeamMember member) || member.Team == team.Team) continue;
                candidate.GetComponent<StatusEffectController>()?.Apply(new StatusEffectSpec { Id = $"ability.{type}", Type = type, Duration = duration, Magnitude = magnitude, StackRule = StatusStackRule.ReplaceIfStronger, ClearOnDeath = true });
            }
        }
        private void OnLevelUp(HeroProgression _, int __) { if (authorityEnabled) { skillPoints++; Changed?.Invoke(this); } }
        private void OnDied(Health _) { CancelBeforeRelease(); }
        private void ClearPending() { castState = AbilityCastState.Idle; pendingSlot = -1; pendingTarget = null; stateTime = 0f; }
        private static bool IsFinite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}

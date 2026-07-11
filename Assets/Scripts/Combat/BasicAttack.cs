using System;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Combat
{
    /// <summary>
    /// Reusable authoritative attack timeline. Consumers provide target acquisition
    /// and movement; this component owns windup, attack point, backswing, cadence and
    /// melee/ranged delivery. It never applies damage on intent/start.
    /// </summary>
    [RequireComponent(typeof(TeamMember))]
    [RequireComponent(typeof(Health))]
    public sealed class BasicAttack : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damage = 45f;
        [SerializeField, Min(0f)] private float range = 2.1f;
        [SerializeField, Min(0.01f)] private float attackInterval = 1.25f;
        // Kept as a compatibility/inspection field for existing UnitDefinition tests.
        [SerializeField, Min(0.01f)] private float attacksPerSecond = 0.8f;
        [SerializeField, Min(0f)] private float attackPoint = 0.3f;
        [SerializeField, Min(0f)] private float backswing = 0.35f;
        [SerializeField] private AttackDelivery delivery = AttackDelivery.Melee;
        [SerializeField, Min(0f)] private float meleeImpactTolerance = 0.2f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 14f;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 4f;
        [SerializeField, Min(0.01f)] private float projectileImpactRadius = 0.2f;
        [SerializeField] private Material projectileMaterial;
        [SerializeField] private bool useUnitDefinition = true;

        private TeamMember teamMember;
        private Health health;
        private Health target;
        private AttackState state;
        private float stateElapsed;
        private float cooldownRemaining;
        private bool releasedThisCycle;
        private bool projectilePresentationEnabled = true;

        public AttackState State => state;
        public Health Target => target;
        public float Range => Mathf.Max(0f, range);
        public float Damage => Mathf.Max(0f, damage);
        public float AttackInterval => Mathf.Max(0.01f, attackInterval);
        public AttackDelivery Delivery => delivery;
        public bool NeedsApproach => state == AttackState.Approaching;
        public float ProjectileSpeed => Mathf.Max(0.01f, projectileSpeed);
        public float ProjectileLifetime => Mathf.Max(0.01f, projectileLifetime);

        public event Action<BasicAttack, Health, AttackDelivery> AttackPointReached;
        public event Action<BasicAttack, Health> ProjectileReleased;

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Safe lazy setup for deterministic Edit Mode construction.</summary>
        public void EnsureInitialized()
        {
            if (teamMember != null && health != null)
            {
                return;
            }

            teamMember = GetComponent<TeamMember>();
            health = GetComponent<Health>();
            UnitDefinition definition = ResolveDefinition();
            if (useUnitDefinition && definition != null)
            {
                damage = definition.AttackDamage;
                range = definition.AttackRange;
                attackInterval = 1f / definition.AttacksPerSecond;
                attacksPerSecond = definition.AttacksPerSecond;
            }

            NormalizeTimings();
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            range = Mathf.Max(0f, range);
            projectileSpeed = Mathf.Max(0.01f, projectileSpeed);
            projectileLifetime = Mathf.Max(0.01f, projectileLifetime);
            projectileImpactRadius = Mathf.Max(0.01f, projectileImpactRadius);
            meleeImpactTolerance = Mathf.Max(0f, meleeImpactTolerance);
            attacksPerSecond = Mathf.Max(0.01f, attacksPerSecond);
            NormalizeTimings();
        }

        public void SetTarget(Health newTarget)
        {
            if (target == newTarget)
            {
                return;
            }

            CancelCurrentCycle();
            target = newTarget;
        }

        /// <summary>
        /// Netcode uses a replicated visual projectile. The local logical projectile
        /// still performs server-side impact validation, but its mesh is hidden so a
        /// host never sees a duplicate.
        /// </summary>
        public void SetProjectilePresentationEnabled(bool enabled)
        {
            projectilePresentationEnabled = enabled;
        }

        /// <summary>Compatibility intent entry point. Damage still waits for Simulate to reach attackPoint.</summary>
        public bool TryAttack(Health newTarget)
        {
            if (!CanAttack(newTarget))
            {
                return false;
            }

            SetTarget(newTarget);
            Simulate(0f);
            return true;
        }

        public void CancelCurrentCycle()
        {
            if (state == AttackState.Windup || state == AttackState.Approaching || state == AttackState.Backswing)
            {
                // Cooldown is intentionally preserved after an attack point, so
                // cancelling backswing cannot increase attack speed.
                state = AttackState.Idle;
                stateElapsed = 0f;
                releasedThisCycle = false;
            }
        }

        public void ClearTarget()
        {
            CancelCurrentCycle();
            target = null;
        }

        /// <summary>Advances at most one attack point; safe for deterministic tests.</summary>
        public bool Simulate(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);

            if (!CanAttack(target))
            {
                ClearTarget();
                return false;
            }

            if (state == AttackState.Idle)
            {
                if (!IsInRange(target))
                {
                    state = AttackState.Approaching;
                    return false;
                }

                if (cooldownRemaining <= 0f)
                {
                    BeginWindup();
                }

                return false;
            }

            if (state == AttackState.Approaching)
            {
                if (IsInRange(target) && cooldownRemaining <= 0f)
                {
                    BeginWindup();
                }

                return false;
            }

            if (state == AttackState.Windup)
            {
                stateElapsed += deltaTime;
                if (!releasedThisCycle && stateElapsed >= attackPoint)
                {
                    releasedThisCycle = true;
                    ReleaseAttackPoint();
                    cooldownRemaining = AttackInterval;
                    state = AttackState.Backswing;
                    stateElapsed = 0f;
                    return true;
                }

                return false;
            }

            // Backswing is visual recovery. It can be cancelled externally but does
            // not decide cadence; cooldownRemaining remains the authoritative gate.
            stateElapsed += deltaTime;
            if (stateElapsed >= backswing)
            {
                state = AttackState.Idle;
                stateElapsed = 0f;
                releasedThisCycle = false;
            }

            return false;
        }

        public bool CanAttack(Health candidate)
        {
            EnsureInitialized();
            if (health == null || !health.IsAlive || candidate == null || !candidate.IsAlive ||
                (MatchStateController.Active != null && !MatchStateController.Active.CanAcceptGameplay))
            {
                return false;
            }

            TeamMember targetTeam = candidate.GetComponent<TeamMember>();
            if (teamMember == null || !teamMember.IsEnemy(targetTeam))
            {
                return false;
            }

            return !candidate.TryGetComponent(out StructureEntity structure) || structure.CanReceiveDamageFrom(teamMember);
        }

        public bool IsInRange(Health candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            float effectiveRange = Range + (delivery == AttackDelivery.Melee ? meleeImpactTolerance : 0f);
            return (GetApproachPosition(candidate) - transform.position).sqrMagnitude <= effectiveRange * effectiveRange;
        }

        public Vector3 GetApproachPosition(Health candidate)
        {
            if (candidate != null && candidate.TryGetComponent(out StructureEntity structure))
            {
                return structure.GetApproachPoint(transform.position);
            }

            if (candidate == null)
            {
                return transform.position;
            }

            // Collider surfaces make unit-to-unit range match the structure approach
            // rule: an attacker stops at the target footprint rather than its centre.
            Collider targetCollider = candidate.GetComponentInChildren<Collider>();
            return targetCollider != null
                ? targetCollider.ClosestPoint(transform.position)
                : candidate.transform.position;
        }

        private void BeginWindup()
        {
            state = AttackState.Windup;
            stateElapsed = 0f;
            releasedThisCycle = false;
        }

        private void ReleaseAttackPoint()
        {
            Health releasedTarget = target;
            AttackPointReached?.Invoke(this, releasedTarget, delivery);
            if (delivery == AttackDelivery.Melee)
            {
                ApplyMeleeImpact(releasedTarget);
                return;
            }

            LaunchProjectile(releasedTarget);
            ProjectileReleased?.Invoke(this, releasedTarget);
        }

        private void ApplyMeleeImpact(Health victim)
        {
            if (!CanAttack(victim) || !IsInRange(victim) || Damage <= 0f)
            {
                return;
            }

            if (victim.TryGetComponent(out StructureEntity structure))
            {
                structure.TryApplyDamage(teamMember, Damage);
            }
            else
            {
                victim.ApplyDamage(Damage);
            }
        }

        private void LaunchProjectile(Health victim)
        {
            if (!CanAttack(victim))
            {
                return;
            }

            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "Attack Projectile";
            projectileObject.transform.position = transform.position + Vector3.up * 1.1f;
            projectileObject.transform.localScale = Vector3.one * 0.28f;
            Collider projectileCollider = projectileObject.GetComponent<Collider>();
            if (projectileCollider != null)
            {
                DestroyForCurrentMode(projectileCollider);
            }

            Renderer renderer = projectileObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = projectilePresentationEnabled;
                if (projectileMaterial != null)
                {
                    renderer.sharedMaterial = projectileMaterial;
                }
            }

            AttackProjectile projectile = projectileObject.AddComponent<AttackProjectile>();
            projectile.Launch(victim, teamMember, Damage, projectileSpeed, projectileLifetime, projectileImpactRadius);
        }

        private UnitDefinition ResolveDefinition()
        {
            return TryGetComponent(out UnitDefinitionProvider provider) ? provider.Definition : null;
        }

        private void NormalizeTimings()
        {
            attackInterval = Mathf.Max(0.01f, attackInterval);
            attackPoint = Mathf.Clamp(attackPoint, 0f, attackInterval);
            backswing = Mathf.Clamp(backswing, 0f, attackInterval - attackPoint);
        }

        private static void DestroyForCurrentMode(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}

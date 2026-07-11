using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Combat
{
    /// <summary>Server/local-authoritative homing projectile. It owns no attack cadence.</summary>
    public sealed class AttackProjectile : MonoBehaviour
    {
        private Health target;
        private TeamMember attackerTeam;
        private float damage;
        private float speed;
        private float lifetime;
        private float impactRadius;
        private float elapsed;
        private bool activeProjectile;

        public void Launch(Health targetHealth, TeamMember sourceTeam, float damageAmount, float projectileSpeed, float maxLifetime, float hitRadius)
        {
            target = targetHealth;
            attackerTeam = sourceTeam;
            damage = Mathf.Max(0f, damageAmount);
            speed = Mathf.Max(0.01f, projectileSpeed);
            lifetime = Mathf.Max(0.01f, maxLifetime);
            impactRadius = Mathf.Max(0.01f, hitRadius);
            activeProjectile = true;
        }

        private void Update()
        {
            Simulate(Time.deltaTime);
        }

        public bool Simulate(float deltaTime)
        {
            if (!activeProjectile)
            {
                return false;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            elapsed += deltaTime;
            if (elapsed >= lifetime || !IsValidTarget())
            {
                DestroyForCurrentMode();
                activeProjectile = false;
                return false;
            }

            Vector3 destination = target.transform.position;
            Vector3 offset = destination - transform.position;
            float step = speed * deltaTime;
            if (offset.sqrMagnitude <= impactRadius * impactRadius || step >= offset.magnitude)
            {
                ApplyImpact();
                DestroyForCurrentMode();
                activeProjectile = false;
                return true;
            }

            transform.position += offset.normalized * step;
            if (offset.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(offset);
            }

            return false;
        }

        private bool IsValidTarget()
        {
            if (target == null || attackerTeam == null || !target.IsAlive)
            {
                return false;
            }

            TeamMember targetTeam = target.GetComponent<TeamMember>();
            return attackerTeam.IsEnemy(targetTeam) &&
                (MatchStateController.Active == null || MatchStateController.Active.IsPlaying);
        }

        private void ApplyImpact()
        {
            if (!IsValidTarget() || damage <= 0f)
            {
                return;
            }

            if (target.TryGetComponent(out StructureEntity structure))
            {
                structure.TryApplyDamage(attackerTeam, damage);
            }
            else
            {
                target.ApplyDamage(damage);
            }
        }

        private void DestroyForCurrentMode()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}

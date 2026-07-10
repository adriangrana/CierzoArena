using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Combat
{
    [RequireComponent(typeof(TeamMember))]
    [RequireComponent(typeof(Health))]
    public sealed class BasicAttack : MonoBehaviour
    {
        [SerializeField] private float damage = 45f;
        [SerializeField] private float range = 2.1f;
        [SerializeField] private float attacksPerSecond = 0.7f;

        private TeamMember teamMember;
        private Health health;
        private float nextAttackTime;

        public float Range => Mathf.Max(0f, range);

        private void Awake()
        {
            teamMember = GetComponent<TeamMember>();
            health = GetComponent<Health>();
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            range = Mathf.Max(0f, range);
            attacksPerSecond = Mathf.Max(0.01f, attacksPerSecond);
        }

        public bool CanAttack(Health target)
        {
            if (health == null || !health.IsAlive || target == null || !target.IsAlive)
            {
                return false;
            }

            TeamMember targetTeam = target.GetComponent<TeamMember>();
            return teamMember.IsEnemy(targetTeam);
        }

        public bool TryAttack(Health target)
        {
            if (!CanAttack(target) || Time.time < nextAttackTime)
            {
                return false;
            }

            if (!IsInRange(target))
            {
                return false;
            }

            target.ApplyDamage(damage);
            nextAttackTime = Time.time + 1f / attacksPerSecond;
            return true;
        }

        public bool IsInRange(Health target)
        {
            if (target == null)
            {
                return false;
            }

            float squaredRange = Range * Range;
            return (target.transform.position - transform.position).sqrMagnitude <= squaredRange;
        }
    }
}

using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Combat
{
    [RequireComponent(typeof(TeamMember))]
    public sealed class BasicAttack : MonoBehaviour
    {
        [SerializeField] private float damage = 45f;
        [SerializeField] private float range = 2.1f;
        [SerializeField] private float attacksPerSecond = 0.7f;

        private TeamMember teamMember;
        private float nextAttackTime;

        public float Range => range;

        private void Awake()
        {
            teamMember = GetComponent<TeamMember>();
        }

        public bool CanAttack(Health target)
        {
            if (target == null || !target.IsAlive)
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

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > range)
            {
                return false;
            }

            target.ApplyDamage(damage);
            nextAttackTime = Time.time + 1f / attacksPerSecond;
            return true;
        }
    }
}

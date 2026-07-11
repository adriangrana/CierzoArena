using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>
    /// Listens only to confirmed hero-vs-hero damage and forwards defensive aggro to
    /// a nearby tower or creep. It keeps AI policy outside Health and is dormant on
    /// non-authoritative clients because replicated health changes do not emit damage.
    /// </summary>
    [RequireComponent(typeof(TeamMember))]
    public sealed class DefensiveAggroResponder : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float heroProtectionRange = 9f;
        [SerializeField, Min(0f)] private float defensiveAggroDuration = 4f;

        private TeamMember teamMember;
        private TowerController tower;
        private CreepController creep;

        private void Awake()
        {
            teamMember = GetComponent<TeamMember>();
            TryGetComponent(out tower);
            TryGetComponent(out creep);
        }

        private void OnEnable()
        {
            CombatEvents.DamageApplied += OnDamageApplied;
        }

        private void OnDisable()
        {
            CombatEvents.DamageApplied -= OnDamageApplied;
        }

        private void OnValidate()
        {
            heroProtectionRange = Mathf.Max(0f, heroProtectionRange);
            defensiveAggroDuration = Mathf.Max(0f, defensiveAggroDuration);
        }

        private void OnDamageApplied(Health victim, DamageContext context)
        {
            if (victim == null || context.Attacker == null || context.Amount <= 0f || teamMember == null ||
                (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying) ||
                !victim.TryGetComponent(out HeroUnit _) || !context.Attacker.TryGetComponent(out HeroUnit _))
            {
                return;
            }

            TeamMember victimTeam = victim.GetComponent<TeamMember>();
            if (victimTeam == null || !teamMember.IsEnemy(context.Attacker) || victimTeam.Team != teamMember.Team)
            {
                return;
            }

            Vector3 protectedOffset = victim.transform.position - transform.position;
            protectedOffset.y = 0f;
            if (protectedOffset.sqrMagnitude > heroProtectionRange * heroProtectionRange)
            {
                return;
            }

            if (tower != null)
            {
                tower.SetDefensiveAggro(context.Attacker.GetComponent<Health>(), defensiveAggroDuration);
            }
            else if (creep != null)
            {
                creep.SetDefensiveAggro(context.Attacker.GetComponent<Health>(), defensiveAggroDuration);
            }
        }
    }
}

using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using System.Collections.Generic;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>One-shot creep proximity XP plus last-hit gold, or M9 hero last-hit XP.</summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(TeamMember))]
    public sealed class ExperienceReward : MonoBehaviour
    {
        [SerializeField, Min(0)] private int experienceReward = 60;
        [SerializeField, Min(0f)] private float experienceRadius = 12f;
        [SerializeField, Min(0)] private int goldReward = 40;
        [SerializeField] private bool shareExperienceWithNearbyHeroes;
        private static readonly List<HeroProgression> nearbyHeroes = new();
        private Health health;
        private TeamMember team;
        private bool granted;

        private void Awake()
        {
            health = GetComponent<Health>();
            team = GetComponent<TeamMember>();
            health.DiedWithContext += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.DiedWithContext -= OnDied;
        }

        private void OnDied(Health _, DamageContext context)
        {
            if (granted ||
                (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying))
            {
                return;
            }

            granted = true;
            if (shareExperienceWithNearbyHeroes)
            {
                GrantProximityExperience();
                GrantLastHitGold(context.Attacker);
                return;
            }

            // Hero deaths retain M9's simple last-hit XP rule until hero assists and
            // bounty design are introduced in a later milestone.
            GrantLastHitExperience(context.Attacker);
        }

        private void GrantProximityExperience()
        {
            if (experienceReward <= 0) return;
            HeroProgression.CopyActiveHeroesTo(nearbyHeroes);
            for (int i = nearbyHeroes.Count - 1; i >= 0; i--)
            {
                HeroProgression candidate = nearbyHeroes[i];
                if (!IsEligibleNearbyHero(candidate)) nearbyHeroes.RemoveAt(i);
            }

            if (nearbyHeroes.Count == 0) return;
            nearbyHeroes.Sort((left, right) => left.GetEntityId().GetHashCode().CompareTo(right.GetEntityId().GetHashCode()));
            int share = experienceReward / nearbyHeroes.Count;
            int remainder = experienceReward % nearbyHeroes.Count;
            for (int i = 0; i < nearbyHeroes.Count; i++)
            {
                nearbyHeroes[i].TryGainExperience(share + (i < remainder ? 1 : 0));
            }
        }

        private bool IsEligibleNearbyHero(HeroProgression candidate)
        {
            if (candidate == null || !candidate.CanReceiveExperience ||
                !candidate.TryGetComponent(out TeamMember member) || !member.IsEnemy(team) ||
                !candidate.TryGetComponent(out Health heroHealth) || !heroHealth.IsAlive)
            {
                return false;
            }

            Vector3 offset = candidate.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= experienceRadius * experienceRadius;
        }

        private void GrantLastHitExperience(TeamMember attacker)
        {
            if (experienceReward > 0 && attacker != null && attacker.IsEnemy(team) &&
                attacker.TryGetComponent(out HeroProgression progression) && progression.CanReceiveExperience)
            {
                progression.TryGainExperience(experienceReward);
            }
        }

        private void GrantLastHitGold(TeamMember attacker)
        {
            if (goldReward > 0 && attacker != null && attacker.IsEnemy(team) &&
                attacker.TryGetComponent(out HeroEconomy economy) && economy.CanReceiveGold)
            {
                economy.TryAddGold(goldReward);
            }
        }
    }
}

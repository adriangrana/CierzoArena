using CierzoArena.Combat;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Structures
{
    /// <summary>
    /// Server/local-authority tower simulation. Candidate scans are periodic and
    /// allocation-free; target choice is nearest enemy health, then instance id.
    /// </summary>
    [RequireComponent(typeof(StructureEntity))]
    public sealed class TowerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float range = 9f;
        [SerializeField, Min(0f)] private float damage = 35f;
        [SerializeField, Min(0.01f)] private float attackInterval = 1f;
        [SerializeField, Min(0.01f)] private float searchInterval = 0.2f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private bool simulationEnabled = true;

        private readonly Collider[] overlapBuffer = new Collider[32];
        private StructureEntity structure;
        private TeamMember teamMember;
        private Health currentTarget;
        private float attackElapsed;
        private float searchElapsed;

        public float Range => Mathf.Max(0f, range);
        public float Damage => Mathf.Max(0f, damage);
        public float AttackInterval => Mathf.Max(0.01f, attackInterval);
        public Health CurrentTarget => currentTarget;
        public bool SimulationEnabled => simulationEnabled;

        private void Awake()
        {
            structure = GetComponent<StructureEntity>();
            teamMember = GetComponent<TeamMember>();
        }

        private void OnValidate()
        {
            range = Mathf.Max(0f, range);
            damage = Mathf.Max(0f, damage);
            attackInterval = Mathf.Max(0.01f, attackInterval);
            searchInterval = Mathf.Max(0.01f, searchInterval);
        }

        private void Update()
        {
            Simulate(Time.deltaTime);
        }

        public void SetSimulationEnabled(bool enabled)
        {
            simulationEnabled = enabled;
            if (!enabled)
            {
                currentTarget = null;
                attackElapsed = 0f;
            }
        }

        /// <summary>Explicit tick entry used by tests and by the normal Update loop.</summary>
        public bool Simulate(float deltaTime)
        {
            if (!simulationEnabled || structure == null || !structure.IsAlive ||
                (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying))
            {
                currentTarget = null;
                return false;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            searchElapsed += deltaTime;
            if (!IsValidTarget(currentTarget) || searchElapsed >= searchInterval)
            {
                currentTarget = FindBestTarget();
                searchElapsed = 0f;
            }

            if (currentTarget == null)
            {
                attackElapsed = 0f;
                return false;
            }

            attackElapsed += deltaTime;
            if (attackElapsed < AttackInterval)
            {
                return false;
            }

            // One attack per simulation tick caps a large hitch instead of creating a
            // burst of retroactive hits. Keep only the remainder for stable cadence.
            attackElapsed -= AttackInterval;
            if (!IsValidTarget(currentTarget) || Damage <= 0f)
            {
                return false;
            }

            currentTarget.ApplyDamage(Damage);
            return true;
        }

        public bool IsValidTarget(Health candidate)
        {
            if (candidate == null || !candidate.IsAlive || structure == null || !structure.IsAlive)
            {
                return false;
            }

            // M5 towers only target units. Structures expose a selectable collider so
            // player attacks can reach them, but are never tower candidates.
            if (candidate.TryGetComponent(out StructureEntity _))
            {
                return false;
            }

            TeamMember candidateTeam = candidate.GetComponent<TeamMember>();
            if (teamMember == null || !teamMember.IsEnemy(candidateTeam))
            {
                return false;
            }

            Vector3 offset = candidate.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= Range * Range;
        }

        public Health FindBestTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, Range, overlapBuffer, targetMask, QueryTriggerInteraction.Ignore);
            Health best = null;
            float bestDistance = float.PositiveInfinity;
            int bestId = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider candidateCollider = overlapBuffer[i];
                overlapBuffer[i] = null;
                if (candidateCollider == null)
                {
                    continue;
                }

                Health candidate = candidateCollider.GetComponentInParent<Health>();
                if (!IsValidTarget(candidate))
                {
                    continue;
                }

                Vector3 offset = candidate.transform.position - transform.position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                int id = candidate.GetEntityId().GetHashCode();
                if (distance < bestDistance || (Mathf.Approximately(distance, bestDistance) && id < bestId))
                {
                    best = candidate;
                    bestDistance = distance;
                    bestId = id;
                }
            }

            return best;
        }
    }
}

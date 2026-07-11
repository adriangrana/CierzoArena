using System;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>
    /// Server/local-authoritative lane unit. It owns route following and target
    /// selection only; all attack timing/damage stays in BasicAttack.
    /// </summary>
    [RequireComponent(typeof(TeamMember))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(BasicAttack))]
    [RequireComponent(typeof(ClickMover))]
    public sealed class CreepController : MonoBehaviour
    {
        [SerializeField] private CreepArchetype archetype = CreepArchetype.Melee;
        [SerializeField] private LaneRoute route;
        [SerializeField, Min(0f)] private float detectionRange = 7f;
        [SerializeField, Min(0f)] private float leashRange = 14f;
        [SerializeField, Min(0.01f)] private float searchInterval = 0.2f;
        [SerializeField, Min(0.05f)] private float waypointTolerance = 0.5f;
        [SerializeField, Min(0f)] private float deathCleanupDelay = 3f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private bool simulationEnabled = true;

        private readonly Collider[] overlapBuffer = new Collider[48];
        private TeamMember teamMember;
        private Health health;
        private BasicAttack attack;
        private ClickMover mover;
        private Health currentTarget;
        private int waypointIndex;
        private float searchElapsed;
        private float defensiveAggroRemaining;
        private float deathElapsed;
        private bool externallyDespawned;
        private Vector3 spawnPosition;

        public CreepArchetype Archetype => archetype;
        public Health CurrentTarget => currentTarget;
        public LaneRoute Route => route;
        public bool SimulationEnabled => simulationEnabled;
        public float LeashRange => Mathf.Max(0f, leashRange);
        public event Action<CreepController> DespawnRequested;

        private void Awake()
        {
            teamMember = GetComponent<TeamMember>();
            health = GetComponent<Health>();
            attack = GetComponent<BasicAttack>();
            mover = GetComponent<ClickMover>();
            spawnPosition = transform.position;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void OnValidate()
        {
            detectionRange = Mathf.Max(0f, detectionRange);
            leashRange = Mathf.Max(0f, leashRange);
            searchInterval = Mathf.Max(0.01f, searchInterval);
            waypointTolerance = Mathf.Max(0.05f, waypointTolerance);
            deathCleanupDelay = Mathf.Max(0f, deathCleanupDelay);
        }

        private void Update()
        {
            Simulate(Time.deltaTime);
        }

        public void ConfigureRoute(LaneRoute laneRoute)
        {
            route = laneRoute;
            waypointIndex = 0;
            spawnPosition = transform.position;
        }

        public void SetSimulationEnabled(bool enabled)
        {
            simulationEnabled = enabled;
            if (!enabled)
            {
                currentTarget = null;
                attack?.ClearTarget();
                mover?.Stop();
            }
        }

        public void SetExternalDespawn(bool enabled)
        {
            externallyDespawned = enabled;
        }

        public bool SetDefensiveAggro(Health aggressor, float duration)
        {
            if (!IsValidTarget(aggressor, requireDetectionRange: false) || !IsWithinLeash(aggressor))
            {
                return false;
            }

            currentTarget = aggressor;
            defensiveAggroRemaining = Mathf.Max(defensiveAggroRemaining, Mathf.Max(0f, duration));
            return true;
        }

        public bool Simulate(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            if (health == null || !health.IsAlive)
            {
                return SimulateDeath(deltaTime);
            }

            if (!simulationEnabled || (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying))
            {
                currentTarget = null;
                attack.ClearTarget();
                mover.Stop();
                return false;
            }

            bool defensiveWasActive = defensiveAggroRemaining > 0f;
            defensiveAggroRemaining = Mathf.Max(0f, defensiveAggroRemaining - deltaTime);
            bool defensive = defensiveAggroRemaining > 0f && IsValidTarget(currentTarget, requireDetectionRange: false) && IsWithinLeash(currentTarget);
            if (!defensive)
            {
                if (defensiveWasActive)
                {
                    currentTarget = null;
                }
                defensiveAggroRemaining = 0f;
                if (!IsValidTarget(currentTarget, requireDetectionRange: true))
                {
                    currentTarget = FindBestTarget();
                }
            }

            if (currentTarget == null)
            {
                attack.ClearTarget();
                FollowRoute();
                return false;
            }

            attack.SetTarget(currentTarget);
            bool released = attack.Simulate(deltaTime);
            if (attack.NeedsApproach)
            {
                mover.MoveTo(attack.GetApproachPosition(currentTarget));
            }
            else
            {
                mover.Stop();
            }

            return released;
        }

        public bool IsValidTarget(Health candidate, bool requireDetectionRange)
        {
            if (candidate == null || !candidate.IsAlive || teamMember == null || !attack.CanAttack(candidate))
            {
                return false;
            }

            if (!requireDetectionRange)
            {
                return true;
            }

            Vector3 offset = attack.GetApproachPosition(candidate) - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= detectionRange * detectionRange;
        }

        public Health FindBestTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, detectionRange, overlapBuffer, targetMask, QueryTriggerInteraction.Ignore);
            Health best = null;
            float bestDistance = float.PositiveInfinity;
            int bestId = int.MaxValue;
            int bestPriority = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapBuffer[i];
                overlapBuffer[i] = null;
                if (collider == null)
                {
                    continue;
                }

                Health candidate = collider.GetComponentInParent<Health>();
                if (!IsValidTarget(candidate, requireDetectionRange: true))
                {
                    continue;
                }

                Vector3 offset = attack.GetApproachPosition(candidate) - transform.position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                int id = candidate.GetEntityId().GetHashCode();
                // Heroes remain legal normal targets, but do not steal a lane creep's
                // focus merely by walking closer. Confirmed hero-vs-hero damage uses
                // SetDefensiveAggro and deliberately bypasses this priority.
                int priority = candidate.TryGetComponent(out HeroUnit _) ? 1 : 0;
                if (priority < bestPriority ||
                    (priority == bestPriority && (distance < bestDistance || (Mathf.Approximately(distance, bestDistance) && id < bestId))))
                {
                    best = candidate;
                    bestDistance = distance;
                    bestId = id;
                    bestPriority = priority;
                }
            }

            return best;
        }

        private void FollowRoute()
        {
            if (route == null || route.IsComplete(waypointIndex))
            {
                mover.Stop();
                return;
            }

            Vector3 waypoint = route.GetWaypoint(waypointIndex);
            Vector3 offset = waypoint - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= waypointTolerance * waypointTolerance)
            {
                waypointIndex++;
                if (route.IsComplete(waypointIndex))
                {
                    mover.Stop();
                    return;
                }

                waypoint = route.GetWaypoint(waypointIndex);
            }

            mover.MoveTo(waypoint);
        }

        private bool IsWithinLeash(Health candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            Vector3 offset = candidate.transform.position - spawnPosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= leashRange * leashRange;
        }

        private void OnDied(Health _)
        {
            currentTarget = null;
            attack.ClearTarget();
            mover.Stop();
        }

        private bool SimulateDeath(float deltaTime)
        {
            mover?.Stop();
            attack?.ClearTarget();
            deathElapsed += deltaTime;
            if (deathElapsed < deathCleanupDelay)
            {
                return false;
            }

            DespawnRequested?.Invoke(this);
            if (!externallyDespawned)
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

            return false;
        }
    }
}

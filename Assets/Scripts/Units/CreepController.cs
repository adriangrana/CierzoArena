using System;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;
using UnityEngine.AI;

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
        private NavMeshPath coreApproachPath;
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
        private Renderer[] presentationRenderers;
        private Collider[] presentationColliders;
        private StructureEntity finalCore;

        public CreepArchetype Archetype => archetype;
        public Health CurrentTarget => currentTarget;
        public LaneRoute Route => route;
        public bool SimulationEnabled => simulationEnabled;
        public float LeashRange => Mathf.Max(0f, leashRange);
        public int CurrentWaypointIndex => waypointIndex;
        public bool IsAtFinalWaypoint => route == null || route.IsComplete(waypointIndex);
        public Vector3 CurrentDestination => mover != null ? mover.LastRequestedDestination : transform.position;
        public StructureEntity FinalObjective => ResolveEnemyCore();
        public bool IsCoreVulnerable => ResolveEnemyCore() != null && finalCore.CanReceiveDamageFrom(teamMember);
        public event Action<CreepController> DespawnRequested;

        /// <summary>
        /// On-demand diagnostic data for the lane-end path. It is intentionally a
        /// query rather than a log, so investigating a stuck creep never floods the
        /// console during a wave.
        /// </summary>
        public CreepNavigationDebugInfo GetNavigationDebugInfo()
        {
            StructureEntity core = ResolveEnemyCore();
            bool reachable = core != null && TryFindNavigableCoreApproach(core, out _);
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            Health target = currentTarget;
            Vector3 targetPoint = target != null ? attack.GetApproachPosition(target) : CurrentDestination;
            float targetDistance = target != null ? Vector3.Distance(transform.position, targetPoint) : 0f;
            return new CreepNavigationDebugInfo(
                route != null ? route.name : "Sin ruta", teamMember != null ? teamMember.Team : TeamId.Neutral,
                target != null ? (attack.NeedsApproach ? "Approaching" : attack.State.ToString()) : (IsAtFinalWaypoint ? "CoreApproach" : "FollowingRoute"),
                waypointIndex, route != null ? route.Count : 0, CurrentDestination, target,
                target != null && target.TryGetComponent(out StructureEntity structure) ? structure.Kind.ToString() : target != null ? "Unit" : "None",
                targetDistance, agent != null && agent.hasPath, agent != null && agent.pathPending,
                agent != null ? agent.pathStatus : NavMeshPathStatus.PathInvalid,
                agent != null ? agent.remainingDistance : float.PositiveInfinity, IsAtFinalWaypoint,
                core != null && core.CanReceiveDamageFrom(teamMember), core != null && core.CanReceiveDamageFrom(teamMember), reachable);
        }

        private void Awake()
        {
            // NavMeshPath is a Unity engine object and cannot be constructed in a
            // MonoBehaviour field initializer. Awake is the first safe lifetime
            // point for this reusable path buffer.
            coreApproachPath = new NavMeshPath();
            teamMember = GetComponent<TeamMember>();
            health = GetComponent<Health>();
            attack = GetComponent<BasicAttack>();
            mover = GetComponent<ClickMover>();
            presentationRenderers = GetComponentsInChildren<Renderer>(true);
            presentationColliders = GetComponentsInChildren<Collider>(true);
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
            finalCore = IsEnemyCore(route != null ? route.FinalObjective : null) ? route.FinalObjective : null;
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

        /// <summary>Rebuilds the cached presentation list after an optional visual
        /// child (such as the goblin model) is attached at runtime.</summary>
        public void RefreshPresentation()
        {
            presentationRenderers = GetComponentsInChildren<Renderer>(true);
            presentationColliders = GetComponentsInChildren<Collider>(true);
        }

        public bool SetDefensiveAggro(Health aggressor, float duration)
        {
            // Lane towers deal damage but never force a creep to abandon its current
            // unit fight. A free creep may choose an attackable tower below; that is
            // normal target acquisition, not defensive aggro.
            if (aggressor != null && aggressor.TryGetComponent(out StructureEntity _))
            {
                return false;
            }

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
                // Creeps prefer opposing lane units, then heroes, and choose a
                // structure only when no living mobile enemy is available. That
                // prevents towers from stealing ongoing lane combat while allowing
                // an otherwise idle wave to siege an exposed, vulnerable tower.
                int priority = candidate.TryGetComponent(out StructureEntity _)
                    ? 2
                    : candidate.TryGetComponent(out HeroUnit _) ? 1 : 0;
                if (priority < bestPriority ||
                    (priority == bestPriority && (distance < bestDistance || (Mathf.Approximately(distance, bestDistance) && id < bestId))))
                {
                    best = candidate;
                    bestDistance = distance;
                    bestId = id;
                    bestPriority = priority;
                }
            }

            // Structure target colliders deliberately live on a selectable layer and
            // can be absent from an AI overlap query in compact/network scenes.
            // Resolve an idle siege target from the domain structures as a reliable
            // fallback rather than relying on that presentation collider. Cores are
            // included only after the progression rule has made them vulnerable.
            return best != null && bestPriority < 2 ? best : FindNearestAttackableStructure(best, bestDistance);
        }

        private Health FindNearestAttackableStructure(Health fallback, float fallbackDistance)
        {
            StructureEntity[] structures = FindObjectsByType<StructureEntity>();
            Health best = fallback;
            float bestDistance = fallback != null ? fallbackDistance : float.PositiveInfinity;
            int bestId = best != null ? best.GetEntityId().GetHashCode() : int.MaxValue;
            for (int i = 0; i < structures.Length; i++)
            {
                StructureEntity candidate = structures[i];
                if (candidate == null || (candidate.Kind != StructureKind.Tower && candidate.Kind != StructureKind.Core) || !candidate.IsAlive ||
                    !candidate.CanReceiveDamageFrom(teamMember) || !IsValidTarget(candidate.Health, requireDetectionRange: true))
                {
                    continue;
                }

                Vector3 offset = candidate.GetApproachPoint(transform.position) - transform.position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                int id = candidate.Health.GetEntityId().GetHashCode();
                if (distance < bestDistance || (Mathf.Approximately(distance, bestDistance) && id < bestId))
                {
                    best = candidate.Health;
                    bestDistance = distance;
                    bestId = id;
                }
            }

            return best;
        }

        private void FollowRoute()
        {
            if (route == null || route.IsComplete(waypointIndex))
            {
                FollowEnemyCore();
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
                    FollowEnemyCore();
                    return;
                }

                waypoint = route.GetWaypoint(waypointIndex);
            }

            mover.MoveTo(waypoint);
        }

        /// <summary>
        /// Lane paths intentionally stop just before each base so they remain safe
        /// around the base geometry. Once their final waypoint is reached, creeps
        /// still need a destination: they advance to the opposing core's nearest
        /// navigable edge. The core is only selected as an attack target once its
        /// progression prerequisite is met; before then this is movement only.
        /// </summary>
        private void FollowEnemyCore()
        {
            StructureEntity core = ResolveEnemyCore();
            if (core == null)
            {
                mover.Stop();
                return;
            }

            if (TryFindNavigableCoreApproach(core, out Vector3 destination))
            {
                mover.MoveTo(destination);
            }
            else
            {
                mover.Stop();
            }
        }

        /// <summary>
        /// The core mesh itself belongs to the Ground source layer. Sampling its
        /// closest collider point can therefore resolve to the disconnected top of
        /// the core, rather than the platform around it. Pick a reachable point just
        /// outside its footprint, biased toward the arriving creep, so the wave
        /// walks past the opposing spawn waypoint and reaches the actual nucleus.
        /// </summary>
        private bool TryFindNavigableCoreApproach(StructureEntity core, out Vector3 destination)
        {
            Vector3 center = core.transform.position;
            Vector3 direct = core.GetApproachPoint(transform.position);
            Vector3 outward = transform.position - center;
            outward.y = 0f;
            if (outward.sqrMagnitude < .01f)
            {
                outward = direct - center;
                outward.y = 0f;
            }
            if (outward.sqrMagnitude < .01f) outward = Vector3.back;
            outward.Normalize();

            float directRadius = new Vector2(direct.x - center.x, direct.z - center.z).magnitude;
            float radius = Mathf.Max(4.5f, directRadius + 1.75f);
            // Start on the lane-facing side. The remaining directions handle a core
            // whose front is temporarily blocked by a destroyed tower's collider or
            // a base decoration without ever selecting the core's top surface.
            float[] angles = { 0f, -25f, 25f, -50f, 50f, 90f, -90f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 requested = center + Quaternion.Euler(0f, angles[i], 0f) * outward * radius;
                // Core roots are centred halfway up their tall mesh. Sampling from
                // that height with a short radius misses the ground NavMesh, which
                // was the exact reason a wave stopped at a base entrance. Sample
                // from the arriving agent's navigation plane instead.
                requested.y = transform.position.y;
                if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                {
                    continue;
                }

                // Never use a sampled point on top of the tall core. A complete
                // path also avoids a base island that the arriving creep cannot
                // actually enter from the lane.
                if (hit.position.y > center.y + .5f ||
                    !NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, coreApproachPath) ||
                    coreApproachPath.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                destination = hit.position;
                return true;
            }

            destination = default;
            return false;
        }

        private StructureEntity ResolveEnemyCore()
        {
            if (IsEnemyCore(finalCore))
            {
                return finalCore;
            }

            StructureEntity routeObjective = route != null ? route.FinalObjective : null;
            if (IsEnemyCore(routeObjective))
            {
                finalCore = routeObjective;
                return finalCore;
            }

            // Compatibility fallback for old or hand-authored test routes. It is
            // cached after the first successful resolution, never uses names, and
            // lets an existing scene keep running until regenerated by the builder.
            StructureEntity[] structures = FindObjectsByType<StructureEntity>();
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < structures.Length; i++)
            {
                StructureEntity candidate = structures[i];
                if (!IsEnemyCore(candidate))
                {
                    continue;
                }

                Vector3 offset = candidate.transform.position - transform.position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    finalCore = candidate;
                }
            }

            return finalCore;
        }

        private bool IsEnemyCore(StructureEntity candidate)
        {
            return candidate != null && candidate.Kind == StructureKind.Core && candidate.IsAlive &&
                   teamMember != null && teamMember.IsEnemy(candidate.GetComponent<TeamMember>());
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
            SetDeathPresentation();
        }

        private void SetDeathPresentation()
        {
            RefreshPresentation();
            for(int i=0;i<presentationRenderers.Length;i++)if(presentationRenderers[i]!=null)presentationRenderers[i].enabled=false;
            for(int i=0;i<presentationColliders.Length;i++)if(presentationColliders[i]!=null)presentationColliders[i].enabled=false;
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

    /// <summary>Inspectable, allocation-free-at-source snapshot of a creep's
    /// strategic and navigation state. Intended for the inspector/debug overlay,
    /// never emitted automatically to the console.</summary>
    public readonly struct CreepNavigationDebugInfo
    {
        public readonly string Lane;
        public readonly TeamId Team;
        public readonly string CurrentState;
        public readonly int CurrentWaypointIndex;
        public readonly int TotalWaypoints;
        public readonly Vector3 CurrentDestination;
        public readonly Health CurrentTarget;
        public readonly string TargetType;
        public readonly float DistanceToTarget;
        public readonly bool HasPath;
        public readonly bool PathPending;
        public readonly NavMeshPathStatus PathStatus;
        public readonly float RemainingDistance;
        public readonly bool IsAtFinalWaypoint;
        public readonly bool IsCoreVulnerable;
        public readonly bool IsCoreTargetable;
        public readonly bool IsCoreReachable;

        public CreepNavigationDebugInfo(string lane, TeamId team, string currentState, int currentWaypointIndex,
            int totalWaypoints, Vector3 currentDestination, Health currentTarget, string targetType,
            float distanceToTarget, bool hasPath, bool pathPending, NavMeshPathStatus pathStatus,
            float remainingDistance, bool isAtFinalWaypoint, bool isCoreVulnerable, bool isCoreTargetable,
            bool isCoreReachable)
        {
            Lane = lane;
            Team = team;
            CurrentState = currentState;
            CurrentWaypointIndex = currentWaypointIndex;
            TotalWaypoints = totalWaypoints;
            CurrentDestination = currentDestination;
            CurrentTarget = currentTarget;
            TargetType = targetType;
            DistanceToTarget = distanceToTarget;
            HasPath = hasPath;
            PathPending = pathPending;
            PathStatus = pathStatus;
            RemainingDistance = remainingDistance;
            IsAtFinalWaypoint = isAtFinalWaypoint;
            IsCoreVulnerable = isCoreVulnerable;
            IsCoreTargetable = isCoreTargetable;
            IsCoreReachable = isCoreReachable;
        }
    }
}

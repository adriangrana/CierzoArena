using CierzoArena.Core;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Units
{
    public sealed class ClickMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float stoppingDistance = 0.15f;
        [SerializeField] private float turnSpeed = 12f;
        [SerializeField] private float acceleration = 24f;
        [SerializeField] private float agentRadius = 0.45f;
        [SerializeField] private float agentHeight = 2f;
        [SerializeField] private float baseOffset = 1f;
        [SerializeField] private LayerMask navigationSourceMask = 1 << 6;
        [SerializeField] private Vector3 runtimeNavMeshExtents = new Vector3(80f, 20f, 80f);
        [SerializeField] private float navMeshSearchRadius = 3f;

        private NavMeshAgent agent;
        private float levelMoveSpeedBonus;
        private float itemMoveSpeedBonus;
        private float abilityMoveSpeedBonus;
        private float statusMoveSpeedBonus;
        private float statusMoveSpeedMultiplier = 1f;
        private bool hasMoveCommand;
        private Vector3 lastRequestedDestination;

        public float EffectiveMoveSpeed => Mathf.Max(0f, (moveSpeed + levelMoveSpeedBonus + itemMoveSpeedBonus + abilityMoveSpeedBonus + statusMoveSpeedBonus) * statusMoveSpeedMultiplier);
        /// <summary>True when the NavMeshAgent is enabled and can accept destinations.</summary>
        public bool IsNavigationEnabled => agent != null && agent.enabled;
        public bool IsNavigationStopped => agent == null || !agent.enabled || agent.isStopped;
        public bool HasMoveCommand => hasMoveCommand;
        public Vector3 LastRequestedDestination => lastRequestedDestination;

        private void Awake()
        {
            UnitDefinition definition = ResolveDefinition();
            if (definition != null)
            {
                moveSpeed = definition.MovementSpeed;
            }

            RuntimeNavMesh.EnsureBuilt(navigationSourceMask, new Bounds(transform.position, runtimeNavMeshExtents));

            if (!TryGetComponent(out agent))
            {
                agent = gameObject.AddComponent<NavMeshAgent>();
            }

            ConfigureAgent();
            SnapAgentToNavMesh();
        }

        private void OnValidate()
        {
            if (agent == null && !TryGetComponent(out agent))
            {
                return;
            }

            ConfigureAgent();
        }

        public bool MoveTo(Vector3 worldPosition)
        {
            if (TryGetComponent(out StatusEffectController effects) && !effects.CanMove) return false;
            if (!EnsureNavigationReady())
            {
                return false;
            }

            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                hasMoveCommand = agent.SetDestination(hit.position);
                if (hasMoveCommand)
                {
                    lastRequestedDestination = hit.position;
                }
                return hasMoveCommand;
            }

            hasMoveCommand = false;
            return false;
        }

        public void Stop()
        {
            if (agent == null || !agent.isOnNavMesh)
            {
                return;
            }

            // ResetPath() (like SetDestination) resets isStopped back to false, so
            // clear the path first and raise the stop flag afterwards. Otherwise the
            // agent would report isStopped == false right after being told to stop.
            agent.ResetPath();
            agent.isStopped = true;
            hasMoveCommand = false;
        }

        /// <summary>
        /// Clears a completed return path and makes the agent ready for a fresh combat
        /// destination. This is deliberately separate from <see cref="Stop"/>: reset
        /// completion must not leave the agent permanently stopped.
        /// </summary>
        public bool RearmNavigation()
        {
            if (agent == null && !TryGetComponent(out agent))
            {
                agent = gameObject.AddComponent<NavMeshAgent>();
            }

            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            agent.updatePosition = true;
            agent.updateRotation = true;
            ConfigureAgent();
            SnapAgentToNavMesh();
            if (!agent.isOnNavMesh)
            {
                hasMoveCommand = false;
                return false;
            }

            agent.ResetPath();
            agent.isStopped = false;
            hasMoveCommand = false;
            return true;
        }

        /// <summary>
        /// Removes the current route without turning the agent into a stopped agent.
        /// Autonomous units use this while idle so their next combat destination can
        /// be accepted immediately.
        /// </summary>
        public bool ClearDestinationAndRemainReady()
        {
            if (!EnsureNavigationReady())
            {
                hasMoveCommand = false;
                return false;
            }

            agent.ResetPath();
            agent.isStopped = false;
            hasMoveCommand = false;
            return true;
        }

        /// <summary>Places the agent at a valid navigation position and clears any old route.</summary>
        public void WarpTo(Vector3 worldPosition)
        {
            if (agent == null)
            {
                transform.position = worldPosition;
                return;
            }

            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                transform.position = worldPosition;
            }

            Stop();
        }

        private bool EnsureNavigationReady()
        {
            if (agent == null || !agent.enabled)
            {
                return RearmNavigation();
            }

            agent.updatePosition = true;
            agent.updateRotation = true;
            ConfigureAgent();
            if (!agent.isOnNavMesh)
            {
                SnapAgentToNavMesh();
            }

            return agent.isOnNavMesh;
        }

        /// <summary>Sets the additive per-match movement speed earned from hero levels.</summary>
        public void SetLevelMoveSpeedBonus(float bonus)
        {
            levelMoveSpeedBonus = Mathf.Max(0f, bonus);
            if (agent != null)
            {
                ConfigureAgent();
            }
        }

        public void SetItemMoveSpeedBonus(float bonus)
        {
            itemMoveSpeedBonus = Mathf.Max(0f, bonus);
            if (agent != null) ConfigureAgent();
        }

        public void SetAbilityMoveSpeedBonus(float bonus)
        {
            abilityMoveSpeedBonus = Mathf.Max(0f, bonus);
            if (agent != null) ConfigureAgent();
        }
        public void SetStatusMoveSpeedBonus(float bonus) { statusMoveSpeedBonus = Mathf.Max(0f, bonus); if (agent != null) ConfigureAgent(); }
        public void SetStatusMoveSpeedMultiplier(float multiplier) { statusMoveSpeedMultiplier = Mathf.Clamp(multiplier, .2f, 1f); if (agent != null) ConfigureAgent(); }
        public void ConfigureHeroMoveSpeed(float value)
        {
            moveSpeed=Mathf.Max(0f,value);levelMoveSpeedBonus=0f;itemMoveSpeedBonus=0f;abilityMoveSpeedBonus=0f;statusMoveSpeedBonus=0f;
            if(agent!=null)ConfigureAgent();
        }

        private void ConfigureAgent()
        {
            agent.speed = EffectiveMoveSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.angularSpeed = turnSpeed * 60f;
            agent.acceleration = acceleration;
            agent.radius = agentRadius;
            agent.height = agentHeight;
            agent.baseOffset = baseOffset;
            agent.updateRotation = true;
        }

        private void SnapAgentToNavMesh()
        {
            if (agent.isOnNavMesh)
            {
                return;
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        private UnitDefinition ResolveDefinition()
        {
            return TryGetComponent(out UnitDefinitionProvider provider) ? provider.Definition : null;
        }
    }
}

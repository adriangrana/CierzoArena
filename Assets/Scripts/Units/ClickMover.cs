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

        public float EffectiveMoveSpeed => Mathf.Max(0f, moveSpeed + levelMoveSpeedBonus + itemMoveSpeedBonus);

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

        public void MoveTo(Vector3 worldPosition)
        {
            if (agent == null || !agent.isOnNavMesh)
            {
                return;
            }

            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
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

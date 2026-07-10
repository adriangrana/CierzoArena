using CierzoArena.Core;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Units
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ClickMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float stoppingDistance = 0.15f;
        [SerializeField] private float turnSpeed = 12f;
        [SerializeField] private LayerMask navigationSourceMask = 1 << 6;
        [SerializeField] private Vector3 runtimeNavMeshExtents = new Vector3(80f, 20f, 80f);
        [SerializeField] private float navMeshSearchRadius = 3f;

        private NavMeshAgent agent;

        private void Awake()
        {
            if (!TryGetComponent(out agent))
            {
                agent = gameObject.AddComponent<NavMeshAgent>();
            }

            ConfigureAgent();
            RuntimeNavMesh.EnsureBuilt(navigationSourceMask, new Bounds(transform.position, runtimeNavMeshExtents));
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
                agent.SetDestination(hit.position);
            }
        }

        private void ConfigureAgent()
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.angularSpeed = turnSpeed * 60f;
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
    }
}

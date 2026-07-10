using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Navigation
{
    /// <summary>
    /// Minimal navigation instrumentation for the M3B scale spike. It observes the
    /// unit's existing <see cref="NavMeshAgent"/> (no new navigation system) and
    /// reports path quality: approximate length, corner count, path status, and
    /// whether the destination ended up off the NavMesh. It also draws the current
    /// path in the Scene view.
    ///
    /// This is throwaway debug tooling: logging + Debug.DrawLine only, no UI.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavPathProbe : MonoBehaviour
    {
        [SerializeField] private bool drawPath = true;
        [SerializeField] private Color pathColor = new Color(0.2f, 0.9f, 1f);
        [SerializeField] private bool logOnDestinationChange = true;
        [SerializeField] private float destinationEpsilon = 0.25f;

        private NavMeshAgent agent;
        private Vector3 lastLoggedDestination;
        private bool hasLoggedOnce;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void Update()
        {
            if (agent == null || !agent.isOnNavMesh)
            {
                return;
            }

            if (drawPath && agent.hasPath)
            {
                DrawPath(agent.path);
            }

            if (logOnDestinationChange && DestinationChanged())
            {
                lastLoggedDestination = agent.destination;
                hasLoggedOnce = true;
                LogCurrentPath();
            }
        }

        private bool DestinationChanged()
        {
            if (!hasLoggedOnce)
            {
                return agent.hasPath || agent.pathPending;
            }

            return (agent.destination - lastLoggedDestination).sqrMagnitude > (destinationEpsilon * destinationEpsilon);
        }

        private void LogCurrentPath()
        {
            NavMeshPath path = agent.path;
            Vector3[] corners = path.corners;
            float length = PathLength(corners);

            Debug.Log($"[NavScale] dest={agent.destination} status={path.status} corners={corners.Length} length~{length:F1}m");

            if (path.status == NavMeshPathStatus.PathPartial)
            {
                Debug.LogWarning("[NavScale] PARTIAL path: destination not fully reachable (blocked region or off-mesh).");
            }
            else if (path.status == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning("[NavScale] INVALID path: no route computed.");
            }
        }

        private void DrawPath(NavMeshPath path)
        {
            Vector3[] corners = path.corners;
            Vector3 lift = Vector3.up * 0.3f;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Debug.DrawLine(corners[i] + lift, corners[i + 1] + lift, pathColor);
            }
        }

        /// <summary>
        /// Sums the segment lengths of a corner list. Pure and unit-testable without a
        /// running NavMesh (the caller passes the already-resolved corners).
        /// </summary>
        public static float PathLength(Vector3[] corners)
        {
            if (corners == null || corners.Length < 2)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                total += Vector3.Distance(corners[i], corners[i + 1]);
            }

            return total;
        }
    }
}

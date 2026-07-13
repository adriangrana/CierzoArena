using System.Collections.Generic;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Explicit scene-authored lane route. Waypoints are never searched globally.</summary>
    public sealed class LaneRoute : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private StructureEntity finalObjective;
        [SerializeField] private Color gizmoColor = Color.yellow;

        public int Count => waypoints != null ? waypoints.Length : 0;
        /// <summary>
        /// Strategic destination after the authored lane waypoints complete. A lane
        /// is not an idle path: it explicitly terminates at the opposing core.
        /// </summary>
        public StructureEntity FinalObjective => finalObjective;
        public bool HasFinalObjective => finalObjective != null;

        public Vector3 GetWaypoint(int index)
        {
            return index >= 0 && index < Count && waypoints[index] != null
                ? waypoints[index].position
                : transform.position;
        }

        public bool IsComplete(int index) => index >= Count;

        /// <summary>Editor/runtime setup hook used by deterministic scene builders
        /// and tests. The route remains scene-authored; no global name lookup is
        /// required to discover its strategic destination.</summary>
        public void Configure(Transform[] orderedWaypoints, StructureEntity objective)
        {
            waypoints = orderedWaypoints;
            finalObjective = objective;
        }

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            Gizmos.color = gizmoColor;
            Vector3 previous = transform.position;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                {
                    continue;
                }

                Vector3 point = waypoints[i].position;
                Gizmos.DrawSphere(point, 0.35f);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }

            if (finalObjective != null)
            {
                Gizmos.color = finalObjective.Team == TeamId.Azure ? Color.cyan : Color.red;
                Gizmos.DrawWireSphere(finalObjective.transform.position, 2.25f);
                Gizmos.DrawLine(previous, finalObjective.transform.position);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Explicit scene-authored lane route. Waypoints are never searched globally.</summary>
    public sealed class LaneRoute : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private Color gizmoColor = Color.yellow;

        public int Count => waypoints != null ? waypoints.Length : 0;

        public Vector3 GetWaypoint(int index)
        {
            return index >= 0 && index < Count && waypoints[index] != null
                ? waypoints[index].position
                : transform.position;
        }

        public bool IsComplete(int index) => index >= Count;

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
        }
    }
}

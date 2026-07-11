using System.Collections.Generic;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Explicit team-owned hero spawn point, registered once per scene.</summary>
    public sealed class HeroSpawnPoint : MonoBehaviour
    {
        private static readonly Dictionary<TeamId, HeroSpawnPoint> activePoints = new();
        [SerializeField] private TeamId team = TeamId.Azure;

        public TeamId Team => team;
        public static HeroSpawnPoint FindFor(TeamId requestedTeam) => activePoints.TryGetValue(requestedTeam, out HeroSpawnPoint point) ? point : null;

        public void SetTeam(TeamId value)
        {
            if (isActiveAndEnabled && activePoints.TryGetValue(team, out HeroSpawnPoint current) && current == this)
            {
                activePoints.Remove(team);
            }

            team = value;
            if (isActiveAndEnabled)
            {
                activePoints[team] = this;
            }
        }

        private void OnEnable()
        {
            // Builders add components before assigning serialized values. Do not let
            // a newly-created Ember point (temporarily Azure by default) replace the
            // real Azure point during that short construction window.
            if (!activePoints.ContainsKey(team))
            {
                activePoints[team] = this;
            }
        }

        private void OnDisable()
        {
            if (activePoints.TryGetValue(team, out HeroSpawnPoint point) && point == this)
            {
                activePoints.Remove(team);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = team == TeamId.Azure ? new Color(0.2f, 0.65f, 1f) : new Color(1f, 0.25f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, 1.2f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }
    }
}

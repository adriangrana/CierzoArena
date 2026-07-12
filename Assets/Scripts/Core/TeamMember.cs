using UnityEngine;

namespace CierzoArena.Core
{
    public sealed class TeamMember : MonoBehaviour
    {
        [SerializeField] private TeamId team = TeamId.Neutral;

        public TeamId Team => team;
        public void ConfigureTeam(TeamId value) => team = value;

        public bool IsEnemy(TeamMember other)
        {
            if (other == null || team == other.team)
            {
                return false;
            }

            // Neutral is a real hostile faction: it fights Azure and Ember, but
            // members of the same neutral camp never fight each other.
            return true;
        }
    }
}

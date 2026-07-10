using UnityEngine;

namespace CierzoArena.Core
{
    public sealed class TeamMember : MonoBehaviour
    {
        [SerializeField] private TeamId team = TeamId.Neutral;

        public TeamId Team => team;

        public bool IsEnemy(TeamMember other)
        {
            return other != null && team != TeamId.Neutral && other.team != TeamId.Neutral && other.team != team;
        }
    }
}

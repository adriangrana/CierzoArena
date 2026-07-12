using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Scene-authored stable match slot. A respawn keeps the same component and id.</summary>
    [RequireComponent(typeof(HeroUnit))]
    [RequireComponent(typeof(TeamMember))]
    public sealed class HeroMatchIdentity : MonoBehaviour
    {
        [SerializeField, Min(0)] private int matchSlot;
        [SerializeField] private string displayName;

        public int MatchSlot => Mathf.Max(0, matchSlot);
        public TeamId Team => TryGetComponent(out TeamMember member) ? member.Team : TeamId.Neutral;
        // Team partition keeps the independently authored Azure and Ember slot zero distinct.
        public int HeroId => ((int)Team * 1000) + MatchSlot;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"{Team} {MatchSlot + 1}" : displayName;

        public void Configure(int slot, string visibleName = null)
        {
            matchSlot = Mathf.Max(0, slot);
            if (!string.IsNullOrWhiteSpace(visibleName)) displayName = visibleName;
        }
    }
}

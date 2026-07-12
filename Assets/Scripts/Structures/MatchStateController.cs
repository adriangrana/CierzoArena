using System;
using CierzoArena.Core;
using CierzoArena.Units;
using UnityEngine;

namespace CierzoArena.Structures
{
    /// <summary>
    /// Single gameplay gate for a match. The first authoritative core-destruction
    /// event wins; later events are intentionally ignored, including a same-frame
    /// second core destruction.
    /// </summary>
    public sealed class MatchStateController : MonoBehaviour
    {
        private static MatchStateController active;

        [SerializeField] private bool localAuthorityEnabled = true;
        [SerializeField] private MatchState currentState = MatchState.Playing;

        public static MatchStateController Active => active;
        public MatchState CurrentState => currentState;
        public bool IsPlaying => currentState == MatchState.Playing;
        public bool CanAcceptGameplay => IsPlaying;

        public event Action<MatchState> StateChanged;

        private void Awake()
        {
            if (active == null)
            {
                active = this;
            }
            else if (active != this)
            {
                Debug.LogWarning("Only one MatchStateController should be active in a scene.", this);
            }

            // Existing prototype scenes remain usable without a forced YAML rewrite.
            // Builders author these explicitly; this is only a backwards-compatible
            // runtime bootstrap for scenes created before M17.
            if (Application.isPlaying)
            {
                if (!TryGetComponent(out MatchStatisticsController _)) gameObject.AddComponent<MatchStatisticsController>();
                if (!TryGetComponent(out MatchScoreboardController _)) gameObject.AddComponent<MatchScoreboardController>();
            }
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
        }

        private void OnEnable()
        {
            if (active == null) active = this;
        }

        private void OnDisable()
        {
            if (active == this) active = null;
        }

        /// <summary>Called by the Netcode bridge on spawn; Runtime stays transport-agnostic.</summary>
        public void SetLocalAuthority(bool enabled)
        {
            localAuthorityEnabled = enabled;
        }

        public bool ResolveCoreDestroyed(StructureEntity core)
        {
            if (!localAuthorityEnabled || !IsPlaying || core == null || core.Kind != StructureKind.Core)
            {
                return false;
            }

            MatchState victory = core.Team == TeamId.Ember
                ? MatchState.AzureVictory
                : core.Team == TeamId.Azure ? MatchState.EmberVictory : MatchState.Playing;

            return victory != MatchState.Playing && SetAuthoritativeState(victory);
        }

        /// <summary>Applies an already-authoritative state; safe for replicated state.</summary>
        public bool ApplyAuthoritativeState(MatchState state)
        {
            if (currentState == state)
            {
                return false;
            }

            // A match result is immutable. Replication cannot reopen or replace it.
            if (!IsPlaying)
            {
                return false;
            }

            currentState = state;
            StateChanged?.Invoke(currentState);
            return true;
        }

        private bool SetAuthoritativeState(MatchState state)
        {
            return ApplyAuthoritativeState(state);
        }
    }
}

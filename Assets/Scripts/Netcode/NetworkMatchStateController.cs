using CierzoArena.Structures;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>Replicates the server-decided match result without client RPCs.</summary>
    [RequireComponent(typeof(MatchStateController))]
    public sealed class NetworkMatchStateController : NetworkBehaviour
    {
        private readonly NetworkVariable<MatchState> replicatedState =
            new(MatchState.Playing, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private MatchStateController match;

        private void Awake()
        {
            match = GetComponent<MatchStateController>();
        }

        public override void OnNetworkSpawn()
        {
            match.SetLocalAuthority(IsServer);
            if (IsServer)
            {
                replicatedState.Value = match.CurrentState;
                match.StateChanged += OnServerStateChanged;
            }
            else
            {
                match.ApplyAuthoritativeState(replicatedState.Value);
                replicatedState.OnValueChanged += OnReplicatedStateChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                match.StateChanged -= OnServerStateChanged;
            }
            else
            {
                replicatedState.OnValueChanged -= OnReplicatedStateChanged;
            }
        }

        private void OnServerStateChanged(MatchState state)
        {
            replicatedState.Value = state;
        }

        private void OnReplicatedStateChanged(MatchState _, MatchState current)
        {
            match.ApplyAuthoritativeState(current);
        }
    }
}

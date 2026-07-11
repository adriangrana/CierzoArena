using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>Server-authoritative replication bridge for the hero life-cycle.</summary>
    [RequireComponent(typeof(HeroLifeCycle))]
    public sealed class NetworkHeroLifeCycle : NetworkBehaviour
    {
        private readonly NetworkVariable<byte> replicatedState = new(
            (byte)HeroLifeState.Alive,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> respawnEndsAt = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private HeroLifeCycle life;

        private void Awake()
        {
            life = GetComponent<HeroLifeCycle>();
            life.SetSimulationEnabled(false);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                life.SetSimulationEnabled(true);
                life.StateChanged += OnServerLifeStateChanged;
                PublishServerState(life.State);
            }
            else
            {
                replicatedState.OnValueChanged += OnReplicatedStateChanged;
                ApplyReplicatedState();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                life.StateChanged -= OnServerLifeStateChanged;
            }
            else
            {
                replicatedState.OnValueChanged -= OnReplicatedStateChanged;
            }
        }

        private void Update()
        {
            if (IsSpawned && !IsServer)
            {
                ApplyReplicatedState();
            }
        }

        private void OnServerLifeStateChanged(HeroLifeCycle _, HeroLifeState next)
        {
            PublishServerState(next);
        }

        private void PublishServerState(HeroLifeState next)
        {
            replicatedState.Value = (byte)next;
            if (next == HeroLifeState.Dead)
            {
                respawnEndsAt.Value = NetworkManager.ServerTime.Time + life.RespawnRemaining;
            }
            else if (next == HeroLifeState.Alive)
            {
                respawnEndsAt.Value = 0d;
            }
        }

        private void OnReplicatedStateChanged(byte _, byte __)
        {
            ApplyReplicatedState();
        }

        private void ApplyReplicatedState()
        {
            HeroLifeState state = (HeroLifeState)replicatedState.Value;
            float remaining = state == HeroLifeState.Alive
                ? 0f
                : Mathf.Max(0f, (float)(respawnEndsAt.Value - NetworkManager.ServerTime.Time));
            life.ApplyReplicatedState(state, remaining);
        }
    }
}

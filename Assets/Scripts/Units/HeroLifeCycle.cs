using System;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Authoritative hero death/respawn state machine built on the shared Health component.</summary>
    [RequireComponent(typeof(HeroUnit))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(TeamMember))]
    [RequireComponent(typeof(BasicAttack))]
    [RequireComponent(typeof(ClickMover))]
    public sealed class HeroLifeCycle : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float respawnDelay = 8f;
        [SerializeField] private bool simulationEnabled = true;

        private Health health;
        private TeamMember teamMember;
        private BasicAttack attack;
        private ClickMover mover;
        private UnitOrderController orders;
        private DeathVisibility deathVisibility;
        private Renderer[] fallbackRenderers;
        private Collider[] fallbackColliders;
        private HeroLifeState state = HeroLifeState.Alive;
        private float remaining;

        public HeroLifeState State => state;
        public float RespawnRemaining => Mathf.Max(0f, remaining);
        public bool IsAliveForGameplay => state == HeroLifeState.Alive && health != null && health.IsAlive;
        public event Action<HeroLifeCycle, HeroLifeState> StateChanged;

        private void Awake()
        {
            health = GetComponent<Health>();
            teamMember = GetComponent<TeamMember>();
            attack = GetComponent<BasicAttack>();
            mover = GetComponent<ClickMover>();
            TryGetComponent(out orders);
            TryGetComponent(out deathVisibility);
            fallbackRenderers = GetComponentsInChildren<Renderer>(true);
            fallbackColliders = GetComponentsInChildren<Collider>(true);
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void OnValidate()
        {
            respawnDelay = Mathf.Max(0.1f, respawnDelay);
        }

        private void Update()
        {
            Simulate(Time.deltaTime);
        }

        public void SetSimulationEnabled(bool enabled)
        {
            simulationEnabled = enabled;
        }

        public bool Simulate(float deltaTime)
        {
            if (!simulationEnabled || state == HeroLifeState.Alive ||
                (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying))
            {
                return false;
            }

            if (state == HeroLifeState.Dead)
            {
                SetState(HeroLifeState.Respawning);
            }

            remaining = Mathf.Max(0f, remaining - Mathf.Max(0f, deltaTime));
            if (remaining > 0f)
            {
                return false;
            }

            Respawn();
            return true;
        }

        /// <summary>Applies server-replicated state on non-authoritative observers.</summary>
        public void ApplyReplicatedState(HeroLifeState replicatedState, float replicatedRemaining)
        {
            simulationEnabled = false;
            // Health and life-state are replicated through separate variables. Do not
            // briefly resurrect presentation if an Alive state arrives before the
            // matching restored-health state.
            if (replicatedState == HeroLifeState.Alive && (health == null || !health.IsAlive))
            {
                return;
            }

            remaining = Mathf.Max(0f, replicatedRemaining);
            if (state != replicatedState)
            {
                SetState(replicatedState);
            }
            ApplyPresentation(replicatedState == HeroLifeState.Alive);
        }

        private void OnDied(Health _)
        {
            if (state != HeroLifeState.Alive)
            {
                return;
            }

            remaining = respawnDelay;
            attack.ClearTarget();
            mover.Stop();
            SetState(HeroLifeState.Dead);
            ApplyPresentation(false);
        }

        private void Respawn()
        {
            if (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying)
            {
                return;
            }

            HeroSpawnPoint spawn = HeroSpawnPoint.FindFor(teamMember.Team);
            if (spawn != null)
            {
                transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
                mover.WarpTo(spawn.transform.position);
            }

            attack.ClearTarget();
            mover.Stop();
            health.RestoreFull();
            if (TryGetComponent(out StatusEffectController effects))
            {
                effects.ClearAll();
            }
            if (TryGetComponent(out HeroMana mana))
            {
                mana.RestoreFull();
            }
            remaining = 0f;
            ApplyPresentation(true);
            SetState(HeroLifeState.Alive);
        }

        private void SetState(HeroLifeState next)
        {
            if (state == next)
            {
                return;
            }

            state = next;
            StateChanged?.Invoke(this, state);
        }

        private void ApplyPresentation(bool alive)
        {
            if (deathVisibility != null)
            {
                deathVisibility.SetVisible(alive);
            }
            else
            {
                for (int i = 0; i < fallbackRenderers.Length; i++)
                {
                    if (fallbackRenderers[i] != null) fallbackRenderers[i].enabled = alive;
                }
                for (int i = 0; i < fallbackColliders.Length; i++)
                {
                    if (fallbackColliders[i] != null) fallbackColliders[i].enabled = alive;
                }
            }

            // DeathVisibility owns the world renderers, whereas SelectableUnit owns
            // whether its ring should be visible. Reapply the selection state after
            // revival so an unselected unit does not gain a ring and the local hero
            // preserves its default selection.
            if (TryGetComponent(out SelectableUnit selectable))
            {
                selectable.SetSelected(alive && selectable.IsSelected);
            }
        }
    }
}

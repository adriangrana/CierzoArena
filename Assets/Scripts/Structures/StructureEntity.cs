using System;
using CierzoArena.Combat;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Structures
{
    /// <summary>
    /// Common, non-moving structure domain component. It reuses Health and
    /// TeamMember, owns the one-shot destroyed state, and never destroys its object;
    /// network references therefore remain valid after destruction.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(TeamMember))]
    public sealed class StructureEntity : MonoBehaviour
    {
        [SerializeField] private StructureKind kind = StructureKind.Tower;
        [SerializeField] private StructureLane lane = StructureLane.None;
        [SerializeField] private StructureTier tier = StructureTier.Outer;
        [SerializeField] private Renderer[] renderersToDisable;
        [SerializeField] private Collider[] collidersToDisable;

        private Health health;
        private TeamMember teamMember;
        private Collider[] approachColliders;
        private bool destroyed;
        private bool initialized;

        public StructureKind Kind => kind;
        public StructureLane Lane => lane;
        public StructureTier Tier => tier;
        public TeamId Team
        {
            get
            {
                EnsureInitialized();
                return teamMember != null ? teamMember.Team : TeamId.Neutral;
            }
        }

        public Health Health
        {
            get
            {
                EnsureInitialized();
                return health;
            }
        }
        public bool IsDestroyed => destroyed;
        public bool IsAlive
        {
            get
            {
                EnsureInitialized();
                return health != null && health.IsAlive && !destroyed;
            }
        }

        public event Action<StructureEntity> Destroyed;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            health = GetComponent<Health>();
            teamMember = GetComponent<TeamMember>();
            approachColliders = GetComponentsInChildren<Collider>();
            if (health != null)
            {
                health.EnsureInitialized();
                health.Died += OnDied;
            }

            initialized = true;
        }

        private void Start()
        {
            // Start handles scene object initialization order; Network-spawned
            // structures also register here after the match controller is spawned.
            StructureProgressionController.Active?.Register(this);
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }

            StructureProgressionController.Active?.Unregister(this);
        }

        /// <summary>Damage entry for systems that know the attacking team.</summary>
        public bool TryApplyDamage(TeamMember attacker, float amount)
        {
            EnsureInitialized();
            if (!CanReceiveDamageFrom(attacker) || amount <= 0f)
            {
                return false;
            }

            health.ApplyDamage(amount);
            return true;
        }

        public bool CanReceiveDamageFrom(TeamMember attacker)
        {
            EnsureInitialized();
            return IsAlive && attacker != null && teamMember != null && attacker.IsEnemy(teamMember)
                && (StructureProgressionController.Active == null || StructureProgressionController.Active.IsAttackable(this))
                && (MatchStateController.Active == null || MatchStateController.Active.CanAcceptGameplay);
        }

        /// <summary>
        /// Closest navigable-facing point of this static structure. Target colliders
        /// live on the Selectable layer and are intentionally ignored; towers use
        /// their Ground-layer navigation blocker and cores use their own body.
        /// </summary>
        public Vector3 GetApproachPoint(Vector3 fromPosition)
        {
            EnsureInitialized();
            for (int i = 0; i < approachColliders.Length; i++)
            {
                Collider candidate = approachColliders[i];
                if (candidate != null && candidate.gameObject.layer != 7)
                {
                    return candidate.ClosestPoint(fromPosition);
                }
            }

            return transform.position;
        }

        private void OnDied(Health _)
        {
            if (destroyed)
            {
                return;
            }

            destroyed = true;
            DisablePresentationAndCollision();
            Destroyed?.Invoke(this);

            if (kind == StructureKind.Core)
            {
                MatchStateController.Active?.ResolveCoreDestroyed(this);
            }
        }

        private void DisablePresentationAndCollision()
        {
            if (renderersToDisable != null)
            {
                foreach (Renderer target in renderersToDisable)
                {
                    if (target != null)
                    {
                        target.enabled = false;
                    }
                }
            }

            if (collidersToDisable != null)
            {
                foreach (Collider target in collidersToDisable)
                {
                    if (target != null)
                    {
                        target.enabled = false;
                    }
                }
            }
        }
    }
}

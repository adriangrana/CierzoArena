using System;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using UnityEngine;
using UnityEngine.AI;

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
        private GameObject towerMageVisual;
        private static GameObject towerMagePrefab;
        private static bool towerMagePrefabAttempted;

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

        /// <summary>Server-side setup for a dynamically spawned copy of an authored structure.</summary>
        public void Configure(TeamId owner, StructureKind nextKind, StructureLane nextLane, StructureTier nextTier, float maximumHealth)
        {
            kind=nextKind;lane=nextLane;tier=nextTier;
            EnsureInitialized();
            teamMember?.ConfigureTeam(owner);
            health?.ConfigureMaximumHealth(maximumHealth);
        }

        private void Awake()
        {
            EnsureInitialized();
            CreateTowerMagePresentation();
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            health = GetComponent<Health>();
            teamMember = GetComponent<TeamMember>();
            NormalizeTowerFootprint();
            approachColliders = GetComponentsInChildren<Collider>();
            if (health != null)
            {
                health.EnsureInitialized();
                health.Died += OnDied;
            }

            initialized = true;
        }

        /// <summary>Keeps the navigation and click footprint aligned with the
        /// visible tower cap. Earlier greybox towers carried a broad invisible
        /// selectable/nav annulus, causing clicks near their visual edge to turn
        /// into invalid attack orders and agents to route too far away.</summary>
        private void NormalizeTowerFootprint()
        {
            if (kind != StructureKind.Tower) return;
            Transform blocker = transform.Find("Navigation Blocker");
            if (blocker != null)
            {
                if (blocker.TryGetComponent(out CapsuleCollider collider)) collider.radius = .55f;
                if (blocker.TryGetComponent(out NavMeshObstacle obstacle)) obstacle.radius = .55f;
            }

            Transform target = transform.Find("Structure Target Collider");
            if (target != null && target.TryGetComponent(out SphereCollider targetCollider)) targetCollider.radius = .8f;
        }

        /// <summary>Visual-only replacement for the greybox tower. Kept inside the
        /// already-authored structure component so it is available in every scene
        /// assembly refresh; health, collision, navigation and authority remain on
        /// this root object.</summary>
        private void CreateTowerMagePresentation()
        {
            if (kind != StructureKind.Tower || towerMageVisual != null) return;
            towerMagePrefab ??= LoadTowerMagePrefab();
            if (towerMagePrefab == null) return;

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.GetComponentInParent<WorldHealthBar>() != null) continue;
                renderer.enabled = false;
                Destroy(renderer);
            }

            towerMageVisual = Instantiate(towerMagePrefab, transform);
            towerMageVisual.name = "Mage Tower Visual";
            towerMageVisual.transform.localPosition = Vector3.zero;
            towerMageVisual.transform.localRotation = Quaternion.identity;
            towerMageVisual.transform.localScale = Vector3.one;
            FitTowerVisual(towerMageVisual);

            Renderer primary = towerMageVisual.GetComponentInChildren<Renderer>(true);
            if (TryGetComponent(out AttackVisual attackVisual)) attackVisual.SetTargetRenderer(primary);
            if (TryGetComponent(out VisionVisibility visibility)) visibility.RefreshPresentation();
        }

        private static GameObject LoadTowerMagePrefab()
        {
            if (towerMagePrefabAttempted) return null;
            towerMagePrefabAttempted = true;
            return Resources.Load<GameObject>("Art/Structures/MageTower/Tower Mage");
        }

        private void FitTowerVisual(GameObject visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > .001f) visual.transform.localScale *= 8.6f / bounds.size.y;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            // Network tower prefabs retain the old greybox root scale (Y = 4).
            // Their capsule consequently extends below the ground plane, so using
            // Collider.bounds.min here buried the new mesh by several world units.
            // The authored tower root is its gameplay ground anchor in both local
            // and network modes; place the mesh base on that anchor instead.
            float baseY = transform.position.y;
            visual.transform.position += Vector3.up * (baseY - bounds.min.y);

            // The old marker placed its health bar at the top of a short cylinder.
            // The replacement asset is substantially taller, so anchor the existing
            // world bar above the actual tower tip rather than at its obsolete local
            // height. This is presentation-only and keeps the bar owned by the
            // structure for fog and destruction handling.
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            WorldHealthBar bar = GetComponentInChildren<WorldHealthBar>(true);
            if (bar != null)
            {
                Vector3 position = bar.transform.position;
                position.x = bounds.center.x;
                position.y = bounds.max.y + .28f;
                position.z = bounds.center.z;
                bar.transform.position = position;
            }
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
        public bool TryApplyDamage(TeamMember attacker, float amount, AttackDelivery delivery = AttackDelivery.Melee)
        {
            EnsureInitialized();
            if (!CanReceiveDamageFrom(attacker) || amount <= 0f)
            {
                return false;
            }

            return health.ApplyDamage(new DamageContext(attacker, amount, delivery));
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
            if (towerMageVisual != null) towerMageVisual.SetActive(false);
            Destroyed?.Invoke(this);

            if (kind == StructureKind.Core)
            {
                MatchStateController.Active?.ResolveCoreDestroyed(this);
            }
        }

        /// <summary>
        /// Called when an enemy regains vision after a structure was destroyed in
        /// fog. Until then, the local client deliberately keeps its last-known model.
        /// </summary>
        public void ApplyDestroyedPresentationAfterVision()
        {
            if (destroyed)
            {
                DisablePresentationAndCollision(true);
            }
        }

        private void DisablePresentationAndCollision(bool force = false)
        {
            // Fog may defer only the renderer. Collision is authoritative gameplay
            // state and must disappear immediately on every peer/server.
            bool preserveKnownRenderer = !force && TryGetComponent(out VisionVisibility visibility) && visibility.PreserveKnownStructurePresentation;

            if (!preserveKnownRenderer && renderersToDisable != null)
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

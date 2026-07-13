using CierzoArena.CameraSystem;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>
    /// Applies local fog-of-war presentation. Mobile enemies are hidden completely;
    /// static enemy structures retain a dark, last-known representation instead.
    /// Gameplay state is never changed here.
    /// </summary>
    [RequireComponent(typeof(TeamMember))]
    public sealed class VisionVisibility : MonoBehaviour
    {
        private const float ObscuredBrightness = 0.28f;

        private TeamMember team;
        private Health health;
        private StructureEntity structure;
        private Renderer[] renderers;
        private Collider[] colliders;
        private WorldHealthBar[] healthBars;
        private MaterialPropertyBlock propertyBlock;
        private Color[] sourceColors;
        private bool knownStructureAlive;
        private bool hasObservedState;
        private bool lastObservedVisible;

        public bool IsStructure { get { EnsureInitialized(); return structure != null; } }
        public bool IsObscured { get; private set; }
        public bool KnownStructureAlive { get { EnsureInitialized(); return structure == null || knownStructureAlive; } }
        public bool PreserveKnownStructurePresentation { get { EnsureInitialized(); return structure != null && hasObservedState && !lastObservedVisible; } }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (renderers != null)
            {
                return;
            }

            team = GetComponent<TeamMember>();
            TryGetComponent(out health);
            structure = GetComponent<StructureEntity>();
            RefreshPresentation();

            knownStructureAlive = structure == null || !structure.IsDestroyed;
        }

        /// <summary>Refreshes visual caches after an authored model is added to a
        /// runtime entity. Visibility rules stay unchanged.</summary>
        public void RefreshPresentation()
        {
            // This method can be called by another component during its Awake
            // (before VisionVisibility.Awake). Initialise domain references here as
            // well, otherwise a newly added tower model is mistaken for an unseen
            // mobile unit and gets disabled on the first visibility update.
            team ??= GetComponent<TeamMember>();
            if (health == null) TryGetComponent(out health);
            if (structure == null) TryGetComponent(out structure);
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            healthBars = GetComponentsInChildren<WorldHealthBar>(true);
            propertyBlock ??= new MaterialPropertyBlock();
            sourceColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                sourceColors[i] = renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color")
                    ? renderer.sharedMaterial.color
                    : Color.white;
            }
            if (structure != null && !hasObservedState) knownStructureAlive = !structure.IsDestroyed;
        }

        private void Update()
        {
            if (LocalHeroProvider.Active == null || LocalHeroProvider.Active.CurrentHero == null)
            {
                return;
            }

            TeamMember observer = LocalHeroProvider.Active.CurrentHero.GetComponent<TeamMember>();
            if (observer != null)
            {
                ApplyVisibilityForTeam(observer.Team);
            }
        }

        public bool IsVisibleTo(TeamId observerTeam)
        {
            EnsureInitialized();
            return team != null && (team.Team == observerTeam || VisionSource.IsVisible(observerTeam, transform.position));
        }

        public bool IsVisibleToLocalTeam()
        {
            if (LocalHeroProvider.Active == null || LocalHeroProvider.Active.CurrentHero == null)
            {
                return true;
            }

            TeamMember observer = LocalHeroProvider.Active.CurrentHero.GetComponent<TeamMember>();
            return observer == null || IsVisibleTo(observer.Team);
        }

        /// <summary>Public for deterministic Edit Mode tests and local presentation refreshes.</summary>
        public void ApplyVisibilityForTeam(TeamId observerTeam)
        {
            EnsureInitialized();
            bool visible = IsVisibleTo(observerTeam);
            hasObservedState = true;
            lastObservedVisible = visible;
            if (structure != null)
            {
                ApplyStructureVisibility(visible);
                return;
            }

            ApplyMobileVisibility(visible);
        }

        private void ApplyMobileVisibility(bool visible)
        {
            // Fog updates must never resurrect the presentation of a dead mobile
            // unit during its despawn/respawn grace period.
            visible &= health == null || health.IsAlive;
            IsObscured = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = visible;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = visible;
            }
        }

        private void ApplyStructureVisibility(bool visible)
        {
            IsObscured = !visible;
            // Builders create the bar after the root components. Runtime scenes are
            // complete before Awake, but this also keeps deterministic edit tests and
            // dynamically spawned prototypes correct.
            if (healthBars.Length == 0)
            {
                healthBars = GetComponentsInChildren<WorldHealthBar>(true);
            }

            if (visible)
            {
                knownStructureAlive = !structure.IsDestroyed;
                if (structure.IsDestroyed)
                {
                    structure.ApplyDestroyedPresentationAfterVision();
                    return;
                }
            }

            // A living (or last-known living) structure remains in the map as a dark silhouette.
            if (knownStructureAlive)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (structure.IsPresentationRendererSuppressed(renderers[i]))
                    {
                        if (renderers[i] != null) renderers[i].enabled = false;
                        continue;
                    }
                    SetRendererColor(i, true, visible ? 1f : ObscuredBrightness);
                }
            }

            // Dynamic information is never retained by fog memory.
            for (int i = 0; i < healthBars.Length; i++)
            {
                if (healthBars[i] != null) healthBars[i].gameObject.SetActive(visible && knownStructureAlive);
            }
        }

        private void SetRendererColor(int index, bool enabled, float brightness)
        {
            Renderer renderer = renderers[index];
            if (renderer == null) return;
            renderer.enabled = enabled;
            if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty("_Color")) return;
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", sourceColors[index] * brightness);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}

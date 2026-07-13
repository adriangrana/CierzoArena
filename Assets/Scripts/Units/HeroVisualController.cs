using CierzoArena.Combat;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>
    /// Owns the optional, client-local visual child of a hero gameplay root. It never
    /// adds networking, colliders, movement or gameplay components: the authoritative
    /// root keeps all of those responsibilities. Resolution runs only after a hero ID
    /// is configured (spawn or replicated ID change), never per frame.
    /// </summary>
    public sealed class HeroVisualController : MonoBehaviour
    {
        public const string VisualRootName = "VisualRoot";
        public const string ModelInstanceName = "HeroModelInstance";

        private GameObject activeInstance;
        private GameObject activePrefab;
        private Renderer placeholderRenderer;
        private HeroLifeCycle lifeCycle;
        private bool hidePlaceholderWhileVisualActive;
        private bool hideTeamIndicatorWhileVisualActive;
        private bool hasGroundAnchor;
        private float groundAnchorLocalY;

        public Transform VisualRoot => GetOrCreateVisualRoot();
        public GameObject ActiveVisualInstance => activeInstance;

        private void Awake()
        {
            placeholderRenderer = GetComponent<Renderer>();
            lifeCycle = GetComponent<HeroLifeCycle>();
        }

        private void OnEnable()
        {
            if (lifeCycle == null) lifeCycle = GetComponent<HeroLifeCycle>();
            if (lifeCycle != null) lifeCycle.StateChanged += OnLifeStateChanged;
        }

        private void OnDisable()
        {
            if (lifeCycle != null) lifeCycle.StateChanged -= OnLifeStateChanged;
        }

        private void LateUpdate()
        {
            // Other gameplay presentation systems can legitimately toggle renderers
            // (death/respawn, replicated life state). Keep the placeholder hidden
            // while a hero-specific visual is active.
            if (activeInstance != null && hidePlaceholderWhileVisualActive && placeholderRenderer != null && placeholderRenderer.enabled)
            {
                placeholderRenderer.enabled = false;
            }
        }

        /// <summary>Resolve from the production catalogue using the authoritative
        /// <see cref="HeroDefinition.HeroId"/> supplied by HeroSelect/spawn.</summary>
        public void Apply(HeroDefinition definition)
        {
            Apply(definition, HeroVisualCatalog.Shared);
        }

        /// <summary>Explicit-catalog overload kept for deterministic editor tests and
        /// tooling. It follows the same ID-only resolution as production.</summary>
        public void Apply(HeroDefinition definition, HeroVisualCatalog catalog)
        {
            HeroVisualDefinition visual = definition == null || catalog == null ? null : catalog.Resolve(definition.HeroId);
            if (visual == null || !visual.HasVisual)
            {
                ClearVisual();
                return;
            }

            Transform root = GetOrCreateVisualRoot();
            if (activeInstance == null || activePrefab != visual.VisualPrefab)
            {
                DestroyActiveInstance();
                activeInstance = Instantiate(visual.VisualPrefab, root);
                activeInstance.name = ModelInstanceName;
                activePrefab = visual.VisualPrefab;
            }

            Transform instance = activeInstance.transform;
            instance.SetParent(root, false);
            if (!hasGroundAnchor)
            {
                groundAnchorLocalY = ResolveGroundAnchorLocalY();
                hasGroundAnchor = true;
            }
            instance.localPosition = visual.LocalPosition + Vector3.up * groundAnchorLocalY;
            instance.localRotation = visual.LocalRotation;
            instance.localScale = visual.LocalScale;
            hidePlaceholderWhileVisualActive = visual.HidePlaceholder;
            hideTeamIndicatorWhileVisualActive = visual.HideTeamIndicator;
            SetPlaceholderVisible(!hidePlaceholderWhileVisualActive);
            SetTeamIndicatorVisible(!hideTeamIndicatorWhileVisualActive);
            SetSilhouetteVisible(false);
            SetModelVisible(IsAlive());
        }

        /// <summary>Removes only the instantiated optional visual and restores the
        /// reusable placeholder. Safe for scene changes, respawns and ID changes.</summary>
        public void ClearVisual()
        {
            DestroyActiveInstance();
            hidePlaceholderWhileVisualActive = false;
            hideTeamIndicatorWhileVisualActive = false;
            SetPlaceholderVisible(IsAlive());
            SetTeamIndicatorVisible(true);
            SetSilhouetteVisible(true);
        }

        private Transform GetOrCreateVisualRoot()
        {
            Transform root = transform.Find(VisualRootName);
            if (root != null) return root;
            GameObject rootObject = new GameObject(VisualRootName);
            root = rootObject.transform;
            root.SetParent(transform, false);
            return root;
        }

        private void DestroyActiveInstance()
        {
            if (activeInstance == null) return;
            if (Application.isPlaying) Destroy(activeInstance);
            else DestroyImmediate(activeInstance);
            activeInstance = null;
            activePrefab = null;
        }

        private void SetPlaceholderVisible(bool visible)
        {
            if (placeholderRenderer == null) placeholderRenderer = GetComponent<Renderer>();
            if (placeholderRenderer != null) placeholderRenderer.enabled = visible;
        }

        private void SetSilhouetteVisible(bool visible)
        {
            HeroSilhouettePresentation silhouette = GetComponent<HeroSilhouettePresentation>();
            silhouette?.SetPresentationVisible(visible);
        }

        private bool IsAlive()
        {
            return lifeCycle == null || lifeCycle.State == HeroLifeState.Alive;
        }

        private void OnLifeStateChanged(HeroLifeCycle _, HeroLifeState state)
        {
            bool alive = state == HeroLifeState.Alive;
            SetModelVisible(alive);
            if (activeInstance == null) SetPlaceholderVisible(alive);
            else SetPlaceholderVisible(alive && !hidePlaceholderWhileVisualActive);
        }

        private void SetModelVisible(bool visible)
        {
            if (activeInstance == null) return;
            foreach (Renderer renderer in activeInstance.GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
        }

        private void SetTeamIndicatorVisible(bool visible)
        {
            Transform indicator = transform.Find("Team Indicator");
            if (indicator == null) return;
            Renderer renderer = indicator.GetComponent<Renderer>();
            // Legacy scene heroes may still carry this old solid team disk. Never
            // restore it on a visual change or respawn; selection owns the only
            // ground indicator for a hero.
            if (renderer != null) renderer.enabled = false;
        }

        private float ResolveGroundAnchorLocalY()
        {
            Collider collider = GetComponent<Collider>();
            if (collider is CapsuleCollider capsule)
            {
                return capsule.center.y - capsule.height * 0.5f;
            }
            if (collider is BoxCollider box)
            {
                return box.center.y - box.size.y * 0.5f;
            }
            if (collider is SphereCollider sphere)
            {
                return sphere.center.y - sphere.radius;
            }

            // Fallback for authored heroes without a root collider component.
            if (placeholderRenderer == null) placeholderRenderer = GetComponent<Renderer>();
            if (placeholderRenderer != null)
            {
                Vector3 localMin = transform.InverseTransformPoint(placeholderRenderer.bounds.min);
                return localMin.y;
            }

            return 0f;
        }
    }
}

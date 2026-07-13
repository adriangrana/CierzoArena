using UnityEngine;
using CierzoArena.Units;
using CierzoArena.CameraSystem;
using CierzoArena.Core;

namespace CierzoArena.Combat
{
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Transform fill;
        [SerializeField] private float width = 1.5f;
        [SerializeField] private float height = 0.12f;
        [SerializeField] private float depth = 0.03f;

        private Camera targetCamera;
        private HeroMana mana;
        private Transform manaBackground;
        private Transform manaFill;
        private float lastHealth = float.NaN;
        private float lastMaximum = float.NaN;
        private float lastMana = float.NaN;
        private float lastMaximumMana = float.NaN;
        private Renderer fillRenderer;
        private TeamMember ownerTeam;
        private MaterialPropertyBlock fillPropertyBlock;

        private static Material manaBackgroundMaterial;
        private static Material manaFillMaterial;

        public Health BoundHealth => health;

        private void Awake()
        {
            BindToOwningHealth();
            BindToAuthoredFill();

            mana = GetComponentInParent<HeroMana>();
        }

        private void Start()
        {
            RefreshNow();
        }

        private void LateUpdate()
        {
            // Scene-authored and NGO-instantiated prefabs both own their bar below
            // the unit root.  Prefer that immediate owner over a serialized object
            // reference: a stale prefab reference can otherwise leave a perfectly
            // visible bar subscribed to another Health instance, while the HUD uses
            // the correct, damaged hero state.
            BindToOwningHealth();
            BindToAuthoredFill();

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                transform.rotation = targetCamera.transform.rotation;
            }

            // Health may be applied from a replicated NetworkVariable after this
            // component was enabled. Polling the already-authoritative local state
            // makes the world presentation robust to event subscription/order
            // changes without affecting combat or simulation.
            RefreshNow();
            EnsureManaBar();
            RefreshMana();
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Changed -= OnHealthChanged;
                health.Died -= OnDied;
            }
        }

        private void OnHealthChanged(Health _, float current, float max)
        {
            if (current > 0f && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            Refresh(current, max);
        }

        private void OnDied(Health _)
        {
            gameObject.SetActive(false);
        }

        private void Refresh(float current, float max)
        {
            if (fill == null)
            {
                return;
            }

            float normalized = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            float fillWidth = width * normalized;
            fill.localScale = new Vector3(fillWidth, height, depth);
            fill.localPosition = new Vector3((fillWidth - width) * 0.5f, 0f, -depth);
            ApplyTeamColor();
            lastHealth = current;
            lastMaximum = max;
        }

        /// <summary>Refreshes the visual from current domain state. Public so a
        /// presentation rebuild can force an immediate sync without causing damage.</summary>
        public void RefreshNow()
        {
            BindToOwningHealth();
            BindToAuthoredFill();
            if (health == null) return;

            // The authored green fill is the world bar the player sees. Refresh it
            // directly instead of relying on a cached value; a network health sync
            // and an inspector-authored reference can otherwise leave it visually
            // stale despite the HUD reading the correct Health component.
            Refresh(health.Current, health.Max);
        }

        private void BindToOwningHealth()
        {
            Health ownerHealth = GetComponentInParent<Health>();
            if (ownerHealth == health)
            {
                return;
            }

            if (health != null)
            {
                health.Changed -= OnHealthChanged;
                health.Died -= OnDied;
            }

            health = ownerHealth;
            lastHealth = float.NaN;
            lastMaximum = float.NaN;
            if (health != null)
            {
                health.Changed += OnHealthChanged;
                health.Died += OnDied;
            }
        }

        private void BindToAuthoredFill()
        {
            // Preserve the scene/prefab bar instead of replacing it. When a
            // presentation rebuild changes children, recover the original fill by
            // name so the visible bar always remains connected to this component.
            if (fill != null && fill.IsChildOf(transform))
            {
                return;
            }

            Transform candidate = transform.Find("Health Bar Fill");
            if (candidate != null)
            {
                fill = candidate;
                fillRenderer = candidate.GetComponent<Renderer>();
                lastHealth = float.NaN;
                lastMaximum = float.NaN;
            }
        }

        private void ApplyTeamColor()
        {
            if (fill == null)
            {
                return;
            }

            fillRenderer ??= fill.GetComponent<Renderer>();
            ownerTeam ??= GetComponentInParent<TeamMember>();
            if (fillRenderer == null || ownerTeam == null)
            {
                return;
            }

            TeamMember observer = LocalHeroProvider.Active != null && LocalHeroProvider.Active.CurrentHero != null
                ? LocalHeroProvider.Active.CurrentHero.GetComponent<TeamMember>()
                : null;
            bool enemy = observer != null && observer.IsEnemy(ownerTeam);
            // Enemy health must read immediately as hostile but without a glowing
            // arcade red. Allies retain the existing readable green treatment.
            Color color = enemy ? new Color(.62f, .16f, .14f, 1f) : new Color(.20f, .85f, .30f, 1f);
            fillPropertyBlock ??= new MaterialPropertyBlock();
            fillRenderer.GetPropertyBlock(fillPropertyBlock);
            fillPropertyBlock.SetColor("_Color", color);
            fillPropertyBlock.SetColor("_BaseColor", color);
            fillRenderer.SetPropertyBlock(fillPropertyBlock);
        }

        private void EnsureManaBar()
        {
            if (manaFill != null) return;
            if (mana == null) mana = GetComponentInParent<HeroMana>();
            if (mana == null) return;

            EnsureManaMaterials();
            float verticalGap = height * 1.85f;

            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Mana Bar Background";
            background.layer = gameObject.layer;
            background.transform.SetParent(transform, false);
            background.transform.localPosition = new Vector3(0f, -verticalGap, 0f);
            background.transform.localScale = new Vector3(width, height * .82f, depth);
            background.GetComponent<Renderer>().sharedMaterial = manaBackgroundMaterial;
            Destroy(background.GetComponent<Collider>());
            manaBackground = background.transform;

            GameObject fillObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fillObject.name = "Mana Bar Fill";
            fillObject.layer = gameObject.layer;
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.localPosition = new Vector3(0f, -verticalGap, -depth);
            fillObject.transform.localScale = new Vector3(width, height * .54f, depth);
            fillObject.GetComponent<Renderer>().sharedMaterial = manaFillMaterial;
            Destroy(fillObject.GetComponent<Collider>());
            manaFill = fillObject.transform;
        }

        private void RefreshMana()
        {
            if (mana == null || manaFill == null) return;
            float current = mana.CurrentMana;
            float maximum = mana.MaximumMana;
            if (Mathf.Approximately(current, lastMana) && Mathf.Approximately(maximum, lastMaximumMana)) return;

            float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            float fillWidth = width * normalized;
            float verticalGap = height * 1.85f;
            manaFill.localScale = new Vector3(fillWidth, height * .54f, depth);
            manaFill.localPosition = new Vector3((fillWidth - width) * .5f, -verticalGap, -depth);
            lastMana = current;
            lastMaximumMana = maximum;
        }

        private static void EnsureManaMaterials()
        {
            if (manaBackgroundMaterial != null && manaFillMaterial != null) return;
            Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            manaBackgroundMaterial = new Material(shader) { name = "Runtime Mana Bar Background", hideFlags = HideFlags.DontSave, color = new Color(.015f, .035f, .09f, .95f) };
            manaFillMaterial = new Material(shader) { name = "Runtime Mana Bar Fill", hideFlags = HideFlags.DontSave, color = new Color(.12f, .5f, 1f, 1f) };
        }

    }
}

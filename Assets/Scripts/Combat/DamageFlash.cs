using UnityEngine;

namespace CierzoArena.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class DamageFlash : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private float duration = 0.1f;

        private Health health;
        private MaterialPropertyBlock propertyBlock;
        private Color baseColor;
        private float previousHealth;
        private float flashEndsAt;

        private void Awake()
        {
            health = GetComponent<Health>();
            targetRenderer = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            baseColor = targetRenderer != null && targetRenderer.sharedMaterial != null
                ? targetRenderer.sharedMaterial.color
                : Color.white;
            health.Changed += OnHealthChanged;
        }

        private void Start()
        {
            previousHealth = health.Current;
        }

        private void Update()
        {
            if (flashEndsAt > 0f && Time.time >= flashEndsAt)
            {
                SetColor(baseColor);
                flashEndsAt = 0f;
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Changed -= OnHealthChanged;
            }
        }

        private void OnHealthChanged(Health _, float current, float __)
        {
            bool tookDamage = current < previousHealth;
            previousHealth = current;

            if (!tookDamage || targetRenderer == null)
            {
                return;
            }

            SetColor(flashColor);
            flashEndsAt = Time.time + Mathf.Max(0f, duration);
        }

        private void SetColor(Color color)
        {
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}

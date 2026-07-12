using UnityEngine;

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

        public Health BoundHealth => health;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponentInParent<Health>();
            }

            if (health != null)
            {
                health.Changed += OnHealthChanged;
                health.Died += OnDied;
            }
        }

        private void Start()
        {
            if (health != null)
            {
                Refresh(health.Current, health.Max);
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                transform.rotation = targetCamera.transform.rotation;
            }
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
        }
    }
}

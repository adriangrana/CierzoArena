using UnityEngine;

namespace CierzoArena.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 2.25f, 0f);
        [SerializeField] private Color textColor = new Color(1f, 0.84f, 0.2f, 1f);
        [SerializeField] private float lifetime = 0.75f;
        [SerializeField] private float riseDistance = 0.6f;

        private Health health;
        private float previousHealth;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Changed += OnHealthChanged;
        }

        private void Start()
        {
            previousHealth = health.Current;
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
            float damage = previousHealth - current;
            previousHealth = current;

            if (damage <= 0f)
            {
                return;
            }

            GameObject numberObject = new GameObject("Damage Number");
            numberObject.transform.position = transform.position + spawnOffset;

            TextMesh textMesh = numberObject.AddComponent<TextMesh>();
            textMesh.text = $"-{damage:0}";
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.08f;
            textMesh.fontSize = 48;
            textMesh.color = textColor;

            DamageNumber number = numberObject.AddComponent<DamageNumber>();
            number.Initialize(textMesh, lifetime, riseDistance);
        }
    }
}

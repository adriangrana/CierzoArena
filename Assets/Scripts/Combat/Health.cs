using System;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Combat
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 500f;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsAlive => Current > 0f;

        public event Action<Health> Died;
        public event Action<Health, float, float> Changed;

        private void Awake()
        {
            UnitDefinition definition = ResolveDefinition();
            if (definition != null)
            {
                maxHealth = definition.MaxHealth;
            }

            maxHealth = Mathf.Max(1f, maxHealth);
            Current = maxHealth;
        }

        private UnitDefinition ResolveDefinition()
        {
            return TryGetComponent(out UnitDefinitionProvider provider) ? provider.Definition : null;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
        }

        public void ApplyDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(this, Current, maxHealth);

            if (Current <= 0f)
            {
                Died?.Invoke(this);
            }
        }

        public void RestoreFull()
        {
            Current = maxHealth;
            Changed?.Invoke(this, Current, maxHealth);
        }
    }
}

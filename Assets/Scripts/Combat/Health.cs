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

        /// <summary>
        /// Applies an exact, externally-authoritative health value (for example a
        /// replicated value received over the network). Unlike <see cref="ApplyDamage"/>
        /// this is a state assignment, not a damage event: it clamps to the valid
        /// range, keeps the alive/dead state consistent, and raises <see cref="Died"/>
        /// at most once (only on the alive-&gt;dead transition). It never writes to the
        /// shared <c>UnitDefinition</c>. It is deliberately transport-agnostic: Health
        /// has no knowledge of networking.
        /// </summary>
        public void ApplyAuthoritativeState(float authoritativeCurrent)
        {
            bool wasAlive = Current > 0f;
            Current = Mathf.Clamp(authoritativeCurrent, 0f, maxHealth);
            Changed?.Invoke(this, Current, maxHealth);

            if (wasAlive && Current <= 0f)
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

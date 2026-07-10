using System;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Combat
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 500f;

        private float current;
        private bool initialized;

        public float Current
        {
            get
            {
                EnsureInitialized();
                return current;
            }
            private set => current = value;
        }

        public float Max
        {
            get
            {
                EnsureInitialized();
                return maxHealth;
            }
        }
        public bool IsAlive => Current > 0f;

        public event Action<Health> Died;
        public event Action<Health, float, float> Changed;

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Makes the component safe for deterministic Edit Mode construction, where
        /// Unity does not guarantee invoking Awake after AddComponent. Normal runtime
        /// execution still initializes through Awake exactly once.
        /// </summary>
        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            UnitDefinition definition = ResolveDefinition();
            if (definition != null)
            {
                maxHealth = definition.MaxHealth;
            }

            maxHealth = Mathf.Max(1f, maxHealth);
            current = maxHealth;
            initialized = true;
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
            EnsureInitialized();
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
            EnsureInitialized();
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
            EnsureInitialized();
            Current = maxHealth;
            Changed?.Invoke(this, Current, maxHealth);
        }
    }
}

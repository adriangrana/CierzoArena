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
        private float itemMaximumHealthBonus;

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
        public event Action<Health, DamageContext> DiedWithContext;
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

        /// <summary>Configures a dynamically spawned entity before its first damage.</summary>
        public void ConfigureMaximumHealth(float value)
        {
            maxHealth = Mathf.Max(1f, value);
            initialized = false;
            EnsureInitialized();
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
            ApplyDamage(new DamageContext(null, amount, AttackDelivery.Melee));
        }

        /// <summary>
        /// Applies confirmed gameplay damage and emits its source only after a real
        /// health reduction. Health remains unaware of targeting/aggro decisions.
        /// </summary>
        public bool ApplyDamage(DamageContext context)
        {
            EnsureInitialized();
            if (!IsAlive || context.Amount <= 0f)
            {
                return false;
            }
            if (TryGetComponent(out CierzoArena.Units.StatusEffectController effects))
            {
                context = new DamageContext(context.Attacker, effects.AbsorbDamage(context.Amount), context.Delivery);
                if (context.Amount <= 0f) return false;
            }

            float before = Current;
            Current = Mathf.Max(0f, Current - context.Amount);
            if (Mathf.Approximately(before, Current))
            {
                return false;
            }

            Changed?.Invoke(this, Current, maxHealth);
            CombatEvents.RaiseDamageApplied(this, new DamageContext(context.Attacker, before - Current, context.Delivery));

            if (Current <= 0f)
            {
                Died?.Invoke(this);
                DiedWithContext?.Invoke(this, context);
            }

            return true;
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

        /// <summary>Raises maximum health and preserves the gained amount as current health.</summary>
        public void AddMaximumHealth(float amount)
        {
            EnsureInitialized();
            if (Mathf.Approximately(amount, 0f))
            {
                return;
            }

            maxHealth = Mathf.Max(1f, maxHealth + amount);
            Current = amount > 0f
                ? Mathf.Min(maxHealth, Current + amount)
                : Mathf.Clamp(Current, IsAlive ? 1f : 0f, maxHealth);
            Changed?.Invoke(this, Current, maxHealth);
        }

        public void SetItemMaximumHealthBonus(float bonus)
        {
            EnsureInitialized();
            float next = Mathf.Max(0f, bonus);
            AddMaximumHealth(next - itemMaximumHealthBonus);
            itemMaximumHealthBonus = next;
        }
    }
}

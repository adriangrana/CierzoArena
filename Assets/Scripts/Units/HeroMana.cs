using System;
using CierzoArena.Combat;
using UnityEngine;

namespace CierzoArena.Units
{
    [RequireComponent(typeof(HeroUnit))]
    [RequireComponent(typeof(Health))]
    public sealed class HeroMana : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maximumMana = 250f;
        [SerializeField, Min(0f)] private float regenerationPerSecond = 6f;
        [SerializeField] private bool authorityEnabled = true;
        private Health health;
        private float currentMana;
        private bool initialized;

        public float MaximumMana { get { EnsureInitialized(); return maximumMana; } }
        public float CurrentMana { get { EnsureInitialized(); return currentMana; } }
        public float RegenerationPerSecond => Mathf.Max(0f, regenerationPerSecond);
        public event Action<HeroMana> Changed;

        private void Awake() => EnsureInitialized();
        private void Update() => Simulate(Time.deltaTime);
        public void SetAuthorityEnabled(bool enabled) => authorityEnabled = enabled;
        public void ConfigureHeroMana(float maximum, float regeneration)
        {
            maximumMana=Mathf.Max(1f,maximum);regenerationPerSecond=Mathf.Max(0f,regeneration);currentMana=maximumMana;initialized=true;Changed?.Invoke(this);
        }
        public void AddMaximumMana(float amount)
        {
            EnsureInitialized();if(Mathf.Approximately(amount,0f))return;
            maximumMana=Mathf.Max(1f,maximumMana+amount);currentMana=amount>0f?Mathf.Min(maximumMana,currentMana+amount):Mathf.Min(currentMana,maximumMana);Changed?.Invoke(this);
        }

        public void EnsureInitialized()
        {
            if (initialized) return;
            health = GetComponent<Health>();
            maximumMana = Mathf.Max(1f, maximumMana);
            regenerationPerSecond = Mathf.Max(0f, regenerationPerSecond);
            currentMana = maximumMana;
            initialized = true;
        }

        public bool Simulate(float deltaTime)
        {
            EnsureInitialized();
            if (!authorityEnabled || health == null || !health.IsAlive || deltaTime <= 0f || currentMana >= maximumMana)
            {
                return false;
            }

            float next = Mathf.Min(maximumMana, currentMana + regenerationPerSecond * deltaTime);
            if (Mathf.Approximately(next, currentMana)) return false;
            currentMana = next;
            Changed?.Invoke(this);
            return true;
        }

        public bool TrySpend(float amount)
        {
            EnsureInitialized();
            amount = Mathf.Max(0f, amount);
            if (!authorityEnabled || amount > currentMana) return false;
            currentMana -= amount;
            Changed?.Invoke(this);
            return true;
        }

        public void RestoreFull()
        {
            EnsureInitialized();
            if (!authorityEnabled) return;
            currentMana = maximumMana;
            Changed?.Invoke(this);
        }

        public void ApplyAuthoritativeState(float authoritativeCurrent, float authoritativeMaximum)
        {
            EnsureInitialized();
            authorityEnabled = false;
            maximumMana = Mathf.Max(1f, authoritativeMaximum);
            currentMana = Mathf.Clamp(authoritativeCurrent, 0f, maximumMana);
            Changed?.Invoke(this);
        }
    }
}

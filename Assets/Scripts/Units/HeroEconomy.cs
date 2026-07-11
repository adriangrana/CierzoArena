using System;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Authoritative per-match gold wallet; item spending is deliberately future work.</summary>
    [RequireComponent(typeof(HeroUnit))]
    public sealed class HeroEconomy : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingGold;
        [SerializeField] private bool authorityEnabled = true;
        private int gold;
        private bool initialized;

        public int Gold { get { EnsureInitialized(); return gold; } }
        public bool CanReceiveGold => authorityEnabled && (MatchStateController.Active == null || MatchStateController.Active.IsPlaying);
        public event Action<HeroEconomy, int> Changed;
        public event Action<HeroEconomy, int> GoldGained;

        private void Awake() => EnsureInitialized();

        public void EnsureInitialized()
        {
            if (initialized) return;
            gold = Mathf.Max(0, startingGold);
            initialized = true;
        }

        public void SetAuthorityEnabled(bool enabled) => authorityEnabled = enabled;

        public bool TryAddGold(int amount)
        {
            EnsureInitialized();
            if (!CanReceiveGold || amount <= 0) return false;
            gold = gold > int.MaxValue - amount ? int.MaxValue : gold + amount;
            GoldGained?.Invoke(this, amount);
            Changed?.Invoke(this, gold);
            return true;
        }

        public bool TrySpendGold(int amount)
        {
            EnsureInitialized();
            if (!CanReceiveGold || amount < 0 || gold < amount) return false;
            gold -= amount;
            Changed?.Invoke(this, gold);
            return true;
        }

        public void ApplyAuthoritativeState(int replicatedGold)
        {
            EnsureInitialized();
            authorityEnabled = false;
            int before = gold;
            gold = Mathf.Max(0, replicatedGold);
            if (gold > before)
            {
                GoldGained?.Invoke(this, gold - before);
            }
            Changed?.Invoke(this, gold);
        }
    }
}

using System;
using System.Collections.Generic;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Optional transport bridge used by the local shop UI in network matches.</summary>
    public interface IHeroInventoryRequestGateway
    {
        bool IsReady { get; }
        void RequestBuy(int itemIdHash);
        void RequestSell(int slot);
    }

    [RequireComponent(typeof(HeroUnit))]
    [RequireComponent(typeof(HeroEconomy))]
    public sealed class HeroInventory : MonoBehaviour
    {
        [SerializeField, Range(1, 12)] private int capacity = 6;
        private readonly List<ItemDefinition> slots = new();
        private HeroEconomy economy; private Health health; private BasicAttack attack; private ClickMover mover; private TeamMember team;
        private bool authorityEnabled = true;
        private bool initialized;
        public int Capacity { get { EnsureInitialized(); return capacity; } }
        public IReadOnlyList<ItemDefinition> Slots { get { EnsureInitialized(); return slots; } }
        public event Action<HeroInventory> Changed;
        public bool CanUseShop(ShopZone zone)
        {
            EnsureInitialized();
            return authorityEnabled && zone != null && team != null && team.Team == zone.Team && zone.Contains(transform.position) && health != null && health.IsAlive && (MatchStateController.Active == null || MatchStateController.Active.IsPlaying);
        }
        public void SetAuthorityEnabled(bool enabled) => authorityEnabled = enabled;
        public bool TryBuyById(string itemId, ShopZone zone)
        {
            return zone != null && zone.Catalog != null && zone.Catalog.TryGet(itemId, out ItemDefinition item) && TryBuy(item, zone);
        }
        public bool TryBuyByHash(int itemHash, ShopZone zone) => zone != null && zone.Catalog != null && zone.Catalog.TryGetByStableHash(itemHash, out ItemDefinition item) && TryBuy(item, zone);
        public int GetItemHash(int slot)
        {
            EnsureInitialized();
            return slot >= 0 && slot < slots.Count && slots[slot] != null ? ItemCatalog.StableHash(slots[slot].ItemId) : 0;
        }
        public bool ApplyAuthoritativeHashes(ItemCatalog catalog, int[] hashes)
        {
            EnsureInitialized();
            if (catalog == null || hashes == null || hashes.Length != capacity) return false;
            for (int i=0;i<capacity;i++) slots[i] = hashes[i] != 0 && catalog.TryGetByStableHash(hashes[i], out ItemDefinition item) ? item : null;
            Recalculate(); Changed?.Invoke(this); return true;
        }
        public string GetItemId(int slot)
        {
            EnsureInitialized();
            return slot >= 0 && slot < slots.Count && slots[slot] != null ? slots[slot].ItemId : string.Empty;
        }
        public bool ApplyAuthoritativeSlots(ItemCatalog catalog, string[] itemIds)
        {
            EnsureInitialized();
            if (catalog == null || itemIds == null || itemIds.Length != capacity) return false;
            for (int i=0;i<capacity;i++) { slots[i] = catalog.TryGet(itemIds[i], out ItemDefinition item) ? item : null; }
            Recalculate(); Changed?.Invoke(this); return true;
        }
        private void Awake() => EnsureInitialized();

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            economy = GetComponent<HeroEconomy>(); TryGetComponent(out health); TryGetComponent(out attack); TryGetComponent(out mover); TryGetComponent(out team);
            capacity = Mathf.Clamp(capacity, 1, 12);
            while (slots.Count < capacity) slots.Add(null);
            initialized = true;
        }
        public bool TryBuy(ItemDefinition item, ShopZone zone)
        {
            EnsureInitialized();
            if (item == null || !CanUseShop(zone) || !HasSpace() || economy.Gold < item.PurchasePrice) return false;
            if (!economy.TrySpendGold(item.PurchasePrice)) return false;
            slots[FirstEmpty()] = item; Recalculate(); Changed?.Invoke(this); return true;
        }
        public bool TrySell(int slot, ShopZone zone)
        {
            EnsureInitialized();
            if (!CanUseShop(zone) || slot < 0 || slot >= slots.Count || slots[slot] == null) return false;
            ItemDefinition item = slots[slot]; slots[slot] = null; economy.TryAddGold(item.SalePrice); Recalculate(); Changed?.Invoke(this); return true;
        }
        private bool HasSpace() => FirstEmpty() >= 0;
        private int FirstEmpty() { for(int i=0;i<slots.Count;i++) if(slots[i]==null) return i; return -1; }
        private void Recalculate()
        {
            float hp=0,dmg=0,move=0,atk=0; for(int i=0;i<slots.Count;i++){var item=slots[i];if(item==null)continue;hp+=item.MaximumHealthBonus;dmg+=item.AttackDamageBonus;move+=item.MovementSpeedBonus;atk+=item.AttackSpeedBonus;}
            health?.SetItemMaximumHealthBonus(hp); attack?.SetItemBonuses(dmg,atk); mover?.SetItemMoveSpeedBonus(move);
        }
    }
}

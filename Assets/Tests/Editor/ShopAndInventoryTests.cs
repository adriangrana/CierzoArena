using System.Collections.Generic;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class ShopAndInventoryTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }
            created.Clear();
        }

        [Test]
        public void CatalogResolvesStableIdsAndReportsDuplicates()
        {
            ItemDefinition plating = CreateItem("bastion.plating", 40, 20, 120f, 0f);
            ItemDefinition duplicate = CreateItem("bastion.plating", -3, -2, 0f, 0f);
            ItemCatalog catalog = CreateCatalog(plating, duplicate);

            Assert.That(catalog.TryGet("bastion.plating", out ItemDefinition resolved), Is.True);
            Assert.That(resolved, Is.SameAs(plating));
            Assert.That(catalog.TryGet("unknown.item", out _), Is.False);
            Assert.That(catalog.HasDuplicateIds, Is.True);
            Assert.That(duplicate.PurchasePrice, Is.EqualTo(0));
            Assert.That(duplicate.SalePrice, Is.EqualTo(0));
        }

        [Test]
        public void PurchaseSellAndHealthBonusesAreAtomicAndReversible()
        {
            ItemDefinition plating = CreateItem("bastion.plating", 40, 20, 120f, 0f);
            ItemCatalog catalog = CreateCatalog(plating);
            HeroInventory inventory = CreateHero("Azure", TeamId.Azure, 100);
            ShopZone zone = CreateZone(TeamId.Azure, catalog, Vector3.zero);
            Health health = inventory.GetComponent<Health>();

            Assert.That(inventory.TryBuyById(plating.ItemId, zone), Is.True);
            Assert.That(inventory.Slots[0], Is.SameAs(plating));
            Assert.That(inventory.GetComponent<HeroEconomy>().Gold, Is.EqualTo(60));
            Assert.That(health.Max, Is.EqualTo(620f));
            Assert.That(health.Current, Is.EqualTo(620f));

            Assert.That(inventory.TrySell(0, zone), Is.True);
            Assert.That(inventory.Slots[0], Is.Null);
            Assert.That(inventory.GetComponent<HeroEconomy>().Gold, Is.EqualTo(80));
            Assert.That(health.Max, Is.EqualTo(500f));
            Assert.That(health.Current, Is.EqualTo(500f));
            Assert.That(inventory.TrySell(0, zone), Is.False);
        }

        [Test]
        public void RejectsInvalidShopStateAndKeepsInventoryAfterDeath()
        {
            ItemDefinition edge = CreateItem("gale.edge", 40, 20, 0f, 15f);
            ItemCatalog catalog = CreateCatalog(edge);
            HeroInventory inventory = CreateHero("Azure", TeamId.Azure, 80);
            ShopZone friendly = CreateZone(TeamId.Azure, catalog, Vector3.zero);
            ShopZone enemy = CreateZone(TeamId.Ember, catalog, Vector3.zero);

            Assert.That(inventory.TryBuyById("missing", friendly), Is.False);
            Assert.That(inventory.TryBuyById(edge.ItemId, enemy), Is.False);
            Assert.That(inventory.TryBuyById(edge.ItemId, friendly), Is.True);
            inventory.GetComponent<Health>().ApplyDamage(999f);
            Assert.That(inventory.TryBuyById(edge.ItemId, friendly), Is.False);
            Assert.That(inventory.Slots[0], Is.SameAs(edge));
            Assert.That(inventory.GetComponent<BasicAttack>().Damage, Is.EqualTo(60f));
        }

        [Test]
        public void FullInventoryRejectsWithoutExtraGoldDeduction()
        {
            ItemDefinition edge = CreateItem("gale.edge", 10, 5, 0f, 1f);
            ItemCatalog catalog = CreateCatalog(edge);
            HeroInventory inventory = CreateHero("Azure", TeamId.Azure, 100);
            ShopZone zone = CreateZone(TeamId.Azure, catalog, Vector3.zero);

            for (int i = 0; i < inventory.Capacity; i++)
            {
                Assert.That(inventory.TryBuyById(edge.ItemId, zone), Is.True);
            }

            int goldBeforeRejectedPurchase = inventory.GetComponent<HeroEconomy>().Gold;
            Assert.That(inventory.TryBuyById(edge.ItemId, zone), Is.False);
            Assert.That(inventory.GetComponent<HeroEconomy>().Gold, Is.EqualTo(goldBeforeRejectedPurchase));
        }

        private ItemDefinition CreateItem(string id, int purchase, int sale, float health, float damage)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            created.Add(item);
            Set(item, "itemId", id);
            Set(item, "purchasePrice", purchase);
            Set(item, "salePrice", sale);
            Set(item, "maximumHealthBonus", health);
            Set(item, "attackDamageBonus", damage);
            return item;
        }

        private ItemCatalog CreateCatalog(params ItemDefinition[] items)
        {
            GameObject gameObject = Track(new GameObject("Catalog"));
            ItemCatalog catalog = gameObject.AddComponent<ItemCatalog>();
            Set(catalog, "items", items);
            catalog.Rebuild();
            return catalog;
        }

        private HeroInventory CreateHero(string name, TeamId team, int gold)
        {
            GameObject gameObject = Track(new GameObject(name));
            TeamMember member = gameObject.AddComponent<TeamMember>();
            Set(member, "team", team);
            gameObject.AddComponent<HeroUnit>();
            gameObject.AddComponent<Health>();
            gameObject.AddComponent<BasicAttack>();
            HeroEconomy economy = gameObject.AddComponent<HeroEconomy>();
            Set(economy, "startingGold", gold);
            Set(economy, "gold", gold);
            Set(economy, "initialized", true);
            HeroInventory inventory = gameObject.AddComponent<HeroInventory>();
            inventory.EnsureInitialized();
            return inventory;
        }

        private ShopZone CreateZone(TeamId team, ItemCatalog catalog, Vector3 position)
        {
            GameObject gameObject = Track(new GameObject("Shop"));
            gameObject.transform.position = position;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(10f, 10f, 10f);
            ShopZone zone = gameObject.AddComponent<ShopZone>();
            Set(zone, "team", team);
            Set(zone, "catalog", catalog);
            return zone;
        }

        private GameObject Track(GameObject gameObject)
        {
            created.Add(gameObject);
            return gameObject;
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"Missing field {field}");
            info.SetValue(target, value);
        }
    }
}

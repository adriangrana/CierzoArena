using System.Collections;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    public sealed class ShopAndInventoryPlayModeTests
    {
        [UnityTest]
        public IEnumerator ShopPurchaseAndSellUpdateInventoryGoldAndStats()
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            Set(item, "itemId", "windstep.boots");
            Set(item, "purchasePrice", 35);
            Set(item, "salePrice", 17);
            Set(item, "movementSpeedBonus", 0.8f);
            ItemCatalog catalog = CreateCatalog(item);
            HeroInventory inventory = CreateHero(50);
            ShopZone zone = CreateZone(catalog);

            yield return null;

            Assert.That(inventory.TryBuyById("windstep.boots", zone), Is.True);
            Assert.That(inventory.GetComponent<HeroEconomy>().Gold, Is.EqualTo(15));
            Assert.That(inventory.Slots[0], Is.SameAs(item));
            Assert.That(inventory.TrySell(0, zone), Is.True);
            Assert.That(inventory.GetComponent<HeroEconomy>().Gold, Is.EqualTo(32));
            Assert.That(inventory.Slots[0], Is.Null);

            Object.Destroy(inventory.gameObject);
            Object.Destroy(zone.gameObject);
            Object.Destroy(catalog.gameObject);
            Object.Destroy(item);
        }

        private static ItemCatalog CreateCatalog(ItemDefinition item)
        {
            GameObject objectWithCatalog = new GameObject("Catalog");
            ItemCatalog catalog = objectWithCatalog.AddComponent<ItemCatalog>();
            Set(catalog, "items", new[] { item });
            catalog.Rebuild();
            return catalog;
        }

        private static HeroInventory CreateHero(int gold)
        {
            GameObject hero = new GameObject("Azure Hero");
            TeamMember team = hero.AddComponent<TeamMember>();
            Set(team, "team", TeamId.Azure);
            hero.AddComponent<HeroUnit>();
            hero.AddComponent<Health>();
            HeroEconomy economy = hero.AddComponent<HeroEconomy>();
            Set(economy, "gold", gold);
            Set(economy, "initialized", true);
            return hero.AddComponent<HeroInventory>();
        }

        private static ShopZone CreateZone(ItemCatalog catalog)
        {
            GameObject zoneObject = new GameObject("Azure Shop");
            zoneObject.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, 8f);
            ShopZone zone = zoneObject.AddComponent<ShopZone>();
            Set(zone, "team", TeamId.Azure);
            Set(zone, "catalog", catalog);
            return zone;
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}

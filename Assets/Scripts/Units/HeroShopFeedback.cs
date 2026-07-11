using CierzoArena.CameraSystem;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>
    /// Deliberately small immediate-mode shop UI. It only exposes purchase intent;
    /// <see cref="HeroInventory"/> and its network bridge remain the authority.
    /// </summary>
    [RequireComponent(typeof(HeroInventory))]
    public sealed class HeroShopFeedback : MonoBehaviour
    {
        private HeroInventory inventory;
        private HeroEconomy economy;
        private CierzoArena.Core.TeamMember team;
        private IHeroInventoryRequestGateway networkGateway;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle rowStyle;

        private void Awake()
        {
            inventory = GetComponent<HeroInventory>();
            economy = GetComponent<HeroEconomy>();
            team = GetComponent<CierzoArena.Core.TeamMember>();
            FindNetworkGateway();
        }

        private void FindNetworkGateway()
        {
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IHeroInventoryRequestGateway gateway)
                {
                    networkGateway = gateway;
                    break;
                }
            }
        }

        private void OnGUI()
        {
            if (LocalHeroProvider.Active == null || LocalHeroProvider.Active.CurrentHero != transform || team == null)
            {
                return;
            }

            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            ShopZone zone = ShopZone.FindFriendlyContaining(team.Team, transform.position);
            DrawInventory();
            if (zone != null && zone.Catalog != null)
            {
                DrawShop(zone);
            }
            else
            {
                GUI.Label(new Rect(24f, 265f, 560f, 36f), "Shop: enter your team's luminous base circle to buy or sell.", rowStyle);
            }

            GUI.matrix = previous;
        }

        private void DrawInventory()
        {
            GUI.Box(new Rect(24f, 315f, 570f, 142f), GUIContent.none, panelStyle);
            GUI.Label(new Rect(38f, 324f, 520f, 30f), $"INVENTORY  {economy?.Gold ?? 0} GOLD", titleStyle);
            for (int i = 0; i < inventory.Capacity; i++)
            {
                ItemDefinition item = inventory.Slots[i];
                float x = 38f + i * 90f;
                GUI.Box(new Rect(x, 362f, 82f, 80f), item == null ? "Empty" : item.DisplayName, rowStyle);
                if (item != null && GUI.Button(new Rect(x, 416f, 82f, 22f), $"Sell +{item.SalePrice}"))
                {
                    RequestSell(i);
                }
            }
        }

        private void DrawShop(ShopZone zone)
        {
            GUI.Box(new Rect(620f, 96f, 480f, 362f), GUIContent.none, panelStyle);
            GUI.Label(new Rect(636f, 106f, 440f, 32f), "TEAM SHOP", titleStyle);
            float y = 145f;
            foreach (ItemDefinition item in zone.Catalog.Items)
            {
                if (item == null)
                {
                    continue;
                }

                GUI.Box(new Rect(636f, y, 448f, 55f), GUIContent.none, rowStyle);
                GUI.Label(new Rect(646f, y + 4f, 260f, 23f), item.DisplayName, rowStyle);
                GUI.Label(new Rect(646f, y + 27f, 290f, 22f), Bonuses(item), rowStyle);
                if (GUI.Button(new Rect(960f, y + 14f, 112f, 30f), $"Buy {item.PurchasePrice}"))
                {
                    RequestBuy(item, zone);
                }
                y += 62f;
            }
        }

        private void RequestBuy(ItemDefinition item, ShopZone zone)
        {
            if ((networkGateway ?? FindAndReturnNetworkGateway()) != null && networkGateway.IsReady)
            {
                networkGateway.RequestBuy(ItemCatalog.StableHash(item.ItemId));
                return;
            }

            inventory.TryBuy(item, zone);
        }

        private void RequestSell(int slot)
        {
            if ((networkGateway ?? FindAndReturnNetworkGateway()) != null && networkGateway.IsReady)
            {
                networkGateway.RequestSell(slot);
                return;
            }

            ShopZone zone = ShopZone.FindFriendlyContaining(team.Team, transform.position);
            inventory.TrySell(slot, zone);
        }

        private static string Bonuses(ItemDefinition item)
        {
            return $"HP +{item.MaximumHealthBonus:0}  DMG +{item.AttackDamageBonus:0}  MOV +{item.MovementSpeedBonus:0.0}  AS +{item.AttackSpeedBonus:0.00}";
        }

        private IHeroInventoryRequestGateway FindAndReturnNetworkGateway()
        {
            FindNetworkGateway();
            return networkGateway;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box);
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.88f, 0.25f) }
            };
            rowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }
    }
}

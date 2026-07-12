using CierzoArena.CameraSystem;
using CierzoArena.Frontend;
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
        private bool shopOpen;

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
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.B)shopOpen=!shopOpen;
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape)shopOpen=false;
            float scale = HudLayout.Scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            ShopZone zone = ShopZone.FindFriendlyContaining(team.Team, transform.position);
            DrawInventory();
            if (shopOpen && zone != null && zone.Catalog != null)
            {
                DrawShop(zone);
            }
            else if(shopOpen)
            {
                GUI.Label(new Rect(HudLayout.Inventory.x, HudLayout.Inventory.y-34f, 560f, 30f), "Shop: enter your team's luminous base circle to buy or sell.", rowStyle);
            }

            GUI.matrix = previous;
        }

        private void DrawInventory()
        {
            Rect panel=HudLayout.Inventory;GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panel.x+14f, panel.y+9f, 520f, 30f), $"INVENTORY  {economy?.Gold ?? 0} GOLD   [B] SHOP", titleStyle);
            for (int i = 0; i < inventory.Capacity; i++)
            {
                ItemDefinition item = inventory.Slots[i];
                float x = panel.x+14f + i * 90f;
                GUI.Box(new Rect(x, panel.y+47f, 82f, 80f), item == null ? "Empty" : item.DisplayName, rowStyle);
                if (item != null && GUI.Button(new Rect(x, panel.y+101f, 82f, 22f), $"Sell +{item.SalePrice}"))
                {
                    RequestSell(i);
                }
            }
        }

        private void DrawShop(ShopZone zone)
        {
            Rect panel=HudLayout.Shop;GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panel.x+16f, panel.y+10f, 440f, 32f), "TEAM SHOP  [B] Cerrar", titleStyle);
            float y = panel.y+50f;
            foreach (ItemDefinition item in zone.Catalog.Items)
            {
                if (item == null)
                {
                    continue;
                }

                GUI.Box(new Rect(panel.x+16f, y, panel.width-32f, 55f), GUIContent.none, rowStyle);
                GUI.Label(new Rect(panel.x+26f, y + 4f, 260f, 23f), item.DisplayName, rowStyle);
                GUI.Label(new Rect(panel.x+26f, y + 27f, 290f, 22f), Bonuses(item), rowStyle);
                if (GUI.Button(new Rect(panel.x+340f, y + 14f, 112f, 30f), $"Buy {item.PurchasePrice}"))
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

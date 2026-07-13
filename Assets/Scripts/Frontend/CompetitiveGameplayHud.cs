using System;
using System.Collections.Generic;
using CierzoArena.CameraSystem;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using CierzoArena.Units;
using UnityEngine;

namespace CierzoArena.Frontend
{
    /// <summary>
    /// Local-only competitive presentation. It binds exclusively through
    /// LocalHeroProvider and consumes existing authoritative/replicated component
    /// state; it never searches for a hero, changes simulation or synchronizes UI.
    /// </summary>
    public sealed class CompetitiveGameplayHud : MonoBehaviour
    {
        public static CompetitiveGameplayHud Active { get; private set; }
        private readonly List<MatchStatisticsSnapshot> scoreRows = new(8);
        private readonly List<Notice> notices = new(5);
        private Transform hero;
        private LocalHeroProvider provider;
        private Health health;
        private HeroMana mana;
        private HeroProgression progression;
        private HeroAbilities abilities;
        private HeroInventory inventory;
        private HeroEconomy economy;
        private HeroLifeCycle life;
        private BasicAttack attack;
        private ClickMover mover;
        private HeroMatchIdentity identity;
        private HeroMatchStatistics heroStatistics;
        private TeamMember team;
        private HeroDefinition definition;
        private IHeroAbilityRequestGateway abilityGateway;
        private IHeroInventoryRequestGateway inventoryGateway;
        private MatchStatisticsController matchStatistics;
        private MatchStateController matchState;
        private GUIStyle titleStyle, bodyStyle, smallStyle, centerStyle, keyStyle, tooltipTitleStyle, buttonStyle;
        private float styleScale = -1f;
        private bool shopOpen;
        private int draggedInventorySlot = -1;

        private struct Notice { public string Text; public float Until; public Color Color; }

        public Transform BoundHero => hero;
        public bool IsShopOpen => shopOpen;
        public int AbilitySlotCount => 4;
        public int InventorySlotCount => 6;
        public bool IsBoundToLocalHero => provider != null && hero != null && provider.CurrentHero == hero;

        private void OnEnable()
        {
            Active = this;
            TryAttachProvider();
        }

        private void OnDisable()
        {
            DetachProvider();
            UnbindHero();
            if (Active == this) Active = null;
        }

        private void Update()
        {
            TryAttachProvider();
            if (provider != null && provider.CurrentHero != hero) BindHero(provider.CurrentHero);
            if (Input.GetKeyDown(KeyCode.B) && hero != null) ToggleShop();
            if (Input.GetKeyDown(KeyCode.Escape)) shopOpen = false;
            HandleInventoryHotkeys();
            for (int i = notices.Count - 1; i >= 0; i--) if (notices[i].Until <= Time.unscaledTime) notices.RemoveAt(i);
        }

        public void ToggleShop() => shopOpen = !shopOpen;
        public void CloseShop() => shopOpen = false;

        private void TryAttachProvider()
        {
            LocalHeroProvider next = LocalHeroProvider.Active;
            if (next == provider) return;
            DetachProvider();
            provider = next;
            if (provider != null)
            {
                provider.HeroChanged += BindHero;
                BindHero(provider.CurrentHero);
            }
        }

        private void DetachProvider()
        {
            if (provider != null) provider.HeroChanged -= BindHero;
            provider = null;
        }

        /// <summary>Public for tests and scene-return cleanup.</summary>
        public void BindHero(Transform nextHero)
        {
            if (hero == nextHero) return;
            UnbindHero();
            if (nextHero == null) return;
            hero = nextHero;
            hero.TryGetComponent(out health); hero.TryGetComponent(out mana); hero.TryGetComponent(out progression);
            hero.TryGetComponent(out abilities); hero.TryGetComponent(out inventory); hero.TryGetComponent(out economy);
            hero.TryGetComponent(out life); hero.TryGetComponent(out attack); hero.TryGetComponent(out mover);
            hero.TryGetComponent(out identity); hero.TryGetComponent(out heroStatistics); hero.TryGetComponent(out team);
            if (identity != null) definition = HeroCatalog.Shared.ResolveOrFallback(identity.HeroDefinitionId);
            CacheGateways(); Subscribe(); DisableLegacyFeedback();
            PushNotice($"{DisplayName()} listo", new Color(.35f,.85f,1f));
        }

        public void ClearBinding() => UnbindHero();

        private void UnbindHero()
        {
            Unsubscribe();
            hero = null; health = null; mana = null; progression = null; abilities = null; inventory = null; economy = null;
            life = null; attack = null; mover = null; identity = null; heroStatistics = null; team = null; definition = null;
            abilityGateway = null; inventoryGateway = null; shopOpen = false;
            draggedInventorySlot = -1;
        }

        private void CacheGateways()
        {
            if (hero == null) return;
            MonoBehaviour[] components = hero.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IHeroAbilityRequestGateway abilitiesRequest) abilityGateway = abilitiesRequest;
                if (components[i] is IHeroInventoryRequestGateway inventoryRequest) inventoryGateway = inventoryRequest;
            }
        }

        private void Subscribe()
        {
            if (health != null) health.Changed += OnHealthChanged;
            if (mana != null) mana.Changed += OnManaChanged;
            if (progression != null) { progression.LevelUp += OnLevelUp; progression.ExperienceGained += OnExperience; }
            if (economy != null) { economy.GoldGained += OnGoldGained; economy.Changed += OnGoldChanged; }
            if (inventory != null) inventory.Changed += OnInventoryChanged;
            if (abilities != null) abilities.Changed += OnAbilitiesChanged;
            if (life != null) life.StateChanged += OnLifeStateChanged;
            matchStatistics = MatchStatisticsController.Active;
            if (matchStatistics != null) matchStatistics.AnnouncementRaised += OnAnnouncement;
            matchState = MatchStateController.Active;
        }

        private void Unsubscribe()
        {
            if (health != null) health.Changed -= OnHealthChanged;
            if (mana != null) mana.Changed -= OnManaChanged;
            if (progression != null) { progression.LevelUp -= OnLevelUp; progression.ExperienceGained -= OnExperience; }
            if (economy != null) { economy.GoldGained -= OnGoldGained; economy.Changed -= OnGoldChanged; }
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            if (abilities != null) abilities.Changed -= OnAbilitiesChanged;
            if (life != null) life.StateChanged -= OnLifeStateChanged;
            if (matchStatistics != null) matchStatistics.AnnouncementRaised -= OnAnnouncement;
            matchStatistics = null; matchState = null;
        }

        private void DisableLegacyFeedback()
        {
            if (hero == null) return;
            HeroProgressionFeedback progressionFeedback = hero.GetComponent<HeroProgressionFeedback>(); if (progressionFeedback != null) progressionFeedback.enabled = false;
            HeroAbilitiesFeedback abilitiesFeedback = hero.GetComponent<HeroAbilitiesFeedback>(); if (abilitiesFeedback != null) abilitiesFeedback.enabled = false;
            HeroShopFeedback shopFeedback = hero.GetComponent<HeroShopFeedback>(); if (shopFeedback != null) shopFeedback.enabled = false;
            HeroRespawnFeedback respawnFeedback = hero.GetComponent<HeroRespawnFeedback>(); if (respawnFeedback != null) respawnFeedback.enabled = false;
        }

        private void OnHealthChanged(Health _, float current, float maximum) { if (current < maximum) PushNotice("Daño recibido", new Color(1f,.52f,.38f)); }
        private void OnManaChanged(HeroMana _) { }
        private void OnLevelUp(HeroProgression _, int level) => PushNotice($"Nivel {level}", new Color(1f,.82f,.28f));
        private void OnExperience(HeroProgression _, int __) { }
        private void OnGoldGained(HeroEconomy _, int amount) => PushNotice($"+{amount} oro", new Color(1f,.82f,.28f));
        private void OnGoldChanged(HeroEconomy _, int __) { }
        private void OnInventoryChanged(HeroInventory _) => PushNotice("Inventario actualizado", new Color(.4f,.9f,.75f));
        private void OnAbilitiesChanged(HeroAbilities _) { }
        private void OnLifeStateChanged(HeroLifeCycle _, HeroLifeState state) => PushNotice(state == HeroLifeState.Alive ? "Reapareciste" : "Héroe caído", state == HeroLifeState.Alive ? new Color(.4f,.9f,.75f) : new Color(1f,.4f,.4f));
        private void OnAnnouncement(string text) => PushNotice(text, new Color(.72f,.8f,1f));
        private void PushNotice(string text, Color color)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (notices.Count == 5) notices.RemoveAt(0);
            notices.Add(new Notice { Text = text, Color = color, Until = Time.unscaledTime + 2.4f });
        }

        private void OnGUI()
        {
            if (hero == null || provider == null || provider.CurrentHero != hero) return;
            EnsureStyles();
            DrawScoreboard(); DrawCommandBar(); DrawNotices(); DrawMatchEnd();
            if (shopOpen) DrawShop();
            DrawDeathState();
        }

        private void DrawScoreboard()
        {
            MatchStatisticsController stats = MatchStatisticsController.Active;
            int azure = 0, ember = 0, seconds = stats != null ? stats.DurationSeconds : 0;
            if (stats != null)
            {
                stats.CopySnapshotsTo(scoreRows);
                for (int i = 0; i < scoreRows.Count; i++)
                {
                    if (scoreRows[i].Team == TeamId.Azure) azure += scoreRows[i].Kills;
                    else if (scoreRows[i].Team == TeamId.Ember) ember += scoreRows[i].Kills;
                }
            }
            float s = Scale(); float width = 470f * s; Rect rect = new((Screen.width - width) * .5f, 20f * s, width, 45f * s);
            Panel(rect, new Color(.02f,.055f,.095f,.92f), new Color(.18f,.7f,1f,.42f));
            GUI.Label(new Rect(rect.x + 12f*s, rect.y, 126f*s, rect.height), $"AZURE  {azure}", centerStyle);
            GUI.Label(new Rect(rect.center.x - 65f*s, rect.y, 130f*s, rect.height), $"{seconds/60:00}:{seconds%60:00}", centerStyle);
            GUI.Label(new Rect(rect.xMax - 138f*s, rect.y, 126f*s, rect.height), $"{ember}  EMBER", centerStyle);
        }

        private void DrawCommandBar()
        {
            float s = Scale(); float height = 228f*s;
            // Reserve the minimap's whole visual footprint plus a gutter. At lower
            // resolutions the command bar shifts left and narrows before it can ever
            // overlap a tactical control.
            Rect map = MinimapFeedback.TacticalMapRect;
            float leftGutter = 20f*s;
            float rightLimit = Mathf.Max(leftGutter + 420f*s, map.x - 18f*s);
            float width = Mathf.Min(1320f*s, rightLimit-leftGutter);
            width = Mathf.Max(420f*s, width);
            float x = Mathf.Clamp((Screen.width-width)*.5f, leftGutter, rightLimit-width);
            Rect bar = new(x, Screen.height-height-20f*s, width, height);
            Panel(bar, new Color(.018f,.045f,.075f,.96f), new Color(.23f,.68f,.9f,.46f));
            // Reserve real room for the portrait/name/stat block. Its information
            // should not collapse into a narrow column at 4K.
            float left = Mathf.Min(345f*s, bar.width*.34f);
            float center = Mathf.Min(540f*s, bar.width*.47f);
            float right = Mathf.Max(150f*s, bar.width-left-center);
            DrawIdentity(new Rect(bar.x,bar.y,left,bar.height),s);
            DrawVitalsAndAbilities(new Rect(bar.x+left,bar.y,center,bar.height),s);
            DrawInventory(new Rect(bar.x+left+center,bar.y,right,bar.height),s);
        }

        private void DrawIdentity(Rect rect, float s)
        {
            Rect portrait = new(rect.x+14f*s,rect.y+16f*s,122f*s,150f*s);
            Panel(portrait, new Color(.025f,.08f,.12f,1f), definition != null ? definition.ThemeColor : Color.cyan);
            if (definition != null && definition.Portrait != null) GUI.DrawTexture(portrait, definition.Portrait, ScaleMode.ScaleAndCrop, true);
            else GUI.Label(portrait, "HERO", centerStyle);
            int level = progression != null ? progression.Level : 1;
            Rect badge = new(portrait.x+6f*s,portrait.yMax-28f*s,36f*s,24f*s); Panel(badge,new Color(.02f,.06f,.1f,.96f),new Color(1f,.78f,.25f)); GUI.Label(badge,$"{level}",centerStyle);
            Rect text = new(portrait.xMax+15f*s,rect.y+19f*s,rect.width-portrait.width-34f*s,30f*s);
            GUI.Label(text,DisplayName(),titleStyle);
            MatchStatisticsSnapshot own = GetOwnSnapshot();
            GUI.Label(new Rect(text.x,text.y+33f*s,text.width,20f*s),$"K/D/A  {own.Kills} / {own.Deaths} / {own.Assists}    LH {own.LastHits}",smallStyle);
            float xp = progression != null && progression.ExperienceForNextLevel > 0 ? progression.Experience/(float)progression.ExperienceForNextLevel : 1f;
            Rect xpBar=new(text.x,text.y+63f*s,text.width,10f*s); Bar(xpBar,xp,new Color(.38f,.75f,1f),new Color(.04f,.08f,.13f));
            GUI.Label(new Rect(text.x,text.y+77f*s,text.width,19f*s),progression != null ? $"XP {progression.Experience}/{Mathf.Max(1,progression.ExperienceForNextLevel)}" : "XP —",smallStyle);
            GUI.Label(new Rect(text.x,text.y+111f*s,text.width,22f*s),$"DMG {attack?.Damage ?? 0:0}   MOV {mover?.EffectiveMoveSpeed ?? 0:0.0}   RNG {attack?.Range ?? 0:0.0}",smallStyle);
        }

        private void DrawVitalsAndAbilities(Rect rect, float s)
        {
            Rect healthBar = new(rect.x+14f*s,rect.y+14f*s,rect.width-28f*s,27f*s);
            float hp = health != null ? health.Current/Mathf.Max(1f,health.Max) : 0f;
            Bar(healthBar,hp,new Color(.18f,.76f,.39f),new Color(.05f,.12f,.09f)); GUI.Label(healthBar,$"{health?.Current ?? 0:0} / {health?.Max ?? 0:0}   +{HealthRegen():0.0}/s",centerStyle);
            Rect manaBar = new(healthBar.x,healthBar.yMax+6f*s,healthBar.width,22f*s);
            float mp = mana != null ? mana.CurrentMana/Mathf.Max(1f,mana.MaximumMana) : 0f;
            Bar(manaBar,mp,new Color(.16f,.55f,.95f),new Color(.04f,.08f,.16f)); GUI.Label(manaBar,$"{mana?.CurrentMana ?? 0:0} / {mana?.MaximumMana ?? 0:0}   +{mana?.RegenerationPerSecond ?? 0:0.0}/s",centerStyle);
            float slotWidth=(rect.width-39f*s)/4f; string[] keys={"Q","W","E","R"};
            for(int i=0;i<4;i++) DrawAbilitySlot(i,keys[i],new Rect(rect.x+14f*s+i*(slotWidth+3f*s),manaBar.yMax+14f*s,slotWidth,94f*s),s);
            if (abilities != null && abilities.SkillPoints > 0) GUI.Label(new Rect(rect.x+14f*s,rect.yMax-25f*s,rect.width-28f*s,18f*s),$"PUNTOS DISPONIBLES: {abilities.SkillPoints}",centerStyle);
        }

        private void DrawAbilitySlot(int slot,string key,Rect rect,float s)
        {
            AbilityDefinition ability=abilities!=null?abilities.GetDefinition(slot):null; bool dead=life!=null&&life.State!=HeroLifeState.Alive;
            float cooldown=abilities!=null?abilities.GetCooldown(slot):0f; int level=abilities!=null?abilities.GetLevel(slot):0;
            float cost=ability!=null?ability.ManaCost(Mathf.Max(1,level)):0f; bool noMana=mana!=null&&mana.CurrentMana<cost;
            Color border=slot==3?new Color(.72f,.48f,1f):new Color(.18f,.62f,.9f); Panel(rect,new Color(.025f,.075f,.12f,.98f),border);
            Rect icon = new(rect.x+5f*s,rect.y+5f*s,rect.width-10f*s,rect.height-10f*s);
            DrawAbilityIcon(ability, icon);
            GUI.Label(new Rect(rect.x+5f*s,rect.y+4f*s,25f*s,22f*s),key,keyStyle);
            GUI.Label(new Rect(rect.x+5f*s,rect.yMax-23f*s,rect.width-10f*s,19f*s),ability!=null?$"Nv {level}/{ability.MaximumLevel}":"—",centerStyle);
            if(cooldown>0f||dead||noMana){Color overlay=dead?new Color(0,0,0,.72f):noMana?new Color(.2f,.09f,.12f,.68f):new Color(0,0,0,.58f);GUI.color=overlay;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(rect,dead?"BLOQUEADA":noMana?"SIN MANÁ":$"{cooldown:0.0}",centerStyle);}
            bool canLevel=ability!=null&&abilities!=null&&abilities.SkillPoints>0&&level<ability.MaximumLevel&&progression!=null&&progression.Level>=ability.RequiredHeroLevel(level+1)&&!dead;
            if(canLevel&&GUI.Button(new Rect(rect.xMax-25f*s,rect.y+4f*s,20f*s,20f*s),"+")){if(abilityGateway!=null&&abilityGateway.IsReady)abilityGateway.RequestUpgrade(slot);else abilities.TryUpgrade(slot);}
            if(Event.current.type==EventType.Repaint&&rect.Contains(Event.current.mousePosition)&&ability!=null) DrawAbilityTooltip(ability,level,rect,s);
        }

        private void DrawInventory(Rect rect,float s)
        {
            float shopButtonWidth = Mathf.Min(193f*s, Mathf.Max(88f*s, rect.width-26f*s));
            GUI.Label(new Rect(rect.x+13f*s,rect.y+15f*s,Mathf.Max(0f,rect.width-shopButtonWidth-28f*s),28f*s),$"◈ {economy?.Gold ?? 0} ORO",titleStyle);
            if(GUI.Button(new Rect(rect.xMax-shopButtonWidth-13f*s,rect.y+11f*s,shopButtonWidth,38f*s),shopOpen?"CERRAR":"ABRIR [B]",buttonStyle))ToggleShop();
            float size=Mathf.Min(70f*s,(rect.width-38f*s)/3f);
            Event input = Event.current;
            for(int i=0;i<6;i++)
            {
                int col=i%3,row=i/3;Rect slot=new(rect.x+13f*s+col*(size+5f*s),rect.y+58f*s+row*(size+7f*s),size,size);
                ItemDefinition item=inventory!=null&&i<inventory.Slots.Count?inventory.Slots[i]:null;
                Panel(slot,new Color(.025f,.06f,.1f,.94f),item!=null?new Color(.75f,.63f,.28f):new Color(.18f,.28f,.36f));
                DrawItemIcon(item, new Rect(slot.x+3f*s,slot.y+3f*s,slot.width-6f*s,slot.height-6f*s));
                GUI.Label(new Rect(slot.x+3f*s,slot.yMax-18f*s,slot.width-6f*s,15f*s),InventoryHotkeyLabel(i),smallStyle);
                if(item!=null&&input.type==EventType.Repaint&&slot.Contains(input.mousePosition))DrawItemTooltip(item,slot,s);
                if(input.type==EventType.MouseDown&&input.button==0&&item!=null&&slot.Contains(input.mousePosition)){draggedInventorySlot=i;input.Use();}
                if(input.type==EventType.MouseUp&&input.button==0&&draggedInventorySlot>=0&&slot.Contains(input.mousePosition))
                {
                    RequestInventorySwap(draggedInventorySlot,i);draggedInventorySlot=-1;input.Use();
                }
            }
            if(input.type==EventType.MouseUp&&input.button==0)draggedInventorySlot=-1;
        }

        private void DrawShop()
        {
            float s=Scale();Rect rect=new Rect(Screen.width*.5f-300f*s,Screen.height*.5f-230f*s,600f*s,430f*s);Panel(rect,new Color(.025f,.065f,.11f,.98f),new Color(.35f,.78f,1f));GUI.Label(new Rect(rect.x+16f*s,rect.y+12f*s,rect.width-32f*s,28f*s),"TIENDA DEL EQUIPO",titleStyle);if(GUI.Button(new Rect(rect.xMax-120f*s,rect.y+10f*s,104f*s,32f*s),"CERRAR",buttonStyle))CloseShop();
            ShopZone zone=team!=null?ShopZone.FindFriendlyContaining(team.Team,hero.position):null;if(zone==null||zone.Catalog==null){GUI.Label(new Rect(rect.x+20f*s,rect.y+64f*s,rect.width-40f*s,50f*s),"Debes estar en la base de tu equipo para comprar.",bodyStyle);return;}
            int index=0;float cell=150f*s;foreach(ItemDefinition item in zone.Catalog.Items){if(item==null)continue;int col=index%3,row=index/3;Rect card=new(rect.x+26f*s+col*(cell+18f*s),rect.y+62f*s+row*(cell+24f*s),cell,cell);bool affordable=economy!=null&&economy.Gold>=item.PurchasePrice;Panel(card,new Color(.035f,.085f,.13f,.96f),affordable?new Color(.25f,.62f,.82f):new Color(.22f,.25f,.3f));DrawItemIcon(item,new Rect(card.x+8f*s,card.y+8f*s,card.width-16f*s,card.height-42f*s));GUI.enabled=affordable;if(GUI.Button(new Rect(card.x+16f*s,card.yMax-30f*s,card.width-32f*s,22f*s),$"{item.PurchasePrice} oro",buttonStyle))Buy(item,zone);GUI.enabled=true;if(Event.current.type==EventType.Repaint&&card.Contains(Event.current.mousePosition))DrawItemTooltip(item,card,s);index++;}
        }

        private void Buy(ItemDefinition item,ShopZone zone)
        {
            bool result;
            if (inventoryGateway != null && inventoryGateway.IsReady)
            {
                inventoryGateway.RequestBuy(ItemCatalog.StableHash(item.ItemId));
                result = true;
            }
            else result = inventory != null && inventory.TryBuy(item, zone);
            PushNotice(result ? "Compra solicitada" : "No se pudo comprar", result ? new Color(.4f,.9f,.75f) : new Color(1f,.45f,.35f));
        }
        private void HandleInventoryHotkeys()
        {
            if (hero == null || !(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) return;
            KeyCode[] keys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.A, KeyCode.S, KeyCode.D };
            for (int i = 0; i < keys.Length; i++)
            {
                if (Input.GetKeyDown(keys[i])) ActivateInventorySlot(i);
            }
        }

        private void ActivateInventorySlot(int slot)
        {
            ItemDefinition item = inventory != null && slot >= 0 && slot < inventory.Slots.Count ? inventory.Slots[slot] : null;
            if (item == null) { PushNotice($"{InventoryHotkeyLabel(slot)} sin objeto", new Color(.7f,.8f,.88f)); return; }
            // M11's current catalogue is deliberately passive-only. The command is
            // bound now, and gives clear feedback instead of silently doing nothing;
            // active-item effects can later use this exact slot contract.
            PushNotice($"{item.DisplayName}: objeto pasivo", new Color(.72f,.82f,1f));
        }

        private void RequestInventorySwap(int sourceSlot, int destinationSlot)
        {
            if (sourceSlot == destinationSlot) return;
            bool accepted;
            if (inventoryGateway != null && inventoryGateway.IsReady)
            {
                inventoryGateway.RequestSwap(sourceSlot, destinationSlot);
                accepted = true;
            }
            else accepted = inventory != null && inventory.TrySwap(sourceSlot, destinationSlot);
            PushNotice(accepted ? "Inventario reorganizado" : "No se pudo reorganizar", accepted ? new Color(.4f,.9f,.75f) : new Color(1f,.45f,.35f));
        }

        private static string InventoryHotkeyLabel(int slot)
        {
            string[] labels = { "ALT+Q", "ALT+W", "ALT+E", "ALT+A", "ALT+S", "ALT+D" };
            return slot >= 0 && slot < labels.Length ? labels[slot] : string.Empty;
        }

        private static void DrawAbilityIcon(AbilityDefinition ability, Rect rect)
        {
            if (HudIconLibrary.TryGetAbility(ability, out Texture2D texture, out Rect uv)) GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            else GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private static void DrawItemIcon(ItemDefinition item, Rect rect)
        {
            if (item != null && HudIconLibrary.TryGetItem(item, out Texture2D texture, out Rect uv)) GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            else if (item == null) GUI.DrawTexture(rect, Texture2D.blackTexture);
            else GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
        private void DrawAbilityTooltip(AbilityDefinition ability,int level,Rect slot,float s){Rect rect=new(slot.x-35f*s,slot.y-170f*s,Mathf.Min(310f*s,Screen.width-24f*s),155f*s);if(rect.x<12f*s)rect.x=12f*s;Panel(rect,new Color(.01f,.03f,.06f,.98f),new Color(.25f,.72f,1f));GUI.Label(new Rect(rect.x+12f*s,rect.y+10f*s,rect.width-24f*s,24f*s),ability.DisplayName,tooltipTitleStyle);GUI.Label(new Rect(rect.x+12f*s,rect.y+38f*s,rect.width-24f*s,55f*s),ability.Description,smallStyle);GUI.Label(new Rect(rect.x+12f*s,rect.y+100f*s,rect.width-24f*s,20f*s),$"Nivel {level}/{ability.MaximumLevel}  ·  {ability.Targeting}  ·  Rango {ability.Range:0.0}",smallStyle);GUI.Label(new Rect(rect.x+12f*s,rect.y+122f*s,rect.width-24f*s,20f*s),$"Maná {ability.ManaCost(Mathf.Max(1,level)):0}  ·  CD {ability.Cooldown(Mathf.Max(1,level)):0.0}s",smallStyle);}
        private void DrawItemTooltip(ItemDefinition item,Rect slot,float s){Rect rect=new(slot.x-135f*s,slot.y-125f*s,260f*s,126f*s);if(rect.x<12f*s)rect.x=12f*s;Panel(rect,new Color(.01f,.03f,.06f,.98f),new Color(.75f,.63f,.28f));GUI.Label(new Rect(rect.x+10f*s,rect.y+8f*s,rect.width-20f*s,22f*s),item.DisplayName,tooltipTitleStyle);GUI.Label(new Rect(rect.x+10f*s,rect.y+34f*s,rect.width-20f*s,42f*s),item.Description,smallStyle);GUI.Label(new Rect(rect.x+10f*s,rect.y+80f*s,rect.width-20f*s,18f*s),$"Precio {item.PurchasePrice}  ·  Venta {item.SalePrice}",smallStyle);GUI.Label(new Rect(rect.x+10f*s,rect.y+101f*s,rect.width-20f*s,18f*s),"Arrastra para reorganizar · objeto pasivo",smallStyle);}
        private void DrawNotices(){float s=Scale();float y=85f*s;for(int i=0;i<notices.Count;i++){Notice n=notices[i];Rect rect=new Rect(Screen.width*.5f-180f*s,y,360f*s,25f*s);Panel(rect,new Color(.02f,.05f,.09f,.8f),n.Color);Color old=GUI.color;GUI.color=n.Color;GUI.Label(rect,n.Text,centerStyle);GUI.color=old;y+=29f*s;}}
        private void DrawDeathState(){if(life==null||life.State==HeroLifeState.Alive)return;float s=Scale();Rect rect=new Rect(Screen.width*.5f-150f*s,Screen.height*.5f-38f*s,300f*s,76f*s);Panel(rect,new Color(.05f,.01f,.02f,.92f),new Color(1f,.32f,.25f));GUI.Label(new Rect(rect.x,rect.y+10f*s,rect.width,25f*s),"HÉROE CAÍDO",titleStyle);GUI.Label(new Rect(rect.x,rect.y+38f*s,rect.width,24f*s),$"Reaparición en {Mathf.CeilToInt(life.RespawnRemaining)}",centerStyle);}
        private void DrawMatchEnd(){MatchStateController state=MatchStateController.Active;if(state==null||state.IsPlaying)return;float s=Scale();bool won=(state.CurrentState==MatchState.AzureVictory&&team!=null&&team.Team==TeamId.Azure)||(state.CurrentState==MatchState.EmberVictory&&team!=null&&team.Team==TeamId.Ember);Rect rect=new Rect(Screen.width*.5f-190f*s,92f*s,380f*s,56f*s);Panel(rect,new Color(.02f,.04f,.08f,.94f),won?new Color(.35f,.9f,.75f):new Color(1f,.34f,.3f));GUI.Label(rect,won?"VICTORIA":"DERROTA",titleStyle);}
        private MatchStatisticsSnapshot GetOwnSnapshot(){MatchStatisticsController stats=MatchStatisticsController.Active;if(stats==null||heroStatistics==null)return default;stats.CopySnapshotsTo(scoreRows);for(int i=0;i<scoreRows.Count;i++)if(scoreRows[i].HeroId==heroStatistics.HeroId)return scoreRows[i];return default;}
        private float HealthRegen()=>definition!=null?definition.HealthRegen:0f; private string DisplayName()=>definition!=null?definition.DisplayName:identity!=null?identity.DisplayName:"Hero"; private static string ShortName(string value)=>string.IsNullOrWhiteSpace(value)?"—":value.Length>13?value.Substring(0,12)+"…":value; private static string Initials(string value)=>string.IsNullOrWhiteSpace(value)?"·":value.Substring(0,1).ToUpperInvariant();
        // At 4K the bar must still occupy a useful 55–70% of the screen rather
        // than shrinking into a technically correct but unreadable strip.
        private static float Scale()=>Mathf.Clamp(Screen.height/1080f,.82f,1.9f);
        private void EnsureStyles(){float s=Scale();if(Mathf.Approximately(styleScale,s))return;styleScale=s;titleStyle=new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(18*s),fontStyle=FontStyle.Bold,wordWrap=false,clipping=TextClipping.Clip,normal={textColor=Color.white},alignment=TextAnchor.MiddleLeft};bodyStyle=new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(13*s),wordWrap=true,normal={textColor=Color.white}};smallStyle=new GUIStyle(bodyStyle){fontSize=Mathf.RoundToInt(11*s),normal={textColor=new Color(.74f,.85f,.93f)}};centerStyle=new GUIStyle(bodyStyle){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(12*s)};keyStyle=new GUIStyle(centerStyle){fontStyle=FontStyle.Bold,fontSize=Mathf.RoundToInt(15*s),normal={textColor=new Color(.55f,.9f,1f)}};tooltipTitleStyle=new GUIStyle(titleStyle){fontSize=Mathf.RoundToInt(15*s),normal={textColor=new Color(.55f,.9f,1f)}};buttonStyle=new GUIStyle(GUI.skin.button){fontSize=Mathf.RoundToInt(12*s),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};}
        private static void Panel(Rect rect,Color fill,Color border){Color old=GUI.color;GUI.color=fill;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=border;GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,1f),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.x,rect.yMax-1f,rect.width,1f),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.x,rect.y,1f,rect.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.xMax-1f,rect.y,1f,rect.height),Texture2D.whiteTexture);GUI.color=old;}
        private static void Bar(Rect rect,float value,Color fill,Color background){Panel(rect,background,new Color(1f,1f,1f,.12f));Color old=GUI.color;GUI.color=fill;GUI.DrawTexture(new Rect(rect.x+1f,rect.y+1f,Mathf.Max(0f,rect.width-2f)*Mathf.Clamp01(value),Mathf.Max(0f,rect.height-2f)),Texture2D.whiteTexture);GUI.color=old;}
    }
}

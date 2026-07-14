using System;
using System.Collections.Generic;
using System.Linq;
using CierzoArena.Core;
using CierzoArena.Online.Room;
using CierzoArena.Units;
using UnityEngine;
using UnityEngine.UI;

namespace CierzoArena.Online
{
    /// <summary>Visual layer for the server-authoritative hero draft. The layout is
    /// intentionally information-first: compact team pick strips, a central roster
    /// and a permanent inspection panel for the currently highlighted hero.</summary>
    public sealed class HeroSelectionPanel : MonoBehaviour
    {
        private const int TeamCapacity = 5;
        private MultiplayerSessionCoordinator coordinator;
        private Font font;
        private Text timer;
        private Text phase;
        private Text detailName;
        private Text detailMeta;
        private Text detailDescription;
        private Text detailStats;
        private Text detailAbilities;
        private Text chooseLabel;
        private Button chooseButton;
        private Image timerSurface;
        private RawImage inspectedPortrait;
        private Transform azureShelf;
        private Transform emberShelf;
        private Transform gallery;
        private HeroDefinition highlighted;
        private string requestedHeroId;
        private readonly Dictionary<string, Button> heroButtons = new();

        public static void Show(Canvas canvas, MultiplayerSessionCoordinator owner)
        {
            if (canvas == null || owner == null) return;
            MultiplayerRoomPanel room = canvas.GetComponentInChildren<MultiplayerRoomPanel>(true);
            if (room != null) room.gameObject.SetActive(false);
            HeroSelectionPanel existing = canvas.GetComponentInChildren<HeroSelectionPanel>(true);
            if (existing != null) { existing.gameObject.SetActive(true); existing.coordinator = owner; existing.Refresh(); return; }
            GameObject root = new GameObject("Hero Selection Panel", typeof(RectTransform), typeof(Image), typeof(HeroSelectionPanel));
            root.transform.SetParent(canvas.transform, false);
            HeroSelectionPanel panel = root.GetComponent<HeroSelectionPanel>();
            panel.coordinator = owner;
            panel.Build();
        }

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform root = GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            GetComponent<Image>().color = new Color(.006f, .016f, .032f, .99f);
            Image("Azure atmosphere", transform, new Vector2(0, .76f), new Vector2(.5f, 1), new Color(.025f, .17f, .25f, .5f)).raycastTarget = false;
            Image("Ember atmosphere", transform, new Vector2(.5f, .76f), Vector2.one, new Color(.22f, .05f, .035f, .34f)).raycastTarget = false;
            Image("Top rule", transform, new Vector2(.02f, .925f), new Vector2(.98f, .928f), new Color(.42f, .67f, .8f, .42f)).raycastTarget = false;

            Label("Brand", "CIERZO ARENA", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(.62f, .83f, .96f), new Vector2(.025f, .94f), new Vector2(.18f, .987f));
            Label("Draft heading", "SELECCIÓN DE HÉROES", 27, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(.29f, .936f), new Vector2(.71f, .987f));
            Label("Draft subtitle", "DRAFT SINCRONIZADO · BLOQUEO AUTORITATIVO", 10, FontStyle.Bold, TextAnchor.MiddleRight, new Color(.58f, .72f, .84f), new Vector2(.75f, .94f), new Vector2(.975f, .987f));

            azureShelf = TeamShelf("Azure Shelf", "AZURE", new Vector2(.025f, .785f), new Vector2(.405f, .91f), new Color(.1f, .68f, 1f));
            emberShelf = TeamShelf("Ember Shelf", "EMBER", new Vector2(.595f, .785f), new Vector2(.975f, .91f), new Color(1f, .31f, .16f));
            timerSurface = Image("Draft clock", transform, new Vector2(.425f, .79f), new Vector2(.575f, .908f), new Color(.015f, .052f, .08f));
            AddOutline(timerSurface.gameObject, new Color(.25f, .75f, 1f, .72f), 1.5f);
            timer = Label("Timer", "25", 36, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0, .31f), new Vector2(1, .92f), timerSurface.transform);
            phase = Label("Phase", "ESPERANDO AL ANFITRIÓN", 10, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(.52f, .82f, 1f), new Vector2(0, .06f), new Vector2(1, .32f), timerSurface.transform);

            Image gallerySurface = Image("Hero roster", transform, new Vector2(.025f, .115f), new Vector2(.675f, .77f), new Color(.009f, .028f, .055f, .95f));
            AddOutline(gallerySurface.gameObject, new Color(.20f, .43f, .59f, .5f), 1f);
            gallery = gallerySurface.transform;
            Label("Roster heading", "ELIGE TU HÉROE", 20, FontStyle.Bold, TextAnchor.UpperLeft, Color.white, new Vector2(.035f, .92f), new Vector2(.54f, .985f), gallery);
            Label("Roster copy", "HÉROES DISPONIBLES · LOS BLOQUEADOS QUEDAN FUERA DEL DRAFT", 10, FontStyle.Bold, TextAnchor.UpperRight, new Color(.48f, .72f, .87f), new Vector2(.42f, .925f), new Vector2(.965f, .98f), gallery);
            Image("Roster divider", gallery, new Vector2(.035f, .895f), new Vector2(.965f, .899f), new Color(.22f, .50f, .67f, .55f)).raycastTarget = false;
            BuildGallery();

            Transform inspection = Image("Hero inspection", transform, new Vector2(.705f, .115f), new Vector2(.975f, .77f), new Color(.008f, .022f, .045f, .98f)).transform;
            AddOutline(inspection.gameObject, new Color(.35f, .58f, .73f, .58f), 1f);
            BuildInspection(inspection);
            Label("Footer", "LA ELECCIÓN FINAL SE CONFIRMA EN EL HOST · CIERZO ARENA PROTOTYPE", 10, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(.37f, .54f, .67f), new Vector2(.15f, .04f), new Vector2(.85f, .085f));
            coordinator.Changed += OnChanged;
            Refresh();
        }

        private void BuildGallery()
        {
            IReadOnlyList<HeroDefinition> heroes = HeroCatalog.Shared.Heroes;
            for (int index = 0; index < heroes.Count; index++)
            {
                HeroDefinition hero = heroes[index]; if (hero == null) continue;
                const int columns = 8;
                int column = index % columns;
                int row = index / columns;
                float x = .035f + column * .117f;
                float yMax = .86f - row * .20f;
                Image card = Image("Hero " + hero.HeroId, gallery, new Vector2(x, yMax - .175f), new Vector2(x + .102f, yMax), new Color(.018f, .058f, .09f, .98f));
                AddOutline(card.gameObject, new Color(hero.ThemeColor.r, hero.ThemeColor.g, hero.ThemeColor.b, .46f), 1f);
                RawImage portrait = Raw("Portrait", card.transform);
                portrait.rectTransform.anchorMin = new Vector2(.04f, .04f); portrait.rectTransform.anchorMax = new Vector2(.96f, .96f); portrait.rectTransform.offsetMin = portrait.rectTransform.offsetMax = Vector2.zero;
                SetPortrait(portrait, hero.Portrait, hero.ThemeColor);
                Button button = card.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => { highlighted = hero; requestedHeroId = hero.HeroId; Refresh(); });
                heroButtons[hero.HeroId] = button;
            }
        }

        private void BuildInspection(Transform panel)
        {
            inspectedPortrait = Raw("Hero portrait", panel);
            inspectedPortrait.rectTransform.anchorMin = new Vector2(.055f, .53f); inspectedPortrait.rectTransform.anchorMax = new Vector2(.945f, .94f); inspectedPortrait.rectTransform.offsetMin = inspectedPortrait.rectTransform.offsetMax = Vector2.zero;
            inspectedPortrait.raycastTarget = false;
            Image("Portrait veil", panel, new Vector2(.055f, .53f), new Vector2(.945f, .67f), new Color(.002f, .008f, .02f, .82f)).raycastTarget = false;
            detailName = Label("Hero name", string.Empty, 22, FontStyle.Bold, TextAnchor.LowerLeft, Color.white, new Vector2(.09f, .61f), new Vector2(.90f, .68f), panel);
            detailMeta = Label("Hero meta", string.Empty, 10, FontStyle.Bold, TextAnchor.LowerLeft, new Color(.35f, .8f, 1f), new Vector2(.09f, .565f), new Vector2(.90f, .615f), panel);
            detailDescription = Label("Hero description", string.Empty, 11, FontStyle.Normal, TextAnchor.UpperLeft, new Color(.77f, .85f, .92f), new Vector2(.075f, .37f), new Vector2(.925f, .515f), panel);
            detailStats = Label("Hero stats", string.Empty, 10, FontStyle.Bold, TextAnchor.UpperLeft, Color.white, new Vector2(.075f, .255f), new Vector2(.925f, .35f), panel);
            detailAbilities = Label("Hero abilities", string.Empty, 10, FontStyle.Bold, TextAnchor.UpperLeft, new Color(.67f, .82f, .94f), new Vector2(.075f, .145f), new Vector2(.925f, .24f), panel);
            chooseButton = Button("Choose hero", "ELEGIR HÉROE", new Vector2(.075f, .045f), new Vector2(.925f, .115f), new Color(.06f, .36f, .58f), panel);
            chooseLabel = chooseButton.GetComponentInChildren<Text>();
            chooseButton.onClick.AddListener(() => { if (!string.IsNullOrWhiteSpace(requestedHeroId)) _ = coordinator.SubmitHeroSelectionAsync(requestedHeroId); });
        }

        private Transform TeamShelf(string name, string teamName, Vector2 min, Vector2 max, Color accent)
        {
            Image shelf = Image(name, transform, min, max, new Color(.008f, .025f, .045f, .96f));
            AddOutline(shelf.gameObject, new Color(accent.r, accent.g, accent.b, .65f), 1f);
            Label("Team", teamName, 11, FontStyle.Bold, TextAnchor.UpperLeft, accent, new Vector2(.025f, .75f), new Vector2(.98f, .98f), shelf.transform);
            return shelf.transform;
        }

        private void Refresh()
        {
            if (coordinator?.Sessions == null) return;
            IReadOnlyList<MatchPlayerSlot> players = coordinator.Sessions.Roster.Players;
            HeroSelectionSnapshot draft = coordinator.Sessions.HeroSelection;
            List<MatchPlayerSlot> order = AlternatingOrder(players);
            MatchPlayerSlot picker = draft.TurnIndex >= 0 && draft.TurnIndex < order.Count ? order[draft.TurnIndex] : null;
            PopulateTeamShelf(azureShelf, players.Where(player => player.Team == TeamId.Azure), picker, new Color(.1f, .68f, 1f));
            PopulateTeamShelf(emberShelf, players.Where(player => player.Team == TeamId.Ember), picker, new Color(1f, .31f, .16f));
            MatchPlayerSlot local = players.FirstOrDefault(player => player.PlayerId == coordinator.Identity?.PlayerId);
            bool localTurn = picker != null && local != null && picker.PlayerId == local.PlayerId;
            if (highlighted == null) highlighted = HeroCatalog.Shared.DefaultHero;
            if (local != null && !string.IsNullOrWhiteSpace(local.HeroIntentId)) requestedHeroId = local.HeroIntentId;
            phase.text = draft.IsLoadingMatch ? "DRAFT COMPLETO" : picker == null ? "ESPERANDO" : localTurn ? "TU TURNO" : "TURNO DE " + picker.DisplayName.ToUpperInvariant();
            chooseButton.interactable = localTurn;
            UpdateInspection(localTurn, local);
            foreach (HeroDefinition hero in HeroCatalog.Shared.Heroes) UpdateHeroButton(hero, players, localTurn);
        }

        private void Update()
        {
            if (coordinator?.Sessions == null || timer == null) return;
            float remaining = coordinator.Sessions.HeroSelection.SecondsRemaining(DateTimeOffset.UtcNow);
            bool critical = remaining > 0 && remaining <= 5;
            timer.text = Mathf.CeilToInt(remaining).ToString("00");
            timer.color = critical ? Color.Lerp(new Color(1f, .25f, .12f), Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 1f)) : Color.white;
            timerSurface.color = critical ? new Color(.28f, .045f, .03f, .98f) : new Color(.015f, .052f, .08f, .98f);
        }

        private void PopulateTeamShelf(Transform shelf, IEnumerable<MatchPlayerSlot> members, MatchPlayerSlot picker, Color accent)
        {
            for (int index = shelf.childCount - 1; index >= 0; index--) if (shelf.GetChild(index).name.StartsWith("Pick ")) Destroy(shelf.GetChild(index).gameObject);
            Dictionary<int, MatchPlayerSlot> slots = members.ToDictionary(member => member.StableSlot, member => member);
            for (int index = 0; index < TeamCapacity; index++)
            {
                float x = .035f + index * .19f;
                Image slot = Image("Pick " + index, shelf, new Vector2(x, .12f), new Vector2(x + .105f, .74f), new Color(.025f, .06f, .085f, .98f));
                if (!slots.TryGetValue(index, out MatchPlayerSlot player)) { Label("Empty", "—", 14, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(.22f, .34f, .43f), Vector2.zero, Vector2.one, slot.transform); continue; }
                bool active = picker != null && picker.PlayerId == player.PlayerId;
                if (active) { AddOutline(slot.gameObject, accent, 2f); slot.color = Color.Lerp(slot.color, accent, .2f); }
                HeroDefinition locked = player.HeroPickState == HeroPickState.Locked || player.HeroPickState == HeroPickState.AutoPicked ? HeroCatalog.Shared.ResolveOrFallback(player.HeroId) : null;
                if (locked?.Portrait != null)
                {
                    RawImage portrait = Raw("Portrait", slot.transform); portrait.raycastTarget = false; SetPortrait(portrait, locked.Portrait, locked.ThemeColor);
                    portrait.rectTransform.anchorMin = new Vector2(.04f, .04f); portrait.rectTransform.anchorMax = new Vector2(.96f, .96f); portrait.rectTransform.offsetMin = portrait.rectTransform.offsetMax = Vector2.zero;
                }
                Label("Name", player.DisplayName, 8, FontStyle.Bold, TextAnchor.UpperCenter, Color.white, new Vector2(-.12f, -.30f), new Vector2(1.12f, -.05f), slot.transform);
                if (active && locked == null) Label("Active", "ELIGE", 8, FontStyle.Bold, TextAnchor.MiddleCenter, accent, Vector2.zero, Vector2.one, slot.transform);
            }
        }

        private void UpdateInspection(bool localTurn, MatchPlayerSlot local)
        {
            if (highlighted == null) return;
            SetPortrait(inspectedPortrait, highlighted.Portrait, highlighted.ThemeColor);
            detailName.text = highlighted.DisplayName.ToUpperInvariant();
            detailMeta.text = Role(highlighted.PrimaryRole).ToUpperInvariant() + " · " + Attack(highlighted.AttackStyle).ToUpperInvariant() + " · DOMINIO " + new string('◆', highlighted.Difficulty);
            detailDescription.text = highlighted.Description;
            detailStats.text = $"VIDA  {highlighted.BaseHealth:0}     MANÁ  {highlighted.BaseMana:0}\nDAÑO  {highlighted.BaseDamage:0}     ALCANCE  {highlighted.AttackRange:0.0}";
            detailAbilities.text = "HABILIDADES\n" + string.Join("  ·  ", Enumerable.Range(0, 4).Select(index => highlighted.GetAbility(index)?.DisplayName ?? "—"));
            bool confirmed = local != null && (local.HeroPickState == HeroPickState.Locked || local.HeroPickState == HeroPickState.AutoPicked);
            chooseLabel.text = confirmed ? "HÉROE CONFIRMADO" : localTurn ? "ELEGIR " + highlighted.DisplayName.ToUpperInvariant() : local != null && local.HeroPickState == HeroPickState.Intent ? "SELECCIÓN ENVIADA" : "ESPERANDO TU TURNO";
        }

        private void UpdateHeroButton(HeroDefinition hero, IReadOnlyList<MatchPlayerSlot> players, bool localTurn)
        {
            if (hero == null || !heroButtons.TryGetValue(hero.HeroId, out Button button)) return;
            bool taken = players.Any(player => (player.HeroPickState == HeroPickState.Locked || player.HeroPickState == HeroPickState.AutoPicked) && player.HeroId == hero.HeroId);
            button.interactable = localTurn && !taken;
            Image card = button.GetComponent<Image>();
            if (card != null) card.color = taken ? new Color(.045f, .045f, .055f, .96f) : hero == highlighted ? Color.Lerp(new Color(.025f, .08f, .12f), hero.ThemeColor, .25f) : new Color(.018f, .058f, .09f, .98f);
        }

        private void OnChanged(MultiplayerSessionCoordinator _) => Refresh();
        private void OnDestroy() { if (coordinator != null) coordinator.Changed -= OnChanged; }
        private static List<MatchPlayerSlot> AlternatingOrder(IReadOnlyList<MatchPlayerSlot> players) { List<MatchPlayerSlot> azure = players.Where(player => player.Team == TeamId.Azure).OrderBy(player => player.StableSlot).ToList(); List<MatchPlayerSlot> ember = players.Where(player => player.Team == TeamId.Ember).OrderBy(player => player.StableSlot).ToList(); List<MatchPlayerSlot> order = new(); for (int index = 0; index < Math.Max(azure.Count, ember.Count); index++) { if (index < azure.Count) order.Add(azure[index]); if (index < ember.Count) order.Add(ember[index]); } return order; }
        private static string Role(HeroRole role) => role switch { HeroRole.Vanguard => "Vanguardia", HeroRole.Carry => "Carry", HeroRole.Duelist => "Duelista", HeroRole.Mage => "Mago", HeroRole.Support => "Apoyo", _ => "Control" };
        private static string Attack(HeroAttackStyle style) => style == HeroAttackStyle.Melee ? "Cuerpo a cuerpo" : "A distancia";
        private Image Image(string name, Transform parent, Vector2 min, Vector2 max, Color color) { GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image)); item.transform.SetParent(parent, false); RectTransform rect = item.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; Image image = item.GetComponent<Image>(); image.color = color; return image; }
        private static RawImage Raw(string name, Transform parent) { GameObject item = new GameObject(name, typeof(RectTransform), typeof(RawImage)); item.transform.SetParent(parent, false); return item.GetComponent<RawImage>(); }
        private static void SetPortrait(RawImage image, Texture texture, Color fallback)
        {
            if (image == null) return;
            image.texture = texture;
            image.color = texture == null ? fallback : Color.white;
            AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>() ?? image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = texture != null && texture.height > 0 ? (float)texture.width / texture.height : 1f;
        }
        private Text Label(string name, string value, int size, FontStyle style, TextAnchor alignment, Color color, Vector2 min, Vector2 max, Transform parent = null) { GameObject item = new GameObject(name, typeof(RectTransform), typeof(Text)); item.transform.SetParent(parent ?? transform, false); RectTransform rect = item.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; Text text = item.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = alignment; text.color = color; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; return text; }
        private Button Button(string name, string value, Vector2 min, Vector2 max, Color color, Transform parent) { Image image = Image(name, parent, min, max, color); Button button = image.gameObject.AddComponent<Button>(); Label("Label", value, 14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, image.transform); return button; }
        private static void AddOutline(GameObject target, Color color, float distance) { Outline outline = target.AddComponent<Outline>(); outline.effectColor = color; outline.effectDistance = new Vector2(distance, -distance); }
    }
}

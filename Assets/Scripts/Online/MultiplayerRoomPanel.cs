using System;
using System.Collections.Generic;
using CierzoArena.Core;
using CierzoArena.Online.Room;
using CierzoArena.Units;
using UnityEngine;
using UnityEngine.UI;

namespace CierzoArena.Online
{
    /// <summary>Canvas presentation for private rooms. Networking stays in the
    /// coordinator; this component only renders the roster and sends player intent.</summary>
    public sealed class MultiplayerRoomPanel : MonoBehaviour
    {
        private const int TeamCapacity = 5;
        private MultiplayerSessionCoordinator coordinator;
        private HeroDefinition hero;
        private TeamId requestedTeam;
        private Font font;
        private RectTransform entrySurface;
        private RectTransform roomSurface;
        private RectTransform chatSurface;
        private RectTransform azureSlots;
        private RectTransform emberSlots;
        private Text entryStatus;
        private Text roomTitle;
        private Text roomStatus;
        private Text unassigned;
        private Text chatLog;
        private InputField displayName;
        private InputField joinCode;
        private InputField chatInput;
        private Button create;
        private Button join;
        private Button retry;
        private Button copy;
        private Button ready;
        private Button start;
        private Button leave;
        private Button sendChat;

        public static void Show(Canvas canvas, HeroDefinition selectedHero, TeamId selectedTeam)
        {
            if (canvas == null) return;
            MultiplayerRoomPanel existing = canvas.GetComponentInChildren<MultiplayerRoomPanel>(true);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                existing.hero = selectedHero;
                existing.requestedTeam = selectedTeam;
                existing.Refresh();
                return;
            }

            GameObject root = new GameObject("Multiplayer Room Panel", typeof(RectTransform), typeof(Image), typeof(MultiplayerRoomPanel));
            root.transform.SetParent(canvas.transform, false);
            MultiplayerRoomPanel panel = root.GetComponent<MultiplayerRoomPanel>();
            panel.hero = selectedHero;
            panel.requestedTeam = selectedTeam;
            panel.Build();
        }

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform root = GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            GetComponent<Image>().color = new Color(.001f, .006f, .016f, .74f);
            Button backdrop = gameObject.AddComponent<Button>();
            backdrop.onClick.AddListener(DismissPanel);

            BuildEntry();
            BuildRoom();
            BuildChat();

            coordinator = MultiplayerSessionCoordinator.Ensure();
            coordinator.Changed += OnChanged;
            _ = coordinator.OpenMultiplayerAsync();
            Refresh();
        }

        private void BuildEntry()
        {
            entrySurface = Panel("Room Entry", new Vector2(.31f, .22f), new Vector2(.69f, .70f), new Color(.008f, .025f, .052f, .985f)).rectTransform;
            CapturePanelClicks(entrySurface.gameObject);
            AddOutline(entrySurface.gameObject, new Color(.18f, .72f, 1f, .72f), 2f);
            Label("Title", entrySurface, "MULTIJUGADOR · SALA PRIVADA", 25, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(.06f,.83f), new Vector2(.94f,.95f));
            entryStatus = Label("Status", entrySurface, "Conectando con servicios…", 14, FontStyle.Normal, TextAnchor.UpperLeft, new Color(.66f,.78f,.9f), new Vector2(.06f,.69f), new Vector2(.94f,.80f));
            displayName = Input("Display Name", entrySurface, "Nombre de jugador", new Vector2(.06f,.54f), new Vector2(.94f,.64f));
            joinCode = Input("Join Code", entrySurface, "Código de sala", new Vector2(.06f,.41f), new Vector2(.94f,.51f));
            create = Button("Create", entrySurface, "CREAR SALA PRIVADA", new Vector2(.06f,.27f), new Vector2(.49f,.36f), new Color(.06f,.38f,.60f));
            join = Button("Join", entrySurface, "UNIRSE POR CÓDIGO", new Vector2(.51f,.27f), new Vector2(.94f,.36f), new Color(.07f,.12f,.21f));
            retry = Button("Retry", entrySurface, "REINTENTAR", new Vector2(.36f,.10f), new Vector2(.64f,.18f), new Color(.08f,.13f,.22f));
            create.onClick.AddListener(() => { ApplyName(); _ = coordinator.CreatePrivateRoomAsync(); });
            join.onClick.AddListener(() => { ApplyName(); _ = coordinator.JoinByCodeAsync(joinCode.text); });
            retry.onClick.AddListener(() => _ = coordinator.RetryAsync());
        }

        private void BuildRoom()
        {
            roomSurface = Panel("Private Room", new Vector2(.645f, .045f), new Vector2(.985f, .885f), new Color(.008f,.019f,.040f,.985f)).rectTransform;
            CapturePanelClicks(roomSurface.gameObject);
            AddOutline(roomSurface.gameObject, new Color(.14f,.46f,.68f,.78f), 2f);
            roomTitle = Label("Title", roomSurface, "SALA PRIVADA", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(.04f,.915f), new Vector2(.70f,.985f));
            copy = Button("Copy Code", roomSurface, "COPIAR CÓDIGO", new Vector2(.73f,.925f), new Vector2(.96f,.975f), new Color(.05f,.20f,.33f));
            roomStatus = Label("Status", roomSurface, string.Empty, 12, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(.60f,.74f,.86f), new Vector2(.04f,.865f), new Vector2(.96f,.91f));
            Label("Azure Heading", roomSurface, "AZURE", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(.37f,.78f,1f), new Vector2(.04f,.795f), new Vector2(.48f,.85f));
            Label("Ember Heading", roomSurface, "EMBER", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f,.46f,.39f), new Vector2(.52f,.795f), new Vector2(.96f,.85f));
            azureSlots = Panel("Azure Slots", roomSurface, new Vector2(.04f,.405f), new Vector2(.48f,.79f), new Color(.012f,.052f,.088f,.94f)).rectTransform;
            emberSlots = Panel("Ember Slots", roomSurface, new Vector2(.52f,.405f), new Vector2(.96f,.79f), new Color(.088f,.020f,.032f,.94f)).rectTransform;
            Image unassignedSurface = Panel("Unassigned", roomSurface, new Vector2(.04f,.315f), new Vector2(.96f,.38f), new Color(.009f,.026f,.047f,.96f));
            unassigned = Label("Players", unassignedSurface.transform, string.Empty, 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(.67f,.75f,.84f), Vector2.zero, Vector2.one);
            Image settings = Panel("Room Settings", roomSurface, new Vector2(.04f,.175f), new Vector2(.96f,.29f), new Color(.013f,.035f,.062f,.96f));
            Label("Settings Label", settings.transform, "AJUSTES DE LA SALA", 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(.29f,.73f,1f), new Vector2(.04f,.55f), new Vector2(.96f,.92f));
            Label("Settings Values", settings.transform, "MODO  ·  ELECCIÓN LIBRE                 SERVIDOR  ·  RELAY\nBOTS  ·  NINGUNO                              VISIBILIDAD  ·  PRIVADA", 11, FontStyle.Bold, TextAnchor.LowerLeft, new Color(.78f,.83f,.89f), new Vector2(.04f,.07f), new Vector2(.96f,.62f));
            ready = Button("Ready", roomSurface, "LISTO", new Vector2(.04f,.09f), new Vector2(.40f,.15f), new Color(.06f,.36f,.25f));
            start = Button("Start", roomSurface, "INICIAR PARTIDA", new Vector2(.42f,.09f), new Vector2(.96f,.15f), new Color(.12f,.43f,.67f));
            leave = Button("Leave", roomSurface, "ABANDONAR SALA", new Vector2(.54f,.02f), new Vector2(.96f,.065f), new Color(.30f,.07f,.09f));
            copy.onClick.AddListener(CopyJoinCode);
            ready.onClick.AddListener(() => _ = coordinator.ToggleReadyAsync());
            start.onClick.AddListener(() => _ = coordinator.StartGameAsync());
            leave.onClick.AddListener(LeaveRoom);
        }

        private void BuildChat()
        {
            chatSurface = Panel("Room Chat", new Vector2(.245f, .045f), new Vector2(.62f, .37f), new Color(.007f,.018f,.035f,.985f)).rectTransform;
            CapturePanelClicks(chatSurface.gameObject);
            AddOutline(chatSurface.gameObject, new Color(.14f,.42f,.60f,.72f), 1.5f);
            Label("Heading", chatSurface, "SALA · CHAT DE JUGADORES", 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(.34f,.79f,1f), new Vector2(.035f,.89f), new Vector2(.96f,.98f));
            chatLog = Label("Messages", chatSurface, string.Empty, 13, FontStyle.Normal, TextAnchor.UpperLeft, new Color(.84f,.9f,.96f), new Vector2(.035f,.24f), new Vector2(.96f,.86f));
            chatInput = Input("Message", chatSurface, "Escribe un mensaje…", new Vector2(.035f,.055f), new Vector2(.78f,.18f));
            sendChat = Button("Send", chatSurface, "ENVIAR", new Vector2(.80f,.055f), new Vector2(.96f,.18f), new Color(.06f,.35f,.55f));
            sendChat.onClick.AddListener(SendChat);
            chatInput.onEndEdit.AddListener(value => { if (UnityEngine.Input.GetKey(KeyCode.Return) || UnityEngine.Input.GetKey(KeyCode.KeypadEnter)) SendChat(); });
        }

        private void OnDestroy()
        {
            if (coordinator != null) coordinator.Changed -= OnChanged;
        }

        private void OnChanged(MultiplayerSessionCoordinator _) => Refresh();

        private void DismissPanel() => gameObject.SetActive(false);

        private void Refresh()
        {
            if (coordinator == null) return;
            bool inRoom = coordinator.Sessions != null && coordinator.Sessions.IsInSession;
            bool heroSelect = coordinator.State == OnlineState.HeroSelect;
            entrySurface.gameObject.SetActive(!inRoom);
            roomSurface.gameObject.SetActive(inRoom && !heroSelect);
            chatSurface.gameObject.SetActive(inRoom && !heroSelect);
            entryStatus.text = coordinator.Status;
            retry.gameObject.SetActive(!inRoom && (coordinator.State == OnlineState.ConfigurationRequired || coordinator.State == OnlineState.Error));
            if (!inRoom || heroSelect) return;

            roomTitle.text = "SALA PRIVADA · " + coordinator.Sessions.JoinCode;
            roomStatus.text = coordinator.Status;
            bool host = coordinator.Sessions.IsLocalHost;
            MatchPlayerSlot local = coordinator.Sessions.Roster.Find(coordinator.Sessions.LocalPlayerId);
            ready.gameObject.SetActive(local != null);
            start.gameObject.SetActive(host);
            ready.GetComponentInChildren<Text>().text = local != null && local.IsReady ? "CANCELAR LISTO" : "LISTO";
            leave.GetComponentInChildren<Text>().text = host ? "CERRAR SALA" : "ABANDONAR SALA";
            RenderTeam(azureSlots, TeamId.Azure, new Color(.14f,.62f,.94f));
            RenderTeam(emberSlots, TeamId.Ember, new Color(.94f,.28f,.24f));
            unassigned.text = "JUGADORES SIN ASIGNAR  ·  0\nLos jugadores se colocan en un equipo al entrar en la sala.";
            RenderChat();
        }

        private void RenderTeam(RectTransform container, TeamId team, Color color)
        {
            ClearChildren(container);
            List<MatchPlayerSlot> players = new List<MatchPlayerSlot>();
            foreach (MatchPlayerSlot player in coordinator.Sessions.Roster.Players) if (player.Team == team) players.Add(player);
            players.Sort((left, right) => left.StableSlot.CompareTo(right.StableSlot));
            for (int slot = 0; slot < TeamCapacity; slot++)
            {
                MatchPlayerSlot player = players.Find(value => value.StableSlot == slot);
                float top = .96f - slot * .19f;
                Image row = Panel("Slot " + (slot + 1), container, new Vector2(.04f, top - .15f), new Vector2(.96f, top), player == null ? new Color(.012f,.025f,.043f,.92f) : new Color(color.r*.18f,color.g*.18f,color.b*.18f,.98f));
                if (player == null)
                {
                    Button move = row.gameObject.AddComponent<Button>();
                    ColorBlock colors = move.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(.66f,.88f,1f,1f);
                    colors.pressedColor = new Color(.38f,.68f,.88f,1f);
                    move.colors = colors;
                    Text open = Label("Open", row.transform, "PLAZA ABIERTA · PULSA PARA UNIRTE", 9, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(.58f,.73f,.84f,.95f), Vector2.zero, Vector2.one);
                    open.raycastTarget = false;
                    int targetSlot = slot;
                    move.onClick.AddListener(() => { requestedTeam = team; _ = coordinator.ChangeTeamAsync(team, targetSlot); });
                    continue;
                }
                bool isLocal = string.Equals(player.PlayerId, coordinator.Sessions.LocalPlayerId, StringComparison.Ordinal);
                Label("Name", row.transform, (isLocal ? "TÚ · " : string.Empty) + player.DisplayName, 12, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(.06f,.10f), new Vector2(.67f,.90f));
                Label("Ready", row.transform, player.IsReady ? "LISTO" : "ESPERANDO", 10, FontStyle.Bold, TextAnchor.MiddleRight, player.IsReady ? new Color(.48f,.95f,.62f) : new Color(.77f,.77f,.72f), new Vector2(.63f,.10f), new Vector2(.94f,.90f));
            }
        }

        private void RenderChat()
        {
            List<ChatLine> lines = new List<ChatLine>();
            HashSet<string> unique = new HashSet<string>();
            foreach (MatchPlayerSlot player in coordinator.Sessions.Roster.Players)
            {
                foreach (RoomChatEntry entry in RoomChatHistory.Parse(player.ChatHistory))
                {
                    string key = player.PlayerId + "|" + entry.Timestamp + "|" + entry.Text;
                    if (unique.Add(key)) lines.Add(new ChatLine(entry.Timestamp, player.DisplayName, entry.Text));
                }
            }
            lines.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
            int first = Mathf.Max(0, lines.Count - 7);
            string value = string.Empty;
            for (int i = first; i < lines.Count; i++) value += "<b>" + lines[i].Name + ":</b> " + lines[i].Text + "\n";
            chatLog.supportRichText = true;
            chatLog.text = string.IsNullOrWhiteSpace(value) ? "Aún no hay mensajes. Saluda a tu equipo." : value;
        }

        private async void SendChat()
        {
            if (chatInput == null || string.IsNullOrWhiteSpace(chatInput.text)) return;
            string message = chatInput.text;
            chatInput.text = string.Empty;
            await coordinator.SendRoomChatAsync(message);
        }

        private void ApplyName()
        {
            if (displayName != null && !string.IsNullOrWhiteSpace(displayName.text)) coordinator.TrySetDisplayName(displayName.text, out _);
        }

        private void CopyJoinCode()
        {
            if (coordinator?.Sessions == null || string.IsNullOrWhiteSpace(coordinator.Sessions.JoinCode)) return;
            GUIUtility.systemCopyBuffer = coordinator.Sessions.JoinCode;
            roomStatus.text = "Código de sala copiado.";
        }

        private async void LeaveRoom()
        {
            bool host = coordinator?.Sessions != null && coordinator.Sessions.IsLocalHost;
            if (coordinator != null)
            {
                if (host && coordinator.Sessions.IsInSession) await coordinator.CloseRoomAsync();
                else await coordinator.LeaveAsync();
            }
            Destroy(gameObject);
        }

        private Image Panel(string name, Vector2 min, Vector2 max, Color color) => Panel(name, transform, min, max, color);
        private static void CapturePanelClicks(GameObject surface)
        {
            Button blocker = surface.AddComponent<Button>();
            blocker.transition = Selectable.Transition.None;
            blocker.targetGraphic = null;
        }
        private Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            item.GetComponent<Image>().color = color;
            return item.GetComponent<Image>();
        }

        private Text Label(string name, Transform parent, string value, int size, FontStyle style, TextAnchor anchor, Color color, Vector2 min, Vector2 max)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = item.GetComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = anchor; text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button Button(string name, Transform parent, string value, Vector2 min, Vector2 max, Color color)
        {
            Image image = Panel(name, parent, min, max, color);
            Button result = image.gameObject.AddComponent<Button>();
            ColorBlock colors = result.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(.72f,.90f,1f,1f); colors.pressedColor = new Color(.45f,.70f,.90f,1f); colors.fadeDuration = .08f;
            result.colors = colors;
            AddOutline(image.gameObject, new Color(.35f,.62f,.80f,.5f), 1f);
            Shadow shadow = image.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0f,0f,0f,.66f); shadow.effectDistance = new Vector2(1.5f,-1.5f);
            Image topBevel = Panel("Top Bevel", image.transform, new Vector2(.025f,.92f), new Vector2(.975f,.965f), new Color(1f,1f,1f,.16f)); topBevel.raycastTarget = false;
            Image bottomBevel = Panel("Bottom Bevel", image.transform, new Vector2(.025f,.035f), new Vector2(.975f,.09f), new Color(0f,0f,0f,.34f)); bottomBevel.raycastTarget = false;
            Label("Label", image.transform, value, 11, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one);
            return result;
        }

        private Button CloseButton(string name, Transform parent, Vector2 min, Vector2 max)
        {
            Button result = Button(name, parent, "×", min, max, new Color(.48f,.08f,.10f,.98f));
            Text label = result.GetComponentInChildren<Text>();
            label.fontSize = 30;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f,.91f,.83f);
            AddOutline(result.gameObject, new Color(1f,.45f,.30f,.75f), 1f);
            return result;
        }

        private InputField Input(string name, Transform parent, string placeholder, Vector2 min, Vector2 max)
        {
            Image image = Panel(name, parent, min, max, new Color(.018f,.057f,.095f,.98f));
            InputField field = image.gameObject.AddComponent<InputField>();
            Text text = Label("Text", image.transform, string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, new Vector2(.04f,0), new Vector2(.96f,1));
            Text hint = Label("Placeholder", image.transform, placeholder, 13, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(.55f,.65f,.75f,.72f), new Vector2(.04f,0), new Vector2(.96f,1));
            field.textComponent = text;
            field.placeholder = hint;
            return field;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }

        private static void AddOutline(GameObject target, Color color, float distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private readonly struct ChatLine
        {
            public readonly long Timestamp;
            public readonly string Name;
            public readonly string Text;
            public ChatLine(long timestamp, string name, string text) { Timestamp = timestamp; Name = name; Text = text; }
        }
    }
}

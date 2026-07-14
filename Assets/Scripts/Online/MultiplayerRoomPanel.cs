using System;
using CierzoArena.Core;
using CierzoArena.Online.Room;
using CierzoArena.Units;
using UnityEngine;
using UnityEngine.UI;

namespace CierzoArena.Online
{
    /// <summary>Runtime Canvas room panel. It contains no transport logic: all
    /// actions are delegated to MultiplayerSessionCoordinator.</summary>
    public sealed class MultiplayerRoomPanel : MonoBehaviour
    {
        private MultiplayerSessionCoordinator coordinator;
        private HeroDefinition hero;
        private TeamId requestedTeam;
        private Text title;
        private Text status;
        private Text roster;
        private InputField displayName;
        private InputField joinCode;
        private Button create;
        private Button join;
        private Button azure;
        private Button ember;
        private Button ready;
        private Button start;
        private Button copy;
        private Button retry;
        private Font font;

        public static void Show(Canvas canvas, HeroDefinition selectedHero, TeamId selectedTeam)
        {
            if (canvas == null) return;
            MultiplayerRoomPanel existing = canvas.GetComponentInChildren<MultiplayerRoomPanel>(true);
            if (existing != null) { existing.gameObject.SetActive(true); existing.hero = selectedHero; existing.requestedTeam = selectedTeam; existing.Refresh(); return; }
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
            RectTransform rect = GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.22f, .12f); rect.anchorMax = new Vector2(.78f, .88f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            GetComponent<Image>().color = new Color(.008f, .02f, .045f, .98f);
            Outline outline = gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.18f, .72f, 1f, .85f); outline.effectDistance = new Vector2(2f, -2f);
            title = Label("Title", "MULTIJUGADOR · SALA PRIVADA", 26, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(.05f, .88f), new Vector2(.84f, .97f));
            status = Label("Status", "Conectando con servicios…", 14, FontStyle.Normal, TextAnchor.UpperLeft, new Color(.66f, .78f, .9f), new Vector2(.05f, .77f), new Vector2(.94f, .86f));
            displayName = Input("Display Name", "Nombre de jugador", new Vector2(.05f,.68f), new Vector2(.46f,.74f));
            joinCode = Input("Join Code", "Código de sala", new Vector2(.54f,.68f), new Vector2(.94f,.74f));
            create = Button("Create", "CREAR SALA PRIVADA", new Vector2(.05f,.59f), new Vector2(.46f,.65f), new Color(.08f,.42f,.64f));
            join = Button("Join", "UNIRSE POR CÓDIGO", new Vector2(.54f,.59f), new Vector2(.94f,.65f), new Color(.075f,.12f,.2f));
            Label("Roster Heading", "JUGADORES · 10 MÁXIMO · 5 POR EQUIPO", 13, FontStyle.Bold, TextAnchor.UpperLeft, new Color(.25f,.78f,1f), new Vector2(.05f,.52f), new Vector2(.94f,.57f));
            roster = Label("Roster", string.Empty, 14, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, new Vector2(.05f,.25f), new Vector2(.94f,.51f));
            azure = Button("Azure", "AZURE", new Vector2(.05f,.16f), new Vector2(.26f,.22f), new Color(.06f,.28f,.47f));
            ember = Button("Ember", "EMBER", new Vector2(.28f,.16f), new Vector2(.49f,.22f), new Color(.32f,.09f,.11f));
            ready = Button("Ready", "LISTO", new Vector2(.51f,.16f), new Vector2(.72f,.22f), new Color(.08f,.38f,.28f));
            start = Button("Start", "INICIAR", new Vector2(.74f,.16f), new Vector2(.94f,.22f), new Color(.42f,.27f,.07f));
            copy = Button("Copy", "COPIAR CÓDIGO", new Vector2(.66f,.89f), new Vector2(.87f,.96f), new Color(.075f,.12f,.2f));
            Button close = Button("Close", "ABANDONAR", new Vector2(.05f,.06f), new Vector2(.33f,.12f), new Color(.25f,.07f,.09f));
            retry = Button("Retry", "REINTENTAR", new Vector2(.48f,.89f), new Vector2(.64f,.96f), new Color(.075f,.12f,.2f));
            create.onClick.AddListener(() => { ApplyName(); _ = coordinator.CreatePrivateRoomAsync(); });
            join.onClick.AddListener(() => { ApplyName(); _ = coordinator.JoinByCodeAsync(joinCode.text); });
            azure.onClick.AddListener(() => { requestedTeam = TeamId.Azure; _ = coordinator.ChangeTeamAsync(requestedTeam); });
            ember.onClick.AddListener(() => { requestedTeam = TeamId.Ember; _ = coordinator.ChangeTeamAsync(requestedTeam); });
            ready.onClick.AddListener(() => _ = coordinator.ToggleReadyAsync());
            start.onClick.AddListener(() => _ = coordinator.StartGameAsync());
            copy.onClick.AddListener(CopyJoinCode);
            close.onClick.AddListener(LeaveRoom);
            retry.onClick.AddListener(() => _ = coordinator.RetryAsync());
            coordinator = MultiplayerSessionCoordinator.Ensure();
            coordinator.Changed += OnChanged;
            _ = coordinator.OpenMultiplayerAsync();
            Refresh();
        }

        private void OnDestroy() { if (coordinator != null) coordinator.Changed -= OnChanged; }
        private void CopyJoinCode()
        {
            if (coordinator?.Sessions == null || string.IsNullOrWhiteSpace(coordinator.Sessions.JoinCode)) return;
            GUIUtility.systemCopyBuffer = coordinator.Sessions.JoinCode;
            status.text = "Código copiado.";
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
        private void OnChanged(MultiplayerSessionCoordinator _) => Refresh();
        private void ApplyName() { if (displayName != null && !string.IsNullOrWhiteSpace(displayName.text)) coordinator.TrySetDisplayName(displayName.text, out _); }
        private void Refresh()
        {
            if (coordinator == null) return;
            bool inRoom = coordinator.Sessions != null && coordinator.Sessions.IsInSession;
            bool heroSelect = coordinator.State == OnlineState.HeroSelect;
            title.text = inRoom ? $"SALA PRIVADA · {coordinator.Sessions.JoinCode}" : "MULTIJUGADOR · SALA PRIVADA";
            status.text = coordinator.Status;
            displayName.gameObject.SetActive(!inRoom);
            joinCode.gameObject.SetActive(!inRoom);
            create.gameObject.SetActive(!inRoom);
            join.gameObject.SetActive(!inRoom);
            azure.gameObject.SetActive(inRoom && !heroSelect);
            ember.gameObject.SetActive(inRoom && !heroSelect);
            ready.gameObject.SetActive(inRoom && !heroSelect);
            bool host = inRoom && coordinator.Sessions.IsLocalHost;
            Button close = transform.Find("Close")?.GetComponent<Button>();
            if (close != null) close.GetComponentInChildren<Text>().text = host ? "CERRAR SALA" : "ABANDONAR";
            retry.gameObject.SetActive(!inRoom && (coordinator.State == OnlineState.ConfigurationRequired || coordinator.State == OnlineState.Error));
            start.gameObject.SetActive(host && !heroSelect);
            copy.gameObject.SetActive(inRoom);
            if (inRoom)
            {
                string azureRows = "AZURE\n";
                string emberRows = "EMBER\n";
                foreach (MatchPlayerSlot player in coordinator.Sessions.Roster.Players)
                {
                    string row = $"{player.StableSlot + 1}. {player.DisplayName}  {(player.IsReady ? "LISTO" : "esperando")}\n";
                    if (player.Team == TeamId.Ember) emberRows += row; else azureRows += row;
                }
                roster.text = azureRows + "\n" + emberRows;
            }
            else roster.text = "Identidad persistente y salas Relay.\nLocal Development y Host/Client directo siguen disponibles para desarrollo.";
        }

        private Text Label(string name, string value, int size, FontStyle style, TextAnchor anchor, Color color, Vector2 min, Vector2 max)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(Text)); item.transform.SetParent(transform, false);
            RectTransform rect = item.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = item.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = anchor; text.color = color; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; return text;
        }

        private Button Button(string name, string value, Vector2 min, Vector2 max, Color color)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); item.transform.SetParent(transform, false);
            RectTransform rect = item.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = item.GetComponent<Image>(); image.color = color; Button button = item.GetComponent<Button>();
            LabelOn(item.transform, value, 14); return button;
        }

        private InputField Input(string name, string placeholder, Vector2 min, Vector2 max)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField)); item.transform.SetParent(transform, false);
            RectTransform rect = item.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            item.GetComponent<Image>().color = new Color(.025f,.065f,.105f,.98f); InputField field = item.GetComponent<InputField>();
            Text text = LabelOn(item.transform, string.Empty, 14); text.alignment = TextAnchor.MiddleLeft; text.rectTransform.offsetMin = new Vector2(10,0); text.rectTransform.offsetMax = new Vector2(-10,0);
            Text hint = LabelOn(item.transform, placeholder, 14); hint.color = new Color(.55f,.65f,.75f,.7f); hint.fontStyle = FontStyle.Italic; hint.alignment = TextAnchor.MiddleLeft; hint.rectTransform.offsetMin = new Vector2(10,0); hint.rectTransform.offsetMax = new Vector2(-10,0);
            field.textComponent = text; field.placeholder = hint; return field;
        }

        private Text LabelOn(Transform parent, string value, int size)
        {
            GameObject child = new GameObject("Label", typeof(RectTransform), typeof(Text)); child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = child.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.fontStyle = FontStyle.Bold; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; return text;
        }
    }
}

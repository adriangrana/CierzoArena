using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using CierzoArena.Core;
using CierzoArena.Frontend;
using CierzoArena.Online.Identity;
using CierzoArena.Online.Room;
using CierzoArena.Online.Sessions;
using CierzoArena.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CierzoArena.Online
{
    /// <summary>Single M24 flow owner. UI delegates intent here; it owns cancellation,
    /// errors and the transition boundary, while existing NGO gameplay remains in the
    /// M18 bootstrap.</summary>
    public sealed class MultiplayerSessionCoordinator : MonoBehaviour
    {
        private const int MatchEndSessionTimeoutMilliseconds = 8000;
        private CancellationTokenSource operation;
        private IOnlineSessionService sessions;
        private UnityServicesBootstrap services;
        private OnlineServicesSettings settings;
        private bool operationRunning;
        private bool heroSelectionTickRunning;
        private bool arenaHandoffStarted;
        private float nextHeroSelectionTick;
        public static MultiplayerSessionCoordinator Active { get; private set; }
        public OnlineState State { get; private set; } = OnlineState.Offline;
        public OnlineErrorCode Error { get; private set; }
        public string Status { get; private set; } = "Modo offline disponible.";
        public bool IsBusy => operationRunning;
        public IPlayerIdentityService Identity => services?.Identity;
        public IOnlineSessionService Sessions => sessions;
        public event Action<MultiplayerSessionCoordinator> Changed;

        public static MultiplayerSessionCoordinator Ensure()
        {
            if (Active != null) return Active;
            GameObject root = new GameObject("Multiplayer Session Coordinator");
            return root.AddComponent<MultiplayerSessionCoordinator>();
        }
        private void Awake()
        {
            if (Active != null && Active != this) { Destroy(gameObject); return; }
            Active = this; DontDestroyOnLoad(gameObject);
            settings = OnlineServicesSettings.RuntimeDefault;
            services = UnityServicesBootstrap.Ensure(settings);
            services.Changed += OnServicesChanged;
            sessions = new UnityMultiplayerSessionService(services, settings);
            sessions.Changed += OnSessionChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private void OnDestroy()
        {
            operation?.Cancel(); operation?.Dispose();
            if (services != null) services.Changed -= OnServicesChanged;
            if (sessions != null) sessions.Changed -= OnSessionChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Active == this) Active = null;
        }
        private void OnServicesChanged(UnityServicesBootstrap bootstrap)
        {
            if (State == OnlineState.CreatingSession || State == OnlineState.JoiningSession) return;
            State = bootstrap.State; Error = bootstrap.Error;
            Status = State == OnlineState.ConfigurationRequired ? OnlineErrorCode.ConfigurationRequired.ToPlayerText() : State == OnlineState.OnlineMenu ? "Conectado como invitado." : "Conectando con servicios...";
            Notify();
        }
        private void OnSessionChanged()
        {
            if (sessions == null) return;
            if (!sessions.IsInSession)
            {
                // A deliberate LeaveAsync owns its own terminal state. Any other
                // removal/deletion is a host/session loss and must not leave a stale room UI.
                if (State != OnlineState.LeavingSession && State != OnlineState.OnlineMenu && State != OnlineState.ConfigurationRequired)
                    NotifyHostLost();
                return;
            }
            if (sessions.HeroSelection.IsActive)
            {
                State = OnlineState.HeroSelect;
                Status = "Selección de héroes en curso.";
                PresentHeroSelection();
            }
            else if (sessions.HeroSelection.IsLoadingMatch || (sessions.HeroSelection.Phase == SessionPhase.InMatch && !arenaHandoffStarted && State != OnlineState.InMatch))
            {
                State = OnlineState.LoadingMatch;
                Status = "Todos los héroes están listos. Cargando arena...";
                StartArenaFromCommittedHero();
            }
            else if (sessions.HeroSelection.Phase == SessionPhase.MatchEnded)
            {
                State = OnlineState.MatchEnded;
                Status = "La partida ha terminado.";
            }
            Notify();
        }
        private void Update()
        {
            if (sessions == null || !sessions.IsLocalHost || !sessions.HeroSelection.IsActive || heroSelectionTickRunning || Time.unscaledTime < nextHeroSelectionTick) return;
            nextHeroSelectionTick = Time.unscaledTime + .25f;
            _ = TickHeroSelectionAsync();
        }
        private async Task TickHeroSelectionAsync()
        {
            heroSelectionTickRunning = true;
            try
            {
                OnlineErrorCode result = await sessions.UpdateHeroSelectionAsync(DateTimeOffset.UtcNow, CancellationToken.None);
                if (result != OnlineErrorCode.None) Fail(result);
            }
            finally { heroSelectionTickRunning = false; }
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MobaGreyboxArena" && State == OnlineState.LoadingMatch)
            {
                State = OnlineState.InMatch;
                Status = "Partida en curso.";
                if (sessions?.IsLocalHost == true) _ = sessions.SetSessionDataAsync("phase", "in-match", CancellationToken.None);
                Notify();
            }
            if (scene.name == "MainMenu" && sessions != null && sessions.IsInSession && State == OnlineState.InRoom)
                StartCoroutine(PresentRetainedRoom());
        }
        private IEnumerator PresentRetainedRoom()
        {
            yield return null;
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) yield break;
            if (sessions.HeroSelection.IsActive) PresentHeroSelection();
            else MultiplayerRoomPanel.Show(canvas, HeroCatalog.Shared.ResolveOrFallback(FrontendLaunchRequest.HeroId), TeamId.Azure);
        }
        public async Task OpenMultiplayerAsync()
        {
            if (!TryBegin(out CancellationToken token)) return;
            try { Status = "Conectando con servicios..."; State = OnlineState.InitializingServices; Notify(); await services.InitializeAndSignInAsync(); OnServicesChanged(services); }
            finally { EndOperation(); }
        }
        public async Task RetryAsync() { if (!TryBegin(out _)) return; try { await services.RetryAsync(); OnServicesChanged(services); } finally { EndOperation(); } }
        public bool TrySetDisplayName(string name, out string message)
        {
            if (Identity == null) { message = "La identidad aún no está preparada."; return false; }
            bool result = Identity.TrySetDisplayName(name, out message); Notify(); return result;
        }
        public async Task CreatePrivateRoomAsync()
        {
            if (!TryBegin(out CancellationToken token)) return;
            try
            {
                State = OnlineState.CreatingSession; Status = "Creando sala privada..."; Notify();
                OnlineErrorCode result = await sessions.CreatePrivateSessionAsync(CreateLocalPlayer(TeamId.Azure), token);
                CompleteRoomOperation(result, "Sala privada creada.");
            }
            finally { EndOperation(); }
        }
        public async Task JoinByCodeAsync(string code)
        {
            if (!TryBegin(out CancellationToken token)) return;
            try
            {
                string normalized = NormalizeJoinCode(code);
                if (!IsValidJoinCode(normalized)) { CompleteRoomOperation(OnlineErrorCode.CodeInvalid, string.Empty); return; }
                State = OnlineState.JoiningSession; Status = "Uniéndose a la sala..."; Notify();
                OnlineErrorCode result = await sessions.JoinByCodeAsync(normalized, CreateLocalPlayer(TeamId.Ember), token);
                CompleteRoomOperation(result, "Te has unido a la sala.");
            }
            finally { EndOperation(); }
        }
        public async Task ChangeTeamAsync(TeamId team)
        {
            if (sessions?.Roster == null || Identity == null) return;
            if (!sessions.Roster.TryChangeTeam(sessions.LocalPlayerId, team, settings, out OnlineErrorCode error)) { Fail(error); return; }
            await sessions.SetPlayerDataAsync(sessions.Roster.Find(sessions.LocalPlayerId), CancellationToken.None); State = OnlineState.InRoom; Notify();
        }
        public async Task ToggleReadyAsync()
        {
            if (sessions?.Roster == null || Identity == null) return;
            MatchPlayerSlot player = sessions.Roster.Find(sessions.LocalPlayerId); if (player == null) return;
            if (!sessions.Roster.TrySetReady(sessions.LocalPlayerId, sessions.LocalPlayerId, !player.IsReady, out OnlineErrorCode error)) { Fail(error); return; }
            await sessions.SetPlayerDataAsync(player, CancellationToken.None); Notify();
        }
        public async Task StartGameAsync()
        {
            if (Identity == null || sessions == null) return;
            if (!sessions.Roster.CanStart(sessions.LocalPlayerId, settings, out OnlineErrorCode error)) { Fail(error); return; }
            if (!TryBegin(out CancellationToken token)) return;
            try
            {
                State = OnlineState.StartingHeroSelect; Status = "Preparando selección de héroe..."; Notify();
                OnlineErrorCode result = await sessions.StartGameAsync(token);
                if (result != OnlineErrorCode.None) { Fail(result); return; }
                State = OnlineState.HeroSelect; Status = "Selecciona tu héroe."; Notify();
                PresentHeroSelection();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                Fail(OnlineErrorCode.Unknown);
            }
            finally { EndOperation(); }
        }
        public async Task SubmitHeroSelectionAsync(string heroId)
        {
            if (sessions == null) return;
            OnlineErrorCode save = await sessions.SubmitHeroSelectionIntentAsync(heroId, CancellationToken.None);
            if (save != OnlineErrorCode.None) Fail(save);
        }
        private void StartArenaFromCommittedHero()
        {
            if (arenaHandoffStarted || Identity == null || sessions?.Roster == null) return;
            MatchPlayerSlot player = sessions.Roster.Find(sessions.LocalPlayerId);
            if (player == null || (player.HeroPickState != HeroPickState.Locked && player.HeroPickState != HeroPickState.AutoPicked)) return;
            arenaHandoffStarted = true;
            TeamId assignedTeam = player.Team;
            State = OnlineState.LoadingMatch; Status = "Cargando arena..."; Notify();
            // Relay allocation is carried by DeferredRelayNetworkHandler. The arena
            // consumes it and starts its existing NGO NetworkManager; no IP/port is
            // exposed in this handoff.
            FrontendLaunchRequest.Set(sessions.IsLocalHost ? FrontendMatchMode.RelayHost : FrontendMatchMode.RelayClient, assignedTeam, "relay", 0, player.HeroId);
            SceneManager.LoadScene("MobaGreyboxArena");
        }
        private void PresentHeroSelection()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) HeroSelectionPanel.Show(canvas, this);
        }
        public async Task LeaveAsync()
        {
            if (!TryBegin(out CancellationToken token)) return;
            try
            {
                State = OnlineState.LeavingSession; Status = "Abandonando sala..."; Notify();
                if (Identity != null && sessions != null) await sessions.LeaveAsync(Identity.PlayerId, token);
                State = services != null && services.IsCloudProjectLinked ? OnlineState.OnlineMenu : OnlineState.ConfigurationRequired;
                Error = State == OnlineState.ConfigurationRequired ? OnlineErrorCode.ConfigurationRequired : OnlineErrorCode.None;
                Status = State == OnlineState.ConfigurationRequired ? Error.ToPlayerText() : "Listo para crear o unirse a una sala."; Notify();
            }
            finally { EndOperation(); }
        }
        public async Task CloseRoomAsync()
        {
            if (!TryBegin(out CancellationToken token)) return;
            try
            {
                if (sessions?.Roster == null || Identity == null || !sessions.IsLocalHost)
                {
                    Fail(OnlineErrorCode.PermissionDenied);
                    return;
                }
                State = OnlineState.LeavingSession; Status = "Cerrando sala..."; Notify();
                OnlineErrorCode result = await sessions.CloseAsync(token);
                if (result != OnlineErrorCode.None) { Fail(result); return; }
                State = OnlineState.OnlineMenu; Error = OnlineErrorCode.None; Status = "Sala cerrada."; Notify();
            }
            finally { EndOperation(); }
        }
        public async Task ReturnToRoomAfterMatchAsync()
        {
            if (sessions == null || !sessions.IsInSession) return;
            State = OnlineState.ReturningToRoom; Status = "Volviendo a la sala privada..."; Notify();
            // Session ownership, rather than the transient NGO transport role, is
            // the only authority allowed to reopen the room. This prevents a client
            // from being shown host controls during a match-end teardown race.
            if (sessions.IsLocalHost)
            {
                OnlineErrorCode result = await EndGameWithTimeoutAsync();
                if (result != OnlineErrorCode.None)
                {
                    // Keep the already-existing private room available even if the
                    // remote unlock request has a transient failure.  Sending the
                    // host to a bare main menu here stranded the rest of the party.
                    State = OnlineState.InRoom;
                    Error = result;
                    Status = "La partida terminó; la sala sigue disponible. No se pudo reabrir aún.";
                    Notify();
                    return;
                }
            }
            State = OnlineState.InRoom; Error = OnlineErrorCode.None; Status = "Sala lista para otra partida."; Notify();
        }

        private async Task<OnlineErrorCode> EndGameWithTimeoutAsync()
        {
            using CancellationTokenSource timeout = new CancellationTokenSource(MatchEndSessionTimeoutMilliseconds);
            Task<OnlineErrorCode> endGameTask = sessions.EndGameAsync(timeout.Token);
            Task completed = await Task.WhenAny(endGameTask, Task.Delay(Timeout.Infinite, timeout.Token));
            if (completed != endGameTask)
            {
                Debug.LogWarning("[M24 MatchEnd] Timed out reopening the private room; returning to its local session state.", this);
                return OnlineErrorCode.Timeout;
            }

            try { return await endGameTask; }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (Exception exception)
            {
                Debug.LogWarning($"[M24 MatchEnd] Could not reopen the private room: {exception.Message}", this);
                return OnlineErrorCode.Unknown;
            }
        }
        public void NotifyHostLost() { State = OnlineState.Disconnected; Error = OnlineErrorCode.HostDisconnected; Status = Error.ToPlayerText(); Notify(); }
        public static string NormalizeJoinCode(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace(" ", string.Empty).Trim().ToUpperInvariant();
        public static bool IsValidJoinCode(string value)
        {
            if (value.Length < 4 || value.Length > 12) return false;
            for (int i = 0; i < value.Length; i++) if (!(char.IsLetterOrDigit(value[i]))) return false;
            return true;
        }
        private MatchPlayerSlot CreateLocalPlayer(TeamId desiredTeam) => new()
        {
            PlayerId = Identity?.PlayerId ?? string.Empty, DisplayName = Identity?.DisplayName ?? "Jugador",
            Team = desiredTeam, IsConnected = true, IsHost = !sessions.IsInSession,
            HeroId = HeroCatalog.Shared.DefaultHero?.HeroId ?? "storm_warden", BuildVersion = settings.BuildVersion,
            ProtocolVersion = settings.ProtocolVersion
        };
        private bool TryBegin(out CancellationToken token)
        {
            token = default;
            if (operationRunning) { Fail(OnlineErrorCode.OperationInProgress); return false; }
            operationRunning = true; operation?.Cancel(); operation?.Dispose(); operation = new CancellationTokenSource(); token = operation.Token; return true;
        }
        private void EndOperation() { operationRunning = false; Notify(); }
        private void CompleteRoomOperation(OnlineErrorCode result, string success)
        { if (result != OnlineErrorCode.None) { Fail(result); return; } arenaHandoffStarted = false; State = OnlineState.InRoom; Error = OnlineErrorCode.None; Status = success; Notify(); }
        private void Fail(OnlineErrorCode error) { Error = error; State = error == OnlineErrorCode.ConfigurationRequired ? OnlineState.ConfigurationRequired : OnlineState.Error; Status = error.ToPlayerText(); Notify(); }
        private void Notify() => Changed?.Invoke(this);
    }
}

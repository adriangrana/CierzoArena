using System;

namespace CierzoArena.Core
{
    /// <summary>
    /// Explicit, scene-independent state for navigating over an active match.  It is
    /// deliberately transport-agnostic: UI and gameplay input can make decisions
    /// without guessing from a scene name, HUD object, or NetworkManager state.
    /// </summary>
    public static class MatchNavigationState
    {
        public static bool IsMatchActive { get; private set; }
        public static bool IsInGameplayView { get; private set; }
        public static bool IsMainMenuVisible { get; private set; }
        public static bool IsOnline { get; private set; }
        public static bool IsLocalMatch => IsMatchActive && !IsOnline;
        public static bool IsHost { get; private set; }
        public static bool IsClient { get; private set; }
        public static bool IsDisconnecting { get; private set; }
        public static bool IsMatchFinished { get; private set; }
        public static bool CanReturnToMatch => IsMatchActive && IsMainMenuVisible && !IsDisconnecting && !IsMatchFinished;
        public static bool IsGameplayInputAllowed => IsMatchActive && IsInGameplayView && !IsMainMenuVisible && !IsDisconnecting && !IsMatchFinished;
        public static string CurrentPhase { get; private set; } = "Sin partida";
        public static string CurrentMatchPhase => CurrentPhase;

        public static event Action Changed;
        public static event Action DisconnectRequested;

        public static void BeginMatch(bool online, bool host, bool client)
        {
            IsMatchActive = true;
            IsInGameplayView = true;
            IsMainMenuVisible = false;
            IsOnline = online;
            IsHost = host;
            IsClient = client;
            IsDisconnecting = false;
            IsMatchFinished = false;
            CurrentPhase = "Partida en curso";
            Notify();
        }

        public static void OpenMainMenu()
        {
            if (!IsMatchActive || IsDisconnecting) return;
            IsMainMenuVisible = true;
            IsInGameplayView = false;
            CurrentPhase = IsMatchFinished ? "Partida finalizada" : "Menú sobre partida activa";
            Notify();
        }

        public static void ReturnToMatch()
        {
            if (!CanReturnToMatch) return;
            IsMainMenuVisible = false;
            IsInGameplayView = true;
            CurrentPhase = "Partida en curso";
            Notify();
        }

        public static void MarkMatchFinished()
        {
            if (!IsMatchActive) return;
            IsMatchFinished = true;
            IsInGameplayView = false;
            CurrentPhase = "Partida finalizada";
            Notify();
        }

        public static void RequestDisconnect()
        {
            if (!IsMatchActive || IsDisconnecting) return;
            IsDisconnecting = true;
            IsMainMenuVisible = true;
            IsInGameplayView = false;
            CurrentPhase = "Saliendo de la partida";
            Notify();
            DisconnectRequested?.Invoke();
        }

        public static void CompleteExit()
        {
            IsMatchActive = false;
            IsInGameplayView = false;
            IsMainMenuVisible = false;
            IsOnline = false;
            IsHost = false;
            IsClient = false;
            IsDisconnecting = false;
            IsMatchFinished = false;
            CurrentPhase = "Sin partida";
            Notify();
        }

        private static void Notify() => Changed?.Invoke();
    }
}

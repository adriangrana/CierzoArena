namespace CierzoArena.Online
{
    public enum OnlineState
    {
        Offline, ConfigurationRequired, InitializingServices, Authenticating,
        OnlineMenu, CreatingSession, JoiningSession, ConfiguringRelay,
        ConnectingHost, ConnectingClient, InRoom, StartingHeroSelect,
        HeroSelect, LoadingMatch, InMatch, MatchEnded, ReturningToRoom, LeavingSession,
        Disconnected, Error
    }

    public enum PlayerIdentityState
    {
        Uninitialized, Initializing, Offline, SigningIn, AuthenticatedGuest,
        AuthenticatedPlatform, AuthenticationFailed, SigningOut
    }

    public enum OnlineErrorCode
    {
        None, ConfigurationRequired, Offline, AuthenticationFailed, CodeEmpty,
        CodeInvalid, SessionNotFound, SessionFull, SessionClosed, MatchStarted,
        VersionMismatch, Timeout, RelayUnavailable, HostDisconnected,
        AlreadyJoined, OperationInProgress, PermissionDenied, Unknown
    }

    public static class OnlineErrorText
    {
        public static string ToPlayerText(this OnlineErrorCode code) => code switch
        {
            OnlineErrorCode.ConfigurationRequired => "El proyecto online necesita configuración.",
            OnlineErrorCode.Offline => "No se pudo conectar con los servicios. El modo local sigue disponible.",
            OnlineErrorCode.AuthenticationFailed => "No se pudo autenticar la cuenta de invitado.",
            OnlineErrorCode.CodeEmpty => "Introduce un código de sala.",
            OnlineErrorCode.CodeInvalid => "Código de sala inválido.",
            OnlineErrorCode.SessionNotFound => "La sala no existe.",
            OnlineErrorCode.SessionFull => "La sala está llena.",
            OnlineErrorCode.SessionClosed => "La sala está cerrada.",
            OnlineErrorCode.MatchStarted => "La partida ya comenzó.",
            OnlineErrorCode.VersionMismatch => "Versión incompatible. Todos deben usar la misma build de CierzoArena.",
            OnlineErrorCode.Timeout => "La conexión agotó el tiempo de espera.",
            OnlineErrorCode.RelayUnavailable => "Relay no está disponible ahora mismo.",
            OnlineErrorCode.HostDisconnected => "El anfitrión se ha desconectado. La sala se ha cerrado.",
            OnlineErrorCode.AlreadyJoined => "Este jugador ya está unido a la sala.",
            OnlineErrorCode.OperationInProgress => "Hay una operación de red en curso.",
            OnlineErrorCode.PermissionDenied => "Esta acción solo la puede realizar el anfitrión.",
            _ => "No se pudo completar la operación online."
        };
    }
}

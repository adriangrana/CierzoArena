using UnityEngine;

namespace CierzoArena.Online
{
    /// <summary>Single source of tunable M24 online policy. It deliberately contains
    /// no credentials: Unity Services obtains authentication and Relay data at runtime.</summary>
    [CreateAssetMenu(menuName = "Cierzo Arena/Online Services Settings", fileName = "OnlineServicesSettings")]
    public sealed class OnlineServicesSettings : ScriptableObject
    {
        [SerializeField] private string environmentName = "production";
        [SerializeField, Range(2, 10)] private int maxPlayers = 10;
        [SerializeField, Range(1, 5)] private int maxPlayersPerTeam = 5;
        [SerializeField, Range(1, 10)] private int minimumPlayersToStart = 2;
        [SerializeField, Min(3)] private int sessionOperationTimeoutSeconds = 20;
        [SerializeField, Min(3)] private int authenticationTimeoutSeconds = 15;
        [SerializeField] private string buildVersion = "1.0";
        [SerializeField, Min(1)] private int protocolVersion = 24;
        [SerializeField] private bool allowDirectDevelopmentNetworking = true;
        [SerializeField] private bool enableVerboseDevelopmentLogs = true;
        [SerializeField] private bool returnToRoomAfterMatch = true;
        [SerializeField] private bool allowSoloDevelopmentStart = true;
        [SerializeField] private bool allowTwoPlayerDevelopmentStart = true;
        [Header("Hero selection")]
        [SerializeField, Range(5, 60)] private int heroSelectionTurnSeconds = 25;

        public string EnvironmentName => string.IsNullOrWhiteSpace(environmentName) ? "production" : environmentName.Trim();
        public int MaxPlayers => Mathf.Clamp(maxPlayers, 2, 10);
        public int MaxPlayersPerTeam => Mathf.Clamp(maxPlayersPerTeam, 1, 5);
        public int MinimumPlayersToStart => Mathf.Clamp(minimumPlayersToStart, 1, MaxPlayers);
        public int SessionOperationTimeoutSeconds => Mathf.Max(3, sessionOperationTimeoutSeconds);
        public int AuthenticationTimeoutSeconds => Mathf.Max(3, authenticationTimeoutSeconds);
        public string BuildVersion => string.IsNullOrWhiteSpace(buildVersion) ? "1.0" : buildVersion.Trim();
        public int ProtocolVersion => Mathf.Max(1, protocolVersion);
        public bool AllowDirectDevelopmentNetworking => allowDirectDevelopmentNetworking;
        public bool EnableVerboseDevelopmentLogs => enableVerboseDevelopmentLogs;
        public bool ReturnToRoomAfterMatch => returnToRoomAfterMatch;
        public bool AllowSoloDevelopmentStart => allowSoloDevelopmentStart;
        public bool AllowTwoPlayerDevelopmentStart => allowTwoPlayerDevelopmentStart;
        public int HeroSelectionTurnSeconds => Mathf.Clamp(heroSelectionTurnSeconds, 5, 60);

        public static OnlineServicesSettings RuntimeDefault
        {
            get
            {
                OnlineServicesSettings found = Resources.Load<OnlineServicesSettings>("Online/OnlineServicesSettings");
                if (found != null) return found;
                OnlineServicesSettings transient = CreateInstance<OnlineServicesSettings>();
                transient.hideFlags = HideFlags.DontSave;
                return transient;
            }
        }
    }
}

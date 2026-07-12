using CierzoArena.Core;

namespace CierzoArena.Frontend
{
    public enum FrontendMatchMode { LocalDevelopment, Host, Client }

    /// <summary>One-shot handoff from MainMenu to the arena. It deliberately contains
    /// only connection intent, never gameplay state or authority.</summary>
    public static class FrontendLaunchRequest
    {
        private static bool pending;
        public static FrontendMatchMode Mode { get; private set; }
        public static TeamId Team { get; private set; }
        public static string Address { get; private set; } = "127.0.0.1";
        public static ushort Port { get; private set; } = 7777;
        public static void Set(FrontendMatchMode mode, TeamId team, string address, ushort port)
        {
            Mode=mode;Team=team;Address=string.IsNullOrWhiteSpace(address)?"127.0.0.1":address.Trim();Port=port==0?(ushort)7777:port;pending=true;
        }
        public static bool TryConsume(out FrontendMatchMode mode,out TeamId team,out string address,out ushort port)
        {
            mode=Mode;team=Team;address=Address;port=Port;
            if(!pending)return false;
            pending=false;return true;
        }
    }
}

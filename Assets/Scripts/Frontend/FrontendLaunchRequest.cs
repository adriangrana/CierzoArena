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
        public static string HeroId { get; private set; } = "storm_warden";
        public static void Set(FrontendMatchMode mode, TeamId team, string address, ushort port, string heroId = null)
        {
            Mode=mode;Team=team;Address=string.IsNullOrWhiteSpace(address)?"127.0.0.1":address.Trim();Port=port==0?(ushort)7777:port;HeroId=string.IsNullOrWhiteSpace(heroId)?HeroId:heroId.Trim();pending=true;
        }
        public static bool TryConsume(out FrontendMatchMode mode,out TeamId team,out string address,out ushort port)
            => TryConsume(out mode,out team,out address,out port,out _);
        public static bool TryConsume(out FrontendMatchMode mode,out TeamId team,out string address,out ushort port,out string heroId)
        {
            mode=Mode;team=Team;address=Address;port=Port;heroId=HeroId;
            if(!pending)return false;
            pending=false;return true;
        }
    }
}

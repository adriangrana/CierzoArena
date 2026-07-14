using System;

namespace CierzoArena.Online.Room
{
    /// <summary>Replicated lifecycle of the pre-match draft. The host is the only
    /// writer of Locked/AutoPicked states and of the active turn.</summary>
    public enum HeroPickState { None, Intent, Locked, AutoPicked }

    public enum SessionPhase { Lobby, HeroSelection, LoadingMatch, InMatch, MatchEnded }

    [Serializable]
    public sealed class HeroSelectionSnapshot
    {
        public SessionPhase Phase = SessionPhase.Lobby;
        public int TurnIndex = -1;
        public long TurnDeadlineUnixMilliseconds;
        public int TurnDurationSeconds;

        public bool IsActive => Phase == SessionPhase.HeroSelection;
        public bool IsLoadingMatch => Phase == SessionPhase.LoadingMatch;
        public bool IsDeadlineReached(DateTimeOffset now) => TurnDeadlineUnixMilliseconds > 0 && now.ToUnixTimeMilliseconds() >= TurnDeadlineUnixMilliseconds;
        public float SecondsRemaining(DateTimeOffset now) => TurnDeadlineUnixMilliseconds <= 0 ? 0f : Math.Max(0f, (TurnDeadlineUnixMilliseconds - now.ToUnixTimeMilliseconds()) / 1000f);
    }
}

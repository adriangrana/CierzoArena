using System;
using System.Collections.Generic;
using CierzoArena.Core;

namespace CierzoArena.Online.Room
{
    [Serializable]
    public sealed class MatchPlayerSlot
    {
        public string PlayerId;
        public ulong ClientId;
        public string DisplayName;
        public TeamId Team;
        public int StableSlot;
        public bool IsConnected;
        public bool IsReady;
        public bool IsHost;
        public string HeroId;
        public string HeroIntentId;
        public HeroPickState HeroPickState;
        public int HeroPickOrder = -1;
        public int JoinOrder;
        public string BuildVersion;
        public int ProtocolVersion;

        public MatchPlayerSlot Clone() => (MatchPlayerSlot)MemberwiseClone();
    }

    [Serializable]
    public sealed class MatchRoster
    {
        private readonly List<MatchPlayerSlot> players = new();
        public IReadOnlyList<MatchPlayerSlot> Players => players;
        public string HostPlayerId { get; private set; }
        public bool IsJoinable { get; private set; } = true;
        public event Action Changed;

        public void ConfigureHost(string playerId) { HostPlayerId = playerId; }
        public void SetJoinable(bool value) { IsJoinable = value; Changed?.Invoke(); }
        /// <summary>Rebuilds the local projection from the authoritative session
        /// membership. The session service is the source of truth; this class only
        /// enforces presentation and deterministic slot allocation.</summary>
        public void Replace(IEnumerable<MatchPlayerSlot> source, string hostPlayerId, bool joinable)
        {
            players.Clear();
            if (source != null)
            {
                foreach (MatchPlayerSlot item in source)
                    if (item != null && !string.IsNullOrWhiteSpace(item.PlayerId)) players.Add(item);
            }
            HostPlayerId = hostPlayerId ?? string.Empty;
            IsJoinable = joinable;
            Changed?.Invoke();
        }
        public bool TryAdd(MatchPlayerSlot player, OnlineServicesSettings settings, out OnlineErrorCode error)
        {
            error = OnlineErrorCode.None;
            if (player == null || string.IsNullOrWhiteSpace(player.PlayerId)) { error = OnlineErrorCode.Unknown; return false; }
            if (Find(player.PlayerId) != null) { error = OnlineErrorCode.AlreadyJoined; return false; }
            if (!IsJoinable) { error = OnlineErrorCode.SessionClosed; return false; }
            if (players.Count >= settings.MaxPlayers) { error = OnlineErrorCode.SessionFull; return false; }
            if (!TryAssign(player, player.Team, settings, out error)) return false;
            player.JoinOrder = players.Count;
            players.Add(player); Changed?.Invoke(); return true;
        }
        public bool TryAssign(MatchPlayerSlot player, TeamId target, OnlineServicesSettings settings, out OnlineErrorCode error)
        {
            error = OnlineErrorCode.None;
            if (player == null || (target != TeamId.Azure && target != TeamId.Ember)) { error = OnlineErrorCode.Unknown; return false; }
            int capacity = settings.MaxPlayersPerTeam;
            int used = 0;
            foreach (MatchPlayerSlot item in players) if (!ReferenceEquals(item, player) && item.Team == target) used++;
            if (used >= capacity) { error = OnlineErrorCode.SessionFull; return false; }
            player.Team = target; player.StableSlot = FirstAvailableSlot(target, player, capacity); player.IsReady = false; return true;
        }
        public bool TryChangeTeam(string playerId, TeamId target, OnlineServicesSettings settings, out OnlineErrorCode error)
        {
            MatchPlayerSlot player = Find(playerId); if (player == null) { error = OnlineErrorCode.Unknown; return false; }
            if (!IsJoinable) { error = OnlineErrorCode.MatchStarted; return false; }
            bool changed = TryAssign(player, target, settings, out error); if (changed) Changed?.Invoke(); return changed;
        }
        public bool TrySetReady(string actorId, string playerId, bool ready, out OnlineErrorCode error)
        {
            error = OnlineErrorCode.None;
            if (!string.Equals(actorId, playerId, StringComparison.Ordinal)) { error = OnlineErrorCode.PermissionDenied; return false; }
            MatchPlayerSlot player = Find(playerId); if (player == null || !IsJoinable) { error = OnlineErrorCode.MatchStarted; return false; }
            player.IsReady = ready; Changed?.Invoke(); return true;
        }
        public bool CanStart(string actorId, OnlineServicesSettings settings, out OnlineErrorCode error)
        {
            error = OnlineErrorCode.None;
            if (!string.Equals(actorId, HostPlayerId, StringComparison.Ordinal)) { error = OnlineErrorCode.PermissionDenied; return false; }
            if (players.Count < settings.MinimumPlayersToStart && !(settings.AllowSoloDevelopmentStart && players.Count == 1)) { error = OnlineErrorCode.Unknown; return false; }
            foreach (MatchPlayerSlot player in players)
            {
                if (player.Team != TeamId.Azure && player.Team != TeamId.Ember) { error = OnlineErrorCode.Unknown; return false; }
                if (!player.IsReady) { error = OnlineErrorCode.Unknown; return false; }
                if (!string.Equals(player.BuildVersion, settings.BuildVersion, StringComparison.Ordinal) || player.ProtocolVersion != settings.ProtocolVersion) { error = OnlineErrorCode.VersionMismatch; return false; }
            }
            return true;
        }
        public bool Remove(string playerId) { MatchPlayerSlot player = Find(playerId); if (player == null) return false; players.Remove(player); Changed?.Invoke(); return true; }
        public MatchPlayerSlot Find(string playerId) => players.Find(value => string.Equals(value.PlayerId, playerId, StringComparison.Ordinal));
        public List<MatchPlayerSlot> Snapshot() => players.ConvertAll(value => value.Clone());
        private int FirstAvailableSlot(TeamId team, MatchPlayerSlot ignored, int max)
        {
            for (int i = 0; i < max; i++)
            {
                bool used = false;
                foreach (MatchPlayerSlot item in players) if (!ReferenceEquals(item, ignored) && item.Team == team && item.StableSlot == i) { used = true; break; }
                if (!used) return i;
            }
            return -1;
        }
    }
}

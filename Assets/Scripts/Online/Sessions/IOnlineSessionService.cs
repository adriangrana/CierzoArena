using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CierzoArena.Online.Room;
using Unity.Services.Multiplayer;

namespace CierzoArena.Online.Sessions
{
    public interface IOnlineSessionService
    {
        bool IsOnlineAvailable { get; }
        bool IsInSession { get; }
        bool IsLocalHost { get; }
        string LocalPlayerId { get; }
        string JoinCode { get; }
        MatchRoster Roster { get; }
        HeroSelectionSnapshot HeroSelection { get; }
        event Action Changed;
        Task<OnlineErrorCode> CreatePrivateSessionAsync(MatchPlayerSlot host, CancellationToken cancellationToken);
        Task<OnlineErrorCode> JoinByCodeAsync(string joinCode, MatchPlayerSlot player, CancellationToken cancellationToken);
        Task<OnlineErrorCode> LeaveAsync(string playerId, CancellationToken cancellationToken);
        Task<OnlineErrorCode> CloseAsync(CancellationToken cancellationToken);
        IReadOnlyList<MatchPlayerSlot> GetPlayers();
        void ObserveSessionChanges();
        Task<OnlineErrorCode> SetPlayerDataAsync(MatchPlayerSlot player, CancellationToken cancellationToken);
        Task<OnlineErrorCode> SetSessionDataAsync(string key, string value, CancellationToken cancellationToken);
        Task<OnlineErrorCode> SetJoinableAsync(bool joinable, CancellationToken cancellationToken);
        Task<OnlineErrorCode> StartGameAsync(CancellationToken cancellationToken);
        Task<OnlineErrorCode> SubmitHeroSelectionIntentAsync(string heroId, CancellationToken cancellationToken);
        Task<OnlineErrorCode> UpdateHeroSelectionAsync(DateTimeOffset now, CancellationToken cancellationToken);
        Task<OnlineErrorCode> EndGameAsync(CancellationToken cancellationToken);
        Task<OnlineErrorCode> JoinMatchmadeSessionAsync(CancellationToken cancellationToken);
        bool TryConsumeRelayConfiguration(out NetworkConfiguration configuration);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CierzoArena.Online.Identity
{
    public sealed class OfflinePlayerIdentityService : IPlayerIdentityService
    {
        private readonly string displayNameKey;
        private readonly string playerId;
        public PlayerIdentityState State { get; private set; } = PlayerIdentityState.Uninitialized;
        public string PlayerId => playerId;
        public string DisplayName { get; private set; }
        public bool IsAuthenticated => false;
        public bool IsOnline => false;
        public bool IsGuest => true;
        public OnlineErrorCode CurrentError { get; private set; } = OnlineErrorCode.None;
        public event Action<IPlayerIdentityService> StateChanged;

        public OfflinePlayerIdentityService(string profile)
        {
            string safeProfile = string.IsNullOrWhiteSpace(profile) ? "offline" : profile;
            playerId = "offline-" + safeProfile;
            displayNameKey = "CierzoArena.M24.DisplayName." + safeProfile;
            DisplayName = PlayerPrefs.GetString(displayNameKey, PlayerDisplayName.FallbackFor(playerId));
        }
        public Task InitializeAsync(CancellationToken cancellationToken) { State = PlayerIdentityState.Offline; Notify(); return Task.CompletedTask; }
        public Task SignInGuestAsync(CancellationToken cancellationToken) { State = PlayerIdentityState.Offline; Notify(); return Task.CompletedTask; }
        public Task SignOutAsync(CancellationToken cancellationToken) { State = PlayerIdentityState.Offline; Notify(); return Task.CompletedTask; }
        public Task RefreshAsync(CancellationToken cancellationToken) => InitializeAsync(cancellationToken);
        public bool TrySetDisplayName(string value, out string error)
        {
            if (!PlayerDisplayName.TryNormalize(value, out string normalized, out error)) return false;
            DisplayName = normalized; PlayerPrefs.SetString(displayNameKey, DisplayName); PlayerPrefs.Save(); Notify(); return true;
        }
        private void Notify() => StateChanged?.Invoke(this);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CierzoArena.Online.Identity
{
    public interface IPlayerIdentityService
    {
        PlayerIdentityState State { get; }
        string PlayerId { get; }
        string DisplayName { get; }
        bool IsAuthenticated { get; }
        bool IsOnline { get; }
        bool IsGuest { get; }
        OnlineErrorCode CurrentError { get; }
        event Action<IPlayerIdentityService> StateChanged;
        Task InitializeAsync(CancellationToken cancellationToken);
        Task SignInGuestAsync(CancellationToken cancellationToken);
        Task SignOutAsync(CancellationToken cancellationToken);
        Task RefreshAsync(CancellationToken cancellationToken);
        bool TrySetDisplayName(string value, out string error);
    }
}

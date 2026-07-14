using System;
using System.Threading;
using System.Threading.Tasks;
using CierzoArena.Online.Identity;
using UnityEngine;

namespace CierzoArena.Online
{
    /// <summary>Only owner of UGS initialization. It has no gameplay authority and
    /// remains alive solely to keep identity/session state across frontend scenes.</summary>
    public sealed class UnityServicesBootstrap : MonoBehaviour
    {
        private static UnityServicesBootstrap active;
        private Task initialization;
        private CancellationTokenSource lifetime;
        public static UnityServicesBootstrap Active => active;
        public OnlineServicesSettings Settings { get; private set; }
        public IPlayerIdentityService Identity { get; private set; }
        public bool IsCloudProjectLinked => !string.IsNullOrWhiteSpace(Application.cloudProjectId);
        public OnlineState State { get; private set; } = OnlineState.Offline;
        public OnlineErrorCode Error { get; private set; }
        public event Action<UnityServicesBootstrap> Changed;

        public static UnityServicesBootstrap Ensure(OnlineServicesSettings settings = null)
        {
            if (active != null) return active;
            GameObject root = new GameObject("Unity Services Bootstrap");
            return root.AddComponent<UnityServicesBootstrap>().Initialize(settings ?? OnlineServicesSettings.RuntimeDefault);
        }
        public UnityServicesBootstrap Initialize(OnlineServicesSettings settings)
        {
            if (Settings == null) Settings = settings ?? OnlineServicesSettings.RuntimeDefault;
            return this;
        }
        private void Awake()
        {
            if (active != null && active != this) { Destroy(gameObject); return; }
            active = this; DontDestroyOnLoad(gameObject); lifetime = new CancellationTokenSource();
            Settings ??= OnlineServicesSettings.RuntimeDefault;
        }
        private void OnDestroy()
        {
            if (active != this) return;
            lifetime?.Cancel(); lifetime?.Dispose(); active = null;
        }
        public Task InitializeAndSignInAsync() => initialization ??= InitializeInternalAsync(lifetime.Token);
        private async Task InitializeInternalAsync(CancellationToken token)
        {
            if (!IsCloudProjectLinked)
            {
                Identity = new OfflinePlayerIdentityService(DevelopmentProfile.Resolve());
                Identity.StateChanged += _ => Notify();
                await Identity.InitializeAsync(token);
                State = OnlineState.ConfigurationRequired; Error = OnlineErrorCode.ConfigurationRequired; Notify(); return;
            }
            State = OnlineState.InitializingServices; Notify();
            Identity = new UnityAnonymousIdentityService(DevelopmentProfile.Resolve(), Settings.EnvironmentName); Identity.StateChanged += _ => Notify();
            try
            {
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(Settings.AuthenticationTimeoutSeconds));
                await Identity.InitializeAsync(timeout.Token);
                State = OnlineState.Authenticating; Notify();
                await Identity.SignInGuestAsync(timeout.Token);
                State = OnlineState.OnlineMenu; Error = OnlineErrorCode.None; Notify();
            }
            catch (OperationCanceledException)
            {
                State = OnlineState.Error; Error = OnlineErrorCode.Timeout; Notify();
            }
            catch
            {
                State = OnlineState.Error; Error = OnlineErrorCode.AuthenticationFailed; Notify();
            }
        }
        public async Task RetryAsync()
        {
            initialization = null;
            await InitializeAndSignInAsync();
        }
        private void Notify() => Changed?.Invoke(this);
    }
}

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CierzoArena.Online.Identity
{
    /// <summary>Runtime-bound UGS adapter. Reflection deliberately keeps local and
    /// direct-development builds compilable while the Dashboard/package is absent;
    /// production uses the installed Multiplayer Services dependencies at runtime.</summary>
    public sealed class UnityAnonymousIdentityService : IPlayerIdentityService
    {
        private const string DisplayNameKey = "CierzoArena.M24.DisplayName";
        private readonly string profile;
        private readonly string environmentName;
        public PlayerIdentityState State { get; private set; } = PlayerIdentityState.Uninitialized;
        public string PlayerId { get; private set; } = string.Empty;
        public string DisplayName { get; private set; }
        public bool IsAuthenticated => State == PlayerIdentityState.AuthenticatedGuest || State == PlayerIdentityState.AuthenticatedPlatform;
        public bool IsOnline => IsAuthenticated;
        public bool IsGuest => State == PlayerIdentityState.AuthenticatedGuest;
        public OnlineErrorCode CurrentError { get; private set; }
        public event Action<IPlayerIdentityService> StateChanged;

        public UnityAnonymousIdentityService(string developmentProfile, string developmentEnvironment)
        {
            profile = developmentProfile;
            environmentName = string.IsNullOrWhiteSpace(developmentEnvironment) ? "development" : developmentEnvironment.Trim();
            DisplayName = PlayerPrefs.GetString(DisplayNameKey, string.Empty);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            State = PlayerIdentityState.Initializing; CurrentError = OnlineErrorCode.None; Notify();
            try
            {
                Type servicesType = FindType("Unity.Services.Core.UnityServices");
                if (servicesType == null) throw new InvalidOperationException("Multiplayer Services package is not installed.");
                object options = CreateInitializationOptions();
                MethodInfo initialize = options == null
                    ? servicesType.GetMethod("InitializeAsync", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)
                    : servicesType.GetMethod("InitializeAsync", BindingFlags.Public | BindingFlags.Static, null, new[] { options.GetType() }, null);
                if (initialize == null) throw new MissingMethodException("UnityServices.InitializeAsync");
                await AwaitTask(initialize.Invoke(null, options == null ? null : new[] { options }), cancellationToken);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[M24 Services] UGS initialization failed without exposing technical details to UI: " + exception.GetType().Name);
                State = PlayerIdentityState.AuthenticationFailed; CurrentError = OnlineErrorCode.ConfigurationRequired; Notify(); throw;
            }
        }

        public async Task SignInGuestAsync(CancellationToken cancellationToken)
        {
            State = PlayerIdentityState.SigningIn; CurrentError = OnlineErrorCode.None; Notify();
            try
            {
                Type serviceType = FindType("Unity.Services.Authentication.AuthenticationService");
                if (serviceType == null) throw new InvalidOperationException("Authentication is unavailable.");
                object service = serviceType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (service == null) throw new InvalidOperationException("Authentication instance is unavailable.");
                bool signedIn = (bool?)service.GetType().GetProperty("IsSignedIn")?.GetValue(service) ?? false;
                if (!signedIn)
                {
                    // SignInOptions is optional in C#, but reflection still sees one
                    // parameter. Support both package shapes without embedding a
                    // package version in this project assembly.
                    MethodInfo signIn = FindSingleOptionalArgumentMethod(service.GetType(), "SignInAnonymouslyAsync");
                    if (signIn == null) throw new MissingMethodException("SignInAnonymouslyAsync");
                    await AwaitTask(signIn.Invoke(service, signIn.GetParameters().Length == 0 ? null : new object[] { null }), cancellationToken);
                }
                PlayerId = service.GetType().GetProperty("PlayerId")?.GetValue(service) as string ?? string.Empty;
                if (string.IsNullOrWhiteSpace(PlayerId)) throw new InvalidOperationException("Authentication did not provide a PlayerId.");
                if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = PlayerDisplayName.FallbackFor(PlayerId);
                State = PlayerIdentityState.AuthenticatedGuest; Notify();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[M24 Identity] Anonymous sign-in failed: " + exception.GetType().Name);
                State = PlayerIdentityState.AuthenticationFailed; CurrentError = OnlineErrorCode.AuthenticationFailed; Notify(); throw;
            }
        }

        public Task RefreshAsync(CancellationToken cancellationToken) => IsAuthenticated ? Task.CompletedTask : SignInGuestAsync(cancellationToken);
        public Task SignOutAsync(CancellationToken cancellationToken)
        {
            State = PlayerIdentityState.SigningOut; Notify();
            Type serviceType = FindType("Unity.Services.Authentication.AuthenticationService");
            object service = serviceType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            service?.GetType().GetMethod("SignOut", Type.EmptyTypes)?.Invoke(service, null);
            PlayerId = string.Empty; State = PlayerIdentityState.Uninitialized; Notify(); return Task.CompletedTask;
        }
        public bool TrySetDisplayName(string value, out string error)
        {
            if (!PlayerDisplayName.TryNormalize(value, out string normalized, out error)) return false;
            DisplayName = normalized; PlayerPrefs.SetString(DisplayNameKey, DisplayName); PlayerPrefs.Save(); Notify(); return true;
        }
        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type result = assembly.GetType(name, false);
                if (result != null) return result;
            }
            return null;
        }
        private object CreateInitializationOptions()
        {
            Type optionsType = FindType("Unity.Services.Core.InitializationOptions");
            if (optionsType == null) return null;
            object options = Activator.CreateInstance(optionsType);
            InvokeFluent(options, "SetProfile", profile);
            InvokeFluent(options, "SetEnvironmentName", environmentName);
            return options;
        }
        private static void InvokeFluent(object target, string methodName, string value)
        {
            if (target == null || string.IsNullOrWhiteSpace(value)) return;
            MethodInfo direct = target.GetType().GetMethod(methodName, new[] { typeof(string) });
            if (direct != null) { direct.Invoke(target, new object[] { value }); return; }

            // SetProfile and SetEnvironmentName are extension methods in the UGS
            // packages, so they are not returned by target.GetType().GetMethod().
            // Resolve the public static extension once during initialization.
            Type targetType = target.GetType();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException exception) { types = exception.Types; }
                if (types == null) continue;
                foreach (Type type in types)
                {
                    if (type == null) continue;
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        ParameterInfo[] parameters = method.GetParameters();
                        if (method.Name == methodName && parameters.Length == 2 && parameters[0].ParameterType.IsAssignableFrom(targetType) && parameters[1].ParameterType == typeof(string))
                        {
                            method.Invoke(null, new[] { target, (object)value });
                            return;
                        }
                    }
                }
            }
        }
        private static MethodInfo FindSingleOptionalArgumentMethod(Type type, string name)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name != name) continue;
                int count = method.GetParameters().Length;
                if (count == 0 || count == 1) return method;
            }
            return null;
        }
        private static async Task AwaitTask(object value, CancellationToken token)
        {
            if (value is not Task task) throw new InvalidOperationException("UGS operation did not return Task.");
            Task canceled = Task.Delay(Timeout.Infinite, token);
            if (await Task.WhenAny(task, canceled) != task) token.ThrowIfCancellationRequested();
            await task;
        }
        private void Notify() => StateChanged?.Invoke(this);
    }
}

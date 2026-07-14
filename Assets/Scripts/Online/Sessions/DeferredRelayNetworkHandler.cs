using System.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace CierzoArena.Online.Sessions
{
    /// <summary>
    /// Receives the Relay allocation from Multiplayer Services while players are in
    /// the room. NGO is deliberately not started here: the arena owns the
    /// NetworkManager and consumes this configuration only after its scene loads.
    /// </summary>
    public sealed class DeferredRelayNetworkHandler : INetworkHandler
    {
        private NetworkConfiguration configuration;
        public bool HasConfiguration => configuration != null;

        public Task StartAsync(NetworkConfiguration networkConfiguration)
        {
            configuration = networkConfiguration;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            configuration = null;
            return Task.CompletedTask;
        }

        public bool TryConsume(out NetworkConfiguration networkConfiguration)
        {
            networkConfiguration = configuration;
            if (networkConfiguration == null) return false;
            // Keep the allocation while the room exists so a rematch can restart
            // NGO without exposing an endpoint or creating another join code.
            return true;
        }
    }
}

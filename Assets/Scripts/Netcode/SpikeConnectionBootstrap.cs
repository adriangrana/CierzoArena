using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>
    /// Minimal connection driver for the two-instance spike. Provides on-screen
    /// Host/Server/Client buttons and, on the authority, spawns the network units at
    /// runtime from registered prefabs.
    ///
    /// Authority model:
    /// - The server/host controls a single Azure unit, spawned once when the server
    ///   starts, owned by <see cref="NetworkManager.ServerClientId"/>.
    /// - Each remote client that connects receives its own Ember unit, spawned and
    ///   owned atomically via <see cref="NetworkObject.SpawnWithOwnership"/> so that
    ///   ownership is never touched before the object is spawned.
    /// - Clients never instantiate or spawn units; they only observe what the server
    ///   spawns and replicates.
    ///
    /// Nothing here is an in-scene NetworkObject, which avoids the duplicated
    /// GlobalObjectIdHash / ScenePlacedObjects registration problem entirely.
    /// It is deliberately tiny and throwaway: no lobby, no relay, no matchmaking.
    /// </summary>
    public sealed class SpikeConnectionBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkObject azurePrefab;
        [SerializeField] private NetworkObject emberPrefab;
        [SerializeField] private Vector3 azureSpawnPosition = new Vector3(-4f, 1f, -2f);
        [SerializeField] private Vector3 emberSpawnPosition = new Vector3(4f, 1f, 1f);

        private NetworkObject azureInstance;
        private readonly Dictionary<ulong, NetworkObject> clientUnits = new Dictionary<ulong, NetworkObject>();

        private void Start()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            manager.OnServerStarted += OnServerStarted;
            manager.OnServerStopped += OnServerStopped;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDestroy()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            manager.OnServerStarted -= OnServerStarted;
            manager.OnServerStopped -= OnServerStopped;
            manager.OnClientConnectedCallback -= OnClientConnected;
            manager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // ----- Server-authority spawning -------------------------------------

        private void OnServerStarted()
        {
            // Fresh authoritative session: clear any stale bookkeeping from a previous
            // run and spawn the single server/host-controlled Azure unit.
            clientUnits.Clear();
            azureInstance = null;

            if (azurePrefab == null)
            {
                Debug.LogWarning("[Spike] Azure network prefab is not assigned; nothing to spawn for the server.");
                return;
            }

            azureInstance = Instantiate(azurePrefab, azureSpawnPosition, Quaternion.identity);
            azureInstance.SpawnWithOwnership(NetworkManager.ServerClientId);
            Debug.Log($"[Spike] Spawned Azure for the server (owner {NetworkManager.ServerClientId}).");
        }

        private void OnServerStopped(bool _)
        {
            azureInstance = null;
            clientUnits.Clear();
        }

        private void OnClientConnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
            {
                return;
            }

            // The server/host already owns Azure; only remote clients receive an Ember
            // unit. On a host, the local client id equals ServerClientId.
            if (clientId == NetworkManager.ServerClientId)
            {
                return;
            }

            if (emberPrefab == null || clientUnits.ContainsKey(clientId))
            {
                return;
            }

            NetworkObject ember = Instantiate(emberPrefab, emberSpawnPosition, Quaternion.identity);
            ember.SpawnWithOwnership(clientId);
            clientUnits[clientId] = ember;
            Debug.Log($"[Spike] Spawned Ember for client {clientId} (owner {clientId}).");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
            {
                return;
            }

            if (!clientUnits.TryGetValue(clientId, out NetworkObject ember))
            {
                return;
            }

            // Despawn (not RemoveOwnership) the unit we spawned for this client, and
            // only if it is actually spawned, so ownership is never mutated on an
            // unspawned object.
            if (ember != null && ember.IsSpawned)
            {
                ember.Despawn();
            }

            clientUnits.Remove(clientId);
        }

        // ----- Debug UI ------------------------------------------------------

        private void OnGUI()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 12f, 220f, 180f));

            if (!manager.IsClient && !manager.IsServer)
            {
                if (GUILayout.Button("Start Host (authoritative)"))
                {
                    manager.StartHost();
                }

                if (GUILayout.Button("Start Server"))
                {
                    manager.StartServer();
                }

                if (GUILayout.Button("Start Client"))
                {
                    manager.StartClient();
                }
            }
            else
            {
                string role = manager.IsHost ? "Host" : manager.IsServer ? "Server" : "Client";
                GUILayout.Label($"Role: {role}");
                GUILayout.Label($"Local client id: {manager.LocalClientId}");
                GUILayout.Label($"Connected clients: {manager.ConnectedClientsIds.Count}");

                if (GUILayout.Button("Shutdown"))
                {
                    manager.Shutdown();
                }
            }

            GUILayout.EndArea();
        }
    }
}

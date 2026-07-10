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
        [SerializeField] private NetworkObject matchStatePrefab;
        [SerializeField] private NetworkObject azureTowerPrefab;
        [SerializeField] private NetworkObject emberTowerPrefab;
        [SerializeField] private NetworkObject azureCorePrefab;
        [SerializeField] private NetworkObject emberCorePrefab;
        [SerializeField] private Vector3 azureSpawnPosition = new Vector3(-4f, 1f, -2f);
        [SerializeField] private Vector3 emberSpawnPosition = new Vector3(4f, 1f, 1f);

        private NetworkObject azureInstance;
        private readonly List<NetworkObject> matchInfrastructure = new List<NetworkObject>();
        private readonly Dictionary<ulong, NetworkObject> clientUnits = new Dictionary<ulong, NetworkObject>();
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;

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
            matchInfrastructure.Clear();

            SpawnMatchInfrastructure();

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
            matchInfrastructure.Clear();
        }

        private void SpawnMatchInfrastructure()
        {
            // All M5 objects are ordinary dynamic network prefabs. This avoids
            // in-scene NetworkObject hashes and deliberately leaves ownership on the
            // server; neither towers nor cores belong to a player.
            SpawnInfrastructure(matchStatePrefab, Vector3.zero);
            SpawnInfrastructure(azureTowerPrefab, new Vector3(-5f, 2f, 0f));
            SpawnInfrastructure(emberTowerPrefab, new Vector3(5f, 2f, 0f));
            SpawnInfrastructure(azureCorePrefab, new Vector3(-13f, 3.5f, 0f));
            SpawnInfrastructure(emberCorePrefab, new Vector3(13f, 3.5f, 0f));
        }

        private void SpawnInfrastructure(NetworkObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[Spike] An M5 infrastructure prefab is not assigned.");
                return;
            }

            NetworkObject instance = Instantiate(prefab, position, Quaternion.identity);
            instance.Spawn();
            matchInfrastructure.Add(instance);
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

            EnsureGuiStyles();
            // IMGUI coordinates are physical pixels. Scale the whole panel from a
            // 1080p baseline so it remains usable at the 4K Game View and in builds.
            float uiScale = Mathf.Max(1f, Screen.height / 1080f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            GUILayout.BeginArea(new Rect(16f, 16f, 360f, 245f), GUI.skin.box);

            if (!manager.IsClient && !manager.IsServer)
            {
                GUILayout.Label("Multiplayer Spike", labelStyle);
                if (GUILayout.Button("Start Host (authoritative)", buttonStyle, GUILayout.Height(44f)))
                {
                    manager.StartHost();
                }

                if (GUILayout.Button("Start Server", buttonStyle, GUILayout.Height(44f)))
                {
                    manager.StartServer();
                }

                if (GUILayout.Button("Start Client", buttonStyle, GUILayout.Height(44f)))
                {
                    manager.StartClient();
                }
            }
            else
            {
                string role = manager.IsHost ? "Host" : manager.IsServer ? "Server" : "Client";
                GUILayout.Label($"Role: {role}", labelStyle);
                GUILayout.Label($"Local client id: {manager.LocalClientId}", labelStyle);
                GUILayout.Label($"Connected clients: {manager.ConnectedClientsIds.Count}", labelStyle);

                if (GUILayout.Button("Shutdown", buttonStyle, GUILayout.Height(44f)))
                {
                    manager.Shutdown();
                }
            }

            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void EnsureGuiStyles()
        {
            if (labelStyle != null && buttonStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }
    }
}

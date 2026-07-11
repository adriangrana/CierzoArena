using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>Server-only adapter that turns Runtime wave requests into NGO spawns.</summary>
    [RequireComponent(typeof(CreepWaveSpawner))]
    public sealed class NetworkCreepWaveSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject meleePrefab;
        [SerializeField] private NetworkObject rangedPrefab;

        private CreepWaveSpawner spawner;
        private NetworkManager manager;

        private void Awake()
        {
            spawner = GetComponent<CreepWaveSpawner>();
            spawner.SetSimulationEnabled(false);
            spawner.SetExternalSpawner(true);
            spawner.CreepRequested += SpawnRequestedCreep;
        }

        private void Start()
        {
            manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            manager.OnServerStarted += OnServerStarted;
            manager.OnServerStopped += OnServerStopped;
            if (manager.IsServer)
            {
                OnServerStarted();
            }
        }

        private void OnDestroy()
        {
            if (spawner != null)
            {
                spawner.CreepRequested -= SpawnRequestedCreep;
            }
            if (manager != null)
            {
                manager.OnServerStarted -= OnServerStarted;
                manager.OnServerStopped -= OnServerStopped;
            }
        }

        private void OnServerStarted() => spawner.SetSimulationEnabled(true);
        private void OnServerStopped(bool _) => spawner.SetSimulationEnabled(false);

        private void SpawnRequestedCreep(CreepWaveSpawner _, CreepArchetype archetype, Vector3 position, LaneRoute route)
        {
            if (manager == null || !manager.IsServer)
            {
                return;
            }

            NetworkObject prefab = archetype == CreepArchetype.Melee ? meleePrefab : rangedPrefab;
            if (prefab == null)
            {
                return;
            }

            NetworkObject instance = Instantiate(prefab, position, Quaternion.identity);
            instance.Spawn();
            if (instance.TryGetComponent(out CreepController creep))
            {
                creep.ConfigureRoute(route);
            }
        }
    }
}

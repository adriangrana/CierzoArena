using System;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>One authoritative, bounded wave source for a team/lane direction.</summary>
    public sealed class CreepWaveSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject meleePrefab;
        [SerializeField] private GameObject rangedPrefab;
        [SerializeField] private LaneRoute route;
        [SerializeField, Min(0f)] private float initialDelay = 5f;
        [SerializeField, Min(0.1f)] private float waveInterval = 25f;
        [SerializeField, Min(0)] private int meleeCount = 2;
        [SerializeField, Min(0)] private int rangedCount = 1;
        [SerializeField, Min(0f)] private float spawnSpacing = 1.2f;
        [SerializeField] private bool simulationEnabled = true;

        private float elapsed;
        private bool initialWavePending = true;
        private bool externalSpawner;

        public event Action<CreepWaveSpawner, CreepArchetype, Vector3, LaneRoute> CreepRequested;
        public LaneRoute Route => route;
        public bool SimulationEnabled => simulationEnabled;

        private void OnValidate()
        {
            initialDelay = Mathf.Max(0f, initialDelay);
            waveInterval = Mathf.Max(0.1f, waveInterval);
            meleeCount = Mathf.Max(0, meleeCount);
            rangedCount = Mathf.Max(0, rangedCount);
            spawnSpacing = Mathf.Max(0f, spawnSpacing);
        }

        private void Update()
        {
            Simulate(Time.deltaTime);
        }

        public void SetSimulationEnabled(bool enabled)
        {
            simulationEnabled = enabled;
        }

        public void SetExternalSpawner(bool enabled)
        {
            externalSpawner = enabled;
        }

        /// <summary>Advances at most one wave, even for a large delta.</summary>
        public bool Simulate(float deltaTime)
        {
            if (!simulationEnabled || route == null || route.Count == 0 ||
                (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying))
            {
                return false;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            float threshold = initialWavePending ? initialDelay : waveInterval;
            if (elapsed < threshold)
            {
                return false;
            }

            elapsed = 0f;
            initialWavePending = false;
            SpawnWave();
            return true;
        }

        public void SpawnWave()
        {
            if (!simulationEnabled || route == null || route.Count == 0 ||
                (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying))
            {
                return;
            }

            int ordinal = 0;
            for (int i = 0; i < meleeCount; i++)
            {
                Spawn(CreepArchetype.Melee, ordinal++);
            }
            for (int i = 0; i < rangedCount; i++)
            {
                Spawn(CreepArchetype.Ranged, ordinal++);
            }
        }

        private void Spawn(CreepArchetype archetype, int ordinal)
        {
            Vector3 start = route.GetWaypoint(0);
            Vector3 next = route.Count > 1 ? route.GetWaypoint(1) : start + Vector3.forward;
            Vector3 direction = next - start;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            Vector3 position = start - direction * (ordinal * spawnSpacing);

            if (externalSpawner)
            {
                CreepRequested?.Invoke(this, archetype, position, route);
                return;
            }

            GameObject prefab = archetype == CreepArchetype.Melee ? meleePrefab : rangedPrefab;
            if (prefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            if (instance.TryGetComponent(out CreepController creep))
            {
                creep.ConfigureRoute(route);
            }
        }
    }
}

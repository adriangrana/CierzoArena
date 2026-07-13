using System.Collections.Generic;
using CierzoArena.Environment;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Core
{
    public static class RuntimeNavMesh
    {
        private static NavMeshDataInstance navMeshDataInstance;
        private static bool hasBuiltRuntimeMesh;

        public static int LastCollectedSourceCount { get; private set; }
        public static int LastObstacleMarkupCount { get; private set; }

        public static void EnsureBuilt(LayerMask sourceMask, Bounds bounds)
        {
            if (NavMesh.SamplePosition(bounds.center, out _, Mathf.Max(bounds.extents.x, bounds.extents.z), NavMesh.AllAreas))
            {
                return;
            }

            if (hasBuiltRuntimeMesh)
            {
                return;
            }

            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);

            // Static environment colliders use the Ground layer so they are collected
            // in the same one-shot bake as terrain.  Mark obstacle roots Not Walkable
            // instead of adding a NavMeshObstacle per house/tree; this keeps the
            // runtime mesh deterministic and prevents agents from walking onto roofs.
            foreach (EnvironmentObstacle obstacle in Object.FindObjectsByType<EnvironmentObstacle>(FindObjectsInactive.Exclude))
            {
                if (obstacle == null || !obstacle.ExcludesFromNavMesh || !obstacle.gameObject.activeInHierarchy)
                {
                    continue;
                }

                markups.Add(new NavMeshBuildMarkup
                {
                    root = obstacle.transform,
                    overrideArea = true,
                    area = 1, // Unity's built-in Not Walkable area.
                    ignoreFromBuild = false,
                });
            }

            NavMeshBuilder.CollectSources(
                bounds,
                sourceMask,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                markups,
                sources);

            LastCollectedSourceCount = sources.Count;
            LastObstacleMarkupCount = markups.Count;

            if (sources.Count == 0)
            {
                return;
            }

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                return;
            }

            navMeshDataInstance = NavMesh.AddNavMeshData(data);
            hasBuiltRuntimeMesh = navMeshDataInstance.valid;
        }
    }
}

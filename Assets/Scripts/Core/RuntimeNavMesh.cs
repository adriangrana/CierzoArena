using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Core
{
    public static class RuntimeNavMesh
    {
        private static NavMeshDataInstance navMeshDataInstance;
        private static bool hasBuiltRuntimeMesh;

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

            NavMeshBuilder.CollectSources(
                bounds,
                sourceMask,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                markups,
                sources);

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

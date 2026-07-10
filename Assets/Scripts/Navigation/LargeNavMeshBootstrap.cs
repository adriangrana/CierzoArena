using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Navigation
{
    /// <summary>
    /// M3B navigation-scale spike helper. Builds a single runtime NavMesh that covers
    /// a whole large map area, instead of the per-unit local patch that
    /// <see cref="ClickMover"/> requests (centered on the unit, ~80 m extents).
    ///
    /// It reuses the existing <see cref="RuntimeNavMesh.EnsureBuilt"/> code path with
    /// map-sized bounds; the only change is the coverage area, which is exactly the
    /// variable M3B needs to evaluate: does the current runtime bake scale to a large
    /// map (build cost, path quality over long distances, separate regions)?
    ///
    /// Runs before units (negative execution order) so that <see cref="ClickMover"/>'s
    /// own EnsureBuilt call finds the full mesh already present and early-returns.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LargeNavMeshBootstrap : MonoBehaviour
    {
        [SerializeField] private LayerMask sourceMask = 1 << 6;
        [SerializeField] private Vector3 mapCenter = Vector3.zero;
        [SerializeField] private Vector3 mapSize = new Vector3(180f, 30f, 120f);
        [SerializeField] private bool logBuildTime = true;

        private void Awake()
        {
            Bounds bounds = new Bounds(mapCenter, mapSize);

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            RuntimeNavMesh.EnsureBuilt(sourceMask, bounds);
            stopwatch.Stop();

            if (logBuildTime)
            {
                Debug.Log($"[NavScale] Runtime NavMesh bake over {mapSize} (center {mapCenter}) took ~{stopwatch.ElapsedMilliseconds} ms.");
            }
        }
    }
}

#if UNITY_EDITOR
using CierzoArena.Environment;
using UnityEditor;
using UnityEngine;

namespace CierzoArena.EditorTools
{
    /// <summary>
    /// M23 Fase F: first deterministic ambience pass for the map outside the bases
    /// (lanes, jungle, river banks, outer boundary). It is additive and safe: every
    /// prop is placed on the default layer with no gameplay collider, so the runtime
    /// NavMesh (built from Ground-layer colliders) is never affected, and nothing is
    /// placed on the lane axes, bridges, camps or the boss pit. Uses a stable seed so
    /// regeneration is reproducible; original package prefabs are never modified.
    /// </summary>
    public static class FantasyVillageMapAmbienceBuilder
    {
        private static readonly Vector3 AzureBase = new Vector3(-60f, 0f, -60f);
        private static readonly Vector3 EmberBase = new Vector3(60f, 0f, 60f);
        private const float LaneClearRadius = 12f;
        private const float BaseClearRadius = 34f;
        private const float RiverClearRadius = 9f;

        public static void Build(FantasyVillageEnvironmentPalette palette)
        {
            if (palette == null || !palette.Validate(out _)) return;

            CleanGenerated();
            GameObject root = new GameObject("M23 Map Environment");
            GameObject boundary = Child(root, "Perimeter Cliffs");
            GameObject jungle = Child(root, "Jungle Vegetation");
            GameObject river = Child(root, "River Banks");
            GameObject bridges = Child(root, "Bridge Visuals");
            GameObject lanes = Child(root, "Lane Paths");

            var rng = new System.Random(0x0C1E320);

            BuildBoundary(boundary.transform, palette, rng);
            BuildJungle(jungle.transform, palette, rng);
            BuildRiverBanks(river.transform, palette, rng);
            BuildBridgeVisuals(bridges.transform, palette);
            BuildLanePaths(lanes.transform, palette);

            GameObject stone = Child(root, "Stone Paths");
            BuildStonePaths(stone.transform, palette, rng);
        }

        public static void CleanGenerated()
        {
            foreach (string rootName in new[] { "M23 Map Environment", "Map Ambience" })
            {
                GameObject root = GameObject.Find(rootName);
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        private static void BuildBridgeVisuals(Transform parent, FantasyVillageEnvironmentPalette palette)
        {
            if (palette.Bridge == null) return;
            foreach (Vector3 position in new[]
            {
                new Vector3(-60f, 0f, 60f),
                Vector3.zero,
                new Vector3(60f, 0f, -60f),
            })
            {
                Place(palette.Bridge, parent, position, 45f, 20f, simplifiedCollider: false);
            }
        }

        /// <summary>
        /// Three explicit visual roads mirror the same polylines used by lane routes:
        /// top travels through NW, mid crosses the centre, and bottom travels through
        /// SE. They are art-only, leave the navigation surface untouched and use the
        /// package path asset at a measured, repeated spacing rather than greybox
        /// ribbons.
        /// </summary>
        private static void BuildLanePaths(Transform parent, FantasyVillageEnvironmentPalette palette)
        {
            GameObject piece = palette.PathPieces.Length > 0 ? palette.PathPieces[0] : palette.PathStraight;
            if (piece == null) return;

            Vector3 northWest = new Vector3(-60f, 0f, 60f);
            Vector3 southEast = new Vector3(60f, 0f, -60f);
            PlaceRoadPolyline(parent, piece, new[] { AzureBase, northWest, EmberBase }, 5.5f);
            PlaceRoadPolyline(parent, piece, new[] { AzureBase, Vector3.zero, EmberBase }, 6.5f);
            PlaceRoadPolyline(parent, piece, new[] { AzureBase, southEast, EmberBase }, 5.5f);
        }

        private static void PlaceRoadPolyline(Transform parent, GameObject piece, Vector3[] points, float pieceMeters)
        {
            for (int segment = 0; segment < points.Length - 1; segment++)
            {
                Vector3 from = points[segment];
                Vector3 to = points[segment + 1];
                Vector3 delta = to - from; delta.y = 0f;
                float length = delta.magnitude;
                if (length < 0.01f) continue;
                Vector3 direction = delta / length;
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / (pieceMeters * 0.9f)));
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                for (int step = 0; step <= steps; step++)
                {
                    Vector3 position = from + direction * (length * step / steps);
                    PlacePaving(piece, parent, position, yaw);
                }
            }
        }

        private static void BuildStonePaths(Transform parent, FantasyVillageEnvironmentPalette palette, System.Random rng)
        {
            // M23 Fase B: selective natural-stone paving at the few zones players read
            // as "streets": a plaza ring around the central core and short paved mouths
            // where the mid lane meets each base. This is decorative only (no collider),
            // laid a hair above the ground to avoid z-fighting; we do not pave every
            // metre, keeping the earthy lanes as the dominant surface.
            GameObject piece = palette.PathStraight;
            if (piece == null && palette.PathPieces.Length > 0) piece = palette.PathPieces[0];
            if (piece == null) return;

            // Plaza ring around the core at origin.
            const int segments = 12;
            const float ringRadius = 9f;
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ringRadius;
                PlacePaving(piece, parent, p, a * Mathf.Rad2Deg + 90f);
            }

            // Short paved mouths from each base toward the mid lane (SW-NE diagonal).
            Vector3 midDir = new Vector3(1f, 0f, 1f).normalized;
            foreach (Vector3 basePos in new[] { AzureBase, EmberBase })
            {
                float sign = Vector3.Dot(basePos, midDir) > 0f ? -1f : 1f;
                for (int step = 1; step <= 4; step++)
                {
                    Vector3 p = basePos + midDir * sign * (BaseClearRadius - 6f + step * 3.5f);
                    PlacePaving(piece, parent, p, 45f);
                }
            }
        }

        private static void PlacePaving(GameObject prefab, Transform parent, Vector3 pos, float yaw)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null) return;
            instance.transform.position = new Vector3(pos.x, 0.02f, pos.z);
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one;
            FantasyVillageMaterialRemapper.RemapInstance(instance);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float nativeMax = Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
                instance.transform.localScale = Vector3.one * (4f / nativeMax);
            }
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            SetStaticRecursive(instance);
        }


        private static void BuildBoundary(Transform parent, FantasyVillageEnvironmentPalette palette, System.Random rng)
        {
            // A square ring of cliffs/mountains just OUTSIDE the playable square, hiding
            // the artificial edge without entering the combat area. A circular ring was
            // wrong: its diagonal dipped onto the base corners (bases sit at +/-60, i.e.
            // ~85 from centre), piling giant cliffs on top of Azure. We walk the four
            // outer edges instead and skip any point close to a base.
            GameObject[] cliffs = palette.Cliffs;
            GameObject[] mountains = palette.Mountains;
            if (cliffs.Length == 0 && mountains.Length == 0) return;
            // The previous 108m ring sat outside the 176m ground slab and appeared
            // as loose scenery beyond the map. Keep the visual perimeter inside the
            // playable ground's edge but behind the invisible +/-84 containment wall.
            const float edge = 78f;
            const float step = 22f;
            int index = 0;
            for (float t = -edge; t <= edge; t += step)
            {
                foreach (Vector3 basePos in new[]
                {
                    new Vector3(t, 0f, edge), new Vector3(t, 0f, -edge),
                    new Vector3(edge, 0f, t), new Vector3(-edge, 0f, t),
                })
                {
                    Vector3 pos = basePos + new Vector3((float)(rng.NextDouble() - 0.5) * 6f, 0f, (float)(rng.NextDouble() - 0.5) * 6f);
                    if (Vector3.Distance(pos, AzureBase) < 38f || Vector3.Distance(pos, EmberBase) < 38f || IsExcluded(pos)) continue;
                    bool useMountain = mountains.Length > 0 && index % 3 == 0;
                    index++;
                    GameObject[] set = useMountain ? mountains : cliffs;
                    if (set.Length == 0) set = cliffs.Length > 0 ? cliffs : mountains;
                    if (set.Length == 0) continue;
                    float meters = useMountain ? 14f + (float)rng.NextDouble() * 3f : 7f + (float)rng.NextDouble() * 2f;
                    // Background perimeter is visual only: it must never alter a
                    // route or create an invisible wall inside the gameplay square.
                    Place(set[rng.Next(set.Length)], parent, pos, (float)rng.NextDouble() * 360f, meters, simplifiedCollider: false);
                }
            }
        }

        private static void BuildJungle(Transform parent, FantasyVillageEnvironmentPalette palette, System.Random rng)
        {
            // Denser vegetation in the four off-lane jungle quadrants; keep lanes,
            // camps and the boss pit clear via exclusion checks.
            Vector3[] centers =
            {
                new Vector3(-38f, 0f, 34f), new Vector3(38f, 0f, -34f),
                new Vector3(-30f, 0f, -8f), new Vector3(30f, 0f, 8f),
                new Vector3(-8f, 0f, -34f), new Vector3(8f, 0f, 34f),
            };
            foreach (Vector3 c in centers)
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float rad = (float)rng.NextDouble() * 10f;
                    Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad;
                    if (IsExcluded(p)) continue;
                    bool pine = palette.PineTrees.Length > 0 && i % 3 == 0;
                    GameObject[] set = pine ? palette.PineTrees : palette.Trees;
                    if (set.Length == 0) continue;
                    Place(set[rng.Next(set.Length)], parent, p, (float)rng.NextDouble() * 360f, (pine ? 7.5f : 6f) + (float)rng.NextDouble() * 2f, simplifiedCollider: false);
                }
                if (palette.Flowers.Length > 0)
                {
                    Vector3 fp = c + new Vector3((float)(rng.NextDouble() - 0.5) * 8f, 0f, (float)(rng.NextDouble() - 0.5) * 8f);
                    if (!IsExcluded(fp)) Place(palette.Flowers[rng.Next(palette.Flowers.Length)], parent, fp, (float)rng.NextDouble() * 360f, 1.4f, simplifiedCollider: false);
                }
            }
        }

        private static void BuildRiverBanks(Transform parent, FantasyVillageEnvironmentPalette palette, System.Random rng)
        {
            // Vegetation and a single boat along the NW-SE river diagonal, offset to the
            // banks so the walkable crossing stays clear.
            for (float d = -70f; d <= 70f; d += 16f)
            {
                Vector3 onRiver = new Vector3(-d, 0f, d) * 0.5f;
                Vector3 bankDir = new Vector3(1f, 0f, 1f).normalized;
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 p = onRiver + bankDir * (RiverClearRadius + 2f) * side;
                    if (IsExcluded(p)) continue;
                    if (palette.Flowers.Length > 0 && Mathf.Abs(d) > 8f)
                        Place(palette.Flowers[rng.Next(palette.Flowers.Length)], parent, p, (float)rng.NextDouble() * 360f, 1.4f, simplifiedCollider: false);
                    if (palette.Rocks.Length > 0 && (int)d % 32 == 0)
                        Place(palette.Rocks[rng.Next(palette.Rocks.Length)], parent, p + bankDir * side, (float)rng.NextDouble() * 360f, 2f, simplifiedCollider: false);
                }
            }

            if (palette.Boat != null)
            {
                Vector3 boatPos = new Vector3(-40f, 0f, 40f) * 0.5f + new Vector3(1f, 0f, 1f).normalized * 12f;
                Place(palette.Boat, parent, boatPos, 210f, 6f, simplifiedCollider: false);
            }
        }

        private static bool IsExcluded(Vector3 p)
        {
            if (Vector3.Distance(p, AzureBase) < BaseClearRadius) return true;
            if (Vector3.Distance(p, EmberBase) < BaseClearRadius) return true;
            // Mid lane (SW-NE diagonal): distance to the line z = x.
            if (Mathf.Abs(p.z - p.x) / 1.41421356f < LaneClearRadius) return true;
            // Side lanes are L-shaped. Their gameplay axes remain clear even though
            // their old flat visual ribbons are now hidden.
            if (DistanceToSegmentXZ(p, AzureBase, new Vector3(-60f, 0f, 60f)) < LaneClearRadius) return true;
            if (DistanceToSegmentXZ(p, new Vector3(-60f, 0f, 60f), EmberBase) < LaneClearRadius) return true;
            if (DistanceToSegmentXZ(p, AzureBase, new Vector3(60f, 0f, -60f)) < LaneClearRadius) return true;
            if (DistanceToSegmentXZ(p, new Vector3(60f, 0f, -60f), EmberBase) < LaneClearRadius) return true;
            // River diagonal (NW-SE, z = -x).
            if (Mathf.Abs(p.z + p.x) / 1.41421356f < RiverClearRadius) return true;
            // Boss pit.
            if (Vector3.Distance(p, new Vector3(-18f, 0f, 18f)) < 14f) return true;
            return false;
        }

        private static float DistanceToSegmentXZ(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; ab.y = 0f;
            Vector3 ap = point - a; ap.y = 0f;
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
            return Vector3.Distance(point, a + ab * t);
        }

        private static GameObject Child(GameObject parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go;
        }

        private static void Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, float targetMeters, bool simplifiedCollider)
        {
            if (prefab == null) return;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null) return;
            instance.transform.position = new Vector3(pos.x, 0f, pos.z);
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one;

            // Fix URP-package-in-Built-in white render (see FantasyVillageMaterialRemapper).
            FantasyVillageMaterialRemapper.RemapInstance(instance);

            // Normalize to a real-world size (metres) by the largest 3D dimension so
            // large native cliffs/mountains no longer fill the map.
            Renderer[] probeRenderers = instance.GetComponentsInChildren<Renderer>(true);
            if (probeRenderers.Length > 0)
            {
                Bounds pb = probeRenderers[0].bounds;
                for (int i = 1; i < probeRenderers.Length; i++) pb.Encapsulate(probeRenderers[i].bounds);
                float nativeMax = Mathf.Max(0.001f, Mathf.Max(pb.size.x, Mathf.Max(pb.size.y, pb.size.z)));
                instance.transform.localScale = Vector3.one * (targetMeters / nativeMax);
                Bounds sb = probeRenderers[0].bounds;
                for (int i = 1; i < probeRenderers.Length; i++) sb.Encapsulate(probeRenderers[i].bounds);
                instance.transform.position += Vector3.up * (0f - sb.min.y);
            }
            else
            {
                instance.transform.localScale = Vector3.one * targetMeters;
            }

            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            if (simplifiedCollider)
            {
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                    BoxCollider box = instance.AddComponent<BoxCollider>();
                    Vector3 lossy = instance.transform.lossyScale;
                    box.center = instance.transform.InverseTransformPoint(b.center);
                    box.size = new Vector3(
                        lossy.x != 0f ? b.size.x / lossy.x : b.size.x,
                        lossy.y != 0f ? b.size.y / lossy.y : b.size.y,
                        lossy.z != 0f ? b.size.z / lossy.z : b.size.z);
                }
            }
            SetStaticRecursive(instance);
        }

        private static void SetStaticRecursive(GameObject go)
        {
            go.isStatic = true;
            foreach (Transform child in go.transform) SetStaticRecursive(child.gameObject);
        }
    }
}
#endif

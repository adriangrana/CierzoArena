#if UNITY_EDITOR
using CierzoArena.Environment;
using UnityEditor;
using UnityEngine;

namespace CierzoArena.EditorTools
{
    /// <summary>
    /// M23 Fase F: first deterministic ambience pass for the map outside the bases
    /// (lanes, jungle, river banks, outer boundary). Decorative props remain on the
    /// default layer without colliders; the small set of deliberately solid visual
    /// groups receives a separate Ground-layer gameplay collider. This lets the
    /// runtime NavMesh match visible art without modifying original package prefabs.
    /// </summary>
    public static class FantasyVillageMapAmbienceBuilder
    {
        private static readonly Vector3 AzureBase = new Vector3(-60f, 0f, -60f);
        private static readonly Vector3 EmberBase = new Vector3(60f, 0f, 60f);
        private const float LaneClearRadius = 12f;
        private const float BaseClearRadius = 34f;
        private const float RiverClearRadius = 9f;
        private const int GroundLayer = 6;
        private const float BridgeDeckSurfaceY = 0.02f;
        private static readonly float[] BridgeProfileSamples = { 0f, .2f, .4f, .5f, .6f, .8f, 1f };

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
            GameObject gameplay = Child(root, "Gameplay Colliders");
            GameObject blockers = Child(root, "Visible Jungle Blockers");

            var rng = new System.Random(0x0C1E320);

            BuildBoundary(boundary.transform, palette, rng);
            BuildJungle(jungle.transform, gameplay.transform, palette, rng);
            BuildRiverBanks(river.transform, palette, rng);
            BuildBridgeVisuals(bridges.transform, gameplay.transform, palette);
            BuildVisibleJungleBlockers(blockers.transform, gameplay.transform, palette, rng);
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

        /// <summary>Regenerates only the three bridge visual/gameplay pairs. This is
        /// deliberately narrower than rebuilding the full arena, so an artist can
        /// update the arched navigation deck without replacing bases, heroes or the
        /// remaining M23 dressing.</summary>
        public static bool RebuildBridgeDecks(FantasyVillageEnvironmentPalette palette)
        {
            if (palette == null || palette.Bridge == null) return false;
            GameObject root = GameObject.Find("M23 Map Environment");
            if (root == null) return false;
            Transform gameplay = root.transform.Find("Gameplay Colliders");
            if (gameplay == null) return false;

            Transform visuals = root.transform.Find("Bridge Visuals");
            if (visuals != null) Object.DestroyImmediate(visuals.gameObject);
            for (int i = gameplay.childCount - 1; i >= 0; i--)
            {
                Transform child = gameplay.GetChild(i);
                if (child.name.EndsWith(" Bridge GameplayRoot")) Object.DestroyImmediate(child.gameObject);
            }

            GameObject bridgeVisuals = Child(root, "Bridge Visuals");
            BuildBridgeVisuals(bridgeVisuals.transform, gameplay, palette);
            return true;
        }

        private static void BuildBridgeVisuals(Transform parent, Transform gameplayParent, FantasyVillageEnvironmentPalette palette)
        {
            if (palette.Bridge == null) return;
            (string Name, Vector3 Position, float Width, float Length)[] bridges =
            {
                ("Top Bridge", new Vector3(-60f, 0f, 60f), 22f, 26f),
                ("Mid Bridge", Vector3.zero, 16f, 26f),
                ("Bottom Bridge", new Vector3(60f, 0f, -60f), 22f, 26f),
            };
            foreach (var bridge in bridges)
            {
                string name = bridge.Name;
                Vector3 position = bridge.Position;
                float width = bridge.Width;
                float length = bridge.Length;
                GameObject visualRoot = Child(parent.gameObject, name + " VisualRoot");
                GameObject visual = Place(palette.Bridge, visualRoot.transform, position, 45f, 20f, simplifiedCollider: false);
                if (visual == null) continue;

                // The source pivot is not the walkable deck. Ray-sample the centre
                // of the real board before seating it, then seat both entrances on
                // the shore. This preserves the prefab's arch instead of flattening
                // it with one global offset.
                Bounds bounds = RendererBounds(visual, out bool hasBounds);
                if (!hasBounds) continue;
                if (!TrySampleBridgeDeck(visual, out Vector3 visualForward, out float[] measuredHeights))
                {
                    Debug.LogError($"[M23 Bridge] Could not sample the walkable board for '{name}'. The arched navigation deck was not generated.");
                    Object.DestroyImmediate(visualRoot);
                    continue;
                }

                float entryAverage = (measuredHeights[0] + measuredHeights[measuredHeights.Length - 1]) * .5f;
                float seatOffset = BridgeDeckSurfaceY - entryAverage;
                visualRoot.transform.position += Vector3.up * seatOffset;
                for (int i = 0; i < measuredHeights.Length; i++) measuredHeights[i] += seatOffset;
                bounds = RendererBounds(visual, out _);

                BridgeVisualProfile profile = visualRoot.AddComponent<BridgeVisualProfile>();

                GameObject deck = new GameObject(name + " GameplayRoot");
                deck.layer = GroundLayer;
                deck.transform.SetParent(gameplayParent);
                deck.transform.position = new Vector3(position.x, 0f, position.z);
                deck.transform.rotation = Quaternion.LookRotation(visualForward, Vector3.up);
                MeshFilter filter = deck.AddComponent<MeshFilter>();
                filter.sharedMesh = CreateArchedDeckMesh(name, width, length, measuredHeights);
                MeshCollider collider = deck.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                EnvironmentObstacle metadata = deck.AddComponent<EnvironmentObstacle>();
                metadata.Configure(EnvironmentObstacle.Category.BridgeDeck, visualRoot.transform, blocksNavigation: false);
                deck.isStatic = true;
                profile.ConfigureArc(bounds, width, length, deck.transform.position, deck.transform.forward,
                    (float[])BridgeProfileSamples.Clone(), measuredHeights, (float[])measuredHeights.Clone());
                Debug.Log($"[M23 Bridge] {name}: entries {profile.EntryHeight:0.###}/{profile.ExitHeight:0.###}, crown {profile.CrownHeight:0.###}, arc {(profile.CrownHeight - Mathf.Min(profile.EntryHeight, profile.ExitHeight)):0.###}, segments {profile.SegmentCount}, visual delta {profile.MaximumVisualDifference:0.###}.");
            }
        }

        private static bool TrySampleBridgeDeck(GameObject visual, out Vector3 forward, out float[] heights)
        {
            forward = Vector3.forward;
            heights = null;
            MeshFilter[] filters = visual.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter best = null;
            float bestHorizontalLength = 0f;
            bool alongLocalZ = true;
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i] != null ? filters[i].sharedMesh : null;
                if (mesh == null) continue;
                Bounds local = mesh.bounds;
                float candidateLength = Mathf.Max(local.size.x, local.size.z) * Mathf.Max(filters[i].transform.lossyScale.x, filters[i].transform.lossyScale.z);
                if (candidateLength <= bestHorizontalLength) continue;
                best = filters[i];
                bestHorizontalLength = candidateLength;
                alongLocalZ = local.size.z >= local.size.x;
            }
            if (best == null || best.sharedMesh == null) return false;

            MeshCollider probe = best.gameObject.AddComponent<MeshCollider>();
            probe.sharedMesh = best.sharedMesh;
            probe.convex = false;
            Physics.SyncTransforms();
            Bounds bounds = best.sharedMesh.bounds;
            Vector3 localCenter = bounds.center;
            float localLength = alongLocalZ ? bounds.size.z : bounds.size.x;
            float localHalfWidth = (alongLocalZ ? bounds.size.x : bounds.size.z) * .06f;
            float[] sampled = new float[BridgeProfileSamples.Length];
            bool allHit = true;
            for (int i = 0; i < BridgeProfileSamples.Length; i++)
            {
                float t = BridgeProfileSamples[i];
                Vector3 local = localCenter;
                if (alongLocalZ) local.z += (t - .5f) * localLength;
                else local.x += (t - .5f) * localLength;
                // A tiny inward shift prevents endpoint rays from missing because
                // they land exactly on a triangle edge. The centreline avoids rails.
                if (i == 0) { if (alongLocalZ) local.z += localLength * .01f; else local.x += localLength * .01f; }
                if (i == BridgeProfileSamples.Length - 1) { if (alongLocalZ) local.z -= localLength * .01f; else local.x -= localLength * .01f; }
                local += alongLocalZ ? Vector3.right * localHalfWidth : Vector3.forward * localHalfWidth;
                Vector3 origin = best.transform.TransformPoint(local + Vector3.up * (bounds.size.y + 10f));
                if (!probe.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, bounds.size.y + 24f))
                {
                    allHit = false;
                    break;
                }
                sampled[i] = hit.point.y;
            }
            forward = alongLocalZ ? best.transform.forward : best.transform.right;
            forward.y = 0f;
            Object.DestroyImmediate(probe);
            if (!allHit || forward.sqrMagnitude < .001f) return false;
            heights = sampled;
            return true;
        }

        private static Mesh CreateArchedDeckMesh(string bridgeName, float width, float length, float[] heights)
        {
            int sections = heights != null ? heights.Length : 0;
            if (sections < 2) throw new System.ArgumentException("An arched deck needs at least two samples.", nameof(heights));
            Vector3[] vertices = new Vector3[sections * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[(sections - 1) * 6];
            for (int i = 0; i < sections; i++)
            {
                float t = i / (float)(sections - 1);
                float z = (t - .5f) * length;
                int vertex = i * 2;
                vertices[vertex] = new Vector3(-width * .5f, heights[i], z);
                vertices[vertex + 1] = new Vector3(width * .5f, heights[i], z);
                uv[vertex] = new Vector2(0f, t);
                uv[vertex + 1] = new Vector2(1f, t);
                if (i == sections - 1) continue;
                int triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }
            Mesh mesh = new Mesh { name = bridgeName + " Arched Navigation Deck" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildVisibleJungleBlockers(Transform visualParent, Transform gameplayParent, FantasyVillageEnvironmentPalette palette, System.Random rng)
        {
            // These six clusters replace the hidden M3C obstacle cubes.  Each has a
            // single footprint collider sized to the visible cluster, so there is no
            // invisible padding and no need for a large fleet of carved obstacles.
            (string Name, Vector3 Position, Vector3 Footprint)[] entries =
            {
                ("Azure North Grove", new Vector3(-44f, 0f, 6f), new Vector3(10f, 6f, 10f)),
                ("Azure South Grove", new Vector3(6f, 0f, -44f), new Vector3(10f, 6f, 10f)),
                ("Ember North Grove", new Vector3(-6f, 0f, 44f), new Vector3(10f, 6f, 10f)),
                ("Ember South Grove", new Vector3(44f, 0f, -6f), new Vector3(10f, 6f, 10f)),
                ("Azure Mid Rockwood", new Vector3(-22f, 0f, -6f), new Vector3(8f, 6f, 8f)),
                ("Ember Mid Rockwood", new Vector3(22f, 0f, 6f), new Vector3(8f, 6f, 8f)),
            };

            foreach (var entry in entries)
            {
                string name = entry.Name;
                Vector3 position = entry.Position;
                Vector3 footprint = entry.Footprint;
                GameObject visualCluster = Child(visualParent.gameObject, name + " VisualCluster");
                visualCluster.transform.position = position;
                GameObject[] trees = palette.Trees.Length > 0 ? palette.Trees : palette.PineTrees;
                if (trees.Length > 0)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = i / 5f * Mathf.PI * 2f + (float)rng.NextDouble() * .25f;
                        float radius = footprint.x * (0.22f + (float)rng.NextDouble() * .22f);
                        Vector3 local = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                        Place(trees[rng.Next(trees.Length)], visualCluster.transform, position + local,
                            (float)rng.NextDouble() * 360f, 5.5f + (float)rng.NextDouble() * 1.5f, simplifiedCollider: false);
                    }
                }
                if (palette.Rocks.Length > 0)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 local = new Vector3((float)(rng.NextDouble() - .5) * footprint.x * .7f, 0f,
                            (float)(rng.NextDouble() - .5) * footprint.z * .7f);
                        Place(palette.Rocks[rng.Next(palette.Rocks.Length)], visualCluster.transform, position + local,
                            (float)rng.NextDouble() * 360f, 2.2f, simplifiedCollider: false);
                    }
                }

                GameObject gameplay = new GameObject(name + " GameplayCollider");
                gameplay.layer = GroundLayer;
                gameplay.transform.SetParent(gameplayParent);
                gameplay.transform.position = position + Vector3.up * (footprint.y * .5f);
                gameplay.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                BoxCollider collider = gameplay.AddComponent<BoxCollider>();
                collider.size = footprint;
                EnvironmentObstacle metadata = gameplay.AddComponent<EnvironmentObstacle>();
                metadata.Configure(EnvironmentObstacle.Category.TreeCluster, visualCluster.transform, blocksNavigation: true);
                gameplay.isStatic = true;
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

        private static void BuildJungle(Transform parent, Transform gameplayParent, FantasyVillageEnvironmentPalette palette, System.Random rng)
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
                    GameObject tree = Place(set[rng.Next(set.Length)], parent, p, (float)rng.NextDouble() * 360f,
                        (pine ? 7.5f : 6f) + (float)rng.NextDouble() * 2f, simplifiedCollider: false);
                    CreateTreeObstacleCollider(gameplayParent, "Jungle Tree GameplayCollider", tree);
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

        private static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, float targetMeters, bool simplifiedCollider)
        {
            if (prefab == null) return null;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null) return null;
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
            return instance;
        }

        private static Bounds RendererBounds(GameObject instance, out bool hasBounds)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            hasBounds = renderers.Length > 0;
            if (!hasBounds) return default;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void CreateTreeObstacleCollider(Transform gameplayParent, string name, GameObject visual)
        {
            if (visual == null) return;
            Bounds bounds = RendererBounds(visual, out bool hasBounds);
            if (!hasBounds) return;

            float radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * .16f, .28f, .72f);
            float height = Mathf.Max(2f, bounds.size.y * .7f);
            GameObject gameplay = new GameObject(name);
            gameplay.layer = GroundLayer;
            gameplay.transform.SetParent(gameplayParent);
            gameplay.transform.position = new Vector3(bounds.center.x, height * .5f, bounds.center.z);
            CapsuleCollider collider = gameplay.AddComponent<CapsuleCollider>();
            collider.radius = radius;
            collider.height = height;
            EnvironmentObstacle metadata = gameplay.AddComponent<EnvironmentObstacle>();
            metadata.Configure(EnvironmentObstacle.Category.TreeObstacle, visual.transform, blocksNavigation: true);
            gameplay.isStatic = true;
        }

        private static void SetStaticRecursive(GameObject go)
        {
            go.isStatic = true;
            foreach (Transform child in go.transform) SetStaticRecursive(child.gameObject);
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using CierzoArena.Core;
using CierzoArena.Environment;
using UnityEditor;
using UnityEngine;

namespace CierzoArena.EditorTools
{
    /// <summary>
    /// Deterministic editor module that turns a bare gameplay core into a fortified
    /// mini-village base (M23 Fase C/E). It builds an explicit anchor hierarchy
    /// (Gameplay) plus a decorative composition from the Fantasy Village palette
    /// (Visuals), never modifying the original package prefabs and never placing decor
    /// inside the playable spawn/plaza/attack zones. Azure and Ember use the same
    /// <see cref="TeamBaseLayoutDefinition"/>, only mirrored by base orientation, so
    /// their competitive footprint is identical.
    /// </summary>
    public static class FantasyVillageBaseBuilder
    {
        public enum ColliderPolicy { Strip, SimplifiedBox, TrunkCapsule }

        /// <summary>World anchors produced for a base, consumed by lane routing and spawns.</summary>
        public sealed class BaseAnchors
        {
            public Transform HeroSpawn;
            public Transform Respawn;
            public Transform CameraStart;
            public Transform Shop;
            public Transform Shopkeeper;
            public Transform CoreDefenseApproach;
            public Transform CoreApproach;
            public readonly Transform[] Gateways = new Transform[3];   // top, mid, bottom
            public readonly Transform[] Interiors = new Transform[3];  // top, mid, bottom
        }

        /// <summary>Builds the base village around an existing gameplay core.</summary>
        public static BaseAnchors Build(
            Transform baseRoot, TeamId team, Vector3 baseCenter,
            TeamBaseLayoutDefinition layout, FantasyVillageEnvironmentPalette palette, Color accent)
        {
            // This method is also callable from the M23 repair menu. Replace only
            // M23-owned children so a second build cannot accumulate houses, paths
            // or stale anchors while the gameplay core remains untouched.
            ClearGeneratedChildren(baseRoot, "Gameplay", "Visuals", "Debug");
            TeamBaseLayoutDefinition.Resolved r = layout.Resolve(baseCenter, Vector3.zero);

            GameObject gameplay = Child(baseRoot, "Gameplay");
            GameObject visuals = Child(baseRoot, "Visuals");
            GameObject debug = Child(baseRoot, "Debug");
            Child(debug, "RouteGizmos");
            Child(debug, "AnchorGizmos");

            var anchors = new BaseAnchors
            {
                HeroSpawn = Anchor(gameplay, "HeroSpawnAnchor", r.HeroSpawn, r.Forward),
                Respawn = Anchor(gameplay, "RespawnAnchor", r.Respawn, r.Forward),
                CameraStart = Anchor(gameplay, "CameraStartAnchor", r.CameraStart, r.Forward),
                Shop = Anchor(gameplay, "ShopAnchor", r.Shop, -r.Forward),
                Shopkeeper = Anchor(gameplay, "ShopkeeperAnchor", r.Shopkeeper, -r.Forward),
                CoreDefenseApproach = Anchor(gameplay, "CoreDefenseApproach", r.CoreDefenseApproach, -r.Forward),
                CoreApproach = Anchor(gameplay, "CoreApproach", r.CoreApproach, -r.Forward),
            };
            anchors.Gateways[0] = Anchor(gameplay, "TopGateway", r.TopGateway, r.Forward);
            anchors.Gateways[1] = Anchor(gameplay, "MidGateway", r.MidGateway, r.Forward);
            anchors.Gateways[2] = Anchor(gameplay, "BottomGateway", r.BottomGateway, r.Forward);
            anchors.Interiors[0] = Anchor(gameplay, "TopInteriorWaypoint", r.TopInterior, r.Forward);
            anchors.Interiors[1] = Anchor(gameplay, "MidInteriorWaypoint", r.MidInterior, r.Forward);
            anchors.Interiors[2] = Anchor(gameplay, "BottomInteriorWaypoint", r.BottomInterior, r.Forward);

            // Attack anchors around the two Core Guards and the core, plus the core
            // itself, so creeps spread instead of stacking on a mesh centre.
            GameObject attack = Child(gameplay, "StructureAttackAnchors");
            RingAnchors(attack, "CoreGuardLeft", r.CoreGuardLeft, 3.2f, 4);
            RingAnchors(attack, "CoreGuardRight", r.CoreGuardRight, 3.2f, 4);
            RingAnchors(Child(gameplay, "CoreAttackAnchors"), "Core", r.Core, 7.5f, 6);

            GameObject simplifiedColliders = Child(gameplay, "SimplifiedColliders");
            Child(gameplay, "VisionAnchors");

            BuildVisuals(simplifiedColliders, visuals, team, r, palette, accent);
            return anchors;
        }

        // ----- Visual composition --------------------------------------------

        private static void BuildVisuals(GameObject simplifiedColliders, GameObject visuals, TeamId team, TeamBaseLayoutDefinition.Resolved r, FantasyVillageEnvironmentPalette palette, Color accent)
        {
            var rng = new System.Random(team == TeamId.Azure ? 0x0A2BADE : 0xE3BE7);

            GameObject townCenter = Child(visuals, "TownCenterVisual");
            GameObject corePlaza = Child(visuals, "CorePlaza");
            GameObject courtyard = Child(visuals, "SpawnCourtyard");
            GameObject shopDistrict = Child(visuals, "ShopDistrict");
            GameObject buildings = Child(visuals, "VillageBuildings");
            GameObject paths = Child(visuals, "Paths");
            GameObject trees = Child(visuals, "Trees");
            GameObject flowers = Child(visuals, "Flowers");
            GameObject fences = Child(visuals, "Fences");
            GameObject lanterns = Child(visuals, "Lanterns");
            GameObject crates = Child(visuals, "Crates");
            GameObject perimeter = Child(visuals, "Perimeter");

            // Town center: the largest building, placed just behind the core so its
            // silhouette reads as the nucleus without a collider intercepting core
            // selection. Colliders are stripped: the gameplay core owns targeting.
            if (palette.MainBuilding != null)
            {
                Vector3 tcPos = r.Core - r.Forward * 3.5f;
                GameObject townCenterVisual = Place(palette.MainBuilding, townCenter.transform, tcPos, Yaw(-r.Forward), 14f, ColliderPolicy.Strip, isStatic: true);
                CreateFootprintCollider(simplifiedColliders.transform, "Town Center GameplayCollider", townCenterVisual,
                    EnvironmentObstacle.Category.TownCenter, 0.76f, r, allowCoreApproach: true);
                BindCoreColliderToTownCenter(simplifiedColliders.transform.parent != null ? simplifiedColliders.transform.parent.parent : null, townCenterVisual);
            }

            // Core plaza: a stone path pad under the core, plus four lantern posts at
            // the plaza corners (emissive-only, no real lights added here).
            if (palette.PathStraight != null)
            {
                Place(palette.PathStraight, corePlaza.transform, r.Core, Yaw(r.Forward), 12f, ColliderPolicy.Strip, isStatic: true);
            }
            if (palette.Lantern != null)
            {
                foreach (Vector3 c in PlazaCorners(r.Core, r.Forward, r.Right, 7f))
                    Place(palette.Lantern, lanterns.transform, c, Yaw(r.Core - c), 3f, ColliderPolicy.Strip, isStatic: true);
            }

            // Spawn courtyard: stone pad behind the core; the centre stays clear. A
            // couple of benches and lanterns sit on the perimeter only.
            if (palette.PathStraight != null)
                Place(palette.PathStraight, courtyard.transform, r.HeroSpawn, Yaw(r.Forward), 13f, ColliderPolicy.Strip, isStatic: true);
            if (palette.Bench != null)
            {
                Place(palette.Bench, courtyard.transform, r.HeroSpawn - r.Right * 7f, Yaw(r.Right), 2.2f, ColliderPolicy.Strip, isStatic: true);
                Place(palette.Bench, courtyard.transform, r.HeroSpawn + r.Right * 7f, Yaw(-r.Right), 2.2f, ColliderPolicy.Strip, isStatic: true);
            }

            // Shop district: a small building + market props around the shop anchor,
            // clear of the exit path. The definitive vendor NPC is not in the package.
            if (palette.SmallHouses.Length > 0)
            {
                GameObject shopHouse = Place(palette.SmallHouses[0], shopDistrict.transform, r.Shop + r.Right * 3.5f, Yaw(-r.Right), 8f, ColliderPolicy.Strip, isStatic: true);
                CreateFootprintCollider(simplifiedColliders.transform, "Shop House GameplayCollider", shopHouse,
                    EnvironmentObstacle.Category.House, 0.72f, r, allowCoreApproach: false);
            }
            if (palette.Crate != null)
            {
                Place(palette.Crate, crates.transform, r.Shop, Yaw(r.Forward), 1.4f, ColliderPolicy.Strip, isStatic: true);
                Place(palette.Crate, crates.transform, r.Shop - r.Right * 1.6f + r.Forward * 0.4f, Yaw(r.Right), 1.2f, ColliderPolicy.Strip, isStatic: true);
            }
            if (palette.FlowerPot != null)
                Place(palette.FlowerPot, shopDistrict.transform, r.Shop + r.Forward * 1.6f, Quaternion.identity, 1.2f, ColliderPolicy.Strip, isStatic: true);
            if (palette.Lantern != null)
                Place(palette.Lantern, lanterns.transform, r.Shop - r.Forward * 1.4f, Quaternion.identity, 3f, ColliderPolicy.Strip, isStatic: true);
            if (palette.Fence != null)
                Place(palette.Fence, fences.transform, r.Shop - r.Right * 3.2f, Yaw(r.Forward), 3.5f, ColliderPolicy.Strip, isStatic: true);

            // Perimeter village buildings behind and to the sides, never between the
            // camera and the core/guards/gateways.
            GameObject[] houses = palette.SmallHouses;
            if (houses.Length > 0)
            {
                Vector3[] slots =
                {
                    r.HeroSpawn - r.Forward * 8f - r.Right * 12f,
                    r.HeroSpawn - r.Forward * 8f + r.Right * 12f,
                    r.HeroSpawn - r.Right * 18f,
                    r.HeroSpawn + r.Right * 18f,
                    r.Core - r.Right * 20f,
                    r.Core + r.Right * 20f,
                };
                for (int i = 0; i < slots.Length; i++)
                {
                    GameObject prefab = houses[i % houses.Length];
                    Quaternion facing = Yaw(r.Core - slots[i]);
                    Quaternion jitter = Quaternion.Euler(0f, i % 2 == 0 ? 8f : -8f, 0f);
                    GameObject house = Place(prefab, buildings.transform, slots[i], facing * jitter, 8.5f, ColliderPolicy.Strip, isStatic: true);
                    CreateFootprintCollider(simplifiedColliders.transform, "Village House " + (i + 1) + " GameplayCollider", house,
                        EnvironmentObstacle.Category.House, 0.72f, r, allowCoreApproach: false);
                }
            }

            // Paths from spawn toward the core plaza and toward each gateway.
            if (palette.PathStraight != null)
            {
                PlacePathRun(paths.transform, palette.PathStraight, r.HeroSpawn, r.Core, 6f);
                PlacePathRun(paths.transform, palette.PathStraight, r.Core, r.MidGateway, 6f);
            }

            // Deterministic vegetation clusters at the rear corners only (exclusion of
            // all playable/attack zones is guaranteed by placing behind the spawn).
            if (palette.Trees.Length > 0)
            {
                Vector3[] centers = { r.HeroSpawn - r.Forward * 10f - r.Right * 20f, r.HeroSpawn - r.Forward * 10f + r.Right * 20f };
                foreach (Vector3 c in centers) ScatterCluster(trees.transform, simplifiedColliders.transform, palette.Trees, c, 5f, 4, rng, ColliderPolicy.TrunkCapsule, r);
            }
            if (palette.Flowers.Length > 0)
            {
                Vector3[] centers = { r.HeroSpawn - r.Right * 9f, r.HeroSpawn + r.Right * 9f, r.Core - r.Right * 11f, r.Core + r.Right * 11f };
                foreach (Vector3 c in centers) ScatterCluster(flowers.transform, simplifiedColliders.transform, palette.Flowers, c, 2.2f, 3, rng, ColliderPolicy.Strip, r);
            }

            // Team accent tint applied to lanterns via a shared MaterialPropertyBlock,
            // never by editing the original package materials.
            ApplyAccent(lanterns, accent);

            // Presentation instances never own gameplay collision. The original
            // prefab colliders were already stripped by Place; this final pass also
            // removes the simplified probe colliders used only while composing the
            // scene. Gameplay blockers remain under Gameplay/SimplifiedColliders.
            foreach (Collider collider in visuals.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }

        // ----- Helpers --------------------------------------------------------

        private static GameObject Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go;
        }

        private static void ClearGeneratedChildren(Transform parent, params string[] names)
        {
            foreach (string name in names)
            {
                Transform child = parent.Find(name);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
        }

        private static GameObject Child(GameObject parent, string name) => Child(parent.transform, name);

        private static Transform Anchor(GameObject parent, string name, Vector3 position, Vector3 facing)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(position.x, 0f, position.z);
            go.transform.rotation = Yaw(facing);
            return go.transform;
        }

        private static void RingAnchors(GameObject parent, string label, Vector3 center, float radius, int count)
        {
            GameObject group = Child(parent, label + " AttackAnchors");
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                GameObject go = new GameObject($"{label} Anchor {i + 1}");
                go.transform.SetParent(group.transform);
                go.transform.position = new Vector3(p.x, 0f, p.z);
            }
        }

        private static Quaternion Yaw(Vector3 dir)
        {
            dir.y = 0f;
            return dir.sqrMagnitude < 1e-4f ? Quaternion.identity : Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private static IEnumerable<Vector3> PlazaCorners(Vector3 center, Vector3 forward, Vector3 right, float d)
        {
            yield return center + forward * d + right * d;
            yield return center + forward * d - right * d;
            yield return center - forward * d + right * d;
            yield return center - forward * d - right * d;
        }

        private static void PlacePathRun(Transform parent, GameObject pathPrefab, Vector3 from, Vector3 to, float pieceMeters)
        {
            Vector3 delta = to - from; delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 0.01f) return;
            Vector3 dir = delta / distance;
            int steps = Mathf.Max(1, Mathf.RoundToInt(distance / (pieceMeters * 0.9f)));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = from + dir * (distance * i / steps);
                Place(pathPrefab, parent, p, Yaw(dir), pieceMeters, ColliderPolicy.Strip, isStatic: true);
            }
        }

        private static void ScatterCluster(Transform parent, Transform gameplayParent, GameObject[] set, Vector3 center, float radius, int count, System.Random rng, ColliderPolicy policy, TeamBaseLayoutDefinition.Resolved layout)
        {
            if (set == null || set.Length == 0) return;
            float targetMeters = policy == ColliderPolicy.TrunkCapsule ? 6f : 1.4f; // trees vs flowers
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = set[rng.Next(set.Length)];
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float rad = (float)rng.NextDouble() * radius;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad;
                float meters = targetMeters * (0.85f + (float)rng.NextDouble() * 0.4f);
                GameObject visual = Place(prefab, parent, p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), meters, ColliderPolicy.Strip, isStatic: true);
                if (policy == ColliderPolicy.TrunkCapsule)
                {
                    CreateTreeCollider(gameplayParent, "Base Tree GameplayCollider", visual, layout);
                }
            }
        }

        private static void CreateFootprintCollider(Transform gameplayParent, string name, GameObject visual,
            EnvironmentObstacle.Category category, float footprintScale, TeamBaseLayoutDefinition.Resolved layout, bool allowCoreApproach)
        {
            if (visual == null) return;
            Bounds bounds = ComputeBounds(visual, out bool hasBounds);
            if (!hasBounds) return;

            Vector3 size = new Vector3(bounds.size.x * footprintScale, Mathf.Max(2f, bounds.size.y * .8f), bounds.size.z * footprintScale);
            Vector3 center = new Vector3(bounds.center.x, size.y * .5f, bounds.center.z);
            if (IntersectsProtectedBaseSpace(center, size, layout, allowCoreApproach)) return;

            GameObject colliderRoot = new GameObject(name);
            colliderRoot.layer = 6; // Ground: collected by the one-shot runtime NavMesh bake.
            colliderRoot.transform.SetParent(gameplayParent);
            colliderRoot.transform.position = center;
            BoxCollider collider = colliderRoot.AddComponent<BoxCollider>();
            collider.size = size;
            EnvironmentObstacle metadata = colliderRoot.AddComponent<EnvironmentObstacle>();
            metadata.Configure(category, visual.transform, blocksNavigation: true);
            colliderRoot.isStatic = true;
        }

        private static void CreateTreeCollider(Transform gameplayParent, string name, GameObject visual, TeamBaseLayoutDefinition.Resolved layout)
        {
            if (visual == null) return;
            Bounds bounds = ComputeBounds(visual, out bool hasBounds);
            if (!hasBounds) return;
            float radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * .16f, .28f, .72f);
            float height = Mathf.Max(2f, bounds.size.y * .7f);
            Vector3 center = new Vector3(bounds.center.x, height * .5f, bounds.center.z);
            if (IntersectsProtectedBaseSpace(center, new Vector3(radius * 2f, height, radius * 2f), layout, allowCoreApproach: false)) return;

            GameObject colliderRoot = new GameObject(name);
            colliderRoot.layer = 6;
            colliderRoot.transform.SetParent(gameplayParent);
            colliderRoot.transform.position = center;
            CapsuleCollider collider = colliderRoot.AddComponent<CapsuleCollider>();
            collider.radius = radius;
            collider.height = height;
            EnvironmentObstacle metadata = colliderRoot.AddComponent<EnvironmentObstacle>();
            metadata.Configure(EnvironmentObstacle.Category.TreeObstacle, visual.transform, blocksNavigation: true);
            colliderRoot.isStatic = true;
        }

        private static void BindCoreColliderToTownCenter(Transform baseRoot, GameObject townCenterVisual)
        {
            if (baseRoot == null || townCenterVisual == null) return;
            foreach (Collider collider in baseRoot.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.name.EndsWith(" Core")) continue;
                EnvironmentObstacle metadata = collider.GetComponent<EnvironmentObstacle>();
                if (metadata == null) metadata = collider.gameObject.AddComponent<EnvironmentObstacle>();
                metadata.Configure(EnvironmentObstacle.Category.Structure, townCenterVisual.transform, blocksNavigation: true);
                return;
            }
        }

        private static bool IntersectsProtectedBaseSpace(Vector3 center, Vector3 size, TeamBaseLayoutDefinition.Resolved layout, bool allowCoreApproach)
        {
            Bounds footprint = new Bounds(center, new Vector3(size.x, 1f, size.z));
            Vector3[] protectedPoints =
            {
                layout.HeroSpawn, layout.Respawn, layout.Shop, layout.Shopkeeper,
                layout.TopGateway, layout.MidGateway, layout.BottomGateway,
                layout.TopInterior, layout.MidInterior, layout.BottomInterior,
                layout.CoreDefenseApproach,
            };
            foreach (Vector3 point in protectedPoints)
            {
                if (footprint.SqrDistance(point) < 2.25f) return true;
            }
            return !allowCoreApproach && footprint.SqrDistance(layout.CoreApproach) < 2.25f;
        }

        // Package prefabs have a large, unknown native size. Instead of blind scale
        // multipliers (which produced map-filling cliffs), every prefab is normalized
        // so its largest dimension becomes "targetMeters" metres, independent of the
        // prefab's authored size. The last Place argument is therefore a real-world
        // target size in metres.
        private static GameObject Place(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, float targetMeters, ColliderPolicy policy, bool isStatic)
        {
            if (prefab == null) return null;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null) return null;
            instance.transform.position = new Vector3(position.x, 0f, position.z);
            instance.transform.rotation = rotation;
            instance.transform.localScale = Vector3.one;

            // Fix the URP-package-in-Built-in-project white render by re-pointing
            // renderers to Built-in Standard variants that carry the original atlas.
            FantasyVillageMaterialRemapper.RemapInstance(instance);

            Bounds probe = ComputeBounds(instance, out bool hasBounds);
            if (hasBounds)
            {
                float nativeMax = Mathf.Max(0.001f, Mathf.Max(probe.size.x, Mathf.Max(probe.size.y, probe.size.z)));
                instance.transform.localScale = Vector3.one * (targetMeters / nativeMax);
                // Reseat on the ground so the mesh base sits at y = 0.
                Bounds seated = ComputeBounds(instance, out bool ok);
                if (ok) instance.transform.position += Vector3.up * (0f - seated.min.y);
            }
            else
            {
                instance.transform.localScale = Vector3.one * targetMeters;
            }

            ApplyColliderPolicy(instance, policy);
            if (isStatic) SetStaticRecursive(instance, true);
            return instance;
        }

        private static void ApplyColliderPolicy(GameObject instance, ColliderPolicy policy)
        {
            Collider[] existing = instance.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in existing) Object.DestroyImmediate(c);

            if (policy == ColliderPolicy.Strip) return;

            Bounds bounds = ComputeBounds(instance, out bool hasBounds);
            if (!hasBounds) return;

            if (policy == ColliderPolicy.SimplifiedBox)
            {
                BoxCollider box = instance.AddComponent<BoxCollider>();
                box.center = instance.transform.InverseTransformPoint(bounds.center);
                Vector3 lossy = instance.transform.lossyScale;
                box.size = new Vector3(
                    lossy.x != 0f ? bounds.size.x / lossy.x : bounds.size.x,
                    lossy.y != 0f ? bounds.size.y / lossy.y : bounds.size.y,
                    lossy.z != 0f ? bounds.size.z / lossy.z : bounds.size.z);
            }
            else // TrunkCapsule
            {
                CapsuleCollider cap = instance.AddComponent<CapsuleCollider>();
                cap.center = instance.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.5f, bounds.center.z));
                cap.height = bounds.size.y;
                cap.radius = Mathf.Max(0.15f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.18f);
            }
        }

        private static Bounds ComputeBounds(GameObject instance, out bool hasBounds)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            hasBounds = renderers.Length > 0;
            if (!hasBounds) return default;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static void SetStaticRecursive(GameObject go, bool value)
        {
            go.isStatic = value;
            foreach (Transform child in go.transform) SetStaticRecursive(child.gameObject, value);
        }

        private static void ApplyAccent(GameObject root, Color accent)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_EmissionColor", accent * 0.6f);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", accent * 0.6f);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
#endif

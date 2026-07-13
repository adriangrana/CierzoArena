#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CierzoArena.Core;
using CierzoArena.Environment;
using CierzoArena.Structures;
using CierzoArena.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.EditorTools
{
    /// <summary>
    /// Editor validator for the M23 base rework. It inspects the currently open scene
    /// and produces a readable pass/fail report: package located, palette valid, two
    /// bases, three gateways per base, spawn behind the core, shop and shopkeeper
    /// anchors, two Core Guards, one core, three strategic towers per lane (and no
    /// accidental fourth), lane routes ending at the enemy core, attack anchors,
    /// decorative visuals that are not targetable, and anchors inside the runtime
    /// NavMesh bounds. It never modifies the scene.
    /// </summary>
    public static class M23EnvironmentValidator
    {
        private const float NavBoundsHalfExtent = 100f;

        [MenuItem("Cierzo Arena/Environment/Validate M23 Base Rework")]
        public static void Validate()
        {
            var report = new StringBuilder();
            int failures = 0;

            void Check(bool ok, string label, string detail = "")
            {
                if (!ok) failures++;
                report.AppendLine($"[{(ok ? "PASS" : "FAIL")}] {label}{(string.IsNullOrEmpty(detail) ? "" : " - " + detail)}");
            }

            // Package + palette
            Check(AssetDatabase.IsValidFolder(FantasyVillagePaletteBuilder.PackagePrefabRoot), "Package located", FantasyVillagePaletteBuilder.PackagePrefabRoot);
            FantasyVillageEnvironmentPalette palette = AssetDatabase.LoadAssetAtPath<FantasyVillageEnvironmentPalette>(FantasyVillagePaletteBuilder.PaletteAssetPath);
            Check(palette != null, "Palette asset present", FantasyVillagePaletteBuilder.PaletteAssetPath);
            if (palette != null)
            {
                bool valid = palette.Validate(out string paletteReport);
                Check(valid, "Palette complete", paletteReport);
            }
            Check(AssetDatabase.LoadAssetAtPath<TeamBaseLayoutDefinition>(FantasyVillagePaletteBuilder.LayoutAssetPath) != null, "Base layout asset present");
            Check(LayerMask.NameToLayer("Ground") == 6, "Ground layer is navigation source", $"layer {LayerMask.NameToLayer("Ground")}");
            NavMeshBuildSettings navSettings = NavMesh.GetSettingsByID(0);
            Check(navSettings.agentRadius > 0f && navSettings.agentHeight > 0f, "NavMesh agent settings are valid",
                $"radius={navSettings.agentRadius:0.##}, height={navSettings.agentHeight:0.##}, step={navSettings.agentClimb:0.##}, slope={navSettings.agentSlope:0.##}");

            StructureEntity[] structures = Object.FindObjectsByType<StructureEntity>(FindObjectsInactive.Include);
            foreach (TeamId team in new[] { TeamId.Azure, TeamId.Ember })
            {
                ValidateTeam(team, structures, Check);
            }

            // Lane routes end at an enemy core
            LaneRoute[] routes = Object.FindObjectsByType<LaneRoute>(FindObjectsInactive.Include);
            Check(routes.Length >= 6, "Six lane routes present", $"found {routes.Length}");
            int routesWithCore = routes.Count(r => r.HasFinalObjective && r.FinalObjective != null && r.FinalObjective.Kind == StructureKind.Core);
            Check(routesWithCore == routes.Length && routes.Length > 0, "All lane routes target an enemy core", $"{routesWithCore}/{routes.Length}");

            // Decorative visuals must not be targetable / team-owned
            int leakedDecor = CountTargetableDecor();
            Check(leakedDecor == 0, "No decorative visual is targetable", $"{leakedDecor} offending objects");

            ValidateEnvironmentCoherence(Check);

            // Material health: inspect the effective material on generated renderers,
            // not merely whether a variant file exists on disk.
            List<string> materialFailures = CollectMaterialFailures();
            Check(materialFailures.Count == 0, "M23 generated materials are Built-in and coloured",
                materialFailures.Count == 0 ? string.Empty : string.Join(" | ", materialFailures.Take(4)));

            report.Insert(0, failures == 0
                ? "M23 validation: ALL CHECKS PASSED\n\n"
                : $"M23 validation: {failures} FAILURE(S)\n\n");

            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("Cierzo Arena - M23 Validation",
                report.ToString().Length > 1200 ? report.ToString().Substring(0, 1200) + "\n... (see Console)" : report.ToString(), "OK");
        }

        [MenuItem("Cierzo Arena/Environment/List White Or Unsupported Renderers")]
        public static void ListWhiteOrUnsupportedRenderers()
        {
            List<string> failures = CollectMaterialFailures();
            Debug.Log(failures.Count == 0
                ? "M23 material audit: no white, missing or unsupported renderer materials found."
                : "M23 material audit failures:\n" + string.Join("\n", failures));
        }

        [MenuItem("Cierzo Arena/Environment/Audit Gameplay Colliders")]
        public static void AuditGameplayColliders()
        {
            var report = new StringBuilder("M23 gameplay collider audit\n\n");
            foreach (Collider collider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include)
                         .OrderBy(c => c.transform.root.name).ThenBy(c => c.name))
            {
                if (!IsInsidePlayableArea(collider.bounds)) continue;
                EnvironmentObstacle metadata = collider.GetComponent<EnvironmentObstacle>();
                Renderer renderer = collider.GetComponent<Renderer>();
                bool rootRendererVisible = renderer != null && renderer.enabled;
                bool hasAssociatedVisual = metadata != null && metadata.VisualRoot != null &&
                    metadata.VisualRoot.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled);
                string classification = ClassifyCollider(collider, metadata);
                string recommendation = hasAssociatedVisual || rootRendererVisible || IsLegitimateUnassociatedCollider(collider)
                    ? "coherent"
                    : "replace with visible art or remove";
                report.AppendLine($"{classification}: {collider.name} | root={collider.transform.root.name} | layer={LayerMask.LayerToName(collider.gameObject.layer)}({collider.gameObject.layer}) | type={collider.GetType().Name} | bounds={collider.bounds} | visual={(hasAssociatedVisual || rootRendererVisible ? "yes" : "no")} | {recommendation}");
            }
            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("Cierzo Arena - Collider Audit", report.Length > 1200 ? report.ToString(0, 1200) + "\n... (see Console)" : report.ToString(), "OK");
        }

        [MenuItem("Cierzo Arena/Environment/Regenerate M23 Arched Bridge Decks")]
        public static void RegenerateArchedBridgeDecks()
        {
            FantasyVillageEnvironmentPalette palette = AssetDatabase.LoadAssetAtPath<FantasyVillageEnvironmentPalette>(FantasyVillagePaletteBuilder.PaletteAssetPath);
            if (!FantasyVillageMapAmbienceBuilder.RebuildBridgeDecks(palette))
            {
                EditorUtility.DisplayDialog("Cierzo Arena - M23 Bridges", "Open MobaGreyboxArena with its M23 Map Environment before regenerating bridge decks.", "OK");
                return;
            }

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            SceneView.RepaintAll();
            Debug.Log("[M23 Bridge] Rebuilt the three arched visual/gameplay deck pairs and saved the active scene.");
        }

        private static void ValidateTeam(TeamId team, StructureEntity[] structures, System.Action<bool, string, string> checkRaw)
        {
            void check(bool ok, string label, string detail = "") => checkRaw(ok, label, detail);
            string prefix = team.ToString();
            List<StructureEntity> mine = structures.Where(s => s != null && s.Team == team).ToList();

            int cores = mine.Count(s => s.Kind == StructureKind.Core);
            check(cores == 1, $"{prefix}: exactly one core", $"found {cores}");

            foreach (StructureLane lane in new[] { StructureLane.Top, StructureLane.Mid, StructureLane.Bottom })
            {
                int laneTowers = mine.Count(s => s.Kind == StructureKind.Tower && s.Lane == lane && s.Tier != StructureTier.CoreGuard);
                check(laneTowers == 3, $"{prefix} {lane}: exactly three strategic towers", $"found {laneTowers}");
            }

            int guards = mine.Count(s => s.Kind == StructureKind.Tower && s.Tier == StructureTier.CoreGuard);
            check(guards == 2, $"{prefix}: two Core Guards", $"found {guards}");

            // Base root + anchors
            GameObject root = GameObject.Find($"Bases/{prefix} Base");
            check(root != null, $"{prefix}: base root present");
            if (root == null) return;

            Transform gameplay = root.transform.Find("Gameplay");
            check(gameplay != null, $"{prefix}: Gameplay hierarchy present");
            if (gameplay == null) return;

            Transform spawn = gameplay.Find("HeroSpawnAnchor");
            Transform shop = gameplay.Find("ShopAnchor");
            Transform shopkeeper = gameplay.Find("ShopkeeperAnchor");
            check(spawn != null, $"{prefix}: HeroSpawnAnchor present");
            check(shop != null, $"{prefix}: ShopAnchor present");
            check(shopkeeper != null, $"{prefix}: ShopkeeperAnchor present");
            check(gameplay.Find("TopGateway") != null && gameplay.Find("MidGateway") != null && gameplay.Find("BottomGateway") != null, $"{prefix}: three gateways present");
            check(gameplay.Find("StructureAttackAnchors") != null && gameplay.Find("CoreAttackAnchors") != null, $"{prefix}: attack anchors present");

            StructureEntity core = mine.FirstOrDefault(s => s.Kind == StructureKind.Core);
            if (spawn != null && core != null)
            {
                Vector3 forward = new Vector3(-core.transform.position.x, 0f, -core.transform.position.z).normalized;
                Vector3 toSpawn = spawn.position - core.transform.position; toSpawn.y = 0f;
                check(Vector3.Dot(toSpawn, forward) < 0f, $"{prefix}: hero spawn is behind the core");
            }

            if (spawn != null)
            {
                bool inBounds = Mathf.Abs(spawn.position.x) < NavBoundsHalfExtent && Mathf.Abs(spawn.position.z) < NavBoundsHalfExtent;
                check(inBounds, $"{prefix}: hero spawn inside NavMesh bounds");
            }
        }

        private static List<string> CollectMaterialFailures()
        {
            var failures = new List<string>();
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                Renderer sourceRenderer = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                Material[] materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    Material material = materials[slot];
                    string sourcePath = sourceRenderer == null ? "<generated>" : AssetDatabase.GetAssetPath(sourceRenderer.gameObject);
                    string label = $"{renderer.gameObject.name}/{renderer.GetType().Name}[{slot}] source={sourcePath}";
                    if (material == null)
                    {
                        failures.Add(label + ": null material");
                        continue;
                    }
                    string shader = material.shader == null ? "<missing>" : material.shader.name;
                    if (material.name == "Default-Material" || shader == "Hidden/InternalErrorShader")
                    {
                        failures.Add(label + $": {material.name} ({shader})");
                        continue;
                    }
                    if (FantasyVillageMaterialRemapper.NeedsRemap(material))
                    {
                        failures.Add(label + $": unsupported shader {shader} on {material.name}");
                        continue;
                    }
                    if (!IsVillageRenderer(renderer)) continue;

                    if (sourceRenderer != null && sourceRenderer.sharedMaterials.Length != materials.Length)
                    {
                        failures.Add(label + $": material-slot count differs from source ({materials.Length}/{sourceRenderer.sharedMaterials.Length})");
                        continue;
                    }

                    if (!FantasyVillageMaterialRemapper.TryGetOriginalMaterial(material, out Material original))
                    {
                        failures.Add(label + $": package renderer still uses an unmapped material {material.name}; expected a GUID-mapped Built-in variant");
                        continue;
                    }

                    string materialPath = AssetDatabase.GetAssetPath(material);
                    if (string.IsNullOrEmpty(materialPath) || !AssetDatabase.Contains(material))
                    {
                        failures.Add(label + $": variant {material.name} is not persisted as an asset");
                        continue;
                    }

                    Texture sourceAtlas = FantasyVillageMaterialRemapper.GetSourceMainTexture(original);
                    Texture finalAtlas = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    if (sourceAtlas != null && finalAtlas == null)
                    {
                        failures.Add(label + $": variant {material.name} lost source atlas {sourceAtlas.name}; _MainTex is null (likely white fallback)");
                        continue;
                    }

                    if (sourceAtlas != null && finalAtlas != sourceAtlas)
                    {
                        failures.Add(label + $": variant {material.name} uses atlas {finalAtlas.name} instead of source atlas {sourceAtlas.name}");
                        continue;
                    }

                    Color sourceColor = FantasyVillageMaterialRemapper.GetSourceBaseColor(original);
                    Color finalColor = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    if ((sourceColor - finalColor).maxColorComponent > 0.02f)
                    {
                        failures.Add(label + $": variant colour {finalColor} differs from source {sourceColor}");
                    }
                }
            }
            return failures;
        }

        private static void ValidateEnvironmentCoherence(System.Action<bool, string, string> check)
        {
            BridgeVisualProfile[] bridgeProfiles = Object.FindObjectsByType<BridgeVisualProfile>(FindObjectsInactive.Include);
            check(bridgeProfiles.Length == 3, "Three bridge visual profiles present", $"found {bridgeProfiles.Length}");
            int alignedDecks = 0;
            foreach (BridgeVisualProfile profile in bridgeProfiles)
            {
                EnvironmentObstacle deck = Object.FindObjectsByType<EnvironmentObstacle>(FindObjectsInactive.Include)
                    .FirstOrDefault(o => o.ObstacleCategory == EnvironmentObstacle.Category.BridgeDeck && o.VisualRoot == profile.transform);
                MeshCollider deckCollider = deck != null ? deck.GetComponent<MeshCollider>() : null;
                if (deckCollider != null && profile.SampleCount >= 7 && profile.SegmentCount >= 6 &&
                    profile.CrownHeight > profile.EntryHeight + .05f && profile.MaximumVisualDifference <= .15f) alignedDecks++;
            }
            check(alignedDecks == bridgeProfiles.Length && bridgeProfiles.Length == 3,
                "Bridge visual decks use matching arched Ground meshes", $"{alignedDecks}/{bridgeProfiles.Length}");

            EnvironmentObstacle[] environment = Object.FindObjectsByType<EnvironmentObstacle>(FindObjectsInactive.Include);
            int houses = environment.Count(e => e.ObstacleCategory == EnvironmentObstacle.Category.House);
            int townCenters = environment.Count(e => e.ObstacleCategory == EnvironmentObstacle.Category.TownCenter);
            int treeObstacles = environment.Count(e => e.ObstacleCategory == EnvironmentObstacle.Category.TreeObstacle || e.ObstacleCategory == EnvironmentObstacle.Category.TreeCluster);
            check(houses >= 8, "Main houses have simplified colliders", $"found {houses}");
            check(townCenters == 2, "Town Centers have simplified colliders", $"found {townCenters}");
            check(treeObstacles >= 6, "Obstacle trees/groups have simplified colliders", $"found {treeObstacles}");

            int decorativeColliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include)
                .Count(c => HasAncestorNamed(c.transform, "Flowers") || HasAncestorNamed(c.transform, "Jungle Vegetation"));
            check(decorativeColliders == 0, "Decorative flowers and ambient trees have no colliders", $"found {decorativeColliders}");

            int invisibleLegacy = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include)
                .Count(c => IsInsidePlayableArea(c.bounds) && IsLegacyInvisibleBlocker(c));
            check(invisibleLegacy == 0, "No legacy invisible greybox blockers remain", $"found {invisibleLegacy}");

            int protectedIntersections = 0;
            foreach (EnvironmentObstacle obstacle in environment.Where(e => e.ExcludesFromNavMesh))
            {
                Collider collider = obstacle.GetComponent<Collider>();
                if (collider == null) continue;
                foreach (Transform anchor in FindProtectedAnchors())
                {
                    if (collider.bounds.SqrDistance(anchor.position) < 2.25f)
                    {
                        protectedIntersections++;
                        break;
                    }
                }
            }
            check(protectedIntersections == 0, "Environment colliders clear spawns, gateways and attack anchors", $"found {protectedIntersections}");
        }

        private static IEnumerable<Transform> FindProtectedAnchors()
        {
            string[] names = { "HeroSpawnAnchor", "RespawnAnchor", "ShopAnchor", "ShopkeeperAnchor", "TopGateway", "MidGateway", "BottomGateway", "CoreDefenseApproach", "CoreApproach" };
            foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (names.Contains(transform.name) || transform.name.Contains("AttackAnchors") || transform.name.Contains(" Anchor ")) yield return transform;
            }
        }

        private static bool IsInsidePlayableArea(Bounds bounds) =>
            Mathf.Abs(bounds.center.x) < 82f && Mathf.Abs(bounds.center.z) < 82f;

        private static bool HasAncestorNamed(Transform transform, string name)
        {
            for (Transform current = transform; current != null; current = current.parent)
                if (current.name == name) return true;
            return false;
        }

        private static bool IsLegacyInvisibleBlocker(Collider collider)
        {
            if (collider == null || collider.isTrigger || IsLegitimateUnassociatedCollider(collider)) return false;
            EnvironmentObstacle metadata = collider.GetComponent<EnvironmentObstacle>();
            if (metadata != null && metadata.VisualRoot != null) return false;
            string name = collider.name.ToLowerInvariant();
            bool oldName = name.Contains("greybox") || name.Contains("placeholder") || name.Contains("pillar gameplay") || name.Contains("jungle gameplay");
            return oldName && collider.GetComponent<Renderer>()?.enabled != true;
        }

        private static bool IsLegitimateUnassociatedCollider(Collider collider)
        {
            if (collider.GetComponent<StructureEntity>() != null || collider.GetComponent<TeamMember>() != null) return true;
            string root = collider.transform.root.name;
            return root == "EnvironmentRoot" || root == "Boundary" || root == "Towers" || root == "Neutral Zones" ||
                   collider.name.Contains("Ground") || collider.name.Contains("Core") || collider.isTrigger;
        }

        private static string ClassifyCollider(Collider collider, EnvironmentObstacle metadata)
        {
            if (metadata != null) return metadata.ObstacleCategory.ToString();
            if (collider.isTrigger) return "trigger";
            if (collider.GetComponent<StructureEntity>() != null) return "structure collider";
            if (collider.name.Contains("Ground")) return "ground collider";
            return IsLegacyInvisibleBlocker(collider) ? "obsolete blocker" : "unknown";
        }

        private static bool IsVillageRenderer(Renderer renderer)
        {
            if (renderer == null) return false;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(renderer.gameObject);
            string sourcePath = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
            if (sourcePath.StartsWith(FantasyVillagePaletteBuilder.PackagePrefabRoot)) return true;
            for (Transform current = renderer.transform; current != null; current = current.parent)
            {
                if (current.name == "M23 Map Environment" || current.name == "Visuals") return true;
            }
            return false;
        }

        private static int CountTargetableDecor()
        {
            int count = 0;
            foreach (StructureEntity structure in Object.FindObjectsByType<StructureEntity>(FindObjectsInactive.Include))
            {
                Transform t = structure.transform;
                while (t != null)
                {
                    if (t.name == "Visuals") { count++; break; }
                    t = t.parent;
                }
            }
            foreach (SelectableUnit selectable in Object.FindObjectsByType<SelectableUnit>(FindObjectsInactive.Include))
            {
                Transform t = selectable.transform;
                while (t != null)
                {
                    if (t.name == "Visuals") { count++; break; }
                    t = t.parent;
                }
            }
            return count;
        }
    }
}
#endif

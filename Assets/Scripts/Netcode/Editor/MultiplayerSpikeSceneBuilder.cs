#if UNITY_EDITOR
using CierzoArena.CameraSystem;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using CierzoArena.Netcode;
using CierzoArena.Units;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CierzoArena.Netcode.EditorTools
{
    /// <summary>
    /// Explicit, menu-driven builder for the M2.5 multiplayer spike scene. It only
    /// runs when the user consciously invokes the menu item; it never regenerates on
    /// editor load and never hand-authors NGO YAML or GlobalObjectIdHash values.
    /// Unity serializes the resulting scene, in-scene NetworkObjects and materials.
    /// </summary>
    public static class MultiplayerSpikeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MultiplayerSpikeArena.unity";
        private const int GroundLayer = 6;
        private const int SelectableLayer = 7;
        private const int AttackableLayer = 8;

        [MenuItem("Cierzo Arena/Create Multiplayer Spike Scene")]
        public static void CreateMultiplayerSpikeScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MultiplayerSpikeArena";

            EnsureLayerName(GroundLayer, "Ground");
            EnsureLayerName(SelectableLayer, "Selectable");
            EnsureLayerName(AttackableLayer, "Attackable");

            Material groundMaterial = CreateMaterial("Assets/Materials/Prototype_Ground.mat", new Color(0.24f, 0.31f, 0.29f));
            Material azureMaterial = CreateMaterial("Assets/Materials/Prototype_Azure.mat", new Color(0.08f, 0.35f, 0.9f));
            Material emberMaterial = CreateMaterial("Assets/Materials/Prototype_Ember.mat", new Color(0.85f, 0.18f, 0.12f));
            Material neutralMaterial = CreateMaterial("Assets/Materials/Prototype_Neutral.mat", new Color(0.46f, 0.33f, 0.56f));
            Material ringMaterial = CreateMaterial("Assets/Materials/Prototype_Selection.mat", new Color(0.95f, 0.86f, 0.24f));
            Material healthBackgroundMaterial = CreateMaterial("Assets/Materials/Prototype_HealthBackground.mat", new Color(0.08f, 0.08f, 0.08f));
            Material healthFillMaterial = CreateMaterial("Assets/Materials/Prototype_HealthFill.mat", new Color(0.2f, 0.85f, 0.3f));

            CreateGround(groundMaterial);
            CreateHeroSpawnPoint("Azure Hero Spawn", new Vector3(-10f, 1f, -6f), TeamId.Azure);
            CreateHeroSpawnPoint("Ember Hero Spawn", new Vector3(10f, 1f, 6f), TeamId.Ember);

            UnitDefinition azureDefinition = CreateOrLoadUnitDefinition(
                "Assets/Data/AzureVanguard.asset", 520f, 5.5f, 48f, 2.2f, 0.8f);
            UnitDefinition emberDefinition = CreateOrLoadUnitDefinition(
                "Assets/Data/EmberTarget.asset", 180f, 4.2f, 30f, 1.8f, 0.5f);
            ItemCatalog itemCatalog = CreateShopCatalog();
            AbilityDefinition[] abilityKit = CreateHeroAbilityKit();
            CreateShopZone("Azure Shop", new Vector3(-10f, 0.05f, -6f), TeamId.Azure, itemCatalog, azureMaterial);
            CreateShopZone("Ember Shop", new Vector3(10f, 0.05f, 6f), TeamId.Ember, itemCatalog, emberMaterial);

            // Units are authored as network prefabs (not in-scene NetworkObjects), so
            // they get valid, non-zero GlobalObjectIdHash values from the asset GUID
            // and are spawned at runtime by the server. This avoids the duplicated
            // in-scene GlobalObjectIdHash / ScenePlacedObjects registration failure.
            string azurePrefabPath = CreateNetworkUnitPrefab(
                "AzureVanguardNetwork", "Azure Vanguard", TeamId.Azure,
                azureMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, azureDefinition, abilityKit);

            string emberPrefabPath = CreateNetworkUnitPrefab(
                "EmberSkirmisherNetwork", "Ember Skirmisher", TeamId.Ember,
                emberMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, emberDefinition, abilityKit);

            string azureTowerPrefabPath = CreateNetworkStructurePrefab("AzureTowerNetwork", "Azure Tower", TeamId.Azure, StructureKind.Tower, StructureLane.Mid, StructureTier.Outer, azureMaterial, healthBackgroundMaterial, healthFillMaterial);
            string emberTowerPrefabPath = CreateNetworkStructurePrefab("EmberTowerNetwork", "Ember Tower", TeamId.Ember, StructureKind.Tower, StructureLane.Mid, StructureTier.Outer, emberMaterial, healthBackgroundMaterial, healthFillMaterial);
            // The compact spike has one lane only, so its cores remain immediately
            // testable once its single outer tower has been removed.
            string azureCorePrefabPath = CreateNetworkStructurePrefab("AzureCoreNetwork", "Azure Core", TeamId.Azure, StructureKind.Core, StructureLane.None, StructureTier.Core, azureMaterial, healthBackgroundMaterial, healthFillMaterial);
            string emberCorePrefabPath = CreateNetworkStructurePrefab("EmberCoreNetwork", "Ember Core", TeamId.Ember, StructureKind.Core, StructureLane.None, StructureTier.Core, emberMaterial, healthBackgroundMaterial, healthFillMaterial);
            string matchPrefabPath = CreateNetworkMatchPrefab();
            string projectilePrefabPath = CreateNetworkProjectilePrefab(emberMaterial);
            string azureMeleeCreepPath = CreateNetworkCreepPrefab("AzureMeleeCreepNetwork", TeamId.Azure, CreepArchetype.Melee, azureMaterial, healthBackgroundMaterial, healthFillMaterial);
            string azureRangedCreepPath = CreateNetworkCreepPrefab("AzureRangedCreepNetwork", TeamId.Azure, CreepArchetype.Ranged, azureMaterial, healthBackgroundMaterial, healthFillMaterial);
            string emberMeleeCreepPath = CreateNetworkCreepPrefab("EmberMeleeCreepNetwork", TeamId.Ember, CreepArchetype.Melee, emberMaterial, healthBackgroundMaterial, healthFillMaterial);
            string emberRangedCreepPath = CreateNetworkCreepPrefab("EmberRangedCreepNetwork", TeamId.Ember, CreepArchetype.Ranged, emberMaterial, healthBackgroundMaterial, healthFillMaterial);
            string neutralSmallPath = CreateNetworkNeutralPrefab("NeutralSmallNetwork", NeutralCampCategory.Small, PrimitiveType.Capsule, neutralMaterial, healthBackgroundMaterial, healthFillMaterial, 260f, 22f, 1.9f, 1.15f, 70, 45);
            string neutralMediumPath = CreateNetworkNeutralPrefab("NeutralMediumNetwork", NeutralCampCategory.Medium, PrimitiveType.Sphere, neutralMaterial, healthBackgroundMaterial, healthFillMaterial, 330f, 28f, 5.8f, 1.35f, 90, 60);
            string neutralLargePath = CreateNetworkNeutralPrefab("NeutralLargeNetwork", NeutralCampCategory.Large, PrimitiveType.Cube, neutralMaterial, healthBackgroundMaterial, healthFillMaterial, 620f, 42f, 2.1f, 1.5f, 150, 95);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            NetworkObject azurePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(azurePrefabPath).GetComponent<NetworkObject>();
            NetworkObject emberPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(emberPrefabPath).GetComponent<NetworkObject>();
            NetworkObject azureTowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(azureTowerPrefabPath).GetComponent<NetworkObject>();
            NetworkObject emberTowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(emberTowerPrefabPath).GetComponent<NetworkObject>();
            NetworkObject azureCorePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(azureCorePrefabPath).GetComponent<NetworkObject>();
            NetworkObject emberCorePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(emberCorePrefabPath).GetComponent<NetworkObject>();
            NetworkObject matchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(matchPrefabPath).GetComponent<NetworkObject>();
            NetworkProjectileVisual projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(projectilePrefabPath).GetComponent<NetworkProjectileVisual>();
            NetworkObject azureMeleeCreep = AssetDatabase.LoadAssetAtPath<GameObject>(azureMeleeCreepPath).GetComponent<NetworkObject>();
            NetworkObject azureRangedCreep = AssetDatabase.LoadAssetAtPath<GameObject>(azureRangedCreepPath).GetComponent<NetworkObject>();
            NetworkObject emberMeleeCreep = AssetDatabase.LoadAssetAtPath<GameObject>(emberMeleeCreepPath).GetComponent<NetworkObject>();
            NetworkObject emberRangedCreep = AssetDatabase.LoadAssetAtPath<GameObject>(emberRangedCreepPath).GetComponent<NetworkObject>();
            NetworkObject neutralSmall = AssetDatabase.LoadAssetAtPath<GameObject>(neutralSmallPath).GetComponent<NetworkObject>();
            NetworkObject neutralMedium = AssetDatabase.LoadAssetAtPath<GameObject>(neutralMediumPath).GetComponent<NetworkObject>();
            NetworkObject neutralLarge = AssetDatabase.LoadAssetAtPath<GameObject>(neutralLargePath).GetComponent<NetworkObject>();

            NetworkPrefabsList spikePrefabs = CreateNetworkPrefabsList(azurePrefab.gameObject, emberPrefab.gameObject, azureTowerPrefab.gameObject, emberTowerPrefab.gameObject, azureCorePrefab.gameObject, emberCorePrefab.gameObject, matchPrefab.gameObject, projectilePrefab.gameObject, azureMeleeCreep.gameObject, azureRangedCreep.gameObject, emberMeleeCreep.gameObject, emberRangedCreep.gameObject, neutralSmall.gameObject, neutralMedium.gameObject, neutralLarge.gameObject);

            CreateNetworkManager(spikePrefabs);

            CreateConnectionBootstrap(azurePrefab, emberPrefab, matchPrefab, azureTowerPrefab, emberTowerPrefab, azureCorePrefab, emberCorePrefab);
            CreateProjectileSpawner(projectilePrefab);
            CreateNetworkWaveSpawners(azureMeleeCreep, azureRangedCreep, emberMeleeCreep, emberRangedCreep);
            CreateNetworkNeutralCamp(neutralSmall, neutralMedium, neutralLarge);
            CreateLighting();
            CreateMobaCamera();
            CreateCommandController();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();

            EditorUtility.DisplayDialog(
                "Cierzo Arena",
                $"Multiplayer spike scene created at {ScenePath}.\n\n" +
                "Open two instances (editor + build, or two builds), press Start Host in one " +
                "and Start Client in the other to run the authoritative spike.",
                "OK");
        }

        private static void CreateGround(Material material)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Spike Ground";
            ground.layer = GroundLayer;
            ground.transform.localScale = new Vector3(3.5f, 1f, 3.5f);
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateNetworkManager(NetworkPrefabsList spikePrefabs)
        {
            GameObject managerObject = new GameObject("Network Manager");
            NetworkManager networkManager = managerObject.AddComponent<NetworkManager>();
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777);

            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = true,
                ConnectionApproval = false,
                SpawnTimeout = 10f
            };

            // Register the spike prefabs via the serialized NetworkPrefabsList so the
            // configuration persists in the scene (NetworkPrefabs.Add is runtime-only
            // and NonSerialized). This is the NGO-sanctioned path; no YAML authoring.
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(spikePrefabs);

            EditorUtility.SetDirty(networkManager);
        }

        private static NetworkPrefabsList CreateNetworkPrefabsList(params GameObject[] prefabs)
        {
            EnsureFolder("Assets", "Data");
            const string path = "Assets/Data/SpikeNetworkPrefabs.asset";

            // Rebuild the list from scratch so regenerating the scene never accumulates
            // duplicate or stale prefab entries.
            if (AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            NetworkPrefabsList list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, path);

            foreach (GameObject prefab in prefabs)
            {
                list.Add(new NetworkPrefab { Prefab = prefab });
            }

            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            return list;
        }

        private static string CreateNetworkUnitPrefab(string assetName, string displayName, TeamId team, Material bodyMaterial, Material ringMaterial, Material healthBackgroundMaterial, Material healthFillMaterial, UnitDefinition definition, AbilityDefinition[] abilityKit)
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Network");
            string path = $"Assets/Prefabs/Network/{assetName}.prefab";

            GameObject root = BuildNetworkUnit(displayName, team, bodyMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, definition, abilityKit);

            // Persisting as a prefab asset triggers NetworkObject.OnValidate, which
            // assigns a deterministic non-zero GlobalObjectIdHash from the asset GUID.
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return path;
        }

        private static GameObject BuildNetworkUnit(string name, TeamId team, Material bodyMaterial, Material ringMaterial, Material healthBackgroundMaterial, Material healthFillMaterial, UnitDefinition definition, AbilityDefinition[] abilityKit)
        {
            GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unit.name = name;
            unit.layer = SelectableLayer;
            unit.transform.position = Vector3.zero;
            unit.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            // Network identity first so NetworkBehaviours resolve their NetworkObject.
            unit.AddComponent<NetworkObject>();
            unit.AddComponent<NetworkTransform>();

            UnitDefinitionProvider definitionProvider = unit.AddComponent<UnitDefinitionProvider>();
            SetObjectReference(definitionProvider, "definition", definition);

            TeamMember teamMember = unit.AddComponent<TeamMember>();
            SetEnum(teamMember, "team", (int)team);
            unit.AddComponent<HeroUnit>();
            unit.AddComponent<VisionSource>();
            unit.AddComponent<VisionVisibility>();

            Health health = unit.AddComponent<Health>();
            SetFloat(health, "maxHealth", definition.MaxHealth);

            DamageFlash damageFlash = unit.AddComponent<DamageFlash>();
            SetObjectReference(damageFlash, "targetRenderer", unit.GetComponent<Renderer>());

            unit.AddComponent<DamageNumberSpawner>();
            CreateHealthBar(unit.transform, health, healthBackgroundMaterial, healthFillMaterial);

            unit.AddComponent<ClickMover>();
            BasicAttack attack = unit.AddComponent<BasicAttack>();
            ConfigureAttack(attack, team == TeamId.Azure ? AttackDelivery.Melee : AttackDelivery.Ranged,
                team == TeamId.Azure ? 3.25f : 7f,
                definition.AttackDamage,
                team == TeamId.Azure ? 1.25f : 1.4f,
                0.3f,
                0.35f);
            AttackVisual attackVisual = unit.AddComponent<AttackVisual>();
            SetObjectReference(attackVisual, "targetRenderer", unit.GetComponent<Renderer>());
            unit.AddComponent<UnitOrderController>();
            HeroProgression progression = unit.AddComponent<HeroProgression>();
            ConfigureHeroProgression(progression);
            ExperienceReward heroReward = unit.AddComponent<ExperienceReward>();
            SetInt(heroReward, "experienceReward", 300);
            SetInt(heroReward, "goldReward", 0);
            unit.AddComponent<HeroEconomy>();
            unit.AddComponent<HeroInventory>();
            unit.AddComponent<HeroMana>();
            unit.AddComponent<StatusEffectController>();
            unit.AddComponent<StatusEffectFeedback>();
            HeroAbilities heroAbilities = unit.AddComponent<HeroAbilities>();
            SetObjectArray(heroAbilities, "abilities", abilityKit);
            unit.AddComponent<HeroProgressionFeedback>();
            unit.AddComponent<HeroShopFeedback>();
            unit.AddComponent<HeroAbilitiesFeedback>();
            unit.AddComponent<HeroLifeCycle>();
            unit.AddComponent<HeroRespawnFeedback>();
            unit.AddComponent<NetworkUnitController>();
            unit.AddComponent<NetworkHeroLifeCycle>();
            unit.AddComponent<NetworkHeroProgression>();
            unit.AddComponent<NetworkHeroEconomy>();
            unit.AddComponent<NetworkHeroInventory>();
            unit.AddComponent<NetworkHeroAbilities>();
            unit.AddComponent<NetworkStatusEffects>();

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Selection Ring";
            ring.layer = SelectableLayer;
            ring.transform.SetParent(unit.transform);
            ring.transform.localPosition = new Vector3(0f, -0.72f, 0f);
            ring.transform.localScale = new Vector3(1.35f, 0.03f, 1.35f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMaterial;
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            SelectableUnit selectableUnit = unit.AddComponent<SelectableUnit>();
            SetObjectReference(selectableUnit, "selectionRing", ring.GetComponent<Renderer>());

            DeathVisibility deathVisibility = unit.AddComponent<DeathVisibility>();
            SerializedObject deathObject = new SerializedObject(deathVisibility);
            SerializedProperty renderers = deathObject.FindProperty("renderersToDisable");
            renderers.arraySize = 2;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = unit.GetComponent<Renderer>();
            renderers.GetArrayElementAtIndex(1).objectReferenceValue = ring.GetComponent<Renderer>();
            SerializedProperty colliders = deathObject.FindProperty("collidersToDisable");
            colliders.arraySize = 1;
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = unit.GetComponent<Collider>();
            deathObject.ApplyModifiedPropertiesWithoutUndo();

            return unit;
        }

        private static void CreateHeroSpawnPoint(string name, Vector3 position, TeamId team)
        {
            GameObject spawnObject = new GameObject(name);
            spawnObject.transform.position = position;
            HeroSpawnPoint spawn = spawnObject.AddComponent<HeroSpawnPoint>();
            spawn.SetTeam(team);
        }

        private static string CreateNetworkCreepPrefab(string assetName, TeamId team, CreepArchetype archetype, Material material, Material healthBackgroundMaterial, Material healthFillMaterial)
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Network");
            string path = $"Assets/Prefabs/Network/{assetName}.prefab";
            GameObject creep = GameObject.CreatePrimitive(archetype == CreepArchetype.Melee ? PrimitiveType.Capsule : PrimitiveType.Sphere);
            creep.name = assetName;
            creep.layer = AttackableLayer;
            creep.transform.localScale = archetype == CreepArchetype.Melee ? Vector3.one * 0.75f : Vector3.one * 0.65f;
            creep.GetComponent<Renderer>().sharedMaterial = material;
            creep.AddComponent<NetworkObject>();
            creep.AddComponent<NetworkTransform>();
            TeamMember member = creep.AddComponent<TeamMember>();
            SetEnum(member, "team", (int)team);
            Health health = creep.AddComponent<Health>();
            SetFloat(health, "maxHealth", archetype == CreepArchetype.Melee ? 220f : 150f);
            creep.AddComponent<StatusEffectController>();
            creep.AddComponent<VisionSource>();
            creep.AddComponent<VisionVisibility>();
            CreateHealthBar(creep.transform, health, healthBackgroundMaterial, healthFillMaterial, 1.65f, 1.05f);
            creep.AddComponent<ClickMover>();
            BasicAttack attack = creep.AddComponent<BasicAttack>();
            ConfigureAttack(attack,
                archetype == CreepArchetype.Melee ? AttackDelivery.Melee : AttackDelivery.Ranged,
                archetype == CreepArchetype.Melee ? 1.8f : 6f,
                archetype == CreepArchetype.Melee ? 16f : 13f,
                archetype == CreepArchetype.Melee ? 1.1f : 1.35f,
                0.25f,
                0.3f);
            AttackVisual visual = creep.AddComponent<AttackVisual>();
            SetObjectReference(visual, "targetRenderer", creep.GetComponent<Renderer>());
            CreepController controller = creep.AddComponent<CreepController>();
            SetEnum(controller, "archetype", (int)archetype);
            SetFloat(controller, "detectionRange", archetype == CreepArchetype.Melee ? 6.5f : 8f);
            SetFloat(controller, "leashRange", 14f);
            creep.AddComponent<DefensiveAggroResponder>();
            ExperienceReward reward = creep.AddComponent<ExperienceReward>();
            SetInt(reward, "experienceReward", archetype == CreepArchetype.Melee ? 60 : 75);
            SetFloat(reward, "experienceRadius", 14f);
            SetInt(reward, "goldReward", archetype == CreepArchetype.Melee ? 40 : 55);
            SetBool(reward, "shareExperienceWithNearbyHeroes", true);
            creep.AddComponent<NetworkCreepController>();
            PrefabUtility.SaveAsPrefabAsset(creep, path);
            Object.DestroyImmediate(creep);
            return path;
        }

        private static string CreateNetworkNeutralPrefab(string assetName, NeutralCampCategory category, PrimitiveType primitive, Material material, Material healthBackgroundMaterial, Material healthFillMaterial, float maxHealth, float damage, float range, float interval, int experience, int gold)
        {
            EnsureFolder("Assets", "Prefabs"); EnsureFolder("Assets/Prefabs", "Network"); string path=$"Assets/Prefabs/Network/{assetName}.prefab";
            GameObject neutral=GameObject.CreatePrimitive(primitive);neutral.name=assetName;neutral.layer=AttackableLayer;neutral.transform.localScale=category==NeutralCampCategory.Large?Vector3.one*1.15f:Vector3.one*.72f;neutral.GetComponent<Renderer>().sharedMaterial=material;neutral.AddComponent<NetworkObject>();neutral.AddComponent<NetworkTransform>();TeamMember member=neutral.AddComponent<TeamMember>();SetEnum(member,"team",(int)TeamId.Neutral);Health health=neutral.AddComponent<Health>();SetFloat(health,"maxHealth",maxHealth);neutral.AddComponent<StatusEffectController>();neutral.AddComponent<VisionVisibility>();CreateHealthBar(neutral.transform,health,healthBackgroundMaterial,healthFillMaterial,category==NeutralCampCategory.Large?2.3f:1.65f,1.1f);neutral.AddComponent<ClickMover>();BasicAttack attack=neutral.AddComponent<BasicAttack>();ConfigureAttack(attack,range>3f?AttackDelivery.Ranged:AttackDelivery.Melee,range,damage,interval,.25f,.3f);AttackVisual visual=neutral.AddComponent<AttackVisual>();SetObjectReference(visual,"targetRenderer",neutral.GetComponent<Renderer>());neutral.AddComponent<NeutralUnitController>();ExperienceReward reward=neutral.AddComponent<ExperienceReward>();SetInt(reward,"experienceReward",experience);SetFloat(reward,"experienceRadius",14f);SetInt(reward,"goldReward",gold);SetBool(reward,"shareExperienceWithNearbyHeroes",true);neutral.AddComponent<NetworkNeutralController>();PrefabUtility.SaveAsPrefabAsset(neutral,path);Object.DestroyImmediate(neutral);return path;
        }

        private static void ConfigureHeroProgression(HeroProgression progression)
        {
            SetInt(progression, "startingLevel", 1);
            SetInt(progression, "maximumLevel", 10);
            SetInt(progression, "baseExperienceForNextLevel", 100);
            SetFloat(progression, "experienceGrowth", 1.25f);
            SetFloat(progression, "maximumHealthPerLevel", 80f);
            SetFloat(progression, "damagePerLevel", 8f);
            SetFloat(progression, "movementSpeedPerLevel", 0.2f);
        }

        private static string CreateNetworkStructurePrefab(string assetName, string displayName, TeamId team, StructureKind kind, StructureLane lane, StructureTier tier, Material material, Material healthBackgroundMaterial, Material healthFillMaterial)
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Network");
            string path = $"Assets/Prefabs/Network/{assetName}.prefab";
            GameObject root = BuildNetworkStructure(displayName, team, kind, lane, tier, material, healthBackgroundMaterial, healthFillMaterial);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }

        private static GameObject BuildNetworkStructure(string name, TeamId team, StructureKind kind, StructureLane lane, StructureTier tier, Material material, Material healthBackgroundMaterial, Material healthFillMaterial)
        {
            PrimitiveType primitive = kind == StructureKind.Core ? PrimitiveType.Cube : PrimitiveType.Cylinder;
            GameObject structureObject = GameObject.CreatePrimitive(primitive);
            structureObject.name = name;
            structureObject.layer = 0;
            structureObject.transform.localScale = kind == StructureKind.Core ? new Vector3(4.5f, 7f, 4.5f) : new Vector3(2.8f, 4f, 2.8f);
            structureObject.transform.position = new Vector3(0f, kind == StructureKind.Core ? 3.5f : 2f, 0f);
            structureObject.GetComponent<Renderer>().sharedMaterial = material;

            structureObject.AddComponent<NetworkObject>();
            structureObject.AddComponent<NetworkTransform>();
            TeamMember teamMember = structureObject.AddComponent<TeamMember>();
            SetEnum(teamMember, "team", (int)team);
            Health health = structureObject.AddComponent<Health>();
            SetFloat(health, "maxHealth", kind == StructureKind.Core ? 1000f : 400f);
            health.RestoreFull();
            StructureEntity entity = structureObject.AddComponent<StructureEntity>();
            structureObject.AddComponent<VisionSource>();
            structureObject.AddComponent<VisionVisibility>();
            SetEnum(entity, "kind", (int)kind);
            SetEnum(entity, "lane", (int)lane);
            SetEnum(entity, "tier", (int)tier);

            SerializedObject entityData = new SerializedObject(entity);
            entityData.FindProperty("renderersToDisable").arraySize = 1;
            entityData.FindProperty("renderersToDisable").GetArrayElementAtIndex(0).objectReferenceValue = structureObject.GetComponent<Renderer>();

            GameObject target = new GameObject("Structure Target Collider");
            target.layer = SelectableLayer;
            target.transform.SetParent(structureObject.transform);
            if (kind == StructureKind.Core)
            {
                // Counter-scale the selectable volume so a large core remains
                // clickable on its actual visible body, not somewhere above it.
                Vector3 scale = structureObject.transform.localScale;
                target.transform.localPosition = Vector3.zero;
                target.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                BoxCollider targetCollider = target.AddComponent<BoxCollider>();
                targetCollider.size = new Vector3(4.5f, 7f, 4.5f);
            }
            else
            {
                target.transform.localPosition = Vector3.zero;
                SphereCollider targetCollider = target.AddComponent<SphereCollider>();
                targetCollider.radius = 1.5f;
            }
            Collider[] allColliders = structureObject.GetComponentsInChildren<Collider>();
            entityData.FindProperty("collidersToDisable").arraySize = allColliders.Length;
            for (int i = 0; i < allColliders.Length; i++)
            {
                entityData.FindProperty("collidersToDisable").GetArrayElementAtIndex(i).objectReferenceValue = allColliders[i];
            }
            entityData.ApplyModifiedPropertiesWithoutUndo();

            CreateHealthBar(
                structureObject.transform,
                health,
                healthBackgroundMaterial,
                healthFillMaterial,
                kind == StructureKind.Core ? 8f : 5f,
                kind == StructureKind.Core ? 2.6f : 2f,
                worldScale: true);
            if (kind == StructureKind.Tower)
            {
                TowerController tower = structureObject.AddComponent<TowerController>();
                structureObject.AddComponent<DefensiveAggroResponder>();
                SetFloat(tower, "searchInterval", 0.2f);
                SetInt(tower, "targetMask", ~0);
                ConfigureAttack(structureObject.GetComponent<BasicAttack>(), AttackDelivery.Ranged, 9f, 28f, 1f, 0.35f, 0.35f);
                structureObject.AddComponent<AttackVisual>();
            }

            structureObject.AddComponent<NetworkStructureController>();
            return structureObject;
        }

        private static string CreateNetworkMatchPrefab()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Network");
            const string path = "Assets/Prefabs/Network/MatchStateNetwork.prefab";
            GameObject match = new GameObject("Match State Controller");
            match.AddComponent<NetworkObject>();
            match.AddComponent<MatchStateController>();
            match.AddComponent<StructureProgressionController>();
            match.AddComponent<MatchVictoryDisplay>();
            match.AddComponent<NetworkMatchStateController>();
            PrefabUtility.SaveAsPrefabAsset(match, path);
            Object.DestroyImmediate(match);
            return path;
        }

        private static string CreateNetworkProjectilePrefab(Material material)
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Network");
            const string path = "Assets/Prefabs/Network/AttackProjectileNetwork.prefab";
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Attack Projectile";
            projectile.transform.localScale = Vector3.one * 0.28f;
            projectile.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(projectile.GetComponent<Collider>());
            projectile.AddComponent<NetworkObject>();
            projectile.AddComponent<NetworkTransform>();
            projectile.AddComponent<NetworkProjectileVisual>();
            PrefabUtility.SaveAsPrefabAsset(projectile, path);
            Object.DestroyImmediate(projectile);
            return path;
        }

        private static void CreateHealthBar(Transform unit, Health health, Material backgroundMaterial, Material fillMaterial, float localHeight = 2.35f, float width = 1.5f, bool worldScale = false)
        {
            GameObject bar = new GameObject("Health Bar");
            bar.layer = 2;
            bar.transform.SetParent(unit);
            if (worldScale)
            {
                Vector3 scale = unit.localScale;
                bar.transform.localPosition = new Vector3(0f, localHeight / scale.y, 0f);
                bar.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            }
            else
            {
                bar.transform.localPosition = new Vector3(0f, localHeight, 0f);
            }
            bar.transform.localRotation = Quaternion.identity;

            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Health Bar Background";
            background.layer = 2;
            background.transform.SetParent(bar.transform);
            background.transform.localPosition = Vector3.zero;
            background.transform.localScale = new Vector3(width, 0.18f, 0.03f);
            background.GetComponent<Renderer>().sharedMaterial = backgroundMaterial;
            Object.DestroyImmediate(background.GetComponent<Collider>());

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Health Bar Fill";
            fill.layer = 2;
            fill.transform.SetParent(bar.transform);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            fill.transform.localScale = new Vector3(width, 0.12f, 0.03f);
            fill.GetComponent<Renderer>().sharedMaterial = fillMaterial;
            Object.DestroyImmediate(fill.GetComponent<Collider>());

            WorldHealthBar healthBar = bar.AddComponent<WorldHealthBar>();
            SerializedObject barObject = new SerializedObject(healthBar);
            barObject.FindProperty("health").objectReferenceValue = health;
            barObject.FindProperty("fill").objectReferenceValue = fill.transform;
            barObject.FindProperty("width").floatValue = width;
            barObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateConnectionBootstrap(NetworkObject azurePrefab, NetworkObject emberPrefab, NetworkObject matchPrefab, NetworkObject azureTowerPrefab, NetworkObject emberTowerPrefab, NetworkObject azureCorePrefab, NetworkObject emberCorePrefab)
        {
            GameObject bootstrapObject = new GameObject("Spike Connection Bootstrap");
            SpikeConnectionBootstrap bootstrap = bootstrapObject.AddComponent<SpikeConnectionBootstrap>();
            SetObjectReference(bootstrap, "azurePrefab", azurePrefab);
            SetObjectReference(bootstrap, "emberPrefab", emberPrefab);
            SetObjectReference(bootstrap, "matchStatePrefab", matchPrefab);
            SetObjectReference(bootstrap, "azureTowerPrefab", azureTowerPrefab);
            SetObjectReference(bootstrap, "emberTowerPrefab", emberTowerPrefab);
            SetObjectReference(bootstrap, "azureCorePrefab", azureCorePrefab);
            SetObjectReference(bootstrap, "emberCorePrefab", emberCorePrefab);
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Sun Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f);
        }

        private static void CreateMobaCamera()
        {
            // Small spike ground: a 10-unit plane scaled 3.5 spans about +/-17.5, so
            // bounds of +/-20 keep a light margin. Height/pitch fix the framing
            // pull-back exactly as in the greybox (centreZ = height * cot(pitch),
            // independent of zoom).
            const float spikeCameraBound = 20f;
            const float spikeCameraHeight = 14f;
            const float spikeCameraPitchDeg = 55f;

            float pitchRad = spikeCameraPitchDeg * Mathf.Deg2Rad;
            float centreZ = spikeCameraHeight * (Mathf.Cos(pitchRad) / Mathf.Sin(pitchRad));
            Vector2 followOffset = new Vector2(0f, -centreZ);

            // In-scene provider so each instance resolves its own
            // LocalHeroProvider.Active before units spawn. It is never networked; the
            // owning NetworkUnitController registers its unit on spawn, so the host
            // follows its unit and the client follows its own, never a remote one.
            GameObject providerObject = new GameObject("Local Hero Provider");
            LocalHeroProvider provider = providerObject.AddComponent<LocalHeroProvider>();

            GameObject cameraObject = new GameObject("MOBA Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.farClipPlane = 500f;

            // No in-scene hero yet: start framed on the spawn origin so following an
            // owned unit that spawns near the centre does not jump.
            cameraObject.transform.position = new Vector3(followOffset.x, spikeCameraHeight, followOffset.y);
            cameraObject.transform.rotation = Quaternion.Euler(spikeCameraPitchDeg, 0f, 0f);

            CameraWorldBounds bounds = cameraObject.AddComponent<CameraWorldBounds>();
            SetFloat(bounds, "minX", -spikeCameraBound);
            SetFloat(bounds, "maxX", spikeCameraBound);
            SetFloat(bounds, "minZ", -spikeCameraBound);
            SetFloat(bounds, "maxZ", spikeCameraBound);

            MobaCameraController controller = cameraObject.AddComponent<MobaCameraController>();
            cameraObject.AddComponent<MinimapFeedback>();
            cameraObject.AddComponent<FogOfWarOverlay>();
            SerializedObject controllerObject = new SerializedObject(controller);
            controllerObject.FindProperty("keyboardPanSpeed").floatValue = 30f;
            controllerObject.FindProperty("edgeScrollingEnabled").boolValue = true;
            controllerObject.FindProperty("edgePanSpeed").floatValue = 30f;
            controllerObject.FindProperty("edgeBorderPixels").intValue = 12;
            controllerObject.FindProperty("zoomSpeed").floatValue = 2f;
            controllerObject.FindProperty("minOrthographicSize").floatValue = 6f;
            controllerObject.FindProperty("maxOrthographicSize").floatValue = 16f;
            controllerObject.FindProperty("targetCamera").objectReferenceValue = camera;
            controllerObject.FindProperty("worldBounds").objectReferenceValue = bounds;
            controllerObject.FindProperty("groundPlaneY").floatValue = 0f;
            controllerObject.FindProperty("heroProvider").objectReferenceValue = provider;
            controllerObject.FindProperty("followPlaneOffset").vector2Value = followOffset;
            controllerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateCommandController()
        {
            GameObject controller = new GameObject("Network Player Command Controller");
            NetworkPlayerCommandController commandController = controller.AddComponent<NetworkPlayerCommandController>();

            SerializedObject commandObject = new SerializedObject(commandController);
            commandObject.FindProperty("commandCamera").objectReferenceValue = Camera.main;
            commandObject.FindProperty("groundMask").intValue = 1 << GroundLayer;
            commandObject.FindProperty("selectableMask").intValue = 1 << SelectableLayer;
            commandObject.FindProperty("attackableMask").intValue = (1 << SelectableLayer) | (1 << AttackableLayer);
            commandObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UnitDefinition CreateOrLoadUnitDefinition(string path, float maxHealth, float movementSpeed, float attackDamage, float attackRange, float attacksPerSecond)
        {
            EnsureFolder("Assets", "Data");

            UnitDefinition definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<UnitDefinition>();
                AssetDatabase.CreateAsset(definition, path);

                SerializedObject definitionObject = new SerializedObject(definition);
                definitionObject.FindProperty("maxHealth").floatValue = maxHealth;
                definitionObject.FindProperty("movementSpeed").floatValue = movementSpeed;
                definitionObject.FindProperty("attackDamage").floatValue = attackDamage;
                definitionObject.FindProperty("attackRange").floatValue = attackRange;
                definitionObject.FindProperty("attacksPerSecond").floatValue = attacksPerSecond;
                definitionObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }

            return definition;
        }

        private static ItemCatalog CreateShopCatalog()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Items");
            ItemDefinition[] items =
            {
                CreateOrLoadItemDefinition("Assets/Data/Items/BastionPlating.asset", "bastion.plating", "Bastion Plating", "A sturdy plate that reinforces a hero's vital reserve.", 40, 20, 120f, 0f, 0f, 0f),
                CreateOrLoadItemDefinition("Assets/Data/Items/GaleEdge.asset", "gale.edge", "Gale Edge", "A honed edge that adds direct striking force.", 45, 22, 0f, 15f, 0f, 0f),
                CreateOrLoadItemDefinition("Assets/Data/Items/WindstepBoots.asset", "windstep.boots", "Windstep Boots", "Light boots for faster movement between lanes.", 35, 17, 0f, 0f, 0.8f, 0f),
                CreateOrLoadItemDefinition("Assets/Data/Items/TempestCog.asset", "tempest.cog", "Tempest Cog", "A simple mechanism that improves attack cadence.", 50, 25, 0f, 0f, 0f, 0.25f),
                CreateOrLoadItemDefinition("Assets/Data/Items/CierzoAlloy.asset", "cierzo.alloy", "Cierzo Alloy", "A balanced alloy providing both endurance and force.", 55, 27, 60f, 8f, 0f, 0f)
            };
            GameObject catalogObject = new GameObject("Item Catalog");
            ItemCatalog catalog = catalogObject.AddComponent<ItemCatalog>();
            SetObjectArray(catalog, "items", items);
            catalog.Rebuild();
            return catalog;
        }

        private static AbilityDefinition[] CreateHeroAbilityKit()
        {
            EnsureFolder("Assets", "Data"); EnsureFolder("Assets/Data", "Abilities");
            return new[]
            {
                CreateOrLoadAbility("Assets/Data/Abilities/ArcBolt.asset", "arc.bolt", "Arc Bolt", "Q: targeted projectile damage.", AbilityTargeting.UnitTarget, AbilityEffect.ProjectileDamage, 8f, .25f, 2.5f, 0f, 14f, new[] { 35f, 45f, 55f, 65f }, new[] { 45f, 70f, 95f, 120f }, new[] { 1, 1, 1, 1 }),
                CreateOrLoadAbility("Assets/Data/Abilities/StormMark.asset", "storm.mark", "Storm Mark", "W: slows enemies in a chosen area.", AbilityTargeting.PointTarget, AbilityEffect.AreaSlow, 7f, .3f, 2.5f, 2.5f, 14f, new[] { 45f, 55f, 65f, 75f }, new[] { .25f, .35f, .45f, .55f }, new[] { 1, 1, 1, 1 }),
                CreateOrLoadAbility("Assets/Data/Abilities/GaleStep.asset", "gale.step", "Gale Step", "E: temporary self movement boost.", AbilityTargeting.NoTarget, AbilityEffect.SelfMoveSpeed, 0f, .1f, 0f, 3f, 14f, new[] { 30f, 35f, 40f, 45f }, new[] { 1f, 1.4f, 1.8f, 2.2f }, new[] { 1, 1, 1, 1 }),
                CreateOrLoadAbility("Assets/Data/Abilities/TempestFall.asset", "tempest.fall", "Tempest Fall", "R: powerful area stun.", AbilityTargeting.PointTarget, AbilityEffect.StrongAreaStun, 9f, .45f, 4f, 1.25f, 14f, new[] { 90f, 120f, 150f, 180f }, new[] { 150f, 240f, 330f, 420f }, new[] { 6, 12, 18, 18 })
            };
        }

        private static AbilityDefinition CreateOrLoadAbility(string path, string id, string displayName, string description, AbilityTargeting targeting, AbilityEffect effect, float range, float castPoint, float radius, float duration, float projectileSpeed, float[] costs, float[] values, int[] requiredLevels)
        {
            AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            if (ability == null) { ability = ScriptableObject.CreateInstance<AbilityDefinition>(); AssetDatabase.CreateAsset(ability, path); }
            SerializedObject serialized = new SerializedObject(ability);
            serialized.FindProperty("abilityId").stringValue = id; serialized.FindProperty("displayName").stringValue = displayName; serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("targeting").enumValueIndex = (int)targeting; serialized.FindProperty("effect").enumValueIndex = (int)effect; serialized.FindProperty("range").floatValue = range; serialized.FindProperty("castPoint").floatValue = castPoint; serialized.FindProperty("areaRadius").floatValue = radius; serialized.FindProperty("duration").floatValue = duration; serialized.FindProperty("projectileSpeed").floatValue = projectileSpeed;
            SetFloatArray(serialized.FindProperty("manaCosts"), costs); SetFloatArray(serialized.FindProperty("effectValues"), values); SetIntArray(serialized.FindProperty("requiredHeroLevels"), requiredLevels);
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(ability); return ability;
        }

        private static ItemDefinition CreateOrLoadItemDefinition(string path, string id, string displayName, string description, int purchasePrice, int salePrice, float health, float damage, float movement, float attackSpeed)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("purchasePrice").intValue = purchasePrice;
            serialized.FindProperty("salePrice").intValue = salePrice;
            serialized.FindProperty("maximumHealthBonus").floatValue = health;
            serialized.FindProperty("attackDamageBonus").floatValue = damage;
            serialized.FindProperty("movementSpeedBonus").floatValue = movement;
            serialized.FindProperty("attackSpeedBonus").floatValue = attackSpeed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void CreateShopZone(string name, Vector3 position, TeamId team, ItemCatalog catalog, Material material)
        {
            GameObject zoneObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            zoneObject.name = name;
            zoneObject.transform.position = position;
            zoneObject.transform.localScale = new Vector3(8f, 0.08f, 8f);
            zoneObject.GetComponent<Renderer>().sharedMaterial = material;
            zoneObject.GetComponent<Collider>().isTrigger = true;
            ShopZone zone = zoneObject.AddComponent<ShopZone>();
            SetEnum(zone, "team", (int)team);
            SetObjectReference(zone, "catalog", catalog);
        }

        private static void EnsureFolder(string parent, string folder)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{folder}"))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static Material CreateMaterial(string path, Color color)
        {
            Shader shader = Shader.Find("Standard");
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Material material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureSceneInBuildSettings()
        {
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (buildScene.path == ScenePath)
                {
                    return;
                }
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            System.Array.Resize(ref scenes, scenes.Length + 1);
            scenes[scenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes;
        }

        private static void EnsureLayerName(int layer, string name)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layerProperty = layers.GetArrayElementAtIndex(layer);
            layerProperty.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Object target, string propertyName, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloatArray(SerializedProperty property, float[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        private static void SetIntArray(SerializedProperty property, int[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).intValue = values[i];
        }

        private static void CreateProjectileSpawner(NetworkProjectileVisual projectilePrefab)
        {
            GameObject spawnerObject = new GameObject("Network Projectile Spawner");
            NetworkProjectileSpawner spawner = spawnerObject.AddComponent<NetworkProjectileSpawner>();
            SetObjectReference(spawner, "projectilePrefab", projectilePrefab);
        }

        private static void CreateNetworkWaveSpawners(NetworkObject azureMelee, NetworkObject azureRanged, NetworkObject emberMelee, NetworkObject emberRanged)
        {
            GameObject root = new GameObject("Creep Wave Spike");
            LaneRoute azureRoute = CreateSpikeRoute(root.transform, "Azure Route", new[] { new Vector3(-12f, 1f, 0f), Vector3.up, new Vector3(12f, 1f, 0f) }, Color.cyan);
            LaneRoute emberRoute = CreateSpikeRoute(root.transform, "Ember Route", new[] { new Vector3(12f, 1f, 0f), Vector3.up, new Vector3(-12f, 1f, 0f) }, Color.red);
            CreateNetworkWaveSpawner(root.transform, "Azure Waves", azureRoute, azureMelee, azureRanged);
            CreateNetworkWaveSpawner(root.transform, "Ember Waves", emberRoute, emberMelee, emberRanged);
        }

        private static void CreateNetworkNeutralCamp(NetworkObject small, NetworkObject medium, NetworkObject large)
        {
            GameObject root=new GameObject("Neutral Camp Spike");root.transform.position=new Vector3(0f,0f,9f);NeutralCamp camp=root.AddComponent<NeutralCamp>();camp.Configure("spike.neutral",new[]{new NeutralSpawnEntry(NeutralCampCategory.Small,small.gameObject,new Vector3(-1.4f,0f,0f)),new NeutralSpawnEntry(NeutralCampCategory.Medium,medium.gameObject,new Vector3(1.4f,0f,0f)),new NeutralSpawnEntry(NeutralCampCategory.Large,large.gameObject,new Vector3(0f,0f,2f))},8f,14f,1.5f,20f);NetworkNeutralCampSpawner bridge=root.AddComponent<NetworkNeutralCampSpawner>();SetObjectReference(bridge,"smallPrefab",small);SetObjectReference(bridge,"mediumPrefab",medium);SetObjectReference(bridge,"largePrefab",large);
        }

        private static LaneRoute CreateSpikeRoute(Transform parent, string name, Vector3[] points, Color color)
        {
            GameObject routeObject = new GameObject(name);
            routeObject.transform.SetParent(parent);
            LaneRoute route = routeObject.AddComponent<LaneRoute>();
            Transform[] waypoints = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                GameObject waypoint = new GameObject($"Waypoint {i + 1}");
                waypoint.transform.SetParent(routeObject.transform);
                waypoint.transform.position = points[i];
                waypoints[i] = waypoint.transform;
            }
            SetObjectArray(route, "waypoints", waypoints);
            SerializedObject serialized = new SerializedObject(route);
            serialized.FindProperty("gizmoColor").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return route;
        }

        private static void CreateNetworkWaveSpawner(Transform parent, string name, LaneRoute route, NetworkObject melee, NetworkObject ranged)
        {
            GameObject source = new GameObject(name);
            source.transform.SetParent(parent);
            CreepWaveSpawner spawner = source.AddComponent<CreepWaveSpawner>();
            SetObjectReference(spawner, "route", route);
            SetFloat(spawner, "initialDelay", 3f);
            SetFloat(spawner, "waveInterval", 18f);
            SetInt(spawner, "meleeCount", 1);
            SetInt(spawner, "rangedCount", 1);
            NetworkCreepWaveSpawner bridge = source.AddComponent<NetworkCreepWaveSpawner>();
            SetObjectReference(bridge, "meleePrefab", melee);
            SetObjectReference(bridge, "rangedPrefab", ranged);
        }

        private static void SetInt(Object target, string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string propertyName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAttack(BasicAttack attack, AttackDelivery delivery, float range, float damage, float interval, float attackPoint, float backswing)
        {
            SerializedObject serialized = new SerializedObject(attack);
            serialized.FindProperty("delivery").enumValueIndex = (int)delivery;
            serialized.FindProperty("range").floatValue = range;
            serialized.FindProperty("damage").floatValue = damage;
            serialized.FindProperty("attackInterval").floatValue = interval;
            serialized.FindProperty("attackPoint").floatValue = attackPoint;
            serialized.FindProperty("backswing").floatValue = backswing;
            serialized.FindProperty("projectileSpeed").floatValue = 15f;
            serialized.FindProperty("projectileLifetime").floatValue = 4f;
            serialized.FindProperty("useUnitDefinition").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif

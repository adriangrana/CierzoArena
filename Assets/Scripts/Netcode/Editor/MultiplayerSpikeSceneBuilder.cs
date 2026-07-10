#if UNITY_EDITOR
using CierzoArena.CameraSystem;
using CierzoArena.Combat;
using CierzoArena.Core;
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

        [MenuItem("Cierzo Arena/Create Multiplayer Spike Scene")]
        public static void CreateMultiplayerSpikeScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MultiplayerSpikeArena";

            EnsureLayerName(GroundLayer, "Ground");
            EnsureLayerName(SelectableLayer, "Selectable");

            Material groundMaterial = CreateMaterial("Assets/Materials/Prototype_Ground.mat", new Color(0.24f, 0.31f, 0.29f));
            Material azureMaterial = CreateMaterial("Assets/Materials/Prototype_Azure.mat", new Color(0.08f, 0.35f, 0.9f));
            Material emberMaterial = CreateMaterial("Assets/Materials/Prototype_Ember.mat", new Color(0.85f, 0.18f, 0.12f));
            Material ringMaterial = CreateMaterial("Assets/Materials/Prototype_Selection.mat", new Color(0.95f, 0.86f, 0.24f));
            Material healthBackgroundMaterial = CreateMaterial("Assets/Materials/Prototype_HealthBackground.mat", new Color(0.08f, 0.08f, 0.08f));
            Material healthFillMaterial = CreateMaterial("Assets/Materials/Prototype_HealthFill.mat", new Color(0.2f, 0.85f, 0.3f));

            CreateGround(groundMaterial);

            UnitDefinition azureDefinition = CreateOrLoadUnitDefinition(
                "Assets/Data/AzureVanguard.asset", 520f, 5.5f, 48f, 2.2f, 0.8f);
            UnitDefinition emberDefinition = CreateOrLoadUnitDefinition(
                "Assets/Data/EmberTarget.asset", 180f, 4.2f, 30f, 1.8f, 0.5f);

            // Units are authored as network prefabs (not in-scene NetworkObjects), so
            // they get valid, non-zero GlobalObjectIdHash values from the asset GUID
            // and are spawned at runtime by the server. This avoids the duplicated
            // in-scene GlobalObjectIdHash / ScenePlacedObjects registration failure.
            string azurePrefabPath = CreateNetworkUnitPrefab(
                "AzureVanguardNetwork", "Azure Vanguard", TeamId.Azure,
                azureMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, azureDefinition);

            string emberPrefabPath = CreateNetworkUnitPrefab(
                "EmberSkirmisherNetwork", "Ember Skirmisher", TeamId.Ember,
                emberMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, emberDefinition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            NetworkObject azurePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(azurePrefabPath).GetComponent<NetworkObject>();
            NetworkObject emberPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(emberPrefabPath).GetComponent<NetworkObject>();

            NetworkPrefabsList spikePrefabs = CreateNetworkPrefabsList(azurePrefab.gameObject, emberPrefab.gameObject);

            CreateNetworkManager(spikePrefabs);

            CreateConnectionBootstrap(azurePrefab, emberPrefab);
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

        private static NetworkPrefabsList CreateNetworkPrefabsList(GameObject azurePrefab, GameObject emberPrefab)
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

            list.Add(new NetworkPrefab { Prefab = azurePrefab });
            list.Add(new NetworkPrefab { Prefab = emberPrefab });

            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            return list;
        }

        private static string CreateNetworkUnitPrefab(string assetName, string displayName, TeamId team, Material bodyMaterial, Material ringMaterial, Material healthBackgroundMaterial, Material healthFillMaterial, UnitDefinition definition)
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Network");
            string path = $"Assets/Prefabs/Network/{assetName}.prefab";

            GameObject root = BuildNetworkUnit(displayName, team, bodyMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, definition);

            // Persisting as a prefab asset triggers NetworkObject.OnValidate, which
            // assigns a deterministic non-zero GlobalObjectIdHash from the asset GUID.
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return path;
        }

        private static GameObject BuildNetworkUnit(string name, TeamId team, Material bodyMaterial, Material ringMaterial, Material healthBackgroundMaterial, Material healthFillMaterial, UnitDefinition definition)
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

            Health health = unit.AddComponent<Health>();
            SetFloat(health, "maxHealth", definition.MaxHealth);

            DamageFlash damageFlash = unit.AddComponent<DamageFlash>();
            SetObjectReference(damageFlash, "targetRenderer", unit.GetComponent<Renderer>());

            unit.AddComponent<DamageNumberSpawner>();
            CreateHealthBar(unit.transform, health, healthBackgroundMaterial, healthFillMaterial);

            unit.AddComponent<ClickMover>();
            unit.AddComponent<BasicAttack>();
            unit.AddComponent<UnitOrderController>();
            unit.AddComponent<NetworkUnitController>();

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

        private static void CreateHealthBar(Transform unit, Health health, Material backgroundMaterial, Material fillMaterial)
        {
            GameObject bar = new GameObject("Health Bar");
            bar.layer = 2;
            bar.transform.SetParent(unit);
            bar.transform.localPosition = new Vector3(0f, 2.35f, 0f);
            bar.transform.localRotation = Quaternion.identity;

            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Health Bar Background";
            background.layer = 2;
            background.transform.SetParent(bar.transform);
            background.transform.localPosition = Vector3.zero;
            background.transform.localScale = new Vector3(1.5f, 0.18f, 0.03f);
            background.GetComponent<Renderer>().sharedMaterial = backgroundMaterial;
            Object.DestroyImmediate(background.GetComponent<Collider>());

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Health Bar Fill";
            fill.layer = 2;
            fill.transform.SetParent(bar.transform);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            fill.transform.localScale = new Vector3(1.5f, 0.12f, 0.03f);
            fill.GetComponent<Renderer>().sharedMaterial = fillMaterial;
            Object.DestroyImmediate(fill.GetComponent<Collider>());

            WorldHealthBar healthBar = bar.AddComponent<WorldHealthBar>();
            SerializedObject barObject = new SerializedObject(healthBar);
            barObject.FindProperty("health").objectReferenceValue = health;
            barObject.FindProperty("fill").objectReferenceValue = fill.transform;
            barObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateConnectionBootstrap(NetworkObject azurePrefab, NetworkObject emberPrefab)
        {
            GameObject bootstrapObject = new GameObject("Spike Connection Bootstrap");
            SpikeConnectionBootstrap bootstrap = bootstrapObject.AddComponent<SpikeConnectionBootstrap>();
            SetObjectReference(bootstrap, "azurePrefab", azurePrefab);
            SetObjectReference(bootstrap, "emberPrefab", emberPrefab);
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

        private static void SetEnum(Object target, string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif

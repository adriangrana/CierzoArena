#if UNITY_EDITOR
using CierzoArena.CameraSystem;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Navigation;
using CierzoArena.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CierzoArena.EditorTools
{
    /// <summary>
    /// Explicit, menu-driven builder for the M3B navigation-scale spike scene. It
    /// creates a deliberately large, primitive-only test bed to evaluate whether the
    /// existing runtime NavMesh + NavMeshAgent stack scales to a MOBA-sized map:
    /// long routes, big obstacles, a ravine that splits the map into two regions, a
    /// single narrow bridge (chokepoint), an enclosed blocked zone, and map bounds.
    ///
    /// It only runs when invoked from the menu (no InitializeOnLoad), never touches
    /// PrototypeArena or MultiplayerSpikeArena, and reuses the existing gameplay and
    /// navigation systems (ClickMover, UnitOrderController, RuntimeNavMesh, etc.).
    /// </summary>
    public static class NavigationScaleSpikeBuilder
    {
        private const string ScenePath = "Assets/Scenes/NavigationScaleSpike.unity";
        private const int GroundLayer = 6;
        private const int SelectableLayer = 7;

        [MenuItem("Cierzo Arena/Create Navigation Scale Spike Scene")]
        public static void CreateNavigationScaleSpikeScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "NavigationScaleSpike";

            EnsureLayerName(GroundLayer, "Ground");
            EnsureLayerName(SelectableLayer, "Selectable");

            Material groundMaterial = CreateMaterial("Assets/Materials/Prototype_Ground.mat", new Color(0.24f, 0.31f, 0.29f));
            Material bridgeMaterial = CreateMaterial("Assets/Materials/Prototype_Bridge.mat", new Color(0.42f, 0.36f, 0.22f));
            Material obstacleMaterial = CreateMaterial("Assets/Materials/Prototype_Obstacle.mat", new Color(0.16f, 0.17f, 0.2f));
            Material canyonMaterial = CreateMaterial("Assets/Materials/Prototype_Canyon.mat", new Color(0.06f, 0.05f, 0.07f));
            Material markerMaterial = CreateMaterial("Assets/Materials/Prototype_Marker.mat", new Color(0.95f, 0.86f, 0.24f));
            Material azureMaterial = CreateMaterial("Assets/Materials/Prototype_Azure.mat", new Color(0.08f, 0.35f, 0.9f));
            Material emberMaterial = CreateMaterial("Assets/Materials/Prototype_Ember.mat", new Color(0.85f, 0.18f, 0.12f));
            Material ringMaterial = CreateMaterial("Assets/Materials/Prototype_Selection.mat", new Color(0.95f, 0.86f, 0.24f));
            Material healthBackgroundMaterial = CreateMaterial("Assets/Materials/Prototype_HealthBackground.mat", new Color(0.08f, 0.08f, 0.08f));
            Material healthFillMaterial = CreateMaterial("Assets/Materials/Prototype_HealthFill.mat", new Color(0.2f, 0.85f, 0.3f));

            BuildTerrain(groundMaterial, bridgeMaterial, obstacleMaterial, canyonMaterial);
            BuildDestinationMarkers(markerMaterial);

            UnitDefinition azureDefinition = CreateOrLoadUnitDefinition(
                "Assets/Data/AzureVanguard.asset", 520f, 5.5f, 48f, 2.2f, 0.8f);
            UnitDefinition emberDefinition = CreateOrLoadUnitDefinition(
                "Assets/Data/EmberTarget.asset", 180f, 4.2f, 30f, 1.8f, 0.5f);

            GameObject azure = CreateUnit(
                "Azure Vanguard", new Vector3(-72f, 1f, -40f), TeamId.Azure,
                azureMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, azureDefinition, startSelected: true);
            azure.AddComponent<NavPathProbe>();

            CreateUnit(
                "Ember Roamer", new Vector3(60f, 1f, 35f), TeamId.Ember,
                emberMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, emberDefinition, startSelected: false);

            CreateNavMeshBootstrap();
            CreateLighting();
            CreateCamera(azure.transform);
            CreateCommandController();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();

            EditorUtility.DisplayDialog(
                "Cierzo Arena",
                $"Navigation scale spike scene created at {ScenePath}.\n\n" +
                "Press Play, select Azure (left click), and right-click distant points, the bridge, " +
                "the blocked zone and the Ember roamer to stress long-range navigation. Watch the " +
                "Console for [NavScale] path length / status logs.",
                "OK");
        }

        // ----- Terrain --------------------------------------------------------

        private static void BuildTerrain(Material groundMaterial, Material bridgeMaterial, Material obstacleMaterial, Material canyonMaterial)
        {
            GameObject terrain = new GameObject("Terrain");

            // Two large walkable regions separated by a north-south ravine (a gap in
            // the ground), connected only by a single narrow bridge (chokepoint).
            CreateGroundBox(terrain.transform, "Ground West", new Vector3(-45f, -0.5f, 0f), new Vector3(70f, 1f, 100f), groundMaterial);
            CreateGroundBox(terrain.transform, "Ground East", new Vector3(45f, -0.5f, 0f), new Vector3(70f, 1f, 100f), groundMaterial);
            CreateGroundBox(terrain.transform, "Bridge", new Vector3(0f, -0.5f, 0f), new Vector3(24f, 1f, 8f), bridgeMaterial);

            // Visual-only canyon floor (default layer, not collected into the NavMesh),
            // sunk deep so the ravine reads as an obstacle rather than walkable ground.
            GameObject canyon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            canyon.name = "Canyon Floor (visual)";
            canyon.layer = 0;
            canyon.transform.SetParent(terrain.transform);
            canyon.transform.position = new Vector3(0f, -8f, 0f);
            canyon.transform.localScale = new Vector3(20f, 1f, 100f);
            canyon.GetComponent<Renderer>().sharedMaterial = canyonMaterial;
            Object.DestroyImmediate(canyon.GetComponent<Collider>());

            GameObject obstacles = new GameObject("Obstacles");
            obstacles.transform.SetParent(terrain.transform);

            // West side: big obstacles to navigate around on long routes.
            CreateObstacle(obstacles.transform, "West Block A", new Vector3(-55f, 3f, 18f), new Vector3(10f, 6f, 34f), obstacleMaterial);
            CreateObstacle(obstacles.transform, "West Block B", new Vector3(-33f, 3f, -22f), new Vector3(28f, 6f, 10f), obstacleMaterial);
            CreateObstacle(obstacles.transform, "West Pillar", new Vector3(-62f, 3f, -34f), new Vector3(12f, 6f, 12f), obstacleMaterial);

            // East approach to the bridge: two blocks forming a narrow funnel.
            CreateObstacle(obstacles.transform, "Bridge Funnel North", new Vector3(18f, 3f, 13f), new Vector3(10f, 6f, 18f), obstacleMaterial);
            CreateObstacle(obstacles.transform, "Bridge Funnel South", new Vector3(18f, 3f, -13f), new Vector3(10f, 6f, 18f), obstacleMaterial);

            // East side: a fully enclosed blocked zone (unreachable interior).
            GameObject blocked = new GameObject("Blocked Zone");
            blocked.transform.SetParent(obstacles.transform);
            CreateObstacle(blocked.transform, "Wall North", new Vector3(55f, 3f, 32f), new Vector3(32f, 6f, 4f), obstacleMaterial);
            CreateObstacle(blocked.transform, "Wall South", new Vector3(55f, 3f, 8f), new Vector3(32f, 6f, 4f), obstacleMaterial);
            CreateObstacle(blocked.transform, "Wall East", new Vector3(69f, 3f, 20f), new Vector3(4f, 6f, 28f), obstacleMaterial);
            CreateObstacle(blocked.transform, "Wall West", new Vector3(41f, 3f, 20f), new Vector3(4f, 6f, 28f), obstacleMaterial);
        }

        private static void CreateGroundBox(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = name;
            ground.layer = GroundLayer;
            ground.transform.SetParent(parent);
            ground.transform.position = center;
            ground.transform.localScale = size;
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateObstacle(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            // Tall cubes on the Ground layer: their steep sides exceed the agent slope,
            // so the NavMesh carves out their footprint and the agent routes around.
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = name;
            obstacle.layer = GroundLayer;
            obstacle.transform.SetParent(parent);
            obstacle.transform.position = center;
            obstacle.transform.localScale = size;
            obstacle.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void BuildDestinationMarkers(Material markerMaterial)
        {
            GameObject markers = new GameObject("Destination Markers");
            CreateMarker(markers.transform, "Marker West Corner", new Vector3(-74f, 0.4f, 42f), markerMaterial);
            CreateMarker(markers.transform, "Marker East Corner", new Vector3(74f, 0.4f, -42f), markerMaterial);
            CreateMarker(markers.transform, "Marker East Entrance", new Vector3(20f, 0.4f, -40f), markerMaterial);
            CreateMarker(markers.transform, "Marker Blocked (unreachable)", new Vector3(55f, 0.4f, 20f), markerMaterial);
        }

        private static void CreateMarker(Transform parent, string name, Vector3 position, Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.layer = 0;
            marker.transform.SetParent(parent);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(2.5f, 0.4f, 2.5f);
            marker.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        // ----- Units ----------------------------------------------------------

        private static GameObject CreateUnit(string name, Vector3 position, TeamId team, Material bodyMaterial, Material ringMaterial, Material healthBackgroundMaterial, Material healthFillMaterial, UnitDefinition definition, bool startSelected)
        {
            GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unit.name = name;
            unit.layer = SelectableLayer;
            unit.transform.position = position;
            unit.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

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

            // Both units are fully controllable so a distant target can be moved
            // manually to exercise dynamic long-range pursuit.
            unit.AddComponent<ClickMover>();
            unit.AddComponent<BasicAttack>();
            unit.AddComponent<UnitOrderController>();

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
            selectableUnit.SetSelected(startSelected);

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

        // ----- Support objects ------------------------------------------------

        private static void CreateNavMeshBootstrap()
        {
            GameObject bootstrapObject = new GameObject("Large NavMesh Bootstrap");
            LargeNavMeshBootstrap bootstrap = bootstrapObject.AddComponent<LargeNavMeshBootstrap>();

            SerializedObject serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("sourceMask").intValue = 1 << GroundLayer;
            serialized.FindProperty("mapCenter").vector3Value = new Vector3(0f, 0f, 0f);
            serialized.FindProperty("mapSize").vector3Value = new Vector3(180f, 30f, 120f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private static void CreateCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("Isometric Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 18f;
            camera.farClipPlane = 400f;
            cameraObject.transform.position = target.position + new Vector3(0f, 30f, -26f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            IsometricCameraRig rig = cameraObject.AddComponent<IsometricCameraRig>();
            rig.SetTarget(target);

            // Tune the M3A rig for the larger play area.
            SerializedObject rigObject = new SerializedObject(rig);
            rigObject.FindProperty("offset").vector3Value = new Vector3(0f, 30f, -26f);
            rigObject.FindProperty("panSpeed").floatValue = 45f;
            rigObject.FindProperty("minZoom").floatValue = 8f;
            rigObject.FindProperty("maxZoom").floatValue = 42f;
            rigObject.FindProperty("zoomStep").floatValue = 3f;
            rigObject.FindProperty("minX").floatValue = -90f;
            rigObject.FindProperty("maxX").floatValue = 90f;
            rigObject.FindProperty("minZ").floatValue = -60f;
            rigObject.FindProperty("maxZ").floatValue = 60f;
            rigObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateCommandController()
        {
            GameObject controller = new GameObject("Player Command Controller");
            PlayerCommandController commandController = controller.AddComponent<PlayerCommandController>();

            SerializedObject commandObject = new SerializedObject(commandController);
            commandObject.FindProperty("commandCamera").objectReferenceValue = Camera.main;
            commandObject.FindProperty("groundMask").intValue = 1 << GroundLayer;
            commandObject.FindProperty("selectableMask").intValue = 1 << SelectableLayer;
            commandObject.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----- Assets / helpers ----------------------------------------------

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

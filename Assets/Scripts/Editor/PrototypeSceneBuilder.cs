#if UNITY_EDITOR
using CierzoArena.CameraSystem;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CierzoArena.EditorTools
{
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/PrototypeArena.unity";
        private const int GroundLayer = 6;
        private const int SelectableLayer = 7;

        [MenuItem("Cierzo Arena/Create Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            CreatePrototypeScene(showConfirmation: true);
        }

        [InitializeOnLoadMethod]
        private static void CreateInitialSceneIfMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    CreatePrototypeScene(showConfirmation: false);
                    return;
                }

                EnsureSceneInBuildSettings();
            };
        }

        private static void CreatePrototypeScene(bool showConfirmation)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PrototypeArena";

            EnsureLayerName(GroundLayer, "Ground");
            EnsureLayerName(SelectableLayer, "Selectable");

            Material groundMaterial = CreateMaterial("Assets/Materials/Prototype_Ground.mat", new Color(0.24f, 0.31f, 0.29f));
            Material allyMaterial = CreateMaterial("Assets/Materials/Prototype_Azure.mat", new Color(0.08f, 0.35f, 0.9f));
            Material enemyMaterial = CreateMaterial("Assets/Materials/Prototype_Ember.mat", new Color(0.85f, 0.18f, 0.12f));
            Material ringMaterial = CreateMaterial("Assets/Materials/Prototype_Selection.mat", new Color(0.95f, 0.86f, 0.24f));
            Material healthBackgroundMaterial = CreateMaterial("Assets/Materials/Prototype_HealthBackground.mat", new Color(0.08f, 0.08f, 0.08f));
            Material healthFillMaterial = CreateMaterial("Assets/Materials/Prototype_HealthFill.mat", new Color(0.2f, 0.85f, 0.3f));

            CreateGround(groundMaterial);
            GameObject player = CreateUnit("Azure Vanguard", new Vector3(-4f, 1f, -2f), TeamId.Azure, allyMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, true, 500f);
            CreateUnit("Ember Target", new Vector3(4f, 1f, 1f), TeamId.Ember, enemyMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, false, 180f);
            CreateLighting();
            CreateCamera(player.transform);
            CreateCommandController();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();

            if (showConfirmation)
            {
                EditorUtility.DisplayDialog("Cierzo Arena", $"Prototype scene created at {ScenePath}", "OK");
            }
        }

        private static void CreateGround(Material material)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Prototype Ground";
            ground.layer = GroundLayer;
            ground.transform.localScale = new Vector3(3.5f, 1f, 3.5f);
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static GameObject CreateUnit(string name, Vector3 position, TeamId team, Material bodyMaterial, Material ringMaterial, Material healthBackgroundMaterial, Material healthFillMaterial, bool playerControlled, float maxHealth)
        {
            GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unit.name = name;
            unit.layer = SelectableLayer;
            unit.transform.position = position;
            unit.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            TeamMember teamMember = unit.AddComponent<TeamMember>();
            SerializedObject teamObject = new SerializedObject(teamMember);
            teamObject.FindProperty("team").enumValueIndex = (int)team;
            teamObject.ApplyModifiedPropertiesWithoutUndo();

            Health health = unit.AddComponent<Health>();
            SerializedObject healthObject = new SerializedObject(health);
            healthObject.FindProperty("maxHealth").floatValue = maxHealth;
            healthObject.ApplyModifiedPropertiesWithoutUndo();

            DamageFlash damageFlash = unit.AddComponent<DamageFlash>();
            SerializedObject flashObject = new SerializedObject(damageFlash);
            flashObject.FindProperty("targetRenderer").objectReferenceValue = unit.GetComponent<Renderer>();
            flashObject.ApplyModifiedPropertiesWithoutUndo();

            unit.AddComponent<DamageNumberSpawner>();
            CreateHealthBar(unit.transform, health, healthBackgroundMaterial, healthFillMaterial);

            if (playerControlled)
            {
                unit.AddComponent<ClickMover>();
                unit.AddComponent<BasicAttack>();
                unit.AddComponent<UnitOrderController>();
            }

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Selection Ring";
            ring.layer = SelectableLayer;
            ring.transform.SetParent(unit.transform);
            ring.transform.localPosition = new Vector3(0f, -0.72f, 0f);
            ring.transform.localScale = new Vector3(1.35f, 0.03f, 1.35f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMaterial;
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            SelectableUnit selectableUnit = unit.AddComponent<SelectableUnit>();
            SerializedObject selectableObject = new SerializedObject(selectableUnit);
            selectableObject.FindProperty("selectionRing").objectReferenceValue = ring.GetComponent<Renderer>();
            selectableObject.ApplyModifiedPropertiesWithoutUndo();
            selectableUnit.SetSelected(playerControlled);

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
            camera.orthographicSize = 9f;
            cameraObject.transform.position = target.position + new Vector3(0f, 14f, -12f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            IsometricCameraRig rig = cameraObject.AddComponent<IsometricCameraRig>();
            rig.SetTarget(target);
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

        private static Material CreateMaterial(string path, Color color)
        {
            Shader shader = Shader.Find("Standard");
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (existing.shader != shader)
                {
                    existing.shader = shader;
                    existing.color = color;
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            Material material = new Material(shader);
            material.color = color;
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
    }
}
#endif

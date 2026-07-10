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

            CreateGround(groundMaterial);
            GameObject player = CreateUnit("Azure Vanguard", new Vector3(-4f, 0.75f, -2f), TeamId.Azure, allyMaterial, ringMaterial, true);
            CreateUnit("Ember Target", new Vector3(5f, 0.75f, 2f), TeamId.Ember, enemyMaterial, ringMaterial, false);
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

        private static GameObject CreateUnit(string name, Vector3 position, TeamId team, Material bodyMaterial, Material ringMaterial, bool selectable)
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

            unit.AddComponent<Health>();
            unit.AddComponent<BasicAttack>();
            unit.AddComponent<ClickMover>();

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
            selectableUnit.SetSelected(selectable);

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
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
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
            scenes[^1] = new EditorBuildSettingsScene(ScenePath, true);
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

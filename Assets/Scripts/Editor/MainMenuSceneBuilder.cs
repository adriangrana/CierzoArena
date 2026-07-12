#if UNITY_EDITOR
using CierzoArena.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CierzoArena.EditorTools
{
    public static class MainMenuSceneBuilder
    {
        private const string ScenePath="Assets/Scenes/MainMenu.unity";
        [MenuItem("Cierzo Arena/Create Main Menu")]
        public static void CreateMainMenu()
        {
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);scene.name="MainMenu";
            EnsureFolder("Assets","Data");EnsureFolder("Assets/Data","Frontend");
            CierzoVisualTheme theme=LoadOrCreate<CierzoVisualTheme>("Assets/Data/Frontend/CierzoVisualTheme.asset");
            HeroPresentationDefinition azure=CreateHero("Assets/Data/Frontend/AzureVanguardPresentation.asset","Azure Vanguard","Vanguard","Melee",2,"A steadfast frontline prototype who turns pressure into space for the team.","Arc Bolt · Storm Mark · Gale Step · Tempest Fall");
            HeroPresentationDefinition ember=CreateHero("Assets/Data/Frontend/EmberSkirmisherPresentation.asset","Ember Skirmisher","Skirmisher","Ranged",2,"A nimble ranged prototype built to pressure lanes and reposition quickly.","Cinder Shot · Ash Veil · Copper Dash · Ember Nova");
            GameObject cameraObject=new GameObject("Main Menu Camera");Camera camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.07f);camera.orthographic=true;camera.orthographicSize=5;
            GameObject lightObject=new GameObject("Menu Key Light");Light light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.transform.rotation=Quaternion.Euler(50,-35,0);
            GameObject canvasObject=new GameObject("Main Menu Canvas");Canvas canvas=canvasObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;CanvasScaler scaler=canvasObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;canvasObject.AddComponent<GraphicRaycaster>();
            GameObject eventSystem=new GameObject("EventSystem");eventSystem.AddComponent<EventSystem>();eventSystem.AddComponent<StandaloneInputModule>();
            GameObject controllerObject=new GameObject("Main Menu Controller");MainMenuController controller=controllerObject.AddComponent<MainMenuController>();SerializedObject data=new SerializedObject(controller);data.FindProperty("theme").objectReferenceValue=theme;SerializedProperty heroes=data.FindProperty("heroes");heroes.arraySize=2;heroes.GetArrayElementAtIndex(0).objectReferenceValue=azure;heroes.GetArrayElementAtIndex(1).objectReferenceValue=ember;data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene,ScenePath);EnsureBuildSettings();EditorUtility.DisplayDialog("Cierzo Arena","MainMenu created and set as the first build scene.","OK");
        }
        private static HeroPresentationDefinition CreateHero(string path,string name,string role,string style,int difficulty,string description,string abilities)
        {
            HeroPresentationDefinition asset=LoadOrCreate<HeroPresentationDefinition>(path);SerializedObject data=new SerializedObject(asset);data.FindProperty("heroName").stringValue=name;data.FindProperty("role").stringValue=role;data.FindProperty("combatStyle").stringValue=style;data.FindProperty("difficulty").intValue=difficulty;data.FindProperty("description").stringValue=description;data.FindProperty("abilities").stringValue=abilities;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);return asset;
        }
        private static T LoadOrCreate<T>(string path) where T:ScriptableObject{T value=AssetDatabase.LoadAssetAtPath<T>(path);if(value!=null)return value;value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);return value;}
        private static void EnsureFolder(string parent,string name){if(!AssetDatabase.IsValidFolder(parent+"/"+name))AssetDatabase.CreateFolder(parent,name);}
        private static void EnsureBuildSettings()
        {
            var existing=EditorBuildSettings.scenes;var next=new System.Collections.Generic.List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(ScenePath,true)};foreach(var item in existing)if(item.path!=ScenePath)next.Add(item);EditorBuildSettings.scenes=next.ToArray();
        }
    }
}
#endif

#if UNITY_EDITOR
using CierzoArena.Units;
using UnityEditor;
using UnityEngine;

namespace CierzoArena.EditorTools
{
    /// <summary>
    /// Keeps the data-driven hero visual catalogue usable in development by ensuring
    /// the first authored visual (Storm Warden) exists after script reload. Without
    /// these assets the runtime resolver correctly falls back to the placeholder.
    /// </summary>
    [InitializeOnLoad]
    public static class HeroVisualAssetBootstrap
    {
        private static bool checkedThisReload;

        static HeroVisualAssetBootstrap()
        {
            EditorApplication.delayCall += EnsureStormWardenAssets;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) EnsureStormWardenAssets();
        }

        private static void EnsureStormWardenAssets()
        {
            if (checkedThisReload) return;
            checkedThisReload = true;

            bool missing = false;
            HeroVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<HeroVisualCatalog>(StormWardenVisualAssetBuilder.CatalogPath);
            if (catalog == null)
            {
                missing = true;
            }
            else if (!catalog.TryGet(StormWardenVisualAssetBuilder.HeroId, out HeroVisualDefinition visual) ||
                     visual == null || visual.VisualPrefab == null)
            {
                missing = true;
            }

            if (!missing) return;

            try
            {
                StormWardenVisualAssetBuilder.Build();
                Debug.Log("Hero visual bootstrap: generated missing Storm Warden visual assets.");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Hero visual bootstrap could not generate Storm Warden assets: {exception.Message}");
            }
        }
    }
}
#endif
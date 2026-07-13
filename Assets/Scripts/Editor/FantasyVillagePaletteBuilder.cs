#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CierzoArena.Environment;
using UnityEditor;
using UnityEngine;

namespace CierzoArena.EditorTools
{
    /// <summary>
    /// Editor-only, deterministic populate step for the M23 Fantasy Village palette.
    /// It locates a curated subset of the OccaSoftware "Low Poly Fantasy Village
    /// Environment" prefabs by path and writes serialized references into a palette
    /// asset. AssetDatabase is used only here, in the editor; builders and runtime
    /// consume the resulting serialized references. Original package assets are never
    /// modified.
    /// </summary>
    public static class FantasyVillagePaletteBuilder
    {
        public const string PackagePrefabRoot = "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs";
        public const string PaletteAssetPath = "Assets/CierzoArena/Settings/Environment/FantasyVillageEnvironmentPalette.asset";
        public const string LayoutAssetPath = "Assets/CierzoArena/Settings/Environment/TeamBaseLayoutDefinition.asset";

        [MenuItem("Cierzo Arena/Environment/Build Fantasy Village Palette")]
        public static void BuildPaletteMenu()
        {
            FantasyVillageEnvironmentPalette palette = EnsurePalette(out string report);
            EditorUtility.DisplayDialog("Cierzo Arena",
                palette == null
                    ? "Package prefabs not found under\n" + PackagePrefabRoot
                    : "Fantasy Village palette written to\n" + PaletteAssetPath + "\n\n" + report,
                "OK");
        }

        /// <summary>Creates (or refreshes) and returns the populated palette asset.
        /// Returns null if the package prefab folder is absent.</summary>
        public static FantasyVillageEnvironmentPalette EnsurePalette(out string report)
        {
            report = string.Empty;
            if (!AssetDatabase.IsValidFolder(PackagePrefabRoot))
            {
                report = "Package prefab folder not found: " + PackagePrefabRoot;
                return null;
            }

            EnsureFolder("Assets", "CierzoArena");
            EnsureFolder("Assets/CierzoArena", "Settings");
            EnsureFolder("Assets/CierzoArena/Settings", "Environment");

            FantasyVillageEnvironmentPalette palette = AssetDatabase.LoadAssetAtPath<FantasyVillageEnvironmentPalette>(PaletteAssetPath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<FantasyVillageEnvironmentPalette>();
                AssetDatabase.CreateAsset(palette, PaletteAssetPath);
            }

            // Deterministic curated selection (not simply the first of each list),
            // chosen for silhouette/footprint/legibility from a top-down MOBA camera.
            palette.SetAll(
                main: Load("House_3"),
                secondary: LoadMany("House_1", "House_2"),
                houses: LoadMany("House_1", "House_2", "House_3"),
                straight: Load("Path_1"),
                pieces: LoadMany("Path Piece_1", "Path Piece_5", "Path Piece_9"),
                bridgePrefab: Load("Bridge"),
                treeSet: LoadMany("Tree_2", "Tree_5", "Tree_7"),
                pineSet: LoadMany("Pine Tree_2", "Pine Tree_4"),
                flowerSet: LoadMany("Flower_1", "Flower_3"),
                pot: Load("Flower Pot"),
                cliffSet: LoadMany("Cliff_3", "Cliff_6", "Cliff_8"),
                mountainSet: LoadMany("Mountain_2", "Mountain_4"),
                rockSet: LoadMany("Rock_2", "Rock_3"),
                benchPrefab: Load("Bench"),
                cratePrefab: Load("Crate"),
                fencePrefab: Load("Fence"),
                lanternPrefab: Load("Lantern"),
                boatPrefab: Load("Boat"));

            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            palette.Validate(out report);
            return palette;
        }

        private static GameObject Load(string prefabName)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PackagePrefabRoot}/{prefabName}.prefab");
        }

        private static GameObject[] LoadMany(params string[] names)
        {
            List<GameObject> list = new List<GameObject>();
            foreach (string n in names)
            {
                GameObject go = Load(n);
                if (go != null) list.Add(go);
            }
            return list.ToArray();
        }

        /// <summary>Creates (or returns) the shared base layout definition asset with
        /// its serialized default offsets. Both bases use this single definition.</summary>
        public static TeamBaseLayoutDefinition EnsureLayout()
        {
            EnsureFolder("Assets", "CierzoArena");
            EnsureFolder("Assets/CierzoArena", "Settings");
            EnsureFolder("Assets/CierzoArena/Settings", "Environment");
            TeamBaseLayoutDefinition layout = AssetDatabase.LoadAssetAtPath<TeamBaseLayoutDefinition>(LayoutAssetPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<TeamBaseLayoutDefinition>();
                AssetDatabase.CreateAsset(layout, LayoutAssetPath);
                AssetDatabase.SaveAssets();
            }
            return layout;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
#endif

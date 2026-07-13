#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CierzoArena.EditorTools
{
    /// <summary>
    /// Editor-only material remapper for the M23 environment. The OccaSoftware
    /// "Low Poly Fantasy Village" package ships URP/Lit materials (their colour atlas
    /// is bound to the URP-only <c>_BaseMap</c> slot), but this project runs the
    /// Built-in pipeline: URP/Lit is unsupported there, so the Standard fallback reads
    /// the empty <c>_MainTex</c> and renders everything solid white.
    ///
    /// This class produces, once per source material, a Built-in <b>Standard</b>
    /// variant that binds the same atlas texture to <c>_MainTex</c> and copies colour,
    /// emission and normal, so the package renders with its authored colours. The
    /// original package materials are never modified; variants are saved under a
    /// CierzoArena folder. Instantiated package renderers are then re-pointed to the
    /// variant. This is a pipeline-compatibility fix, not a colour override.
    /// </summary>
    public static class FantasyVillageMaterialRemapper
    {
        public const string VariantFolder = "Assets/CierzoArena/Art/Environment/FantasyVillage/Materials";
        private const string PackageMaterialRoot = "Assets/OccaSoftware/Low Poly Fantasy Village/Materials";
        private const string PackageGradientPath = "Assets/OccaSoftware/Low Poly Fantasy Village/Textures/Gradient.png";

        private static readonly Dictionary<Material, Material> Cache = new Dictionary<Material, Material>();
        private const string MappingPrefix = "CierzoOriginalMaterialGuid=";

        /// <summary>True when a material uses a shader that the active (Built-in)
        /// pipeline cannot render, i.e. an unsupported URP shader.</summary>
        public static bool NeedsRemap(Material material)
        {
            if (material == null) return false;
            Shader shader = material.shader;
            if (shader == null) return true;
            if (!shader.isSupported) return true;
            string name = shader.name;
            return name.StartsWith("Universal Render Pipeline/") || name.StartsWith("URP/") || name.Contains("Shader Graphs/");
        }

        /// <summary>Walks a freshly instantiated package object and re-points every
        /// renderer material that needs remapping to its Built-in Standard variant.</summary>
        public static void RemapInstance(GameObject instance)
        {
            if (instance == null) return;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (NeedsRemap(shared[i]))
                    {
                        shared[i] = GetVariant(shared[i]);
                        changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = shared;
            }
        }

        /// <summary>Returns (creating if needed) the Built-in Standard equivalent of a
        /// package material. Cached in-memory and persisted as an asset so regenerating
        /// the scene reuses the same variant deterministically.</summary>
        public static Material GetVariant(Material source)
        {
            if (source == null) return null;
            if (Cache.TryGetValue(source, out Material cached) && cached != null) return cached;

            EnsureFolder();
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            // Names are not a stable key: Color.mat and Light.mat are common in
            // third-party packages. The source GUID is the persisted mapping key.
            string safeGuid = string.IsNullOrEmpty(sourceGuid) ? SafeName(source.name) : sourceGuid;
            string assetPath = $"{VariantFolder}/{safeGuid}_BuiltIn.mat";
            Material variant = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (variant == null)
            {
                Shader standard = Shader.Find("Standard");
                variant = new Material(standard) { name = source.name + "_BuiltIn" };
                AssetDatabase.CreateAsset(variant, assetPath);
            }

            CopyProperties(source, variant);
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer != null && !string.IsNullOrEmpty(sourceGuid))
            {
                importer.userData = MappingPrefix + sourceGuid;
                AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            }
            EditorUtility.SetDirty(variant);
            AssetDatabase.SaveAssets();
            Cache[source] = variant;
            return variant;
        }

        private static void CopyProperties(Material source, Material variant)
        {
            // Base colour atlas: URP _BaseMap -> Standard _MainTex (also try _MainTex).
            // When the source shader is unsupported, Material.HasProperty can lie
            // even though Unity still serializes its URP properties. Read the saved
            // material property directly as a robust fallback; this is the missing
            // link which previously produced Color_BuiltIn with a null _MainTex.
            Texture baseMap = GetTexture(source, "_BaseMap")
                              ?? GetTexture(source, "_MainTex")
                              ?? GetSerializedTexture(source, "_BaseMap")
                              ?? GetSerializedTexture(source, "_MainTex")
                              ?? GetYamlTexture(source, "_BaseMap")
                              ?? GetYamlTexture(source, "_MainTex")
                              ?? GetPackageGradientFallback(source);
            variant.SetTexture("_MainTex", baseMap);

            Color baseColor = GetColor(source, "_BaseColor",
                GetColor(source, "_Color", GetSerializedColor(source, "_BaseColor", GetSerializedColor(source, "_Color", Color.white))));
            variant.SetColor("_Color", baseColor);

            Texture bump = GetTexture(source, "_BumpMap") ?? GetSerializedTexture(source, "_BumpMap");
            if (bump != null) { variant.SetTexture("_BumpMap", bump); variant.EnableKeyword("_NORMALMAP"); }

            Color emission = GetColor(source, "_EmissionColor", GetSerializedColor(source, "_EmissionColor", Color.black));
            if (emission.maxColorComponent > 0.001f)
            {
                variant.SetColor("_EmissionColor", emission);
                variant.EnableKeyword("_EMISSION");
                variant.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (source.HasProperty("_Smoothness")) variant.SetFloat("_Glossiness", source.GetFloat("_Smoothness"));
            else if (source.HasProperty("_Glossiness")) variant.SetFloat("_Glossiness", source.GetFloat("_Glossiness"));
            if (source.HasProperty("_Metallic")) variant.SetFloat("_Metallic", source.GetFloat("_Metallic"));
        }

        private static Texture GetTexture(Material m, string prop) => m.HasProperty(prop) ? m.GetTexture(prop) : null;
        private static Color GetColor(Material m, string prop, Color fallback) => m.HasProperty(prop) ? m.GetColor(prop) : fallback;

        private static Texture GetSerializedTexture(Material material, string propertyName)
        {
            SerializedObject serialized = new SerializedObject(material);
            SerializedProperty entries = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
            if (entries == null) return null;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue != propertyName) continue;
                return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            }
            return null;
        }

        /// <summary>
        /// Unity 6 can omit the URP property from SerializedObject when the active
        /// pipeline is Built-in. The material file remains authoritative and safely
        /// exposes the GUID, so use it as the final editor-only fallback. This is not
        /// a name lookup and does not write to the source material.
        /// </summary>
        private static Texture GetYamlTexture(Material material, string propertyName)
        {
            string path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            string escapedProperty = Regex.Escape(propertyName);
            string yaml = File.ReadAllText(path);
            Match match = Regex.Match(
                yaml,
                @"^\s*-\s+" + escapedProperty + @":\s*\r?\n\s*m_Texture:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+),\s*type:\s*\d+\}",
                RegexOptions.Multiline);
            if (!match.Success) return null;
            string texturePath = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
            return string.IsNullOrEmpty(texturePath) ? null : AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
        }

        private static Texture GetPackageGradientFallback(Material material)
        {
            // Color.mat contains a stale external texture GUID in this installed
            // package. The only atlas shipped locally is Gradient.png (which
            // Light.mat also references), so it is the safe package-local fallback.
            // This branch intentionally applies only to the audited package and only
            // when every direct/serialized source lookup failed.
            string sourcePath = AssetDatabase.GetAssetPath(material);
            if (!sourcePath.StartsWith(PackageMaterialRoot)) return null;
            return AssetDatabase.LoadAssetAtPath<Texture>(PackageGradientPath);
        }

        private static Color GetSerializedColor(Material material, string propertyName, Color fallback)
        {
            SerializedObject serialized = new SerializedObject(material);
            SerializedProperty entries = serialized.FindProperty("m_SavedProperties.m_Colors");
            if (entries == null) return fallback;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == propertyName)
                    return entry.FindPropertyRelative("second").colorValue;
            }
            return fallback;
        }

        public static bool IsVariantFor(Material original, Material variant)
        {
            if (original == null || variant == null) return false;
            string originalGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
            AssetImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(variant));
            return !string.IsNullOrEmpty(originalGuid) && importer != null && importer.userData == MappingPrefix + originalGuid;
        }

        /// <summary>Returns the source package material recorded by a generated
        /// variant. This makes material validation inspect the actual persistent
        /// GUID mapping instead of guessing from a material name.</summary>
        public static bool TryGetOriginalMaterial(Material variant, out Material original)
        {
            original = null;
            if (variant == null) return false;
            AssetImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(variant));
            if (importer == null || string.IsNullOrEmpty(importer.userData) || !importer.userData.StartsWith(MappingPrefix)) return false;
            string guid = importer.userData.Substring(MappingPrefix.Length);
            original = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            return original != null;
        }

        /// <summary>Extracts the main atlas even when Unity cannot evaluate the
        /// source URP shader in the active Built-in project.</summary>
        public static Texture GetSourceMainTexture(Material source)
        {
            return source == null ? null : GetTexture(source, "_BaseMap")
                ?? GetTexture(source, "_MainTex")
                ?? GetSerializedTexture(source, "_BaseMap")
                ?? GetSerializedTexture(source, "_MainTex")
                ?? GetYamlTexture(source, "_BaseMap")
                ?? GetYamlTexture(source, "_MainTex")
                ?? GetPackageGradientFallback(source);
        }

        public static Color GetSourceBaseColor(Material source)
        {
            return source == null ? Color.white : GetColor(source, "_BaseColor",
                GetColor(source, "_Color", GetSerializedColor(source, "_BaseColor", GetSerializedColor(source, "_Color", Color.white))));
        }

        [MenuItem("Cierzo Arena/Environment/Rebuild Fantasy Village Material Variants")]
        public static void RebuildVariantsMenu()
        {
            int count = RebuildVariants();
            EditorUtility.DisplayDialog("Cierzo Arena", $"Rebuilt {count} Built-in Fantasy Village material variants.", "OK");
        }

        /// <summary>Refreshes every source material in the package. It is safe to run
        /// repeatedly and repairs variants produced before the serialized _BaseMap
        /// fallback was available.</summary>
        public static int RebuildVariants()
        {
            Cache.Clear();
            RemoveLegacyNameBasedVariants();
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/OccaSoftware/Low Poly Fantasy Village/Materials" }))
            {
                Material source = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (source == null) continue;
                GetVariant(source);
                count++;
            }
            AssetDatabase.SaveAssets();
            return count;
        }

        /// <summary>
        /// M23 initially used material names as the cache key, producing variants
        /// such as Color_BuiltIn.mat. Material names are not unique across packages
        /// and those files also predate the _BaseMap repair. Remove only those
        /// unmapped, name-based variants; GUID-mapped variants are preserved and
        /// reused on every subsequent regeneration.
        /// </summary>
        private static void RemoveLegacyNameBasedVariants()
        {
            if (!AssetDatabase.IsValidFolder(VariantFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { VariantFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material candidate = AssetDatabase.LoadAssetAtPath<Material>(path);
                AssetImporter importer = AssetImporter.GetAtPath(path);
                bool hasGuidMapping = importer != null && importer.userData.StartsWith(MappingPrefix);
                if (candidate != null && !hasGuidMapping && path.EndsWith("_BuiltIn.mat"))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        [MenuItem("Cierzo Arena/Environment/Compare Original And Remapped Prefab")]
        public static void CompareOriginalAndRemappedPrefabMenu()
        {
            string report = ComparePrefab("House_3");
            Debug.Log(report);
            EditorUtility.DisplayDialog("Cierzo Arena - Material comparison", report, "OK");
        }

        /// <summary>Creates an original and a remapped temporary instance side by
        /// side, logs every material slot, then destroys both immediately.</summary>
        public static string ComparePrefab(string prefabName)
        {
            string path = $"{FantasyVillagePaletteBuilder.PackagePrefabRoot}/{prefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return "FAIL: prefab not found: " + path;

            GameObject original = null;
            GameObject remapped = null;
            try
            {
                original = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                remapped = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                original.transform.position = new Vector3(-3f, 0f, 0f);
                remapped.transform.position = new Vector3(3f, 0f, 0f);
                RemapInstance(remapped);

                Renderer[] beforeRenderers = original.GetComponentsInChildren<Renderer>(true);
                Renderer[] afterRenderers = remapped.GetComponentsInChildren<Renderer>(true);
                var report = new System.Text.StringBuilder();
                report.AppendLine($"M23 comparison: {prefabName}; renderers original/remapped = {beforeRenderers.Length}/{afterRenderers.Length}");
                for (int i = 0; i < Mathf.Min(beforeRenderers.Length, afterRenderers.Length); i++)
                {
                    Material[] before = beforeRenderers[i].sharedMaterials;
                    Material[] after = afterRenderers[i].sharedMaterials;
                    report.AppendLine($"[{i}] {beforeRenderers[i].name}: slots {before.Length}/{after.Length}");
                    for (int slot = 0; slot < Mathf.Min(before.Length, after.Length); slot++)
                    {
                        Material source = before[slot];
                        Material variant = after[slot];
                        Texture directSourceAtlas = source == null ? null : GetTexture(source, "_BaseMap") ?? GetSerializedTexture(source, "_BaseMap") ?? GetYamlTexture(source, "_BaseMap") ?? GetTexture(source, "_MainTex") ?? GetSerializedTexture(source, "_MainTex") ?? GetYamlTexture(source, "_MainTex");
                        Texture sourceAtlas = GetSourceMainTexture(source);
                        Texture finalAtlas = variant != null && variant.HasProperty("_MainTex") ? variant.GetTexture("_MainTex") : null;
                        string fallback = directSourceAtlas == null && sourceAtlas != null ? " (package-local atlas fallback)" : string.Empty;
                        report.AppendLine($"  [{slot}] {source?.name ?? "<null>"} ({source?.shader?.name ?? "<missing>"}) -> {variant?.name ?? "<null>"} ({variant?.shader?.name ?? "<missing>"}); effective atlas {sourceAtlas?.name ?? "<null>"}/{finalAtlas?.name ?? "<null>"}{fallback}; GUID mapped={IsVariantFor(source, variant)}");
                    }
                }
                return report.ToString();
            }
            finally
            {
                if (original != null) Object.DestroyImmediate(original);
                if (remapped != null) Object.DestroyImmediate(remapped);
            }
        }

        private static string SafeName(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrEmpty(name) ? "Material" : name;
        }

        private static void EnsureFolder()
        {
            string[] parts = { "Assets", "CierzoArena", "Art", "Environment", "FantasyVillage", "Materials" };
            string path = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = path + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }
    }
}
#endif

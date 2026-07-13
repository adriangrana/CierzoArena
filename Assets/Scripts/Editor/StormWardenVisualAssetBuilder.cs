#if UNITY_EDITOR
using CierzoArena.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CierzoArena.EditorTools
{
    /// <summary>Creates the non-destructive Built-in material, visual-only wrapper
    /// prefab and data-driven catalogue entry for the static Storm Warden model.</summary>
    public static class StormWardenVisualAssetBuilder
    {
        public const string HeroId = "storm_warden";
        public const string SourceRoot = "Assets/Resources/Art/Heroes/StormWarden";
        public const string ModelPath = SourceRoot + "/Models/Meshy_AI_Redshield_Warrior_0713162419_texture.fbx";
        public const string AlbedoPath = SourceRoot + "/Textures/Meshy_AI_Redshield_Warrior_0713162419_texture.png";
        public const string MetallicPath = SourceRoot + "/Textures/Meshy_AI_Redshield_Warrior_0713162419_texture_metallic.png";
        public const string NormalPath = SourceRoot + "/Textures/Meshy_AI_Redshield_Warrior_0713162419_texture_normal.png";
        public const string OutputRoot = "Assets/Art/Heroes/StormWarden";
        public const string MaterialPath = OutputRoot + "/Materials/MAT_StormWarden.mat";
        public const string PrefabPath = OutputRoot + "/Prefabs/StormWardenVisual.prefab";
        public const string CatalogPath = "Assets/Resources/Heroes/HeroVisualCatalog.asset";
        private const float TargetHeightMeters = 2.15f;
        private const float ManualSmoothness = 0.33f;

        [MenuItem("Cierzo Arena/Heroes/Build Storm Warden Static Visual")]
        public static void Build()
        {
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Heroes");
            EnsureFolder("Assets/Art/Heroes", "StormWarden");
            EnsureFolder(OutputRoot, "Materials");
            EnsureFolder(OutputRoot, "Prefabs");
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Heroes");

            ConfigureImporters();
            Material material = CreateOrUpdateMaterial();
            GameObject prefab = CreateOrUpdatePrefab(material);
            CreateOrUpdateCatalog(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Storm Warden visual built: {PrefabPath}");
        }

        private static void ConfigureImporters()
        {
            ModelImporter model = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (model == null) throw new System.InvalidOperationException($"Storm Warden model missing: {ModelPath}");
            model.importCameras = false;
            model.importLights = false;
            model.isReadable = false;
            model.addCollider = false;
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.SaveAndReimport();

            TextureImporter normal = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (normal == null) throw new System.InvalidOperationException($"Storm Warden normal map missing: {NormalPath}");
            normal.textureType = TextureImporterType.NormalMap;
            normal.sRGBTexture = false;
            normal.SaveAndReimport();
        }

        private static Material CreateOrUpdateMaterial()
        {
            Shader standard = Shader.Find("Standard");
            if (standard == null) throw new System.InvalidOperationException("Built-in Standard shader is unavailable.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(standard) { name = "MAT_StormWarden" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = standard;
            material.SetTexture("_MainTex", RequiredAsset<Texture2D>(AlbedoPath));
            material.SetTexture("_BumpMap", RequiredAsset<Texture2D>(NormalPath));
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", RequiredAsset<Texture2D>(MetallicPath));
            material.EnableKeyword("_METALLICGLOSSMAP");
            material.SetFloat("_Metallic", 1f);
            // The supplied roughness map is intentionally left untouched. For this
            // first static pass Standard uses the existing metallic map plus a stable
            // manual smoothness (1 - roughness packing can be added offline later).
            material.SetFloat("_Glossiness", ManualSmoothness);
            material.SetFloat("_Mode", 0f);
            material.renderQueue = (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateOrUpdatePrefab(Material material)
        {
            GameObject modelAsset = RequiredAsset<GameObject>(ModelPath);
            GameObject root = new GameObject("StormWardenVisual");
            try
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
                modelInstance.name = "ModelRoot";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;
                ConfigureRenderers(modelInstance, material);
                RemoveColliders(modelInstance);

                Bounds unscaled = RendererBounds(modelInstance);
                float scale = TargetHeightMeters / Mathf.Max(0.001f, unscaled.size.y);
                modelInstance.transform.localScale = Vector3.one * scale;
                Bounds scaled = RendererBounds(modelInstance);
                modelInstance.transform.localPosition = new Vector3(-scaled.center.x, -scaled.min.y, -scaled.center.z);
                Bounds finalBounds = RendererBounds(modelInstance);

                HeroVisualPrefabMetadata metadata = root.AddComponent<HeroVisualPrefabMetadata>();
                metadata.Configure(finalBounds, modelInstance.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return RequiredAsset<GameObject>(PrefabPath);
        }

        private static void CreateOrUpdateCatalog(GameObject prefab)
        {
            HeroVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<HeroVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HeroVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            HeroVisualDefinition stormWarden = new HeroVisualDefinition();
            stormWarden.Configure(HeroId, prefab, Vector3.zero, Vector3.zero, Vector3.one, hide: true);
            catalog.SetVisuals(new[] { stormWarden });
            EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureRenderers(GameObject root, Material material)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            }
        }

        private static void RemoveColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        }

        private static Bounds RendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new System.InvalidOperationException("Storm Warden model has no renderers.");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static T RequiredAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new System.InvalidOperationException($"Required asset missing: {path}");
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
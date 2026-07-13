#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CierzoArena.EditorTools
{
    /// <summary>Single Built-in-compatible source of truth for environment imports
    /// and shared materials. It deliberately uses no Resources.Load at runtime.</summary>
    [InitializeOnLoad]
    public static class EnvironmentArtPipeline
    {
        public const string RockyDiffusePath = "Assets/Resources/Art/Environment/Materials/RockyTerrain/rocky_terrain_02_diff_4k.jpg";
        public const string RockyNormalPath = "Assets/Resources/Art/Environment/Materials/RockyTerrain/rocky_terrain_02_nor_gl_4k.exr";
        public const string RockyRoughnessPath = "Assets/Resources/Art/Environment/Materials/RockyTerrain/rocky_terrain_02_rough_4k.exr";
        public const string ConcreteDiffusePath = "Assets/Resources/Art/Environment/Materials/ConcreteWall/concrete_wall_009_diff_4k.jpg";
        public const string ConcreteNormalPath = "Assets/Resources/Art/Environment/Materials/ConcreteWall/concrete_wall_009_nor_gl_4k.exr";
        public const string ConcreteRoughnessPath = "Assets/Resources/Art/Environment/Materials/ConcreteWall/concrete_wall_009_rough_4k.exr";
        public const string WaterBasePath = "Assets/Stylize Water Texture/Textures/Vol_36_5_Base_Color.png";
        public const string WaterNormalPath = "Assets/Stylize Water Texture/Textures/Vol_36_5_Normal.png";
        public const string WaterRoughnessPath = "Assets/Stylize Water Texture/Textures/Vol_36_5_Roughness.png";

        private const string MaterialRoot = "Assets/Art/Environment/Materials";
        public const string RockyMaterialPath = MaterialRoot + "/MAT_RockyTerrain_02.mat";
        public const string ConcreteMaterialPath = MaterialRoot + "/MAT_ConcreteWall_01.mat";
        public const string WaterMaterialPath = MaterialRoot + "/MAT_CierzoWater.mat";

        public readonly struct ArtSet
        {
            public readonly Material Rocky;
            public readonly Material Concrete;
            public readonly Material Water;
            public ArtSet(Material rocky, Material concrete, Material water) { Rocky=rocky; Concrete=concrete; Water=water; }
        }

        static EnvironmentArtPipeline()
        {
            // Material assets are generated deterministically on the next editor
            // refresh; rebuilding the scene itself remains an explicit menu action.
            EditorApplication.delayCall += () => EnsureAssets();
        }

        [MenuItem("Cierzo Arena/Environment/Repair Imports and Materials")]
        public static void RepairImportsAndMaterials() => EnsureAssets();

        public static ArtSet EnsureAssets()
        {
            EnsureFolder("Assets", "Art"); EnsureFolder("Assets/Art", "Environment"); EnsureFolder("Assets/Art/Environment", "Materials");
            ConfigureTexture(RockyDiffusePath, false, false); ConfigureTexture(RockyNormalPath, true, true); ConfigureTexture(RockyRoughnessPath, false, true);
            ConfigureTexture(ConcreteDiffusePath, false, false); ConfigureTexture(ConcreteNormalPath, true, true); ConfigureTexture(ConcreteRoughnessPath, false, true);
            ConfigureTexture(WaterBasePath, false, false); ConfigureTexture(WaterNormalPath, true, true); ConfigureTexture(WaterRoughnessPath, false, true);

            Material rocky = EnsureLitMaterial(RockyMaterialPath, RockyDiffusePath, RockyNormalPath, new Color(.68f,.70f,.66f), .23f, new Vector2(6f,6f));
            Material concrete = EnsureLitMaterial(ConcreteMaterialPath, ConcreteDiffusePath, ConcreteNormalPath, new Color(.64f,.67f,.68f), .31f, new Vector2(4f,4f));
            Material water = EnsureWaterMaterial();
            AssetDatabase.SaveAssets();
            return new ArtSet(rocky, concrete, water);
        }

        public static bool HasValidMaterial(Material material) => material != null && material.shader != null && material.shader.isSupported && material.shader.name != "Hidden/InternalErrorShader";

        private static Material EnsureLitMaterial(string path, string diffusePath, string normalPath, Color tint, float smoothness, Vector2 tiling)
        {
            Shader standard = Shader.Find("Standard");
            if (standard == null) throw new System.InvalidOperationException("Built-in Standard shader was not found.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(standard) { name = System.IO.Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(material,path); }
            if (material.shader != standard) material.shader = standard;
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath); Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            material.SetTexture("_MainTex", diffuse); material.SetTextureScale("_MainTex", tiling); material.color = tint;
            material.SetFloat("_Metallic", 0f); material.SetFloat("_Glossiness", smoothness);
            material.SetTexture("_BumpMap", normal); if(normal != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material); return material;
        }

        private static Material EnsureWaterMaterial()
        {
            // The river is a textured opaque surface. Transparent Standard
            // materials are sorted per renderer and can incorrectly appear over
            // bridge geometry from an isometric camera. Opaque depth writes keep
            // the water below the deck reliably on every supported resolution.
            Material material = EnsureLitMaterial(WaterMaterialPath, WaterBasePath, WaterNormalPath, new Color(.16f,.46f,.62f,1f), .72f, new Vector2(4f,4f));
            material.SetFloat("_Mode", 0f); material.SetOverrideTag("RenderType", "Opaque");
            material.SetInt("_SrcBlend", (int)BlendMode.One); material.SetInt("_DstBlend", (int)BlendMode.Zero); material.SetInt("_ZWrite", 1);
            material.renderQueue = (int)RenderQueue.Geometry; material.DisableKeyword("_ALPHABLEND_ON"); material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            EditorUtility.SetDirty(material); return material;
        }

        private static void ConfigureTexture(string path, bool normal, bool linear)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !linear; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 4; importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Compressed; importer.compressionQuality = 70;
            if (normal) importer.flipGreenChannel = false;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif

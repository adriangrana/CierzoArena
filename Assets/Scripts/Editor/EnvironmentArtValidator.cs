#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CierzoArena.EditorTools
{
    public static class EnvironmentArtValidator
    {
        [MenuItem("Cierzo Arena/Environment/Validate Art Pipeline")]
        public static void ValidateMenu()
        {
            List<string> errors = Validate();
            if (errors.Count > 0) throw new System.InvalidOperationException(string.Join("\n", errors));
            Debug.Log("[M21] Environment art pipeline validation passed.");
        }

        public static List<string> Validate()
        {
            EnvironmentArtPipeline.ArtSet set = EnvironmentArtPipeline.EnsureAssets();
            List<string> errors = new List<string>();
            ValidateMaterial("RockyTerrain", set.Rocky, errors); ValidateMaterial("ConcreteWall", set.Concrete, errors); ValidateMaterial("CierzoWater", set.Water, errors);
            ValidateTexture(EnvironmentArtPipeline.RockyDiffusePath, false, false, errors); ValidateTexture(EnvironmentArtPipeline.RockyNormalPath, true, true, errors); ValidateTexture(EnvironmentArtPipeline.RockyRoughnessPath, false, true, errors);
            ValidateTexture(EnvironmentArtPipeline.ConcreteDiffusePath, false, false, errors); ValidateTexture(EnvironmentArtPipeline.ConcreteNormalPath, true, true, errors); ValidateTexture(EnvironmentArtPipeline.WaterNormalPath, true, true, errors);
            return errors;
        }

        private static void ValidateMaterial(string name, Material material, List<string> errors)
        {
            if (!EnvironmentArtPipeline.HasValidMaterial(material)) errors.Add(name + " has an invalid or unsupported shader.");
            else if (material.GetTexture("_MainTex") == null) errors.Add(name + " is missing its base texture.");
        }

        private static void ValidateTexture(string path, bool normal, bool linear, List<string> errors)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { errors.Add("Missing texture importer: " + path); return; }
            if (importer.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default)) errors.Add("Incorrect texture type: " + path);
            if (importer.sRGBTexture != !linear) errors.Add("Incorrect sRGB setting: " + path);
            if (importer.maxTextureSize > 2048) errors.Add("Texture budget exceeded: " + path);
        }
    }
}
#endif

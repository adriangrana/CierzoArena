using UnityEditor;

namespace CierzoArena.EditorTools
{
    /// <summary>Keeps generated roster portraits as UI-ready single sprites without
    /// relying on manual inspector changes on every machine.</summary>
    public sealed class HeroPortraitImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Art/UI/HeroPortraits/")) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 70;
        }
    }
}

#if UNITY_EDITOR
using System;
using CierzoArena.Units;
using UnityEditor;
using UnityEngine;

namespace CierzoArena.EditorTools
{
    /// <summary>Produces the missing, project-local roster art as deterministic
    /// Texture2D assets. Existing imported founding portraits are never replaced.
    /// The generated images are intentionally graphic/illustrative: they are a
    /// consistent base-art pipeline, not gameplay prefabs or placeholder meshes.</summary>
    public static class HeroRosterArtBuilder
    {
        private const string PortraitRoot = "Assets/Resources/Art/UI/HeroPortraits";
        private const string IconRoot = "Assets/Resources/Art/UI/AbilityIcons";
        [MenuItem("Cierzo Arena/Heroes/Build Roster Portraits and Icons")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Resources/Art/UI", "HeroPortraits");
            EnsureFolder("Assets/Resources/Art/UI", "AbilityIcons");
            foreach (HeroDefinition hero in HeroCatalog.Shared.Heroes)
            {
                if (hero == null) continue;
                string portraitName = Pascal(hero.HeroId) + "Portrait";
                string portraitAssetPath = PortraitRoot + "/" + portraitName + ".asset";
                string importedPortraitPath = PortraitRoot + "/" + portraitName + ".png";
                // The founding six use imported PNGs. Remove only the generated
                // same-name asset so Resources cannot select it instead.
                if (System.IO.File.Exists(importedPortraitPath) && AssetDatabase.LoadAssetAtPath<Texture2D>(portraitAssetPath) != null)
                    AssetDatabase.DeleteAsset(portraitAssetPath);
                if (!System.IO.File.Exists(importedPortraitPath)) CreateIfMissing(portraitAssetPath, BuildPortrait(hero));
                Texture2D portrait = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.File.Exists(importedPortraitPath) ? importedPortraitPath : portraitAssetPath);
                if (portrait != null) hero.SetPresentation(portrait, portrait);
                for (int slot = 0; slot < 4; slot++)
                {
                    AbilityDefinition ability = hero.GetAbility(slot);
                    if (ability != null) CreateOrUpdateIcon(IconRoot + "/" + Pascal(ability.AbilityId) + "Icon.asset", BuildIcon(hero, ability, slot));
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateIfMissing(string path, Texture2D texture)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) { UnityEngine.Object.DestroyImmediate(texture); return; }
            texture.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(texture, path);
        }

        private static void CreateOrUpdateIcon(string path, Texture2D texture)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) AssetDatabase.DeleteAsset(path);
            texture.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(texture, path);
        }

        private static Texture2D BuildPortrait(HeroDefinition hero)
        {
            const int size = 384;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            int seed = StableHash(hero.HeroId); Color accent = hero.ThemeColor; Color dark = Color.Lerp(accent, new Color(.008f, .015f, .035f), .78f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size, v = (y + .5f) / size;
                float dx = u - .5f, dy = v - .52f, radius = Mathf.Sqrt(dx * dx + dy * dy);
                float glow = Mathf.Clamp01(1f - radius * 1.35f);
                float ray = Mathf.Max(0f, Mathf.Sin(Mathf.Atan2(dy, dx) * (4 + seed % 4) + seed * .0001f));
                Color pixel = Color.Lerp(dark, accent, glow * .42f + ray * .13f);
                float head = dx * dx / .055f + (v - .43f) * (v - .43f) / .075f;
                float shoulders = dx * dx / .19f + (v - .74f) * (v - .74f) / .13f;
                float mantle = Mathf.Abs(dx) * 1.5f + Mathf.Abs(v - .64f) * .82f;
                if (shoulders < 1f || mantle < .42f) pixel = Color.Lerp(pixel, accent, .7f);
                if (head < 1f) pixel = Color.Lerp(pixel, Color.Lerp(accent, Color.white, .32f), .75f);
                if (head < .36f && v < .45f) pixel = Color.Lerp(pixel, Color.white, .24f);
                if (Mathf.Abs(dx) < .018f && v > .15f && v < .72f) pixel = Color.Lerp(pixel, Color.white, .2f);
                texture.SetPixel(x, y, pixel);
            }
            texture.Apply(false, false); return texture;
        }

        private static Texture2D BuildIcon(HeroDefinition hero, AbilityDefinition ability, int slot)
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            int seed = StableHash(ability.AbilityId); Color accent = slot == 3 ? Color.Lerp(hero.ThemeColor, new Color(1f, .58f, .12f), .55f) : hero.ThemeColor; Color dark = Color.Lerp(accent, Color.black, .82f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x - size * .5f) / (size * .5f), dy = (y - size * .5f) / (size * .5f), radius = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx); float motif = Mathf.Abs(Mathf.Sin(angle * (2 + seed % 5) + radius * 8f));
                Color pixel = radius > .94f ? Color.black : Color.Lerp(dark, accent, Mathf.Clamp01(1f - radius + motif * .16f));
                bool glyph = slot switch
                {
                    0 => Mathf.Abs(dy + dx * .48f) < .12f && Mathf.Abs(dx) < .67f, // strike / projectile
                    1 => Mathf.Abs(dx) < .46f * (1f - Mathf.Max(0f, dy) * .35f) && dy > -.54f && dy < .54f && radius > .24f, // shield
                    2 => Mathf.Abs(dy) < .13f + Mathf.Abs(dx) * .34f && dx > -.58f && dx < .58f, // movement chevron
                    _ => radius < .54f && Mathf.Abs(Mathf.Sin(angle * 4f)) > .42f // ultimate burst
                };
                if (glyph) pixel = Color.Lerp(pixel, Color.white, slot == 3 ? .86f : .72f);
                texture.SetPixel(x, y, pixel);
            }
            texture.Apply(false, false); return texture;
        }

        private static int StableHash(string value) { unchecked { int hash = 23; foreach (char character in value) hash = hash * 31 + character; return hash & int.MaxValue; } }
        private static string Pascal(string id) { string[] words = id.Split(new[] { '.', '_' }, StringSplitOptions.RemoveEmptyEntries); string result = string.Empty; foreach (string word in words) result += char.ToUpperInvariant(word[0]) + word.Substring(1); return result; }
        private static void EnsureFolder(string parent, string name) { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }
    }
}
#endif

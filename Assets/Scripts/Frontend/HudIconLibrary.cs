using System;
using CierzoArena.Units;
using UnityEngine;

namespace CierzoArena.Frontend
{
    /// <summary>Maps the authored Cierzo icon atlases to stable gameplay IDs. The
    /// library is presentation-only: abilities and items remain data/authority
    /// systems and never depend on texture availability.</summary>
    public static class HudIconLibrary
    {
        private static Texture2D abilityAtlas;
        private static Texture2D itemAtlas;

        private const int AbilityAtlasColumns = 10;
        private const int AbilityAtlasRows = 8;
        private const int AbilityAtlasCapacity = AbilityAtlasColumns * AbilityAtlasRows;

        private static readonly string[] ItemIds =
        {
            "bastion.plating", "gale.edge", "windstep.boots", "tempest.cog", "cierzo.alloy", "fallback"
        };

        public static bool TryGetAbility(AbilityDefinition ability, out Texture2D texture, out Rect uv)
        {
            texture = LoadAbilityAtlas();
            int index = AbilityAtlasIndex(ability);
            if (texture != null) { uv = Cell(index, AbilityAtlasColumns, AbilityAtlasRows, .003f, .006f); return true; }
            if (ability != null && ability.Icon != null) { texture = ability.Icon; uv = new Rect(0f, 0f, 1f, 1f); return true; }
            uv = new Rect(0f, 0f, 1f, 1f); return false;
        }

        // Atlas cells follow the data-driven roster order: every hero has four
        // dedicated cells, so the HUD and the hero detail always address the
        // exact same image source.
        private static int AbilityAtlasIndex(AbilityDefinition ability)
        {
            if (ability == null) return 0;
            int index = 0;
            foreach (HeroDefinition hero in HeroCatalog.Shared.Heroes)
            {
                if (hero == null) continue;
                for (int slot = 0; slot < 4; slot++, index++)
                {
                    AbilityDefinition candidate = hero.GetAbility(slot);
                    if (candidate != null && candidate.AbilityId == ability.AbilityId) return index;
                }
            }
            return StableHash(ability.AbilityId) % AbilityAtlasCapacity;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value ?? string.Empty) hash = hash * 31 + character;
                return hash & int.MaxValue;
            }
        }

        public static bool TryGetItem(ItemDefinition item, out Texture2D texture, out Rect uv)
        {
            texture = LoadItemAtlas();
            int index = item == null ? 5 : Array.IndexOf(ItemIds, item.ItemId);
            if (index < 0) index = 5;
            // The two generated rows have a deliberate atmospheric margin. Crop it
            // so each inventory slot displays only its framed item illustration.
            int column = index % 3;
            int row = index / 3;
            uv = new Rect(column / 3f + .008f, row == 0 ? .49f : .105f, .318f, .36f);
            return texture != null;
        }

        private static Texture2D LoadAbilityAtlas() => abilityAtlas ??= Resources.Load<Texture2D>("Art/UI/Hud/CierzoAbilityIconsAtlas");
        private static Texture2D LoadItemAtlas() => itemAtlas ??= Resources.Load<Texture2D>("Art/UI/Hud/CierzoItemIconsAtlas");

        private static Rect Cell(int index, int columns, int rows, float insetX, float insetY)
        {
            int column = index % columns;
            int row = index / columns;
            float width = 1f / columns;
            float height = 1f / rows;
            return new Rect(column * width + insetX, 1f - (row + 1) * height + insetY, width - insetX * 2f, height - insetY * 2f);
        }
    }
}

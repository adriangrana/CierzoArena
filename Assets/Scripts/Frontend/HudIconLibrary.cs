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

        private static readonly string[] AbilityIds =
        {
            "rampart_strike", "windward_guard", "grounding_ring", "citadel_crash",
            "rift_lunge", "duelist_wind", "counterveil", "redline",
            "piercing_gale", "tailwind", "updraft_step", "horizon_breaker",
            "arc_bolt", "storm_mark", "gale_step", "tempest_fall",
            "kindling_orb", "cairn_barrier", "restoring_draft", "sanctuary_field",
            "pressure_drop", "static_lattice", "crosswind", "eye_of_tempest"
        };

        private static readonly string[] ItemIds =
        {
            "bastion.plating", "gale.edge", "windstep.boots", "tempest.cog", "cierzo.alloy", "fallback"
        };

        public static bool TryGetAbility(AbilityDefinition ability, out Texture2D texture, out Rect uv)
        {
            texture = LoadAbilityAtlas();
            int index = ability == null ? -1 : Array.IndexOf(AbilityIds, ability.AbilityId);
            uv = index >= 0 ? Cell(index, 6, 4, .006f, .012f) : new Rect(0f, 0f, 1f / 6f, 1f / 4f);
            return texture != null;
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
            return new Rect(column * width + insetX, row * height + insetY, width - insetX * 2f, height - insetY * 2f);
        }
    }
}

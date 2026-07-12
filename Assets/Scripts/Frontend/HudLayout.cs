using UnityEngine;

namespace CierzoArena.Frontend
{
    /// <summary>Shared safe-area layout for the legacy immediate-mode HUD. It keeps
    /// presentation responsive while gameplay systems remain unchanged.</summary>
    public static class HudLayout
    {
        public static float Scale=>Mathf.Clamp(Screen.height/1080f,1f,2.25f);
        public static float Width=>Screen.width/Scale;
        public static float Height=>Screen.height/Scale;
        public static Rect Inventory=>new Rect(24f,Height-195f,570f,142f);
        public static Rect Abilities=>new Rect(Width*.5f-286f,Height-162f,572f,142f);
        public static Rect Shop=>new Rect(Width*.5f-250f,Height*.5f-195f,500f,390f);
    }
}

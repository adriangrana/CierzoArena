using UnityEngine;

namespace CierzoArena.Frontend
{
    [CreateAssetMenu(fileName="CierzoVisualTheme",menuName="Cierzo Arena/Visual Theme")]
    public sealed class CierzoVisualTheme : ScriptableObject
    {
        [Header("Surface")] public Color background=new Color(.035f,.055f,.09f,.96f); public Color panel=new Color(.075f,.105f,.16f,.94f); public Color border=new Color(.31f,.45f,.57f,.9f);
        [Header("Text")] public Color text=Color.white; public Color muted=new Color(.64f,.72f,.8f); public Color azure=new Color(.18f,.72f,1f); public Color ember=new Color(1f,.31f,.16f); public Color gold=new Color(1f,.76f,.24f);
        [Header("States")] public Color hover=new Color(.16f,.32f,.46f); public Color selected=new Color(.10f,.42f,.62f); public Color disabled=new Color(.18f,.2f,.24f); public Color danger=new Color(.7f,.16f,.14f); public Color success=new Color(.22f,.72f,.45f);
        public bool IsValid()=>background.a>0f&&panel.a>0f&&text.a>0f&&azure.a>0f&&ember.a>0f;
    }
}

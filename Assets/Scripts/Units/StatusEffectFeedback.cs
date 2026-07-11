using System.Text;
using CierzoArena.CameraSystem;
using UnityEngine;

namespace CierzoArena.Units
{
    [RequireComponent(typeof(StatusEffectController))]
    public sealed class StatusEffectFeedback : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<StatusEffectState> states = new();
        private StatusEffectController effects; private GUIStyle style;
        private void Awake()=>effects=GetComponent<StatusEffectController>();
        private void OnGUI(){ if(LocalHeroProvider.Active==null||LocalHeroProvider.Active.CurrentHero!=transform)return; if(style==null)style=new GUIStyle(GUI.skin.box){fontSize=16,normal={textColor=Color.white}}; effects.CopyStatesTo(states); StringBuilder b=new(); foreach(var e in states)b.Append($"{e.Type} {e.Remaining:0.0}s  "); if(effects.Shield>0)b.Append($"Shield {effects.Shield:0}"); if(b.Length>0)GUI.Label(new Rect(24,630,700,34),b.ToString(),style); }
    }
}

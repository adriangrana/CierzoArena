using CierzoArena.CameraSystem;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Stops projectiles and short effects from revealing fogged positions.</summary>
    public sealed class VisionEffectVisibility : MonoBehaviour
    {
        private Renderer[] renderers;
        private void Awake()=>renderers=GetComponentsInChildren<Renderer>(true);
        private void LateUpdate(){if(LocalHeroProvider.Active==null||LocalHeroProvider.Active.CurrentHero==null)return;TeamMember observer=LocalHeroProvider.Active.CurrentHero.GetComponent<TeamMember>();if(observer==null)return;bool visible=VisionSource.IsVisible(observer.Team,transform.position);for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=visible;}
    }
}

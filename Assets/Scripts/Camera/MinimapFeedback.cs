using System.Collections.Generic;
using CierzoArena.Core;
using CierzoArena.Structures;
using CierzoArena.Units;
using UnityEngine;

namespace CierzoArena.CameraSystem
{
    public sealed class MinimapFeedback : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float halfMapSize = 86f;
        private readonly List<VisionSource> sources = new();
        private void Awake(){if(targetCamera==null)targetCamera=Camera.main;}
        private void OnGUI()
        {
            if(LocalHeroProvider.Active==null||LocalHeroProvider.Active.CurrentHero==null)return;
            TeamMember observer=LocalHeroProvider.Active.CurrentHero.GetComponent<TeamMember>(); if(observer==null)return;
            Rect map=new Rect(Screen.width-230,Screen.height-230,200,200); GUI.Box(map,"MINIMAP"); DrawTerrain(map); VisionSource.CopySourcesTo(sources);
            foreach(VisionSource source in sources){if(source==null||!ShouldRenderSource(observer.Team,source,out bool obscured))continue;StructureEntity structure=source.Structure;Vector3 p=source.transform.position;float x=map.x+(Mathf.Clamp(p.x,-halfMapSize,halfMapSize)+halfMapSize)/(halfMapSize*2)*map.width;float y=map.y+map.height-(Mathf.Clamp(p.z,-halfMapSize,halfMapSize)+halfMapSize)/(halfMapSize*2)*map.height;Color old=GUI.color;Color marker=source.Team==TeamId.Azure?Color.cyan:Color.red;GUI.color=obscured?new Color(marker.r,marker.g,marker.b,.3f):marker;float size=structure!=null?8f:6f;GUI.DrawTexture(new Rect(x-size*.5f,y-size*.5f,size,size),Texture2D.whiteTexture);GUI.color=old;}
            Event e=Event.current;if(e.type==EventType.MouseDown&&e.button==0&&map.Contains(e.mousePosition)&&targetCamera!=null){float x=(e.mousePosition.x-map.x)/map.width*halfMapSize*2-halfMapSize;float z=(1f-(e.mousePosition.y-map.y)/map.height)*halfMapSize*2-halfMapSize;Vector3 pos=targetCamera.transform.position;pos.x=x;pos.z=z;targetCamera.transform.position=pos;e.Use();}
        }
        public static bool ShouldRenderSource(TeamId observerTeam, VisionSource source, out bool obscured)
        {
            obscured=false;if(source==null)return false;StructureEntity structure=source.Structure;VisionVisibility memory=source.GetComponent<VisionVisibility>();if(structure!=null&&memory!=null&&!memory.KnownStructureAlive)return false;bool visible=source.Team==observerTeam||VisionSource.IsVisible(observerTeam,source.transform.position);if(visible)return true;bool knownStructure=structure!=null&&memory!=null&&memory.KnownStructureAlive;obscured=knownStructure;return knownStructure;
        }
        private static void DrawTerrain(Rect map)
        {
            Color old=GUI.color;GUI.color=new Color(.3f,.36f,.3f,.8f);GUI.DrawTexture(new Rect(map.x+5,map.center.y-2,map.width-10,4),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(map.center.x-2,map.y+5,4,map.height-10),Texture2D.whiteTexture);GUI.color=old;
        }
    }
}

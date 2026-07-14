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
        private static Texture2D circleIcon;
        private Camera minimapCamera;
        private RenderTexture minimapTexture;
        private float nextMapRender;
        public static MinimapFeedback Active { get; private set; }
        /// <summary>GUI-space minimap bounds shared with HUD layout so bottom
        /// presentation never obscures this tactical control.</summary>
        public static Rect TacticalMapRect => GetMapRect();
        private void Awake(){if(targetCamera==null)targetCamera=Camera.main;CreateMapCamera();}
        private void OnEnable(){Active=this;}
        private void OnDisable(){if(Active==this)Active=null;}
        private void LateUpdate(){if(minimapCamera!=null&&Time.unscaledTime>=nextMapRender){nextMapRender=Time.unscaledTime+.1f;minimapCamera.Render();}}
        private void OnDestroy(){if(minimapCamera!=null){if(Application.isPlaying)Destroy(minimapCamera.gameObject);else DestroyImmediate(minimapCamera.gameObject);}if(minimapTexture!=null){minimapTexture.Release();if(Application.isPlaying)Destroy(minimapTexture);else DestroyImmediate(minimapTexture);}}

        /// <summary>
        /// Converts an Input.mousePosition (bottom-left origin) inside the visible
        /// minimap into a ground point. Command controllers call this before their
        /// normal world raycast so UI clicks never leak through to terrain behind it.
        /// </summary>
        public static bool TryGetWorldPositionAtScreenPoint(Vector2 screenPoint,out Vector3 position)
        {
            position=Vector3.zero;
            if(Active==null)return false;
            return Active.TryGetWorldPositionAtScreenPointInternal(screenPoint,out position);
        }
        private bool TryGetWorldPositionAtScreenPointInternal(Vector2 screenPoint,out Vector3 position)
        {
            Vector2 guiPoint=new Vector2(screenPoint.x,Screen.height-screenPoint.y);
            return TryGetWorldPositionAtGuiPoint(guiPoint,out position);
        }
        private bool TryGetWorldPositionAtGuiPoint(Vector2 guiPoint,out Vector3 position)
        {
            Rect map=GetMapRect();
            if(!map.Contains(guiPoint)){position=default;return false;}
            position=GuiPointToWorld(guiPoint,map,halfMapSize);
            return true;
        }
        private static Rect GetMapRect()
        {
            // The minimap remains a primary tactical control at 4K, not a tiny
            // decorative inset. Keep a generous but bounded footprint on all ratios.
            float scale=Mathf.Clamp(Screen.height/1080f,.82f,1.38f);float size=320f*scale;float margin=26f*scale;
            return new Rect(Screen.width-size-margin,Screen.height-size-margin,size,size);
        }
        public static Vector3 GuiPointToWorld(Vector2 guiPoint,Rect map,float halfMapSize)
        {
            float x=(guiPoint.x-map.x)/map.width*halfMapSize*2-halfMapSize;
            float z=(1f-(guiPoint.y-map.y)/map.height)*halfMapSize*2-halfMapSize;
            return new Vector3(x,0f,z);
        }
        private void OnGUI()
        {
            if (CierzoArena.Core.MatchNavigationState.IsMainMenuVisible) return;
            if(LocalHeroProvider.Active==null||LocalHeroProvider.Active.CurrentHero==null)return;
            TeamMember observer=LocalHeroProvider.Active.CurrentHero.GetComponent<TeamMember>(); if(observer==null)return;
            Rect map=GetMapRect(); DrawFrame(map); DrawTerrain(map); VisionSource.CopySourcesTo(sources);
            foreach(VisionSource source in sources){if(source==null||!ShouldRenderSource(observer.Team,source,out bool obscured))continue;DrawSourceIcon(map,source,obscured);}
            DrawCameraViewport(map);
            Event e=Event.current;
            if(e.type==EventType.MouseDown&&map.Contains(e.mousePosition))
            {
                if(e.button==0&&targetCamera!=null)
                {
                    Vector3 world=GuiPointToWorld(e.mousePosition,map,halfMapSize);
                    Vector3 pos=targetCamera.transform.position;pos.x=world.x;pos.z=world.z;targetCamera.transform.position=pos;
                }
                // Right-click movement is issued in Update by the active command
                // controller, which reads the same minimap mapping before raycasting.
                e.Use();
            }
        }
        public static bool ShouldRenderSource(TeamId observerTeam, VisionSource source, out bool obscured)
        {
            obscured=false;if(source==null)return false;StructureEntity structure=source.Structure;VisionVisibility memory=source.GetComponent<VisionVisibility>();if(structure!=null&&memory!=null&&!memory.KnownStructureAlive)return false;bool visible=source.Team==observerTeam||VisionSource.IsVisible(observerTeam,source.transform.position);if(visible)return true;bool knownStructure=structure!=null&&memory!=null&&memory.KnownStructureAlive;obscured=knownStructure;return knownStructure;
        }
        private void DrawTerrain(Rect map)
        {
            if(minimapTexture!=null){GUI.DrawTexture(map,minimapTexture,ScaleMode.StretchToFill,false);Color veil=new Color(.01f,.04f,.07f,.22f);GUI.color=veil;GUI.DrawTexture(map,Texture2D.whiteTexture);GUI.color=Color.white;return;}
            Color old=GUI.color;GUI.color=new Color(.025f,.07f,.10f,.94f);GUI.DrawTexture(map,Texture2D.whiteTexture);GUI.color=old;
        }
        private static void DrawFrame(Rect map)
        {
            Color old=GUI.color;GUI.color=new Color(.01f,.03f,.06f,.92f);GUI.DrawTexture(new Rect(map.x-8,map.y-24,map.width+16,map.height+32),Texture2D.whiteTexture);GUI.color=new Color(.26f,.72f,.96f,.78f);GUI.DrawTexture(new Rect(map.x-2,map.y-2,map.width+4,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(map.x-2,map.yMax,map.width+4,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(map.x-2,map.y,2,map.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(map.xMax,map.y,2,map.height),Texture2D.whiteTexture);GUI.color=old;GUI.Label(new Rect(map.x,map.y-23,map.width,20),"MAPA TÁCTICO",GUI.skin.label);
        }
        private void DrawSourceIcon(Rect map,VisionSource source,bool obscured)
        {
            StructureEntity structure=source.Structure;Vector3 p=source.transform.position;float x=map.x+(Mathf.Clamp(p.x,-halfMapSize,halfMapSize)+halfMapSize)/(halfMapSize*2)*map.width;float y=map.y+map.height-(Mathf.Clamp(p.z,-halfMapSize,halfMapSize)+halfMapSize)/(halfMapSize*2)*map.height;Color old=GUI.color;Color marker=source.Team==TeamId.Azure?new Color(.2f,.9f,1f):source.Team==TeamId.Ember?new Color(1f,.34f,.25f):new Color(.82f,.62f,1f);GUI.color=obscured?new Color(marker.r,marker.g,marker.b,.28f):marker;
            if(structure!=null){float size=structure.Kind==StructureKind.Core?12f:9f;GUI.DrawTexture(new Rect(x-size*.5f,y-size*.5f,size,size),Texture2D.whiteTexture);}
            else if(source.TryGetComponent(out HeroUnit _)){float size=11f;GUI.DrawTexture(new Rect(x-size*.5f,y-size*.5f,size,size),CircleIcon());}
            else {float size=5f;GUI.DrawTexture(new Rect(x-size*.5f,y-size*.5f,size,size),Texture2D.whiteTexture);}
            GUI.color=old;
        }
        private void DrawCameraViewport(Rect map)
        {
            if(targetCamera==null||!targetCamera.orthographic)return;float worldH=targetCamera.orthographicSize*2f;float worldW=worldH*targetCamera.aspect;float width=Mathf.Clamp(worldW/(halfMapSize*2f)*map.width,8f,map.width);float height=Mathf.Clamp(worldH/(halfMapSize*2f)*map.height,8f,map.height);float x=map.x+(targetCamera.transform.position.x+halfMapSize)/(halfMapSize*2f)*map.width-width*.5f;float y=map.y+map.height-(targetCamera.transform.position.z+halfMapSize)/(halfMapSize*2f)*map.height-height*.5f;Color old=GUI.color;GUI.color=new Color(.9f,.96f,1f,.72f);GUI.DrawTexture(new Rect(x,y,width,1f),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x,y+height-1f,width,1f),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x,y,1f,height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x+width-1f,y,1f,height),Texture2D.whiteTexture);GUI.color=old;
        }
        private static Texture2D CircleIcon()
        {
            if(circleIcon!=null)return circleIcon;const int size=32;circleIcon=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Minimap Hero Icon",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=x-(size-1)*.5f,dy=y-(size-1)*.5f;circleIcon.SetPixel(x,y,dx*dx+dy*dy<=((size-1)*.5f)*((size-1)*.5f)?Color.white:Color.clear);}circleIcon.Apply(false,true);return circleIcon;
        }
        private void CreateMapCamera()
        {
            minimapTexture=new RenderTexture(512,512,16,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default){name="Cierzo Minimap World",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear,useMipMap=false,autoGenerateMips=false};minimapTexture.Create();
            GameObject cameraObject=new GameObject("Minimap World Camera"){hideFlags=HideFlags.DontSave};cameraObject.transform.position=new Vector3(0f,180f,0f);cameraObject.transform.rotation=Quaternion.Euler(90f,0f,0f);minimapCamera=cameraObject.AddComponent<Camera>();minimapCamera.enabled=false;minimapCamera.orthographic=true;minimapCamera.orthographicSize=halfMapSize;minimapCamera.clearFlags=CameraClearFlags.SolidColor;minimapCamera.backgroundColor=new Color(.015f,.035f,.055f);minimapCamera.cullingMask=~(1<<5);minimapCamera.targetTexture=minimapTexture;minimapCamera.nearClipPlane=.1f;minimapCamera.farClipPlane=300f;
        }
    }
}

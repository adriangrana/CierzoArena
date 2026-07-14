using CierzoArena.Core;
using CierzoArena.Units;
using CierzoArena.Online;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CierzoArena.Frontend
{
    /// <summary>Presentation-only frontend. It stores a launch request then loads the
    /// existing arena; it never creates a NetworkManager or starts a session itself.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum MatchMenuDialog { None, Disconnect, Quit }

        [SerializeField] private CierzoVisualTheme theme;
        [SerializeField] private HeroPresentationDefinition[] heroes;
        [SerializeField] private Texture2D logoImage;
        [SerializeField] private string arenaScene="MobaGreyboxArena";
        private int section;
        private TeamId team=TeamId.Azure;
        private string address="127.0.0.1";
        private string port="7777";
        private GUIStyle title,heading,body,button,selected,muted,card,eyebrow;
        private Texture2D keyVisual;
        private Texture2D leftFadeTex,navFadeTex;
        private Canvas menuCanvas;
        private RectTransform menuRoot,contentRoot,playPanelRoot;
        private Font uiFont;
        private bool canvasPresentation;
        private HeroCatalog heroCatalog;
        private HeroDefinition selectedHero;
        private int heroFilter;
        private MatchMenuDialog matchMenuDialog;
        // Playing is a contextual action from Inicio, not a destination in the
        // global navigation. This keeps the primary navigation focused on areas
        // of the client and lets the match choices open beside the home screen.
        private readonly string[] navigation={"Inicio","Héroes","Aprender","Ajustes"};
        private bool playPanelOpen;

        public int ActiveSection=>section;
        public static int ResolveSingleActivePanel(int requested,int count)=>Mathf.Clamp(requested,0,Mathf.Max(0,count-1));
        private void Awake()
        {
            keyVisual=Resources.Load<Texture2D>("Frontend/MainMenuKeyVisual");
            // Unity can retain a destroyed or moved serialized asset as a
            // "fake null" reference. Use Unity's null operator, not ??=, so
            // Resources supplies the logo in that case as well.
            if(logoImage==null)logoImage=Resources.Load<Texture2D>("Frontend/MainMenuLogo");
            uiFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            heroCatalog=HeroCatalog.Shared;selectedHero=heroCatalog.ResolveOrFallback(FrontendLaunchRequest.HeroId);
            BuildCanvasPresentation();
        }
        private void OnEnable() => MatchNavigationState.Changed += RefreshMatchPresentation;
        private void OnDisable() => MatchNavigationState.Changed -= RefreshMatchPresentation;

        private bool IsActiveMatchMenu => MatchNavigationState.IsMatchActive && MatchNavigationState.IsMainMenuVisible;

        private void RefreshMatchPresentation()
        {
            if (menuCanvas == null || !IsActiveMatchMenu) return;
            menuCanvas.overrideSorting = true;
            menuCanvas.sortingOrder = 100;
            BuildContent();
        }
        private void OnGUI()
        {
            if(canvasPresentation)return;
            // Keep a 1920x1080-ish logical canvas at 4K instead of leaving a tiny
            // fixed-width menu in a much wider coordinate space.
            EnsureStyles(); float scale=Mathf.Clamp(Screen.height/1080f,1f,2.25f); Matrix4x4 old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float width=Screen.width/scale,height=Screen.height/scale;
            GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture); Color restore=GUI.color;GUI.color=theme!=null?theme.background:new Color(.035f,.055f,.09f);GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture);GUI.color=restore;
            DrawBackdrop(width,height); DrawTopBar(width); GUI.BeginGroup(new Rect(width*.075f,118,width*.85f,height-145));
            section=ResolveSingleActivePanel(section,navigation.Length);
            if(section==0){DrawHome(width*.86f,height-140);if(playPanelOpen)DrawPlayPanel(width*.86f,height-140);}else if(section==1)DrawHeroes(width*.86f,height-140);else if(section==2)DrawPlaceholder("Aprender","Guías, tutoriales y retos llegarán en un próximo milestone.");else DrawSettings(width*.86f,height-140);
            GUI.EndGroup();GUI.matrix=old;
        }
        private void DrawBackdrop(float width,float height)
        {
            keyVisual ??= Resources.Load<Texture2D>("Frontend/MainMenuKeyVisual");
            if(keyVisual!=null)DrawKeyVisual(width,height);
            else Fill(new Rect(0,0,width,height),new Color(.018f,.03f,.055f));
            // A restrained readability veil; the illustration remains visible behind
            // the UI instead of being replaced with opaque placeholder panels.
            Fill(new Rect(0,0,width*.47f,height),new Color(.005f,.012f,.028f,.70f));
            // Soft transitional fog makes ultrawide's left UI field feel part of the
            // same illustrated world instead of a separate rectangle.
            for(int i=0;i<9;i++)Fill(new Rect(width*(.34f+i*.022f),height*.08f,42f,height*.84f),new Color(.03f,.12f,.18f,.075f-i*.006f));
            Fill(new Rect(0,0,width,height),new Color(.01f,.025f,.06f,.18f));
            for(int i=0;i<5;i++)DrawRotated(new Rect(width*(.34f+i*.12f),height*(.14f+i*.08f+Mathf.Sin(Time.unscaledTime*.3f+i)*8f),300f,2f),-15f,new Color(.4f,.85f,1f,.1f));
        }
        private void DrawTopBar(float width)
        {
            Fill(new Rect(0,0,width,104),new Color(.012f,.025f,.05f,.94f));Fill(new Rect(0,101,width,3),theme!=null?theme.azure:new Color(.18f,.72f,1f));
            GUI.Label(new Rect(42,13,355,58),"CIERZO ARENA",title);GUI.Label(new Rect(48,74,280,18),"ARENA OF THE NORTH WIND",eyebrow);
            float x=425;for(int i=0;i<navigation.Length;i++){GUIStyle style=i==section?selected:button;if(GUI.Button(new Rect(x+i*134,27,124,46),navigation[i],style)){section=i;playPanelOpen=false;}}
            Fill(new Rect(width-365,18,1,62),new Color(.42f,.65f,.78f,.45f));
            GUI.Label(new Rect(width-350,22,58,20),"◉  ✉  ⚙",eyebrow);
            DrawCard(new Rect(width-280,16,235,64),"AERIN · NIVEL 12","● En línea  ·  Perfil provisional");
        }
        private void DrawHome(float width,float height)
        {
            const float column=610f;
            GUI.Label(new Rect(4,43,column,26),"TEMPORADA PROTOTIPO · EL ASCENSO DEL CIERZO",eyebrow);
            GUI.Label(new Rect(0,77,column,132),"EL VIENTO\nDECIDE LA ARENA",title);GUI.Label(new Rect(4,226,540,66),"Un enfrentamiento de estrategia, energía elemental y control del terreno. Reúne tu escuadra y entra a CierzoArena.",body);
            if(GUI.Button(new Rect(4,315,306,66),"✦  JUGAR AHORA",selected))playPanelOpen=true;if(GUI.Button(new Rect(324,315,230,66),"VER HÉROES",button))section=1;
            DrawCard(new Rect(0,432,188,176),"◈  NOTICIAS","Vertical slice\nAzure · río · Guardián");
            DrawCard(new Rect(202,432,188,176),"◌  GRUPO","Sin grupo activo\nPróximamente");
            DrawCard(new Rect(404,432,188,176),"△  VERSIÓN","M19 · Frontend\nUnity 6");
            DrawGuardianCallout(width,height);
        }
        private void DrawHeroes(float width,float height)
        {
            GUI.Label(new Rect(0,18,width,42),"HÉROES PROTOTIPO",title);GUI.Label(new Rect(4,64,width,28),"Catálogo preparado para futuras plantillas; los datos no dependen de GameObjects de escena.",muted);
            if(heroes==null||heroes.Length==0){DrawCard(new Rect(0,110,620,160),"Catálogo pendiente","No hay definiciones de presentación configuradas.");return;}
            for(int i=0;i<heroes.Length;i++){HeroPresentationDefinition hero=heroes[i];if(hero==null)continue;float x=i*330;GUI.Box(new Rect(x,115,310,320),GUIContent.none,card);GUI.Label(new Rect(x+20,135,270,38),hero.HeroName,heading);GUI.Label(new Rect(x+20,178,270,24),$"{hero.Role} · {hero.CombatStyle} · Dificultad {hero.Difficulty}/3",muted);GUI.Label(new Rect(x+20,214,270,70),hero.Description,body);GUI.Label(new Rect(x+20,292,270,100),"HABILIDADES\n"+hero.Abilities,muted);}
        }
        private void DrawPlayPanel(float width,float height)
        {
            Rect panel=new Rect(width*.62f,0,width*.38f,height);if(Event.current.type==EventType.MouseDown&&!panel.Contains(Event.current.mousePosition)){playPanelOpen=false;Event.current.Use();return;}Fill(new Rect(0,0,width,height),new Color(.002f,.007f,.018f,.42f));Fill(panel,new Color(.018f,.045f,.078f,.985f));Fill(new Rect(panel.x,panel.y,panel.width,2f),Accent);GUI.Label(new Rect(panel.x+20,panel.y+18,panel.width-40,38),"JUGAR",heading);
            GUI.Label(new Rect(panel.x+20,panel.y+58,panel.width-40,28),"ELIGE CÓMO ENTRAR A LA ARENA",eyebrow);
            float y=panel.y+96;
            if(GUI.Button(new Rect(panel.x+20,y,panel.width-40,38),"SALA PRIVADA",selected))MultiplayerRoomPanel.Show(menuCanvas,selectedHero,team);y+=48;
            if(GUI.Button(new Rect(panel.x+20,y,panel.width-40,38),"PARTIDA LOCAL",button))Launch(FrontendMatchMode.LocalDevelopment);
        }
        private void DrawSettings(float width,float height){GUI.Label(new Rect(0,18,width,42),"AJUSTES",title);DrawCard(new Rect(0,100,520,180),"INTERFAZ","Escalado automático: activo\nContraste elevado: activo\nAtajos: ratón y teclado básicos.");DrawCard(new Rect(550,100,390,180),"DESARROLLO","Abrir MobaGreyboxArena directamente conserva el selector técnico de M18.");}
        private void DrawPlaceholder(string name,string message){GUI.Label(new Rect(0,18,900,42),name.ToUpperInvariant(),title);DrawCard(new Rect(0,105,620,160),"PRÓXIMAMENTE",message);}
        private void DrawGuardianCallout(float width,float height)
        {
            Rect panel=new Rect(width*.61f,height*.64f,Mathf.Min(430f,width*.31f),108f);Fill(panel,new Color(.01f,.025f,.05f,.68f));Fill(new Rect(panel.x,panel.y,panel.width,1f),new Color(.36f,.76f,.95f,.68f));GUI.Label(new Rect(panel.x+18,panel.y+14,panel.width-36,28),"GUARDIÁN DEL CIERZO",heading);GUI.Label(new Rect(panel.x+18,panel.y+45,panel.width-36,22),"Domina la fosa. Reclama el ascendente.",muted);GUI.Label(new Rect(panel.x+18,panel.y+75,panel.width-36,20),"◆ EVENTO ACTIVO   ·   VER DESAFÍO  ›",eyebrow);
        }
        private void DrawCard(Rect rect,string label,string text)
        {
            bool hover=rect.Contains(Event.current.mousePosition);Color surface=hover?new Color(.045f,.10f,.16f,.94f):new Color(.025f,.05f,.09f,.84f);Fill(rect,surface);Fill(new Rect(rect.x,rect.y,rect.width,1f),new Color(.62f,.48f,.24f,.75f));Fill(new Rect(rect.x,rect.y,1f,rect.height),new Color(.48f,.7f,.86f,.4f));
            if(rect.height<100f){GUI.Label(new Rect(rect.x+14,rect.y+10,rect.width-28,24),label,heading);GUI.Label(new Rect(rect.x+14,rect.y+36,rect.width-28,20),text,muted);return;}
            if(keyVisual!=null)GUI.DrawTexture(new Rect(rect.x+14,rect.y+48,rect.width-28,42),keyVisual,ScaleMode.ScaleAndCrop,true);
            GUI.Label(new Rect(rect.x+16,rect.y+14,rect.width-32,28),label,heading);GUI.Label(new Rect(rect.x+16,rect.y+101,rect.width-32,rect.height-130),text,body);GUI.Label(new Rect(rect.x+16,rect.y+rect.height-25,rect.width-32,18),"VER DETALLES  ›",eyebrow);
        }
        private void Launch(FrontendMatchMode mode){ushort parsed=7777;ushort.TryParse(port,out parsed);FrontendLaunchRequest.Set(mode,team,address,parsed,selectedHero?.HeroId);SceneManager.LoadScene(arenaScene);}
        // ----- Retained-mode presentation -----------------------------------
        // The MainMenu uses real Canvas controls. The former IMGUI routines remain
        // only as a development fallback should the Canvas be unavailable.
        private void BuildCanvasPresentation()
        {
            // The MainMenu scene owns its existing screen-space Canvas. Never use
            // FindAnyObjectByType here: an additive menu over the arena would then
            // steal a world-space health-bar Canvas.
            menuCanvas=FindCanvasInOwnScene();
            if(menuCanvas==null){Debug.LogError("[MainMenu] The MainMenu scene has no Canvas.",this);return;}
            menuCanvas.renderMode=RenderMode.ScreenSpaceOverlay;CanvasScaler scaler=menuCanvas.GetComponent<CanvasScaler>();
            if(scaler!=null){scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;}
            menuCanvas.overrideSorting=IsActiveMatchMenu;menuCanvas.sortingOrder=IsActiveMatchMenu?100:0;
            ConfigureEmbeddedMenuScene();
            menuRoot=CreateRect("MainMenu Layers",menuCanvas.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);RawImage keyArt=CreateRaw("Background Key Art",menuRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,keyVisual);AspectRatioFitter keyFit=keyArt.gameObject.AddComponent<AspectRatioFitter>();keyFit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;keyFit.aspectRatio=keyVisual!=null?(float)keyVisual.width/keyVisual.height:16f/9f;keyArt.rectTransform.pivot=new Vector2(1f,.5f);
            CreateImage("Atmospheric Overlay",menuRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.01f,.03f,.07f,.18f));
            leftFadeTex??=MakeAxisGradient(new Color(.004f,.012f,.028f),.92f,0f,true);
            RawImage leftFade=CreateRaw("Left Readability Fade",menuRoot,new Vector2(0,0),new Vector2(.62f,1),Vector2.zero,Vector2.zero,leftFadeTex);leftFade.raycastTarget=false;
            BuildTopNavigation();contentRoot=CreateRect("Main Content",menuRoot,new Vector2(.045f,.11f),new Vector2(.955f,.88f),Vector2.zero,Vector2.zero);BuildContent();BuildActiveMatchControls();canvasPresentation=true;
            OpenExistingPrivateRoom();
        }

        private void OpenExistingPrivateRoom()
        {
            if (MatchNavigationState.IsMatchActive) return;
            MultiplayerSessionCoordinator coordinator = MultiplayerSessionCoordinator.Active;
            if (coordinator?.Sessions == null || !coordinator.Sessions.IsInSession) return;

            // A completed match returns to the same private room. Do this from the
            // menu scene itself so the host is not left on Inicio while clients see
            // the room panel, and so UI role labels come from the session SDK.
            section = 0;
            playPanelOpen = true;
            BuildContent();
            BuildTopNavigationRefresh();
            MultiplayerRoomPanel.Show(menuCanvas, selectedHero, team);
        }
        private void BuildTopNavigation()
        {
            RectTransform top=CreateRect("Top Navigation",menuRoot,new Vector2(0,.905f),Vector2.one,Vector2.zero,Vector2.zero);
            navFadeTex??=MakeAxisGradient(new Color(.02f,.045f,.09f),.985f,.9f,false);RawImage navSurface=CreateRaw("Navigation Surface",top,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,navFadeTex);navSurface.raycastTarget=true;
            CreateImage("Navigation Bottom Accent",top,new Vector2(0,0),new Vector2(1,.02f),Vector2.zero,Vector2.zero,new Color(Accent.r,Accent.g,Accent.b,.55f)).raycastTarget=false;
            if(logoImage!=null){RectTransform logoSlot=CreateRect("Brand Logo Slot",top,new Vector2(.02f,.12f),new Vector2(.2f,.9f),Vector2.zero,Vector2.zero);RawImage logo=CreateRaw("Brand Logo",logoSlot,new Vector2(0,0),new Vector2(1,1),Vector2.zero,Vector2.zero,logoImage);logo.raycastTarget=false;AspectRatioFitter logoFit=logo.gameObject.AddComponent<AspectRatioFitter>();logoFit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;logoFit.aspectRatio=logoImage.height>0?(float)logoImage.width/logoImage.height:3.4f;logo.rectTransform.pivot=new Vector2(0f,.5f);}
            else{Text brand=CreateText("Brand",top,"CIERZO\nARENA",30,FontStyle.Bold,TextAnchor.MiddleLeft,Color.white,new Vector2(.025f,.08f),new Vector2(.19f,.92f));AddShadow(brand.gameObject,new Color(0,0,0,.8f),2f);CreateText("Brand Tag",top,"ARENA OF THE NORTH WIND",10,FontStyle.Bold,TextAnchor.LowerLeft,Accent,new Vector2(.028f,.04f),new Vector2(.19f,.34f));}
            for(int i=0;i<navigation.Length;i++){int index=i;float x0=.22f+i*.11f,x1=.32f+i*.11f;Button tab=CreateButton("Tab "+navigation[i],top,navigation[i],new Vector2(x0,.22f),new Vector2(x1,.78f),i==section?Selected:Panel);tab.onClick.AddListener(()=>{section=index;playPanelOpen=false;BuildContent();BuildTopNavigationRefresh();});}
            CreateText("Currencies",top,"◆ 3 450     ◈ 18 760",14,FontStyle.Bold,TextAnchor.MiddleRight,Accent,new Vector2(.72f,.34f),new Vector2(.84f,.75f));CreateImage("Profile Divider",top,new Vector2(.845f,.18f),new Vector2(.846f,.82f),Vector2.zero,Vector2.zero,new Color(.4f,.7f,.86f,.5f));CreateText("Profile",top,"AERIN\nNIVEL 12",15,FontStyle.Bold,TextAnchor.MiddleLeft,Color.white,new Vector2(.855f,.25f),new Vector2(.94f,.8f));CreateText("Profile Icons",top,"◉   ✉   ⚙",16,FontStyle.Bold,TextAnchor.MiddleCenter,Muted,new Vector2(.94f,.25f),new Vector2(.995f,.8f));
            if(IsActiveMatchMenu){Button quit=CreateButton("Quit Game",top,"⏻  SALIR",new Vector2(.88f,.08f),new Vector2(.985f,.22f),new Color(.28f,.08f,.10f,.95f));quit.onClick.AddListener(()=>{matchMenuDialog=MatchMenuDialog.Quit;BuildContent();});}
        }
        private void BuildTopNavigationRefresh(){if(menuRoot==null)return;Transform old=menuRoot.Find("Top Navigation");if(old!=null)Destroy(old.gameObject);BuildTopNavigation();}
        private void BuildContent()
        {
            if(contentRoot==null)return;ClearPlayPanel();for(int i=contentRoot.childCount-1;i>=0;i--)Destroy(contentRoot.GetChild(i).gameObject);
            if(IsActiveMatchMenu&&matchMenuDialog!=MatchMenuDialog.None){BuildActiveMatchConfirmation();return;}
            if(section==0)
            {
                BuildHome();
                if(playPanelOpen)BuildPlayPanel();
            }
            else if(section==1)BuildHeroes();
            else BuildPlaceholder(navigation[section]);
        }

        private void ClearPlayPanel()
        {
            if(playPanelRoot==null)return;
            Destroy(playPanelRoot.gameObject);
            playPanelRoot=null;
        }

        private void BuildActiveMatchControls()
        {
            if(!IsActiveMatchMenu||menuRoot==null)return;
            RectTransform controls=CreateRect("Active Match Controls",menuRoot,new Vector2(.83f,.025f),new Vector2(.985f,.095f),Vector2.zero,Vector2.zero);
            Button disconnect=CreateButton("Disconnect Match",controls,"DESCONECTARSE  ×",new Vector2(0f,.54f),Vector2.one,new Color(.32f,.075f,.085f,.96f));
            disconnect.onClick.AddListener(()=>{matchMenuDialog=MatchMenuDialog.Disconnect;BuildContent();});
            Button resume=CreateButton("Return To Match",controls,"VOLVER A LA PARTIDA",Vector2.zero,new Vector2(1f,.46f),new Color(.08f,.38f,.26f,.98f));
            resume.interactable=MatchNavigationState.CanReturnToMatch;resume.onClick.AddListener(MatchNavigationState.ReturnToMatch);
        }

        private void BuildActiveMatchContent()
        {
            if(matchMenuDialog!=MatchMenuDialog.None){BuildActiveMatchConfirmation();return;}

            Image surface=CreateImage("Active Match Surface",contentRoot,new Vector2(.06f,.14f),new Vector2(.73f,.86f),Vector2.zero,Vector2.zero,new Color(.012f,.035f,.07f,.92f));
            AddOutline(surface.gameObject,new Color(Accent.r,Accent.g,Accent.b,.75f),1.5f);
            Transform view=surface.transform;
            CreateText("Match Status",view,"PARTIDA EN CURSO",42,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.07f,.70f),new Vector2(.93f,.90f));
            string connection=MatchNavigationState.IsOnline
                ? (MatchNavigationState.IsHost?"ANFITRIÓN · La partida sigue simulándose para todos.":"CLIENTE · La conexión continúa activa.")
                : "PARTIDA LOCAL · La simulación continúa en segundo plano.";
            CreateText("Match Detail",view,connection+"\nEl menú bloquea órdenes de juego, pero no pausa la arena.",18,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.07f,.48f),new Vector2(.90f,.64f));
            Image status=CreateImage("Active Match Indicator",view,new Vector2(.07f,.38f),new Vector2(.78f,.44f),Vector2.zero,Vector2.zero,new Color(.05f,.24f,.14f,.95f));
            status.raycastTarget=false;CreateText("Label",status.transform,"●  PARTIDA EN CURSO",15,FontStyle.Bold,TextAnchor.MiddleCenter,new Color(.55f,.95f,.72f),Vector2.zero,Vector2.one);
            Button resume=CreateButton("Return To Match",view,"←  VOLVER A LA PARTIDA",new Vector2(.07f,.19f),new Vector2(.78f,.31f),Selected);
            resume.interactable=MatchNavigationState.CanReturnToMatch;resume.onClick.AddListener(MatchNavigationState.ReturnToMatch);
            Button disconnect=CreateButton("Disconnect Match",view,"DESCONECTARSE DE LA PARTIDA",new Vector2(.42f,.06f),new Vector2(.93f,.14f),new Color(.38f,.08f,.10f,.96f));
            disconnect.onClick.AddListener(()=>{matchMenuDialog=MatchMenuDialog.Disconnect;BuildContent();});
            CreateText("Match Safety",view,"Las opciones para iniciar, crear o unirse a otra partida están bloqueadas hasta desconectarte.",13,FontStyle.Normal,TextAnchor.LowerLeft,Muted,new Vector2(.07f,.06f),new Vector2(.39f,.14f));
        }

        private void BuildActiveMatchReadOnly(string title,string message)
        {
            Image surface=CreateImage("Read Only Surface",contentRoot,new Vector2(.06f,.14f),new Vector2(.73f,.86f),Vector2.zero,Vector2.zero,new Color(.012f,.035f,.07f,.92f));
            AddOutline(surface.gameObject,new Color(Accent.r,Accent.g,Accent.b,.75f),1.5f);
            CreateText("Heading",surface.transform,title,42,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.07f,.68f),new Vector2(.93f,.88f));
            CreateText("Description",surface.transform,message,19,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.07f,.48f),new Vector2(.86f,.61f));
            Button resume=CreateButton("Return To Match",surface.transform,"←  VOLVER A LA PARTIDA",new Vector2(.07f,.20f),new Vector2(.78f,.32f),Selected);
            resume.onClick.AddListener(MatchNavigationState.ReturnToMatch);
        }

        private void BuildActiveMatchConfirmation()
        {
            bool quit=matchMenuDialog==MatchMenuDialog.Quit;
            bool host=MatchNavigationState.IsOnline&&MatchNavigationState.IsHost;
            string title=quit?"¿SALIR DE CIERZO ARENA?":host?"ERES EL ANFITRIÓN":"¿DESCONECTARTE DE LA PARTIDA?";
            string message=quit
                ? (host?"También cerrarás la partida y la sala para todos.":"También te desconectarás de la partida actual.")
                : (host?"Al desconectarte, la partida y la sala se cerrarán para todos.":"La partida continuará sin ti.");
            CreateText("Confirmation Title",contentRoot,title,38,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.13f,.70f),new Vector2(.87f,.87f));
            CreateText("Confirmation Text",contentRoot,message,20,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.13f,.51f),new Vector2(.82f,.65f));
            Button cancel=CreateButton("Cancel Confirmation",contentRoot,"CANCELAR",new Vector2(.13f,.29f),new Vector2(.43f,.40f),Panel);cancel.onClick.AddListener(()=>{matchMenuDialog=MatchMenuDialog.None;BuildContent();});
            Button confirm=CreateButton("Confirm Confirmation",contentRoot,quit?"SALIR":"DESCONECTARSE",new Vector2(.49f,.29f),new Vector2(.87f,.40f),new Color(.46f,.10f,.11f,.96f));
            confirm.onClick.AddListener(quit?(UnityEngine.Events.UnityAction)QuitApplication:MatchNavigationState.RequestDisconnect);
        }

        private void QuitApplication()
        {
            if(MatchNavigationState.IsMatchActive) MatchNavigationState.RequestDisconnect();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }

        private Canvas FindCanvasInOwnScene()
        {
            foreach(GameObject root in gameObject.scene.GetRootGameObjects())
            {
                Canvas candidate=root.GetComponentInChildren<Canvas>(true);
                if(candidate!=null)return candidate;
            }
            return null;
        }

        private void ConfigureEmbeddedMenuScene()
        {
            if(!IsActiveMatchMenu)return;
            foreach(GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach(Camera camera in root.GetComponentsInChildren<Camera>(true))camera.gameObject.SetActive(false);
                EventSystem eventSystem=root.GetComponentInChildren<EventSystem>(true);
                if(eventSystem!=null&&HasExternalEventSystem())eventSystem.gameObject.SetActive(false);
            }
        }

        private bool HasExternalEventSystem()
        {
            foreach(EventSystem candidate in FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude))
                if(candidate.gameObject.scene.handle!=gameObject.scene.handle)return true;
            return false;
        }
        private void BuildHome()
        {
            RectTransform left=CreateRect("Main Hero Copy",contentRoot,new Vector2(0,.18f),new Vector2(.43f,.89f),Vector2.zero,Vector2.zero);
            CreateText("Season",left,"◆  TEMPORADA PROTOTIPO · EL ASCENSO DEL CIERZO",13,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(0,.9f),Vector2.one);
            Text heroTitle=CreateText("Hero Title",left,"EL VIENTO\nDECIDE LA ARENA",58,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(0,.57f),new Vector2(1,.9f));AddShadow(heroTitle.gameObject,new Color(.02f,.05f,.10f,.85f),3f);
            CreateImage("Title Flourish",left,new Vector2(.006f,.545f),new Vector2(.20f,.556f),Vector2.zero,Vector2.zero,new Color(Accent.r,Accent.g,Accent.b,.9f)).raycastTarget=false;CreateText("Title Flourish Mark",left,"◆",13,FontStyle.Bold,TextAnchor.MiddleLeft,Accent,new Vector2(.205f,.533f),new Vector2(.26f,.568f));
            CreateText("Hero Description",left,"Un enfrentamiento de estrategia, energía elemental y control del terreno. Reúne tu escuadra y entra a Cierzo Arena.",18,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.006f,.35f),new Vector2(.9f,.52f));
            Button play=CreateButton("Play Now",left,"✦  JUGAR AHORA",new Vector2(0,.2f),new Vector2(.51f,.32f),Selected);AddOutline(play.gameObject,new Color(Accent.r,Accent.g,Accent.b,.95f),2f);play.onClick.AddListener(OpenPlayPanel);Button heroesButton=CreateButton("View Heroes",left,"⚔  VER HÉROES",new Vector2(.54f,.2f),new Vector2(.93f,.32f),Panel);AddOutline(heroesButton.gameObject,new Color(.32f,.6f,.78f,.55f),1.5f);heroesButton.onClick.AddListener(()=>{section=1;playPanelOpen=false;BuildContent();BuildTopNavigationRefresh();});
            RectTransform cards=CreateRect("Information Cards",contentRoot,new Vector2(0,0),new Vector2(.44f,.22f),Vector2.zero,Vector2.zero);CreateInfoCard(cards,"◈","Noticias","NUEVO PARCHE 1.1.0","Balance de héroes, mejoras y correcciones.","LEER MÁS  ›",0);CreateInfoCard(cards,"◉","Grupo","2 / 5 EN LÍNEA","Grupo provisional · social próximamente.","VER GRUPO  ›",1);CreateInfoCard(cards,"◆","Versión","M19 FRONTEND","Vertical slice visual disponible.","NOTAS  ›",2);
            RectTransform featured=CreateRect("Featured Event",contentRoot,new Vector2(.6f,.12f),new Vector2(.99f,.34f),Vector2.zero,Vector2.zero);CreateImage("Event Veil",featured,new Vector2(0,0),new Vector2(1,.9f),Vector2.zero,Vector2.zero,new Color(.004f,.014f,.03f,.5f));Text eventTitle=CreateText("Event Title",featured,"GUARDIÁN DEL CIERZO",26,FontStyle.Bold,TextAnchor.UpperRight,Color.white,new Vector2(0,.6f),new Vector2(.97f,.98f));AddShadow(eventTitle.gameObject,new Color(0,0,0,.9f),2f);Text eventDesc=CreateText("Event Description",featured,"DOMINA LA FOSA. RECLAMA EL ASCENDENTE.",14,FontStyle.Bold,TextAnchor.UpperRight,Muted,new Vector2(0,.4f),new Vector2(.97f,.62f));AddShadow(eventDesc.gameObject,new Color(0,0,0,.85f),1.5f);CreateText("Event State",featured,"◆  EVENTO ACTIVO",13,FontStyle.Bold,TextAnchor.UpperRight,Accent,new Vector2(0,.2f),new Vector2(.97f,.4f));CreateText("Event Dots",featured,"◇    ◇    ◇    ◆",14,FontStyle.Bold,TextAnchor.UpperRight,new Color(Accent.r,Accent.g,Accent.b,.85f),new Vector2(0,.03f),new Vector2(.97f,.2f));
        }
        private void CreateInfoCard(RectTransform parent,string icon,string titleText,string headline,string detail,string action,int index)
        {
            float min=index/3f+.008f,max=(index+1)/3f-.012f;RectTransform card=CreateRect(titleText+" Card",parent,new Vector2(min,0),new Vector2(max,1),Vector2.zero,Vector2.zero);
            CreateImage("Surface",card,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Panel);AddOutline(card.gameObject,new Color(.24f,.5f,.68f,.5f),1f);
            Button interaction=card.gameObject.AddComponent<Button>();ColorBlock colors=interaction.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.55f,.82f,1f,1f);interaction.colors=colors;
            CreateImage("Top Accent",card,new Vector2(0,.965f),Vector2.one,Vector2.zero,Vector2.zero,new Color(Accent.r,Accent.g,Accent.b,.85f)).raycastTarget=false;
            CreateText("Card Icon",card,icon,13,FontStyle.Bold,TextAnchor.MiddleLeft,Accent,new Vector2(.07f,.83f),new Vector2(.2f,.955f));CreateText("Card Title",card,titleText.ToUpperInvariant(),13,FontStyle.Bold,TextAnchor.MiddleLeft,Color.white,new Vector2(.2f,.83f),new Vector2(.94f,.955f));
            CreateImage("Header Divider",card,new Vector2(.07f,.8f),new Vector2(.93f,.807f),Vector2.zero,Vector2.zero,new Color(.3f,.5f,.65f,.35f)).raycastTarget=false;
            CreateRaw("Thumbnail",card,new Vector2(.07f,.44f),new Vector2(.93f,.78f),Vector2.zero,Vector2.zero,keyVisual);
            CreateText("Headline",card,headline,13,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.07f,.26f),new Vector2(.93f,.42f));CreateText("Detail",card,detail,11,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.07f,.11f),new Vector2(.93f,.26f));CreateText("Action",card,action,11,FontStyle.Bold,TextAnchor.LowerLeft,Accent,new Vector2(.07f,.02f),new Vector2(.93f,.12f));
        }
        private void BuildHeroes()
        {
            heroCatalog??=HeroCatalog.Shared;selectedHero??=heroCatalog.DefaultHero;
            foreach(HeroDefinition hero in heroCatalog.Heroes)
            {
                hero?.TryRefreshPresentationFromResources();
                if(hero==null) continue;
                for(int slot=0;slot<4;slot++) hero.GetAbility(slot)?.TryRefreshIconFromResources();
            }
            CreateImage("Heroes Contrast Veil",contentRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.002f,.008f,.02f,.64f));
            CreateImage("Catalog Surface",contentRoot,new Vector2(0,.045f),new Vector2(.595f,.905f),Vector2.zero,Vector2.zero,new Color(.006f,.016f,.035f,.76f));
            CreateText("Heroes Heading",contentRoot,"HÉROES",40,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.015f,.915f),new Vector2(.59f,.99f));
            CreateText("Heroes Subheading",contentRoot,"ELIGE TU ESTILO DE COMBATE",12,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.018f,.885f),new Vector2(.4f,.915f));
            RectTransform filters=CreateRect("Hero Filters",contentRoot,new Vector2(.015f,.795f),new Vector2(.585f,.875f),Vector2.zero,Vector2.zero);
            CreateText("Attack Filter Label",filters,"TIPO",11,FontStyle.Bold,TextAnchor.MiddleLeft,Muted,new Vector2(0,.54f),new Vector2(.13f,1));
            string[] attackFilters={"Todos","Cuerpo a cuerpo","Distancia"};
            for(int i=0;i<attackFilters.Length;i++){int index=i;Button filter=CreateChip("Attack Filter "+attackFilters[i],filters,attackFilters[i],new Vector2(.14f+i*.18f,.55f),new Vector2(.31f+i*.18f,.98f),heroFilter==index?Selected:Panel);filter.onClick.AddListener(()=>{heroFilter=index;BuildContent();});}
            CreateText("Role Filter Label",filters,"ROL",11,FontStyle.Bold,TextAnchor.MiddleLeft,Muted,new Vector2(0,.02f),new Vector2(.13f,.46f));
            string[] roleFilters={"Vanguardia","Tirador","Duelista","Mago","Apoyo","Control","Asesino","Utilidad"};
            for(int i=0;i<roleFilters.Length;i++){int index=i+3;float x=.14f+i*.105f;Button filter=CreateChip("Role Filter "+roleFilters[i],filters,roleFilters[i],new Vector2(x,.02f),new Vector2(x+.097f,.45f),heroFilter==index?Selected:Panel);filter.onClick.AddListener(()=>{heroFilter=index;BuildContent();});}
            RectTransform grid=CreateRect("Hero Grid",contentRoot,new Vector2(.015f,.07f),new Vector2(.585f,.775f),Vector2.zero,Vector2.zero);int visible=0;
            foreach(HeroDefinition hero in heroCatalog.Heroes)
            {
                if(hero==null||!MatchesFilter(hero))continue;int slot=visible++;const int columns=8;int column=slot%columns,row=slot/columns;float minX=column/(float)columns+.008f,maxX=(column+1)/(float)columns-.008f,minY=.67f-row*.3f,maxY=.955f-row*.3f;
                RectTransform card=CreateRect("Hero "+hero.HeroId,grid,new Vector2(minX,minY),new Vector2(maxX,maxY),Vector2.zero,Vector2.zero);Image surface=CreateImage("Surface",card,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.025f,.052f,.09f,.98f));Button choose=card.gameObject.AddComponent<Button>();choose.onClick.AddListener(()=>{selectedHero=hero;BuildContent();});HeroCardHover hover=card.gameObject.AddComponent<HeroCardHover>();hover.Configure(hero==selectedHero?1.01f:1.025f);
                // Outline needs a Graphic on its own object. Applying it to the
                // card container rendered nothing; the visible surface owns it.
                if(hero==selectedHero)AddOutline(surface.gameObject,new Color(.88f,.66f,.20f,1f),3f);
                CreatePortrait("Portrait",card,new Vector2(.045f,.045f),new Vector2(.955f,.955f),hero.Portrait,hero.ThemeColor);
            }
            BuildHeroDetail(selectedHero);
        }
        private bool MatchesFilter(HeroDefinition hero)
        {
            if(heroFilter==0)return true;if(heroFilter==1)return hero.AttackStyle==HeroAttackStyle.Melee;if(heroFilter==2)return hero.AttackStyle==HeroAttackStyle.Ranged;
            return (int)hero.PrimaryRole==heroFilter-3||(int)hero.SecondaryRole==heroFilter-3;
        }
        private void BuildHeroDetail(HeroDefinition hero)
        {
            if(hero==null)return;RectTransform detail=CreateRect("Hero Detail",contentRoot,new Vector2(.615f,.07f),new Vector2(.99f,.895f),Vector2.zero,Vector2.zero);Image surface=CreateImage("Surface",detail,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.008f,.019f,.042f,.985f));Outline panelOutline=detail.gameObject.AddComponent<Outline>();panelOutline.effectColor=new Color(.16f,.48f,.68f,.48f);panelOutline.effectDistance=new Vector2(1f,-1f);
            // The portrait is deliberately kept free of copy and fitted inside its
            // frame: this preserves the whole hero illustration instead of using
            // it as a cropped background behind labels.
            CreatePortraitFit("Portrait",detail,new Vector2(.04f,.595f),new Vector2(.96f,.98f),hero.Portrait,hero.ThemeColor);
            CreateText("Name",detail,hero.DisplayName.ToUpperInvariant(),28,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.07f,.545f),new Vector2(.93f,.59f));CreateText("Epithet",detail,LocalizedEpithet(hero),13,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.07f,.515f),new Vector2(.93f,.545f));
            CreatePill("Primary Role",detail,RoleLabel(hero.PrimaryRole),new Vector2(.07f,.475f),new Vector2(.29f,.505f),hero.ThemeColor);CreatePill("Secondary Role",detail,RoleLabel(hero.SecondaryRole),new Vector2(.30f,.475f),new Vector2(.52f,.505f),Panel);CreatePill("Attack Style",detail,AttackLabel(hero.AttackStyle),new Vector2(.53f,.475f),new Vector2(.70f,.505f),Panel);CreatePill("Difficulty",detail,DifficultyDots(hero.Difficulty),new Vector2(.71f,.475f),new Vector2(.93f,.505f),new Color(.16f,.10f,.22f,.96f));
            CreateText("Attributes Heading",detail,"ATRIBUTOS",11,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.07f,.435f),new Vector2(.93f,.465f));CreateAttribute(detail,"FUERZA",AttributeStrength(hero),AttributeStrengthGrowth(hero),new Color(.88f,.34f,.22f),0);CreateAttribute(detail,"AGILIDAD",AttributeAgility(hero),AttributeAgilityGrowth(hero),new Color(.32f,.82f,.44f),1);CreateAttribute(detail,"INTELIGENCIA",AttributeIntelligence(hero),AttributeIntelligenceGrowth(hero),new Color(.25f,.68f,1f),2);
            CreateResourceBar(detail,"VIDA",hero.BaseHealth,hero.HealthPerLevel,new Color(.19f,.63f,.35f),.355f);CreateResourceBar(detail,"MANÁ",hero.BaseMana,hero.ManaPerLevel,new Color(.15f,.47f,.78f),.297f);
            CreateText("Stats Heading",detail,"ESTADÍSTICAS DE COMBATE",11,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.07f,.255f),new Vector2(.93f,.282f));CreateStatChip(detail,"DAÑO",hero.BaseDamage.ToString("0"),0,0);CreateStatChip(detail,"RANGO",hero.AttackRange.ToString("0.0"),1,0);CreateStatChip(detail,"VEL. ATAQUE",(1f/hero.AttackInterval).ToString("0.00")+"/s",2,0);
            CreateText("Abilities Heading",detail,"HABILIDADES",11,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.07f,.175f),new Vector2(.93f,.198f));string[] keys={"Q","W","E","R"};for(int i=0;i<4;i++)CreateAbilityTile(detail,hero,hero.GetAbility(i),keys[i],i);
            Button select=CreateButton("Select Hero",detail,"✓  ELEGIR PARA JUGAR",new Vector2(.54f,.01f),new Vector2(.93f,.045f),Selected);select.onClick.AddListener(()=>{selectedHero=hero;OpenPlayPanel();});
        }
        private Button CreateChip(string name,Transform parent,string label,Vector2 min,Vector2 max,Color color)
        {
            Image image=CreateImage(name,parent,min,max,Vector2.zero,Vector2.zero,color);Button button=image.gameObject.AddComponent<Button>();ColorBlock states=button.colors;states.normalColor=Color.white;states.highlightedColor=new Color(.7f,.9f,1f,1f);states.pressedColor=new Color(.44f,.72f,.9f,1f);button.colors=states;CreateText("Label",image.transform,label,12,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white,Vector2.zero,Vector2.one);return button;
        }
        private void CreatePill(string name,Transform parent,string label,Vector2 min,Vector2 max,Color color)
        {
            Image pill=CreateImage(name,parent,min,max,Vector2.zero,Vector2.zero,color);pill.raycastTarget=false;CreateText("Label",pill.transform,label,10,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white,Vector2.zero,Vector2.one);
        }
        private void CreateHeroCardInfo(RectTransform card,HeroDefinition hero,bool isSelected)
        {
            Image surface=CreateImage("Card Text Veil",card,new Vector2(.04f,.05f),new Vector2(.96f,.42f),Vector2.zero,Vector2.zero,new Color(.006f,.014f,.03f,.96f));surface.raycastTarget=false;
            VerticalLayoutGroup layout=surface.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(18,18,14,12);layout.spacing=6f;layout.childAlignment=TextAnchor.UpperLeft;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            CreateLayoutText("Name",surface.transform,hero.DisplayName,18,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,24f,1f);
            CreateLayoutText("Role",surface.transform,$"{RoleLabel(hero.PrimaryRole)} · {AttackLabel(hero.AttackStyle)}",12,FontStyle.Bold,TextAnchor.UpperLeft,Muted,16f,1f);
            RectTransform spacer=CreateRect("Card Spacer",surface.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);LayoutElement spacerLayout=spacer.gameObject.AddComponent<LayoutElement>();spacerLayout.flexibleHeight=1f;spacerLayout.minHeight=4f;
            RectTransform footer=CreateRect("Card Footer",surface.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);LayoutElement footerLayout=footer.gameObject.AddComponent<LayoutElement>();footerLayout.preferredHeight=18f;footerLayout.flexibleHeight=0f;HorizontalLayoutGroup row=footer.gameObject.AddComponent<HorizontalLayoutGroup>();row.spacing=6f;row.childAlignment=TextAnchor.LowerLeft;row.childControlWidth=true;row.childControlHeight=true;row.childForceExpandWidth=false;row.childForceExpandHeight=false;
            CreateLayoutText("Difficulty",footer,$"DIFICULTAD  {DifficultyDots(hero.Difficulty)}",10,FontStyle.Bold,TextAnchor.LowerLeft,hero.ThemeColor,18f,1f);
            CreateLayoutText("Selected",footer,isSelected?"✓  SELECCIONADO":"VER DETALLE  ›",10,FontStyle.Bold,TextAnchor.LowerRight,Accent,18f,0f,96f);
            // The cards are created at runtime. Force their layout now so all
            // three text rows receive their calculated position in this frame.
            LayoutRebuilder.ForceRebuildLayoutImmediate(surface.rectTransform);
        }
        private Text CreateLayoutText(string name,Transform parent,string value,int size,FontStyle style,TextAnchor alignment,Color color,float height,float flexibleWidth,float preferredWidth=-1f)
        {
            Text text=CreateText(name,parent,value,size,style,alignment,color,Vector2.zero,Vector2.one);LayoutElement layout=text.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=height;layout.flexibleWidth=flexibleWidth;if(preferredWidth>0f)layout.preferredWidth=preferredWidth;return text;
        }
        private void CreateStatChip(Transform parent,string label,string value,int column,int row)
        {
            float x=.07f+column*.287f,y=.198f-row*.055f;Image chip=CreateImage("Stat "+label,parent,new Vector2(x,y),new Vector2(x+.27f,y+.043f),Vector2.zero,Vector2.zero,new Color(.025f,.065f,.105f,.94f));chip.raycastTarget=false;CreateText("Label",chip.transform,label,8,FontStyle.Bold,TextAnchor.MiddleLeft,Muted,new Vector2(.08f,.12f),new Vector2(.52f,.88f));CreateText("Value",chip.transform,value,11,FontStyle.Bold,TextAnchor.MiddleRight,Color.white,new Vector2(.52f,.12f),new Vector2(.92f,.88f));
        }
        private void CreateAttribute(Transform parent,string label,int value,float growth,Color color,int column)
        {
            float x=.07f+column*.287f;Image chip=CreateImage("Attribute "+label,parent,new Vector2(x,.375f),new Vector2(x+.27f,.425f),Vector2.zero,Vector2.zero,new Color(.018f,.045f,.078f,.94f));chip.raycastTarget=false;CreateImage("Mark",chip.transform,new Vector2(.055f,.2f),new Vector2(.095f,.8f),Vector2.zero,Vector2.zero,color).raycastTarget=false;CreateText("Label",chip.transform,label,8,FontStyle.Bold,TextAnchor.MiddleLeft,Muted,new Vector2(.13f,.1f),new Vector2(.57f,.9f));CreateText("Value",chip.transform,value+" +"+growth.ToString("0.0"),12,FontStyle.Bold,TextAnchor.MiddleRight,Color.white,new Vector2(.55f,.1f),new Vector2(.94f,.9f));
        }
        private void CreateResourceBar(Transform parent,string label,float value,float growth,Color color,float y)
        {
            Image track=CreateImage(label+" Bar",parent,new Vector2(.07f,y),new Vector2(.93f,y+.047f),Vector2.zero,Vector2.zero,new Color(.014f,.035f,.064f,.98f));track.raycastTarget=false;CreateImage("Fill",track.transform,new Vector2(.01f,.12f),new Vector2(.99f,.88f),Vector2.zero,Vector2.zero,color).raycastTarget=false;CreateText("Label",track.transform,label,9,FontStyle.Bold,TextAnchor.MiddleLeft,Color.white,new Vector2(.04f,0),new Vector2(.30f,1));CreateText("Value",track.transform,value.ToString("0")+"  +"+growth.ToString("0.0"),13,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white,new Vector2(.28f,0),new Vector2(.72f,1));CreateText("Growth",track.transform,"POR NIVEL",8,FontStyle.Bold,TextAnchor.MiddleRight,new Color(1f,1f,1f,.8f),new Vector2(.70f,0),new Vector2(.96f,1));
        }
        private void CreateAbilityTile(RectTransform detail,HeroDefinition hero,AbilityDefinition ability,string key,int slot)
        {
            // Keep the four controls as a tight, square icon strip. Their size is
            // calculated against the detail panel's aspect ratio, so they do not
            // become the wide text buttons used by the former layout.
            // The horizontal anchor span determines the tile width. A 1:1 fitter
            // then derives its height from that width, keeping icons square at
            // every game-view aspect ratio instead of compressing them vertically.
            float x=.07f+slot*.115f;Image tile=CreateImage("Ability "+key,detail,new Vector2(x,.055f),new Vector2(x+.11f,.055f),Vector2.zero,Vector2.zero,slot==3?new Color(.135f,.075f,.19f,.98f):new Color(.025f,.065f,.105f,.98f));tile.rectTransform.pivot=new Vector2(.5f,0f);AspectRatioFitter square=tile.gameObject.AddComponent<AspectRatioFitter>();square.aspectMode=AspectRatioFitter.AspectMode.WidthControlsHeight;square.aspectRatio=1f;Button button=tile.gameObject.AddComponent<Button>();ColorBlock states=button.colors;states.normalColor=Color.white;states.highlightedColor=new Color(.68f,.9f,1f,1f);states.pressedColor=new Color(.42f,.7f,.9f,1f);button.colors=states;
            Texture2D iconTexture;Rect iconUv;bool hasIcon=HudIconLibrary.TryGetAbility(ability,out iconTexture,out iconUv);RawImage icon=CreateRaw("Icon",tile.transform,new Vector2(.055f,.055f),new Vector2(.945f,.945f),Vector2.zero,Vector2.zero,iconTexture);icon.raycastTarget=false;icon.uvRect=iconUv;icon.color=hasIcon?Color.white:(slot==3?new Color(.78f,.51f,.22f,1f):hero.ThemeColor);CreateImage("Key Plate",tile.transform,new Vector2(.045f,.045f),new Vector2(.30f,.30f),Vector2.zero,Vector2.zero,slot==3?new Color(.40f,.18f,.12f,.94f):new Color(.02f,.08f,.14f,.94f)).raycastTarget=false;CreateText("Key",tile.transform,key,14,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white,new Vector2(.045f,.045f),new Vector2(.30f,.30f));
            RectTransform tooltip=CreateRect("Tooltip "+key,detail,new Vector2(x,.215f),new Vector2(Mathf.Min(.93f,x+.43f),.405f),Vector2.zero,Vector2.zero);Image tooltipSurface=CreateImage("Surface",tooltip,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.003f,.009f,.022f,.98f));Outline tooltipOutline=tooltip.gameObject.AddComponent<Outline>();tooltipOutline.effectColor=new Color(Accent.r,Accent.g,Accent.b,.76f);tooltipOutline.effectDistance=new Vector2(1f,-1f);CreateText("Title",tooltip,ability==null?"Sin habilidad":key+" · "+AbilityName(ability),12,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.06f,.76f),new Vector2(.94f,.95f));CreateText("Body",tooltip,ability==null?string.Empty:AbilityTooltipText(ability),10,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.06f,.08f),new Vector2(.94f,.73f));tooltip.gameObject.SetActive(false);HeroAbilityTooltip tooltipController=tile.gameObject.AddComponent<HeroAbilityTooltip>();tooltipController.Configure(tooltip.gameObject);
        }
        private static string RoleLabel(HeroRole role) => role switch { HeroRole.Vanguard=>"Vanguardia",HeroRole.Carry=>"Tirador",HeroRole.Duelist=>"Duelista",HeroRole.Mage=>"Mago",HeroRole.Support=>"Apoyo",HeroRole.Controller=>"Controlador",HeroRole.Assassin=>"Asesino",HeroRole.Utility=>"Utilidad",_=>role.ToString() };
        private static string AttackLabel(HeroAttackStyle style) => style==HeroAttackStyle.Melee?"Cuerpo a cuerpo":"Distancia";
        private static string DifficultyDots(int difficulty) => difficulty switch { 1=>"● ○ ○",2=>"● ● ○",_=>"● ● ●" };
        // These display attributes are calculated exclusively from the existing
        // balance fields, so the presentation gains a readable attribute layer
        // without creating a second, divergent set of gameplay numbers.
        private static int AttributeStrength(HeroDefinition hero) => Mathf.Clamp(Mathf.RoundToInt((hero.BaseHealth-250f)/14f),8,40);
        private static float AttributeStrengthGrowth(HeroDefinition hero) => hero.HealthPerLevel/20f;
        private static int AttributeAgility(HeroDefinition hero) => Mathf.Clamp(Mathf.RoundToInt((hero.BaseDamage+hero.MoveSpeed*4f)/4f),8,40);
        private static float AttributeAgilityGrowth(HeroDefinition hero) => hero.DamagePerLevel/3f+hero.MoveSpeedPerLevel*4f;
        private static int AttributeIntelligence(HeroDefinition hero) => Mathf.Clamp(Mathf.RoundToInt(hero.BaseMana/15f),8,40);
        private static float AttributeIntelligenceGrowth(HeroDefinition hero) => hero.ManaPerLevel/10f;
        private static string LocalizedEpithet(HeroDefinition hero) => hero.HeroId switch { "stone_aegis"=>"Bastión del vendaval", "rift_duelist"=>"Filo en la frontera del vendaval", "skyline_marksman"=>"Flecha de la corriente abierta", "storm_warden"=>"Guardián de la alta corriente", "cairn_warden"=>"Refugio de los tocados por la tormenta", "tempest_arbiter"=>"Voz entre los nubarrones", "ember_bastion"=>"Escudo del horno viviente", "ironroot_colossus"=>"La cuenca que camina", "frostveil_sentinel"=>"Vigía del paso azul", "ashen_vow"=>"Hoja juramentada de la última chispa", "zephyr_reaver"=>"Filo del cielo sin amarras", "thornbound"=>"La zarza que no cede", "cinderlash"=>"La respuesta del incendio", "sunspoke_ranger"=>"Flecha de la primera luz", "glaciershard"=>"El invierno de cristal", "verdant_cantor"=>"Voz de la corriente verde", "umbral_sable"=>"El ocaso entre chispas", "lumenweaver"=>"Tejedora de la línea del alba", "prism_oracle"=>"Lectora de mil refracciones", "tidebinder"=>"Guardián de la orilla retornante", _=>hero.Epithet };
        private static string LocalizedDescription(HeroDefinition hero) => hero.HeroId switch { "stone_aegis"=>"Iniciador resistente que protege terreno y dispersa a quienes intentan asaltar su posición.", "rift_duelist"=>"Hostigador de corta distancia que convierte el ritmo del duelo en presión constante.", "skyline_marksman"=>"Portador a distancia que castiga con precisión y domina las líneas abiertas.", "storm_warden"=>"Mago de combate que castiga formaciones agrupadas con energía tormentosa.", "cairn_warden"=>"Apoyo protector que intercambia daño por barreras y supervivencia para su equipo.", "tempest_arbiter"=>"Controlador de zonas que niega rutas y aísla objetivos con tormentas arcanas.", "ember_bastion"=>"Vanguardia forjada en fuego que convierte la presión enemiga en una primera línea abrasadora.", "ironroot_colossus"=>"Guardián de piedra y raíz que ancla a sus aliados mediante control del terreno.", "frostveil_sentinel"=>"Centinela de hielo que domina las entradas y protege las retiradas.", "ashen_vow"=>"Caballero híbrido que vence en duelos prolongados mediante riesgo disciplinado.", "zephyr_reaver"=>"Asesino móvil que abre una brecha y desaparece antes de la respuesta enemiga.", "thornbound"=>"Luchador persistente que cambia alcance por presión y capacidad de persecución.", "cinderlash"=>"Hostigador de látigo que se lanza al combate, pero cae ante un contraataque sostenido.", "sunspoke_ranger"=>"Tirador físico paciente que recompensa la posición y las líneas de visión despejadas.", "glaciershard"=>"Mago de artillería cuyos fragmentos castigan a los enemigos agrupados.", "verdant_cantor"=>"Apoyo utilitario que abre rutas seguras con canciones vivas y presión sostenida.", "umbral_sable"=>"Asesino híbrido de sombras, letal en flancos y vulnerable cuando falla.", "lumenweaver"=>"Apoyo defensivo de luz especializado en salvar aliados, no en rematar enemigos.", "prism_oracle"=>"Controlador arcano preciso que convierte las rutas enemigas en terreno peligroso.", "tidebinder"=>"Controlador de agua que impulsa rotaciones y protege la retaguardia.", _=>hero.Description };
        private static string AbilityName(AbilityDefinition ability) => ability.AbilityId switch { "rampart_strike"=>"Golpe de Muralla", "windward_guard"=>"Guardia de Barlovento", "grounding_ring"=>"Anillo de Tierra", "citadel_crash"=>"Impacto de Ciudadela", "rift_lunge"=>"Estocada de Brecha", "duelist_wind"=>"Viento del Duelista", "counterveil"=>"Velo de Réplica", "redline"=>"Línea Roja", "piercing_gale"=>"Vendaval Perforante", "tailwind"=>"Viento de Cola", "updraft_step"=>"Paso Ascendente", "horizon_breaker"=>"Ruptura del Horizonte", "arc_bolt"=>"Rayo de Arco", "storm_mark"=>"Marca de Tormenta", "gale_step"=>"Paso de Vendaval", "tempest_fall"=>"Caída de Tempestad", "kindling_orb"=>"Orbe de Ascua", "cairn_barrier"=>"Barrera de Cairn", "restoring_draft"=>"Brisa Restauradora", "sanctuary_field"=>"Campo Santuario", "pressure_drop"=>"Caída de Presión", "static_lattice"=>"Retícula Estática", "crosswind"=>"Viento Cruzado", "eye_of_tempest"=>"Ojo de la Tempestad", "ember_bastion.cinder_ram"=>"Embestida de Ceniza", "ember_bastion.kiln_plate"=>"Placa del Horno", "ember_bastion.ash_ring"=>"Anillo de Ceniza", "ember_bastion.furnace_gate"=>"Puerta del Horno", "ironroot_colossus.root_hook"=>"Gancho de Raíces", "ironroot_colossus.barkward"=>"Corteza Protectora", "ironroot_colossus.moss_quake"=>"Sismo de Musgo", "ironroot_colossus.worldroot"=>"Raíz del Mundo", "frostveil_sentinel.ice_lance"=>"Lanza de Hielo", "frostveil_sentinel.rime_guard"=>"Guardia de Escarcha", "frostveil_sentinel.hoarfrost_field"=>"Campo de Cencellada", "frostveil_sentinel.whiteout"=>"Ventisca", "ashen_vow.oath_cleave"=>"Tajo del Juramento", "ashen_vow.cinder_oath"=>"Juramento de Ceniza", "ashen_vow.penitent_stride"=>"Paso Penitente", "ashen_vow.last_vow"=>"Último Juramento", "zephyr_reaver.gust_cut"=>"Corte de Ráfaga", "zephyr_reaver.windskin"=>"Piel de Viento", "zephyr_reaver.slipstream"=>"Corriente de Deslizamiento", "zephyr_reaver.sky_sunder"=>"Hendidura Celeste", "thornbound.briar_snap"=>"Chasquido de Zarzas", "thornbound.ironbark"=>"Corteza de Hierro", "thornbound.creeping_vines"=>"Enredaderas Rastreras", "thornbound.wild_bloom"=>"Floración Salvaje", "cinderlash.flare_lash"=>"Látigo de Llamas", "cinderlash.coal_guard"=>"Guardia de Carbón", "cinderlash.sparkskip"=>"Salto de Chispas", "cinderlash.pyreline"=>"Línea de Pira", "sunspoke_ranger.sunpin"=>"Clavo Solar", "sunspoke_ranger.glare_ward"=>"Guardia de Resplandor", "sunspoke_ranger.golden_draft"=>"Corriente Dorada", "sunspoke_ranger.dawnspear"=>"Lanza del Alba", "glaciershard.shatterbolt"=>"Rayo Quebrado", "glaciershard.crystal_sheen"=>"Brillo Cristalino", "glaciershard.permafrost"=>"Permafrost", "glaciershard.avalanche_prism"=>"Prisma de Avalancha", "verdant_cantor.seedburst"=>"Estallido de Semilla", "verdant_cantor.chorus_bark"=>"Corteza del Coro", "verdant_cantor.trailing_song"=>"Canción Persistente", "verdant_cantor.grove_refrain"=>"Estribillo de Arboleda", "umbral_sable.night_pierce"=>"Perforación Nocturna", "umbral_sable.eclipsed_skin"=>"Piel Eclipsada", "umbral_sable.duskstep"=>"Paso del Crepúsculo", "umbral_sable.black_sun"=>"Sol Negro", "lumenweaver.lumen_orb"=>"Orbe de Lumen", "lumenweaver.prism_shroud"=>"Manto Prismático", "lumenweaver.guiding_ray"=>"Rayo Guía", "lumenweaver.daybreak_circle"=>"Círculo del Amanecer", "prism_oracle.refraction"=>"Refracción", "prism_oracle.mirror_shell"=>"Caparazón Espejo", "prism_oracle.angle_shift"=>"Cambio de Ángulo", "prism_oracle.zenith_array"=>"Matriz Cenital", "tidebinder.surge_orb"=>"Orbe de Oleaje", "tidebinder.foamward"=>"Guardia de Espuma", "tidebinder.currentwalk"=>"Camino de Corriente", "tidebinder.maelstrom_basin"=>"Cuenca del Maelstrom", _=>ability.DisplayName };
        private static string AbilityTooltipText(AbilityDefinition ability)
        {
            string effect=ability.Effect switch { AbilityEffect.ProjectileDamage=>"Lanza un proyectil que inflige daño.",AbilityEffect.AreaDamage=>"Daña a los enemigos en el área objetivo.",AbilityEffect.SelfMoveSpeed=>"Aumenta temporalmente la velocidad de movimiento.",AbilityEffect.StrongAreaDamage=>"Inflige gran daño en un área.",AbilityEffect.AreaSlow=>"Ralentiza a los enemigos dentro del área.",AbilityEffect.StrongAreaStun=>"Aturde a los enemigos dentro del área.",AbilityEffect.SelfShield=>"Otorga un escudo protector.",_=>"Habilidad de combate."};
            return $"{effect}\nAlcance {ability.Range:0.#} · Radio {ability.AreaRadius:0.#} · Duración {ability.Duration:0.#} s\n{AbilityRanks(ability)}";
        }
        private static string AbilityRanks(AbilityDefinition ability)
        {
            string result=string.Empty;
            for(int level=1;level<=ability.MaximumLevel;level++) result+=(level>1?" · ":string.Empty)+$"N{level}: {ability.EffectValue(level):0} / {ability.ManaCost(level):0}M / {ability.Cooldown(level):0.#}s";
            return result;
        }
        private void OpenPlayPanel()
        {
            section=0;
            playPanelOpen=true;
            BuildContent();
            BuildTopNavigationRefresh();
        }

        private void BuildPlayPanel()
        {
            selectedHero ??= HeroCatalog.Shared.DefaultHero;
            // The drawer shares the same right-hand footprint as the room UI. It
            // belongs to the menu root (rather than Main Content) so it is flush
            // with the right edge and spans almost the full area below the nav.
            playPanelRoot=CreateRect("Play Panel Layer",menuRoot,Vector2.zero,new Vector2(1f,.905f),Vector2.zero,Vector2.zero);
            Image veil=CreateImage("Play Focus Veil",playPanelRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.001f,.006f,.016f,.58f));
            Button dismiss=veil.gameObject.AddComponent<Button>();
            dismiss.onClick.AddListener(()=>{playPanelOpen=false;BuildContent();});
            Image panel=CreateImage("Play Options Panel",playPanelRoot,new Vector2(.73f,.045f),new Vector2(.985f,.99f),Vector2.zero,Vector2.zero,new Color(.008f,.024f,.047f,.99f));
            AddOutline(panel.gameObject,new Color(.14f,.46f,.68f,.68f),1f);
            CreateImage("Panel Top Accent",panel.transform,new Vector2(0,.996f),Vector2.one,Vector2.zero,Vector2.zero,new Color(Accent.r,Accent.g,Accent.b,.82f)).raycastTarget=false;
            CreateText("Play Panel Heading",panel.transform,"JUGAR",25,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.06f,.925f),new Vector2(.94f,.985f));
            CreateText("Play Panel Subheading",panel.transform,"ELIGE CÓMO ENTRAR A LA ARENA",11,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.06f,.875f),new Vector2(.94f,.915f));

            Button multiplayer=CreatePlayPanelOption(panel.transform,"Private Multiplayer","SALA PRIVADA",.790f,Selected);
            multiplayer.interactable=!IsActiveMatchMenu;
            if(!IsActiveMatchMenu)multiplayer.onClick.AddListener(()=>MultiplayerRoomPanel.Show(menuCanvas,selectedHero,team));
            Button local=CreatePlayPanelOption(panel.transform,"Local Development","PARTIDA LOCAL",.720f,Panel);
            local.interactable=!IsActiveMatchMenu;
            if(!IsActiveMatchMenu)local.onClick.AddListener(()=>Launch(FrontendMatchMode.LocalDevelopment));

            CreateText("Play Panel Note",panel.transform,"Las salas privadas usan identidad de invitado, código de unión y Relay.",12,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.06f,.08f),new Vector2(.94f,.15f));
        }

        private Button CreatePlayPanelOption(Transform parent,string name,string titleText,float y,Color color)
        {
            return CreateButton(name,parent,titleText,new Vector2(.06f,y),new Vector2(.94f,y+.052f),color);
        }
        private void BuildPlaceholder(string name){CreateText("Placeholder",contentRoot,name.ToUpperInvariant()+"\n\nPRÓXIMAMENTE",42,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(0,.55f),new Vector2(.7f,.9f));}
        private Color Accent=>theme!=null?theme.azure:new Color(.18f,.72f,1f);private Color Muted=>theme!=null?theme.muted:new Color(.64f,.72f,.8f);private Color Panel=>theme!=null?theme.panel:new Color(.075f,.105f,.16f,.94f);private Color Selected=>theme!=null?theme.selected:new Color(.1f,.42f,.62f);
        private RectTransform CreateRect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax){GameObject item=new GameObject(name,typeof(RectTransform));RectTransform rect=item.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;return rect;}
        private Image CreateImage(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax,Color color){RectTransform rect=CreateRect(name,parent,min,max,offsetMin,offsetMax);Image image=rect.gameObject.AddComponent<Image>();image.color=color;return image;}
        private RawImage CreateRaw(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax,Texture texture){RectTransform rect=CreateRect(name,parent,min,max,offsetMin,offsetMax);RawImage image=rect.gameObject.AddComponent<RawImage>();image.texture=texture;image.color=Color.white;return image;}
        private RawImage CreatePortrait(string name,Transform parent,Vector2 min,Vector2 max,Texture texture,Color fallback)
        {
            // The fitter enlarges a square portrait to cover a wide card.  Keep that
            // enlargement inside a masked frame so artwork can never spill over
            // labels, filters, or neighbouring controls.
            Image frame=CreateImage(name+" Frame",parent,min,max,Vector2.zero,Vector2.zero,Color.white);
            frame.raycastTarget=false;Mask mask=frame.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            RawImage image=CreateRaw(name,frame.transform,new Vector2(.5f,1f),new Vector2(.5f,1f),Vector2.zero,Vector2.zero,texture);
            image.rectTransform.pivot=new Vector2(.5f,1f);image.raycastTarget=false;image.color=texture!=null?Color.white:fallback*.65f;
            AspectRatioFitter fitter=image.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fitter.aspectRatio=texture!=null&&texture.height>0?(float)texture.width/texture.height:1f;
            return image;
        }
        private RawImage CreatePortraitFit(string name,Transform parent,Vector2 min,Vector2 max,Texture texture,Color fallback)
        {
            Image frame=CreateImage(name+" Frame",parent,min,max,Vector2.zero,Vector2.zero,Color.Lerp(fallback,new Color(.004f,.01f,.025f),.78f));frame.raycastTarget=false;Mask mask=frame.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            RawImage image=CreateRaw(name,frame.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,texture);image.rectTransform.pivot=new Vector2(.5f,.5f);image.raycastTarget=false;image.color=texture!=null?Color.white:fallback*.65f;AspectRatioFitter fitter=image.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=texture!=null&&texture.height>0?(float)texture.width/texture.height:1f;return image;
        }
        private Text CreateText(string name,Transform parent,string value,int size,FontStyle style,TextAnchor alignment,Color color,Vector2 min,Vector2 max){RectTransform rect=CreateRect(name,parent,min,max,Vector2.zero,Vector2.zero);Text text=rect.gameObject.AddComponent<Text>();text.font=uiFont;text.text=value;text.fontSize=size;text.fontStyle=style;text.alignment=alignment;text.color=color;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;return text;}
        private Button CreateButton(string name,Transform parent,string label,Vector2 min,Vector2 max,Color color)
        {
            Image image=CreateImage(name,parent,min,max,Vector2.zero,Vector2.zero,color);
            Button button=image.gameObject.AddComponent<Button>();
            ColorBlock states=button.colors;
            states.normalColor=Color.white;
            states.highlightedColor=new Color(.72f,.92f,1f,1f);
            states.pressedColor=new Color(.42f,.66f,.82f,1f);
            states.fadeDuration=.08f;
            button.colors=states;
            AddOutline(image.gameObject,new Color(.42f,.68f,.84f,.52f),1f);
            AddShadow(image.gameObject,new Color(0f,0f,0f,.68f),1.5f);
            Image topBevel=CreateImage("Top Bevel",image.transform,new Vector2(.025f,.92f),new Vector2(.975f,.965f),Vector2.zero,Vector2.zero,new Color(1f,1f,1f,.16f));
            topBevel.raycastTarget=false;
            Image bottomBevel=CreateImage("Bottom Bevel",image.transform,new Vector2(.025f,.035f),new Vector2(.975f,.09f),Vector2.zero,Vector2.zero,new Color(0f,0f,0f,.34f));
            bottomBevel.raycastTarget=false;
            CreateText("Label",image.transform,label,16,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white,Vector2.zero,Vector2.one);
            return button;
        }
        private Outline AddOutline(GameObject target,Color color,float distance=1f){Outline outline=target.AddComponent<Outline>();outline.effectColor=color;outline.effectDistance=new Vector2(distance,-distance);return outline;}
        private void AddShadow(GameObject target,Color color,float distance=2f){Shadow shadow=target.AddComponent<Shadow>();shadow.effectColor=color;shadow.effectDistance=new Vector2(distance,-distance);}
        private static Texture2D MakeAxisGradient(Color color,float startAlpha,float endAlpha,bool horizontal,int steps=128){Texture2D tex=new Texture2D(horizontal?steps:1,horizontal?1:steps,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};for(int i=0;i<steps;i++){float t=Mathf.SmoothStep(0f,1f,i/(float)(steps-1));float a=Mathf.Lerp(startAlpha,endAlpha,t);Color c=new Color(color.r,color.g,color.b,a);if(horizontal)tex.SetPixel(i,0,c);else tex.SetPixel(0,steps-1-i,c);}tex.Apply();return tex;}
        private void EnsureStyles(){if(title!=null)return;Color text=theme!=null?theme.text:Color.white;Color mutedColor=theme!=null?theme.muted:new Color(.7f,.75f,.8f);Color panel=theme!=null?theme.panel:new Color(.08f,.1f,.16f,.94f);title=new GUIStyle(GUI.skin.label){fontSize=60,fontStyle=FontStyle.Bold,normal={textColor=text}};heading=new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold,normal={textColor=text}};body=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true,normal={textColor=text}};muted=new GUIStyle(body){fontSize=15,normal={textColor=mutedColor}};eyebrow=new GUIStyle(body){fontSize=13,fontStyle=FontStyle.Bold,normal={textColor=theme!=null?theme.azure:new Color(.18f,.72f,1f)}};button=new GUIStyle(GUI.skin.button){fontSize=17,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=mutedColor,background=MakeColorTexture(new Color(.03f,.07f,.12f,.78f))},hover={textColor=Color.white,background=MakeColorTexture(new Color(.08f,.18f,.28f,.96f))}};selected=new GUIStyle(button){fontSize=19,normal={textColor=Color.white,background=MakeColorTexture(theme!=null?theme.selected:new Color(.1f,.4f,.6f))},hover={textColor=Color.white,background=MakeColorTexture(new Color(.12f,.52f,.73f))}};card=new GUIStyle(GUI.skin.box){normal={background=MakeColorTexture(panel),textColor=text}};}
        private static Texture2D MakeColorTexture(Color color){Texture2D texture=new Texture2D(1,1);texture.SetPixel(0,0,color);texture.Apply();return texture;}
        private void DrawKeyVisual(float width,float height)
        {
            float targetHeight=height*.95f;float targetWidth=targetHeight*keyVisual.width/keyVisual.height;
            if(targetWidth>width){targetWidth=width;targetHeight=targetWidth*keyVisual.height/keyVisual.width;}
            Rect frame=new Rect(width-targetWidth,(height-targetHeight)*.5f,targetWidth,targetHeight);GUI.DrawTexture(frame,keyVisual,ScaleMode.StretchToFill,true);
        }
        private void Fill(Rect rect,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        private void DrawRotated(Rect rect,float angle,Color color){Matrix4x4 old=GUI.matrix;GUIUtility.RotateAroundPivot(angle,rect.center);Fill(rect,color);GUI.matrix=old;}
    }
}

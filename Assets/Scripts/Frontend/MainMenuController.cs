using CierzoArena.Core;
using CierzoArena.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CierzoArena.Frontend
{
    /// <summary>Presentation-only frontend. It stores a launch request then loads the
    /// existing arena; it never creates a NetworkManager or starts a session itself.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
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
        private RectTransform menuRoot,contentRoot;
        private Font uiFont;
        private bool canvasPresentation;
        private HeroCatalog heroCatalog;
        private HeroDefinition selectedHero;
        private int heroFilter;
        private readonly string[] navigation={"Inicio","Héroes","Jugar","Aprender","Ajustes"};

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
        private void OnGUI()
        {
            if(canvasPresentation)return;
            // Keep a 1920x1080-ish logical canvas at 4K instead of leaving a tiny
            // fixed-width menu in a much wider coordinate space.
            EnsureStyles(); float scale=Mathf.Clamp(Screen.height/1080f,1f,2.25f); Matrix4x4 old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float width=Screen.width/scale,height=Screen.height/scale;
            GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture); Color restore=GUI.color;GUI.color=theme!=null?theme.background:new Color(.035f,.055f,.09f);GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture);GUI.color=restore;
            DrawBackdrop(width,height); DrawTopBar(width); GUI.BeginGroup(new Rect(width*.075f,118,width*.85f,height-145));
            section=ResolveSingleActivePanel(section,navigation.Length);
            if(section==0)DrawHome(width*.86f,height-140);else if(section==1)DrawHeroes(width*.86f,height-140);else if(section==2)DrawPlay(width*.86f,height-140);else if(section==3)DrawPlaceholder("Aprender","Guías, tutoriales y retos llegarán en un próximo milestone.");else DrawSettings(width*.86f,height-140);
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
            float x=425;for(int i=0;i<navigation.Length;i++){GUIStyle style=i==section?selected:button;if(GUI.Button(new Rect(x+i*134,27,124,46),navigation[i],style))section=i;}
            Fill(new Rect(width-365,18,1,62),new Color(.42f,.65f,.78f,.45f));
            GUI.Label(new Rect(width-350,22,58,20),"◉  ✉  ⚙",eyebrow);
            DrawCard(new Rect(width-280,16,235,64),"AERIN · NIVEL 12","● En línea  ·  Perfil provisional");
        }
        private void DrawHome(float width,float height)
        {
            const float column=610f;
            GUI.Label(new Rect(4,43,column,26),"TEMPORADA PROTOTIPO · EL ASCENSO DEL CIERZO",eyebrow);
            GUI.Label(new Rect(0,77,column,132),"EL VIENTO\nDECIDE LA ARENA",title);GUI.Label(new Rect(4,226,540,66),"Un enfrentamiento de estrategia, energía elemental y control del terreno. Reúne tu escuadra y entra a CierzoArena.",body);
            if(GUI.Button(new Rect(4,315,306,66),"✦  JUGAR AHORA",selected))section=2;if(GUI.Button(new Rect(324,315,230,66),"VER HÉROES",button))section=1;
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
        private void DrawPlay(float width,float height)
        {
            GUI.Label(new Rect(0,18,width,42),"JUGAR",title);GUI.Label(new Rect(4,64,width,26),"El frontend prepara la configuración; la arena mantiene toda la autoridad de M18.",muted);
            DrawCard(new Rect(0,110,470,320),"PARTIDA DE DESARROLLO","Inicia una partida completa sin conexión.");if(GUI.Button(new Rect(24,248,420,46),"INICIAR LOCAL",selected))Launch(FrontendMatchMode.LocalDevelopment);
            DrawCard(new Rect(500,110,470,320),"HOST / CLIENT","Dirección y puerto para una prueba directa.");GUI.Label(new Rect(524,190,92,26),"Dirección",body);address=GUI.TextField(new Rect(618,190,300,28),address);GUI.Label(new Rect(524,228,92,26),"Puerto",body);port=GUI.TextField(new Rect(618,228,120,28),port);
            if(GUI.Button(new Rect(524,276,190,42),team==TeamId.Azure?"Equipo: Azure":"Equipo: Ember",button))team=team==TeamId.Azure?TeamId.Ember:TeamId.Azure;
            if(GUI.Button(new Rect(724,276,194,42),"CREAR COMO HOST",selected))Launch(FrontendMatchMode.Host);if(GUI.Button(new Rect(724,326,194,36),"UNIRSE CLIENT",button))Launch(FrontendMatchMode.Client);
            DrawCard(new Rect(0,460,300,120),"PARTIDAS PERSONALIZADAS","Próximamente");DrawCard(new Rect(320,460,300,120),"BUSCAR PARTIDA","Próximamente");DrawCard(new Rect(640,460,300,120),"ENTRENAMIENTO","Próximamente");
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
            menuCanvas=FindAnyObjectByType<Canvas>();
            if(menuCanvas==null){GameObject canvasObject=new GameObject("Main Menu Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));menuCanvas=canvasObject.GetComponent<Canvas>();}
            menuCanvas.renderMode=RenderMode.ScreenSpaceOverlay;CanvasScaler scaler=menuCanvas.GetComponent<CanvasScaler>()??menuCanvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;if(menuCanvas.GetComponent<GraphicRaycaster>()==null)menuCanvas.gameObject.AddComponent<GraphicRaycaster>();
            menuRoot=CreateRect("MainMenu Layers",menuCanvas.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);RawImage keyArt=CreateRaw("Background Key Art",menuRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,keyVisual);AspectRatioFitter keyFit=keyArt.gameObject.AddComponent<AspectRatioFitter>();keyFit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;keyFit.aspectRatio=keyVisual!=null?(float)keyVisual.width/keyVisual.height:16f/9f;keyArt.rectTransform.pivot=new Vector2(1f,.5f);
            CreateImage("Atmospheric Overlay",menuRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.01f,.03f,.07f,.18f));
            leftFadeTex??=MakeAxisGradient(new Color(.004f,.012f,.028f),.92f,0f,true);
            RawImage leftFade=CreateRaw("Left Readability Fade",menuRoot,new Vector2(0,0),new Vector2(.62f,1),Vector2.zero,Vector2.zero,leftFadeTex);leftFade.raycastTarget=false;
            BuildTopNavigation();contentRoot=CreateRect("Main Content",menuRoot,new Vector2(.045f,.11f),new Vector2(.955f,.88f),Vector2.zero,Vector2.zero);BuildContent();canvasPresentation=true;
        }
        private void BuildTopNavigation()
        {
            RectTransform top=CreateRect("Top Navigation",menuRoot,new Vector2(0,.905f),Vector2.one,Vector2.zero,Vector2.zero);
            navFadeTex??=MakeAxisGradient(new Color(.02f,.045f,.09f),.985f,.9f,false);RawImage navSurface=CreateRaw("Navigation Surface",top,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,navFadeTex);navSurface.raycastTarget=true;
            CreateImage("Navigation Bottom Accent",top,new Vector2(0,0),new Vector2(1,.02f),Vector2.zero,Vector2.zero,new Color(Accent.r,Accent.g,Accent.b,.55f)).raycastTarget=false;
            if(logoImage!=null){RectTransform logoSlot=CreateRect("Brand Logo Slot",top,new Vector2(.02f,.12f),new Vector2(.2f,.9f),Vector2.zero,Vector2.zero);RawImage logo=CreateRaw("Brand Logo",logoSlot,new Vector2(0,0),new Vector2(1,1),Vector2.zero,Vector2.zero,logoImage);logo.raycastTarget=false;AspectRatioFitter logoFit=logo.gameObject.AddComponent<AspectRatioFitter>();logoFit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;logoFit.aspectRatio=logoImage.height>0?(float)logoImage.width/logoImage.height:3.4f;logo.rectTransform.pivot=new Vector2(0f,.5f);}
            else{Text brand=CreateText("Brand",top,"CIERZO\nARENA",30,FontStyle.Bold,TextAnchor.MiddleLeft,Color.white,new Vector2(.025f,.08f),new Vector2(.19f,.92f));AddShadow(brand.gameObject,new Color(0,0,0,.8f),2f);CreateText("Brand Tag",top,"ARENA OF THE NORTH WIND",10,FontStyle.Bold,TextAnchor.LowerLeft,Accent,new Vector2(.028f,.04f),new Vector2(.19f,.34f));}
            for(int i=0;i<navigation.Length;i++){int index=i;float x0=.22f+i*.095f,x1=.31f+i*.095f;Button tab=CreateButton("Tab "+navigation[i],top,navigation[i],new Vector2(x0,.22f),new Vector2(x1,.78f),i==section?Selected:Panel);if(i==section)CreateImage("Tab Underline",top,new Vector2(x0+.012f,.15f),new Vector2(x1-.012f,.185f),Vector2.zero,Vector2.zero,Accent).raycastTarget=false;tab.onClick.AddListener(()=>{section=index;BuildContent();BuildTopNavigationRefresh();});}
            CreateText("Currencies",top,"◆ 3 450     ◈ 18 760",14,FontStyle.Bold,TextAnchor.MiddleRight,Accent,new Vector2(.72f,.34f),new Vector2(.84f,.75f));CreateImage("Profile Divider",top,new Vector2(.845f,.18f),new Vector2(.846f,.82f),Vector2.zero,Vector2.zero,new Color(.4f,.7f,.86f,.5f));CreateText("Profile",top,"AERIN\nNIVEL 12",15,FontStyle.Bold,TextAnchor.MiddleLeft,Color.white,new Vector2(.855f,.25f),new Vector2(.94f,.8f));CreateText("Profile Icons",top,"◉   ✉   ⚙",16,FontStyle.Bold,TextAnchor.MiddleCenter,Muted,new Vector2(.94f,.25f),new Vector2(.995f,.8f));
        }
        private void BuildTopNavigationRefresh(){if(menuRoot==null)return;Transform old=menuRoot.Find("Top Navigation");if(old!=null)Destroy(old.gameObject);BuildTopNavigation();}
        private void BuildContent()
        {
            if(contentRoot==null)return;for(int i=contentRoot.childCount-1;i>=0;i--)Destroy(contentRoot.GetChild(i).gameObject);
            if(section==0)BuildHome();else if(section==1)BuildHeroes();else if(section==2)BuildPlay();else BuildPlaceholder(navigation[section]);
        }
        private void BuildHome()
        {
            RectTransform left=CreateRect("Main Hero Copy",contentRoot,new Vector2(0,.18f),new Vector2(.43f,.89f),Vector2.zero,Vector2.zero);
            CreateText("Season",left,"◆  TEMPORADA PROTOTIPO · EL ASCENSO DEL CIERZO",13,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(0,.9f),Vector2.one);
            Text heroTitle=CreateText("Hero Title",left,"EL VIENTO\nDECIDE LA ARENA",58,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(0,.57f),new Vector2(1,.9f));AddShadow(heroTitle.gameObject,new Color(.02f,.05f,.10f,.85f),3f);
            CreateImage("Title Flourish",left,new Vector2(.006f,.545f),new Vector2(.20f,.556f),Vector2.zero,Vector2.zero,new Color(Accent.r,Accent.g,Accent.b,.9f)).raycastTarget=false;CreateText("Title Flourish Mark",left,"◆",13,FontStyle.Bold,TextAnchor.MiddleLeft,Accent,new Vector2(.205f,.533f),new Vector2(.26f,.568f));
            CreateText("Hero Description",left,"Un enfrentamiento de estrategia, energía elemental y control del terreno. Reúne tu escuadra y entra a Cierzo Arena.",18,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.006f,.35f),new Vector2(.9f,.52f));
            Button play=CreateButton("Play Now",left,"✦  JUGAR AHORA",new Vector2(0,.2f),new Vector2(.51f,.32f),Selected);AddOutline(play.gameObject,new Color(Accent.r,Accent.g,Accent.b,.95f),2f);play.onClick.AddListener(()=>{section=2;BuildContent();BuildTopNavigationRefresh();});Button heroesButton=CreateButton("View Heroes",left,"⚔  VER HÉROES",new Vector2(.54f,.2f),new Vector2(.93f,.32f),Panel);AddOutline(heroesButton.gameObject,new Color(.32f,.6f,.78f,.55f),1.5f);heroesButton.onClick.AddListener(()=>{section=1;BuildContent();BuildTopNavigationRefresh();});
            RectTransform cards=CreateRect("Information Cards",contentRoot,new Vector2(0,0),new Vector2(.44f,.22f),Vector2.zero,Vector2.zero);CreateInfoCard(cards,"◈","Noticias","NUEVO PARCHE 1.1.0","Balance de héroes, mejoras y correcciones.","LEER MÁS  ›",0);CreateInfoCard(cards,"◉","Grupo","2 / 5 EN LÍNEA","Grupo provisional · social próximamente.","VER GRUPO  ›",1);CreateInfoCard(cards,"◆","Versión","M19 FRONTEND","Vertical slice visual disponible.","NOTAS  ›",2);
            RectTransform featured=CreateRect("Featured Event",contentRoot,new Vector2(.6f,.12f),new Vector2(.99f,.34f),Vector2.zero,Vector2.zero);CreateImage("Event Veil",featured,new Vector2(0,0),new Vector2(1,.9f),Vector2.zero,Vector2.zero,new Color(.004f,.014f,.03f,.5f));Text eventTitle=CreateText("Event Title",featured,"GUARDIÁN DEL CIERZO",26,FontStyle.Bold,TextAnchor.UpperRight,Color.white,new Vector2(0,.6f),new Vector2(.97f,.98f));AddShadow(eventTitle.gameObject,new Color(0,0,0,.9f),2f);Text eventDesc=CreateText("Event Description",featured,"DOMINA LA FOSA. RECLAMA EL ASCENDENTE.",14,FontStyle.Bold,TextAnchor.UpperRight,Muted,new Vector2(0,.4f),new Vector2(.97f,.62f));AddShadow(eventDesc.gameObject,new Color(0,0,0,.85f),1.5f);CreateText("Event State",featured,"◆  EVENTO ACTIVO",13,FontStyle.Bold,TextAnchor.UpperRight,Accent,new Vector2(0,.2f),new Vector2(.97f,.4f));CreateText("Event Dots",featured,"◇    ◇    ◇    ◆",14,FontStyle.Bold,TextAnchor.UpperRight,new Color(Accent.r,Accent.g,Accent.b,.85f),new Vector2(0,.03f),new Vector2(.97f,.2f));
            CreateDailyChallenges();
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
        private void CreateDailyChallenges(){RectTransform panel=CreateRect("Daily Challenges",contentRoot,new Vector2(.76f,-.02f),new Vector2(1,.09f),Vector2.zero,Vector2.zero);CreateImage("Challenge Surface",panel,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.01f,.03f,.055f,.9f));AddOutline(panel.gameObject,new Color(.24f,.5f,.68f,.5f),1f);CreateImage("Top Accent",panel,new Vector2(0,.94f),Vector2.one,Vector2.zero,Vector2.zero,new Color(Accent.r,Accent.g,Accent.b,.85f)).raycastTarget=false;CreateText("Challenge Icon",panel,"▣",16,FontStyle.Bold,TextAnchor.MiddleLeft,Accent,new Vector2(.04f,.15f),new Vector2(.16f,.9f));CreateText("Challenge",panel,"DESAFÍOS DIARIOS\n3 / 5 COMPLETADOS",13,FontStyle.Bold,TextAnchor.MiddleLeft,Color.white,new Vector2(.17f,.15f),new Vector2(.95f,.9f));CreateImage("Progress Track",panel,new Vector2(.17f,.12f),new Vector2(.7f,.16f),Vector2.zero,Vector2.zero,new Color(.1f,.16f,.24f,.9f)).raycastTarget=false;CreateImage("Progress",panel,new Vector2(.17f,.12f),new Vector2(.49f,.16f),Vector2.zero,Vector2.zero,Accent).raycastTarget=false;}
        private void BuildHeroes()
        {
            heroCatalog??=HeroCatalog.Shared;selectedHero??=heroCatalog.DefaultHero;
            CreateImage("Heroes Contrast Veil",contentRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.002f,.008f,.02f,.64f));
            CreateImage("Catalog Surface",contentRoot,new Vector2(0,.045f),new Vector2(.595f,.905f),Vector2.zero,Vector2.zero,new Color(.006f,.016f,.035f,.76f));
            CreateText("Heroes Heading",contentRoot,"HÉROES · PRIMERA PLANTILLA",40,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.015f,.915f),new Vector2(.59f,.99f));
            CreateText("Heroes Subheading",contentRoot,"ELIGE TU ESTILO DE COMBATE",12,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.018f,.885f),new Vector2(.4f,.915f));
            RectTransform filters=CreateRect("Hero Filters",contentRoot,new Vector2(.015f,.795f),new Vector2(.585f,.875f),Vector2.zero,Vector2.zero);
            CreateText("Attack Filter Label",filters,"TIPO",11,FontStyle.Bold,TextAnchor.MiddleLeft,Muted,new Vector2(0,.54f),new Vector2(.13f,1));
            string[] attackFilters={"Todos","Melee","Distancia"};
            for(int i=0;i<attackFilters.Length;i++){int index=i;Button filter=CreateChip("Attack Filter "+attackFilters[i],filters,attackFilters[i],new Vector2(.14f+i*.18f,.55f),new Vector2(.31f+i*.18f,.98f),heroFilter==index?Selected:Panel);filter.onClick.AddListener(()=>{heroFilter=index;BuildContent();});}
            CreateText("Role Filter Label",filters,"ROL",11,FontStyle.Bold,TextAnchor.MiddleLeft,Muted,new Vector2(0,.02f),new Vector2(.13f,.46f));
            string[] roleFilters={"Vanguardia","Carry","Duelista","Mago","Apoyo","Control"};
            for(int i=0;i<roleFilters.Length;i++){int index=i+3;float x=.14f+i*.142f;Button filter=CreateChip("Role Filter "+roleFilters[i],filters,roleFilters[i],new Vector2(x,.02f),new Vector2(x+.132f,.45f),heroFilter==index?Selected:Panel);filter.onClick.AddListener(()=>{heroFilter=index;BuildContent();});}
            RectTransform grid=CreateRect("Hero Grid",contentRoot,new Vector2(.015f,.07f),new Vector2(.585f,.775f),Vector2.zero,Vector2.zero);int visible=0;
            foreach(HeroDefinition hero in heroCatalog.Heroes)
            {
                if(hero==null||!MatchesFilter(hero))continue;int slot=visible++;int column=slot%3,row=slot/3;float minX=column/3f+.01f,maxX=(column+1)/3f-.01f,minY=.51f-row*.5f,maxY=.99f-row*.5f;
                RectTransform card=CreateRect("Hero "+hero.HeroId,grid,new Vector2(minX,minY),new Vector2(maxX,maxY),Vector2.zero,Vector2.zero);Image surface=CreateImage("Surface",card,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.025f,.052f,.09f,.98f));Button choose=card.gameObject.AddComponent<Button>();choose.onClick.AddListener(()=>{selectedHero=hero;BuildContent();});HeroCardHover hover=card.gameObject.AddComponent<HeroCardHover>();hover.Configure(hero==selectedHero?1.01f:1.025f);
                if(hero==selectedHero){Outline outline=card.gameObject.AddComponent<Outline>();outline.effectColor=new Color(Accent.r,Accent.g,Accent.b,.96f);outline.effectDistance=new Vector2(2f,-2f);}
                CreatePortrait("Portrait",card,new Vector2(.04f,.42f),new Vector2(.96f,.96f),hero.Portrait,hero.ThemeColor);CreateHeroCardInfo(card,hero,hero==selectedHero);
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
            if(hero==null)return;RectTransform detail=CreateRect("Hero Detail",contentRoot,new Vector2(.615f,.07f),new Vector2(.99f,.875f),Vector2.zero,Vector2.zero);Image surface=CreateImage("Surface",detail,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.008f,.019f,.042f,.985f));Outline panelOutline=detail.gameObject.AddComponent<Outline>();panelOutline.effectColor=new Color(.16f,.48f,.68f,.48f);panelOutline.effectDistance=new Vector2(1f,-1f);
            CreatePortrait("Portrait",detail,new Vector2(.04f,.55f),new Vector2(.96f,.97f),hero.Portrait,hero.ThemeColor);CreateImage("Portrait Caption Veil",detail,new Vector2(.04f,.55f),new Vector2(.96f,.70f),Vector2.zero,Vector2.zero,new Color(.004f,.012f,.028f,.82f));
            CreateText("Name",detail,hero.DisplayName.ToUpperInvariant(),30,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.07f,.645f),new Vector2(.93f,.695f));CreateText("Epithet",detail,LocalizedEpithet(hero),14,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.07f,.598f),new Vector2(.93f,.645f));
            CreatePill("Primary Role",detail,RoleLabel(hero.PrimaryRole),new Vector2(.07f,.557f),new Vector2(.29f,.59f),hero.ThemeColor);CreatePill("Secondary Role",detail,RoleLabel(hero.SecondaryRole),new Vector2(.30f,.557f),new Vector2(.52f,.59f),Panel);CreatePill("Attack Style",detail,AttackLabel(hero.AttackStyle),new Vector2(.53f,.557f),new Vector2(.70f,.59f),Panel);CreatePill("Difficulty",detail,DifficultyDots(hero.Difficulty),new Vector2(.71f,.557f),new Vector2(.93f,.59f),new Color(.16f,.10f,.22f,.96f));
            CreateText("Description Heading",detail,"ESTILO DE JUEGO",11,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.07f,.495f),new Vector2(.93f,.53f));CreateText("Description",detail,LocalizedDescription(hero),14,FontStyle.Normal,TextAnchor.UpperLeft,Color.white,new Vector2(.07f,.415f),new Vector2(.93f,.485f));
            CreateText("Stats Heading",detail,"ESTADÍSTICAS BASE",11,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.07f,.35f),new Vector2(.93f,.385f));CreateStatChip(detail,"VIDA",hero.BaseHealth.ToString("0"),0,0);CreateStatChip(detail,"MANÁ",hero.BaseMana.ToString("0"),1,0);CreateStatChip(detail,"DAÑO",hero.BaseDamage.ToString("0"),2,0);CreateStatChip(detail,"RANGO",hero.AttackRange.ToString("0.0"),0,1);CreateStatChip(detail,"MOV.",hero.MoveSpeed.ToString("0.0"),1,1);CreateStatChip(detail,"ATAQUE",(1f/hero.AttackInterval).ToString("0.00")+"/s",2,1);
            CreateText("Abilities Heading",detail,"HABILIDADES",11,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(.07f,.17f),new Vector2(.93f,.205f));string[] keys={"Q","W","E","R"};for(int i=0;i<4;i++)CreateAbilityTile(detail,hero,hero.GetAbility(i),keys[i],i);
            Button select=CreateButton("Select Hero",detail,"✓  ELEGIR PARA JUGAR",new Vector2(.54f,.01f),new Vector2(.93f,.06f),Selected);select.onClick.AddListener(()=>{selectedHero=hero;section=2;BuildContent();BuildTopNavigationRefresh();});
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
            float x=.07f+column*.287f,y=row==0?.285f:.22f;Image chip=CreateImage("Stat "+label,parent,new Vector2(x,y),new Vector2(x+.27f,y+.053f),Vector2.zero,Vector2.zero,new Color(.025f,.065f,.105f,.94f));chip.raycastTarget=false;CreateText("Label",chip.transform,label,9,FontStyle.Bold,TextAnchor.UpperLeft,Muted,new Vector2(.08f,.08f),new Vector2(.92f,.96f));CreateText("Value",chip.transform,value,13,FontStyle.Bold,TextAnchor.LowerRight,Color.white,new Vector2(.08f,.04f),new Vector2(.92f,.75f));
        }
        private void CreateAbilityTile(RectTransform detail,HeroDefinition hero,AbilityDefinition ability,string key,int slot)
        {
            float x=.07f+slot*.216f;Image tile=CreateImage("Ability "+key,detail,new Vector2(x,.09f),new Vector2(x+.202f,.15f),Vector2.zero,Vector2.zero,slot==3?new Color(.135f,.075f,.19f,.98f):new Color(.025f,.065f,.105f,.98f));Button button=tile.gameObject.AddComponent<Button>();ColorBlock states=button.colors;states.normalColor=Color.white;states.highlightedColor=new Color(.68f,.9f,1f,1f);states.pressedColor=new Color(.42f,.7f,.9f,1f);button.colors=states;
            Image icon=CreateImage("Icon",tile.transform,new Vector2(.035f,.18f),new Vector2(.25f,.82f),Vector2.zero,Vector2.zero,slot==3?new Color(.78f,.51f,.22f,1f):hero.ThemeColor);icon.raycastTarget=false;CreateText("Key",icon.transform,key,15,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white,Vector2.zero,Vector2.one);CreateText("Name",tile.transform,ability==null?"Sin habilidad":AbilityName(ability),10,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.3f,.48f),new Vector2(.98f,.92f));CreateText("Cost",tile.transform,ability==null?string.Empty: $"{ability.ManaCost(1):0} maná · {ability.Cooldown(1):0.#} s",8,FontStyle.Normal,TextAnchor.LowerLeft,Muted,new Vector2(.3f,.08f),new Vector2(.98f,.48f));
            RectTransform tooltip=CreateRect("Tooltip "+key,detail,new Vector2(x,.345f),new Vector2(Mathf.Min(.93f,x+.31f),.445f),Vector2.zero,Vector2.zero);Image tooltipSurface=CreateImage("Surface",tooltip,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.003f,.009f,.022f,.98f));Outline tooltipOutline=tooltip.gameObject.AddComponent<Outline>();tooltipOutline.effectColor=new Color(Accent.r,Accent.g,Accent.b,.76f);tooltipOutline.effectDistance=new Vector2(1f,-1f);CreateText("Title",tooltip,ability==null?"Sin habilidad":key+" · "+AbilityName(ability),12,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.06f,.63f),new Vector2(.94f,.95f));CreateText("Body",tooltip,ability==null?string.Empty:AbilityTooltipText(ability),10,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.06f,.08f),new Vector2(.94f,.64f));tooltip.gameObject.SetActive(false);HeroAbilityTooltip tooltipController=tile.gameObject.AddComponent<HeroAbilityTooltip>();tooltipController.Configure(tooltip.gameObject);
        }
        private static string RoleLabel(HeroRole role) => role switch { HeroRole.Vanguard=>"Vanguardia",HeroRole.Carry=>"Carry",HeroRole.Duelist=>"Duelista",HeroRole.Mage=>"Mago",HeroRole.Support=>"Apoyo",HeroRole.Controller=>"Controlador",_=>role.ToString() };
        private static string AttackLabel(HeroAttackStyle style) => style==HeroAttackStyle.Melee?"Melee":"Distancia";
        private static string DifficultyDots(int difficulty) => difficulty switch { 1=>"● ○ ○",2=>"● ● ○",_=>"● ● ●" };
        private static string LocalizedEpithet(HeroDefinition hero) => hero.HeroId switch { "stone_aegis"=>"Bastión del vendaval", "rift_duelist"=>"Filo en la frontera del vendaval", "skyline_marksman"=>"Flecha de la corriente abierta", "storm_warden"=>"Guardián de la alta corriente", "cairn_warden"=>"Refugio de los tocados por la tormenta", "tempest_arbiter"=>"Voz entre los nubarrones", _=>hero.Epithet };
        private static string LocalizedDescription(HeroDefinition hero) => hero.HeroId switch { "stone_aegis"=>"Iniciador resistente que protege terreno y dispersa a quienes intentan asaltar su posición.", "rift_duelist"=>"Hostigador de corta distancia que convierte el ritmo del duelo en presión constante.", "skyline_marksman"=>"Portador a distancia que castiga con precisión y domina las líneas abiertas.", "storm_warden"=>"Mago de combate que castiga formaciones agrupadas con energía tormentosa.", "cairn_warden"=>"Apoyo protector que intercambia daño por barreras y supervivencia para su equipo.", "tempest_arbiter"=>"Controlador de zonas que niega rutas y aísla objetivos con tormentas arcanas.", _=>hero.Description };
        private static string AbilityName(AbilityDefinition ability) => ability.AbilityId switch { "rampart_strike"=>"Golpe de Muralla", "windward_guard"=>"Guardia de Barlovento", "grounding_ring"=>"Anillo de Tierra", "citadel_crash"=>"Impacto de Ciudadela", "rift_lunge"=>"Estocada de Brecha", "duelist_wind"=>"Viento del Duelista", "counterveil"=>"Velo de Réplica", "redline"=>"Línea Roja", "piercing_gale"=>"Vendaval Perforante", "tailwind"=>"Viento de Cola", "updraft_step"=>"Paso Ascendente", "horizon_breaker"=>"Ruptura del Horizonte", "arc_bolt"=>"Rayo de Arco", "storm_mark"=>"Marca de Tormenta", "gale_step"=>"Paso de Vendaval", "tempest_fall"=>"Caída de Tempestad", "kindling_orb"=>"Orbe de Ascua", "cairn_barrier"=>"Barrera de Cairn", "restoring_draft"=>"Brisa Restauradora", "sanctuary_field"=>"Campo Santuario", "pressure_drop"=>"Caída de Presión", "static_lattice"=>"Retícula Estática", "crosswind"=>"Viento Cruzado", "eye_of_tempest"=>"Ojo de la Tempestad", _=>ability.DisplayName };
        private static string AbilityTooltipText(AbilityDefinition ability)
        {
            string effect=ability.Effect switch { AbilityEffect.ProjectileDamage=>"Lanza un proyectil que inflige daño.",AbilityEffect.AreaDamage=>"Daña a los enemigos en el área objetivo.",AbilityEffect.SelfMoveSpeed=>"Aumenta temporalmente la velocidad de movimiento.",AbilityEffect.StrongAreaDamage=>"Inflige gran daño en un área.",AbilityEffect.AreaSlow=>"Ralentiza a los enemigos dentro del área.",AbilityEffect.StrongAreaStun=>"Aturde a los enemigos dentro del área.",AbilityEffect.SelfShield=>"Otorga un escudo protector.",_=>"Habilidad de combate."};
            return $"{effect}\nAlcance {ability.Range:0.#} · Efecto {ability.EffectValue(1):0} · {ability.MaximumLevel} niveles";
        }
        private void BuildPlay(){selectedHero??=HeroCatalog.Shared.DefaultHero;CreateText("Play Heading",contentRoot,"JUGAR",42,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(0,.9f),Vector2.one);CreateText("Selected Hero",contentRoot,$"HÉROE SELECCIONADO · {selectedHero.DisplayName}\n{selectedHero.PrimaryRole} · {selectedHero.AttackStyle} · {selectedHero.Epithet}",20,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(0,.76f),new Vector2(.72f,.87f));Button heroSelect=CreateButton("Change Hero",contentRoot,"CAMBIAR HÉROE",new Vector2(.58f,.77f),new Vector2(.82f,.84f),Panel);heroSelect.onClick.AddListener(()=>{section=1;BuildContent();BuildTopNavigationRefresh();});CreateText("Team",contentRoot,"EQUIPO SOLICITADO",13,FontStyle.Bold,TextAnchor.UpperLeft,Muted,new Vector2(0,.58f),new Vector2(.25f,.63f));Button azure=CreateButton("Azure Team",contentRoot,"AZURE",new Vector2(0,.53f),new Vector2(.2f,.58f),team==TeamId.Azure?Selected:Panel);azure.onClick.AddListener(()=>{team=TeamId.Azure;BuildContent();});Button ember=CreateButton("Ember Team",contentRoot,"EMBER",new Vector2(.22f,.53f),new Vector2(.42f,.58f),team==TeamId.Ember?Selected:Panel);ember.onClick.AddListener(()=>{team=TeamId.Ember;BuildContent();});Button local=CreateButton("Local",contentRoot,"PARTIDA LOCAL DE DESARROLLO",new Vector2(0,.42f),new Vector2(.42f,.51f),Selected);local.onClick.AddListener(()=>Launch(FrontendMatchMode.LocalDevelopment));Button host=CreateButton("Host",contentRoot,"CREAR PARTIDA COMO HOST",new Vector2(0,.26f),new Vector2(.42f,.37f),Panel);host.onClick.AddListener(()=>Launch(FrontendMatchMode.Host));Button client=CreateButton("Client",contentRoot,"UNIRSE COMO CLIENT",new Vector2(0,.1f),new Vector2(.42f,.21f),Panel);client.onClick.AddListener(()=>Launch(FrontendMatchMode.Client));CreateText("Play Note",contentRoot,"La selección es una preferencia individual de desarrollo. El servidor valida solo el HeroId y crea el prefab registrado. Se permiten duplicados hasta que exista draft.",16,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.48f,.38f),new Vector2(.92f,.7f));}
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
        private Text CreateText(string name,Transform parent,string value,int size,FontStyle style,TextAnchor alignment,Color color,Vector2 min,Vector2 max){RectTransform rect=CreateRect(name,parent,min,max,Vector2.zero,Vector2.zero);Text text=rect.gameObject.AddComponent<Text>();text.font=uiFont;text.text=value;text.fontSize=size;text.fontStyle=style;text.alignment=alignment;text.color=color;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;return text;}
        private Button CreateButton(string name,Transform parent,string label,Vector2 min,Vector2 max,Color color){Image image=CreateImage(name,parent,min,max,Vector2.zero,Vector2.zero,color);Button button=image.gameObject.AddComponent<Button>();ColorBlock states=button.colors;states.normalColor=Color.white;states.highlightedColor=new Color(.62f,.88f,1f,1f);states.pressedColor=new Color(.45f,.7f,.86f,1f);button.colors=states;CreateText("Label",image.transform,label,16,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white,Vector2.zero,Vector2.one);return button;}
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

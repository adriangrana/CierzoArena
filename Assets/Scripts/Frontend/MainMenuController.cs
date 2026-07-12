using CierzoArena.Core;
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
        [SerializeField] private string arenaScene="MobaGreyboxArena";
        private int section;
        private TeamId team=TeamId.Azure;
        private string address="127.0.0.1";
        private string port="7777";
        private GUIStyle title,heading,body,button,selected,muted,card,eyebrow;
        private Texture2D keyVisual;
        private Canvas menuCanvas;
        private RectTransform menuRoot,contentRoot;
        private Font uiFont;
        private bool canvasPresentation;
        private readonly string[] navigation={"Inicio","Héroes","Jugar","Aprender","Ajustes"};

        public int ActiveSection=>section;
        public static int ResolveSingleActivePanel(int requested,int count)=>Mathf.Clamp(requested,0,Mathf.Max(0,count-1));
        private void Awake()
        {
            keyVisual=Resources.Load<Texture2D>("Frontend/MainMenuKeyVisual");
            uiFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        private void Launch(FrontendMatchMode mode){ushort parsed=7777;ushort.TryParse(port,out parsed);FrontendLaunchRequest.Set(mode,team,address,parsed);SceneManager.LoadScene(arenaScene);}
        // ----- Retained-mode presentation -----------------------------------
        // The MainMenu uses real Canvas controls. The former IMGUI routines remain
        // only as a development fallback should the Canvas be unavailable.
        private void BuildCanvasPresentation()
        {
            menuCanvas=FindFirstObjectByType<Canvas>();
            if(menuCanvas==null){GameObject canvasObject=new GameObject("Main Menu Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));menuCanvas=canvasObject.GetComponent<Canvas>();}
            menuCanvas.renderMode=RenderMode.ScreenSpaceOverlay;CanvasScaler scaler=menuCanvas.GetComponent<CanvasScaler>()??menuCanvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;if(menuCanvas.GetComponent<GraphicRaycaster>()==null)menuCanvas.gameObject.AddComponent<GraphicRaycaster>();
            menuRoot=CreateRect("MainMenu Layers",menuCanvas.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);RawImage keyArt=CreateRaw("Background Key Art",menuRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,keyVisual);AspectRatioFitter keyFit=keyArt.gameObject.AddComponent<AspectRatioFitter>();keyFit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;keyFit.aspectRatio=keyVisual!=null?(float)keyVisual.width/keyVisual.height:16f/9f;keyArt.rectTransform.pivot=new Vector2(1f,.5f);
            CreateImage("Atmospheric Overlay",menuRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.01f,.03f,.07f,.18f));
            CreateImage("Left Readability Veil",menuRoot,new Vector2(0,0),new Vector2(.5f,1),Vector2.zero,Vector2.zero,new Color(.005f,.012f,.03f,.78f));
            for(int i=0;i<6;i++)CreateImage("Fog Transition",menuRoot,new Vector2(.38f+i*.025f,0),new Vector2(.45f+i*.025f,1),Vector2.zero,Vector2.zero,new Color(.05f,.17f,.24f,.07f));
            BuildTopNavigation();contentRoot=CreateRect("Main Content",menuRoot,new Vector2(.045f,.11f),new Vector2(.955f,.88f),Vector2.zero,Vector2.zero);BuildContent();canvasPresentation=true;
        }
        private void BuildTopNavigation()
        {
            RectTransform top=CreateRect("Top Navigation",menuRoot,new Vector2(0,.905f),Vector2.one,Vector2.zero,Vector2.zero);CreateImage("Navigation Surface",top,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.01f,.025f,.055f,.94f));
            CreateText("Brand",top,"CIERZO\nARENA",30,FontStyle.Bold,TextAnchor.MiddleLeft,Color.white,new Vector2(.025f,.08f),new Vector2(.19f,.92f));CreateText("Brand Tag",top,"ARENA OF THE NORTH WIND",10,FontStyle.Bold,TextAnchor.LowerLeft,Accent,new Vector2(.028f,.04f),new Vector2(.19f,.34f));
            for(int i=0;i<navigation.Length;i++){int index=i;Button tab=CreateButton("Tab "+navigation[i],top,navigation[i],new Vector2(.22f+i*.095f,.22f),new Vector2(.31f+i*.095f,.78f),i==section?Selected:Panel);tab.onClick.AddListener(()=>{section=index;BuildContent();BuildTopNavigationRefresh();});}
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
            RectTransform left=CreateRect("Main Hero Copy",contentRoot,new Vector2(0,.18f),new Vector2(.43f,.89f),Vector2.zero,Vector2.zero);CreateText("Season",left,"TEMPORADA PROTOTIPO · EL ASCENSO DEL CIERZO",13,FontStyle.Bold,TextAnchor.UpperLeft,Accent,new Vector2(0,.91f),Vector2.one);CreateText("Hero Title",left,"EL VIENTO\nDECIDE LA ARENA",57,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(0,.56f),new Vector2(1,.9f));CreateText("Hero Description",left,"Un enfrentamiento de estrategia, energía elemental y control del terreno. Reúne tu escuadra y entra a CierzoArena.",18,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.005f,.37f),new Vector2(.9f,.55f));
            Button play=CreateButton("Play Now",left,"✦  JUGAR AHORA",new Vector2(0,.2f),new Vector2(.51f,.32f),Selected);play.onClick.AddListener(()=>{section=2;BuildContent();BuildTopNavigationRefresh();});Button heroesButton=CreateButton("View Heroes",left,"VER HÉROES",new Vector2(.54f,.2f),new Vector2(.93f,.32f),Panel);heroesButton.onClick.AddListener(()=>{section=1;BuildContent();BuildTopNavigationRefresh();});
            RectTransform cards=CreateRect("Information Cards",contentRoot,new Vector2(0,0),new Vector2(.44f,.22f),Vector2.zero,Vector2.zero);CreateInfoCard(cards,"Noticias","NUEVO PARCHE 1.1.0","Balance de héroes, mejoras y correcciones.",0);CreateInfoCard(cards,"Grupo","2 / 5 EN LÍNEA","Grupo provisional · social próximamente.",1);CreateInfoCard(cards,"Versión","M19 FRONTEND","Vertical slice visual disponible.",2);
            RectTransform featured=CreateRect("Featured Event",contentRoot,new Vector2(.64f,.14f),new Vector2(.99f,.32f),Vector2.zero,Vector2.zero);CreateImage("Event Veil",featured,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.005f,.02f,.045f,.66f));CreateText("Event Title",featured,"GUARDIÁN DEL CIERZO",25,FontStyle.Bold,TextAnchor.UpperCenter,Color.white,new Vector2(0,.62f),Vector2.one);CreateText("Event Description",featured,"DOMINA LA FOSA. RECLAMA EL ASCENDENTE.",14,FontStyle.Bold,TextAnchor.UpperCenter,Muted,new Vector2(0,.4f),new Vector2(1,.65f));CreateText("Event State",featured,"◆  EVENTO ACTIVO     ·     VER DESAFÍO  ›",13,FontStyle.Bold,TextAnchor.UpperCenter,Accent,new Vector2(0,.15f),new Vector2(1,.4f));
            CreateSocialBar();CreateDailyChallenges();
        }
        private void CreateInfoCard(RectTransform parent,string titleText,string headline,string detail,int index)
        {
            float min=index/3f+.008f,max=(index+1)/3f-.012f;RectTransform card=CreateRect(titleText+" Card",parent,new Vector2(min,0),new Vector2(max,1),Vector2.zero,Vector2.zero);Image surface=CreateImage("Surface",card,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Panel);Button interaction=card.gameObject.AddComponent<Button>();ColorBlock colors=interaction.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.55f,.82f,1f,1f);interaction.colors=colors;CreateRaw("Thumbnail",card,new Vector2(.08f,.48f),new Vector2(.92f,.84f),Vector2.zero,Vector2.zero,keyVisual);CreateText("Card Title",card,titleText.ToUpperInvariant(),14,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.08f,.83f),new Vector2(.92f,.98f));CreateText("Headline",card,headline,13,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.08f,.27f),new Vector2(.92f,.46f));CreateText("Detail",card,detail,11,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.08f,.1f),new Vector2(.92f,.28f));CreateText("Action",card,"VER DETALLES  ›",11,FontStyle.Bold,TextAnchor.LowerLeft,Accent,new Vector2(.08f,.02f),new Vector2(.92f,.13f));
        }
        private void CreateSocialBar(){RectTransform bar=CreateRect("Social Bar",contentRoot,new Vector2(0,-.02f),new Vector2(.22f,.075f),Vector2.zero,Vector2.zero);CreateImage("Social Surface",bar,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Panel);CreateText("Social",bar,"◉  2     ◆     ◆     +",14,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white,Vector2.zero,Vector2.one);}
        private void CreateDailyChallenges(){RectTransform panel=CreateRect("Daily Challenges",contentRoot,new Vector2(.76f,-.02f),new Vector2(1,.09f),Vector2.zero,Vector2.zero);CreateImage("Challenge Surface",panel,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.01f,.03f,.055f,.84f));CreateText("Challenge",panel,"DESAFÍOS DIARIOS\n3 / 5 COMPLETADOS",13,FontStyle.Bold,TextAnchor.MiddleLeft,Color.white,new Vector2(.08f,.15f),new Vector2(.92f,.9f));CreateImage("Progress",panel,new Vector2(.08f,.12f),new Vector2(.68f,.16f),Vector2.zero,Vector2.zero,Accent);}
        private void BuildHeroes(){CreateText("Heroes Heading",contentRoot,"HÉROES PROTOTIPO",42,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(0,.9f),Vector2.one);for(int i=0;i<(heroes?.Length??0);i++){HeroPresentationDefinition hero=heroes[i];if(hero==null)continue;RectTransform card=CreateRect("Hero Card",contentRoot,new Vector2(i*.27f,.28f),new Vector2(i*.27f+.24f,.78f),Vector2.zero,Vector2.zero);CreateImage("Surface",card,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Panel);CreateText("Name",card,hero.HeroName,23,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(.08f,.78f),new Vector2(.92f,.95f));CreateText("Details",card,$"{hero.Role} · {hero.CombatStyle}\nDificultad {hero.Difficulty}/3\n\n{hero.Description}\n\n{hero.Abilities}",14,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.08f,.1f),new Vector2(.92f,.72f));}}
        private void BuildPlay(){CreateText("Play Heading",contentRoot,"JUGAR",42,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(0,.9f),Vector2.one);Button local=CreateButton("Local",contentRoot,"PARTIDA LOCAL DE DESARROLLO",new Vector2(0,.64f),new Vector2(.42f,.75f),Selected);local.onClick.AddListener(()=>Launch(FrontendMatchMode.LocalDevelopment));Button host=CreateButton("Host",contentRoot,"CREAR PARTIDA COMO HOST",new Vector2(0,.48f),new Vector2(.42f,.59f),Panel);host.onClick.AddListener(()=>Launch(FrontendMatchMode.Host));Button client=CreateButton("Client",contentRoot,"UNIRSE COMO CLIENT",new Vector2(0,.32f),new Vector2(.42f,.43f),Panel);client.onClick.AddListener(()=>Launch(FrontendMatchMode.Client));CreateText("Play Note",contentRoot,"IP y puerto configurables en la versión de desarrollo.\nPartidas personalizadas, búsqueda y entrenamiento: Próximamente.",16,FontStyle.Normal,TextAnchor.UpperLeft,Muted,new Vector2(.48f,.46f),new Vector2(.92f,.7f));}
        private void BuildPlaceholder(string name){CreateText("Placeholder",contentRoot,name.ToUpperInvariant()+"\n\nPRÓXIMAMENTE",42,FontStyle.Bold,TextAnchor.UpperLeft,Color.white,new Vector2(0,.55f),new Vector2(.7f,.9f));}
        private Color Accent=>theme!=null?theme.azure:new Color(.18f,.72f,1f);private Color Muted=>theme!=null?theme.muted:new Color(.64f,.72f,.8f);private Color Panel=>theme!=null?theme.panel:new Color(.075f,.105f,.16f,.94f);private Color Selected=>theme!=null?theme.selected:new Color(.1f,.42f,.62f);
        private RectTransform CreateRect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax){GameObject item=new GameObject(name,typeof(RectTransform));RectTransform rect=item.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;return rect;}
        private Image CreateImage(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax,Color color){RectTransform rect=CreateRect(name,parent,min,max,offsetMin,offsetMax);Image image=rect.gameObject.AddComponent<Image>();image.color=color;return image;}
        private RawImage CreateRaw(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax,Texture texture){RectTransform rect=CreateRect(name,parent,min,max,offsetMin,offsetMax);RawImage image=rect.gameObject.AddComponent<RawImage>();image.texture=texture;image.color=Color.white;return image;}
        private Text CreateText(string name,Transform parent,string value,int size,FontStyle style,TextAnchor alignment,Color color,Vector2 min,Vector2 max){RectTransform rect=CreateRect(name,parent,min,max,Vector2.zero,Vector2.zero);Text text=rect.gameObject.AddComponent<Text>();text.font=uiFont;text.text=value;text.fontSize=size;text.fontStyle=style;text.alignment=alignment;text.color=color;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;return text;}
        private Button CreateButton(string name,Transform parent,string label,Vector2 min,Vector2 max,Color color){Image image=CreateImage(name,parent,min,max,Vector2.zero,Vector2.zero,color);Button button=image.gameObject.AddComponent<Button>();ColorBlock states=button.colors;states.normalColor=Color.white;states.highlightedColor=new Color(.62f,.88f,1f,1f);states.pressedColor=new Color(.45f,.7f,.86f,1f);button.colors=states;CreateText("Label",image.transform,label,16,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white,Vector2.zero,Vector2.one);return button;}
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

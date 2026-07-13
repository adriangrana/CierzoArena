using System;
using System.Collections.Generic;
using CierzoArena.CameraSystem;
using CierzoArena.Core;
using CierzoArena.Structures;
using CierzoArena.Frontend;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Lightweight IMGUI scoreboard, combat feed and provisional final screen.</summary>
    [RequireComponent(typeof(MatchStatisticsController))]
    [RequireComponent(typeof(MatchStateController))]
    public sealed class MatchScoreboardController : MonoBehaviour
    {
        public const int RowsPerTeam = 5;
        private struct FeedEntry { public string Message; public float Until; }
        private readonly List<MatchStatisticsSnapshot> rows = new();
        private readonly List<MatchStatisticsSnapshot> azureRows = new(5);
        private readonly List<MatchStatisticsSnapshot> emberRows = new(5);
        private readonly List<FeedEntry> feed = new();
        private MatchStatisticsController statistics;
        private MatchStateController match;
        private GUIStyle title, rowLeft, rowCenter, rowRight, headerLeft, headerCenter, headerRight, feedStyle;
        private bool focused = true;
        private float styleScale;
        private bool rowsDirty = true;

        private void Awake()
        {
            statistics=GetComponent<MatchStatisticsController>();match=GetComponent<MatchStateController>();
            statistics.AnnouncementRaised+=AddAnnouncement;statistics.StatisticsChanged+=MarkRowsDirty;
        }
        private void OnDestroy(){if(statistics!=null){statistics.AnnouncementRaised-=AddAnnouncement;statistics.StatisticsChanged-=MarkRowsDirty;}}
        private void OnApplicationFocus(bool hasFocus){focused=hasFocus;}
        private void OnGUI()
        {
            if(statistics==null||match==null)return;
            RefreshRowsIfNeeded();EnsureStyles();
            // M22 owns the compact live scoreboard and notification layer. This
            // controller remains responsible for the expanded Tab/final table.
            bool final=!match.IsPlaying;
            bool tabHeld=focused&&ScoreboardInputController.Active!=null&&ScoreboardInputController.Active.IsHeld;
            if(ShouldShowScoreboard(final,tabHeld))DrawScoreboard(final);
        }
        private void MarkRowsDirty()=>rowsDirty=true;
        private void RefreshRowsIfNeeded()
        {
            if(!rowsDirty)return;
            statistics.CopySnapshotsTo(rows);
            rowsDirty=false;
        }
        // The match-end layer owns victory/defeat. The expanded player board is
        // strictly hold-Tab and therefore always closes when focus is lost or the
        // key is released, including after the match has ended.
        public static bool ShouldShowScoreboard(bool matchFinished, bool tabHeld) => tabHeld;
        private void DrawHud()
        {
            MatchStatisticsSnapshot own=FindLocalRow();
            float scale=UiScale();float width=Mathf.Min(Screen.width-40f,650f*scale);float height=54f*scale;
            GUI.Box(new Rect((Screen.width-width)*.5f,18f*scale,width,height),$"{FormatTime(statistics.DurationSeconds)}    K/D/A {own.Kills}/{own.Deaths}/{own.Assists}    LH {own.LastHits}    GOLD {own.CurrentGold}",rowLeft);
        }
        private void DrawFeed()
        {
            float scale=UiScale();float y=82f*scale;float now=Time.unscaledTime;
            for(int i=feed.Count-1;i>=0;i--)if(feed[i].Until<=now)feed.RemoveAt(i);
            float width=Mathf.Min(Screen.width-40f,680f*scale);
            for(int i=0;i<feed.Count;i++){GUI.Label(new Rect((Screen.width-width)*.5f,y,width,34f*scale),feed[i].Message,feedStyle);y+=36f*scale;}
        }
        private void DrawScoreboard(bool final)
        {
            float scale=UiScale();float width=Mathf.Min(1240f*scale,Screen.width-48f),x=(Screen.width-width)*.5f;
            float height=Mathf.Min(Screen.height-58f,650f*scale),y=54f*scale;
            Rect panel=new Rect(x,y,width,height);DrawPanel(panel,new Color(.015f,.045f,.08f,.91f),new Color(.28f,.68f,.9f,.65f));
            SplitRows();int azureScore=TeamKills(azureRows),emberScore=TeamKills(emberRows);
            GUI.Label(new Rect(x+18f*scale,y+12f*scale,width*.3f,38f*scale),$"◇ AZURE  {azureScore}",headerLeft);
            GUI.Label(new Rect(x+width*.35f,y+12f*scale,width*.3f,38f*scale),$"{FormatTime(statistics.DurationSeconds)}  ·  MARCADOR",title);
            GUI.Label(new Rect(x+width*.7f,y+12f*scale,width*.27f,38f*scale),$"{emberScore}  EMBER ◆",headerRight);
            float sectionHeight=(height-82f*scale)*.5f;DrawTeamSection(new Rect(x+16f*scale,y+66f*scale,width-32f*scale,sectionHeight-6f*scale),TeamId.Azure,azureRows,RowsPerTeam,scale);DrawTeamSection(new Rect(x+16f*scale,y+66f*scale+sectionHeight,width-32f*scale,sectionHeight-12f*scale),TeamId.Ember,emberRows,RowsPerTeam,scale);
            if(final)GUI.Label(new Rect(x+18f*scale,y+height-28f*scale,width-36f*scale,20f*scale),"Estadísticas finales congeladas",headerLeft);
        }
        private void SplitRows(){azureRows.Clear();emberRows.Clear();for(int i=0;i<rows.Count;i++){if(rows[i].Team==TeamId.Azure)azureRows.Add(rows[i]);else if(rows[i].Team==TeamId.Ember)emberRows.Add(rows[i]);}}
        private static int TeamKills(List<MatchStatisticsSnapshot> values){int total=0;for(int i=0;i<values.Count;i++)total+=values[i].Kills;return total;}
        private void DrawTeamSection(Rect rect,TeamId team,List<MatchStatisticsSnapshot> values,int capacity,float scale)
        {
            Color accent=team==TeamId.Azure?new Color(.22f,.78f,1f):new Color(1f,.35f,.25f);DrawPanel(rect,new Color(.025f,.065f,.105f,.78f),accent);
            GUI.Label(new Rect(rect.x+10f*scale,rect.y+6f*scale,rect.width,22f*scale),$"{(team==TeamId.Azure?"◇":"◆")} EQUIPO {team.ToString().ToUpperInvariant()}  ·  {values.Count}/{capacity}",headerLeft);
            Rect headerRect=new Rect(rect.x+12f*scale,rect.y+29f*scale,rect.width-24f*scale,18f*scale);
            DrawColumnHeader(headerRect);
            float rowHeight=31f*scale;for(int i=0;i<capacity;i++){Rect rowRect=new Rect(rect.x+9f*scale,rect.y+49f*scale+i*rowHeight,rect.width-18f*scale,rowHeight-2f*scale);if(i<values.Count)DrawPlayerRow(rowRect,values[i],team,i,accent,scale);else DrawEmptyRow(rowRect,team,i,scale);}
        }

        private void DrawColumnHeader(Rect rect)
        {
            for (int i = 0; i < ScoreboardColumnLayout.ColumnCount; i++)
            {
                ScoreboardColumn column=(ScoreboardColumn)i;
                GUI.Label(ScoreboardColumnLayout.GetCell(rect,column),ScoreboardColumnLayout.GetHeader(column),HeaderStyle(column));
            }
        }

        private void DrawPlayerRow(Rect rect,MatchStatisticsSnapshot value,TeamId team,int rowIndex,Color accent,float scale)
        {
            DrawPanel(rect,new Color(.045f,.09f,.14f,.88f),new Color(accent.r,accent.g,accent.b,.24f));
            HeroDefinition hero=FindHeroDefinition(value.DisplayName);
            Rect playerCell=ScoreboardColumnLayout.GetCell(rect,ScoreboardColumn.PlayerHero);
            Color old=GUI.color;GUI.color=hero!=null?Color.white:new Color(.4f,.45f,.5f);
            Rect iconRect=new Rect(playerCell.x+5f*scale,rect.y+3f*scale,25f*scale,25f*scale);
            GUI.DrawTexture(iconRect,hero!=null&&hero.SmallIcon!=null?hero.SmallIcon:Texture2D.whiteTexture,ScaleMode.ScaleAndCrop);GUI.color=old;
            string player=$"{team} {StableSlot(value,rowIndex)+1}";
            GUI.Label(new Rect(playerCell.x+35f*scale,playerCell.y,Mathf.Max(0f,playerCell.width-38f*scale),playerCell.height),$"{player}  ·  {value.DisplayName}",rowLeft);
            DrawCell(rect,ScoreboardColumn.Level,value.Level.ToString());
            DrawCell(rect,ScoreboardColumn.Kills,value.Kills.ToString());
            DrawCell(rect,ScoreboardColumn.Deaths,value.Deaths.ToString());
            DrawCell(rect,ScoreboardColumn.Assists,value.Assists.ToString());
            DrawCell(rect,ScoreboardColumn.LastHits,value.LastHits.ToString());
            DrawCell(rect,ScoreboardColumn.Gold,GoldText(value));
            DrawCell(rect,ScoreboardColumn.Status,PublicState(value));
        }

        private void DrawEmptyRow(Rect rect,TeamId team,int slot,float scale)
        {
            DrawPanel(rect,new Color(.025f,.055f,.085f,.5f),new Color(.25f,.35f,.45f,.18f));
            Rect playerCell=ScoreboardColumnLayout.GetCell(rect,ScoreboardColumn.PlayerHero);
            GUI.Label(new Rect(playerCell.x+5f*scale,playerCell.y,Mathf.Max(0f,playerCell.width-8f*scale),playerCell.height),$"{team} {slot+1}  ·  Esperando jugador",rowLeft);
            for(int i=1;i<ScoreboardColumnLayout.ColumnCount;i++)DrawCell(rect,(ScoreboardColumn)i,"—");
        }

        private void DrawCell(Rect rowRect,ScoreboardColumn column,string value)=>GUI.Label(ScoreboardColumnLayout.GetCell(rowRect,column),value,RowStyle(column));
        private GUIStyle RowStyle(ScoreboardColumn column)=>ScoreboardColumnLayout.GetAlignment(column) switch {TextAnchor.MiddleCenter=>rowCenter,TextAnchor.MiddleRight=>rowRight,_=>rowLeft};
        private GUIStyle HeaderStyle(ScoreboardColumn column)=>ScoreboardColumnLayout.GetAlignment(column) switch {TextAnchor.MiddleCenter=>headerCenter,TextAnchor.MiddleRight=>headerRight,_=>headerLeft};
        private static bool ShouldRevealGold(bool hasLocalTeam,TeamId localTeam,TeamId rowTeam)=>hasLocalTeam&&localTeam==rowTeam;
        public static bool ShouldRevealGold(TeamId localTeam,TeamId rowTeam)=>ShouldRevealGold(true,localTeam,rowTeam);
        private string GoldText(MatchStatisticsSnapshot value)
        {
            if(!TryResolveLocalTeam(out TeamId localTeam)||!ShouldRevealGold(localTeam,value.Team))return "—";
            if(statistics.IsAuthoritative)return value.CurrentGold.ToString();
            return statistics.TryGetReplicatedVisibleGold(value.HeroId,out int gold)?gold.ToString():"—";
        }
        private static bool TryResolveLocalTeam(out TeamId team)
        {
            team=TeamId.Neutral;
            Transform local=LocalHeroProvider.Active!=null?LocalHeroProvider.Active.CurrentHero:null;
            if(local==null||!local.TryGetComponent(out TeamMember member))return false;
            team=member.Team;
            return team==TeamId.Azure||team==TeamId.Ember;
        }
        public static int StableSlotForHeroId(int heroId)=>Mathf.Max(0,heroId%1000);
        private static int StableSlot(MatchStatisticsSnapshot value,int fallback)=>StableSlotForHeroId(value.HeroId);
        private static string PublicState(MatchStatisticsSnapshot value)=>value.LifeState==HeroLifeState.Alive?"● Vivo":$"✕ {value.RespawnSeconds}s";
        private static HeroDefinition FindHeroDefinition(string displayName){IReadOnlyList<HeroDefinition> heroes=HeroCatalog.Shared.Heroes;for(int i=0;i<heroes.Count;i++)if(heroes[i]!=null&&heroes[i].DisplayName==displayName)return heroes[i];return null;}
        private static void DrawPanel(Rect rect,Color fill,Color border){Color old=GUI.color;GUI.color=fill;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=border;GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,1f),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.x,rect.yMax-1f,rect.width,1f),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.x,rect.y,1f,rect.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.xMax-1f,rect.y,1f,rect.height),Texture2D.whiteTexture);GUI.color=old;}
        private MatchStatisticsSnapshot FindLocalRow()
        {
            Transform local=LocalHeroProvider.Active!=null?LocalHeroProvider.Active.CurrentHero:null;
            if(local!=null&&local.TryGetComponent(out HeroMatchStatistics own))for(int i=0;i<rows.Count;i++)if(rows[i].HeroId==own.HeroId)return rows[i];
            return rows.Count>0?rows[0]:default;
        }
        private string FinalHeading()
        {
            TeamId winner=match.CurrentState==MatchState.AzureVictory?TeamId.Azure:TeamId.Ember;
            MatchStatisticsSnapshot own=FindLocalRow();
            return own.Team==winner?$"VICTORIA — {winner} ({FormatTime(statistics.DurationSeconds)})":$"DERROTA — gana {winner} ({FormatTime(statistics.DurationSeconds)})";
        }
        private void AddAnnouncement(string message)
        {
            if(string.IsNullOrWhiteSpace(message))return;
            feed.Add(new FeedEntry{Message=message,Until=Time.unscaledTime+5f});
            if(feed.Count>5)feed.RemoveAt(0);
        }
        private static string FormatTime(int seconds)=>$"{seconds/60:00}:{seconds%60:00}";
        private void EnsureStyles()
        {
            float scale=UiScale();if(title!=null&&Mathf.Approximately(styleScale,scale))return;styleScale=scale;
            title=new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(28*scale),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
            rowLeft=new GUIStyle(GUI.skin.box){fontSize=Mathf.RoundToInt(18*scale),alignment=TextAnchor.MiddleLeft,wordWrap=false,clipping=TextClipping.Clip,normal={textColor=Color.white}};
            rowCenter=new GUIStyle(rowLeft){alignment=TextAnchor.MiddleCenter};
            rowRight=new GUIStyle(rowLeft){alignment=TextAnchor.MiddleRight};
            headerLeft=new GUIStyle(rowLeft){fontSize=Mathf.RoundToInt(19*scale),fontStyle=FontStyle.Bold,normal={textColor=new Color(.95f,.86f,.3f)}};
            headerCenter=new GUIStyle(headerLeft){alignment=TextAnchor.MiddleCenter};
            headerRight=new GUIStyle(headerLeft){alignment=TextAnchor.MiddleRight};
            feedStyle=new GUIStyle(rowCenter){fontSize=Mathf.RoundToInt(20*scale),normal={textColor=Color.white}};
        }
        private static float UiScale()=>Mathf.Clamp(Screen.height/1080f,1f,2.25f);
    }

    /// <summary>
    /// The shared geometry contract for scoreboard headers, populated rows and
    /// empty slots. It deliberately lives with the scoreboard controller so the
    /// runtime assembly always compiles it in the same Unity import pass.
    /// </summary>
    public enum ScoreboardColumn
    {
        PlayerHero,
        Level,
        Kills,
        Deaths,
        Assists,
        LastHits,
        Gold,
        Status
    }

    public static class ScoreboardColumnLayout
    {
        public const int ColumnCount = 8;

        // Player, level, K/D/A, last hits, gold, status. These fractions add up
        // to one and reserve readable space for every column at low resolutions.
        private static readonly float[] WidthFractions = { .46f, .065f, .045f, .045f, .045f, .065f, .105f, .17f };
        private static readonly string[] Headers = { "JUGADOR / HÉROE", "NIV.", "K", "D", "A", "LH", "ORO", "ESTADO" };

        public static float GetWidthFraction(ScoreboardColumn column) => WidthFractions[(int)column];
        public static string GetHeader(ScoreboardColumn column) => Headers[(int)column];

        public static Rect GetCell(Rect row, ScoreboardColumn column)
        {
            float x = row.x;
            int target = (int)column;
            for (int i = 0; i < target; i++) x += row.width * WidthFractions[i];
            return new Rect(x, row.y, row.width * WidthFractions[target], row.height);
        }

        public static TextAnchor GetAlignment(ScoreboardColumn column)
        {
            return column switch
            {
                ScoreboardColumn.PlayerHero => TextAnchor.MiddleLeft,
                ScoreboardColumn.Gold => TextAnchor.MiddleRight,
                ScoreboardColumn.Status => TextAnchor.MiddleLeft,
                _ => TextAnchor.MiddleCenter
            };
        }
    }
}

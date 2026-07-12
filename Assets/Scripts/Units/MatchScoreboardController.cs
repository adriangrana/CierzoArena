using System;
using System.Collections.Generic;
using CierzoArena.CameraSystem;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Lightweight IMGUI scoreboard, combat feed and provisional final screen.</summary>
    [RequireComponent(typeof(MatchStatisticsController))]
    [RequireComponent(typeof(MatchStateController))]
    public sealed class MatchScoreboardController : MonoBehaviour
    {
        private struct FeedEntry { public string Message; public float Until; }
        private readonly List<MatchStatisticsSnapshot> rows = new();
        private readonly List<FeedEntry> feed = new();
        private MatchStatisticsController statistics;
        private MatchStateController match;
        private GUIStyle title, row, header, feedStyle;
        private bool focused = true;
        private float styleScale;

        private void Awake()
        {
            statistics=GetComponent<MatchStatisticsController>();match=GetComponent<MatchStateController>();
            statistics.AnnouncementRaised+=AddAnnouncement;
        }
        private void OnDestroy(){if(statistics!=null)statistics.AnnouncementRaised-=AddAnnouncement;}
        private void OnApplicationFocus(bool hasFocus){focused=hasFocus;}
        private void OnGUI()
        {
            if(statistics==null||match==null)return;
            statistics.CopySnapshotsTo(rows);EnsureStyles();
            DrawHud();DrawFeed();
            bool final=!match.IsPlaying;
            if(ShouldShowScoreboard(final,focused&&Input.GetKey(KeyCode.Tab)))DrawScoreboard(final);
        }
        public static bool ShouldShowScoreboard(bool matchFinished, bool tabHeld) => matchFinished || tabHeld;
        private void DrawHud()
        {
            MatchStatisticsSnapshot own=FindLocalRow();
            float scale=UiScale();float width=Mathf.Min(Screen.width-40f,650f*scale);float height=54f*scale;
            GUI.Box(new Rect((Screen.width-width)*.5f,18f*scale,width,height),$"{FormatTime(statistics.DurationSeconds)}    K/D/A {own.Kills}/{own.Deaths}/{own.Assists}    LH {own.LastHits}    GOLD {own.CurrentGold}",row);
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
            float scale=UiScale();float width=Mathf.Min(1180f*scale,Screen.width-60f),x=(Screen.width-width)*.5f;
            float height=final?Mathf.Min(Screen.height-70f,620f*scale):Mathf.Min(Screen.height-100f,500f*scale),y=(Screen.height-height)*.5f;
            GUI.Box(new Rect(x,y,width,height),GUIContent.none);
            string heading=final?FinalHeading():"MATCH SCOREBOARD (hold Tab)";
            GUI.Label(new Rect(x+12f*scale,y+12f*scale,width-24f*scale,42f*scale),heading,title);
            GUI.Label(new Rect(x+18f*scale,y+62f*scale,width-36f*scale,26f*scale),"HERO                 LV   K   D   A   LH   GOLD   EARNED   XP     HERO DMG   TAKEN   STRUCT   OBJ   STATE",header);
            float rowY=y+92f*scale;TeamId section=TeamId.Neutral;
            for(int i=0;i<rows.Count;i++)
            {
                MatchStatisticsSnapshot value=rows[i];
                if(value.Team!=section){section=value.Team;GUI.Label(new Rect(x+18f*scale,rowY,width-36f*scale,24f*scale),section.ToString().ToUpperInvariant(),header);rowY+=26f*scale;}
                string state=value.LifeState==HeroLifeState.Alive?"Alive":$"{value.LifeState} {value.RespawnSeconds}s";
                string objective=$"N{value.NeutralLastHits} B{value.BossParticipations}/{value.MajorObjectiveSecures}";
                GUI.Label(new Rect(x+18f*scale,rowY,width-36f*scale,26f*scale),$"{value.DisplayName,-20} {value.Level,2}  {value.Kills,2}  {value.Deaths,2}  {value.Assists,2}  {value.LastHits,3}  {value.CurrentGold,5}  {value.GoldEarned,6}  {value.ExperienceEarned,5}  {value.HeroDamageDealt,8}  {value.HeroDamageReceived,6}  {value.StructureDamage,6}  {objective,-8} {state}",row);
                rowY+=29f*scale;
            }
            if(final)GUI.Label(new Rect(x+18f*scale,y+height-42f*scale,width-36f*scale,28f*scale),"Final statistics are frozen. Press Tab is no longer required.",header);
        }
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
            row=new GUIStyle(GUI.skin.box){fontSize=Mathf.RoundToInt(18*scale),alignment=TextAnchor.MiddleLeft,normal={textColor=Color.white}};
            header=new GUIStyle(row){fontSize=Mathf.RoundToInt(19*scale),fontStyle=FontStyle.Bold,normal={textColor=new Color(.95f,.86f,.3f)}};
            feedStyle=new GUIStyle(row){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(20*scale),normal={textColor=Color.white}};
        }
        private static float UiScale()=>Mathf.Clamp(Screen.height/1080f,1f,2.25f);
    }
}

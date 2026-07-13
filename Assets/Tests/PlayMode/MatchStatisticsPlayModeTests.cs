using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    public sealed class MatchStatisticsPlayModeTests
    {
        private readonly List<GameObject> created=new();
        [UnityTest]
        public IEnumerator KillSurvivesRespawnAndScoreboardVisibilityRuleClosesWhenTabIsReleased()
        {
            GameObject matchObject=Track(new GameObject("Match"));matchObject.AddComponent<MatchStateController>();MatchStatisticsController stats=matchObject.AddComponent<MatchStatisticsController>();matchObject.AddComponent<MatchScoreboardController>();
            HeroMatchStatistics azure=CreateHero("Azure",TeamId.Azure,0),ember=CreateHero("Ember",TeamId.Ember,0);
            HeroLifeCycle life=ember.GetComponent<HeroLifeCycle>();Set(life,"respawnDelay",.01f);
            yield return null;
            ember.Health.ApplyDamage(new DamageContext(azure.GetComponent<TeamMember>(),999f,AttackDelivery.Melee));
            yield return null;
            Assert.That(Snapshot(stats,azure.HeroId).Kills,Is.EqualTo(1));Assert.That(Snapshot(stats,ember.HeroId).Deaths,Is.EqualTo(1));
            life.Simulate(.02f);life.Simulate(.02f);
            Assert.That(Snapshot(stats,ember.HeroId).Deaths,Is.EqualTo(1));
            Assert.That(MatchScoreboardController.ShouldShowScoreboard(false,false),Is.False);Assert.That(MatchScoreboardController.ShouldShowScoreboard(false,true),Is.True);Assert.That(MatchScoreboardController.ShouldShowScoreboard(true,false),Is.False);
            Cleanup();
        }
        [TearDown] public void TearDown()=>Cleanup();
        private HeroMatchStatistics CreateHero(string name,TeamId team,int slot)
        {
            GameObject go=Track(new GameObject(name));SetTeam(go,team);go.AddComponent<HeroUnit>();go.AddComponent<Health>();go.AddComponent<ClickMover>();go.AddComponent<BasicAttack>();go.AddComponent<HeroLifeCycle>();HeroMatchIdentity identity=go.AddComponent<HeroMatchIdentity>();identity.Configure(slot,name);return go.AddComponent<HeroMatchStatistics>();
        }
        private static MatchStatisticsSnapshot Snapshot(MatchStatisticsController stats,int id){List<MatchStatisticsSnapshot> values=new();stats.CopySnapshotsTo(values);for(int i=0;i<values.Count;i++)if(values[i].HeroId==id)return values[i];Assert.Fail("Missing row");return default;}
        private GameObject Track(GameObject item){created.Add(item);return item;}
        private static void SetTeam(GameObject target,TeamId team){TeamMember member=target.AddComponent<TeamMember>();Set(member,"team",team);}
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private void Cleanup(){for(int i=created.Count-1;i>=0;i--)if(created[i]!=null)Object.Destroy(created[i]);created.Clear();}
    }
}

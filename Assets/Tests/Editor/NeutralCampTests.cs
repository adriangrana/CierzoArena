using System.Collections.Generic;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using CierzoArena.Structures;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class NeutralCampTests
    {
        private readonly List<Object> created=new(); private readonly List<NeutralUnitController> units=new();
        [TearDown] public void TearDown(){for(int i=created.Count-1;i>=0;i--)if(created[i]!=null)Object.DestroyImmediate(created[i]);}
        [Test]
        public void NeutralFactionIsHostileToBothTeamsButNotItself()
        {
            TeamMember neutral=CreateMember("Neutral",TeamId.Neutral);TeamMember azure=CreateMember("Azure",TeamId.Azure);TeamMember ember=CreateMember("Ember",TeamId.Ember);TeamMember otherNeutral=CreateMember("Other neutral",TeamId.Neutral);
            Assert.That(neutral.IsEnemy(azure),Is.True);Assert.That(neutral.IsEnemy(ember),Is.True);Assert.That(neutral.IsEnemy(otherNeutral),Is.False);
        }
        [Test]
        public void CampSpawnsOnceTransitionsToRespawnAndReturnsWholeComposition()
        {
            GameObject prefab=CreateNeutralPrefab();GameObject campObject=new GameObject("Camp");created.Add(campObject);NeutralCamp camp=campObject.AddComponent<NeutralCamp>();camp.Configure("test",new[]{new NeutralSpawnEntry(NeutralCampCategory.Small,prefab,Vector3.zero,2)},8f,15f,1f,2f);
            camp.SpawnInitial();camp.SpawnInitial();Assert.That(camp.State,Is.EqualTo(NeutralCampState.Alive));Assert.That(camp.ActiveUnitCount,Is.EqualTo(2));camp.CopyUnitsTo(units);foreach(NeutralUnitController unit in units)unit.GetComponent<Health>().ApplyDamage(9999f);
            Assert.That(camp.State,Is.EqualTo(NeutralCampState.Respawning));camp.Simulate(1.9f);Assert.That(camp.State,Is.EqualTo(NeutralCampState.Respawning));camp.Simulate(.2f);Assert.That(camp.State,Is.EqualTo(NeutralCampState.Alive));Assert.That(camp.ActiveUnitCount,Is.EqualTo(2));
        }
        [Test]
        public void NeutralAcquiresHeroButRejectsNeutralAlly()
        {
            GameObject campObject=new GameObject("Camp");created.Add(campObject);NeutralCamp camp=campObject.AddComponent<NeutralCamp>();camp.Configure("test",System.Array.Empty<NeutralSpawnEntry>(),8f,15f,1f,10f);GameObject neutral=CreateNeutralPrefab();NeutralUnitController controller=neutral.GetComponent<NeutralUnitController>();controller.Configure(camp,Vector3.zero);GameObject hero=CreateCombatant("Azure",TeamId.Azure,new Vector3(2f,0f,0f));GameObject ally=CreateCombatant("Ally",TeamId.Neutral,new Vector3(2f,0f,0f));
            Assert.That(controller.TryAssist(hero.GetComponent<Health>()),Is.True);Assert.That(controller.TryAssist(ally.GetComponent<Health>()),Is.False);
        }
        [Test]
        public void RespawningCampDoesNotReturnAfterMatchVictory()
        {
            GameObject prefab=CreateNeutralPrefab();GameObject matchObject=new GameObject("Match");created.Add(matchObject);MatchStateController match=matchObject.AddComponent<MatchStateController>();typeof(MatchStateController).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(match,null);GameObject campObject=new GameObject("Camp");created.Add(campObject);NeutralCamp camp=campObject.AddComponent<NeutralCamp>();camp.Configure("test",new[]{new NeutralSpawnEntry(NeutralCampCategory.Small,prefab,Vector3.zero)},8f,15f,1f,1f);camp.SpawnInitial();camp.CopyUnitsTo(units);units[0].GetComponent<Health>().ApplyDamage(9999f);match.ApplyAuthoritativeState(MatchState.AzureVictory);camp.Simulate(99f);Assert.That(camp.State,Is.EqualTo(NeutralCampState.Respawning));
        }
        private TeamMember CreateMember(string name,TeamId team){GameObject item=new GameObject(name);created.Add(item);TeamMember member=item.AddComponent<TeamMember>();Set(member,"team",team);return member;}
        private GameObject CreateNeutralPrefab(){GameObject item=CreateCombatant("Neutral",TeamId.Neutral,Vector3.zero);item.AddComponent<NeutralUnitController>();return item;}
        private GameObject CreateCombatant(string name,TeamId team,Vector3 position){GameObject item=new GameObject(name);created.Add(item);item.transform.position=position;TeamMember member=item.AddComponent<TeamMember>();Set(member,"team",team);item.AddComponent<Health>();item.AddComponent<BasicAttack>();item.AddComponent<ClickMover>();return item;}
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    }
}

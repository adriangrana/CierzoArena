using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class NeutralBossTests
    {
        private GameObject bossObject; private GameObject heroObject;
        [TearDown] public void TearDown(){if(bossObject!=null)Object.DestroyImmediate(bossObject);if(heroObject!=null)Object.DestroyImmediate(heroObject);}
        [Test]
        public void DamageCreatesThreatAndBossSelectsTheAttacker()
        {
            NeutralBossController boss=CreateBoss();Health hero=CreateHero(new Vector3(2f,0f,0f));boss.Simulate(0f);bossObject.GetComponent<Health>().ApplyDamage(new DamageContext(heroObject.GetComponent<TeamMember>(),10f,AttackDelivery.Melee));boss.Simulate(0f);Assert.That(boss.CurrentTarget,Is.EqualTo(hero));Assert.That(boss.State,Is.EqualTo(NeutralBossState.Engaged));
        }
        [Test]
        public void AreaStrikeWaitsForCastPointAndAppliesOnce()
        {
            NeutralBossController boss=CreateBoss();Set(boss,"specialCastPoint",1f);Set(boss,"specialDamage",40f);Health hero=CreateHero(new Vector3(2f,0f,0f));bossObject.GetComponent<Health>().ApplyDamage(new DamageContext(heroObject.GetComponent<TeamMember>(),10f,AttackDelivery.Melee));float before=hero.Current;boss.Simulate(0f);Assert.That(boss.TelegraphActive,Is.True);Assert.That(hero.Current,Is.EqualTo(before));boss.Simulate(.9f);Assert.That(hero.Current,Is.EqualTo(before));boss.Simulate(.2f);Assert.That(hero.Current,Is.LessThan(before));Assert.That(boss.TelegraphActive,Is.False);
        }
        private NeutralBossController CreateBoss(){bossObject=new GameObject("Boss");SetTeam(bossObject,TeamId.Neutral);bossObject.AddComponent<Health>();bossObject.AddComponent<BasicAttack>();bossObject.AddComponent<ClickMover>();NeutralBossController boss=bossObject.AddComponent<NeutralBossController>();boss.Configure(Vector3.zero,10f,20f,1f,30f);return boss;}
        private Health CreateHero(Vector3 position){heroObject=new GameObject("Hero");heroObject.transform.position=position;SetTeam(heroObject,TeamId.Azure);heroObject.AddComponent<HeroUnit>();return heroObject.AddComponent<Health>();}
        private static void SetTeam(GameObject target,TeamId team){TeamMember member=target.AddComponent<TeamMember>();Set(member,"team",team);}
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    }
}

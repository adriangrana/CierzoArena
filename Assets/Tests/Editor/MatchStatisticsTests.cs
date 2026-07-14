using System.Collections.Generic;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class MatchStatisticsTests
    {
        private readonly List<GameObject> created=new();
        [TearDown] public void TearDown(){for(int i=created.Count-1;i>=0;i--)if(created[i]!=null)Object.DestroyImmediate(created[i]);created.Clear();}

        [Test]
        public void HeroKillRecordsDeathDamageAndRecentAssistExactlyOnce()
        {
            MatchStatisticsController stats=CreateMatch();
            HeroMatchStatistics killer=CreateHero(stats,"Killer",TeamId.Azure,0);
            HeroMatchStatistics assistant=CreateHero(stats,"Assistant",TeamId.Azure,1);
            HeroMatchStatistics victim=CreateHero(stats,"Victim",TeamId.Ember,0);
            victim.Health.ApplyDamage(new DamageContext(assistant.GetComponent<TeamMember>(),30f,AttackDelivery.Melee));
            victim.Health.ApplyDamage(new DamageContext(killer.GetComponent<TeamMember>(),999f,AttackDelivery.Melee));
            victim.Health.ApplyDamage(new DamageContext(killer.GetComponent<TeamMember>(),1f,AttackDelivery.Melee));
            MatchStatisticsSnapshot a=Snapshot(stats,killer.HeroId),b=Snapshot(stats,assistant.HeroId),v=Snapshot(stats,victim.HeroId);
            Assert.That(a.Kills,Is.EqualTo(1));Assert.That(v.Deaths,Is.EqualTo(1));Assert.That(b.Assists,Is.EqualTo(1));
            Assert.That(a.HeroDamageDealt,Is.GreaterThan(0));Assert.That(v.HeroDamageReceived,Is.EqualTo(a.HeroDamageDealt+b.HeroDamageDealt));
        }

        [Test]
        public void LastHitsGoldExperienceAndStructureDamageUseConfirmedEventsAndFreezeAtVictory()
        {
            MatchStatisticsController stats=CreateMatch();HeroMatchStatistics hero=CreateHero(stats,"Azure",TeamId.Azure,0,true);
            HeroEconomy economy=hero.GetComponent<HeroEconomy>();HeroProgression progression=hero.GetComponent<HeroProgression>();
            economy.TryAddGold(70);economy.TrySpendGold(20);progression.TryGainExperience(50);
            GameObject creep=Track(new GameObject("Creep"));SetTeam(creep,TeamId.Ember);Health creepHealth=creep.AddComponent<Health>();creep.AddComponent<CreepController>();
            creepHealth.ApplyDamage(new DamageContext(hero.GetComponent<TeamMember>(),999f,AttackDelivery.Melee));
            GameObject tower=Track(new GameObject("Tower"));SetTeam(tower,TeamId.Ember);tower.AddComponent<Health>();StructureEntity structure=tower.AddComponent<StructureEntity>();
            structure.TryApplyDamage(hero.GetComponent<TeamMember>(),40f);
            MatchStatisticsSnapshot before=Snapshot(stats,hero.HeroId);
            Assert.That(before.LastHits,Is.EqualTo(1));Assert.That(before.CurrentGold,Is.EqualTo(50));Assert.That(before.GoldEarned,Is.EqualTo(70));Assert.That(before.ExperienceEarned,Is.EqualTo(50));Assert.That(before.StructureDamage,Is.EqualTo(40));
            stats.GetComponent<MatchStateController>().ApplyAuthoritativeState(MatchState.AzureVictory);
            structure.TryApplyDamage(hero.GetComponent<TeamMember>(),40f);economy.TryAddGold(20);progression.TryGainExperience(20);
            MatchStatisticsSnapshot frozen=Snapshot(stats,hero.HeroId);
            Assert.That(frozen.GoldEarned,Is.EqualTo(before.GoldEarned));Assert.That(frozen.StructureDamage,Is.EqualTo(before.StructureDamage));Assert.That(frozen.ExperienceEarned,Is.EqualTo(before.ExperienceEarned));
        }

        [Test]
        public void NonHeroDeathGivesVictimDeathButNeverInventsKill()
        {
            MatchStatisticsController stats=CreateMatch();HeroMatchStatistics victim=CreateHero(stats,"Victim",TeamId.Azure,0);
            GameObject neutral=Track(new GameObject("Neutral"));TeamMember neutralTeam=SetTeam(neutral,TeamId.Neutral);
            victim.Health.ApplyDamage(new DamageContext(neutralTeam,999f,AttackDelivery.Melee));
            MatchStatisticsSnapshot value=Snapshot(stats,victim.HeroId);
            Assert.That(value.Deaths,Is.EqualTo(1));Assert.That(value.Kills,Is.Zero);
        }

        [Test]
        public void BossSecureCountsOnlyRecentSameTeamParticipants()
        {
            MatchStatisticsController stats=CreateMatch();HeroMatchStatistics killer=CreateHero(stats,"Killer",TeamId.Azure,0);HeroMatchStatistics ally=CreateHero(stats,"Ally",TeamId.Azure,1);HeroMatchStatistics enemy=CreateHero(stats,"Enemy",TeamId.Ember,0);
            GameObject boss=Track(new GameObject("Boss"));SetTeam(boss,TeamId.Neutral);boss.AddComponent<Health>();boss.AddComponent<BasicAttack>();boss.AddComponent<ClickMover>();boss.AddComponent<NeutralBossController>();
            Health health=boss.GetComponent<Health>();health.ApplyDamage(new DamageContext(ally.GetComponent<TeamMember>(),20f,AttackDelivery.Melee));health.ApplyDamage(new DamageContext(enemy.GetComponent<TeamMember>(),20f,AttackDelivery.Melee));health.ApplyDamage(new DamageContext(killer.GetComponent<TeamMember>(),999f,AttackDelivery.Melee));
            Assert.That(Snapshot(stats,killer.HeroId).MajorObjectiveSecures,Is.EqualTo(1));Assert.That(Snapshot(stats,killer.HeroId).BossParticipations,Is.EqualTo(1));Assert.That(Snapshot(stats,ally.HeroId).BossParticipations,Is.EqualTo(1));Assert.That(Snapshot(stats,enemy.HeroId).BossParticipations,Is.Zero);
        }

        [Test]
        public void ReRegisteringAfterRuntimeTeamAssignmentKeepsBothScoreboardRows()
        {
            MatchStatisticsController stats=CreateMatch();
            HeroMatchStatistics azure=CreateHero(stats,"Azure",TeamId.Azure,0);
            HeroMatchStatistics pending=CreateHero(stats,"Pending",TeamId.Azure,0);
            pending.GetComponent<TeamMember>().ConfigureTeam(TeamId.Ember);
            stats.RegisterHero(pending);
            List<MatchStatisticsSnapshot> rows=new();stats.CopySnapshotsTo(rows);
            Assert.That(rows.Count,Is.EqualTo(2));
            Assert.That(rows.Exists(value=>value.HeroId==azure.HeroId),Is.True);
            Assert.That(rows.Exists(value=>value.HeroId==pending.HeroId&&value.Team==TeamId.Ember),Is.True);
        }

        private MatchStatisticsController CreateMatch(){GameObject go=Track(new GameObject("Match"));go.AddComponent<MatchStateController>();return go.AddComponent<MatchStatisticsController>();}
        private HeroMatchStatistics CreateHero(MatchStatisticsController stats,string name,TeamId team,int slot,bool fullProgression=false)
        {
            GameObject go=Track(new GameObject(name));SetTeam(go,team);go.AddComponent<HeroUnit>();go.AddComponent<Health>();
            if(fullProgression){go.AddComponent<ClickMover>();go.AddComponent<BasicAttack>();go.AddComponent<HeroProgression>();go.AddComponent<HeroEconomy>();}
            HeroMatchIdentity identity=go.AddComponent<HeroMatchIdentity>();identity.Configure(slot,name);HeroMatchStatistics hero=go.AddComponent<HeroMatchStatistics>();stats.RegisterHero(hero);return hero;
        }
        private static MatchStatisticsSnapshot Snapshot(MatchStatisticsController stats,int id){List<MatchStatisticsSnapshot> values=new();stats.CopySnapshotsTo(values);for(int i=0;i<values.Count;i++)if(values[i].HeroId==id)return values[i];Assert.Fail("Missing statistics row.");return default;}
        private GameObject Track(GameObject value){created.Add(value);return value;}
        private static TeamMember SetTeam(GameObject value,TeamId team){TeamMember member=value.AddComponent<TeamMember>();typeof(TeamMember).GetField("team",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(member,team);return member;}
    }
}

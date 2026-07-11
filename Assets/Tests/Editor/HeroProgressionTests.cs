using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class HeroProgressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp() => ClearMatchSingleton();

        [TearDown]
        public void TearDown() => ClearMatchSingleton();

        [Test]
        public void ExperienceCarriesAcrossLevelsAndImprovesBasicStats()
        {
            GameObject hero = CreateHero("Azure", TeamId.Azure);
            HeroProgression progression = hero.GetComponent<HeroProgression>();
            Health health = hero.GetComponent<Health>();
            BasicAttack attack = hero.GetComponent<BasicAttack>();

            Assert.That(progression.Level, Is.EqualTo(1));
            Assert.That(progression.Experience, Is.Zero);
            Assert.That(progression.TryGainExperience(99), Is.False);
            Assert.That(progression.Level, Is.EqualTo(1));
            Assert.That(progression.Experience, Is.EqualTo(99));

            progression.TryGainExperience(1);

            Assert.That(progression.Level, Is.EqualTo(2));
            Assert.That(progression.Experience, Is.Zero);
            Assert.That(health.Max, Is.EqualTo(580f));
            Assert.That(health.Current, Is.EqualTo(580f));
            Assert.That(attack.Damage, Is.EqualTo(53f));

            progression.TryGainExperience(1000);
            Assert.That(progression.Level, Is.GreaterThan(2));
            Assert.That(progression.Experience, Is.GreaterThanOrEqualTo(0));

            Object.DestroyImmediate(hero);
        }

        [Test]
        public void MaximumLevelCapsFurtherExperienceWithoutLooping()
        {
            GameObject hero = CreateHero("Azure", TeamId.Azure);
            HeroProgression progression = hero.GetComponent<HeroProgression>();

            progression.TryGainExperience(int.MaxValue);

            Assert.That(progression.Level, Is.EqualTo(progression.MaximumLevel));
            Assert.That(progression.Experience, Is.Zero);
            Assert.That(progression.TryGainExperience(100), Is.False);

            Object.DestroyImmediate(hero);
        }

        [Test]
        public void CreepAndHeroRewardsGoOnceOnlyToEnemyHeroLastHitter()
        {
            GameObject azure = CreateHero("Azure", TeamId.Azure);
            GameObject emberCreep = CreateRewardVictim("Ember Creep", TeamId.Ember, 60, hero: false);
            GameObject emberHero = CreateRewardVictim("Ember Hero", TeamId.Ember, 300, hero: true);
            HeroProgression progression = azure.GetComponent<HeroProgression>();
            Health azureHealth = azure.GetComponent<Health>();
            TeamMember azureTeam = azure.GetComponent<TeamMember>();

            emberCreep.GetComponent<Health>().ApplyDamage(new DamageContext(azureTeam, 9999f, AttackDelivery.Melee));
            Assert.That(progression.TotalExperience, Is.EqualTo(60));
            emberCreep.GetComponent<Health>().ApplyDamage(new DamageContext(azureTeam, 1f, AttackDelivery.Melee));
            Assert.That(progression.TotalExperience, Is.EqualTo(60));

            emberHero.GetComponent<Health>().ApplyDamage(new DamageContext(azureTeam, 9999f, AttackDelivery.Melee));
            Assert.That(progression.TotalExperience, Is.EqualTo(360));

            Object.DestroyImmediate(azureHealth.gameObject);
            Object.DestroyImmediate(emberCreep);
            Object.DestroyImmediate(emberHero);
        }

        [Test]
        public void FriendlyAndFinishedMatchKillsDoNotGrantExperience()
        {
            GameObject azure = CreateHero("Azure", TeamId.Azure);
            GameObject victim = CreateRewardVictim("Azure Friendly", TeamId.Azure, 60, hero: false);
            HeroProgression progression = azure.GetComponent<HeroProgression>();

            victim.GetComponent<Health>().ApplyDamage(new DamageContext(azure.GetComponent<TeamMember>(), 9999f, AttackDelivery.Melee));
            Assert.That(progression.TotalExperience, Is.Zero);

            Object.DestroyImmediate(victim);
            GameObject matchObject = new GameObject("Match");
            MatchStateController match = matchObject.AddComponent<MatchStateController>();
            InvokePrivate(match, "Awake");
            match.ApplyAuthoritativeState(MatchState.AzureVictory);
            GameObject endedVictim = CreateRewardVictim("Ended Creep", TeamId.Ember, 60, hero: false);
            endedVictim.GetComponent<Health>().ApplyDamage(new DamageContext(azure.GetComponent<TeamMember>(), 9999f, AttackDelivery.Melee));
            Assert.That(progression.TotalExperience, Is.Zero);

            Object.DestroyImmediate(endedVictim);
            Object.DestroyImmediate(matchObject);
            Object.DestroyImmediate(azure);
        }

        private static GameObject CreateHero(string name, TeamId team)
        {
            GameObject hero = new GameObject(name);
            TeamMember member = hero.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            Health health = hero.AddComponent<Health>();
            InvokePrivate(health, "Awake");
            hero.AddComponent<HeroUnit>();
            hero.AddComponent<ClickMover>();
            hero.AddComponent<BasicAttack>();
            HeroProgression progression = hero.AddComponent<HeroProgression>();
            InvokePrivate(progression, "Awake");
            return hero;
        }

        private static GameObject CreateRewardVictim(string name, TeamId team, int reward, bool hero)
        {
            GameObject victim = new GameObject(name);
            TeamMember member = victim.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            Health health = victim.AddComponent<Health>();
            InvokePrivate(health, "Awake");
            if (hero) victim.AddComponent<HeroUnit>();
            ExperienceReward experienceReward = victim.AddComponent<ExperienceReward>();
            SetPrivate(experienceReward, "experienceReward", reward);
            InvokePrivate(experienceReward, "Awake");
            return victim;
        }

        private static void SetPrivate(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        private static void InvokePrivate(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
        private static void ClearMatchSingleton() => typeof(MatchStateController).GetField("active", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
    }
}

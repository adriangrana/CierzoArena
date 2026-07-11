using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class SharedExperienceAndGoldTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            DestroyResidualObjects();
            ClearMatch();
        }
        [TearDown] public void TearDown() => ClearMatch();

        [Test]
        public void NearbyHeroesSplitCreepExperienceAndOnlyLastHitterGetsGold()
        {
            GameObject first = CreateHero("First", TeamId.Azure, new Vector3(1f, 0f, 0f));
            GameObject second = CreateHero("Second", TeamId.Azure, new Vector3(2f, 0f, 0f));
            GameObject distant = CreateHero("Distant", TeamId.Azure, new Vector3(30f, 0f, 0f));
            GameObject creep = CreateCreepReward(TeamId.Ember, 61, 40, 5f);

            creep.GetComponent<Health>().ApplyDamage(new DamageContext(second.GetComponent<TeamMember>(), 9999f, AttackDelivery.Melee));

            int firstXp = first.GetComponent<HeroProgression>().TotalExperience;
            int secondXp = second.GetComponent<HeroProgression>().TotalExperience;
            Assert.That(firstXp + secondXp, Is.EqualTo(61));
            Assert.That(Mathf.Abs(firstXp - secondXp), Is.LessThanOrEqualTo(1));
            Assert.That(distant.GetComponent<HeroProgression>().TotalExperience, Is.Zero);
            Assert.That(first.GetComponent<HeroEconomy>().Gold, Is.Zero);
            Assert.That(second.GetComponent<HeroEconomy>().Gold, Is.EqualTo(40));

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(distant);
            Object.DestroyImmediate(creep);
        }

        [Test]
        public void NonHeroOrFriendlyLastHitDoesNotGrantGoldAndVictoryBlocksRewards()
        {
            GameObject azure = CreateHero("Azure", TeamId.Azure, Vector3.zero);
            GameObject creep = CreateCreepReward(TeamId.Ember, 60, 40, 8f);
            GameObject tower = new GameObject("Tower");
            TeamMember towerTeam = tower.AddComponent<TeamMember>();
            SetPrivate(towerTeam, "team", TeamId.Azure);

            creep.GetComponent<Health>().ApplyDamage(new DamageContext(towerTeam, 9999f, AttackDelivery.Melee));
            Assert.That(azure.GetComponent<HeroEconomy>().Gold, Is.Zero);

            Object.DestroyImmediate(creep);
            GameObject matchObject = new GameObject("Match");
            MatchStateController match = matchObject.AddComponent<MatchStateController>();
            InvokePrivate(match, "Awake");
            match.ApplyAuthoritativeState(MatchState.AzureVictory);
            GameObject endedCreep = CreateCreepReward(TeamId.Ember, 60, 40, 8f);
            endedCreep.GetComponent<Health>().ApplyDamage(new DamageContext(azure.GetComponent<TeamMember>(), 9999f, AttackDelivery.Melee));
            Assert.That(azure.GetComponent<HeroProgression>().TotalExperience, Is.EqualTo(60));
            Assert.That(azure.GetComponent<HeroEconomy>().Gold, Is.Zero);

            Object.DestroyImmediate(azure);
            Object.DestroyImmediate(endedCreep);
            Object.DestroyImmediate(tower);
            Object.DestroyImmediate(matchObject);
        }

        private static GameObject CreateHero(string name, TeamId team, Vector3 position)
        {
            GameObject hero = new GameObject(name);
            hero.transform.position = position;
            TeamMember member = hero.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            Health health = hero.AddComponent<Health>();
            InvokePrivate(health, "Awake");
            hero.AddComponent<HeroUnit>();
            hero.AddComponent<ClickMover>();
            hero.AddComponent<BasicAttack>();
            HeroProgression progression = hero.AddComponent<HeroProgression>();
            InvokePrivate(progression, "Awake");
            HeroEconomy economy = hero.AddComponent<HeroEconomy>();
            InvokePrivate(economy, "Awake");
            return hero;
        }

        private static GameObject CreateCreepReward(TeamId team, int experience, int gold, float radius)
        {
            GameObject creep = new GameObject("Creep");
            TeamMember member = creep.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            Health health = creep.AddComponent<Health>();
            InvokePrivate(health, "Awake");
            ExperienceReward reward = creep.AddComponent<ExperienceReward>();
            SetPrivate(reward, "experienceReward", experience);
            SetPrivate(reward, "goldReward", gold);
            SetPrivate(reward, "experienceRadius", radius);
            SetPrivate(reward, "shareExperienceWithNearbyHeroes", true);
            InvokePrivate(reward, "Awake");
            return creep;
        }

        private static void SetPrivate(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        private static void InvokePrivate(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
        private static void ClearMatch() => typeof(MatchStateController).GetField("active", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);

        private static void DestroyResidualObjects()
        {
            foreach (string name in new[] { "First", "Second", "Distant", "Azure", "Creep", "Tower", "Match" })
            {
                GameObject residual = GameObject.Find(name);
                if (residual != null) Object.DestroyImmediate(residual);
            }
        }
    }
}

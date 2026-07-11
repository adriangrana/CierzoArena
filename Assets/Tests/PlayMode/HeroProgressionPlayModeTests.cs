using System.Collections;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    public sealed class HeroProgressionPlayModeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator HeroLastHitsCreepsLevelsUpAndKeepsProgressAfterDeath()
        {
            GameObject hero = CreateHero("Azure", TeamId.Azure);
            GameObject firstCreep = CreateCreep("Ember One", TeamId.Ember, 60);
            GameObject secondCreep = CreateCreep("Ember Two", TeamId.Ember, 60);
            HeroProgression progression = hero.GetComponent<HeroProgression>();
            Health heroHealth = hero.GetComponent<Health>();
            TeamMember team = hero.GetComponent<TeamMember>();

            firstCreep.GetComponent<Health>().ApplyDamage(new DamageContext(team, 9999f, AttackDelivery.Melee));
            secondCreep.GetComponent<Health>().ApplyDamage(new DamageContext(team, 9999f, AttackDelivery.Melee));
            yield return null;

            Assert.That(progression.Level, Is.EqualTo(2));
            Assert.That(heroHealth.Max, Is.EqualTo(580f));
            heroHealth.ApplyDamage(heroHealth.Max);
            Assert.That(progression.Level, Is.EqualTo(2));
            Assert.That(progression.TotalExperience, Is.EqualTo(120));

            Object.Destroy(hero);
            Object.Destroy(firstCreep);
            Object.Destroy(secondCreep);
        }

        private static GameObject CreateHero(string name, TeamId team)
        {
            GameObject hero = new GameObject(name);
            TeamMember member = hero.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            hero.AddComponent<Health>();
            hero.AddComponent<HeroUnit>();
            hero.AddComponent<ClickMover>();
            hero.AddComponent<BasicAttack>();
            hero.AddComponent<HeroProgression>();
            return hero;
        }

        private static GameObject CreateCreep(string name, TeamId team, int reward)
        {
            GameObject creep = new GameObject(name);
            TeamMember member = creep.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            creep.AddComponent<Health>();
            ExperienceReward experienceReward = creep.AddComponent<ExperienceReward>();
            SetPrivate(experienceReward, "experienceReward", reward);
            return creep;
        }

        private static void SetPrivate(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    }
}

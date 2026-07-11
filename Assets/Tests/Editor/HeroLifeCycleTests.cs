using System.Collections;
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
    public sealed class HeroLifeCycleTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            DestroyLegacyFailedTestObjects();
            ClearSingletons();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            ClearSingletons();
        }

        [Test]
        public void HeroDeathBlocksOrdersThenRespawnsAtItsTeamPointFullyRestored()
        {
            GameObject spawnObject = Track(CreateSpawn(TeamId.Azure, new Vector3(-12f, 1f, 4f)));
            GameObject heroObject = Track(CreateHero("M8 Test Azure Hero", TeamId.Azure, Vector3.zero));
            GameObject enemyObject = Track(CreateTarget("M8 Test Ember Target", TeamId.Ember));
            HeroLifeCycle life = heroObject.GetComponent<HeroLifeCycle>();
            Health health = heroObject.GetComponent<Health>();
            UnitOrderController orders = heroObject.GetComponent<UnitOrderController>();
            BasicAttack enemyAttack = enemyObject.AddComponent<BasicAttack>();
            SetPrivate(life, "respawnDelay", 3f);
            int deathCount = 0;
            health.Died += _ => deathCount++;

            health.ApplyDamage(health.Max);
            health.ApplyDamage(1f);

            Assert.That(life.State, Is.EqualTo(HeroLifeState.Dead));
            Assert.That(deathCount, Is.EqualTo(1));
            Assert.That(orders.IssueMove(new Vector3(4f, 0f, 4f)), Is.False);
            Assert.That(orders.IssueAttack(enemyObject.GetComponent<Health>()), Is.False);
            Assert.That(enemyAttack.CanAttack(health), Is.False);
            Assert.That(heroObject.GetComponent<Renderer>().enabled, Is.False);

            Assert.That(life.Simulate(2.99f), Is.False);
            Assert.That(life.State, Is.EqualTo(HeroLifeState.Respawning));
            Assert.That(life.Simulate(0.01f), Is.True);

            Assert.That(life.State, Is.EqualTo(HeroLifeState.Alive));
            Assert.That(health.Current, Is.EqualTo(health.Max));
            Assert.That(heroObject.transform.position, Is.EqualTo(spawnObject.transform.position));
            Assert.That(heroObject.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(orders.IssueMove(new Vector3(4f, 0f, 4f)), Is.True);
            Assert.That(enemyAttack.CanAttack(health), Is.True);

        }

        [Test]
        public void HeroDoesNotRespawnAfterMatchVictory()
        {
            GameObject spawnObject = Track(CreateSpawn(TeamId.Azure, Vector3.zero));
            GameObject matchObject = Track(new GameObject("M8 Test Match"));
            MatchStateController match = matchObject.AddComponent<MatchStateController>();
            InvokePrivate(match, "Awake");
            GameObject heroObject = Track(CreateHero("M8 Test Azure Hero", TeamId.Azure, new Vector3(2f, 0f, 0f)));
            HeroLifeCycle life = heroObject.GetComponent<HeroLifeCycle>();
            Health health = heroObject.GetComponent<Health>();
            SetPrivate(life, "respawnDelay", 1f);

            Assert.That(match.ApplyAuthoritativeState(MatchState.AzureVictory), Is.True);
            health.ApplyDamage(health.Max);

            Assert.That(life.Simulate(10f), Is.False);
            Assert.That(life.State, Is.EqualTo(HeroLifeState.Dead));
            Assert.That(health.IsAlive, Is.False);

        }

        private GameObject Track(GameObject gameObject)
        {
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static GameObject CreateHero(string name, TeamId team, Vector3 position)
        {
            GameObject hero = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hero.name = name;
            hero.transform.position = position;
            TeamMember member = hero.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            Health health = hero.AddComponent<Health>();
            InvokePrivate(health, "Awake");
            hero.AddComponent<HeroUnit>();
            hero.AddComponent<ClickMover>();
            hero.AddComponent<BasicAttack>();
            UnitOrderController orders = hero.AddComponent<UnitOrderController>();
            InvokePrivate(orders, "Awake");
            HeroLifeCycle life = hero.AddComponent<HeroLifeCycle>();
            InvokePrivate(life, "Awake");
            return hero;
        }

        private static GameObject CreateTarget(string name, TeamId team)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = name;
            TeamMember member = target.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            Health health = target.AddComponent<Health>();
            InvokePrivate(health, "Awake");
            return target;
        }

        private static GameObject CreateSpawn(TeamId team, Vector3 position)
        {
            GameObject spawn = new GameObject($"{team} Spawn");
            spawn.transform.position = position;
            HeroSpawnPoint point = spawn.AddComponent<HeroSpawnPoint>();
            point.SetTeam(team);
            InvokePrivate(point, "OnEnable");
            return spawn;
        }

        private static void SetPrivate(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        private static void InvokePrivate(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);

        private static void ClearSingletons()
        {
            typeof(MatchStateController).GetField("active", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            IDictionary points = (IDictionary)typeof(HeroSpawnPoint).GetField("activePoints", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            points.Clear();
        }

        private static void DestroyLegacyFailedTestObjects()
        {
            // One earlier version used these generic names and could leak them when
            // an assertion aborted the test before its manual cleanup.
            foreach (string name in new[] { "Azure Hero", "Ember Target", "Azure Spawn" })
            {
                GameObject residual = GameObject.Find(name);
                if (residual != null)
                {
                    Object.DestroyImmediate(residual);
                }
            }
        }
    }
}

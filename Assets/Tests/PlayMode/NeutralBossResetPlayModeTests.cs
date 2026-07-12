using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    /// <summary>Regression coverage for Resetting -> Alive movement reactivation.</summary>
    public sealed class NeutralBossResetPlayModeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> created = new();

        [UnityTest]
        public IEnumerator BossRearmsNavigationAndPursuesAfterTwoCompleteLeashResets()
        {
            CreateGround();
            NeutralBossController boss = CreateBoss();
            Health firstHero = CreateHero("First Hero", new Vector3(8f, 1f, 0f));
            Physics.SyncTransforms();
            yield return null;

            Engage(boss, firstHero);
            yield return WaitUntil(() => boss.HasMoveCommand, 60);
            Assert.That(boss.HasMoveCommand, Is.True, "The first engagement must issue an approach destination.");

            firstHero.transform.position = new Vector3(16f, 1f, 0f);
            Physics.SyncTransforms();
            boss.Simulate(0.02f);
            Assert.That(boss.State, Is.EqualTo(NeutralBossState.Resetting));
            yield return WaitUntil(() => boss.State == NeutralBossState.Alive, 240);
            Assert.That(boss.State, Is.EqualTo(NeutralBossState.Alive));
            Assert.That(boss.NavigationReady, Is.True, "Completing the reset must release NavMeshAgent.isStopped.");

            Health secondHero = CreateHero("Second Hero", new Vector3(0f, 1f, 8f));
            Physics.SyncTransforms();
            Vector3 firstRestartPosition = boss.transform.position;
            boss.Simulate(0.02f);
            Assert.That(boss.CurrentTarget, Is.EqualTo(secondHero));
            Assert.That(boss.GetComponent<BasicAttack>().NeedsApproach, Is.True);
            Assert.That(boss.HasMoveCommand, Is.True, "A new distant target after reset must receive a new destination.");
            yield return WaitUntil(() => HorizontalDistance(boss.transform.position, firstRestartPosition) > .2f, 120);
            Assert.That(HorizontalDistance(boss.transform.position, firstRestartPosition), Is.GreaterThan(.2f));
            yield return WaitUntil(() => secondHero.Current < secondHero.Max, 240);
            Assert.That(secondHero.Current, Is.LessThan(secondHero.Max), "The boss must attack after the post-reset approach.");

            secondHero.transform.position = new Vector3(0f, 1f, 16f);
            Physics.SyncTransforms();
            boss.Simulate(0.02f);
            Assert.That(boss.State, Is.EqualTo(NeutralBossState.Resetting));
            yield return WaitUntil(() => boss.State == NeutralBossState.Alive, 240);
            Assert.That(boss.NavigationReady, Is.True, "Navigation must also re-arm on the second reset.");

            Health thirdHero = CreateHero("Third Hero", new Vector3(-8f, 1f, 0f));
            Physics.SyncTransforms();
            Vector3 secondRestartPosition = boss.transform.position;
            boss.Simulate(0.02f);
            Assert.That(boss.CurrentTarget, Is.EqualTo(thirdHero));
            Assert.That(boss.GetComponent<BasicAttack>().NeedsApproach, Is.True);
            Assert.That(boss.HasMoveCommand, Is.True);
            yield return WaitUntil(() => HorizontalDistance(boss.transform.position, secondRestartPosition) > .2f, 120);
            Assert.That(HorizontalDistance(boss.transform.position, secondRestartPosition), Is.GreaterThan(.2f));

            Cleanup();
        }

        [TearDown]
        public void TearDown() => Cleanup();

        private NeutralBossController CreateBoss()
        {
            GameObject unit = Track(new GameObject("Boss"));
            unit.transform.position = new Vector3(0f, 1f, 0f);
            SetTeam(unit, TeamId.Neutral);
            unit.AddComponent<Health>();
            BasicAttack attack = unit.AddComponent<BasicAttack>();
            Set(attack, "useUnitDefinition", false);
            Set(attack, "range", 2f);
            Set(attack, "damage", 30f);
            Set(attack, "attackPoint", .01f);
            Set(attack, "backswing", .01f);
            unit.AddComponent<ClickMover>();
            NeutralBossController boss = unit.AddComponent<NeutralBossController>();
            boss.Configure(unit.transform.position, 10f, 12f, .5f, 30f);
            Set(boss, "specialRadius", .1f);
            return boss;
        }

        private Health CreateHero(string name, Vector3 position)
        {
            GameObject hero = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            hero.name = name;
            hero.transform.position = position;
            SetTeam(hero, TeamId.Azure);
            hero.AddComponent<HeroUnit>();
            return hero.AddComponent<Health>();
        }

        private void CreateGround()
        {
            GameObject ground = Track(GameObject.CreatePrimitive(PrimitiveType.Plane));
            ground.name = "Navigation Ground";
            ground.layer = 6;
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
        }

        private static void Engage(NeutralBossController boss, Health hero)
        {
            boss.GetComponent<Health>().ApplyDamage(new DamageContext(hero.GetComponent<TeamMember>(), 10f, AttackDelivery.Melee));
            boss.Simulate(0.02f);
            Assert.That(boss.CurrentTarget, Is.EqualTo(hero));
        }

        private static IEnumerator WaitUntil(Func<bool> condition, int maximumFrames)
        {
            // Test Runner can execute plain `yield return null` frames far faster
            // than real gameplay time. NavMeshAgent movement is time-based, so give
            // each poll a fixed slice of simulation time before measuring distance.
            for (int i = 0; i < maximumFrames && !condition(); i++) yield return new WaitForSeconds(.02f);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static void SetTeam(GameObject target, TeamId team)
        {
            TeamMember member = target.AddComponent<TeamMember>();
            Set(member, "team", team);
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

        private GameObject Track(GameObject value)
        {
            created.Add(value);
            return value;
        }

        private void Cleanup()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null) UnityEngine.Object.Destroy(created[i]);
            }
            created.Clear();
        }
    }
}

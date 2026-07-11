using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    /// <summary>Deterministic M6 timeline coverage; no animations or frame timing.</summary>
    public sealed class AdvancedAttackModelTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            SetMatchActive(null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (AttackProjectile projectile in Object.FindObjectsByType<AttackProjectile>())
            {
                Object.DestroyImmediate(projectile.gameObject);
            }
            SetMatchActive(null);
        }

        [Test]
        public void MeleeFollowsIdleWindupImpactAndBackswingWithoutEarlyDamage()
        {
            GameObject attacker = CreateUnit("Azure", TeamId.Azure, Vector3.zero);
            GameObject target = CreateUnit("Ember", TeamId.Ember, Vector3.right);
            BasicAttack attack = ConfigureAttack(attacker, AttackDelivery.Melee, 2f, 25f, 1f, 0.3f, 0.3f);
            Health health = target.GetComponent<Health>();

            Assert.That(attack.State, Is.EqualTo(AttackState.Idle));
            attack.SetTarget(health);
            attack.Simulate(0f);
            Assert.That(attack.State, Is.EqualTo(AttackState.Windup));
            attack.Simulate(0.29f);
            Assert.That(health.Current, Is.EqualTo(health.Max));
            attack.Simulate(0.02f);
            Assert.That(health.Current, Is.EqualTo(health.Max - 25f));
            Assert.That(attack.State, Is.EqualTo(AttackState.Backswing));

            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void OutOfRangeTargetApproachesAndCancellingWindupPreventsDamage()
        {
            GameObject attacker = CreateUnit("Azure", TeamId.Azure, Vector3.zero);
            GameObject target = CreateUnit("Ember", TeamId.Ember, Vector3.right * 5f);
            BasicAttack attack = ConfigureAttack(attacker, AttackDelivery.Melee, 2f, 25f, 1f, 0.3f, 0.3f);
            Health health = target.GetComponent<Health>();

            attack.SetTarget(health);
            attack.Simulate(0f);
            Assert.That(attack.State, Is.EqualTo(AttackState.Approaching));
            target.transform.position = Vector3.right;
            attack.Simulate(0f);
            Assert.That(attack.State, Is.EqualTo(AttackState.Windup));
            attack.CancelCurrentCycle();
            attack.Simulate(1f);
            Assert.That(health.Current, Is.EqualTo(health.Max));

            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void RangedReleasesAtAttackPointAndDamagesOnlyOnProjectileImpact()
        {
            GameObject attacker = CreateUnit("Azure", TeamId.Azure, Vector3.zero);
            GameObject target = CreateUnit("Ember", TeamId.Ember, Vector3.right * 3f);
            BasicAttack attack = ConfigureAttack(attacker, AttackDelivery.Ranged, 5f, 30f, 1f, 0.2f, 0.3f);
            Health health = target.GetComponent<Health>();
            int releases = 0;
            attack.ProjectileReleased += (_, _) => releases++;

            attack.SetTarget(health);
            attack.Simulate(0f);
            attack.Simulate(0.2f);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(health.Current, Is.EqualTo(health.Max));
            AttackProjectile projectile = Object.FindAnyObjectByType<AttackProjectile>();
            Assert.That(projectile, Is.Not.Null);
            projectile.Simulate(1f);
            Assert.That(health.Current, Is.EqualTo(health.Max - 30f));
            Assert.That(projectile == null, Is.True);
            Assert.That(health.Current, Is.EqualTo(health.Max - 30f));

            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void ProjectileKeepsOriginalTargetAfterAttackerDiesAndDropsDeadTarget()
        {
            GameObject attacker = CreateUnit("Azure", TeamId.Azure, Vector3.zero);
            GameObject target = CreateUnit("Ember", TeamId.Ember, Vector3.right * 3f);
            BasicAttack attack = ConfigureAttack(attacker, AttackDelivery.Ranged, 5f, 20f, 1f, 0.1f, 0.2f);
            Health attackerHealth = attacker.GetComponent<Health>();
            Health targetHealth = target.GetComponent<Health>();
            attack.SetTarget(targetHealth);
            attack.Simulate(0f);
            attack.Simulate(0.1f);
            AttackProjectile projectile = Object.FindAnyObjectByType<AttackProjectile>();
            attackerHealth.ApplyDamage(attackerHealth.Max);
            projectile.Simulate(1f);
            Assert.That(targetHealth.Current, Is.EqualTo(targetHealth.Max - 20f));
            if (projectile != null)
            {
                Object.DestroyImmediate(projectile.gameObject);
            }

            GameObject secondAttacker = CreateUnit("Azure Two", TeamId.Azure, Vector3.zero);
            BasicAttack secondAttack = ConfigureAttack(secondAttacker, AttackDelivery.Ranged, 5f, 20f, 1f, 0.1f, 0.2f);
            secondAttack.SetTarget(targetHealth);
            secondAttack.Simulate(0f);
            secondAttack.Simulate(0.1f);
            AttackProjectile abandoned = Object.FindAnyObjectByType<AttackProjectile>();
            targetHealth.ApplyDamage(targetHealth.Max);
            Assert.That(abandoned.Simulate(1f), Is.False);

            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(secondAttacker);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void InvalidTimingsNormalizeAndLargeTickReleasesOnce()
        {
            GameObject attacker = CreateUnit("Azure", TeamId.Azure, Vector3.zero);
            GameObject target = CreateUnit("Ember", TeamId.Ember, Vector3.right);
            BasicAttack attack = ConfigureAttack(attacker, AttackDelivery.Melee, -1f, 5f, -1f, 9f, 9f);
            Assert.That(attack.Range, Is.Zero);
            Assert.That(attack.AttackInterval, Is.GreaterThan(0f));

            SetPrivate(attack, "range", 2f);
            InvokePrivate(attack, "OnValidate");

            int points = 0;
            attack.AttackPointReached += (_, _, _) => points++;
            attack.SetTarget(target.GetComponent<Health>());
            attack.Simulate(0f);
            attack.Simulate(10f);
            Assert.That(points, Is.EqualTo(1));

            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void MatchEndCancelsWindupBeforeItsAttackPoint()
        {
            GameObject matchObject = new GameObject("Match");
            MatchStateController match = matchObject.AddComponent<MatchStateController>();
            InvokePrivate(match, "Awake");
            GameObject attacker = CreateUnit("Azure", TeamId.Azure, Vector3.zero);
            GameObject target = CreateUnit("Ember", TeamId.Ember, Vector3.right);
            BasicAttack attack = ConfigureAttack(attacker, AttackDelivery.Melee, 2f, 15f, 1f, 0.3f, 0.2f);
            Health targetHealth = target.GetComponent<Health>();

            attack.SetTarget(targetHealth);
            attack.Simulate(0f);
            match.ApplyAuthoritativeState(MatchState.AzureVictory);
            attack.Simulate(1f);
            Assert.That(attack.Target, Is.Null);
            Assert.That(targetHealth.Current, Is.EqualTo(targetHealth.Max));

            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(matchObject);
        }

        private static BasicAttack ConfigureAttack(GameObject unit, AttackDelivery delivery, float range, float damage, float interval, float point, float recovery)
        {
            BasicAttack attack = unit.AddComponent<BasicAttack>();
            SetPrivate(attack, "useUnitDefinition", false);
            SetPrivate(attack, "delivery", delivery);
            SetPrivate(attack, "range", range);
            SetPrivate(attack, "damage", damage);
            SetPrivate(attack, "attackInterval", interval);
            SetPrivate(attack, "attackPoint", point);
            SetPrivate(attack, "backswing", recovery);
            InvokePrivate(attack, "OnValidate");
            return attack;
        }

        private static GameObject CreateUnit(string name, TeamId team, Vector3 position)
        {
            GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unit.name = name;
            unit.transform.position = position;
            TeamMember member = unit.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            unit.AddComponent<Health>();
            return unit;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        }

        private static void InvokePrivate(object target, string method)
        {
            target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
        }

        private static void SetMatchActive(MatchStateController value)
        {
            typeof(MatchStateController).GetField("active", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }
    }
}

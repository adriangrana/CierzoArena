using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class HealthCombatAndOrderTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void HealthDamageClampsAtZeroAndRaisesDeathOnce()
        {
            GameObject unit = CreateUnit("Health Unit", TeamId.Azure);
            Health health = unit.GetComponent<Health>();
            int deathCount = 0;
            health.Died += _ => deathCount++;

            health.ApplyDamage(health.Max + 50f);
            health.ApplyDamage(1f);

            Assert.That(health.Current, Is.Zero);
            Assert.That(health.IsAlive, Is.False);
            Assert.That(deathCount, Is.EqualTo(1));

            Object.DestroyImmediate(unit);
        }

        [Test]
        public void HealthRestoresToMaximumAndRejectsInvalidDamage()
        {
            GameObject unit = CreateUnit("Health Unit", TeamId.Azure);
            Health health = unit.GetComponent<Health>();

            health.ApplyDamage(50f);
            health.ApplyDamage(0f);
            health.ApplyDamage(-10f);
            Assert.That(health.Current, Is.EqualTo(health.Max - 50f));

            health.RestoreFull();
            Assert.That(health.Current, Is.EqualTo(health.Max));
            Assert.That(health.IsAlive, Is.True);

            Object.DestroyImmediate(unit);
        }

        [Test]
        public void HealthValidationClampsMaximumToOne()
        {
            GameObject unit = CreateUnit("Health Unit", TeamId.Azure);
            Health health = unit.GetComponent<Health>();
            SetPrivateField(health, "maxHealth", -20f);
            InvokePrivateMethod(health, "OnValidate");
            health.RestoreFull();

            Assert.That(health.Max, Is.EqualTo(1f));
            Assert.That(health.Current, Is.EqualTo(1f));

            Object.DestroyImmediate(unit);
        }

        [Test]
        public void BasicAttackRejectsAlliesAndAcceptsEnemies()
        {
            GameObject attackerObject = CreateUnit("Attacker", TeamId.Azure);
            BasicAttack attack = attackerObject.AddComponent<BasicAttack>();
            GameObject allyObject = CreateUnit("Ally", TeamId.Azure);
            GameObject enemyObject = CreateUnit("Enemy", TeamId.Ember);

            Assert.That(attack.CanAttack(allyObject.GetComponent<Health>()), Is.False);
            Assert.That(attack.CanAttack(enemyObject.GetComponent<Health>()), Is.True);

            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(allyObject);
            Object.DestroyImmediate(enemyObject);
        }

        [Test]
        public void BasicAttackDamagesEnemyAndDeadAttackerCannotAttack()
        {
            GameObject attackerObject = CreateUnit("Attacker", TeamId.Azure);
            BasicAttack attack = attackerObject.AddComponent<BasicAttack>();
            GameObject enemyObject = CreateUnit("Enemy", TeamId.Ember);
            enemyObject.transform.position = attackerObject.transform.position + Vector3.right;
            Health attackerHealth = attackerObject.GetComponent<Health>();
            Health enemyHealth = enemyObject.GetComponent<Health>();

            Assert.That(attack.TryAttack(enemyHealth), Is.True);
            Assert.That(enemyHealth.Current, Is.LessThan(enemyHealth.Max));
            Assert.That(attack.TryAttack(enemyHealth), Is.False);

            attackerHealth.ApplyDamage(attackerHealth.Max);
            Assert.That(attack.CanAttack(enemyHealth), Is.False);

            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(enemyObject);
        }

        [Test]
        public void AttackValidationProtectsAgainstInvalidCadenceAndRange()
        {
            GameObject attackerObject = CreateUnit("Attacker", TeamId.Azure);
            BasicAttack attack = attackerObject.AddComponent<BasicAttack>();
            SetPrivateField(attack, "range", -1f);
            SetPrivateField(attack, "attacksPerSecond", 0f);
            InvokePrivateMethod(attack, "OnValidate");

            Assert.That(attack.Range, Is.Zero);
            Assert.That((float)GetPrivateField(attack, "attacksPerSecond"), Is.GreaterThan(0f));

            Object.DestroyImmediate(attackerObject);
        }

        [Test]
        public void MoveAndStopOrdersClearAnActiveAttackOrder()
        {
            GameObject ground = CreateGround();
            GameObject attackerObject = CreateOrderUnit("Attacker", TeamId.Azure);
            GameObject enemyObject = CreateUnit("Enemy", TeamId.Ember);
            UnitOrderController orders = attackerObject.GetComponent<UnitOrderController>();
            Health enemyHealth = enemyObject.GetComponent<Health>();

            Assert.That(orders.IssueAttack(enemyHealth), Is.True);
            Assert.That(GetPrivateField(orders, "attackTarget"), Is.EqualTo(enemyHealth));

            Assert.That(orders.IssueMove(Vector3.right * 4f), Is.True);
            Assert.That(GetPrivateField(orders, "attackTarget"), Is.Null);

            Assert.That(orders.IssueAttack(enemyHealth), Is.True);
            orders.Stop();
            Assert.That(GetPrivateField(orders, "attackTarget"), Is.Null);

            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(enemyObject);
        }

        [Test]
        public void DeadUnitRejectsOrdersAndDeadTargetClearsAttackOrder()
        {
            GameObject ground = CreateGround();
            GameObject attackerObject = CreateOrderUnit("Attacker", TeamId.Azure);
            GameObject enemyObject = CreateUnit("Enemy", TeamId.Ember);
            UnitOrderController orders = attackerObject.GetComponent<UnitOrderController>();
            Health attackerHealth = attackerObject.GetComponent<Health>();
            Health enemyHealth = enemyObject.GetComponent<Health>();

            Assert.That(orders.IssueAttack(enemyHealth), Is.True);
            enemyHealth.ApplyDamage(enemyHealth.Max);
            InvokePrivateMethod(orders, "Update");
            Assert.That(GetPrivateField(orders, "attackTarget"), Is.Null);

            attackerHealth.ApplyDamage(attackerHealth.Max);
            Assert.That(orders.IssueMove(Vector3.right), Is.False);
            Assert.That(orders.IssueAttack(enemyHealth), Is.False);

            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(enemyObject);
        }

        private static GameObject CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = 6;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            return ground;
        }

        private static GameObject CreateOrderUnit(string name, TeamId team)
        {
            GameObject unit = CreateUnit(name, team);
            unit.transform.position = new Vector3(0f, 1f, 0f);
            unit.AddComponent<ClickMover>();
            unit.AddComponent<BasicAttack>();
            unit.AddComponent<UnitOrderController>();
            return unit;
        }

        private static GameObject CreateUnit(string name, TeamId team)
        {
            GameObject unit = new GameObject(name);
            TeamMember teamMember = unit.AddComponent<TeamMember>();
            SetPrivateField(teamMember, "team", team);
            unit.AddComponent<Health>();
            return unit;
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName, InstancePrivate).GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, InstancePrivate).SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, InstancePrivate).Invoke(target, null);
        }
    }
}

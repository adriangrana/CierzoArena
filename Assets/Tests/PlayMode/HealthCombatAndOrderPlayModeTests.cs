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
    public sealed class HealthCombatAndOrderPlayModeTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator HealthDamageClampsAtZeroAndRaisesDeathOnce()
        {
            GameObject unit = CreateUnit("Health Unit", TeamId.Azure, Vector3.zero);
            Health health = unit.GetComponent<Health>();
            yield return null;

            int deathCount = 0;
            health.Died += _ => deathCount++;

            health.ApplyDamage(health.Max + 50f);
            health.ApplyDamage(1f);

            Assert.That(health.Current, Is.Zero);
            Assert.That(health.IsAlive, Is.False);
            Assert.That(deathCount, Is.EqualTo(1));

            Object.Destroy(unit);
        }

        [UnityTest]
        public IEnumerator HealthRestoresToMaximumAndRejectsInvalidDamage()
        {
            GameObject unit = CreateUnit("Health Unit", TeamId.Azure, Vector3.zero);
            Health health = unit.GetComponent<Health>();
            yield return null;

            health.ApplyDamage(50f);
            health.ApplyDamage(0f);
            health.ApplyDamage(-10f);
            Assert.That(health.Current, Is.EqualTo(health.Max - 50f));

            health.RestoreFull();
            Assert.That(health.Current, Is.EqualTo(health.Max));
            Assert.That(health.IsAlive, Is.True);

            Object.Destroy(unit);
        }

        [UnityTest]
        public IEnumerator BasicAttackRejectsAlliesAndAcceptsEnemies()
        {
            GameObject attackerObject = CreateUnit("Attacker", TeamId.Azure, Vector3.zero);
            BasicAttack attack = attackerObject.AddComponent<BasicAttack>();
            GameObject allyObject = CreateUnit("Ally", TeamId.Azure, Vector3.zero);
            GameObject enemyObject = CreateUnit("Enemy", TeamId.Ember, Vector3.zero);
            yield return null;

            Assert.That(attack.CanAttack(allyObject.GetComponent<Health>()), Is.False);
            Assert.That(attack.CanAttack(enemyObject.GetComponent<Health>()), Is.True);

            Object.Destroy(attackerObject);
            Object.Destroy(allyObject);
            Object.Destroy(enemyObject);
        }

        [UnityTest]
        public IEnumerator BasicAttackDamagesEnemyAndDeadAttackerCannotAttack()
        {
            GameObject attackerObject = CreateUnit("Attacker", TeamId.Azure, Vector3.zero);
            BasicAttack attack = attackerObject.AddComponent<BasicAttack>();
            GameObject enemyObject = CreateUnit("Enemy", TeamId.Ember, Vector3.right);
            Health attackerHealth = attackerObject.GetComponent<Health>();
            Health enemyHealth = enemyObject.GetComponent<Health>();
            yield return null;

            Assert.That(attack.TryAttack(enemyHealth), Is.True);
            Assert.That(enemyHealth.Current, Is.LessThan(enemyHealth.Max));
            Assert.That(attack.TryAttack(enemyHealth), Is.False);

            attackerHealth.ApplyDamage(attackerHealth.Max);
            Assert.That(attack.CanAttack(enemyHealth), Is.False);

            Object.Destroy(attackerObject);
            Object.Destroy(enemyObject);
        }

        [UnityTest]
        public IEnumerator MoveAndStopOrdersClearAnActiveAttackOrder()
        {
            GameObject ground = CreateGround();
            GameObject attackerObject = CreateOrderUnit("Attacker", TeamId.Azure, new Vector3(0f, 1f, 0f));
            GameObject enemyObject = CreateUnit("Enemy", TeamId.Ember, Vector3.zero);
            UnitOrderController orders = attackerObject.GetComponent<UnitOrderController>();
            Health enemyHealth = enemyObject.GetComponent<Health>();
            yield return null;

            Assert.That(orders.IssueAttack(enemyHealth), Is.True);
            Assert.That(GetAttackTarget(orders), Is.EqualTo(enemyHealth));

            Assert.That(orders.IssueMove(Vector3.right * 4f), Is.True);
            Assert.That(GetAttackTarget(orders), Is.Null);

            Assert.That(orders.IssueAttack(enemyHealth), Is.True);
            orders.Stop();
            Assert.That(GetAttackTarget(orders), Is.Null);

            Object.Destroy(ground);
            Object.Destroy(attackerObject);
            Object.Destroy(enemyObject);
        }

        [UnityTest]
        public IEnumerator DeadUnitRejectsOrdersAndDeadTargetClearsAttackOrder()
        {
            GameObject ground = CreateGround();
            GameObject attackerObject = CreateOrderUnit("Attacker", TeamId.Azure, new Vector3(0f, 1f, 0f));
            GameObject enemyObject = CreateUnit("Enemy", TeamId.Ember, Vector3.zero);
            UnitOrderController orders = attackerObject.GetComponent<UnitOrderController>();
            Health attackerHealth = attackerObject.GetComponent<Health>();
            Health enemyHealth = enemyObject.GetComponent<Health>();
            yield return null;

            Assert.That(orders.IssueAttack(enemyHealth), Is.True);
            enemyHealth.ApplyDamage(enemyHealth.Max);
            yield return null;
            Assert.That(GetAttackTarget(orders), Is.Null);

            attackerHealth.ApplyDamage(attackerHealth.Max);
            Assert.That(orders.IssueMove(Vector3.right), Is.False);
            Assert.That(orders.IssueAttack(enemyHealth), Is.False);

            Object.Destroy(ground);
            Object.Destroy(attackerObject);
            Object.Destroy(enemyObject);
        }

        private static GameObject CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = 6;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            return ground;
        }

        private static GameObject CreateOrderUnit(string name, TeamId team, Vector3 position)
        {
            GameObject unit = CreateUnit(name, team, position);
            unit.AddComponent<ClickMover>();
            unit.AddComponent<BasicAttack>();
            unit.AddComponent<UnitOrderController>();
            return unit;
        }

        private static GameObject CreateUnit(string name, TeamId team, Vector3 position)
        {
            GameObject unit = new GameObject(name);
            unit.transform.position = position;
            TeamMember teamMember = unit.AddComponent<TeamMember>();
            teamMember.GetType().GetField("team", InstancePrivate).SetValue(teamMember, team);
            unit.AddComponent<Health>();
            return unit;
        }

        private static Health GetAttackTarget(UnitOrderController orders)
        {
            return (Health)typeof(UnitOrderController)
                .GetField("attackTarget", InstancePrivate)
                .GetValue(orders);
        }
    }
}

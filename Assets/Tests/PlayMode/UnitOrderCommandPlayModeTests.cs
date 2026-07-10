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
    /// <summary>
    /// M2.4 coverage: the explicit order boundary (UnitOrderCommand +
    /// UnitOrderController.Execute) can be driven without PlayerCommandController or
    /// the Input System, validates orders in a single place, and preserves the
    /// active-order transitions of the existing behaviour.
    /// </summary>
    public sealed class UnitOrderCommandPlayModeTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator BoundaryAcceptsMoveAttackAndStopWithoutInputSystem()
        {
            GameObject ground = CreateGround();
            GameObject attacker = CreateOrderUnit("Attacker", TeamId.Azure, new Vector3(0f, 1f, 0f));
            GameObject enemy = CreateUnit("Enemy", TeamId.Ember, Vector3.zero);
            UnitOrderController orders = attacker.GetComponent<UnitOrderController>();
            Health enemyHealth = enemy.GetComponent<Health>();
            yield return null;

            Assert.That(orders.Execute(UnitOrderCommand.Move(Vector3.right * 3f)), Is.True);
            Assert.That(orders.Execute(UnitOrderCommand.Attack(enemyHealth)), Is.True);
            Assert.That(GetAttackTarget(orders), Is.EqualTo(enemyHealth));
            Assert.That(orders.Execute(UnitOrderCommand.Stop()), Is.True);
            Assert.That(GetAttackTarget(orders), Is.Null);

            Object.Destroy(ground);
            Object.Destroy(attacker);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator InvalidOrdersAreRejectedWithoutMutatingActiveOrder()
        {
            GameObject ground = CreateGround();
            GameObject attacker = CreateOrderUnit("Attacker", TeamId.Azure, new Vector3(0f, 1f, 0f));
            GameObject ally = CreateUnit("Ally", TeamId.Azure, Vector3.zero);
            GameObject enemy = CreateUnit("Enemy", TeamId.Ember, Vector3.zero);
            UnitOrderController orders = attacker.GetComponent<UnitOrderController>();
            Health attackerHealth = attacker.GetComponent<Health>();
            Health allyHealth = ally.GetComponent<Health>();
            Health enemyHealth = enemy.GetComponent<Health>();
            yield return null;

            // Establish a valid active attack order first.
            Assert.That(orders.Execute(UnitOrderCommand.Attack(enemyHealth)), Is.True);
            Assert.That(GetAttackTarget(orders), Is.EqualTo(enemyHealth));

            // Attacking an ally is rejected and must not overwrite the active order.
            Assert.That(orders.Execute(UnitOrderCommand.Attack(allyHealth)), Is.False);
            Assert.That(GetAttackTarget(orders), Is.EqualTo(enemyHealth));

            // Attacking a dead target is rejected and must not overwrite the active order.
            Health deadTarget = CreateUnit("DeadEnemy", TeamId.Ember, Vector3.zero).GetComponent<Health>();
            deadTarget.ApplyDamage(deadTarget.Max);
            Assert.That(orders.Execute(UnitOrderCommand.Attack(deadTarget)), Is.False);
            Assert.That(GetAttackTarget(orders), Is.EqualTo(enemyHealth));

            // A dead unit rejects every order type.
            attackerHealth.ApplyDamage(attackerHealth.Max);
            Assert.That(orders.Execute(UnitOrderCommand.Move(Vector3.right)), Is.False);
            Assert.That(orders.Execute(UnitOrderCommand.Attack(enemyHealth)), Is.False);

            Object.Destroy(ground);
            Object.Destroy(attacker);
            Object.Destroy(ally);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator MoveCommandCancelsActiveAttackOrder()
        {
            GameObject ground = CreateGround();
            GameObject attacker = CreateOrderUnit("Attacker", TeamId.Azure, new Vector3(0f, 1f, 0f));
            GameObject enemy = CreateUnit("Enemy", TeamId.Ember, Vector3.zero);
            UnitOrderController orders = attacker.GetComponent<UnitOrderController>();
            Health enemyHealth = enemy.GetComponent<Health>();
            yield return null;

            Assert.That(orders.Execute(UnitOrderCommand.Attack(enemyHealth)), Is.True);
            Assert.That(GetAttackTarget(orders), Is.EqualTo(enemyHealth));

            Assert.That(orders.Execute(UnitOrderCommand.Move(Vector3.right * 4f)), Is.True);
            Assert.That(GetAttackTarget(orders), Is.Null);

            Object.Destroy(ground);
            Object.Destroy(attacker);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator StopIsAcceptedWhileAliveAndRejectedWhenDead()
        {
            GameObject ground = CreateGround();
            GameObject attacker = CreateOrderUnit("Attacker", TeamId.Azure, new Vector3(0f, 1f, 0f));
            UnitOrderController orders = attacker.GetComponent<UnitOrderController>();
            Health attackerHealth = attacker.GetComponent<Health>();
            yield return null;

            // Alive: Stop is accepted.
            Assert.That(orders.Execute(UnitOrderCommand.Stop()), Is.True);

            // Dead: Stop is rejected like any other external order.
            attackerHealth.ApplyDamage(attackerHealth.Max);
            Assert.That(orders.Execute(UnitOrderCommand.Stop()), Is.False);

            Object.Destroy(ground);
            Object.Destroy(attacker);
        }

        [UnityTest]
        public IEnumerator DeathClearsActiveAttackAndMovement()
        {
            GameObject ground = CreateGround();
            GameObject attacker = CreateOrderUnit("Attacker", TeamId.Azure, new Vector3(0f, 1f, 0f));
            GameObject enemy = CreateUnit("Enemy", TeamId.Ember, Vector3.zero);
            UnitOrderController orders = attacker.GetComponent<UnitOrderController>();
            Health attackerHealth = attacker.GetComponent<Health>();
            Health enemyHealth = enemy.GetComponent<Health>();
            UnityEngine.AI.NavMeshAgent agent = attacker.GetComponent<UnityEngine.AI.NavMeshAgent>();
            yield return null;

            Assert.That(orders.Execute(UnitOrderCommand.Attack(enemyHealth)), Is.True);
            Assert.That(GetAttackTarget(orders), Is.EqualTo(enemyHealth));

            // Death cleanup happens through an internal path, not a public order.
            attackerHealth.ApplyDamage(attackerHealth.Max);
            Assert.That(GetAttackTarget(orders), Is.Null);
            if (agent != null && agent.isOnNavMesh)
            {
                Assert.That(agent.isStopped, Is.True);
            }

            Object.Destroy(ground);
            Object.Destroy(attacker);
            Object.Destroy(enemy);
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

using System.Collections;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    public sealed class OrderAndCombatSmokeTests
    {
        [UnityTest]
        public IEnumerator AttackOrderUsesNavMeshAndCancelsWhenMoved()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = 6;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            GameObject azure = CreateOrderUnit("Azure", TeamId.Azure, new Vector3(0f, 1f, 0f));
            GameObject ember = CreateUnit("Ember", TeamId.Ember, new Vector3(1f, 1f, 0f));
            UnitOrderController orders = azure.GetComponent<UnitOrderController>();
            Health emberHealth = ember.GetComponent<Health>();

            yield return null;

            NavMeshAgent agent = azure.GetComponent<NavMeshAgent>();
            Assert.That(agent, Is.Not.Null);
            Assert.That(agent.isOnNavMesh, Is.True);
            Assert.That(orders.IssueAttack(emberHealth), Is.True);

            yield return null;

            Assert.That(emberHealth.Current, Is.LessThan(emberHealth.Max));
            Assert.That(orders.IssueMove(new Vector3(3f, 1f, 0f)), Is.True);
            Assert.That(GetAttackTarget(orders), Is.Null);

            Object.Destroy(ground);
            Object.Destroy(azure);
            Object.Destroy(ember);
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
            typeof(TeamMember).GetField("team", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(teamMember, team);
            unit.AddComponent<Health>();
            return unit;
        }

        private static Health GetAttackTarget(UnitOrderController orders)
        {
            return (Health)typeof(UnitOrderController)
                .GetField("attackTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(orders);
        }
    }
}

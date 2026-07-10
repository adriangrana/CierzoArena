using System.Collections;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Netcode;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Netcode.Tests
{
    /// <summary>
    /// M2.5 bridge/adapter coverage. These tests exercise the authoritative
    /// translation layer (<see cref="AuthoritativeOrderProcessor"/>) in isolation,
    /// without spinning up a NetworkManager or simulated clients. They verify the
    /// authority contract: sender ownership, target resolution and that final
    /// validation is still delegated to the existing domain boundary
    /// (<see cref="UnitOrderController"/>). Two-instance connectivity is validated
    /// manually, not here.
    /// </summary>
    public sealed class AuthoritativeOrderProcessorPlayModeTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const ulong OwnerClientId = 1;
        private const ulong ForeignClientId = 2;

        [UnityTest]
        public IEnumerator ValidMoveRequestFromOwnerBecomesDomainMoveOrder()
        {
            GameObject ground = CreateGround();
            GameObject unit = CreateOrderUnit("Unit", TeamId.Azure, new Vector3(0f, 1f, 0f));
            GameObject enemy = CreateUnit("Enemy", TeamId.Ember, Vector3.zero);
            UnitOrderController orders = unit.GetComponent<UnitOrderController>();
            AuthoritativeOrderProcessor processor = new AuthoritativeOrderProcessor(orders, OwnerClientId);
            yield return null;

            // Establish an active attack order, then a Move from the owner must be
            // accepted and translated into a domain Move that cancels the attack.
            Assert.That(processor.ProcessAttack(OwnerClientId, enemy.GetComponent<Health>()),
                Is.EqualTo(OrderRequestResult.Accepted));
            Assert.That(GetAttackTarget(orders), Is.EqualTo(enemy.GetComponent<Health>()));

            Assert.That(processor.ProcessMove(OwnerClientId, Vector3.right * 3f),
                Is.EqualTo(OrderRequestResult.Accepted));
            Assert.That(GetAttackTarget(orders), Is.Null);

            Object.Destroy(ground);
            Object.Destroy(unit);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator RequestFromForeignClientIsRejectedWithoutTouchingDomain()
        {
            GameObject ground = CreateGround();
            GameObject unit = CreateOrderUnit("Unit", TeamId.Azure, new Vector3(0f, 1f, 0f));
            GameObject enemy = CreateUnit("Enemy", TeamId.Ember, Vector3.zero);
            UnitOrderController orders = unit.GetComponent<UnitOrderController>();
            Health enemyHealth = enemy.GetComponent<Health>();
            AuthoritativeOrderProcessor processor = new AuthoritativeOrderProcessor(orders, OwnerClientId);
            yield return null;

            // A non-owning client cannot command this unit for any order type, and the
            // domain order state must stay untouched.
            Assert.That(processor.ProcessMove(ForeignClientId, Vector3.right),
                Is.EqualTo(OrderRequestResult.RejectedUnauthorizedSender));
            Assert.That(processor.ProcessAttack(ForeignClientId, enemyHealth),
                Is.EqualTo(OrderRequestResult.RejectedUnauthorizedSender));
            Assert.That(processor.ProcessStop(ForeignClientId),
                Is.EqualTo(OrderRequestResult.RejectedUnauthorizedSender));
            Assert.That(GetAttackTarget(orders), Is.Null);

            Object.Destroy(ground);
            Object.Destroy(unit);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator UnresolvedAttackTargetIsRejected()
        {
            GameObject ground = CreateGround();
            GameObject unit = CreateOrderUnit("Unit", TeamId.Azure, new Vector3(0f, 1f, 0f));
            UnitOrderController orders = unit.GetComponent<UnitOrderController>();
            AuthoritativeOrderProcessor processor = new AuthoritativeOrderProcessor(orders, OwnerClientId);
            yield return null;

            // The network layer failed to resolve the referenced NetworkObject into a
            // Health target; the authority rejects it instead of guessing.
            Assert.That(processor.ProcessAttack(OwnerClientId, null),
                Is.EqualTo(OrderRequestResult.RejectedUnresolvedTarget));
            Assert.That(GetAttackTarget(orders), Is.Null);

            Object.Destroy(ground);
            Object.Destroy(unit);
        }

        [UnityTest]
        public IEnumerator AttackStillPassesThroughDomainValidation()
        {
            GameObject ground = CreateGround();
            GameObject unit = CreateOrderUnit("Unit", TeamId.Azure, new Vector3(0f, 1f, 0f));
            GameObject ally = CreateUnit("Ally", TeamId.Azure, Vector3.zero);
            GameObject enemy = CreateUnit("Enemy", TeamId.Ember, Vector3.zero);
            UnitOrderController orders = unit.GetComponent<UnitOrderController>();
            AuthoritativeOrderProcessor processor = new AuthoritativeOrderProcessor(orders, OwnerClientId);
            yield return null;

            // Authorized sender, but the domain boundary still forbids attacking an
            // ally: the request reaches Execute/CanAccept and is rejected there.
            Assert.That(processor.ProcessAttack(OwnerClientId, ally.GetComponent<Health>()),
                Is.EqualTo(OrderRequestResult.RejectedByDomain));
            Assert.That(GetAttackTarget(orders), Is.Null);

            // A valid enemy is accepted through the same boundary.
            Assert.That(processor.ProcessAttack(OwnerClientId, enemy.GetComponent<Health>()),
                Is.EqualTo(OrderRequestResult.Accepted));
            Assert.That(GetAttackTarget(orders), Is.EqualTo(enemy.GetComponent<Health>()));

            Object.Destroy(ground);
            Object.Destroy(unit);
            Object.Destroy(ally);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator DeadUnitRejectsExternalStopThroughDomain()
        {
            GameObject ground = CreateGround();
            GameObject unit = CreateOrderUnit("Unit", TeamId.Azure, new Vector3(0f, 1f, 0f));
            UnitOrderController orders = unit.GetComponent<UnitOrderController>();
            Health unitHealth = unit.GetComponent<Health>();
            AuthoritativeOrderProcessor processor = new AuthoritativeOrderProcessor(orders, OwnerClientId);
            yield return null;

            Assert.That(processor.ProcessStop(OwnerClientId), Is.EqualTo(OrderRequestResult.Accepted));

            // M2.4 invariant preserved end-to-end: a dead unit rejects external Stop.
            unitHealth.ApplyDamage(unitHealth.Max);
            Assert.That(processor.ProcessStop(OwnerClientId), Is.EqualTo(OrderRequestResult.RejectedByDomain));

            Object.Destroy(ground);
            Object.Destroy(unit);
        }

        [UnityTest]
        public IEnumerator ReassignedOwnershipUpdatesAuthorizedSender()
        {
            GameObject ground = CreateGround();
            GameObject unit = CreateOrderUnit("Unit", TeamId.Azure, new Vector3(0f, 1f, 0f));
            UnitOrderController orders = unit.GetComponent<UnitOrderController>();
            AuthoritativeOrderProcessor processor = new AuthoritativeOrderProcessor(orders, OwnerClientId);
            yield return null;

            Assert.That(processor.ProcessStop(ForeignClientId),
                Is.EqualTo(OrderRequestResult.RejectedUnauthorizedSender));

            // Server reassigns ownership; the previously foreign client is now allowed.
            processor.SetOwningClient(ForeignClientId);
            Assert.That(processor.ProcessStop(ForeignClientId), Is.EqualTo(OrderRequestResult.Accepted));
            Assert.That(processor.ProcessStop(OwnerClientId),
                Is.EqualTo(OrderRequestResult.RejectedUnauthorizedSender));

            Object.Destroy(ground);
            Object.Destroy(unit);
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

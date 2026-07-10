using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class HealthCombatAndOrderTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

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
        public void AuthoritativeStateAppliesExactClampedValue()
        {
            GameObject unit = CreateUnit("Authoritative Unit", TeamId.Azure);
            Health health = unit.GetComponent<Health>();

            // Exact state assignment (not a damage event): within range keeps the value,
            // above max clamps to max, below zero clamps to zero.
            health.ApplyAuthoritativeState(120f);
            Assert.That(health.Current, Is.EqualTo(120f));

            health.ApplyAuthoritativeState(health.Max + 999f);
            Assert.That(health.Current, Is.EqualTo(health.Max));

            health.ApplyAuthoritativeState(-50f);
            Assert.That(health.Current, Is.EqualTo(0f));
            Assert.That(health.IsAlive, Is.False);

            Object.DestroyImmediate(unit);
        }

        [Test]
        public void RepeatedDeadAuthoritativeStateRaisesDiedOnce()
        {
            GameObject unit = CreateUnit("Authoritative Dead Unit", TeamId.Azure);
            Health health = unit.GetComponent<Health>();

            // Make the unit alive first, then subscribe so we only count transitions.
            health.ApplyAuthoritativeState(100f);
            int diedCount = 0;
            health.Died += _ => diedCount++;

            // First zero syncs the alive->dead transition; further dead syncs (e.g. late
            // or duplicated state updates) must not raise Died again.
            health.ApplyAuthoritativeState(0f);
            health.ApplyAuthoritativeState(0f);

            Assert.That(diedCount, Is.EqualTo(1));

            Object.DestroyImmediate(unit);
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

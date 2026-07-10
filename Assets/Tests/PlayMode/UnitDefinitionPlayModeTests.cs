using System.Collections;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    /// <summary>
    /// M2.3 coverage: reusable UnitDefinition configuration is separated from mutable
    /// per-match state. Two distinct definitions must drive distinct runtime values
    /// through the same components, and mutable runtime state must never be written
    /// back into the shared ScriptableObject.
    /// </summary>
    public sealed class UnitDefinitionPlayModeTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator DistinctDefinitionsProduceDistinctRuntimeConfig()
        {
            UnitDefinition azure = CreateDefinition(maxHealth: 520f, movementSpeed: 5.5f, attackDamage: 48f, attackRange: 2.2f, attacksPerSecond: 0.8f);
            UnitDefinition ember = CreateDefinition(maxHealth: 180f, movementSpeed: 4.2f, attackDamage: 30f, attackRange: 1.8f, attacksPerSecond: 0.5f);

            GameObject azureUnit = CreateConfiguredUnit("Azure", azure);
            GameObject emberUnit = CreateConfiguredUnit("Ember", ember);
            yield return null;

            Health azureHealth = azureUnit.GetComponent<Health>();
            Health emberHealth = emberUnit.GetComponent<Health>();
            BasicAttack azureAttack = azureUnit.GetComponent<BasicAttack>();
            BasicAttack emberAttack = emberUnit.GetComponent<BasicAttack>();

            Assert.That(azureHealth.Max, Is.EqualTo(520f));
            Assert.That(emberHealth.Max, Is.EqualTo(180f));
            Assert.That(azureHealth.Max, Is.Not.EqualTo(emberHealth.Max));

            Assert.That(azureAttack.Range, Is.EqualTo(2.2f));
            Assert.That(emberAttack.Range, Is.EqualTo(1.8f));
            Assert.That(azureAttack.Range, Is.Not.EqualTo(emberAttack.Range));

            Object.Destroy(azureUnit);
            Object.Destroy(emberUnit);
        }

        [UnityTest]
        public IEnumerator MutableStateIsNotWrittenBackIntoDefinition()
        {
            UnitDefinition definition = CreateDefinition(maxHealth: 520f, movementSpeed: 5.5f, attackDamage: 48f, attackRange: 2.2f, attacksPerSecond: 0.8f);
            GameObject unit = CreateConfiguredUnit("Azure", definition);
            yield return null;

            Health health = unit.GetComponent<Health>();
            health.ApplyDamage(120f);

            Assert.That(health.Current, Is.EqualTo(400f));
            Assert.That(health.Max, Is.EqualTo(520f));
            Assert.That(definition.MaxHealth, Is.EqualTo(520f));

            Object.Destroy(unit);
        }

        private static GameObject CreateConfiguredUnit(string name, UnitDefinition definition)
        {
            GameObject unit = new GameObject(name);
            UnitDefinitionProvider provider = unit.AddComponent<UnitDefinitionProvider>();
            SetPrivateField(provider, "definition", definition);
            TeamMember teamMember = unit.AddComponent<TeamMember>();
            SetPrivateField(teamMember, "team", TeamId.Azure);
            unit.AddComponent<Health>();
            unit.AddComponent<BasicAttack>();
            return unit;
        }

        private static UnitDefinition CreateDefinition(float maxHealth, float movementSpeed, float attackDamage, float attackRange, float attacksPerSecond)
        {
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            SetPrivateField(definition, "maxHealth", maxHealth);
            SetPrivateField(definition, "movementSpeed", movementSpeed);
            SetPrivateField(definition, "attackDamage", attackDamage);
            SetPrivateField(definition, "attackRange", attackRange);
            SetPrivateField(definition, "attacksPerSecond", attacksPerSecond);
            return definition;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, InstancePrivate).SetValue(target, value);
        }
    }
}

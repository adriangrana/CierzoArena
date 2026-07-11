using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class StructureAndMatchTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            ClearSingletons();
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletons();
        }

        [Test]
        public void StructureStartsAtMaximumAndEnemyDamageIsClamped()
        {
            GameObject structureObject = CreateStructure("Azure Tower", TeamId.Azure, StructureKind.Tower);
            GameObject enemyObject = CreateUnit("Ember", TeamId.Ember, Vector3.zero);
            StructureEntity structure = structureObject.GetComponent<StructureEntity>();
            Health health = structure.Health;

            Assert.That(health.Current, Is.EqualTo(health.Max));
            Assert.That(structure.TryApplyDamage(enemyObject.GetComponent<TeamMember>(), health.Max + 10f), Is.True);
            Assert.That(health.Current, Is.Zero);
            Assert.That(structure.IsDestroyed, Is.True);

            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(structureObject);
        }

        [Test]
        public void StructureRejectsFriendlyAndPostDestructionDamage()
        {
            GameObject structureObject = CreateStructure("Azure Tower", TeamId.Azure, StructureKind.Tower);
            GameObject friendlyObject = CreateUnit("Azure", TeamId.Azure, Vector3.zero);
            GameObject enemyObject = CreateUnit("Ember", TeamId.Ember, Vector3.zero);
            StructureEntity structure = structureObject.GetComponent<StructureEntity>();
            float initial = structure.Health.Current;

            Assert.That(structure.TryApplyDamage(friendlyObject.GetComponent<TeamMember>(), 10f), Is.False);
            Assert.That(structure.Health.Current, Is.EqualTo(initial));
            structure.TryApplyDamage(enemyObject.GetComponent<TeamMember>(), structure.Health.Max);
            Assert.That(structure.TryApplyDamage(enemyObject.GetComponent<TeamMember>(), 1f), Is.False);

            Object.DestroyImmediate(friendlyObject);
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(structureObject);
        }

        [Test]
        public void CoreNotifiesOnceAndFirstDestroyedCoreFixesWinner()
        {
            GameObject matchObject = new GameObject("Match");
            MatchStateController match = matchObject.AddComponent<MatchStateController>();
            InvokePrivate(match, "Awake");
            GameObject emberCoreObject = CreateStructure("Ember Core", TeamId.Ember, StructureKind.Core);
            GameObject azureCoreObject = CreateStructure("Azure Core", TeamId.Azure, StructureKind.Core);
            GameObject azure = CreateUnit("Azure", TeamId.Azure, Vector3.zero);
            GameObject ember = CreateUnit("Ember", TeamId.Ember, Vector3.zero);
            int destroyed = 0;
            emberCoreObject.GetComponent<StructureEntity>().Destroyed += _ => destroyed++;

            emberCoreObject.GetComponent<StructureEntity>().TryApplyDamage(azure.GetComponent<TeamMember>(), 9999f);
            azureCoreObject.GetComponent<StructureEntity>().TryApplyDamage(ember.GetComponent<TeamMember>(), 9999f);

            Assert.That(destroyed, Is.EqualTo(1));
            Assert.That(match.CurrentState, Is.EqualTo(MatchState.AzureVictory));

            Object.DestroyImmediate(azure);
            Object.DestroyImmediate(ember);
            Object.DestroyImmediate(emberCoreObject);
            Object.DestroyImmediate(azureCoreObject);
            Object.DestroyImmediate(matchObject);
        }

        [Test]
        public void TowerSelectsNearestEnemyAndIgnoresAlliesOrDeadTargets()
        {
            GameObject towerObject = CreateStructure("Azure Tower", TeamId.Azure, StructureKind.Tower);
            TowerController tower = towerObject.AddComponent<TowerController>();
            InvokePrivate(tower, "Awake");
            BasicAttack attack = towerObject.GetComponent<BasicAttack>();
            SetPrivate(attack, "useUnitDefinition", false);
            SetPrivate(attack, "range", 9f);
            InvokePrivate(attack, "OnValidate");
            towerObject.transform.position = Vector3.zero;
            GameObject ally = CreateUnit("Azure Ally", TeamId.Azure, new Vector3(1f, 0f, 0f));
            GameObject deadEnemy = CreateUnit("Dead Ember", TeamId.Ember, new Vector3(2f, 0f, 0f));
            GameObject nearEnemy = CreateUnit("Near Ember", TeamId.Ember, new Vector3(3f, 0f, 0f));
            GameObject farEnemy = CreateUnit("Far Ember", TeamId.Ember, new Vector3(6f, 0f, 0f));
            deadEnemy.GetComponent<Health>().ApplyDamage(9999f);
            Physics.SyncTransforms();

            Assert.That(tower.FindBestTarget(), Is.EqualTo(nearEnemy.GetComponent<Health>()));

            Object.DestroyImmediate(ally);
            Object.DestroyImmediate(deadEnemy);
            Object.DestroyImmediate(nearEnemy);
            Object.DestroyImmediate(farEnemy);
            Object.DestroyImmediate(towerObject);
        }

        [Test]
        public void TowerReleasesOneRangedProjectilePerCadenceAndStopsAfterVictory()
        {
            GameObject matchObject = new GameObject("Match");
            MatchStateController match = matchObject.AddComponent<MatchStateController>();
            InvokePrivate(match, "Awake");
            GameObject towerObject = CreateStructure("Azure Tower", TeamId.Azure, StructureKind.Tower);
            TowerController tower = towerObject.AddComponent<TowerController>();
            InvokePrivate(tower, "Awake");
            GameObject enemy = CreateUnit("Ember", TeamId.Ember, new Vector3(2f, 0f, 0f));
            SetPrivate(tower, "searchInterval", 0.01f);
            BasicAttack attack = towerObject.GetComponent<BasicAttack>();
            SetPrivate(attack, "delivery", AttackDelivery.Ranged);
            SetPrivate(attack, "attackInterval", 1f);
            SetPrivate(attack, "attackPoint", 0.25f);
            SetPrivate(attack, "backswing", 0.25f);
            InvokePrivate(attack, "OnValidate");
            int releases = 0;
            attack.ProjectileReleased += (_, _) => releases++;
            Physics.SyncTransforms();

            Assert.That(tower.Simulate(0f), Is.False);
            Assert.That(tower.Simulate(0.24f), Is.False);
            Assert.That(releases, Is.Zero);
            Assert.That(tower.Simulate(0.02f), Is.True);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(tower.Simulate(5f), Is.False, "A large tick must not release multiple attacks.");

            match.ApplyAuthoritativeState(MatchState.AzureVictory);
            Assert.That(tower.Simulate(5f), Is.False);
            Assert.That(releases, Is.EqualTo(1));

            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(towerObject);
            Object.DestroyImmediate(matchObject);
        }

        [Test]
        public void StructureProgressionUnlocksOuterThenInnerThenGate()
        {
            GameObject progressionObject = new GameObject("Structure Progression");
            StructureProgressionController progression = progressionObject.AddComponent<StructureProgressionController>();
            InvokePrivate(progression, "Awake");
            GameObject attacker = CreateUnit("Azure", TeamId.Azure, Vector3.zero);
            TeamMember attackerTeam = attacker.GetComponent<TeamMember>();
            StructureEntity outer = CreateStructure("Outer", TeamId.Ember, StructureKind.Tower).GetComponent<StructureEntity>();
            StructureEntity inner = CreateStructure("Inner", TeamId.Ember, StructureKind.Tower).GetComponent<StructureEntity>();
            StructureEntity gate = CreateStructure("Gate", TeamId.Ember, StructureKind.Tower).GetComponent<StructureEntity>();
            SetPrivate(outer, "lane", StructureLane.Top);
            SetPrivate(inner, "lane", StructureLane.Top);
            SetPrivate(inner, "tier", StructureTier.Inner);
            SetPrivate(gate, "lane", StructureLane.Top);
            SetPrivate(gate, "tier", StructureTier.Gate);

            progression.Register(outer);
            progression.Register(inner);
            progression.Register(gate);

            Assert.That(inner.TryApplyDamage(attackerTeam, 1f), Is.False);
            outer.TryApplyDamage(attackerTeam, outer.Health.Max);
            Assert.That(inner.TryApplyDamage(attackerTeam, 1f), Is.True);
            Assert.That(gate.TryApplyDamage(attackerTeam, 1f), Is.False);
            inner.TryApplyDamage(attackerTeam, inner.Health.Max);
            Assert.That(gate.TryApplyDamage(attackerTeam, 1f), Is.True);

            Object.DestroyImmediate(outer.gameObject);
            Object.DestroyImmediate(inner.gameObject);
            Object.DestroyImmediate(gate.gameObject);
            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(progressionObject);
        }

        private static GameObject CreateStructure(string name, TeamId team, StructureKind kind)
        {
            GameObject item = new GameObject(name);
            TeamMember member = item.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            Health health = item.AddComponent<Health>();
            InvokePrivate(health, "Awake");
            StructureEntity entity = item.AddComponent<StructureEntity>();
            SetPrivate(entity, "kind", kind);
            InvokePrivate(entity, "Awake");
            return item;
        }

        private static GameObject CreateUnit(string name, TeamId team, Vector3 position)
        {
            GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unit.name = name;
            unit.transform.position = position;
            TeamMember member = unit.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            Health health = unit.AddComponent<Health>();
            InvokePrivate(health, "Awake");
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

        private static void ClearSingletons()
        {
            typeof(MatchStateController)
                .GetField("active", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
            typeof(StructureProgressionController)
                .GetField("active", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
        }
    }
}

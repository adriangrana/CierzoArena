using System.Collections;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    public sealed class StructureAndMatchPlayModeTests
    {
        [UnityTest]
        public IEnumerator TowerAppliesDamageThenStopsWhenMatchEnds()
        {
            GameObject matchObject = new GameObject("Match");
            MatchStateController match = matchObject.AddComponent<MatchStateController>();
            GameObject towerObject = CreateStructure(TeamId.Azure);
            TowerController tower = towerObject.AddComponent<TowerController>();
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.transform.position = new Vector3(2f, 0f, 0f);
            TeamMember enemyTeam = enemy.AddComponent<TeamMember>();
            SetPrivate(enemyTeam, "team", TeamId.Ember);
            Health enemyHealth = enemy.AddComponent<Health>();
            SetPrivate(tower, "damage", 25f);
            SetPrivate(tower, "attackInterval", 0.01f);
            SetPrivate(tower, "searchInterval", 0.01f);
            Physics.SyncTransforms();

            yield return null;
            tower.Simulate(0.1f);
            float afterHit = enemyHealth.Current;
            Assert.That(afterHit, Is.LessThan(enemyHealth.Max));

            match.ApplyAuthoritativeState(MatchState.AzureVictory);
            tower.Simulate(1f);
            Assert.That(enemyHealth.Current, Is.EqualTo(afterHit));

            Object.Destroy(towerObject);
            Object.Destroy(enemy);
            Object.Destroy(matchObject);
        }

        private static GameObject CreateStructure(TeamId team)
        {
            GameObject structureObject = new GameObject("Tower");
            TeamMember member = structureObject.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            structureObject.AddComponent<Health>();
            structureObject.AddComponent<StructureEntity>();
            return structureObject;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}

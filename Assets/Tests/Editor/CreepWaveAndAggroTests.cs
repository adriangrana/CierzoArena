using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class CreepWaveAndAggroTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp() => SetMatchActive(null);

        [TearDown]
        public void TearDown() => SetMatchActive(null);

        [Test]
        public void EmptyRouteIsSafeAndRouteExposesOrderedWaypoints()
        {
            GameObject emptyObject = new GameObject("Empty Route");
            LaneRoute empty = emptyObject.AddComponent<LaneRoute>();
            Assert.That(empty.Count, Is.Zero);
            Assert.That(empty.IsComplete(0), Is.True);

            LaneRoute route = CreateRoute(new Vector3(1f, 0f, 0f), new Vector3(3f, 0f, 0f));
            Assert.That(route.Count, Is.EqualTo(2));
            Assert.That(route.GetWaypoint(0), Is.EqualTo(new Vector3(1f, 0f, 0f)));
            Assert.That(route.GetWaypoint(1), Is.EqualTo(new Vector3(3f, 0f, 0f)));
            Assert.That(route.IsComplete(2), Is.True);

            Object.DestroyImmediate(emptyObject);
            Object.DestroyImmediate(route.gameObject);
        }

        [Test]
        public void EveryLaneDirectionUsesTheOpposingCoreAsItsExplicitFinalObjective()
        {
            GameObject azureCoreObject = CreateCore(TeamId.Azure, new Vector3(-10f, 0f, -10f));
            GameObject emberCoreObject = CreateCore(TeamId.Ember, new Vector3(10f, 0f, 10f));
            StructureEntity azureCore = azureCoreObject.GetComponent<StructureEntity>();
            StructureEntity emberCore = emberCoreObject.GetComponent<StructureEntity>();

            LaneRoute azureTop = CreateRoute(Vector3.zero, Vector3.right);
            LaneRoute azureMid = CreateRoute(Vector3.zero, Vector3.forward);
            LaneRoute azureBottom = CreateRoute(Vector3.zero, Vector3.left);
            LaneRoute emberTop = CreateRoute(Vector3.one, Vector3.zero);
            LaneRoute emberMid = CreateRoute(Vector3.one, Vector3.back);
            LaneRoute emberBottom = CreateRoute(Vector3.one, Vector3.right);
            SetPrivate(azureTop, "finalObjective", emberCore);
            SetPrivate(azureMid, "finalObjective", emberCore);
            SetPrivate(azureBottom, "finalObjective", emberCore);
            SetPrivate(emberTop, "finalObjective", azureCore);
            SetPrivate(emberMid, "finalObjective", azureCore);
            SetPrivate(emberBottom, "finalObjective", azureCore);

            Assert.That(azureTop.FinalObjective, Is.EqualTo(emberCore));
            Assert.That(azureMid.FinalObjective, Is.EqualTo(emberCore));
            Assert.That(azureBottom.FinalObjective, Is.EqualTo(emberCore));
            Assert.That(emberTop.FinalObjective, Is.EqualTo(azureCore));
            Assert.That(emberMid.FinalObjective, Is.EqualTo(azureCore));
            Assert.That(emberBottom.FinalObjective, Is.EqualTo(azureCore));

            Object.DestroyImmediate(azureTop.gameObject);
            Object.DestroyImmediate(azureMid.gameObject);
            Object.DestroyImmediate(azureBottom.gameObject);
            Object.DestroyImmediate(emberTop.gameObject);
            Object.DestroyImmediate(emberMid.gameObject);
            Object.DestroyImmediate(emberBottom.gameObject);
            Object.DestroyImmediate(azureCoreObject);
            Object.DestroyImmediate(emberCoreObject);
        }

        [Test]
        public void CompletedLaneUsesConfiguredEnemyCoreInsteadOfEnteringPermanentIdle()
        {
            GameObject creepObject = CreateCreep(TeamId.Azure, Vector3.zero);
            GameObject coreObject = CreateCore(TeamId.Ember, new Vector3(8f, 0f, 0f));
            LaneRoute route = CreateRoute();
            StructureEntity core = coreObject.GetComponent<StructureEntity>();
            SetPrivate(route, "finalObjective", core);

            CreepController creep = creepObject.GetComponent<CreepController>();
            creep.ConfigureRoute(route);

            Assert.That(creep.IsAtFinalWaypoint, Is.True);
            Assert.That(creep.FinalObjective, Is.EqualTo(core));
            Assert.That(creep.IsCoreVulnerable, Is.True);
            Assert.That(creep.GetNavigationDebugInfo().CurrentState, Is.EqualTo("CoreApproach"));

            Object.DestroyImmediate(route.gameObject);
            Object.DestroyImmediate(coreObject);
            Object.DestroyImmediate(creepObject);
        }

        [Test]
        public void HeroAndCreepUseTheSameCoreTargetabilityRule()
        {
            GameObject coreObject = CreateCore(TeamId.Ember, new Vector3(5f, 0f, 0f));
            GameObject creepObject = CreateCreep(TeamId.Azure, Vector3.zero);
            GameObject heroObject = CreateUnit(TeamId.Azure, Vector3.one, hero: true);
            BasicAttack heroAttack = heroObject.AddComponent<BasicAttack>();
            SetPrivate(heroAttack, "useUnitDefinition", false);
            StructureEntity core = coreObject.GetComponent<StructureEntity>();

            Assert.That(creepObject.GetComponent<BasicAttack>().CanAttack(core.Health), Is.True);
            Assert.That(heroAttack.CanAttack(core.Health), Is.True);

            Object.DestroyImmediate(heroObject);
            Object.DestroyImmediate(creepObject);
            Object.DestroyImmediate(coreObject);
        }

        [Test]
        public void WaveSpawnerHonoursInitialDelayAndNeverCatchesUpMultipleWavesInOneTick()
        {
            LaneRoute route = CreateRoute(Vector3.zero, Vector3.right);
            GameObject source = new GameObject("Wave Source");
            CreepWaveSpawner spawner = source.AddComponent<CreepWaveSpawner>();
            SetPrivate(spawner, "route", route);
            SetPrivate(spawner, "initialDelay", 1f);
            SetPrivate(spawner, "waveInterval", 5f);
            SetPrivate(spawner, "meleeCount", 2);
            SetPrivate(spawner, "rangedCount", 1);
            spawner.SetExternalSpawner(true);
            int requested = 0;
            spawner.CreepRequested += (_, _, _, _) => requested++;

            Assert.That(spawner.Simulate(0.99f), Is.False);
            Assert.That(requested, Is.Zero);
            Assert.That(spawner.Simulate(100f), Is.True);
            Assert.That(requested, Is.EqualTo(3));
            Assert.That(spawner.Simulate(0f), Is.False);
            Assert.That(requested, Is.EqualTo(3));

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(route.gameObject);
        }

        [Test]
        public void VictoryStopsWaveGeneration()
        {
            GameObject matchObject = new GameObject("Match");
            MatchStateController match = matchObject.AddComponent<MatchStateController>();
            InvokePrivate(match, "Awake");
            LaneRoute route = CreateRoute(Vector3.zero, Vector3.right);
            GameObject source = new GameObject("Wave Source");
            CreepWaveSpawner spawner = source.AddComponent<CreepWaveSpawner>();
            SetPrivate(spawner, "route", route);
            spawner.SetExternalSpawner(true);
            int requested = 0;
            spawner.CreepRequested += (_, _, _, _) => requested++;
            match.ApplyAuthoritativeState(MatchState.AzureVictory);

            Assert.That(spawner.Simulate(100f), Is.False);
            Assert.That(requested, Is.Zero);

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(route.gameObject);
            Object.DestroyImmediate(matchObject);
        }

        [Test]
        public void CreepTargetingChoosesNearestEnemyAndKeepsValidCurrentTarget()
        {
            GameObject creepObject = CreateCreep(TeamId.Azure, Vector3.zero);
            CreepController creep = creepObject.GetComponent<CreepController>();
            GameObject ally = CreateUnit(TeamId.Azure, new Vector3(1f, 0f, 0f), hero: false);
            GameObject near = CreateUnit(TeamId.Ember, new Vector3(2f, 0f, 0f), hero: false);
            GameObject far = CreateUnit(TeamId.Ember, new Vector3(4f, 0f, 0f), hero: false);
            Physics.SyncTransforms();

            Assert.That(creep.FindBestTarget(), Is.EqualTo(near.GetComponent<Health>()));
            Assert.That(creep.SetDefensiveAggro(far.GetComponent<Health>(), 2f), Is.True);
            Assert.That(creep.CurrentTarget, Is.EqualTo(far.GetComponent<Health>()));

            Object.DestroyImmediate(creepObject);
            Object.DestroyImmediate(ally);
            Object.DestroyImmediate(near);
            Object.DestroyImmediate(far);
        }

        [Test]
        public void IdleCreepSiegesAttackableTowerButTowerDamageCannotStealExistingAggro()
        {
            GameObject creepObject = CreateCreep(TeamId.Azure, Vector3.zero);
            CreepController creep = creepObject.GetComponent<CreepController>();
            GameObject towerObject = CreateTower(TeamId.Ember);
            towerObject.transform.position = new Vector3(5f, 0f, 0f);
            StructureEntity tower = towerObject.GetComponent<StructureEntity>();

            Assert.That(creep.FindBestTarget(), Is.EqualTo(tower.Health), "A free lane creep must acquire a nearby vulnerable enemy tower.");
            Assert.That(creep.SetDefensiveAggro(tower.Health, 2f), Is.False, "Tower damage must not replace a creep's current unit target.");

            GameObject enemyCreep = CreateUnit(TeamId.Ember, new Vector3(2f, 0f, 0f), hero: false);
            Physics.SyncTransforms();
            Assert.That(creep.FindBestTarget(), Is.EqualTo(enemyCreep.GetComponent<Health>()), "A living enemy unit keeps priority over a tower.");

            Object.DestroyImmediate(enemyCreep);
            Object.DestroyImmediate(towerObject);
            Object.DestroyImmediate(creepObject);
        }

        [Test]
        public void IdleCreepTargetsAnUnlockedEnemyCoreAfterLaneDefendersAreGone()
        {
            GameObject creepObject = CreateCreep(TeamId.Azure, Vector3.zero);
            CreepController creep = creepObject.GetComponent<CreepController>();
            GameObject coreObject = CreateTower(TeamId.Ember);
            coreObject.name = "Ember Core";
            coreObject.transform.position = new Vector3(5f, 0f, 0f);
            StructureEntity core = coreObject.GetComponent<StructureEntity>();
            SetPrivate(core, "kind", StructureKind.Core);

            Assert.That(core.CanReceiveDamageFrom(creepObject.GetComponent<TeamMember>()), Is.True);
            Assert.That(creep.FindBestTarget(), Is.EqualTo(core.Health), "A wave with no mobile enemies or tower must attack the vulnerable enemy core.");

            Object.DestroyImmediate(coreObject);
            Object.DestroyImmediate(creepObject);
        }

        [Test]
        public void ConfirmedHeroDamageTriggersTowerAggroButZeroDamageDoesNot()
        {
            GameObject towerObject = CreateTower(TeamId.Azure);
            TowerController tower = towerObject.GetComponent<TowerController>();
            DefensiveAggroResponder responder = towerObject.AddComponent<DefensiveAggroResponder>();
            InvokePrivate(responder, "Awake");
            responder.enabled = false;
            responder.enabled = true;
            GameObject victim = CreateUnit(TeamId.Azure, new Vector3(2f, 0f, 0f), hero: true);
            GameObject aggressor = CreateUnit(TeamId.Ember, new Vector3(3f, 0f, 0f), hero: true);
            Health victimHealth = victim.GetComponent<Health>();
            TeamMember aggressorTeam = aggressor.GetComponent<TeamMember>();

            victimHealth.ApplyDamage(new DamageContext(aggressorTeam, 0f, AttackDelivery.Melee));
            Assert.That(tower.CurrentTarget, Is.Null);
            victimHealth.ApplyDamage(new DamageContext(aggressorTeam, 10f, AttackDelivery.Melee));
            InvokePrivate(responder, "OnDamageApplied", victimHealth, new DamageContext(aggressorTeam, 10f, AttackDelivery.Melee));
            Assert.That(tower.CurrentTarget, Is.EqualTo(aggressor.GetComponent<Health>()));

            Object.DestroyImmediate(towerObject);
            Object.DestroyImmediate(victim);
            Object.DestroyImmediate(aggressor);
        }

        [Test]
        public void NonHeroDamageDoesNotTriggerDefensiveAggro()
        {
            GameObject towerObject = CreateTower(TeamId.Azure);
            TowerController tower = towerObject.GetComponent<TowerController>();
            DefensiveAggroResponder responder = towerObject.AddComponent<DefensiveAggroResponder>();
            InvokePrivate(responder, "Awake");
            responder.enabled = false;
            responder.enabled = true;
            GameObject victim = CreateUnit(TeamId.Azure, new Vector3(2f, 0f, 0f), hero: true);
            GameObject creepAttacker = CreateUnit(TeamId.Ember, new Vector3(3f, 0f, 0f), hero: false);

            victim.GetComponent<Health>().ApplyDamage(new DamageContext(creepAttacker.GetComponent<TeamMember>(), 10f, AttackDelivery.Melee));
            Assert.That(tower.CurrentTarget, Is.Null);

            Object.DestroyImmediate(towerObject);
            Object.DestroyImmediate(victim);
            Object.DestroyImmediate(creepAttacker);
        }

        private static LaneRoute CreateRoute(params Vector3[] points)
        {
            GameObject root = new GameObject("Route");
            LaneRoute route = root.AddComponent<LaneRoute>();
            Transform[] transforms = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                GameObject waypoint = new GameObject($"Waypoint {i}");
                waypoint.transform.SetParent(root.transform);
                waypoint.transform.position = points[i];
                transforms[i] = waypoint.transform;
            }
            SetPrivate(route, "waypoints", transforms);
            return route;
        }

        private static GameObject CreateCreep(TeamId team, Vector3 position)
        {
            GameObject creep = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            creep.transform.position = position;
            TeamMember member = creep.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            creep.AddComponent<Health>();
            creep.AddComponent<ClickMover>();
            BasicAttack attack = creep.AddComponent<BasicAttack>();
            SetPrivate(attack, "useUnitDefinition", false);
            SetPrivate(attack, "range", 2f);
            CreepController controller = creep.AddComponent<CreepController>();
            SetPrivate(controller, "detectionRange", 8f);
            SetPrivate(controller, "leashRange", 20f);
            InvokePrivate(controller, "Awake");
            return creep;
        }

        private static GameObject CreateTower(TeamId team)
        {
            GameObject tower = new GameObject("Tower");
            TeamMember member = tower.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            tower.AddComponent<Health>();
            tower.AddComponent<StructureEntity>();
            TowerController controller = tower.AddComponent<TowerController>();
            BasicAttack attack = tower.GetComponent<BasicAttack>();
            SetPrivate(attack, "useUnitDefinition", false);
            SetPrivate(attack, "range", 9f);
            InvokePrivate(controller, "Awake");
            return tower;
        }

        private static GameObject CreateCore(TeamId team, Vector3 position)
        {
            GameObject core = new GameObject("Core");
            core.transform.position = position;
            TeamMember member = core.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            core.AddComponent<Health>();
            StructureEntity structure = core.AddComponent<StructureEntity>();
            SetPrivate(structure, "kind", StructureKind.Core);
            SetPrivate(structure, "tier", StructureTier.Core);
            return core;
        }

        private static GameObject CreateUnit(TeamId team, Vector3 position, bool hero)
        {
            GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unit.transform.position = position;
            TeamMember member = unit.AddComponent<TeamMember>();
            SetPrivate(member, "team", team);
            unit.AddComponent<Health>();
            if (hero) unit.AddComponent<HeroUnit>();
            return unit;
        }

        private static void SetPrivate(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        private static void InvokePrivate(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
        private static void InvokePrivate(object target, string method, params object[] arguments) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, arguments);
        private static void SetMatchActive(MatchStateController value) => typeof(MatchStateController).GetField("active", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    }
}

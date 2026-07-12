using System.Reflection;
using CierzoArena.Core;
using CierzoArena.Combat;
using CierzoArena.CameraSystem;
using CierzoArena.Structures;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class VisionTests
    {
        [Test]
        public void TeamsHaveIndependentCircularVision()
        {
            GameObject azure = Create("Azure",TeamId.Azure,Vector3.zero,5f);
            GameObject ember = Create("Ember",TeamId.Ember,new Vector3(20,0,0),5f);
            azure.GetComponent<VisionSource>().EnsureRegistered(); ember.GetComponent<VisionSource>().EnsureRegistered();
            Assert.That(VisionSource.IsVisible(TeamId.Azure,new Vector3(4,0,0)),Is.True);
            Assert.That(VisionSource.IsVisible(TeamId.Azure,new Vector3(8,0,0)),Is.False);
            Assert.That(VisionSource.IsVisible(TeamId.Ember,new Vector3(20,0,0)),Is.True);
            Assert.That(VisionSource.IsVisible(TeamId.Ember,Vector3.zero),Is.False);
            Object.DestroyImmediate(azure);Object.DestroyImmediate(ember);
        }
        [Test]
        public void EnemyMobileDisappearsButKnownStructureRemainsObscuredWithoutHealthBar()
        {
            GameObject azure=Create("Azure",TeamId.Azure,Vector3.zero,5f);
            GameObject enemy=Create("Enemy",TeamId.Ember,new Vector3(20,0,0),0f);enemy.AddComponent<MeshRenderer>();enemy.AddComponent<BoxCollider>();VisionVisibility mobile=enemy.AddComponent<VisionVisibility>();
            GameObject tower=Create("Tower",TeamId.Ember,new Vector3(20,0,2),0f);tower.AddComponent<Health>();tower.AddComponent<MeshRenderer>();tower.AddComponent<BoxCollider>();StructureEntity structure=tower.AddComponent<StructureEntity>();VisionVisibility towerVision=tower.AddComponent<VisionVisibility>();GameObject bar=new GameObject("Health Bar");bar.transform.SetParent(tower.transform);bar.AddComponent<WorldHealthBar>();
            azure.GetComponent<VisionSource>().EnsureRegistered();mobile.ApplyVisibilityForTeam(TeamId.Azure);towerVision.ApplyVisibilityForTeam(TeamId.Azure);
            Assert.That(enemy.GetComponent<Renderer>().enabled,Is.False);Assert.That(tower.GetComponent<Renderer>().enabled,Is.True);Assert.That(towerVision.IsObscured,Is.True);Assert.That(bar.activeSelf,Is.False);
            Object.DestroyImmediate(azure);Object.DestroyImmediate(enemy);Object.DestroyImmediate(tower);
        }
        [Test]
        public void StructureDestroyedInFogUsesKnownAliveStateUntilVisionReturns()
        {
            GameObject azure=Create("Azure",TeamId.Azure,Vector3.zero,5f);
            GameObject tower=Create("Tower",TeamId.Ember,new Vector3(20,0,0),0f);
            Health health=tower.AddComponent<Health>();
            Renderer renderer=tower.AddComponent<MeshRenderer>();
            tower.AddComponent<BoxCollider>();
            StructureEntity structure=tower.AddComponent<StructureEntity>();
            _ = structure.Health;
            Set(structure,"renderersToDisable",new[]{renderer});
            VisionVisibility visibility=tower.AddComponent<VisionVisibility>();
            azure.GetComponent<VisionSource>().EnsureRegistered();
            visibility.ApplyVisibilityForTeam(TeamId.Azure);
            health.ApplyDamage(9999f);
            visibility.ApplyVisibilityForTeam(TeamId.Azure);
            Assert.That(structure.IsDestroyed,Is.True);
            Assert.That(visibility.KnownStructureAlive,Is.True);
            Assert.That(tower.GetComponent<Renderer>().enabled,Is.True);
            azure.transform.position=new Vector3(19,0,0);
            visibility.ApplyVisibilityForTeam(TeamId.Azure);
            Assert.That(visibility.KnownStructureAlive,Is.False);
            Assert.That(tower.GetComponent<Renderer>().enabled,Is.False);
            Object.DestroyImmediate(azure);Object.DestroyImmediate(tower);
        }
        [Test]
        public void MinimapUsesKnownStructuresButNeverUnseenMobileEnemies()
        {
            GameObject azure=Create("Azure",TeamId.Azure,Vector3.zero,5f);GameObject mobile=Create("Mobile",TeamId.Ember,new Vector3(20,0,0),0f);mobile.AddComponent<VisionVisibility>();GameObject tower=Create("Tower",TeamId.Ember,new Vector3(20,0,2),0f);tower.AddComponent<Health>();tower.AddComponent<MeshRenderer>();StructureEntity structure=tower.AddComponent<StructureEntity>();VisionVisibility visibility=tower.AddComponent<VisionVisibility>();
            azure.GetComponent<VisionSource>().EnsureRegistered();visibility.ApplyVisibilityForTeam(TeamId.Azure);
            Assert.That(MinimapFeedback.ShouldRenderSource(TeamId.Azure,mobile.GetComponent<VisionSource>(),out _),Is.False);Assert.That(MinimapFeedback.ShouldRenderSource(TeamId.Azure,tower.GetComponent<VisionSource>(),out bool obscured),Is.True);Assert.That(obscured,Is.True);
            azure.transform.position=new Vector3(19,0,0);visibility.ApplyVisibilityForTeam(TeamId.Azure);Assert.That(MinimapFeedback.ShouldRenderSource(TeamId.Azure,tower.GetComponent<VisionSource>(),out _),Is.True);structure.Health.ApplyDamage(9999f);visibility.ApplyVisibilityForTeam(TeamId.Azure);Assert.That(MinimapFeedback.ShouldRenderSource(TeamId.Azure,tower.GetComponent<VisionSource>(),out _),Is.False);
            Object.DestroyImmediate(azure);Object.DestroyImmediate(mobile);Object.DestroyImmediate(tower);
        }
        [Test]
        public void DeadOrDestroyedSourcesStopGrantingVisionAndSourcesCombine()
        {
            GameObject first=Create("First",TeamId.Azure,Vector3.zero,3f);GameObject second=Create("Second",TeamId.Azure,new Vector3(10,0,0),3f);GameObject hero=Create("Hero",TeamId.Azure,new Vector3(20,0,0),5f);Health heroHealth=hero.AddComponent<Health>();GameObject tower=Create("Tower",TeamId.Azure,new Vector3(30,0,0),5f);tower.AddComponent<Health>();StructureEntity structure=tower.AddComponent<StructureEntity>();
            Assert.That(VisionSource.IsVisible(TeamId.Azure,new Vector3(12,0,0)),Is.True);Assert.That(VisionSource.IsVisible(TeamId.Azure,new Vector3(25,0,0)),Is.True);
            heroHealth.ApplyDamage(9999f);structure.Health.ApplyDamage(9999f);
            Assert.That(VisionSource.IsVisible(TeamId.Azure,new Vector3(25,0,0)),Is.False);Assert.That(VisionSource.IsVisible(TeamId.Azure,new Vector3(30,0,0)),Is.False);
            Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(hero);Object.DestroyImmediate(tower);
        }
        [Test]
        public void TerrainRemainsKnownWhileFogOnlyDarkensIt()
        {
            GameObject overlayObject=new GameObject("Fog");FogOfWarOverlay overlay=overlayObject.AddComponent<FogOfWarOverlay>();Assert.That(overlay.IsTerrainVisible(new Vector3(100,0,100)),Is.True);Object.DestroyImmediate(overlayObject);
        }
        [Test]
        public void MinimapPointMapsToTheCorrespondingWorldGroundPosition()
        {
            Rect map=new Rect(100,200,200,200);
            Assert.That(MinimapFeedback.GuiPointToWorld(new Vector2(100,200),map,86f),Is.EqualTo(new Vector3(-86,0,86)));
            Assert.That(MinimapFeedback.GuiPointToWorld(new Vector2(300,400),map,86f),Is.EqualTo(new Vector3(86,0,-86)));
            Assert.That(MinimapFeedback.GuiPointToWorld(map.center,map,86f),Is.EqualTo(Vector3.zero));
        }
        private static GameObject Create(string name,TeamId team,Vector3 position,float radius)
        {
            GameObject item=new GameObject(name);item.transform.position=position;TeamMember member=item.AddComponent<TeamMember>();member.GetType().GetField("team",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(member,team);VisionSource source=item.AddComponent<VisionSource>();source.GetType().GetField("radius",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(source,radius);source.EnsureRegistered();return item;
        }
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    }
}

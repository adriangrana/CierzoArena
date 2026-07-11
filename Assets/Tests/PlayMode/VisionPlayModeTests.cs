using System.Collections;
using System.Reflection;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    public sealed class VisionPlayModeTests
    {
        [UnityTest]
        public IEnumerator EnemyMobileAppearsOnlyWhenTeamVisionReachesIt()
        {
            GameObject observer=Create("Azure",TeamId.Azure,Vector3.zero,5f);GameObject enemy=Create("Ember",TeamId.Ember,new Vector3(12,0,0),0f);Renderer renderer=enemy.AddComponent<MeshRenderer>();enemy.AddComponent<BoxCollider>();VisionVisibility visibility=enemy.AddComponent<VisionVisibility>();
            yield return null;visibility.ApplyVisibilityForTeam(TeamId.Azure);Assert.That(renderer.enabled,Is.False);
            observer.transform.position=new Vector3(8,0,0);visibility.ApplyVisibilityForTeam(TeamId.Azure);Assert.That(renderer.enabled,Is.True);
            Object.Destroy(observer);Object.Destroy(enemy);
        }
        private static GameObject Create(string name,TeamId team,Vector3 position,float radius){GameObject item=new GameObject(name);item.transform.position=position;TeamMember member=item.AddComponent<TeamMember>();typeof(TeamMember).GetField("team",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(member,team);VisionSource source=item.AddComponent<VisionSource>();typeof(VisionSource).GetField("radius",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(source,radius);source.EnsureRegistered();return item;}
    }
}

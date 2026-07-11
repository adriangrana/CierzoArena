using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Netcode.Tests
{
    public sealed class NetworkVisionAuthorityTests
    {
        [Test]
        public void ServerCombatRejectsHiddenEnemyUntilTeamVisionDiscoversIt()
        {
            GameObject azure=Create("Azure",TeamId.Azure,Vector3.zero,4f);Health attackerHealth=azure.AddComponent<Health>();BasicAttack attack=azure.AddComponent<BasicAttack>();GameObject ember=Create("Ember",TeamId.Ember,new Vector3(12,0,0),0f);Health target=ember.AddComponent<Health>();
            Assert.That(attack.CanAttack(target),Is.False);
            azure.transform.position=new Vector3(9,0,0);Assert.That(attack.CanAttack(target),Is.True);
            Object.DestroyImmediate(azure);Object.DestroyImmediate(ember);
        }
        private static GameObject Create(string name,TeamId team,Vector3 position,float radius){GameObject item=new GameObject(name);item.transform.position=position;TeamMember member=item.AddComponent<TeamMember>();typeof(TeamMember).GetField("team",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(member,team);VisionSource source=item.AddComponent<VisionSource>();typeof(VisionSource).GetField("radius",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(source,radius);source.EnsureRegistered();return item;}
    }
}

using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class StatusEffectTests
    {
        [Test]
        public void ShieldAbsorbsThenOverflowDamagesHealthAndControlExpires()
        {
            GameObject unit = new GameObject("Azure");
            unit.AddComponent<HeroUnit>();
            TeamMember team = unit.AddComponent<TeamMember>(); Set(team,"team",TeamId.Azure);
            Health health = unit.AddComponent<Health>(); StatusEffectController effects = unit.AddComponent<StatusEffectController>();
            effects.Apply(new StatusEffectSpec{Id="shield",Type=StatusEffectType.Shield,Duration=5,Magnitude=20,ClearOnDeath=true});
            health.ApplyDamage(15); Assert.That(health.Current,Is.EqualTo(500)); Assert.That(effects.Shield,Is.EqualTo(5));
            health.ApplyDamage(10); Assert.That(health.Current,Is.EqualTo(495));
            effects.Apply(new StatusEffectSpec{Id="stun",Type=StatusEffectType.Stun,Duration=1,ClearOnDeath=true}); Assert.That(effects.CanMove,Is.False);
            effects.Simulate(1.1f); Assert.That(effects.CanMove,Is.True);
            Object.DestroyImmediate(unit);
        }
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    }
}

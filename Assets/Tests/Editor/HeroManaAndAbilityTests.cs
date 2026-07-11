using System.Collections.Generic;
using System.Reflection;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class HeroManaAndAbilityTests
    {
        private readonly List<Object> created = new();
        [TearDown] public void TearDown() { for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]); }

        [Test]
        public void ManaRegeneratesAndClampsWhileAlive()
        {
            HeroMana mana = CreateHero(Vector3.zero).GetComponent<HeroMana>();
            Assert.That(mana.CurrentMana, Is.EqualTo(250f));
            Assert.That(mana.TrySpend(100f), Is.True);
            mana.Simulate(2f);
            Assert.That(mana.CurrentMana, Is.EqualTo(162f));
            mana.Simulate(999f);
            Assert.That(mana.CurrentMana, Is.EqualTo(mana.MaximumMana));
            Assert.That(mana.TrySpend(999f), Is.False);
        }

        [Test]
        public void LearnedAbilityConsumesManaStartsCooldownAndPreReleaseCancelIsFree()
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>(); created.Add(ability);
            Set(ability, "targeting", AbilityTargeting.UnitTarget); Set(ability, "effect", AbilityEffect.ProjectileDamage); Set(ability, "castPoint", 0f); Set(ability, "range", 8f);
            Set(ability, "manaCosts", new[] { 20f }); Set(ability, "cooldowns", new[] { 4f }); Set(ability, "effectValues", new[] { 1f }); Set(ability, "requiredHeroLevels", new[] { 1 });
            GameObject hero = CreateHero(Vector3.zero); HeroAbilities abilities = hero.GetComponent<HeroAbilities>(); Set(abilities, "abilities", new[] { ability, null, null, null });
            GameObject enemy = CreateHero(new Vector3(2f, 0f, 0f), TeamId.Ember);
            HeroMana mana = hero.GetComponent<HeroMana>();

            Assert.That(abilities.TryUpgrade(0), Is.True);
            Assert.That(abilities.TryStartCast(0, enemy.GetComponent<Health>(), enemy.transform.position), Is.True);
            abilities.Simulate(0f);
            Assert.That(mana.CurrentMana, Is.EqualTo(230f));
            Assert.That(abilities.GetCooldown(0), Is.EqualTo(4f));

            Set(ability, "castPoint", 1f);
            abilities.Simulate(5f);
            Assert.That(abilities.TryStartCast(0, enemy.GetComponent<Health>(), enemy.transform.position), Is.True);
            Assert.That(abilities.CancelBeforeRelease(), Is.True);
            Assert.That(mana.CurrentMana, Is.EqualTo(230f));
            Assert.That(abilities.GetCooldown(0), Is.Zero);
        }

        private GameObject CreateHero(Vector3 position, TeamId team = TeamId.Azure)
        {
            GameObject hero = new GameObject("Hero"); created.Add(hero); hero.transform.position = position;
            TeamMember member = hero.AddComponent<TeamMember>(); Set(member, "team", team);
            hero.AddComponent<HeroUnit>(); hero.AddComponent<Health>(); hero.AddComponent<ClickMover>(); hero.AddComponent<BasicAttack>(); hero.AddComponent<HeroProgression>(); hero.AddComponent<HeroMana>(); hero.AddComponent<HeroAbilities>();
            return hero;
        }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}

using CierzoArena.Core;
using CierzoArena.Combat;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Scene-authored stable match slot. A respawn keeps the same component and id.</summary>
    [RequireComponent(typeof(HeroUnit))]
    [RequireComponent(typeof(TeamMember))]
    public sealed class HeroMatchIdentity : MonoBehaviour
    {
        [SerializeField, Min(0)] private int matchSlot;
        [SerializeField] private string displayName;
        [SerializeField] private string heroDefinitionId;

        public int MatchSlot => Mathf.Max(0, matchSlot);
        public TeamId Team => TryGetComponent(out TeamMember member) ? member.Team : TeamId.Neutral;
        // Team partition keeps the independently authored Azure and Ember slot zero distinct.
        public int HeroId => ((int)Team * 1000) + MatchSlot;
        public string HeroDefinitionId => heroDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"{Team} {MatchSlot + 1}" : displayName;

        public void Configure(int slot, string visibleName = null)
        {
            matchSlot = Mathf.Max(0, slot);
            if (!string.IsNullOrWhiteSpace(visibleName)) displayName = visibleName;
        }

        /// <summary>Configures the existing common hero components from one
        /// definition. Team stays untouched: Azure/Ember is presentation and spawn
        /// ownership, never a hero archetype.</summary>
        public void ConfigureHero(HeroDefinition definition)
        {
            if (definition == null) return;
            heroDefinitionId=definition.HeroId;displayName=definition.DisplayName;
            if(TryGetComponent(out Health health))health.ConfigureHeroBaseHealth(definition.BaseHealth);
            if(TryGetComponent(out HeroMana mana))mana.ConfigureHeroMana(definition.BaseMana,definition.ManaRegen);
            if(TryGetComponent(out BasicAttack attack))attack.ConfigureHeroAttack(definition.BaseDamage,definition.AttackRange,definition.AttackInterval,definition.AttackPoint,definition.Backswing,definition.AttackStyle==HeroAttackStyle.Ranged?AttackDelivery.Ranged:AttackDelivery.Melee,definition.ProjectileSpeed);
            if(TryGetComponent(out ClickMover mover))mover.ConfigureHeroMoveSpeed(definition.MoveSpeed);
            if(TryGetComponent(out HeroProgression progression))progression.ConfigureHeroGrowth(definition.HealthPerLevel,definition.ManaPerLevel,definition.DamagePerLevel,definition.MoveSpeedPerLevel);
            if(TryGetComponent(out HeroAbilities abilities))abilities.ConfigureHeroKit(new[]{definition.GetAbility(0),definition.GetAbility(1),definition.GetAbility(2),definition.GetAbility(3)});
            (GetComponent<HeroSilhouettePresentation>() ?? gameObject.AddComponent<HeroSilhouettePresentation>()).Apply(definition);
            (GetComponent<HeroVisualController>() ?? gameObject.AddComponent<HeroVisualController>()).Apply(definition);
            ApplyPlaceholderVisual(definition.ThemeColor);
            ApplyTeamPresentation(Team);
        }

        public void ApplyTeamPresentation(TeamId team)
        {
            // Team colour is communicated through the health bar, minimap and the
            // selected hero ring. The old solid disk competed with the character
            // model and looked like a second blue pedestal below every hero.
            Transform indicator=transform.Find("Team Indicator");
            if(indicator==null)return;
            Renderer renderer=indicator.GetComponent<Renderer>();
            if(renderer!=null)renderer.enabled=false;
        }

        private void ApplyPlaceholderVisual(Color color)
        {
            // The root mesh is the legacy placeholder. Do not walk visual children:
            // a hero-specific prefab owns shared authored materials and must never be
            // tinted or cloned by this generic gameplay presentation code.
            Renderer renderer=GetComponent<Renderer>();
            if(renderer==null)return;
            Material material=renderer.material;
            if(material.HasProperty("_Color"))material.color=Color.Lerp(material.color,color,.65f);
        }
    }
}

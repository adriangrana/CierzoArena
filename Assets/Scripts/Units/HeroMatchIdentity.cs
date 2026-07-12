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
            ApplyVisual(definition.ThemeColor);
            (GetComponent<HeroSilhouettePresentation>() ?? gameObject.AddComponent<HeroSilhouettePresentation>()).Apply(definition);
            ApplyTeamPresentation(Team);
        }

        public void ApplyTeamPresentation(TeamId team)
        {
            Transform indicator=transform.Find("Team Indicator");
            if(indicator==null){indicator=GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;indicator.name="Team Indicator";indicator.SetParent(transform,false);Collider collider=indicator.GetComponent<Collider>();if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);}
            indicator.localPosition=new Vector3(0,-.62f,0);indicator.localScale=new Vector3(1.05f,.025f,1.05f);
            Renderer renderer=indicator.GetComponent<Renderer>();if(renderer==null)return;Shader shader=Shader.Find("Standard");if(shader==null)return;Material material=renderer.material;material.color=team==TeamId.Ember?new Color(.95f,.22f,.12f):team==TeamId.Azure?new Color(.12f,.68f,1f):Color.gray;
        }

        private void ApplyVisual(Color color)
        {
            foreach(Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.name.Contains("Health")||renderer.name.Contains("Selection"))continue;
                Material material=renderer.material;
                if(material.HasProperty("_Color"))material.color=Color.Lerp(material.color,color,.65f);
            }
        }
    }
}

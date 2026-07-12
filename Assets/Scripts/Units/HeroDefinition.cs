using System.Collections.Generic;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Immutable hero archetype. Mutable match state stays on the existing
    /// Health, HeroMana, HeroProgression, BasicAttack and HeroAbilities components.</summary>
    [CreateAssetMenu(fileName = "HeroDefinition", menuName = "Cierzo Arena/Hero Definition")]
    public sealed class HeroDefinition : ScriptableObject
    {
        [SerializeField] private string heroId = "storm_warden";
        [SerializeField] private string displayName = "Storm Warden";
        [SerializeField] private string epithet = "Keeper of the high current";
        [TextArea, SerializeField] private string description;
        [SerializeField] private HeroRole primaryRole;
        [SerializeField] private HeroRole secondaryRole;
        [SerializeField] private HeroAttackStyle attackStyle;
        [SerializeField, Range(1, 3)] private int difficulty = 1;
        [SerializeField] private Color themeColor = Color.cyan;
        [SerializeField] private Texture2D portrait;
        [SerializeField] private Texture2D smallIcon;
        [SerializeField] private GameObject prefab;
        [SerializeField] private AbilityDefinition[] abilities = new AbilityDefinition[4];
        [SerializeField, Min(1f)] private float baseHealth = 500f;
        [SerializeField, Min(0f)] private float healthPerLevel = 80f;
        [SerializeField, Min(1f)] private float baseMana = 250f;
        [SerializeField, Min(0f)] private float manaPerLevel = 20f;
        [SerializeField, Min(0f)] private float healthRegen = 2f;
        [SerializeField, Min(0f)] private float manaRegen = 6f;
        [SerializeField, Min(0f)] private float baseDamage = 45f;
        [SerializeField, Min(0f)] private float damagePerLevel = 8f;
        [SerializeField, Min(0f)] private float moveSpeed = 5.5f;
        [SerializeField, Min(0f)] private float moveSpeedPerLevel = .2f;
        [SerializeField, Min(0f)] private float attackRange = 2.1f;
        [SerializeField, Min(.01f)] private float attackInterval = 1.25f;
        [SerializeField, Min(0f)] private float attackPoint = .3f;
        [SerializeField, Min(0f)] private float backswing = .35f;
        [SerializeField, Min(.01f)] private float projectileSpeed = 14f;
        [SerializeField] private string[] styleTags = new string[0];

        public string HeroId => heroId;
        public string DisplayName => displayName;
        public string Epithet => epithet;
        public string Description => description;
        public HeroRole PrimaryRole => primaryRole;
        public HeroRole SecondaryRole => secondaryRole;
        public HeroAttackStyle AttackStyle => attackStyle;
        public int Difficulty => Mathf.Clamp(difficulty, 1, 3);
        public Color ThemeColor => themeColor;
        public Texture2D Portrait => portrait;
        public Texture2D SmallIcon => smallIcon;
        public GameObject Prefab => prefab;
        public float BaseHealth => Mathf.Max(1f, baseHealth);
        public float HealthPerLevel => Mathf.Max(0f, healthPerLevel);
        public float BaseMana => Mathf.Max(1f, baseMana);
        public float ManaPerLevel => Mathf.Max(0f, manaPerLevel);
        public float HealthRegen => Mathf.Max(0f, healthRegen);
        public float ManaRegen => Mathf.Max(0f, manaRegen);
        public float BaseDamage => Mathf.Max(0f, baseDamage);
        public float DamagePerLevel => Mathf.Max(0f, damagePerLevel);
        public float MoveSpeed => Mathf.Max(0f, moveSpeed);
        public float MoveSpeedPerLevel => Mathf.Max(0f, moveSpeedPerLevel);
        public float AttackRange => Mathf.Max(0f, attackRange);
        public float AttackInterval => Mathf.Max(.01f, attackInterval);
        public float AttackPoint => Mathf.Max(0f, attackPoint);
        public float Backswing => Mathf.Max(0f, backswing);
        public float ProjectileSpeed => Mathf.Max(.01f, projectileSpeed);
        public IReadOnlyList<AbilityDefinition> Abilities => abilities;
        public IReadOnlyList<string> StyleTags => styleTags;

        public AbilityDefinition GetAbility(int slot) => slot >= 0 && slot < abilities.Length ? abilities[slot] : null;
        public bool IsValid(bool requirePrefab, out string reason)
        {
            if (string.IsNullOrWhiteSpace(heroId)) { reason = "HeroId is empty."; return false; }
            if (requirePrefab && prefab == null) { reason = $"{heroId} has no prefab."; return false; }
            if (abilities == null || abilities.Length != 4) { reason = $"{heroId} must define Q/W/E/R."; return false; }
            for (int i = 0; i < 4; i++) if (abilities[i] == null) { reason = $"{heroId} has an empty ability slot."; return false; }
            reason = string.Empty; return true;
        }

        public void ConfigureRuntime(string id, string name, string title, string text, HeroRole primary, HeroRole secondary, HeroAttackStyle style, int skill, Color color, HeroStats stats, AbilityDefinition[] kit)
        {
            heroId = id; displayName = name; epithet = title; description = text; primaryRole = primary; secondaryRole = secondary; attackStyle = style; difficulty = skill; themeColor = color;
            baseHealth = stats.BaseHealth; healthPerLevel = stats.HealthPerLevel; baseMana = stats.BaseMana; manaPerLevel = stats.ManaPerLevel; healthRegen = stats.HealthRegen; manaRegen = stats.ManaRegen; baseDamage = stats.BaseDamage; damagePerLevel = stats.DamagePerLevel; moveSpeed = stats.MoveSpeed; moveSpeedPerLevel = stats.MoveSpeedPerLevel; attackRange = stats.AttackRange; attackInterval = stats.AttackInterval; attackPoint = stats.AttackPoint; backswing = stats.Backswing; projectileSpeed = stats.ProjectileSpeed; abilities = kit;
        }
        public void SetPrefab(GameObject value) => prefab = value;
        public void SetPresentation(Texture2D large, Texture2D icon) { portrait=large;smallIcon=icon; }
    }

    public readonly struct HeroStats
    {
        public readonly float BaseHealth, HealthPerLevel, BaseMana, ManaPerLevel, HealthRegen, ManaRegen, BaseDamage, DamagePerLevel, MoveSpeed, MoveSpeedPerLevel, AttackRange, AttackInterval, AttackPoint, Backswing, ProjectileSpeed;
        public HeroStats(float hp,float hpGrowth,float mana,float manaGrowth,float hpRegen,float manaRegen,float damage,float damageGrowth,float move,float moveGrowth,float range,float interval,float point,float recovery,float projectile)
        { BaseHealth=hp;HealthPerLevel=hpGrowth;BaseMana=mana;ManaPerLevel=manaGrowth;HealthRegen=hpRegen;ManaRegen=manaRegen;BaseDamage=damage;DamagePerLevel=damageGrowth;MoveSpeed=move;MoveSpeedPerLevel=moveGrowth;AttackRange=range;AttackInterval=interval;AttackPoint=point;Backswing=recovery;ProjectileSpeed=projectile; }
    }
}

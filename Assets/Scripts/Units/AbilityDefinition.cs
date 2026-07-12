using UnityEngine;

namespace CierzoArena.Units
{
    public enum AbilityTargeting { NoTarget, UnitTarget, PointTarget }
    public enum AbilityEffect { ProjectileDamage, AreaDamage, SelfMoveSpeed, StrongAreaDamage, AreaSlow, StrongAreaStun, SelfShield }

    [CreateAssetMenu(fileName = "AbilityDefinition", menuName = "Cierzo Arena/Ability Definition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [SerializeField] private string abilityId = "hero.ability";
        [SerializeField] private string displayName = "Ability";
        [TextArea, SerializeField] private string description;
        [SerializeField, Range(1, 4)] private int maximumLevel = 4;
        [SerializeField] private int[] requiredHeroLevels = { 1, 1, 1, 1 };
        [SerializeField] private float[] manaCosts = { 30f, 40f, 50f, 60f };
        [SerializeField] private float[] cooldowns = { 6f, 5.5f, 5f, 4.5f };
        [SerializeField, Min(0f)] private float castPoint = 0.25f;
        [SerializeField, Min(0f)] private float range = 8f;
        [SerializeField] private AbilityTargeting targeting;
        [SerializeField] private AbilityEffect effect;
        [SerializeField] private float[] effectValues = { 40f, 70f, 100f, 130f };
        [SerializeField, Min(0f)] private float areaRadius = 2.5f;
        [SerializeField, Min(0f)] private float duration = 3f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 14f;

        public string AbilityId => abilityId;
        public string DisplayName => displayName;
        public string Description => description;
        public int MaximumLevel => Mathf.Clamp(maximumLevel, 1, 4);
        public float CastPoint => Mathf.Max(0f, castPoint);
        public float Range => Mathf.Max(0f, range);
        public AbilityTargeting Targeting => targeting;
        public AbilityEffect Effect => effect;
        public float AreaRadius => Mathf.Max(0f, areaRadius);
        public float Duration => Mathf.Max(0f, duration);
        public float ProjectileSpeed => Mathf.Max(0.01f, projectileSpeed);
        public int RequiredHeroLevel(int level) => Value(requiredHeroLevels, level, 1);
        public float ManaCost(int level) => Mathf.Max(0f, Value(manaCosts, level, 0f));
        public float Cooldown(int level) => Mathf.Max(0f, Value(cooldowns, level, 0f));
        public float EffectValue(int level) => Mathf.Max(0f, Value(effectValues, level, 0f));

        /// <summary>Builds data-only provisional kits for the first roster. Runtime
        /// instances are immutable after setup and are never sent through Netcode.</summary>
        public void ConfigureRuntime(string id, string name, string text, AbilityTargeting targetType, AbilityEffect effectType, float mana, float cooldown, float value, float abilityRange, float radius, float effectDuration, float castDelay = .2f, float speed = 14f)
        {
            abilityId=id;displayName=name;description=text;targeting=targetType;effect=effectType;castPoint=Mathf.Max(0f,castDelay);range=Mathf.Max(0f,abilityRange);areaRadius=Mathf.Max(0f,radius);duration=Mathf.Max(0f,effectDuration);projectileSpeed=Mathf.Max(.01f,speed);
            maximumLevel=4;requiredHeroLevels=new[]{1,3,5,7};manaCosts=new[]{mana,mana+5f,mana+10f,mana+15f};cooldowns=new[]{cooldown,Mathf.Max(.1f,cooldown-.4f),Mathf.Max(.1f,cooldown-.8f),Mathf.Max(.1f,cooldown-1.2f)};effectValues=new[]{value,value*1.35f,value*1.7f,value*2.05f};
        }

        private static float Value(float[] values, int level, float fallback) => values != null && level > 0 && level <= values.Length ? values[level - 1] : fallback;
        private static int Value(int[] values, int level, int fallback) => values != null && level > 0 && level <= values.Length ? Mathf.Max(1, values[level - 1]) : fallback;
    }
}

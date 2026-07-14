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
        [SerializeField] private Texture2D icon;

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
        public Texture2D Icon => icon;
        public int RequiredHeroLevel(int level) => Value(requiredHeroLevels, level, 1);
        public float ManaCost(int level) => Mathf.Max(0f, Value(manaCosts, level, 0f));
        public float Cooldown(int level) => Mathf.Max(0f, Value(cooldowns, level, 0f));
        public float EffectValue(int level) => Mathf.Max(0f, Value(effectValues, level, 0f));
        /// <summary>Refreshes editor-generated UI art without changing ability
        /// behaviour. Useful when the menu stays alive across asset generation.</summary>
        public bool TryRefreshIconFromResources()
        {
            if (string.IsNullOrWhiteSpace(abilityId)) return false;
            string[] words=abilityId.Split(new[]{'.','_'},System.StringSplitOptions.RemoveEmptyEntries);
            string fileName=string.Empty;
            for(int i=0;i<words.Length;i++) fileName+=char.ToUpperInvariant(words[i][0])+words[i].Substring(1);
            Texture2D loaded=Resources.Load<Texture2D>($"Art/UI/AbilityIcons/{fileName}Icon");
            if(loaded==null) return false;
            icon=loaded;return true;
        }

        /// <summary>Builds data-only provisional kits for the first roster. Runtime
        /// instances are immutable after setup and are never sent through Netcode.</summary>
        public void ConfigureRuntime(string id, string name, string text, AbilityTargeting targetType, AbilityEffect effectType, float mana, float cooldown, float value, float abilityRange, float radius, float effectDuration, float castDelay = .2f, float speed = 14f, Texture2D abilityIcon = null, int levels = 4)
        {
            abilityId=id;displayName=name;description=text;targeting=targetType;effect=effectType;castPoint=Mathf.Max(0f,castDelay);range=Mathf.Max(0f,abilityRange);areaRadius=Mathf.Max(0f,radius);duration=Mathf.Max(0f,effectDuration);projectileSpeed=Mathf.Max(.01f,speed);icon=abilityIcon;
            maximumLevel=Mathf.Clamp(levels,1,4);requiredHeroLevels=levels==3?new[]{6,11,16}:new[]{1,3,5,7};manaCosts=BuildValues(mana,5f,levels);cooldowns=BuildCooldowns(cooldown,levels);effectValues=BuildValues(value,value*.35f,levels);
        }

        private static float[] BuildValues(float initial, float step, int levels) { float[] values=new float[Mathf.Clamp(levels,1,4)]; for(int i=0;i<values.Length;i++) values[i]=initial+step*i; return values; }
        private static float[] BuildCooldowns(float initial, int levels) { float[] values=new float[Mathf.Clamp(levels,1,4)]; for(int i=0;i<values.Length;i++) values[i]=Mathf.Max(.1f,initial-.4f*i); return values; }

        private static float Value(float[] values, int level, float fallback) => values != null && level > 0 && level <= values.Length ? values[level - 1] : fallback;
        private static int Value(int[] values, int level, int fallback) => values != null && level > 0 && level <= values.Length ? Mathf.Max(1, values[level - 1]) : fallback;
    }
}

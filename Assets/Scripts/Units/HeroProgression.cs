using System;
using System.Collections.Generic;
using CierzoArena.Combat;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Authoritative per-match XP, level and basic-stat progression for heroes.</summary>
    [RequireComponent(typeof(HeroUnit))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(BasicAttack))]
    [RequireComponent(typeof(ClickMover))]
    public sealed class HeroProgression : MonoBehaviour
    {
        private static readonly List<HeroProgression> activeHeroes = new();
        [SerializeField, Min(1)] private int startingLevel = 1;
        [SerializeField, Min(1)] private int maximumLevel = 10;
        [SerializeField, Min(1)] private int baseExperienceForNextLevel = 100;
        [SerializeField, Min(1f)] private float experienceGrowth = 1.25f;
        [SerializeField, Min(0f)] private float maximumHealthPerLevel = 80f;
        [SerializeField, Min(0f)] private float damagePerLevel = 8f;
        [SerializeField, Min(0f)] private float movementSpeedPerLevel = 0.2f;
        [SerializeField] private bool authorityEnabled = true;

        private Health health;
        private BasicAttack attack;
        private ClickMover mover;
        private int level;
        private int experience;
        private int totalExperience;
        private bool initialized;

        public int Level { get { EnsureInitialized(); return level; } }
        public int Experience { get { EnsureInitialized(); return experience; } }
        public int TotalExperience { get { EnsureInitialized(); return totalExperience; } }
        public int ExperienceForNextLevel { get { EnsureInitialized(); return CalculateExperienceForNextLevel(level); } }
        public int MaximumLevel => Mathf.Max(1, maximumLevel);
        public bool CanReceiveExperience => authorityEnabled && (MatchStateController.Active == null || MatchStateController.Active.IsPlaying);
        public event Action<HeroProgression> Changed;
        public event Action<HeroProgression, int> LevelUp;
        /// <summary>Raised only for an authoritative, accepted positive XP reward.</summary>
        public event Action<HeroProgression, int> ExperienceGained;

        private void Awake() => EnsureInitialized();

        private void OnEnable()
        {
            if (!activeHeroes.Contains(this)) activeHeroes.Add(this);
        }

        private void OnDisable() => activeHeroes.Remove(this);

        /// <summary>Copies live registered heroes without a scene-wide query.</summary>
        public static void CopyActiveHeroesTo(List<HeroProgression> destination)
        {
            destination.Clear();
            for (int i = 0; i < activeHeroes.Count; i++)
            {
                if (activeHeroes[i] != null) destination.Add(activeHeroes[i]);
            }
        }

        private void OnValidate()
        {
            startingLevel = Mathf.Max(1, startingLevel);
            maximumLevel = Mathf.Max(startingLevel, maximumLevel);
            baseExperienceForNextLevel = Mathf.Max(1, baseExperienceForNextLevel);
            experienceGrowth = Mathf.Max(1f, experienceGrowth);
            maximumHealthPerLevel = Mathf.Max(0f, maximumHealthPerLevel);
            damagePerLevel = Mathf.Max(0f, damagePerLevel);
            movementSpeedPerLevel = Mathf.Max(0f, movementSpeedPerLevel);
        }

        public void EnsureInitialized()
        {
            RegisterActiveHero();
            if (initialized)
            {
                return;
            }

            health = GetComponent<Health>();
            attack = GetComponent<BasicAttack>();
            mover = GetComponent<ClickMover>();
            OnValidate();
            level = Mathf.Clamp(startingLevel, 1, MaximumLevel);
            ApplyInitialLevelStats(level - 1);
            initialized = true;
        }

        public void SetAuthorityEnabled(bool enabled) => authorityEnabled = enabled;

        /// <summary>Attempts an authoritative XP award. Surplus carries through multiple levels.</summary>
        public bool TryGainExperience(int amount)
        {
            EnsureInitialized();
            if (!CanReceiveExperience || amount <= 0 || level >= MaximumLevel)
            {
                return false;
            }

            experience = SaturatingAdd(experience, amount);
            totalExperience = SaturatingAdd(totalExperience, amount);
            bool levelled = false;
            while (level < MaximumLevel)
            {
                int required = CalculateExperienceForNextLevel(level);
                if (experience < required)
                {
                    break;
                }

                experience -= required;
                level++;
                ApplyNewLevelStats();
                levelled = true;
                LevelUp?.Invoke(this, level);
            }

            if (level >= MaximumLevel)
            {
                // M9 deliberately caps and discards post-cap XP rather than storing a
                // value with no gameplay meaning.
                experience = 0;
            }

            ExperienceGained?.Invoke(this, amount);
            Changed?.Invoke(this);
            return levelled;
        }

        /// <summary>Applies exact server state to an observer without granting local XP.</summary>
        public void ApplyAuthoritativeState(int replicatedLevel, int replicatedExperience, int replicatedTotalExperience)
        {
            EnsureInitialized();
            authorityEnabled = false;
            int targetLevel = Mathf.Clamp(replicatedLevel, 1, MaximumLevel);
            if (targetLevel > level)
            {
                for (int next = level + 1; next <= targetLevel; next++)
                {
                    level = next;
                    ApplyNewLevelStats();
                    LevelUp?.Invoke(this, level);
                }
            }

            level = targetLevel;
            experience = level >= MaximumLevel ? 0 : Mathf.Max(0, replicatedExperience);
            totalExperience = Mathf.Max(0, replicatedTotalExperience);
            Changed?.Invoke(this);
        }

        private int CalculateExperienceForNextLevel(int currentLevel)
        {
            if (currentLevel >= MaximumLevel)
            {
                return 0;
            }

            double scaled = baseExperienceForNextLevel * Math.Pow(experienceGrowth, Mathf.Max(0, currentLevel - 1));
            return Mathf.Clamp((int)Math.Min(int.MaxValue, Math.Ceiling(scaled)), 1, int.MaxValue);
        }

        private void ApplyInitialLevelStats(int completedLevelUps)
        {
            health?.AddMaximumHealth(maximumHealthPerLevel * completedLevelUps);
            attack?.SetLevelDamageBonus(damagePerLevel * completedLevelUps);
            mover?.SetLevelMoveSpeedBonus(movementSpeedPerLevel * completedLevelUps);
        }

        private void ApplyNewLevelStats()
        {
            health?.AddMaximumHealth(maximumHealthPerLevel);
            attack?.SetLevelDamageBonus(damagePerLevel * (level - 1));
            mover?.SetLevelMoveSpeedBonus(movementSpeedPerLevel * (level - 1));
        }

        private static int SaturatingAdd(int left, int right) => left > int.MaxValue - right ? int.MaxValue : left + right;

        private void RegisterActiveHero()
        {
            if (!activeHeroes.Contains(this)) activeHeroes.Add(this);
        }
    }
}

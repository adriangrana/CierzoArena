using CierzoArena.CameraSystem;
using CierzoArena.Combat;
using CierzoArena.Frontend;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Local-only provisional XP/level feedback; progression remains authoritative elsewhere.</summary>
    [RequireComponent(typeof(HeroProgression))]
    public sealed class HeroProgressionFeedback : MonoBehaviour
    {
        private HeroProgression progression;
        private Health health;
        private BasicAttack attack;
        private ClickMover mover;
        private HeroEconomy economy;
        private float levelUpUntil;
        private int recentGold;
        private float goldGainUntil;
        private GUIStyle statsStyle;
        private GUIStyle levelUpStyle;

        private void Awake()
        {
            progression = GetComponent<HeroProgression>();
            health = GetComponent<Health>();
            attack = GetComponent<BasicAttack>();
            mover = GetComponent<ClickMover>();
            economy = GetComponent<HeroEconomy>();
            progression.LevelUp += OnLevelUp;
            if (economy != null) economy.GoldGained += OnGoldGained;
        }

        private void OnDestroy()
        {
            if (progression != null) progression.LevelUp -= OnLevelUp;
            if (economy != null) economy.GoldGained -= OnGoldGained;
        }

        private void OnLevelUp(HeroProgression _, int __) => levelUpUntil = Time.unscaledTime + 2f;
        private void OnGoldGained(HeroEconomy _, int amount)
        {
            recentGold = amount;
            goldGainUntil = Time.unscaledTime + 1.5f;
        }

        private void OnGUI()
        {
            if (progression == null || LocalHeroProvider.Active == null || LocalHeroProvider.Active.CurrentHero != transform)
            {
                return;
            }

            int next = progression.ExperienceForNextLevel;
            string progress = next > 0 ? $"XP {progression.Experience}/{next}" : "XP MAX";
            EnsureStyles();
            float scale = HudLayout.Scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GUI.Label(new Rect(24f, 96f, 480f, 48f), $"LEVEL {progression.Level}    {progress}", statsStyle);
            string stats = $"HP {health?.Max:0}   DMG {attack?.Damage:0}   MOVE {mover?.EffectiveMoveSpeed:0.0}";
            GUI.Label(new Rect(24f, 150f, 480f, 44f), stats, statsStyle);
            GUI.Label(new Rect(24f, 200f, 300f, 44f), $"GOLD {economy?.Gold ?? 0}", statsStyle);
            if (Time.unscaledTime < levelUpUntil)
            {
                GUI.Label(new Rect(24f, 250f, 280f, 54f), "LEVEL UP!", levelUpStyle);
            }
            if (Time.unscaledTime < goldGainUntil)
            {
                GUI.Label(new Rect(330f, 200f, 150f, 44f), $"+{recentGold}", levelUpStyle);
            }

            GUI.matrix = previousMatrix;
        }

        private void EnsureStyles()
        {
            if (statsStyle != null)
            {
                return;
            }

            statsStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 4, 4),
                normal = { textColor = Color.white }
            };
            levelUpStyle = new GUIStyle(statsStyle)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.88f, 0.25f) }
            };
        }
    }
}

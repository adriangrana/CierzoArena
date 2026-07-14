using CierzoArena.Combat;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Per-hero mutable match counters. Only MatchStatisticsController mutates them.</summary>
    [RequireComponent(typeof(HeroUnit))]
    [RequireComponent(typeof(HeroMatchIdentity))]
    [RequireComponent(typeof(Health))]
    public sealed class HeroMatchStatistics : MonoBehaviour
    {
        private HeroMatchIdentity identity;
        private Health health;
        private HeroProgression progression;
        private HeroEconomy economy;
        private HeroLifeCycle life;
        private int kills, deaths, assists, lastHits, neutralLastHits, goldEarned, experienceEarned, bossParticipations, majorObjectiveSecures, killStreak;
        private long heroDamageDealt, heroDamageReceived, structureDamage;
        private bool deathRecorded;

        public HeroMatchIdentity Identity { get { Ensure(); return identity; } }
        public Health Health { get { Ensure(); return health; } }
        public TeamId Team => Identity.Team;
        public int HeroId => Identity.HeroId;
        public bool DeathRecorded => deathRecorded;
        public void Ensure()
        {
            if (identity != null) return;
            identity = GetComponent<HeroMatchIdentity>(); health = GetComponent<Health>();
            progression = GetComponent<HeroProgression>(); economy = GetComponent<HeroEconomy>(); life = GetComponent<HeroLifeCycle>();
        }
        private void Awake(){Ensure();}
        private void OnEnable()
        {
            // A transport can activate a prefab before it applies the authoritative
            // team and match slot. Runtime stays transport-agnostic through this
            // small optional interface rather than referencing Netcode directly.
            MonoBehaviour[] behaviours=GetComponents<MonoBehaviour>();
            for(int i=0;i<behaviours.Length;i++)
                if(behaviours[i] is IHeroMatchRegistrationGate gate&&!gate.IsHeroMatchRegistrationReady)return;
            MatchStatisticsController.Active?.RegisterHero(this);
        }
        private void OnDisable(){MatchStatisticsController.Active?.UnregisterHero(this);}

        public MatchStatisticsSnapshot Snapshot => new MatchStatisticsSnapshot(HeroId, Team, Identity.DisplayName,
            progression != null ? progression.Level : 1, kills, deaths, assists, lastHits, neutralLastHits,
            economy != null ? economy.Gold : 0, goldEarned, experienceEarned, heroDamageDealt, heroDamageReceived,
            structureDamage, bossParticipations, majorObjectiveSecures, killStreak,
            life != null ? life.State : (health != null && health.IsAlive ? HeroLifeState.Alive : HeroLifeState.Dead),
            life != null ? Mathf.CeilToInt(life.RespawnRemaining) : 0);

        internal void AddKill(){kills=SaturatingAdd(kills,1);killStreak=SaturatingAdd(killStreak,1);}
        internal void AddDeath(){deaths=SaturatingAdd(deaths,1);killStreak=0;deathRecorded=true;}
        internal void AddAssist()=>assists=SaturatingAdd(assists,1);
        internal void AddLastHit(bool neutral){if(neutral)neutralLastHits=SaturatingAdd(neutralLastHits,1);else lastHits=SaturatingAdd(lastHits,1);}
        internal void AddGold(int amount){if(amount>0)goldEarned=SaturatingAdd(goldEarned,amount);}
        internal void AddExperience(int amount){if(amount>0)experienceEarned=SaturatingAdd(experienceEarned,amount);}
        internal void AddHeroDamageDealt(float amount){heroDamageDealt=SaturatingAdd(heroDamageDealt,amount);}
        internal void AddHeroDamageReceived(float amount){heroDamageReceived=SaturatingAdd(heroDamageReceived,amount);}
        internal void AddStructureDamage(float amount){structureDamage=SaturatingAdd(structureDamage,amount);}
        internal void AddBossParticipation(bool secure){bossParticipations=SaturatingAdd(bossParticipations,1);if(secure)majorObjectiveSecures=SaturatingAdd(majorObjectiveSecures,1);}
        internal void ResetDeathGuard()=>deathRecorded=false;
        internal void ApplyReplicated(MatchStatisticsSnapshot value)
        {
            kills=value.Kills;deaths=value.Deaths;assists=value.Assists;lastHits=value.LastHits;neutralLastHits=value.NeutralLastHits;goldEarned=value.GoldEarned;experienceEarned=value.ExperienceEarned;heroDamageDealt=value.HeroDamageDealt;heroDamageReceived=value.HeroDamageReceived;structureDamage=value.StructureDamage;bossParticipations=value.BossParticipations;majorObjectiveSecures=value.MajorObjectiveSecures;killStreak=value.KillStreak;
        }
        private static int SaturatingAdd(int value,int amount)=>amount>0&&value>int.MaxValue-amount?int.MaxValue:value+amount;
        private static long SaturatingAdd(long value,float amount){if(amount<=0f)return value;long rounded=(long)Mathf.CeilToInt(amount);return value>long.MaxValue-rounded?long.MaxValue:value+rounded;}
    }
}

using System;
using CierzoArena.Units;

namespace CierzoArena.Core
{
    [Serializable]
    public struct MatchStatisticsSnapshot
    {
        public int HeroId, Level, Kills, Deaths, Assists, LastHits, NeutralLastHits, CurrentGold, GoldEarned, ExperienceEarned, BossParticipations, MajorObjectiveSecures, KillStreak, RespawnSeconds;
        public TeamId Team;
        public string DisplayName;
        public long HeroDamageDealt, HeroDamageReceived, StructureDamage;
        public HeroLifeState LifeState;
        public MatchStatisticsSnapshot(int heroId,TeamId team,string name,int level,int kills,int deaths,int assists,int lastHits,int neutralLastHits,int currentGold,int goldEarned,int experienceEarned,long heroDamageDealt,long heroDamageReceived,long structureDamage,int bossParticipations,int majorObjectiveSecures,int killStreak,HeroLifeState lifeState,int respawnSeconds)
        {HeroId=heroId;Team=team;DisplayName=name;Level=level;Kills=kills;Deaths=deaths;Assists=assists;LastHits=lastHits;NeutralLastHits=neutralLastHits;CurrentGold=currentGold;GoldEarned=goldEarned;ExperienceEarned=experienceEarned;HeroDamageDealt=heroDamageDealt;HeroDamageReceived=heroDamageReceived;StructureDamage=structureDamage;BossParticipations=bossParticipations;MajorObjectiveSecures=majorObjectiveSecures;KillStreak=killStreak;LifeState=lifeState;RespawnSeconds=respawnSeconds;}
    }
}

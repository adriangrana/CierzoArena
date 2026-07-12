using System;
using System.Collections.Generic;
using CierzoArena.Core;
using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>Server-owned, throttled snapshot replication for the match scoreboard.</summary>
    [RequireComponent(typeof(MatchStatisticsController))]
    public sealed class NetworkMatchStatisticsController : NetworkBehaviour
    {
        private struct NetworkHeroStatistics : INetworkSerializable, IEquatable<NetworkHeroStatistics>
        {
            public int HeroId,Level,Kills,Deaths,Assists,LastHits,NeutralLastHits,CurrentGold,GoldEarned,ExperienceEarned,BossParticipations,MajorObjectiveSecures,KillStreak,RespawnSeconds;
            public byte Team,LifeState;
            public long HeroDamageDealt,HeroDamageReceived,StructureDamage;
            public NetworkHeroStatistics(MatchStatisticsSnapshot value)
            {HeroId=value.HeroId;Level=value.Level;Kills=value.Kills;Deaths=value.Deaths;Assists=value.Assists;LastHits=value.LastHits;NeutralLastHits=value.NeutralLastHits;CurrentGold=value.CurrentGold;GoldEarned=value.GoldEarned;ExperienceEarned=value.ExperienceEarned;BossParticipations=value.BossParticipations;MajorObjectiveSecures=value.MajorObjectiveSecures;KillStreak=value.KillStreak;RespawnSeconds=value.RespawnSeconds;Team=(byte)value.Team;LifeState=(byte)value.LifeState;HeroDamageDealt=value.HeroDamageDealt;HeroDamageReceived=value.HeroDamageReceived;StructureDamage=value.StructureDamage;}
            public MatchStatisticsSnapshot ToRuntime()=>new MatchStatisticsSnapshot(HeroId,(TeamId)Team,$"{(TeamId)Team} {HeroId%1000+1}",Level,Kills,Deaths,Assists,LastHits,NeutralLastHits,CurrentGold,GoldEarned,ExperienceEarned,HeroDamageDealt,HeroDamageReceived,StructureDamage,BossParticipations,MajorObjectiveSecures,KillStreak,(HeroLifeState)LifeState,RespawnSeconds);
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T:IReaderWriter
            {serializer.SerializeValue(ref HeroId);serializer.SerializeValue(ref Level);serializer.SerializeValue(ref Kills);serializer.SerializeValue(ref Deaths);serializer.SerializeValue(ref Assists);serializer.SerializeValue(ref LastHits);serializer.SerializeValue(ref NeutralLastHits);serializer.SerializeValue(ref CurrentGold);serializer.SerializeValue(ref GoldEarned);serializer.SerializeValue(ref ExperienceEarned);serializer.SerializeValue(ref BossParticipations);serializer.SerializeValue(ref MajorObjectiveSecures);serializer.SerializeValue(ref KillStreak);serializer.SerializeValue(ref RespawnSeconds);serializer.SerializeValue(ref Team);serializer.SerializeValue(ref LifeState);serializer.SerializeValue(ref HeroDamageDealt);serializer.SerializeValue(ref HeroDamageReceived);serializer.SerializeValue(ref StructureDamage);}
            public bool Equals(NetworkHeroStatistics other)=>HeroId==other.HeroId&&Kills==other.Kills&&Deaths==other.Deaths&&Assists==other.Assists&&LastHits==other.LastHits&&CurrentGold==other.CurrentGold&&GoldEarned==other.GoldEarned&&ExperienceEarned==other.ExperienceEarned&&HeroDamageDealt==other.HeroDamageDealt&&HeroDamageReceived==other.HeroDamageReceived&&StructureDamage==other.StructureDamage&&BossParticipations==other.BossParticipations&&MajorObjectiveSecures==other.MajorObjectiveSecures&&LifeState==other.LifeState&&RespawnSeconds==other.RespawnSeconds;
        }
        private readonly NetworkList<NetworkHeroStatistics> replicatedRows=new(null,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> replicatedDuration=new(0,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> replicatedFinal=new(false,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> announcementVersion=new(0,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<byte> announcementKind=new(0,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<byte> announcementTeam=new(0,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly List<MatchStatisticsSnapshot> snapshots=new();
        private readonly List<MatchStatisticsSnapshot> received=new();
        private MatchStatisticsController statistics;
        private bool dirty;
        private float nextPublish;
        private bool initialized;
        private void Awake()=>EnsureInitialized();
        /// <summary>Safe explicit setup for Edit Mode construction and normal Awake.</summary>
        public void EnsureInitialized()
        {
            if(initialized)return;
            statistics=GetComponent<MatchStatisticsController>();
            SetPreSpawnObserverAuthority();
            initialized=true;
        }
        /// <summary>Keeps the runtime projection read-only until NGO confirms server spawn.</summary>
        public void SetPreSpawnObserverAuthority()
        {
            if(statistics==null)statistics=GetComponent<MatchStatisticsController>();
            statistics.SetAuthorityEnabled(false);
        }
        public override void OnNetworkSpawn()
        {
            EnsureInitialized();
            if(IsServer)
            {
                statistics.SetAuthorityEnabled(true);statistics.StatisticsChanged+=MarkDirty;statistics.AnnouncementRaised+=PublishAnnouncement;dirty=true;PublishNow();
            }
            else
            {
                replicatedRows.OnListChanged+=OnRowsChanged;replicatedDuration.OnValueChanged+=OnScalarChanged;replicatedFinal.OnValueChanged+=OnFinalChanged;announcementVersion.OnValueChanged+=OnAnnouncementChanged;ApplyRemote();
            }
        }
        public override void OnNetworkDespawn()
        {
            if(IsServer){statistics.StatisticsChanged-=MarkDirty;statistics.AnnouncementRaised-=PublishAnnouncement;}
            else{replicatedRows.OnListChanged-=OnRowsChanged;replicatedDuration.OnValueChanged-=OnScalarChanged;replicatedFinal.OnValueChanged-=OnFinalChanged;announcementVersion.OnValueChanged-=OnAnnouncementChanged;}
        }
        private void Update(){if(IsServer&&dirty&&Time.unscaledTime>=nextPublish)PublishNow();}
        private void MarkDirty(){dirty=true;}
        private void PublishNow()
        {
            dirty=false;nextPublish=Time.unscaledTime+.1f;statistics.CopySnapshotsTo(snapshots);
            replicatedRows.Clear();for(int i=0;i<snapshots.Count;i++)replicatedRows.Add(new NetworkHeroStatistics(snapshots[i]));
            replicatedDuration.Value=statistics.DurationSeconds;replicatedFinal.Value=statistics.IsFrozen;
        }
        private void PublishAnnouncement(string message)
        {
            if(!IsServer||string.IsNullOrWhiteSpace(message))return;
            announcementKind.Value=GetAnnouncementKind(message);announcementTeam.Value=GetAnnouncementTeam(message);announcementVersion.Value++;
        }
        private void OnRowsChanged(NetworkListEvent<NetworkHeroStatistics> _)=>ApplyRemote();
        private void OnScalarChanged(int _,int __)=>ApplyRemote();
        private void OnFinalChanged(bool _,bool __)=>ApplyRemote();
        private void OnAnnouncementChanged(int _,int __)=>statistics.ReceiveReplicatedAnnouncement(GetAnnouncementText(announcementKind.Value,announcementTeam.Value));
        private void ApplyRemote()
        {
            if(IsServer)return;received.Clear();for(int i=0;i<replicatedRows.Count;i++)received.Add(replicatedRows[i].ToRuntime());statistics.ApplyReplicatedSnapshots(received,replicatedDuration.Value,replicatedFinal.Value);
        }
        private static byte GetAnnouncementKind(string message)
        {
            if(message.Contains("Guardián"))return 2;
            if(message.Contains("ganado"))return 4;
            if(message.Contains("racha"))return 1;
            if(message.Contains("destruyó"))return 3;
            return 0;
        }
        private static byte GetAnnouncementTeam(string message)=>message.StartsWith("Ember",StringComparison.Ordinal)?(byte)TeamId.Ember:(byte)TeamId.Azure;
        private static string GetAnnouncementText(byte kind,byte team)
        {
            string name=((TeamId)team).ToString();
            return kind switch
            {
                1=>$"{name} está en racha",
                2=>$"{name} aseguró al Guardián del Cierzo",
                3=>$"{name} destruyó una estructura",
                4=>$"{name} ha ganado la partida",
                _=>"Eliminación confirmada"
            };
        }
    }
}

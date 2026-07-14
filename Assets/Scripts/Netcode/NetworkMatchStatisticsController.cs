using System;
using System.Collections.Generic;
using CierzoArena.Core;
using CierzoArena.Units;
using Unity.Collections;
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
            // Current gold and earned gold are deliberately absent. Public match
            // rows are visible to both teams, while gold is sent separately to the
            // local player's team via a server-targeted RPC below.
            public int HeroId,Level,Kills,Deaths,Assists,LastHits,NeutralLastHits,ExperienceEarned,BossParticipations,MajorObjectiveSecures,KillStreak,RespawnSeconds;
            public byte Team,LifeState;
            public long HeroDamageDealt,HeroDamageReceived,StructureDamage;
            // A sanitized display name is public roster data. It is intentionally
            // distinct from authentication identifiers and from private gold data.
            public FixedString64Bytes DisplayName;
            public NetworkHeroStatistics(MatchStatisticsSnapshot value)
            {HeroId=value.HeroId;Level=value.Level;Kills=value.Kills;Deaths=value.Deaths;Assists=value.Assists;LastHits=value.LastHits;NeutralLastHits=value.NeutralLastHits;ExperienceEarned=value.ExperienceEarned;BossParticipations=value.BossParticipations;MajorObjectiveSecures=value.MajorObjectiveSecures;KillStreak=value.KillStreak;RespawnSeconds=value.RespawnSeconds;Team=(byte)value.Team;LifeState=(byte)value.LifeState;HeroDamageDealt=value.HeroDamageDealt;HeroDamageReceived=value.HeroDamageReceived;StructureDamage=value.StructureDamage;DisplayName=new FixedString64Bytes(value.DisplayName??string.Empty);}
            public MatchStatisticsSnapshot ToRuntime()=>new MatchStatisticsSnapshot(HeroId,(TeamId)Team,DisplayName.ToString(),Level,Kills,Deaths,Assists,LastHits,NeutralLastHits,0,0,ExperienceEarned,HeroDamageDealt,HeroDamageReceived,StructureDamage,BossParticipations,MajorObjectiveSecures,KillStreak,(HeroLifeState)LifeState,RespawnSeconds);
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T:IReaderWriter
            {serializer.SerializeValue(ref HeroId);serializer.SerializeValue(ref Level);serializer.SerializeValue(ref Kills);serializer.SerializeValue(ref Deaths);serializer.SerializeValue(ref Assists);serializer.SerializeValue(ref LastHits);serializer.SerializeValue(ref NeutralLastHits);serializer.SerializeValue(ref ExperienceEarned);serializer.SerializeValue(ref BossParticipations);serializer.SerializeValue(ref MajorObjectiveSecures);serializer.SerializeValue(ref KillStreak);serializer.SerializeValue(ref RespawnSeconds);serializer.SerializeValue(ref Team);serializer.SerializeValue(ref LifeState);serializer.SerializeValue(ref HeroDamageDealt);serializer.SerializeValue(ref HeroDamageReceived);serializer.SerializeValue(ref StructureDamage);serializer.SerializeValue(ref DisplayName);}
            public bool Equals(NetworkHeroStatistics other)=>HeroId==other.HeroId&&Level==other.Level&&Kills==other.Kills&&Deaths==other.Deaths&&Assists==other.Assists&&LastHits==other.LastHits&&NeutralLastHits==other.NeutralLastHits&&ExperienceEarned==other.ExperienceEarned&&HeroDamageDealt==other.HeroDamageDealt&&HeroDamageReceived==other.HeroDamageReceived&&StructureDamage==other.StructureDamage&&BossParticipations==other.BossParticipations&&MajorObjectiveSecures==other.MajorObjectiveSecures&&KillStreak==other.KillStreak&&LifeState==other.LifeState&&RespawnSeconds==other.RespawnSeconds&&DisplayName.Equals(other.DisplayName);
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
            PublishTeamGold(snapshots);
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

        /// <summary>
        /// Sends only same-team gold to each client. The broad NetworkList carries
        /// public combat score data but never an enemy economy value.
        /// </summary>
        private void PublishTeamGold(List<MatchStatisticsSnapshot> values)
        {
            if(!IsServer||NetworkManager==null)return;
            foreach(ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if(!TryGetClientTeam(clientId,out TeamId team))continue;
                List<int> heroIds=new();List<int> goldValues=new();
                for(int i=0;i<values.Count;i++)
                {
                    MatchStatisticsSnapshot value=values[i];
                    if(value.Team!=team)continue;
                    heroIds.Add(value.HeroId);goldValues.Add(value.CurrentGold);
                }
                ReceiveTeamGoldRpc(heroIds.ToArray(),goldValues.ToArray(),RpcTarget.Single(clientId,RpcTargetUse.Temp));
            }
        }
        private static bool TryGetClientTeam(ulong clientId,out TeamId team)
        {
            NetworkUnitController[] units=FindObjectsByType<NetworkUnitController>();
            for(int i=0;i<units.Length;i++)
            {
                NetworkUnitController unit=units[i];
                if(unit==null||!unit.IsSpawned||unit.OwnerClientId!=clientId||!unit.TryGetComponent(out TeamMember member))continue;
                team=member.Team;
                return team==TeamId.Azure||team==TeamId.Ember;
            }
            team=TeamId.Neutral;
            return false;
        }
        [Rpc(SendTo.SpecifiedInParams)]
        private void ReceiveTeamGoldRpc(int[] heroIds,int[] goldValues,RpcParams rpcParams=default)
        {
            if(IsServer)return;
            statistics.ApplyReplicatedTeamGold(heroIds,goldValues);
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

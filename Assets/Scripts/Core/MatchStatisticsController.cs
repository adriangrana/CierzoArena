using System;
using System.Collections.Generic;
using CierzoArena.Combat;
using CierzoArena.Structures;
using CierzoArena.Units;
using UnityEngine;

namespace CierzoArena.Core
{
    /// <summary>
    /// Authoritative match-event projection. It observes already-confirmed damage,
    /// deaths, economy and progression events; it never decides combat outcomes.
    /// Hero damage means effective health damage after shields/mitigation, because
    /// that is the confirmed amount exposed by Health and CombatEvents.
    /// </summary>
    [RequireComponent(typeof(MatchStateController))]
    public sealed class MatchStatisticsController : MonoBehaviour
    {
        private struct Contribution { public float Time; public float Amount; }
        private static MatchStatisticsController active;
        private readonly Dictionary<int, HeroMatchStatistics> heroesById = new();
        private readonly List<HeroMatchStatistics> heroOrder = new();
        private readonly Dictionary<Health, Dictionary<int, Contribution>> contributions = new();
        private readonly HashSet<Health> deathObserved = new();
        private readonly Dictionary<int, MatchStatisticsSnapshot> replicatedSnapshots = new();
        // Team-scoped gold arrives separately from the public scoreboard snapshot.
        // It is intentionally not merged into replicatedSnapshots, which prevents a
        // remote client from treating enemy current gold as public match data.
        private readonly Dictionary<int, int> replicatedVisibleGold = new();
        private readonly List<Health> staleDeaths = new();
        private readonly List<int> staleContributorIds = new();
        [SerializeField, Min(1f)] private float assistWindowSeconds = 10f;
        [SerializeField] private bool authorityEnabled = true;
        private MatchStateController match;
        private float duration;
        private int publishedSecond = -1;
        private bool frozen;
        private bool initialized;

        public static MatchStatisticsController Active => active;
        public float Duration => duration;
        public int DurationSeconds => Mathf.Max(0, Mathf.FloorToInt(duration));
        public bool IsFrozen => frozen;
        public bool IsAuthoritative => authorityEnabled;
        public event Action StatisticsChanged;
        public event Action<string> AnnouncementRaised;

        private void Awake()=>EnsureInitialized();
        /// <summary>
        /// Unity does not guarantee Awake after AddComponent in Edit Mode tests.
        /// Registration is also an authoritative entry point, so make it establish
        /// the event subscriptions before any deterministic damage is applied.
        /// </summary>
        public void EnsureInitialized()
        {
            if(initialized)return;
            // The newest authoritative match controller owns the global combat-event
            // projection. This also keeps isolated Edit Mode scenes from allowing a
            // controller left by another fixture to count the same hit twice.
            active=this;
            match=GetComponent<MatchStateController>();
            CombatEvents.DamageApplied+=OnDamageApplied;
            if(match!=null)match.StateChanged+=OnMatchStateChanged;
            initialized=true;
        }
        private void Start()
        {
            EnsureInitialized();
            // Legacy scenes authored before M17 have HeroProgression but not the two
            // new match components. Add the small data components once at startup so
            // the currently saved MOBA scene gains statistics without rebuilding it.
            HeroProgression[] progressions=FindObjectsByType<HeroProgression>();
            for(int i=0;i<progressions.Length;i++)
            {
                HeroProgression progression=progressions[i];if(progression==null)continue;
                if(!progression.TryGetComponent(out HeroMatchIdentity _))progression.gameObject.AddComponent<HeroMatchIdentity>();
                HeroMatchStatistics stats=progression.GetComponent<HeroMatchStatistics>();
                if(stats==null)stats=progression.gameObject.AddComponent<HeroMatchStatistics>();
                RegisterHero(stats);
            }
            HeroMatchStatistics[] found=FindObjectsByType<HeroMatchStatistics>();
            for(int i=0;i<found.Length;i++)RegisterHero(found[i]);
        }
        private void OnDestroy()
        {
            if(!initialized)return;
            CombatEvents.DamageApplied-=OnDamageApplied;
            if(match!=null)match.StateChanged-=OnMatchStateChanged;
            for(int i=0;i<heroOrder.Count;i++)Unsubscribe(heroOrder[i]);
            foreach(Health health in deathObserved)if(health!=null)health.DiedWithContext-=OnAnyDied;
            if(active==this)active=null;
        }
        private void Update()
        {
            if(!CanRecord())return;
            duration+=Mathf.Max(0f,Time.deltaTime);
            int second=DurationSeconds;
            if(second!=publishedSecond){publishedSecond=second;Changed();}
            for(int i=0;i<heroOrder.Count;i++)
            {
                HeroMatchStatistics hero=heroOrder[i];
                if(hero!=null&&hero.Health!=null&&hero.Health.IsAlive&&hero.DeathRecorded)hero.ResetDeathGuard();
            }
            CleanupDestroyedDeathSources();
            PruneExpiredContributions();
        }

        public void SetAuthorityEnabled(bool enabled)
        {
            EnsureInitialized();
            authorityEnabled=enabled;
            if(!enabled)frozen=false;
        }
        public void RegisterHero(HeroMatchStatistics hero)
        {
            EnsureInitialized();
            if(hero==null)return;hero.Ensure();int id=hero.HeroId;
            if(heroesById.TryGetValue(id,out HeroMatchStatistics existing))
            {
                if(existing==hero)return;
                Debug.LogWarning($"Duplicate match hero id {id}; configure distinct HeroMatchIdentity slots.",hero);
                return;
            }
            heroesById.Add(id,hero);heroOrder.Add(hero);heroOrder.Sort(CompareHeroes);ObserveDeath(hero.Health);
            if(hero.TryGetComponent(out HeroEconomy economy)){economy.GoldGained+=OnGoldGained;economy.Changed+=OnGoldChanged;}
            if(hero.TryGetComponent(out HeroProgression progression))progression.ExperienceGained+=OnExperienceGained;
            if(hero.TryGetComponent(out HeroLifeCycle life))life.StateChanged+=OnHeroLifeStateChanged;
            if(!authorityEnabled&&replicatedSnapshots.TryGetValue(id,out MatchStatisticsSnapshot remote))hero.ApplyReplicated(remote);
            Changed();
        }
        public void UnregisterHero(HeroMatchStatistics hero)
        {
            if(hero==null||!heroesById.TryGetValue(hero.HeroId,out HeroMatchStatistics current)||current!=hero)return;
            Unsubscribe(hero);heroesById.Remove(hero.HeroId);heroOrder.Remove(hero);
        }
        public void CopySnapshotsTo(List<MatchStatisticsSnapshot> destination)
        {
            EnsureInitialized();
            destination.Clear();
            if(!authorityEnabled)
            {
                foreach(KeyValuePair<int,MatchStatisticsSnapshot> entry in replicatedSnapshots)destination.Add(entry.Value);
                destination.Sort(CompareSnapshots);return;
            }
            for(int i=0;i<heroOrder.Count;i++)if(heroOrder[i]!=null)destination.Add(heroOrder[i].Snapshot);
        }
        public void ApplyReplicatedSnapshots(IReadOnlyList<MatchStatisticsSnapshot> values, float replicatedDuration, bool final)
        {
            EnsureInitialized();
            if(authorityEnabled)return;
            replicatedSnapshots.Clear();
            for(int i=0;i<values.Count;i++)
            {
                MatchStatisticsSnapshot value=values[i];replicatedSnapshots[value.HeroId]=value;
                if(heroesById.TryGetValue(value.HeroId,out HeroMatchStatistics hero))hero.ApplyReplicated(value);
            }
            duration=Mathf.Max(0f,replicatedDuration);frozen=final;Changed();
        }
        /// <summary>Applies the server-filtered gold snapshot for the local team only.</summary>
        public void ApplyReplicatedTeamGold(IReadOnlyList<int> heroIds, IReadOnlyList<int> goldValues)
        {
            EnsureInitialized();
            if(authorityEnabled)return;
            replicatedVisibleGold.Clear();
            int count=Mathf.Min(heroIds!=null?heroIds.Count:0,goldValues!=null?goldValues.Count:0);
            for(int i=0;i<count;i++)replicatedVisibleGold[heroIds[i]]=Mathf.Max(0,goldValues[i]);
            Changed();
        }
        /// <summary>Returns gold only when the server has explicitly exposed it to this client.</summary>
        public bool TryGetReplicatedVisibleGold(int heroId,out int gold)=>replicatedVisibleGold.TryGetValue(heroId,out gold);
        public void ReceiveReplicatedAnnouncement(string message)
        {
            if(!string.IsNullOrWhiteSpace(message))AnnouncementRaised?.Invoke(message);
        }

        private bool CanRecord()=>active==this&&authorityEnabled&&!frozen&&(match==null||match.IsPlaying);
        private void OnMatchStateChanged(MatchState state)
        {
            if(state==MatchState.Playing)return;
            frozen=true;Changed();
            RaiseAnnouncement(state==MatchState.AzureVictory?"Azure ha ganado la partida":"Ember ha ganado la partida");
        }
        private void OnDamageApplied(Health victim, DamageContext context)
        {
            if(!CanRecord()||victim==null||context.Attacker==null||context.Amount<=0f)return;
            ObserveDeath(victim);
            HeroMatchStatistics attacker=FindHero(context.Attacker);
            if(attacker==null)return;
            if(victim.TryGetComponent(out HeroMatchStatistics victimHero))
            {
                if(victimHero==attacker||victimHero.Team==attacker.Team)return;
                attacker.AddHeroDamageDealt(context.Amount);victimHero.AddHeroDamageReceived(context.Amount);
                RecordContribution(victim,attacker,context.Amount);Changed();return;
            }
            if(victim.TryGetComponent(out StructureEntity _)){attacker.AddStructureDamage(context.Amount);Changed();return;}
            if(victim.TryGetComponent(out NeutralBossController _)){RecordContribution(victim,attacker,context.Amount);}
        }
        private void OnAnyDied(Health victim, DamageContext context)
        {
            if(!CanRecord()||victim==null)return;
            HeroMatchStatistics victimHero=FindHero(victim);
            HeroMatchStatistics killer=FindHero(context.Attacker);
            if(victimHero!=null){ResolveHeroDeath(victim,victimHero,killer);return;}
            if(killer==null)return;
            if(victim.TryGetComponent(out NeutralBossController _)){ResolveBoss(victim,killer);return;}
            if(victim.TryGetComponent(out StructureEntity structure))
            {
                RaiseAnnouncement($"{killer.Team} destruyó {(structure.Kind==StructureKind.Core?"el núcleo":"una torre")}");
                return;
            }
            if(victim.TryGetComponent(out CreepController _)){killer.AddLastHit(false);Changed();return;}
            if(victim.TryGetComponent(out NeutralUnitController _)){killer.AddLastHit(true);Changed();}
        }
        private void ResolveHeroDeath(Health victim, HeroMatchStatistics victimHero, HeroMatchStatistics killer)
        {
            if(victimHero.DeathRecorded)return;
            victimHero.AddDeath();
            bool validKiller=killer!=null&&killer!=victimHero&&killer.Team!=victimHero.Team;
            if(validKiller)
            {
                killer.AddKill();
                GrantAssists(victim,killer,victimHero.Team);
                RaiseAnnouncement($"{killer.Identity.DisplayName} eliminó a {victimHero.Identity.DisplayName}");
                MatchStatisticsSnapshot snapshot=killer.Snapshot;
                if(snapshot.KillStreak==3)RaiseAnnouncement($"{killer.Identity.DisplayName} está en racha");
                else if(snapshot.KillStreak==5)RaiseAnnouncement($"{killer.Identity.DisplayName} está en gran racha");
            }
            contributions.Remove(victim);Changed();
        }
        private void GrantAssists(Health victim, HeroMatchStatistics killer, TeamId victimTeam)
        {
            if(!contributions.TryGetValue(victim,out Dictionary<int,Contribution> table))return;
            float now=Time.unscaledTime;
            foreach(KeyValuePair<int,Contribution> entry in table)
            {
                if(entry.Key==killer.HeroId||now-entry.Value.Time>assistWindowSeconds)continue;
                if(heroesById.TryGetValue(entry.Key,out HeroMatchStatistics assistant)&&assistant.Team!=victimTeam)assistant.AddAssist();
            }
        }
        private void ResolveBoss(Health boss, HeroMatchStatistics killer)
        {
            if(!contributions.TryGetValue(boss,out Dictionary<int,Contribution> table)){killer.AddBossParticipation(true);Changed();return;}
            float now=Time.unscaledTime;
            foreach(KeyValuePair<int,Contribution> entry in table)
            {
                if(now-entry.Value.Time>assistWindowSeconds||!heroesById.TryGetValue(entry.Key,out HeroMatchStatistics participant)||participant.Team!=killer.Team)continue;
                participant.AddBossParticipation(participant==killer);
            }
            contributions.Remove(boss);RaiseAnnouncement($"{killer.Team} aseguró al Guardián del Cierzo");Changed();
        }
        private void RecordContribution(Health victim,HeroMatchStatistics attacker,float amount)
        {
            if(!contributions.TryGetValue(victim,out Dictionary<int,Contribution> table)){table=new Dictionary<int,Contribution>();contributions.Add(victim,table);}
            table.TryGetValue(attacker.HeroId,out Contribution record);record.Time=Time.unscaledTime;record.Amount=Mathf.Max(0f,record.Amount)+Mathf.Max(0f,amount);table[attacker.HeroId]=record;
        }
        private void OnGoldGained(HeroEconomy economy,int amount)
        {
            if(!CanRecord())return;
            HeroMatchStatistics hero=FindHero(economy);if(hero==null)return;
            hero.AddGold(amount);Changed();
        }
        private void OnGoldChanged(HeroEconomy _,int __){if(CanRecord())Changed();}
        private void OnExperienceGained(HeroProgression progression,int amount)
        {
            if(!CanRecord())return;
            HeroMatchStatistics hero=FindHero(progression);if(hero==null)return;
            hero.AddExperience(amount);Changed();
        }
        private void OnHeroLifeStateChanged(HeroLifeCycle life,HeroLifeState state)
        {
            if(!authorityEnabled)return;
            if(state==HeroLifeState.Alive)FindHero(life)?.ResetDeathGuard();
            Changed();
        }
        private HeroMatchStatistics FindHero(Component component)=>component!=null?component.GetComponent<HeroMatchStatistics>():null;
        private HeroMatchStatistics FindHero(TeamMember member)=>member!=null?member.GetComponent<HeroMatchStatistics>():null;
        private void ObserveDeath(Health health){if(health!=null&&deathObserved.Add(health))health.DiedWithContext+=OnAnyDied;}
        private void Unsubscribe(HeroMatchStatistics hero)
        {
            if(hero.TryGetComponent(out HeroEconomy economy)){economy.GoldGained-=OnGoldGained;economy.Changed-=OnGoldChanged;}
            if(hero.TryGetComponent(out HeroProgression progression))progression.ExperienceGained-=OnExperienceGained;
            if(hero.TryGetComponent(out HeroLifeCycle life))life.StateChanged-=OnHeroLifeStateChanged;
        }
        private void CleanupDestroyedDeathSources()
        {
            staleDeaths.Clear();foreach(Health source in deathObserved)if(source==null)staleDeaths.Add(source);
            for(int i=0;i<staleDeaths.Count;i++)deathObserved.Remove(staleDeaths[i]);
        }
        private void PruneExpiredContributions()
        {
            float now=Time.unscaledTime;
            foreach(KeyValuePair<Health,Dictionary<int,Contribution>> victim in contributions)
            {
                staleContributorIds.Clear();
                foreach(KeyValuePair<int,Contribution> entry in victim.Value)
                    if(now-entry.Value.Time>assistWindowSeconds||!heroesById.ContainsKey(entry.Key))staleContributorIds.Add(entry.Key);
                for(int i=0;i<staleContributorIds.Count;i++)victim.Value.Remove(staleContributorIds[i]);
            }
        }
        // A replicated client has no authority to publish, but its local views still
        // need a reactive notification when the server snapshot or team-only gold
        // changes. NetworkMatchStatisticsController subscribes only on the server,
        // so notifying observers on both sides cannot create a client write path.
        private void Changed()=>StatisticsChanged?.Invoke();
        private void RaiseAnnouncement(string message){AnnouncementRaised?.Invoke(message);}
        private static int CompareHeroes(HeroMatchStatistics a,HeroMatchStatistics b)=>a.Team!=b.Team?((int)a.Team).CompareTo((int)b.Team):a.HeroId.CompareTo(b.HeroId);
        private static int CompareSnapshots(MatchStatisticsSnapshot a,MatchStatisticsSnapshot b)=>a.Team!=b.Team?((int)a.Team).CompareTo((int)b.Team):a.HeroId.CompareTo(b.HeroId);
    }
}

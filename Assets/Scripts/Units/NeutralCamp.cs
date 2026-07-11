using System;
using System.Collections.Generic;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    public enum NeutralCampState { Inactive, Alive, Engaged, Respawning }
    public enum NeutralCampCategory { Small, Medium, Large }

    [Serializable]
    public struct NeutralSpawnEntry
    {
        public NeutralCampCategory Category;
        public GameObject Prefab;
        public Vector3 LocalPosition;
        [Min(1)] public int Count;
        public NeutralSpawnEntry(NeutralCampCategory category, GameObject prefab, Vector3 localPosition, int count = 1)
        { Category = category; Prefab = prefab; LocalPosition = localPosition; Count = Mathf.Max(1, count); }
    }

    /// <summary>Authoritative lifecycle for one fixed neutral camp.</summary>
    public sealed class NeutralCamp : MonoBehaviour
    {
        [SerializeField] private string campId = "neutral.camp";
        [SerializeField] private NeutralSpawnEntry[] composition = Array.Empty<NeutralSpawnEntry>();
        [SerializeField, Min(1f)] private float aggroRadius = 8f;
        [SerializeField, Min(2f)] private float leashRange = 15f;
        [SerializeField, Min(.1f)] private float resetRadius = 1.5f;
        [SerializeField, Min(.1f)] private float respawnDelay = 45f;
        [SerializeField] private bool simulationEnabled = true;
        private readonly List<NeutralUnitController> units = new();
        private float respawnElapsed;
        private bool externalSpawner;

        public string CampId => campId;
        public NeutralCampState State { get; private set; } = NeutralCampState.Inactive;
        public float AggroRadius => aggroRadius;
        public float LeashRange => leashRange;
        public float ResetRadius => resetRadius;
        public bool SimulationEnabled => simulationEnabled;
        public int ActiveUnitCount { get { int count=0;for(int i=0;i<units.Count;i++)if(units[i]!=null&&units[i].TryGetComponent(out Health health)&&health.IsAlive)count++;return count; } }
        public event Action<NeutralCamp, NeutralSpawnEntry, Vector3> SpawnRequested;

        private void Start() => SpawnInitial();
        private void Update() => Simulate(Time.deltaTime);
        private void OnValidate(){aggroRadius=Mathf.Max(1f,aggroRadius);leashRange=Mathf.Max(aggroRadius,leashRange);resetRadius=Mathf.Clamp(resetRadius,.1f,leashRange);respawnDelay=Mathf.Max(.1f,respawnDelay);}
        private void OnDestroy(){for(int i=0;i<units.Count;i++)Unsubscribe(units[i]);}

        public void Configure(string id, NeutralSpawnEntry[] entries, float aggro, float leash, float reset, float respawn)
        { campId=string.IsNullOrWhiteSpace(id)?"neutral.camp":id;composition=entries??Array.Empty<NeutralSpawnEntry>();aggroRadius=Mathf.Max(1f,aggro);leashRange=Mathf.Max(aggroRadius,leash);resetRadius=Mathf.Clamp(reset,.1f,leashRange);respawnDelay=Mathf.Max(.1f,respawn); }
        public void SetSimulationEnabled(bool enabled){simulationEnabled=enabled;if(!enabled)for(int i=0;i<units.Count;i++)units[i]?.SetSimulationEnabled(false);}
        public void SetExternalSpawner(bool enabled)=>externalSpawner=enabled;
        public void SpawnInitial(){if(State!=NeutralCampState.Inactive&&State!=NeutralCampState.Respawning)return; if(!CanRun()||composition.Length==0)return;SpawnComposition();}
        public void Simulate(float deltaTime)
        {
            if(!simulationEnabled||!CanRun())return;
            if(State==NeutralCampState.Inactive){SpawnInitial();return;}
            if(State!=NeutralCampState.Respawning)return;
            respawnElapsed+=Mathf.Max(0f,deltaTime);if(respawnElapsed>=respawnDelay)SpawnComposition();
        }
        public void RegisterSpawned(NeutralUnitController unit)
        {
            if(unit==null||units.Contains(unit))return;units.Add(unit);unit.Configure(this,unit.transform.position);unit.SetSimulationEnabled(simulationEnabled);Health health=unit.GetComponent<Health>();if(health!=null)health.Died+=OnUnitDied;
        }
        public void CopyUnitsTo(List<NeutralUnitController> destination){destination.Clear();for(int i=0;i<units.Count;i++)if(units[i]!=null)destination.Add(units[i]);}
        public void NotifyEngaged(NeutralUnitController source, Health aggressor)
        {
            if(!simulationEnabled||State==NeutralCampState.Respawning||aggressor==null)return;State=NeutralCampState.Engaged;
            for(int i=0;i<units.Count;i++){NeutralUnitController ally=units[i];if(ally!=null&&ally!=source)ally.TryAssist(aggressor);}
        }
        public void NotifyUnitIdle()
        {
            if(State!=NeutralCampState.Engaged)return;
            for(int i=0;i<units.Count;i++)if(units[i]!=null&&units[i].IsInCombat)return;
            State=NeutralCampState.Alive;
        }
        private bool CanRun()=>MatchStateController.Active==null||MatchStateController.Active.IsPlaying;
        private void SpawnComposition()
        {
            respawnElapsed=0f;State=NeutralCampState.Alive;units.Clear();
            for(int i=0;i<composition.Length;i++){NeutralSpawnEntry entry=composition[i];if(entry.Prefab==null)continue;for(int ordinal=0;ordinal<Mathf.Max(1,entry.Count);ordinal++){Vector3 position=transform.position+entry.LocalPosition+SpawnOffset(ordinal);if(externalSpawner)SpawnRequested?.Invoke(this,entry,position);else{GameObject instance=Instantiate(entry.Prefab,position,Quaternion.identity);instance.transform.SetParent(transform);RegisterSpawned(instance.GetComponent<NeutralUnitController>());}}}
            if(units.Count==0&&!externalSpawner)State=NeutralCampState.Inactive;
        }
        private static Vector3 SpawnOffset(int ordinal)=>ordinal==0?Vector3.zero:new Vector3((ordinal%2==0?1f:-1f)*.8f,0f,(ordinal/2)*.8f);
        private void OnUnitDied(Health health)
        {
            for(int i=0;i<units.Count;i++)if(units[i]!=null&&units[i].GetComponent<Health>()==health)units[i].SetSimulationEnabled(false);
            if(!AllDead())return;State=NeutralCampState.Respawning;respawnElapsed=0f;
        }
        private bool AllDead(){if(units.Count==0)return false;for(int i=0;i<units.Count;i++){NeutralUnitController unit=units[i];if(unit!=null&&unit.TryGetComponent(out Health health)&&health.IsAlive)return false;}return true;}
        private void Unsubscribe(NeutralUnitController unit){if(unit!=null&&unit.TryGetComponent(out Health health))health.Died-=OnUnitDied;}
    }
}

using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>Turns server camp requests into NGO neutral spawns; clients never simulate a camp.</summary>
    [RequireComponent(typeof(NeutralCamp))]
    public sealed class NetworkNeutralCampSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject smallPrefab;
        [SerializeField] private NetworkObject mediumPrefab;
        [SerializeField] private NetworkObject largePrefab;
        private NeutralCamp camp;private NetworkManager manager;private bool networkMode;
        private void Awake(){camp=GetComponent<NeutralCamp>();camp.SpawnRequested+=SpawnRequested;}
        private void Start(){if(!networkMode)return;Connect();}
        private void OnDestroy(){if(camp!=null)camp.SpawnRequested-=SpawnRequested;if(manager!=null){manager.OnServerStarted-=OnServerStarted;manager.OnServerStopped-=OnServerStopped;}}
        public void ActivateNetworkMode(){if(networkMode)return;networkMode=true;camp.SetSimulationEnabled(false);camp.SetExternalSpawner(true);Connect();}
        private void Connect(){manager=NetworkManager.Singleton;if(manager==null)return;manager.OnServerStarted+=OnServerStarted;manager.OnServerStopped+=OnServerStopped;if(manager.IsServer)OnServerStarted();}
        private void OnServerStarted()=>camp.SetSimulationEnabled(true);
        private void OnServerStopped(bool _)=>camp.SetSimulationEnabled(false);
        private void SpawnRequested(NeutralCamp _,NeutralSpawnEntry entry,Vector3 position)
        {
            if(manager==null||!manager.IsServer)return;NetworkObject prefab=entry.Category==NeutralCampCategory.Small?smallPrefab:entry.Category==NeutralCampCategory.Medium?mediumPrefab:largePrefab;if(prefab==null)return;NetworkObject instance=Instantiate(prefab,position,Quaternion.identity);instance.Spawn();camp.RegisterSpawned(instance.GetComponent<NeutralUnitController>());
        }
    }
}

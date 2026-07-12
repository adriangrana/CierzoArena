using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    public sealed class NetworkNeutralBossSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject bossPrefab;
        [SerializeField] private Vector3 spawnPosition;
        private NetworkManager manager;private bool spawned;
        private void Start(){manager=NetworkManager.Singleton;if(manager==null)return;manager.OnServerStarted+=SpawnServerBoss;if(manager.IsServer)SpawnServerBoss();}
        private void OnDestroy(){if(manager!=null)manager.OnServerStarted-=SpawnServerBoss;}
        private void SpawnServerBoss(){if(spawned||manager==null||!manager.IsServer||bossPrefab==null)return;NetworkObject instance=Instantiate(bossPrefab,spawnPosition,Quaternion.identity);instance.Spawn();spawned=true;}
    }
}

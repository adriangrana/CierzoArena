using System.Collections.Generic;
using System.Text;
using CierzoArena.Combat;
using CierzoArena.CameraSystem;
using CierzoArena.Core;
using CierzoArena.Frontend;
using CierzoArena.Structures;
using CierzoArena.Units;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CierzoArena.Netcode
{
    public enum ArenaStartupState { WaitingForMode, StartingLocal, RunningLocal, StartingHost, StartingClient, RunningNetwork, Failed }
    /// <summary>
    /// Explicit entry point for the complete arena. The scene starts in local-development
    /// mode; choosing Host or Client disables its local gameplay actors before any NGO
    /// session begins. The server then creates every gameplay entity from network prefabs.
    /// </summary>
    public sealed class MobaNetworkMatchBootstrap : MonoBehaviour
    {
        // A complete arena join contains the match, structures, a hero and initial
        // creeps. The UTP default is 128 packets, which is too small for that first
        // replication burst even on localhost.
        private const int ArenaPacketQueueSize = 1024;
        [SerializeField] private NetworkObject azureHeroPrefab;
        [SerializeField] private NetworkObject emberHeroPrefab;
        [SerializeField] private NetworkObject matchPrefab;
        [SerializeField] private NetworkObject azureTowerPrefab;
        [SerializeField] private NetworkObject emberTowerPrefab;
        [SerializeField] private NetworkObject azureCorePrefab;
        [SerializeField] private NetworkObject emberCorePrefab;
        [SerializeField] private Vector3 azureSpawn;
        [SerializeField] private Vector3 emberSpawn;
        [SerializeField] private GameObject[] localOnlyObjects;
        [SerializeField] private StructureEntity[] localStructures;
        [SerializeField] private NetworkCreepWaveSpawner[] waveSpawners;
        [SerializeField] private NetworkNeutralCampSpawner[] campSpawners;
        [SerializeField] private NetworkNeutralBossSpawner bossSpawner;

        private readonly Dictionary<ulong, NetworkObject> heroes = new();
        private readonly Dictionary<ulong, TeamId> clientTeams = new();
        private readonly Dictionary<ulong, string> clientHeroIds = new();
        private readonly List<NetworkObject> infrastructure = new();
        private NetworkManager manager;
        private bool networkMode;
        private bool localDevelopmentMode;
        private bool launchedFromFrontend;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private TeamId requestedTeam = TeamId.Azure;
        private string requestedHeroId = "storm_warden";
        private TeamId localAssignedTeam = TeamId.Neutral;

        public static MobaNetworkMatchBootstrap Active { get; private set; }
        public bool IsNetworkMatchMode => networkMode;
        public ArenaStartupState State { get; private set; } = ArenaStartupState.WaitingForMode;
        public static bool AreModesExclusive(bool networkMode, bool localActorsActive) => networkMode != localActorsActive;
        public static string ValidateRequestedHeroId(string requestedHeroId) => HeroCatalog.Shared.ResolveOrFallback(requestedHeroId)?.HeroId ?? string.Empty;

        public void Configure(NetworkObject azure, NetworkObject ember, NetworkObject match,
            NetworkObject azureTower, NetworkObject emberTower, NetworkObject azureCore, NetworkObject emberCore,
            Vector3 azurePoint, Vector3 emberPoint, GameObject[] localActors, StructureEntity[] structures,
            NetworkCreepWaveSpawner[] waves, NetworkNeutralCampSpawner[] camps, NetworkNeutralBossSpawner boss)
        {
            azureHeroPrefab=azure;emberHeroPrefab=ember;matchPrefab=match;azureTowerPrefab=azureTower;emberTowerPrefab=emberTower;azureCorePrefab=azureCore;emberCorePrefab=emberCore;
            azureSpawn=azurePoint;emberSpawn=emberPoint;localOnlyObjects=localActors;localStructures=structures;waveSpawners=waves;campSpawners=camps;bossSpawner=boss;
        }

        private void Start()
        {
            Active=this;
            SetSceneLocalHeroRegistration(false);
            manager=NetworkManager.Singleton;
            if(manager==null)return;
            HeroCatalog.Shared.BindDevelopmentPrefab(azureHeroPrefab != null ? azureHeroPrefab.gameObject : null);
            if(manager.TryGetComponent(out UnityTransport transport))
            {
                transport.MaxPacketQueueSize=Mathf.Max(transport.MaxPacketQueueSize,ArenaPacketQueueSize);
                Debug.Log($"[M18 Transport] MaxPacketQueueSize={transport.MaxPacketQueueSize}",this);
            }
            manager.OnServerStarted+=OnServerStarted;
            manager.OnServerStopped+=OnServerStopped;
            manager.OnClientConnectedCallback+=OnClientConnected;
            manager.OnClientDisconnectCallback+=OnClientDisconnected;
            manager.ConnectionApprovalCallback=ApproveConnection;
            ArenaVisualPass.Repair(gameObject);
            foreach(Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include))ArenaVisualPass.Repair(renderer.gameObject);
            PauseLocalGameplay();
            if(FrontendLaunchRequest.TryConsume(out FrontendMatchMode mode,out TeamId team,out string address,out ushort port,out string heroId))
            {
                launchedFromFrontend=true;requestedTeam=team;requestedHeroId=ValidateRequestedHeroId(heroId);
                if(manager.TryGetComponent(out UnityTransport frontendTransport))frontendTransport.SetConnectionData(address,port);
                if(mode==FrontendMatchMode.LocalDevelopment)StartLocalDevelopment();
                else if(mode==FrontendMatchMode.Host)StartHost();
                else StartClient();
            }
        }
        private void OnDestroy()
        {
            if(manager==null)return;
            manager.OnServerStarted-=OnServerStarted;manager.OnServerStopped-=OnServerStopped;
            manager.OnClientConnectedCallback-=OnClientConnected;manager.OnClientDisconnectCallback-=OnClientDisconnected;
            if(manager.ConnectionApprovalCallback==ApproveConnection)manager.ConnectionApprovalCallback=null;
            if(Active==this)Active=null;
        }

        public void StartHost()
        {
            if(!EnterNetworkMode(ArenaStartupState.StartingHost))return;
            // The host itself passes through ConnectionApproval during StartHost.
            // Do not reserve its requested team before that callback: doing so makes
            // IsTeamAvailable see its own reservation and incorrectly flip Azure to
            // Ember (and subsequently reject the actual Ember client).
            clientTeams.Remove(NetworkManager.ServerClientId);
            clientHeroIds[NetworkManager.ServerClientId]=ValidateRequestedHeroId(requestedHeroId);
            manager.NetworkConfig.ConnectionData=BuildConnectionPayload(requestedTeam,requestedHeroId);
            localAssignedTeam=TeamId.Neutral;
            bool started=manager.StartHost();
            Debug.Log($"[M18 Spawn] StartHost result={started} IsServer={manager.IsServer} IsHost={manager.IsHost} LocalClientId={manager.LocalClientId}",this);
            // NGO connects the host immediately. Cover both callback orderings: when
            // OnServerStarted ran synchronously it is already idempotently spawned;
            // otherwise this is a harmless no-op until that callback runs.
            if(started&&manager.IsServer)EnsureConnectedPlayersSpawned();
        }
        public void StartClient()
        {
            if(!EnterNetworkMode(ArenaStartupState.StartingClient))return;
            manager.NetworkConfig.ConnectionData=BuildConnectionPayload(requestedTeam,requestedHeroId);manager.StartClient();
        }
        public void StartLocalDevelopment()
        {
            if(networkMode||localDevelopmentMode)return;
            State=ArenaStartupState.StartingLocal;
            localDevelopmentMode=true;
            SetSceneLocalHeroRegistration(true);
            EnableLocalGameplay();
            ConfigureLocalSelectedHero();
            State=ArenaStartupState.RunningLocal;
        }
        private bool EnterNetworkMode(ArenaStartupState startingState)
        {
            if(networkMode)return manager!=null&&!manager.IsListening;
            if(localDevelopmentMode)return false;
            manager=NetworkManager.Singleton;if(manager==null||manager.IsListening)return false;
            networkMode=true;
            State=startingState;
            SetSceneLocalHeroRegistration(false);
            ClearLocalDynamicUnits();
            for(int i=0;i<localOnlyObjects.Length;i++)if(localOnlyObjects[i]!=null)localOnlyObjects[i].SetActive(false);
            SetLocalStructuresActive(false);
            // Dynamic sources intentionally remain inactive here. Starting them before
            // the host's player object has spawned can flood the initial NGO receive
            // queue and, more importantly, leaves the local camera/HUD without an
            // owner to bind to. The server enables them only after SpawnHero succeeds.
            return true;
        }
        private void PauseLocalGameplay()
        {
            for(int i=0;i<localOnlyObjects.Length;i++)if(localOnlyObjects[i]!=null)localOnlyObjects[i].SetActive(false);
            SetLocalStructuresActive(false);
            for(int i=0;i<waveSpawners.Length;i++)if(waveSpawners[i]!=null)waveSpawners[i].GetComponent<CreepWaveSpawner>()?.SetSimulationEnabled(false);
            for(int i=0;i<campSpawners.Length;i++)if(campSpawners[i]!=null)campSpawners[i].GetComponent<NeutralCamp>()?.SetSimulationEnabled(false);
        }
        private void EnableLocalGameplay()
        {
            for(int i=0;i<localOnlyObjects.Length;i++)if(localOnlyObjects[i]!=null)localOnlyObjects[i].SetActive(true);
            SetLocalStructuresActive(true);
            for(int i=0;i<waveSpawners.Length;i++)if(waveSpawners[i]!=null)waveSpawners[i].GetComponent<CreepWaveSpawner>()?.SetSimulationEnabled(true);
            for(int i=0;i<campSpawners.Length;i++)if(campSpawners[i]!=null)campSpawners[i].GetComponent<NeutralCamp>()?.SetSimulationEnabled(true);
        }
        private void SetLocalStructuresActive(bool active)
        {
            for(int i=0;i<localStructures.Length;i++)if(localStructures[i]!=null)localStructures[i].gameObject.SetActive(active);
            foreach(WorldHealthBar bar in FindObjectsByType<WorldHealthBar>(FindObjectsInactive.Include))
            {
                if(bar==null||bar.BoundHealth==null)continue;
                for(int i=0;i<localStructures.Length;i++)
                {
                    StructureEntity structure=localStructures[i];
                    if(structure!=null&&bar.BoundHealth==structure.Health)
                    {
                        bar.gameObject.SetActive(active&&structure.IsAlive);
                        break;
                    }
                }
            }
        }
        /// <summary>
        /// Keeps the scene-owned LocalHeroProvider alive in every mode. Only the
        /// registrar belongs to local development: in a network match it must be
        /// disabled so the owner callback on the spawned NetworkUnitController can
        /// register the correct hero for each peer.
        /// </summary>
        private static void SetSceneLocalHeroRegistration(bool enabled)
        {
            foreach(LocalHeroProvider provider in FindObjectsByType<LocalHeroProvider>(FindObjectsInactive.Include))
            {
                if(provider!=null&&!provider.gameObject.activeSelf)provider.gameObject.SetActive(true);
            }
            foreach(SceneLocalHeroRegistrar registrar in FindObjectsByType<SceneLocalHeroRegistrar>(FindObjectsInactive.Include))
            {
                if(registrar!=null)registrar.enabled=enabled;
            }
        }
        private static void ClearLocalDynamicUnits()
        {
            foreach(CreepController creep in FindObjectsByType<CreepController>())
                if(creep!=null&&!creep.TryGetComponent(out NetworkObject _))Destroy(creep.gameObject);
            foreach(NeutralUnitController neutral in FindObjectsByType<NeutralUnitController>())
                if(neutral!=null&&!neutral.TryGetComponent(out NetworkObject _))Destroy(neutral.gameObject);
        }

        private void OnServerStarted()
        {
            heroes.Clear();infrastructure.Clear();
            Spawn(matchPrefab,Vector3.zero,Quaternion.identity);
            Debug.Log($"[M18 Spawn] OnServerStarted IsServer={manager.IsServer} IsHost={manager.IsHost} LocalClientId={manager.LocalClientId} connected={manager.ConnectedClientsIds.Count}",this);
            // The host is already connected when StartHost completes, but NGO does not
            // guarantee that OnClientConnected is observed in a particular order.
            // EnsureConnectedPlayersSpawned covers that initial connection explicitly.
            EnsureConnectedPlayersSpawned();
            if(!heroes.TryGetValue(NetworkManager.ServerClientId,out NetworkObject hostHero)||hostHero==null||!hostHero.IsSpawned)
            {
                State=ArenaStartupState.Failed;
                Debug.LogError("[M18 Spawn] Server started without a spawned host hero; world sources remain disabled.",this);
                return;
            }
            for(int i=0;i<localStructures.Length;i++)SpawnStructure(localStructures[i]);
            ActivateServerGameplaySources();
            State=ArenaStartupState.RunningNetwork;
        }
        private void OnServerStopped(bool _){heroes.Clear();infrastructure.Clear();clientTeams.Clear();clientHeroIds.Clear();}
        private void OnClientConnected(ulong clientId)
        {
            if(manager==null)return;
            if(!manager.IsServer)
            {
                if(clientId==manager.LocalClientId)State=ArenaStartupState.RunningNetwork;
                return;
            }
            TeamId assigned=GetAssignedTeam(clientId);
            Debug.Log($"[M18 Spawn] OnClientConnected clientId={clientId} alreadyRegistered={heroes.ContainsKey(clientId)} requested={requestedTeam} assigned={assigned}",this);
            EnsurePlayerSpawned(clientId);
        }
        private void OnClientDisconnected(ulong clientId)
        {
            if(manager==null)return;
            if(!manager.IsServer)
            {
                if(clientId==manager.LocalClientId)State=ArenaStartupState.Failed;
                return;
            }
            if(!heroes.TryGetValue(clientId,out NetworkObject hero))return;
            if(hero!=null&&hero.IsSpawned)hero.Despawn(true);heroes.Remove(clientId);
        }
        private void EnsureConnectedPlayersSpawned()
        {
            if(manager==null||!manager.IsServer)return;
            foreach(ulong clientId in manager.ConnectedClientsIds)EnsurePlayerSpawned(clientId);
        }
        /// <summary>Idempotent server-side spawn boundary for one connected player.</summary>
        private void EnsurePlayerSpawned(ulong clientId)
        {
            if(manager==null||!manager.IsServer)return;
            if(heroes.TryGetValue(clientId,out NetworkObject existing)&&existing!=null&&existing.IsSpawned)return;

            TeamId team=GetAssignedTeam(clientId);
            HeroDefinition definition=HeroCatalog.Shared.ResolveOrFallback(GetRequestedHeroId(clientId));
            NetworkObject prefab=definition != null && definition.Prefab != null ? definition.Prefab.GetComponent<NetworkObject>() : null;
            if(prefab==null)prefab=azureHeroPrefab;
            Vector3 position=team==TeamId.Ember?emberSpawn:azureSpawn;
            Debug.Log($"[M18 Spawn] Before hero create clientId={clientId} team={team} prefab={(prefab==null?"null":prefab.name)} hasNetworkObject={(prefab!=null&&prefab.GetComponent<NetworkObject>()!=null)} position={position}",this);
            if(prefab==null)
            {
                State=ArenaStartupState.Failed;
                Debug.LogError($"MOBA network startup failed: no hero prefab is configured for {team}.",this);
                return;
            }

            NetworkObject hero=Instantiate(prefab,position,Quaternion.identity);
            ArenaVisualPass.Repair(hero.gameObject);
            if(hero.TryGetComponent(out HeroMatchIdentity identity))identity.ConfigureHero(definition);
            if(hero.TryGetComponent(out NetworkUnitController networkUnit))networkUnit.ConfigureHeroDefinitionServer(definition?.HeroId);
            Debug.Log($"[M18 Spawn] After Instantiate clientId={clientId} instance={(hero!=null?hero.name:"null")} networkObjectFound={hero!=null} isSpawnedBefore={(hero!=null&&hero.IsSpawned)}",this);
            if(hero==null)
            {
                State=ArenaStartupState.Failed;
                Debug.LogError($"MOBA network startup failed: Instantiate returned no hero for client {clientId}.",this);
                return;
            }
            if(hero.TryGetComponent(out TeamMember teamMember))teamMember.ConfigureTeam(team);
            if(hero.TryGetComponent(out NetworkUnitController teamNetworkUnit))teamNetworkUnit.ConfigureTeamServer(team);
            try
            {
                hero.SpawnWithOwnership(clientId);
            }
            catch(System.Exception exception)
            {
                Destroy(hero.gameObject);
                State=ArenaStartupState.Failed;
                Debug.LogException(exception,this);
                return;
            }
            if(!hero.IsSpawned)
            {
                Destroy(hero.gameObject);
                State=ArenaStartupState.Failed;
                Debug.LogError($"MOBA network startup failed: could not spawn the {team} hero for client {clientId}.",this);
                return;
            }
            heroes[clientId]=hero;
            Debug.Log($"[M18 Spawn] After SpawnWithOwnership clientId={clientId} IsSpawned={hero.IsSpawned} OwnerClientId={hero.OwnerClientId} NetworkObjectId={hero.NetworkObjectId} position={hero.transform.position}",this);
        }
        private void ActivateServerGameplaySources()
        {
            // Do not begin the world simulation unless the host owns a spawned hero.
            // This preserves the same complete match loop for host and clients without
            // allowing creeps/camps to run in an incomplete session.
            if(!heroes.TryGetValue(NetworkManager.ServerClientId,out NetworkObject hostHero)||hostHero==null||!hostHero.IsSpawned)return;
            for(int i=0;i<waveSpawners.Length;i++)waveSpawners[i]?.ActivateNetworkMode();
            for(int i=0;i<campSpawners.Length;i++)campSpawners[i]?.ActivateNetworkMode();
            bossSpawner?.ActivateNetworkMode();
        }
        private TeamId GetAssignedTeam(ulong clientId)
        {
            if(clientTeams.TryGetValue(clientId,out TeamId assigned))return assigned;
            TeamId requested=TeamId.Azure;
            TeamId resolved=IsTeamAvailable(requested)?requested:TeamId.Ember;
            clientTeams[clientId]=resolved;return resolved;
        }
        private bool IsTeamAvailable(TeamId team)
        {
            foreach(KeyValuePair<ulong,TeamId> entry in clientTeams)if(entry.Value==team)return false;
            return true;
        }
        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response)
        {
            ParseConnectionPayload(request.Payload,out TeamId requested,out string heroId);
            TeamId assigned=IsTeamAvailable(requested)?requested:(requested==TeamId.Azure&&IsTeamAvailable(TeamId.Ember)?TeamId.Ember:TeamId.Neutral);
            response.Approved=assigned!=TeamId.Neutral;response.CreatePlayerObject=false;
            if(response.Approved){clientTeams[request.ClientNetworkId]=assigned;clientHeroIds[request.ClientNetworkId]=ValidateRequestedHeroId(heroId);}
            Debug.Log($"[M18 Spawn] Approval clientId={request.ClientNetworkId} alreadyRegistered={heroes.ContainsKey(request.ClientNetworkId)} requested={requested} assigned={assigned} approved={response.Approved}",this);
        }
        public void NotifyLocalOwner(TeamId team)
        {
            localAssignedTeam=team;
            if(!networkMode)return;
            State=ArenaStartupState.RunningNetwork;
        }

        private string GetRequestedHeroId(ulong clientId) => clientHeroIds.TryGetValue(clientId,out string heroId) ? heroId : requestedHeroId;
        private static byte[] BuildConnectionPayload(TeamId team,string heroId) => Encoding.UTF8.GetBytes($"{(int)team}|{heroId ?? string.Empty}");
        private static void ParseConnectionPayload(byte[] payload,out TeamId team,out string heroId)
        {
            team=TeamId.Azure;heroId=string.Empty;
            if(payload==null||payload.Length==0)return;
            string value=Encoding.UTF8.GetString(payload);string[] split=value.Split('|');
            if(split.Length>0&&int.TryParse(split[0],out int raw)&&raw==(int)TeamId.Ember)team=TeamId.Ember;
            if(split.Length>1)heroId=split[1];
        }
        private void ConfigureLocalSelectedHero()
        {
            HeroDefinition definition=HeroCatalog.Shared.ResolveOrFallback(requestedHeroId);
            foreach(HeroMatchIdentity identity in FindObjectsByType<HeroMatchIdentity>(FindObjectsInactive.Exclude))
            {
                if(identity.TryGetComponent(out NetworkObject _))continue;
                if(identity.TryGetComponent(out TeamMember member))member.ConfigureTeam(requestedTeam);
                identity.ConfigureHero(definition);
                HeroSpawnPoint spawn=HeroSpawnPoint.FindFor(requestedTeam);
                if(spawn!=null){identity.transform.SetPositionAndRotation(spawn.transform.position,spawn.transform.rotation);if(identity.TryGetComponent(out ClickMover mover))mover.WarpTo(spawn.transform.position);}
                LocalHeroProvider.Active?.Register(identity.transform);
                if(identity.TryGetComponent(out SelectableUnit selectable))selectable.SetSelected(true);
                return;
            }
            Debug.LogWarning("[M20] No scene-local hero was found; local mode kept its configured fallback.",this);
        }
        private void SpawnStructure(StructureEntity source)
        {
            if(source==null)return;
            NetworkObject prefab=source.Kind==StructureKind.Core?(source.Team==TeamId.Azure?azureCorePrefab:emberCorePrefab):(source.Team==TeamId.Azure?azureTowerPrefab:emberTowerPrefab);
            if(prefab==null)return;
            NetworkObject instance=Instantiate(prefab,source.transform.position,source.transform.rotation);
            ArenaVisualPass.Repair(instance.gameObject);
            if(instance.TryGetComponent(out StructureEntity target))target.Configure(source.Team,source.Kind,source.Lane,source.Tier,source.Health.Max);
            instance.Spawn();infrastructure.Add(instance);
        }
        private void Spawn(NetworkObject prefab,Vector3 position,Quaternion rotation)
        {
            if(prefab==null)return;NetworkObject instance=Instantiate(prefab,position,rotation);ArenaVisualPass.Repair(instance.gameObject);instance.Spawn();infrastructure.Add(instance);
        }

        private void OnGUI()
        {
            if(launchedFromFrontend)return;
            manager=NetworkManager.Singleton;if(manager==null)return;EnsureStyles();
            float scale=Mathf.Clamp(Screen.height/1080f,1f,2.25f);Matrix4x4 previous=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1f));
            GUILayout.BeginArea(new Rect(16f,16f,390f,320f),GUI.skin.box);
            if(!manager.IsListening)
            {
                GUILayout.Label(localDevelopmentMode?"MOBA LOCAL DEVELOPMENT":networkMode?"MOBA NETWORK MATCH":"MOBA: CHOOSE MODE",labelStyle);
                if(!networkMode&&!localDevelopmentMode)
                {
                    GUILayout.Label("The arena is paused until a mode is chosen.",labelStyle);
                    if(GUILayout.Button("Start Local Development",buttonStyle,GUILayout.Height(42f)))StartLocalDevelopment();
                }
                if(!localDevelopmentMode)
                {
                    GUILayout.Label($"Requested team: {requestedTeam}",labelStyle);
                    GUILayout.BeginHorizontal();
                    if(GUILayout.Button("Team: Azure",buttonStyle,GUILayout.Height(32f)))requestedTeam=TeamId.Azure;
                    if(GUILayout.Button("Team: Ember",buttonStyle,GUILayout.Height(32f)))requestedTeam=TeamId.Ember;
                    GUILayout.EndHorizontal();
                    if(GUILayout.Button("Start Host",buttonStyle,GUILayout.Height(42f)))StartHost();
                    if(GUILayout.Button("Start Client",buttonStyle,GUILayout.Height(42f)))StartClient();
                }
            }
            else
            {
                string role=manager.IsHost?"Host":manager.IsServer?"Server":"Client";
                GUILayout.Label($"MOBA {role} | Team: {localAssignedTeam} | clients: {manager.ConnectedClientsIds.Count}",labelStyle);
                if(GUILayout.Button("Shutdown",buttonStyle,GUILayout.Height(42f)))manager.Shutdown();
            }
            GUILayout.EndArea();GUI.matrix=previous;
        }
        private void EnsureStyles()
        {
            if(labelStyle!=null)return;
            labelStyle=new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            buttonStyle=new GUIStyle(GUI.skin.button){fontSize=18,fontStyle=FontStyle.Bold};
        }
    }
}

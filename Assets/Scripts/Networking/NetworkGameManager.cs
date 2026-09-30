using System;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElevatorGame
{
    public sealed class NetworkGameManager : MonoBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }
        public PlayerController playerPrefab;
        public NetworkObject[] networkPrefabs;
        public string Address = "127.0.0.1";
        public string Status = "";
        public ushort port = 7777;
        NetworkManager manager;
        public bool UsingSteam {get;private set;}
        // Checkpoint restoration creates existing characters after all peers reconnect.
        public bool SuppressAutomaticSpawning { get; set; }
        public readonly Dictionary<ulong,ulong> SteamOwners=new();
        SteamTransport steamTransport;
        public bool Connecting { get; private set; }
        float connectStarted;
        void Awake()
        {
            Instance = this;
            manager = GetComponent<NetworkManager>();steamTransport=GetComponent<SteamTransport>();if(!steamTransport)steamTransport=gameObject.AddComponent<SteamTransport>();
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.PlayerPrefab = null;
            manager.NetworkConfig.TickRate = 60;
            manager.NetworkConfig.EnableSceneManagement = true;
            foreach (var prefab in networkPrefabs) manager.AddNetworkPrefab(prefab.gameObject);
            manager.ConnectionApprovalCallback += Approve;
            manager.OnClientConnectedCallback += Connected;
            manager.OnClientDisconnectCallback += Disconnected;
        }
        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("-elevatorHost")) CreateRoom();
            if (args.Contains("-elevatorClient")) JoinRoom();
        }
        void Update()
        {
            if (!UsingSteam && Connecting && Time.unscaledTime - connectStarted > 12)
            { Status = "Connection timed out. Check address and UDP port 7777."; manager.Shutdown(); Connecting = false; }
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if(UsingSteam)
            {
                bool local=request.ClientNetworkId==manager.LocalClientId&&manager.IsServer;
                ulong identity=local?SteamSession.Instance.LocalId:0;
                bool authenticated=local||steamTransport.ApproveIdentity(request.Payload,out identity);
                bool restoring=SteamSession.Instance.Restoring;
                bool allowed=RoundManager.Instance.Phase.Value==RoundPhase.Lobby||restoring;
                response.Approved=authenticated&&allowed&&SteamOwners.Count<8&&!SteamOwners.Values.Contains(identity);
                if(restoring&&SteamSession.Instance.Latest!=null)response.Approved&=SteamSession.Instance.Latest.players.Any(p=>p.steam==identity&&!p.bot);
                response.CreatePlayerObject=false;response.Pending=false;response.Reason="Steam membership, capacity or game state rejected the connection.";
                if(response.Approved)SteamOwners[request.ClientNetworkId]=identity;
                return;
            }
            bool lobby = !RoundManager.Instance || RoundManager.Instance.Phase.Value == RoundPhase.Lobby;
            response.Approved = lobby && manager.ConnectedClientsIds.Count < 8 && RoundManager.Players().Length < 8;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = lobby ? "Room is full (8 players)." : "A round is already in progress.";
        }
        public ulong? OwnerForSteam(ulong steam){foreach(var pair in SteamOwners)if(pair.Value==steam&&manager.ConnectedClientsIds.Contains(pair.Key))return pair.Key;return null;}
        public bool StartSteam(bool host)
        {
            if(manager.IsListening)return false;var session=SteamSession.Instance;if(!session||!session.Ready||!session.InRoom)return false;
            UsingSteam=true;SteamOwners.Clear();GameSession.Instance.StartMultiplayer();manager.NetworkConfig.NetworkTransport=steamTransport;
            steamTransport.Lobby=session.LobbyId;steamTransport.SessionEpoch=session.Epoch;steamTransport.RemoteHost=session.HostId;
            manager.NetworkConfig.ConnectionData=Array.Empty<byte>();connectStarted=Time.unscaledTime;
            bool success=host?manager.StartHost():manager.StartClient();Connecting=!host&&success;
            Status=success?"Connecting through Steam...":"Steam connection could not start.";return success;
        }
        public void CreateRoom()
        {
            if (manager.IsListening) return;
            UsingSteam=false;manager.NetworkConfig.NetworkTransport=GetComponent<UnityTransport>();GameSession.Instance?.StartMultiplayer();
            manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", port, "0.0.0.0");
            Status = manager.StartHost() ? "Room open / UDP 7777" : "Could not start host.";
        }
        public void JoinRoom()
        {
            if (manager.IsListening || Connecting) return;
            if (!System.Net.IPAddress.TryParse(Address.Trim(), out _)) { Status = "Enter a valid host IP address."; return; }
            UsingSteam=false;manager.NetworkConfig.NetworkTransport=GetComponent<UnityTransport>();GameSession.Instance?.StartMultiplayer();
            manager.GetComponent<UnityTransport>().SetConnectionData(Address.Trim(), port);
            Connecting = manager.StartClient(); connectStarted = Time.unscaledTime;
            Status = Connecting ? "Connecting..." : "Could not start client.";
        }
        void Connected(ulong id)
        {
            if (id == manager.LocalClientId) { Connecting = false; Status = "Connected"; }
            if (manager.IsServer&&!SuppressAutomaticSpawning&&!(UsingSteam&&SteamSession.Instance.Restoring)) Spawn(id, false);
        }
        void Disconnected(ulong id)
        {
            SteamOwners.Remove(id);if(UsingSteam&&id==manager.LocalClientId){SteamSession.Instance.ConnectionLost();Connecting=false;return;}
            if (id == manager.LocalClientId)
            { Connecting = false; Status = string.IsNullOrEmpty(manager.DisconnectReason) ? "Disconnected. Return to menu to reconnect." : manager.DisconnectReason; }
        }
        public void Spawn(ulong owner, bool bot)
        {
            if (!manager.IsServer || RoundManager.Players().Length >= 8) return;
            int slot = Enumerable.Range(0, 8).First(n => !RoundManager.Players().Any(p => p.Slot.Value == n));
            var p = Instantiate(playerPrefab, RoundManager.SpawnPoint(slot), Quaternion.identity);

            if (bot) p.NetworkObject.Spawn();
            else p.NetworkObject.SpawnAsPlayerObject(owner);
            p.Slot.Value = slot;if(UsingSteam&&!bot&&SteamOwners.TryGetValue(owner,out ulong steam))p.SteamId.Value=steam; p.IsBot.Value = bot; p.Ready.Value = bot; p.AI?.Initialize();
            p.name = bot ? "Dummy " + (slot + 1) : "Player " + (slot + 1);
        }
        public PlayerController SpawnRestored(ulong owner,MigrationPlayer saved)
        {
            var p=Instantiate(playerPrefab,saved.body.position,saved.body.rotation);
            if(GameSession.Offline)p.InitializeOffline(saved.slot,saved.bot);else if(saved.bot)p.NetworkObject.Spawn();else p.NetworkObject.SpawnAsPlayerObject(owner);
            p.Slot.Value=saved.slot;p.IsBot.Value=saved.bot;p.Ready.Value=saved.ready;p.Alive.Value=saved.alive;if(!GameSession.Offline)p.SteamId.Value=saved.steam;p.AI.Initialize();return p;
        }
        public void AddDummy()
        {
            if (manager.IsServer && RoundManager.Instance.Phase.Value == RoundPhase.Lobby) Spawn(manager.LocalClientId, true);
        }
        public void Leave()
        {
            if(UsingSteam&&SteamSession.Instance&&SteamSession.Instance.InRoom){SteamSession.Instance.LeaveRoom();return;}
            Time.timeScale=1;manager.Shutdown();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        void OnDestroy()
        {
            if (manager)
            {
                manager.ConnectionApprovalCallback -= Approve;
                manager.OnClientConnectedCallback -= Connected;
                manager.OnClientDisconnectCallback -= Disconnected;
            }
            if (Instance == this) Instance = null;
        }
    }
}









using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ElevatorGame
{
    [Serializable] public sealed class SteamConfiguration{public uint appId=480;public string buildVersion="elevator-steam-4";}
    public sealed class SteamRoom{public ulong id;public string name;public int members;}
    public sealed class SteamSession : MonoBehaviour
    {
        const int ControlChannel=71;
        public static SteamSession Instance{get;private set;}
        public bool Ready {get;private set;}
        public bool InRoom=>LobbyId!=0;
        public bool Migrating {get;private set;}
        public bool Restoring {get;private set;}
        public bool Leaving {get;private set;}
        public ulong LobbyId {get;private set;}
        public ulong HostId {get;private set;}
        public int Epoch {get;private set;}
        public ulong LocalId=>Ready?SteamUser.GetSteamID().m_SteamID:0;
        public string Status {get;private set;}="Steam is not connected.";
        public SteamConfiguration Config {get;private set;}
        public readonly List<SteamRoom> Rooms=new();
        public MigrationSnapshot Latest {get;private set;}
        Callback<GameLobbyJoinRequested_t> invited;Callback<LobbyChatUpdate_t> changed;
        Callback<LobbyDataUpdate_t> dataChanged;Callback<SteamNetworkingMessagesSessionRequest_t> sessionRequest;
        CallResult<LobbyCreated_t> created;CallResult<LobbyEnter_t> entered;CallResult<LobbyMatchList_t> listed;
        readonly IntPtr[] inbox=new IntPtr[16];readonly Dictionary<ulong,int> acks=new();
        float nextSnapshot,nextLobbyCheck;int sequence,appliedEpoch;bool starting;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap(){if(!Instance)new GameObject("Steam Session (persistent)").AddComponent<SteamSession>();}
        void Awake()
        {
            if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);
            var config=Resources.Load<TextAsset>("SteamConfig");Config=config?JsonUtility.FromJson<SteamConfiguration>(config.text):new SteamConfiguration();
        }
        void Start()
        {
            if(Application.isBatchMode)return;
            EnsureReady();var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"+connect_lobby");
            if(index>=0&&index+1<args.Length&&ulong.TryParse(args[index+1],out ulong lobby))Join(lobby);
        }
        public bool EnsureReady()
        {
            if(Ready)return true;
            try
            {
                Environment.SetEnvironmentVariable("SteamAppId",Config.appId.ToString());
                var result=SteamAPI.InitEx(out string error);
                if(result!=ESteamAPIInitResult.k_ESteamAPIInitResult_OK){Status="Open Steam and sign in, then press CONNECT STEAM. ("+error+")";return false;}
                Ready=true;SteamNetworkingUtils.InitRelayNetworkAccess();
                invited=Callback<GameLobbyJoinRequested_t>.Create(e=>Join(e.m_steamIDLobby.m_SteamID));
                changed=Callback<LobbyChatUpdate_t>.Create(e=>{if(e.m_ulSteamIDLobby==LobbyId)CheckLobby();});
                dataChanged=Callback<LobbyDataUpdate_t>.Create(e=>{if(e.m_ulSteamIDLobby==LobbyId)CheckLobby();});
                sessionRequest=Callback<SteamNetworkingMessagesSessionRequest_t>.Create(e=>{if(IsMember(e.m_identityRemote.GetSteamID64())){var identity=e.m_identityRemote;SteamNetworkingMessages.AcceptSessionWithUser(ref identity);}});
                created=CallResult<LobbyCreated_t>.Create(OnCreated);entered=CallResult<LobbyEnter_t>.Create(OnEntered);listed=CallResult<LobbyMatchList_t>.Create(OnListed);
                Status="Steam: "+SteamFriends.GetPersonaName()+" / App "+Config.appId;return true;
            }
            catch(Exception e) when(e is DllNotFoundException||e is EntryPointNotFoundException||e is BadImageFormatException)
            {Status="Steam runtime unavailable: "+e.GetType().Name;return false;}
        }
        public string NameFor(ulong id)=>Ready?SteamFriends.GetFriendPersonaName(new CSteamID(id)):"PLAYER";
        public ulong[] Members()
        {
            if(!Ready||!InRoom)return Array.Empty<ulong>();var lobby=new CSteamID(LobbyId);int count=SteamMatchmaking.GetNumLobbyMembers(lobby);var members=new ulong[count];for(int i=0;i<count;i++)members[i]=SteamMatchmaking.GetLobbyMemberByIndex(lobby,i).m_SteamID;return members;
        }
        public bool IsMember(ulong id)=>id!=0&&Members().Contains(id);
        public void Create(bool publicRoom)
        {
            if(GameSession.Offline){Status="Return to the main menu before joining a Steam room.";return;}
            if(!EnsureReady()||InRoom||starting||!NetworkManager.Singleton||NetworkManager.Singleton.IsListening)return;
            starting=true;Status="Creating Steam lobby...";created.Set(SteamMatchmaking.CreateLobby(publicRoom?ELobbyType.k_ELobbyTypePublic:ELobbyType.k_ELobbyTypeFriendsOnly,8));
        }
        void OnCreated(LobbyCreated_t e,bool failed)
        {
            starting=false;if(failed||e.m_eResult!=EResult.k_EResultOK){Status="Steam lobby creation failed: "+e.m_eResult;return;}
            LobbyId=e.m_ulSteamIDLobby;HostId=LocalId;Epoch=1;appliedEpoch=1;sequence=0;Latest=null;Leaving=false;
            var lobby=new CSteamID(LobbyId);SteamMatchmaking.SetLobbyData(lobby,"elevator_build",Config.buildVersion);SteamMatchmaking.SetLobbyData(lobby,"name",SteamFriends.GetPersonaName()+"'s Elevator");
            PublishHost(LocalId,1);SteamMatchmaking.SetLobbyMemberData(lobby,"checkpoint","1");
            if(!NetworkGameManager.Instance.StartSteam(true)){Status="Could not start Steam host.";LeaveNow();return;}Status="Steam room open. Invite your friends.";
        }
        public void Search()
        {
            if(!EnsureReady()||InRoom)return;Rooms.Clear();Status="Finding Steam rooms...";
            SteamMatchmaking.AddRequestLobbyListStringFilter("elevator_build",Config.buildVersion,ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);listed.Set(SteamMatchmaking.RequestLobbyList());
        }
        void OnListed(LobbyMatchList_t e,bool failed)
        {
            if(failed){Status="Steam room search failed.";return;}Rooms.Clear();for(int i=0;i<Math.Min(20,e.m_nLobbiesMatching);i++){var id=SteamMatchmaking.GetLobbyByIndex(i);Rooms.Add(new SteamRoom{id=id.m_SteamID,name=SteamMatchmaking.GetLobbyData(id,"name"),members=SteamMatchmaking.GetNumLobbyMembers(id)});}Status=Rooms.Count==0?"No public rooms found. You can create one.":"Select a room to join.";
        }
        public void Join(ulong lobby)
        {
            if(GameSession.Offline){Status="Return to the main menu before joining a Steam room.";return;}
            if(!EnsureReady()||InRoom||starting||!NetworkManager.Singleton||NetworkManager.Singleton.IsListening)return;starting=true;Leaving=false;Status="Joining Steam lobby...";entered.Set(SteamMatchmaking.JoinLobby(new CSteamID(lobby)));
        }
        void OnEntered(LobbyEnter_t e,bool failed)
        {
            starting=false;if(failed||e.m_EChatRoomEnterResponse!=(uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess){Status="Steam room could not be joined.";return;}
            LobbyId=e.m_ulSteamIDLobby;var lobby=new CSteamID(LobbyId);
            if(SteamMatchmaking.GetLobbyData(lobby,"elevator_build")!=Config.buildVersion){Status="This room uses a different game version.";LeaveNow();return;}
            ReadHost();appliedEpoch=Epoch;Latest=null;sequence=0;
            if(!IsMember(HostId)){Status="Room is changing host. Please join again shortly.";LeaveNow();return;}
            NetworkGameManager.Instance.StartSteam(false);Status="Connecting through Steam...";
        }
        public void Invite(){if(Ready&&InRoom)SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(LobbyId));}
        void ReadHost(){var record=SteamMatchmaking.GetLobbyData(new CSteamID(LobbyId),"hostRecord").Split('|');if(record.Length!=2)return;ulong.TryParse(record[0],out ulong host);int.TryParse(record[1],out int epoch);HostId=host;Epoch=epoch;}
        void PublishHost(ulong host,int epoch)
        {var lobby=new CSteamID(LobbyId);SteamMatchmaking.SetLobbyData(lobby,"hostRecord",host+"|"+epoch);SteamMatchmaking.SetLobbyData(lobby,"state","migrating");}
        public static ulong ChooseSuccessor(ulong[] members,ulong departing,int randomIndex)
        {var candidates=members.Where(id=>id!=departing).OrderBy(id=>id).ToArray();return candidates.Length==0?0:candidates[Math.Abs(randomIndex%candidates.Length)];}
        IEnumerator TransferLobbyOwner(ulong next){yield return new WaitForSecondsRealtime(.5f);if(InRoom&&SteamMatchmaking.GetLobbyOwner(new CSteamID(LobbyId)).m_SteamID==LocalId)SteamMatchmaking.SetLobbyOwner(new CSteamID(LobbyId),new CSteamID(next));}
        void CheckLobby()
        {
            if(!InRoom||Leaving||starting)return;
            ReadHost();
            if(!IsMember(HostId)&&SteamMatchmaking.GetLobbyOwner(new CSteamID(LobbyId)).m_SteamID==LocalId)
            {
                ulong[] ready=Members().Where(id=>id==LocalId||SteamMatchmaking.GetLobbyMemberData(new CSteamID(LobbyId),new CSteamID(id),"checkpoint")==Epoch.ToString()).ToArray();
                ulong next=ChooseSuccessor(ready,HostId,UnityEngine.Random.Range(0,int.MaxValue));if(next==0)next=LocalId;
                PublishHost(next,Epoch+1);StartCoroutine(TransferLobbyOwner(next));ReadHost();
            }
            if(Epoch>appliedEpoch&&HostId!=0&&!Migrating){appliedEpoch=Epoch;StartCoroutine(Migrate());}
        }
        void Update()
        {
            if(!Ready)return;SteamAPI.RunCallbacks();ReceiveControl();
            if(!InRoom||Leaving)return;
            if(Time.unscaledTime>=nextLobbyCheck){nextLobbyCheck=Time.unscaledTime+.3f;CheckLobby();}
            var r=RoundManager.Instance;
            if(!Migrating&&NetworkManager.Singleton&&NetworkManager.Singleton.IsHost&&r&&r.IsSpawned&&LocalId==HostId&&Time.unscaledTime>=nextSnapshot)
            {
                nextSnapshot=Time.unscaledTime+.5f;PublishCheckpoint();
                bool lobby=r.Phase.Value==RoundPhase.Lobby;SteamMatchmaking.SetLobbyJoinable(new CSteamID(LobbyId),lobby);
                SteamMatchmaking.SetLobbyData(new CSteamID(LobbyId),"state",lobby?"lobby":r.Phase.Value==RoundPhase.Results?"results":"playing");
            }
        }
        public void PublishCheckpoint()
        {
            if(!InRoom||!NetworkManager.Singleton||!NetworkManager.Singleton.IsHost||!RoundManager.Instance.IsSpawned)return;
            Latest=MigrationSnapshot.Capture(++sequence,Epoch);byte[] bytes=Pack(Latest);
            foreach(ulong id in Members())if(id!=LocalId)SendControl(id,10,bytes,sequence);
        }
        public static byte[] Pack(MigrationSnapshot snapshot)
        {byte[] text=Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot));using var output=new MemoryStream();using(var zip=new DeflateStream(output,System.IO.Compression.CompressionLevel.Fastest,true))zip.Write(text,0,text.Length);if(output.Length>250000)throw new InvalidDataException("Checkpoint too large");return output.ToArray();}
        public static MigrationSnapshot Unpack(byte[] bytes)
        {
            using var input=new MemoryStream(bytes);using var zip=new DeflateStream(input,CompressionMode.Decompress);using var output=new MemoryStream();var buffer=new byte[8192];int count;while((count=zip.Read(buffer,0,buffer.Length))>0){if(output.Length+count>2000000)throw new InvalidDataException("Checkpoint exceeded limit");output.Write(buffer,0,count);}return JsonUtility.FromJson<MigrationSnapshot>(Encoding.UTF8.GetString(output.ToArray()));
        }
        void SendControl(ulong id,byte type,byte[] payload,int seq)
        {var bytes=new byte[17+payload.Length];bytes[0]=type;Buffer.BlockCopy(BitConverter.GetBytes(LobbyId),0,bytes,1,8);Buffer.BlockCopy(BitConverter.GetBytes(Epoch),0,bytes,9,4);Buffer.BlockCopy(BitConverter.GetBytes(seq),0,bytes,13,4);Buffer.BlockCopy(payload,0,bytes,17,payload.Length);SteamTransport.SendSteam(id,bytes,ControlChannel,true);}
        void ReceiveControl()
        {
            int count=SteamNetworkingMessages.ReceiveMessagesOnChannel(ControlChannel,inbox,inbox.Length);
            for(int i=0;i<count;i++)try
            {
                var message=SteamNetworkingMessage_t.FromIntPtr(inbox[i]);ulong sender=message.m_identityPeer.GetSteamID64();if(!InRoom||!IsMember(sender)||message.m_cbSize<17||message.m_cbSize>250017)continue;
                var bytes=new byte[message.m_cbSize];Marshal.Copy(message.m_pData,bytes,0,bytes.Length);
                if(BitConverter.ToUInt64(bytes,1)!=LobbyId||BitConverter.ToInt32(bytes,9)!=Epoch)continue;int seq=BitConverter.ToInt32(bytes,13);
                if(bytes[0]==10&&sender==HostId&&(Latest==null||seq>Latest.sequence))
                {
                    var payload=new byte[bytes.Length-17];Buffer.BlockCopy(bytes,17,payload,0,payload.Length);var saved=Unpack(payload);
                    if(saved.epoch!=Epoch||saved.sequence!=seq)continue;Latest=saved;SendControl(sender,11,Array.Empty<byte>(),seq);
                    if(SteamMatchmaking.GetLobbyMemberData(new CSteamID(LobbyId),new CSteamID(LocalId),"checkpoint")!=Epoch.ToString())SteamMatchmaking.SetLobbyMemberData(new CSteamID(LobbyId),"checkpoint",Epoch.ToString());
                }
                else if(bytes[0]==11&&LocalId==HostId)acks[sender]=seq;
            }
            catch(Exception e){Debug.LogWarning("Ignored invalid Steam checkpoint: "+e.Message);}
            finally{SteamNetworkingMessage_t.Release(inbox[i]);}
        }
        public void ConnectionLost()
        {if(InRoom&&!Leaving&&!Migrating){Status="Host connection lost. Selecting a new host...";CheckLobby();}}
        IEnumerator Migrate()
        {
            Migrating=Restoring=true;Status="Changing host... restoring your elevator.";Time.timeScale=1;
            var saved=Latest;var old=NetworkManager.Singleton;if(old){old.Shutdown();while(old&&old.ShutdownInProgress)yield return null;if(old)Destroy(old.gameObject);}
            yield return null;yield return SceneManager.LoadSceneAsync("Elevator");yield return null;
            float deadline=Time.unscaledTime+10;while(!NetworkGameManager.Instance&&Time.unscaledTime<deadline)yield return null;
            if(!NetworkGameManager.Instance){Status="Could not restore scene. Leave and rejoin.";Restoring=false;yield break;}
            bool host=HostId==LocalId;
            if(!NetworkGameManager.Instance.StartSteam(host)){Status="Host reconnect failed. Leave and rejoin.";Restoring=false;yield break;}
            deadline=Time.unscaledTime+25;
            if(host)
            {
                while(Time.unscaledTime<deadline&&Members().Any(id=>!NetworkGameManager.Instance.OwnerForSteam(id).HasValue))yield return null;
                if(saved!=null){saved.Restore(NetworkGameManager.Instance.OwnerForSteam);sequence=saved.sequence;}
                else {foreach(var pair in NetworkGameManager.Instance.SteamOwners.ToArray())NetworkGameManager.Instance.Spawn(pair.Key,false);Status="No checkpoint yet; lobby recovered.";}
                Restoring=false;Migrating=false;nextSnapshot=0;SteamMatchmaking.SetLobbyMemberData(new CSteamID(LobbyId),"checkpoint",Epoch.ToString());
                if(saved!=null)Status="Host changed. Game resumed.";
            }
            else
            {
                while(Time.unscaledTime<deadline&&(!NetworkManager.Singleton.IsConnectedClient||!RoundManager.Players().Any(p=>p.ControlledLocally)))yield return null;
                if(!NetworkManager.Singleton.IsConnectedClient||!RoundManager.Players().Any(p=>p.ControlledLocally)){Status="Reconnect timed out. Leave and rejoin.";Restoring=false;yield break;}
                Restoring=false;Migrating=false;Status="Host changed. Game resumed.";
            }
            Cursor.lockState=RoundManager.Instance.Phase.Value!=RoundPhase.Results?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=Cursor.lockState!=CursorLockMode.Locked;
        }
        public void LeaveRoom(){if(!Leaving){StopAllCoroutines();StartCoroutine(LeaveRoutine());}}
        IEnumerator LeaveRoutine()
        {
            Leaving=true;Status="Leaving room...";
            if(!Migrating&&InRoom&&LocalId==HostId&&Members().Length>1)
            {
                PublishCheckpoint();int seq=sequence;float until=Time.unscaledTime+1.2f;
                while(Time.unscaledTime<until&&Members().Any(id=>id!=LocalId&&(!acks.TryGetValue(id,out int ack)||ack<seq)))yield return null;
                ulong[] ready=Members().Where(id=>id!=LocalId&&acks.TryGetValue(id,out int ack)&&ack>=seq).ToArray();
                if(ready.Length==0)ready=Members().Where(id=>id!=LocalId).ToArray();
                ulong next=ChooseSuccessor(ready,LocalId,UnityEngine.Random.Range(0,int.MaxValue));
                if(next!=0){PublishHost(next,Epoch+1);yield return new WaitForSecondsRealtime(.5f);SteamMatchmaking.SetLobbyOwner(new CSteamID(LobbyId),new CSteamID(next));yield return new WaitForSecondsRealtime(.25f);}
            }
            LeaveNow();var manager=NetworkManager.Singleton;if(manager){manager.Shutdown();while(manager&&manager.ShutdownInProgress)yield return null;if(manager)Destroy(manager.gameObject);}yield return null;yield return SceneManager.LoadSceneAsync("Elevator");Leaving=false;
        }
        void LeaveNow(){if(Ready&&InRoom)SteamMatchmaking.LeaveLobby(new CSteamID(LobbyId));LobbyId=HostId=0;Epoch=appliedEpoch=0;Latest=null;Migrating=Restoring=false;acks.Clear();}
        void OnApplicationQuit(){if(Ready){LeaveNow();SteamAPI.Shutdown();Ready=false;}}
        void OnDestroy(){invited?.Dispose();changed?.Dispose();dataChanged?.Dispose();sessionRequest?.Dispose();created?.Dispose();entered?.Dispose();listed?.Dispose();if(Ready){LeaveNow();SteamAPI.Shutdown();Ready=false;}if(Instance==this)Instance=null;}
    }
}




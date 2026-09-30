using System;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ElevatorGame
{
    public enum RoundPhase { Lobby, Playing, Results }
    public enum ElevatorStage { Moving, Stopping, Ding, Waiting, Opening, Event, Closing }
    public sealed class RoundManager : NetworkBehaviour
    {
        public bool IsActive => GameSession.Offline || IsSpawned;
        public bool IsAuthority => GameSession.Offline || IsServer;
        public double Clock => GameSession.Offline ? Time.timeAsDouble : NetworkManager.ServerTime.Time;
        public static RoundManager Instance { get; private set; }
        readonly NetworkVariable<RoundPhase> netPhase = new(RoundPhase.Lobby);
        SessionValue<RoundPhase> localPhase;
        public SessionValue<RoundPhase> Phase => localPhase ??= new SessionValue<RoundPhase>(netPhase,RoundPhase.Lobby);
        readonly NetworkVariable<ElevatorStage> netStage = new(ElevatorStage.Moving);
        SessionValue<ElevatorStage> localStage;
        public SessionValue<ElevatorStage> Stage => localStage ??= new SessionValue<ElevatorStage>(netStage,ElevatorStage.Moving);
        readonly NetworkVariable<int> netFloor = new(0);
        SessionValue<int> localFloor;
        public SessionValue<int> Floor => localFloor ??= new SessionValue<int>(netFloor,0);
        readonly NetworkVariable<int> netSeed = new(0);
        SessionValue<int> localSeed;
        public SessionValue<int> Seed => localSeed ??= new SessionValue<int>(netSeed,0);
        readonly NetworkVariable<int> netAliveCount = new(0);
        SessionValue<int> localAliveCount;
        public SessionValue<int> AliveCount => localAliveCount ??= new SessionValue<int>(netAliveCount,0);
        readonly NetworkVariable<int> netTotalCount = new(0);
        SessionValue<int> localTotalCount;
        public SessionValue<int> TotalCount => localTotalCount ??= new SessionValue<int>(netTotalCount,0);
        readonly NetworkVariable<int> netWinner = new(-1);
        SessionValue<int> localWinner;
        public SessionValue<int> Winner => localWinner ??= new SessionValue<int>(netWinner,-1);
        readonly NetworkVariable<double> netStageEnds = new(0);
        SessionValue<double> localStageEnds;
        public SessionValue<double> StageEnds => localStageEnds ??= new SessionValue<double>(netStageEnds,0);
        readonly NetworkVariable<FixedString128Bytes> netEventTitle = new(new FixedString128Bytes("NEXT FLOOR..."));
        SessionValue<FixedString128Bytes> localEventTitle;
        public SessionValue<FixedString128Bytes> EventTitle => localEventTitle ??= new SessionValue<FixedString128Bytes>(netEventTitle,new FixedString128Bytes("NEXT FLOOR..."));
        public ElevatorDoorController doors;
        public FloorEventManager events;
        public PlayerEliminationController elimination;
        public TextMesh floorDisplay;
        readonly NetworkVariable<ulong> netBroken=new(0); SessionValue<ulong> broken;
        public SessionValue<ulong> BrokenPanels=>broken??=new(netBroken,0);
        WeaponSystem weapons;
        float nextCheck;
        public float Remaining => IsActive ? Mathf.Max(0, (float)(StageEnds.Value - Clock)) : 0;
        public static PlayerController[] Players() => FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Where(p => p.IsActive).OrderBy(p => p.Slot.Value).ToArray();
        public static Vector3 SpawnPoint(int slot) => new((slot % 4 - 1.5f) * 1.25f, 1.2f, slot < 4 ? -1.35f : .3f);
        void Awake() { Instance = this; weapons=gameObject.AddComponent<WeaponSystem>(); }
        public override void OnDestroy() { if (Instance == this) Instance = null; base.OnDestroy(); }
        public bool CanStart => IsAuthority && Players().Length >= 2 && Players().All(p => p.IsLobbyHost || p.Ready.Value);
        public void StartRound()
        {
            if (!IsAuthority || (Phase.Value == RoundPhase.Lobby && !CanStart) || (Phase.Value == RoundPhase.Results && Remaining > 0)) return;
            weapons.ResetAll();BrokenPanels.Value=0;foreach(var panel in FindObjectsByType<CabinPanel>(FindObjectsSortMode.None))panel.ResetHealth();
            events.Cleanup(true); elimination.ResetState(); GameSession.Instance?.OnRoundStart();
            foreach (var p in Players()) p.ResetForRound(SpawnPoint(p.Slot.Value));
            Seed.Value = UnityEngine.Random.Range(1, int.MaxValue);
            events.SetSeed(Seed.Value);
            Floor.Value = 0; Winner.Value = -1;
            TotalCount.Value = Players().Length; AliveCount.Value = TotalCount.Value;
            Phase.Value = RoundPhase.Playing; NextFloor(); if(GameSession.Offline) CaptureLocal(); else CaptureClientRpc();
        }
        public void ReturnToLobby()
        {
            if (!IsAuthority || Phase.Value != RoundPhase.Results || Remaining > 0) return;
            weapons.ResetAll();BrokenPanels.Value=0;foreach(var panel in FindObjectsByType<CabinPanel>(FindObjectsSortMode.None))panel.ResetHealth();events.Cleanup(true); Phase.Value = RoundPhase.Lobby; Floor.Value = 0;Stage.Value=ElevatorStage.Moving;StageEnds.Value=Clock;
            foreach (var p in Players()) { p.ResetForRound(SpawnPoint(p.Slot.Value)); p.Ready.Value = p.IsBot.Value; }
        }
        public void NextFloor()
        {
            if (!IsAuthority || Phase.Value != RoundPhase.Playing) return;
            events.Cleanup(false); Floor.Value++;weapons.OnFloor(Floor.Value);
            EventTitle.Value = new FixedString128Bytes("NEXT FLOOR...");
            SetStage(ElevatorStage.Moving, 12);
        }
        void SetStage(ElevatorStage stage, float duration)
        {
            Stage.Value = stage; StageEnds.Value = Clock + duration;
            if (stage == ElevatorStage.Ding) PlayCue("ding");
            if (stage == ElevatorStage.Opening || stage == ElevatorStage.Closing) PlayCue(stage==ElevatorStage.Closing?"doorClose":"door");
        }
        void Update()
        {
            if(SteamSession.Instance&&SteamSession.Instance.Migrating)return;
            if (floorDisplay) floorDisplay.text = Floor.Value.ToString("00");
            if (!IsActive || !IsAuthority || Phase.Value != RoundPhase.Playing) return;
            if (Time.time > nextCheck)
            {
                nextCheck = Time.time + .1f;
                var players = Players(); elimination.Check(players);
                var living = players.Where(p => p.Alive.Value).ToArray();
                AliveCount.Value = living.Length;
                if (living.Length <= 1)
                {
                    Winner.Value = living.Length == 1 ? living[0].Slot.Value : -1;
                    Phase.Value = RoundPhase.Results; GameSession.Instance?.RecordSurvival();
                    StageEnds.Value = Clock + 5;
                    events.Cleanup(false); PlayCue("win"); return;
                }
            }
            if (Remaining > 0) return;
            switch (Stage.Value)
            {
                case ElevatorStage.Moving: SetStage(ElevatorStage.Stopping, .4f); break;
                case ElevatorStage.Stopping: SetStage(ElevatorStage.Ding, .2f); break;
                case ElevatorStage.Ding: SetStage(ElevatorStage.Waiting, 1.5f); break;
                case ElevatorStage.Waiting: events.Prepare(Floor.Value); SetStage(ElevatorStage.Opening, 2); break;
                case ElevatorStage.Opening:
                    EventTitle.Value = new FixedString128Bytes(events.Title);
                    events.Begin(); SetStage(ElevatorStage.Event, events.Duration); break;
                case ElevatorStage.Event: events.End(); SetStage(ElevatorStage.Closing, 2); break;
                case ElevatorStage.Closing: NextFloor(); break;
            }
        }
        void FixedUpdate()
        {
            if (!IsActive) { doors.SetAperture(0); return; }
            float aperture = Stage.Value switch
            {
                ElevatorStage.Opening => 1 - Remaining / 2,
                ElevatorStage.Event => 1,
                ElevatorStage.Closing => Remaining / 2,
                _ => 0
            };
            doors.SetAperture(Phase.Value == RoundPhase.Lobby ? 0 : aperture);
        }
        public void WeaponEffect(Vector3 start,Vector3 end,int kind){if(GameSession.Offline)WeaponSystem.ShowEffect(start,end,kind);else WeaponEffectClientRpc(start,end,kind);}
        [ClientRpc] void WeaponEffectClientRpc(Vector3 start,Vector3 end,int kind)=>WeaponSystem.ShowEffect(start,end,kind);
        public void PlayCue(string cue) { if(GameSession.Offline) AudioManager.Instance?.Play(cue); else CueClientRpc(cue); }
        [ClientRpc] public void CueClientRpc(string cue) => AudioManager.Instance?.Play(cue);
        [ClientRpc] void CaptureClientRpc() => CaptureLocal();
        void CaptureLocal()
        {
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            CameraRig.Instance?.ResetView();
        }
    }
}








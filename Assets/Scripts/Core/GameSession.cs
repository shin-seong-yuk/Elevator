using System;
using Unity.Netcode;
using UnityEngine;
namespace ElevatorGame
{
    public enum GameMode { SinglePlayer, Multiplayer }
    public enum AIDifficulty { Easy, Normal, Hard }

    // Local gameplay owns local values. The multiplayer adapter owns replicated values.
    public sealed class SessionValue<T>
    {
        readonly NetworkVariable<T> network;
        T local;
        public SessionValue(NetworkVariable<T> network,T initial){this.network=network;local=initial;}
        public T Value { get => GameSession.Offline ? local : network.Value; set { if(GameSession.Offline)local=value;else network.Value=value; } }
    }

    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance {get;private set;}
        public GameMode CurrentGameMode {get;private set;}=GameMode.Multiplayer;
        public AIDifficulty Difficulty=AIDifficulty.Normal;
        public int AICount=5;
        public bool Active {get;private set;}
        public static bool Offline => Instance && Instance.Active && Instance.CurrentGameMode==GameMode.SinglePlayer;
        public double StartedAt {get;private set;}
        public double SurvivalSeconds {get;private set;}
        public bool ShowTutorial {get;set;}
        public static readonly string[] Names={"YOU","BOB","MISO","KEVIN","PIP","NORI","BEAN","SAM"};
        void Awake(){Instance=this;}
        public void StartSinglePlayer()
        {
            if(Active)return;
            CurrentGameMode=GameMode.SinglePlayer;Active=true;
            ShowTutorial=PlayerPrefs.GetInt("ElevatorTutorialSeen",0)==0;
            var prefab=NetworkGameManager.Instance.playerPrefab;
            for(int i=0;i<=AICount;i++)
            {
                var p=Instantiate(prefab,RoundManager.SpawnPoint(i),Quaternion.identity);
                p.InitializeOffline(i,i>0);
            }
            RoundManager.Instance.StartRound();if(ShowTutorial)Time.timeScale=0;
        }
        public void StartMultiplayer(){CurrentGameMode=GameMode.Multiplayer;Active=true;}
        public void OnRoundStart(){StartedAt=Time.timeAsDouble;SurvivalSeconds=0;}
        public void RestoreTiming(float elapsed,float survival){StartedAt=Time.timeAsDouble-elapsed;SurvivalSeconds=survival;}
        public void RecordSurvival(){if(SurvivalSeconds==0)SurvivalSeconds=Time.timeAsDouble-StartedAt;}
        public void DismissTutorial(){ShowTutorial=false;Time.timeScale=1;PlayerPrefs.SetInt("ElevatorTutorialSeen",1);}
        public string NameFor(int slot)
        {
            var p=RoundManager.Players();
            foreach(var actor in p)if(actor.Slot.Value==slot)return actor.DisplayName;
            return slot<0?"NOBODY":"PLAYER "+(slot+1);
        }
        public void AddAI()
        {
            if(RoundManager.Players().Length>=8)return;
            if(!Offline){NetworkGameManager.Instance.Spawn(0,true);return;}
            int slot=0;while(Array.Exists(RoundManager.Players(),p=>p.Slot.Value==slot))slot++;
            var actor=Instantiate(NetworkGameManager.Instance.playerPrefab,RoundManager.SpawnPoint(slot),Quaternion.identity);
            actor.InitializeOffline(slot,true);
            RoundManager.Instance.TotalCount.Value=RoundManager.Players().Length;
        }
        public void RemoveAI()
        {
            var ai=Array.FindLast(RoundManager.Players(),p=>p.IsBot.Value);
            if(!ai)return;
            ai.Grab.ReleaseAll();
            if(Offline)Destroy(ai.gameObject);else if(ai.IsServer)ai.NetworkObject.Despawn();
        }
    }
}




using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ElevatorGame.Editor
{
    [InitializeOnLoad]
    public static class ElevatorSmokeTests
    {
        static IEnumerator routine;
        static double waitUntil;
        static readonly List<string> checks=new();
        static ElevatorSmokeTests()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("ElevatorSmoke",false))
                {SessionState.SetBool("ElevatorSmoke",false);checks.Clear();waitUntil=0;routine=Run();EditorApplication.update+=Tick;}
            };
        }
        [MenuItem("Elevator/Run Gameplay Smoke Tests")]
        public static void Start()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit play mode first.");
            SessionState.SetBool("ElevatorSmoke",true);EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(Time.time<waitUntil)return;
            try
            {
                if(routine.MoveNext()){waitUntil=Time.time+Convert.ToDouble(routine.Current);return;}
                Finish(true,null);
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool passed,string error)
        {
            EditorApplication.update-=Tick;
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/gameplay-smoke.txt",string.Join("\n",checks)+"\n"+(passed?"PASS":"FAIL: "+error));
            Debug.Log("ELEVATOR_SMOKE_"+(passed?"PASS":"FAIL")+" "+error);
            EditorApplication.isPlaying=false;
        }
        static void Check(bool result,string message)
        {
            if(!result)throw new Exception(message);
            checks.Add("PASS "+message);Debug.Log("CHECK "+message);
        }
        static IEnumerator Run()
        {
            yield return 1;
            NetworkGameManager.Instance.CreateRoom();yield return 2;
            var round=RoundManager.Instance;
            Check(round.IsSpawned&&round.IsServer,"Host starts; round object spawns");
            for(int i=0;i<3;i++)NetworkGameManager.Instance.AddDummy();
            yield return 1;
            var players=RoundManager.Players();
            Check(players.Length==4,"Four separate players spawn");
            Check(players.Select(p=>p.transform.position).Distinct().Count()==4,"Spawn positions do not overlap");
            foreach(var p in players){p.Ready.Value=true;p.IsBot.Value=false;}
            Check(round.CanStart,"Ready gate opens");
            round.StartRound();yield return 1;
            Check(round.Phase.Value==RoundPhase.Playing,"Round enters Playing");
            var p0=players[0];var p1=players[1];
            var rail=GameObject.Find("Back handrail").GetComponent<Collider>();
            p0.Body.position=new Vector3(0,1,6.2f);
            p0.Grab.Attach(0,rail,rail.ClosestPoint(p0.transform.position));
            Check(PlayerEliminationController.Supported(p0,players),"Outside player attached to cabin survives");
            p0.Grab.ReleaseAll();p0.Body.position=new Vector3(0,1,6.2f);p1.Body.position=new Vector3(0,1,2.7f);
            p1.Grab.Attach(0,p0.GetComponent<Collider>(),p0.transform.position);
            Check(PlayerEliminationController.Supported(p0,players),"Incoming player grab rescues outside player");
            p1.Body.position=new Vector3(0,1,6.8f);
            p0.Grab.Attach(0,p1.GetComponent<Collider>(),p1.transform.position);
            Check(!PlayerEliminationController.Supported(p0,players),"Unsupported grab cycle does not grant immunity");
            foreach(var p in players){p.Grab.ReleaseAll();p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));}
            round.StageEnds.Value=round.NetworkManager.ServerTime.Time+.1;
            yield return 5;
            Check(round.Stage.Value==ElevatorStage.Event||round.Stage.Value==ElevatorStage.Opening,"Moving / stop / ding / wait / opening sequence runs: "+round.Stage.Value+" / "+round.Phase.Value);
            Check(round.doors.Aperture>.5f,"Doors physically open");
            foreach(var kind in Enum.GetValues(typeof(EventKind)).Cast<EventKind>())
            {
                round.events.Cleanup(true);
                foreach(var p in players)p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));
                round.Phase.Value=RoundPhase.Lobby; // isolate event tests from round elimination
                round.events.Force(kind);round.events.Prepare(8);round.events.Begin();
                var startPositions=players.Select(p=>p.Body.position).ToArray();
                yield return kind==EventKind.Dinosaur?12:kind==EventKind.FakeEmpty?4.5:2.5;
                Check(UnityEngine.Object.FindObjectsByType<FloorEvent>(FindObjectsSortMode.None).Length>0,"Event executes: "+kind);
                if(kind==EventKind.Wind||kind==EventKind.Vacuum)
                    Check(players.Where((p,i)=>Vector3.Distance(p.Body.position,startPositions[i])>.1f).Any(),"Force moves bodies: "+kind);
                round.events.Cleanup(true);yield return .15;
                Check(UnityEngine.Object.FindObjectsByType<NetworkProp>(FindObjectsSortMode.None).Count(p=>!p.GetComponent<WeaponPickup>())==0,"Event cleanup: "+kind);
            }
            round.Phase.Value=RoundPhase.Lobby;
            foreach(var p in players)p.Ready.Value=true;
            round.StartRound();yield return .2;
            for(int i=1;i<players.Length;i++)round.elimination.Eliminate(players[i]);
            yield return .3;
            Check(round.Phase.Value==RoundPhase.Results&&round.Winner.Value==players[0].Slot.Value,"Last living player wins");
            round.StageEnds.Value=round.NetworkManager.ServerTime.Time;
            round.StartRound();yield return .4;
            Check(players.All(p=>p.Alive.Value)&&round.Floor.Value==1,"Restart resets lives and floor");
            players[1].ResetForRound(new Vector3(0,-5,7));yield return 1;
            Check(!players[1].Alive.Value,"Outside unsupported player eliminated after grace: phase="+round.Phase.Value+" pos="+players[1].Body.position+" inside="+PlayerEliminationController.Inside(players[1].Body.position)+" supported="+PlayerEliminationController.Supported(players[1],players)+" alive="+players.Count(p=>p.Alive.Value));
            round.events.Cleanup(true);
            round.Phase.Value=RoundPhase.Results;round.StageEnds.Value=round.NetworkManager.ServerTime.Time;round.ReturnToLobby();
            Check(round.Phase.Value==RoundPhase.Lobby&&players.All(p=>!p.Ready.Value),"Return to lobby resets ready state");
        }
    }
}





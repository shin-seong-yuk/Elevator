using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ElevatorGame.Editor
{
    [InitializeOnLoad] public static class SteamMigrationSmokeTests
    {
        static IEnumerator routine;static double until;static readonly List<string> checks=new(),errors=new();
        static SteamMigrationSmokeTests(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("SteamMigrationTests",false)){SessionState.SetBool("SteamMigrationTests",false);checks.Clear();errors.Clear();until=0;routine=Run();EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}};}
        [MenuItem("Elevator/Test Migration Checkpoints")]public static void Start(){SessionState.SetBool("SteamMigrationTests",true);EditorApplication.isPlaying=true;}
        static void Log(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error||message.Contains("Serialization depth"))&&!stack.Contains("Unity.AI.")&&!message.Contains("Subscription"))errors.Add(message);}
        static void Tick(){if(!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();try{if(Time.time<until)return;if(routine.MoveNext()){until=Time.time+Convert.ToDouble(routine.Current);return;}Finish(true,"");}catch(Exception e){Finish(false,e.ToString());}}
        static void Check(bool pass,string message){if(!pass)throw new Exception(message);checks.Add("PASS "+message);Debug.Log("MIGRATION_CHECK "+message);}
        static void Finish(bool pass,string message){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Time.timeScale=1;File.WriteAllText("TestResults/steam-migration-smoke.txt",string.Join("\n",checks)+"\n"+(pass?"PASS":"FAIL "+message)+"\nRuntime errors: "+string.Join(" | ",errors));EditorApplication.isPlaying=false;}
        static void ClearActors(){foreach(var p in RoundManager.Players())UnityEngine.Object.Destroy(p.gameObject);}
        static IEnumerator Run()
        {
            yield return .5;GameSession.Instance.AICount=3;GameSession.Instance.StartSinglePlayer();GameSession.Instance.DismissTutorial();
            var r=RoundManager.Instance;r.enabled=false;
            foreach(var p in RoundManager.Players()){p.InputEnabled=false;p.AI.enabled=false;p.AI.ResetBrain();}
            var elected=new HashSet<ulong>();for(int i=0;i<20;i++)elected.Add(SteamSession.ChooseSuccessor(new ulong[]{11,22,33,44},11,i));
            Check(elected.SetEquals(new ulong[]{22,33,44}),"Random successor can be any remaining member and never the departed host");
            Check(SteamSession.ChooseSuccessor(new ulong[]{11},11,0)==0,"No phantom host when last member leaves");
            foreach(EventKind kind in Enum.GetValues(typeof(EventKind)))
            {
                r.events.Cleanup(true);r.GetComponent<WeaponSystem>().ResetAll();yield return .1;
                r.Stage.Value=ElevatorStage.Event;r.StageEnds.Value=r.Clock+25;r.doors.SetAperture(1);r.events.Force(kind);r.events.Prepare(8);r.events.Begin();
                yield return kind==EventKind.Dinosaur?4.3:kind==EventKind.GiantHand?1.5:.8;
                var before=MigrationSnapshot.Capture(12,3);float nextRandom=r.events.Random(0,1);
                byte[] packet=SteamSession.Pack(before);var copy=SteamSession.Unpack(packet);
                Check(copy.sequence==12&&copy.epoch==3&&copy.events.Length==before.events.Length,"Checkpoint serialized: "+kind);
                r.events.Cleanup(true);r.GetComponent<WeaponSystem>().ResetAll();ClearActors();yield return .1;
                copy.Restore(_=>0);
                Check(RoundManager.Players().Length==4,"All character slots restored: "+kind);
                Check(UnityEngine.Object.FindObjectsByType<NetworkProp>(FindObjectsSortMode.None).Length==before.props.Length,"Event objects restored without extra spawning: "+kind);
                Check(Mathf.Abs(r.events.Random(0,1)-nextRandom)<.00001f,"Event random sequence resumes: "+kind);
                var after=MigrationSnapshot.Capture(13,4);
                string beforeEvents=JsonUtility.ToJson(new EventEnvelope{events=before.events});
                string afterEvents=JsonUtility.ToJson(new EventEnvelope{events=after.events});
                if(beforeEvents!=afterEvents){int mismatch=0;while(mismatch<Mathf.Min(beforeEvents.Length,afterEvents.Length)&&beforeEvents[mismatch]==afterEvents[mismatch])mismatch++;checks.Add("INFO "+kind+" mismatch at "+mismatch+" before="+beforeEvents.Substring(Mathf.Max(0,mismatch-70),Mathf.Min(160,beforeEvents.Length-Mathf.Max(0,mismatch-70)))+" after="+afterEvents.Substring(Mathf.Max(0,mismatch-70),Mathf.Min(160,afterEvents.Length-Mathf.Max(0,mismatch-70))));}
                Check(beforeEvents==afterEvents,"Event attack timers and object links preserved: "+kind);
                foreach(var p in RoundManager.Players()){p.InputEnabled=false;p.AI.enabled=false;p.AI.ResetBrain();}
                yield return .2;
            }
            r.events.Cleanup(true);r.GetComponent<WeaponSystem>().ResetAll();yield return .1;
            var all=RoundManager.Players();foreach(var p in all){p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));p.InputEnabled=false;p.AI.enabled=false;}
            var actor=all[0];actor.enabled=false;
            var rail=UnityEngine.Object.FindObjectsByType<GrabAnchor>(FindObjectsSortMode.None).First(a=>a.name=="Side handrail"&&a.GetComponentInParent<CabinPanel>().index==26).GetComponent<Collider>();
            actor.Grab.Attach(0,rail,rail.ClosestPoint(actor.Grab.leftHand.position));actor.Grab.ApplyImpact(300);
            all[1].Grab.Attach(1,actor.GetComponent<Collider>(),actor.Body.position);
            var w=r.events.SpawnProp(9,actor.Body.position+Vector3.forward*.4f,Vector3.zero,true).GetComponent<WeaponPickup>();w.Kind.Value=4;w.RestoreAmmo(1);w.BornFloor.Value=6;w.Holder.Value=actor.Slot.Value;
            var embedded=r.events.SpawnProp(17,actor.Body.position+Vector3.up*.3f,Vector3.zero,true).GetComponent<HwachaArrow>();embedded.Stick(actor,actor.Body.position+Vector3.up*.3f,Quaternion.identity);
            r.Floor.Value=8;r.BrokenPanels.Value=(1UL<<3)|(1UL<<24);r.Stage.Value=ElevatorStage.Moving;r.StageEnds.Value=r.Clock+7.25f;
            r.RestoreDrop(-.25,1.1,true,false);
            var state=SteamSession.Unpack(SteamSession.Pack(MigrationSnapshot.Capture(20,3)));
            r.events.Cleanup(true);r.GetComponent<WeaponSystem>().ResetAll();ClearActors();yield return .1;
            state.Restore(_=>0);all=RoundManager.Players();actor=all[0];
            Check(actor.Grab.LeftHeld&&actor.Grab.HasStructuralAnchor(),"Cabin grip reconnects after migration");
            Check(all[1].Grab.RightHeld&&all[1].Grab.ConnectedBodies().Contains(actor.Body),"Player-to-player grip reconnects");
            Check(Mathf.Abs(actor.Grab.GripStrength-40)<.1f,"Hidden grip stamina preserved");
            Check(r.Floor.Value==8&&r.BrokenPanels.Value==state.broken&&Mathf.Abs(r.Remaining-7.25f)<.1f,"Floor, holes and remaining stage time preserved");
            Check(r.DropActive&&r.DropLaunched&&!r.DropLanded&&Mathf.Abs((float)(r.DropEnds.Value-r.Clock)-1.1f)<.1f,"Mid-drop timing and impact phase preserved");
            var held=WeaponSystem.Held(actor);Check(held&&held.Kind.Value==4&&held.BornFloor.Value==6&&held.RemainingAmmo==1,"Weapon type, holder, remaining ammo and birth floor preserved");
            embedded=UnityEngine.Object.FindFirstObjectByType<HwachaArrow>();Check(embedded&&embedded.StuckSlot.Value==actor.Slot.Value&&Vector3.Distance(embedded.transform.position,actor.transform.TransformPoint(embedded.LocalPosition.Value))<.5f,"Embedded Hwacha arrow remains attached after migration");
            yield return .1;
            Check(actor.Grab.LeftHeld,"Restored grip survives the reconnect input grace period");
            var floor=UnityEngine.Object.FindObjectsByType<CabinPanel>(FindObjectsSortMode.None).First(p=>p.index==0);
            Check(Mathf.Abs(floor.transform.lossyScale.x-2.4375f)<.0001f,"Expanded floor tile dimensions close seams");
            Check(errors.Count==0,"No checkpoint runtime errors");r.enabled=true;
        }
        [Serializable] public sealed class EventEnvelope{public MigrationEvent[] events;}
    }
}


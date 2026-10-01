using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ElevatorGame.Editor
{
    [InitializeOnLoad] public static class ChaosSmokeTests
    {
        static IEnumerator routine;static double until;static Action drive;
        static readonly List<string> checks=new(),errors=new();
        static ChaosSmokeTests(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("ChaosTests",false)){SessionState.SetBool("ChaosTests",false);checks.Clear();errors.Clear();until=0;routine=Run();EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}};}
        [MenuItem("Elevator/Test Weapons Grip And Destruction")] public static void Start(){SessionState.SetBool("ChaosTests",true);EditorApplication.isPlaying=true;}
        static void Log(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error)&&!stack.Contains("Unity.AI.")&&!message.Contains("Subscription"))errors.Add(message);}
        static void Tick(){if(!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();try{drive?.Invoke();if(Time.time<until)return;if(routine.MoveNext()){until=Time.time+Convert.ToDouble(routine.Current);return;}Finish(true,"");}catch(Exception e){Finish(false,e.ToString());}}
        static void Check(bool pass,string message){if(!pass)throw new Exception(message);checks.Add("PASS "+message);Debug.Log("CHAOS_CHECK "+message);}
        static void Finish(bool pass,string message){drive=null;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;File.WriteAllText("TestResults/chaos-smoke.txt",string.Join("\n",checks)+"\n"+(pass?"PASS":"FAIL "+message)+"\nRuntime errors: "+string.Join(" | ",errors));EditorApplication.isPlaying=false;}
        static IEnumerator Run()
        {
            yield return .4;
            var session=GameSession.Instance;session.AICount=5;session.StartSinglePlayer();session.DismissTutorial();
            var r=RoundManager.Instance;r.enabled=false;
            var players=RoundManager.Players();var human=players[0];var victim=players[1];
            foreach(var p in players){p.InputEnabled=false;p.AI.enabled=false;p.AI.ResetBrain();}
            yield return .2;
            Check(r.Stage.Value==ElevatorStage.Moving&&r.Remaining>11,"Travel gives twelve seconds for weapon combat");
            Check(UnityEngine.Object.FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None).Length==1,"One weapon spawned on floor one");
            var initial=UnityEngine.Object.FindFirstObjectByType<WeaponPickup>();
            for(int i=0;i<3;i++){r.NextFloor();yield return .1;}
            Check(initial&&UnityEngine.Object.FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None).Length==4,"Weapon persists for four floors with one new weapon per floor");
            r.NextFloor();yield return .1;
            Check(!initial&&UnityEngine.Object.FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None).Length==4,"Oldest weapon expires on fifth floor");
            var weapons=r.GetComponent<WeaponSystem>();weapons.ResetAll();yield return .1;
            foreach(var p in players)p.ResetForRound(new Vector3(10+p.Slot.Value*2,1,10));
            human.ResetForRound(new Vector3(0,1.15f,0));victim.ResetForRound(new Vector3(0,1.15f,1.8f));
            int enabledTicks=0,disabledTicks=0;drive=()=>{if(human.CanDrive)enabledTicks++;else disabledTicks++;};yield return 3;
            drive=null;Check(enabledTicks>0&&disabledTicks>0,"Movement alternates enabled and ignored input periods");
            human.enabled=false;human.Grab.ResetGrip();var anchor=UnityEngine.Object.FindObjectsByType<GrabAnchor>(FindObjectsSortMode.None).First(a=>a.name=="Side handrail").GetComponent<Collider>();
            human.Grab.Attach(0,anchor,anchor.ClosestPoint(human.Grab.leftHand.position));human.Grab.ApplyImpact(300);
            Check(human.Grab.LeftHeld&&Mathf.Abs(human.Grab.GripStrength-40)<.01f,"First heavy impact consumes 60 grip without detaching");
            yield return .35;human.Grab.ApplyImpact(300);
            Check(!human.Grab.LeftHeld&&human.Grab.GripStrength==0,"Second heavy impact detaches exhausted grip");
            for(int i=0;i<60;i++)human.Grab.Tick(false,false);
            Check(human.Grab.GripStrength>4.9f&&human.Grab.GripStrength<5.1f,"Hidden grip recovers five points per physics second");human.enabled=true;
            yield return .3; // let the released joint leave the physics step before teleporting for weapon tests
            foreach(WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                weapons.ResetAll();r.events.Cleanup(true);r.BrokenPanels.Value=0;
                foreach(var panel in UnityEngine.Object.FindObjectsByType<CabinPanel>(FindObjectsSortMode.None))panel.ResetHealth();
                human.ResetForRound(new Vector3(0,1.15f,0));victim.ResetForRound(new Vector3(0,1.15f,1.8f));
                yield return .35;human.SetMoveInput(Vector2.zero,0,false,false);
                var item=r.events.SpawnProp(9,new Vector3(0,1.2f,.4f),Vector3.zero,true).GetComponent<WeaponPickup>();item.Kind.Value=(int)kind;item.BornFloor.Value=r.Floor.Value;
                WeaponSystem.PickupOrThrow(human);yield return .12;
                Check(WeaponSystem.Held(human)==item,"F pickup: "+kind);
                Check(Vector3.Distance(human.Body.position,victim.Body.position)<2.2f,"Target in range before use: "+kind+" human="+human.Body.position+" victim="+victim.Body.position);
                Vector3 victimBefore=victim.Body.position;
                bool blocked=UnityEngine.Physics.Linecast(human.Body.position+Vector3.up*.55f,victim.Body.position,out var blocker,1<<8,QueryTriggerInteraction.Ignore);
                checks.Add("INFO aim="+human.AimDirection+" alive="+victim.Alive.Value+" blocker="+(blocked?blocker.collider.name:"none")+" before="+victimBefore);
                WeaponSystem.Use(human);yield return kind==WeaponKind.GrenadeLauncher?2.2:kind==WeaponKind.Bazooka?.35:.12;
                Check(item.Prop.Action.Value>0,"RMB activates: "+kind);
                if(kind==WeaponKind.Bazooka||kind==WeaponKind.GrenadeLauncher)Check(UnityEngine.Object.FindObjectsByType<WeaponProjectile>(FindObjectsSortMode.None).Length==0,"Projectile detonates and cleans up: "+kind);
                else Check(victim.Body.linearVelocity.magnitude>.5f||Vector3.Distance(victimBefore,victim.Body.position)>.25f,"Weapon affects physical opponent: "+kind+" distance="+Vector3.Distance(human.Body.position,victim.Body.position)+" speed="+victim.Body.linearVelocity.magnitude+" after="+victim.Body.position+" human="+human.Body.position);
                WeaponSystem.Drop(human,true);Check(item.Holder.Value<0&&item.Prop.Body.linearVelocity.magnitude>8,"F throws physical weapon: "+kind);
            }
            weapons.ResetAll();r.events.Cleanup(true);r.BrokenPanels.Value=0;yield return .1;
            var panelToBreak=UnityEngine.Object.FindObjectsByType<CabinPanel>(FindObjectsSortMode.None).First(p=>p.index<16);panelToBreak.ResetHealth();panelToBreak.Damage(60);yield return .05;
            Check(panelToBreak.GetComponent<Collider>().enabled,"Floor survives sub-threshold damage");panelToBreak.Damage(440);yield return .1;
            Check(!panelToBreak.GetComponent<Collider>().enabled&&!panelToBreak.GetComponent<Renderer>().enabled,"Broken floor opens a real collider and visual hole");
            human.ResetForRound(panelToBreak.transform.position+Vector3.up*1.4f);yield return .8;
            Check(human.Body.position.y<-.7f,"Player physically falls through broken floor");
            r.StartRound();yield return .15;
            Check(r.BrokenPanels.Value==0&&panelToBreak.GetComponent<Collider>().enabled,"Restart repairs floor and resets weapons");
                        var wall=UnityEngine.Object.FindObjectsByType<CabinPanel>(FindObjectsSortMode.None).First(p=>p.index==24);
            var neighbor=UnityEngine.Object.FindObjectsByType<CabinPanel>(FindObjectsSortMode.None).First(p=>p.index==26);
            wall.Damage(CabinPanel.MaxHealth);yield return .1;
            Check(wall.GetComponentsInChildren<Collider>().All(c=>!c.enabled)&&neighbor.GetComponent<Collider>().enabled,"Only struck wall section and attached rail open a hole");
            human.ResetForRound(new Vector3(-3.75f,1.15f,wall.transform.position.z));human.Knock(Vector3.left*9,1.2f);yield return .45;
            Check(human.Body.position.x< -5.025f,"Character passes through the broken wall");
            r.StartRound();yield return .1;Check(wall.GetComponent<Collider>().enabled,"Restart repairs wall holes");
            r.Stage.Value=ElevatorStage.Event;r.doors.SetAperture(1);
            foreach(var kind in new[]{EventKind.Chicken,EventKind.GiantHand})
            {r.events.Cleanup(true);yield return .1;r.events.Force(kind);r.events.Prepare(1);r.events.Begin();yield return .1;Check(UnityEngine.Object.FindObjectsByType<NetworkProp>(FindObjectsSortMode.None).Count(p=>!p.GetComponent<WeaponPickup>())>=3,"At least three hazards: "+kind);}
            r.events.Cleanup(true);Check(errors.Count==0,"No gameplay runtime exceptions");r.enabled=true;
        }
    }
}







using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
namespace ElevatorGame.Editor
{
    [InitializeOnLoad]
    public static class SinglePlayerSmokeTests
    {
        static bool impactOnly;static IEnumerator routine;static double until;static Action drive;
        static readonly List<string> checks=new();static readonly List<string> errors=new();
        static SinglePlayerSmokeTests()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("SingleSmoke",false))
                {SessionState.SetBool("SingleSmoke",false);checks.Clear();errors.Clear();until=0;drive=null;impactOnly=SessionState.GetBool("SingleImpact",false);routine=impactOnly?RunImpacts():Run();Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}
            };
        }
        [MenuItem("Elevator/Run Single Player Tests")]
        public static void Start(){SessionState.SetBool("SingleImpact",false);SessionState.SetBool("SingleSmoke",true);EditorApplication.isPlaying=true;}
        [MenuItem("Elevator/Test Ball And Cart Impacts")] public static void StartImpacts(){SessionState.SetBool("SingleImpact",true);SessionState.SetBool("SingleSmoke",true);EditorApplication.isPlaying=true;}
        static void Log(string text,string stack,LogType type)
        {if((type==LogType.Exception||type==LogType.Error)&&!text.Contains("Subscription")&&!stack.Contains("Unity.AI."))errors.Add(text);}
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            EditorApplication.QueuePlayerLoopUpdate();drive?.Invoke();
            if(Time.time<until)return;
            try
            {
                if(routine.MoveNext()){until=Time.time+Convert.ToDouble(routine.Current);return;}
                Finish(true,"");
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        static void Check(bool value,string description){if(!value)throw new Exception(description);checks.Add("PASS "+description);Debug.Log("SINGLE_CHECK "+description);}
        static void Finish(bool pass,string message)
        {
            drive=null;Application.logMessageReceived-=Log;EditorApplication.update-=Tick;
            Directory.CreateDirectory("TestResults");
            File.WriteAllText(impactOnly?"TestResults/impact-smoke.txt":"TestResults/single-player-smoke.txt",string.Join("\n",checks)+"\n"+(pass?"PASS":"FAIL "+message)+"\nRuntime errors: "+string.Join(" | ",errors));
            Debug.Log("ELEVATOR_SINGLE_"+(pass?"PASS":"FAIL")+" "+message);Time.timeScale=1;EditorApplication.isPlaying=false;
        }
        static IEnumerator RunImpacts()
        {
            yield return .4;
            GameSession.Instance.AICount=7;GameSession.Instance.StartSinglePlayer();GameSession.Instance.DismissTutorial();
            var r=RoundManager.Instance;var players=RoundManager.Players();
            Check(players.Length==8,"Eight local physics passengers");
            foreach(var p in players){p.InputEnabled=false;if(p.AI){p.AI.enabled=false;p.AI.ResetBrain();}}
            r.events.SetSeed(42);r.enabled=false;r.Stage.Value=ElevatorStage.Event;r.StageEnds.Value=r.Clock+120;r.doors.SetAperture(1);
            yield return .6;
            r.events.Force(EventKind.BowlingBall);r.events.Prepare(1);r.events.Begin();
            yield return .63;
            var ball=UnityEngine.Object.FindObjectsByType<NetworkProp>(FindObjectsSortMode.None).First(p=>p.name.StartsWith("BowlingBall"));
            Check(ball.Body.linearVelocity.magnitude>18,"Ball approaches above 18 m/s from distant corridor");
            float arrival=(ball.Body.position.z-1.2f)/-ball.Body.linearVelocity.z; players[0].ResetForRound(new Vector3(Mathf.Clamp(ball.Body.position.x+ball.Body.linearVelocity.x*arrival,-2.4f,2.4f),1.15f,1.2f));
            bool launched=false;
            drive=()=>{if(players.Any(p=>p.Body.linearVelocity.magnitude>6||p.Body.linearVelocity.y>2.5f))launched=true;};
            yield return 3.2;
            drive=null;Check(launched,"Ball contact launches physical passengers");
            r.events.Cleanup(true);foreach(var p in players)p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));
            yield return .3;
            r.events.Force(EventKind.ShoppingCart);r.events.Prepare(1);r.events.Begin();
            bool returning=false;bool exited=false;bool cartLaunch=false;int maxCarts=0;float minCartZ=100,maxReturnSpeed=-100;
            drive=()=>{
                var carts=UnityEngine.Object.FindObjectsByType<NetworkProp>(FindObjectsSortMode.None).Where(p=>p.name.StartsWith("ShoppingCart")).ToArray();
                maxCarts=Math.Max(maxCarts,carts.Length); foreach(var c in carts){minCartZ=Mathf.Min(minCartZ,c.Body.position.z);if(c.Body.position.z<3.45f)maxReturnSpeed=Mathf.Max(maxReturnSpeed,c.Body.linearVelocity.z);}
                if(carts.Any(p=>p.Body.position.z<3.45f&&p.Body.linearVelocity.z>7))returning=true;
                if(returning&&carts.Any(p=>p.Body.position.z>5.5f&&p.Body.linearVelocity.z>7))exited=true;
                if(players.Any(p=>p.Body.linearVelocity.magnitude>5||p.Body.linearVelocity.y>2.5f))cartLaunch=true;
            };
            yield return 7.5;
            drive=null;
            Check(maxCarts>=4,"Cart event sends repeated waves");
            Check(returning,"Cart reverses inside cabin and rushes toward open door: minZ="+minCartZ+" returnSpeed="+maxReturnSpeed);
            Check(minCartZ< -1f,"Cart reaches cabin interior before turnaround");
            Check(exited,"Cart leaves through the open door after turnaround");
            Check(cartLaunch,"Cart contacts knock passengers into motion");
            Check(errors.Count==0,"No impact-test runtime errors");
            r.events.Cleanup(true);r.enabled=true;
        }
        static IEnumerator Run()
        {
            yield return .4;
            var session=GameSession.Instance;session.AICount=5;session.Difficulty=AIDifficulty.Normal;session.StartSinglePlayer();session.DismissTutorial();
            yield return 1;
            var r=RoundManager.Instance;var players=RoundManager.Players();var human=players.Single(p=>!p.IsBot.Value);
            Check(GameSession.Offline&&!NetworkManager.Singleton.IsListening,"Offline mode has no listening NetworkManager");
            Check(players.Length==6&&players.Count(p=>p.IsBot.Value)==5,"One human plus five AI");
            Check(players.All(p=>!p.Body.isKinematic)&&players.Select(p=>p.Slot.Value).Distinct().Count()==6,"Human and AI share physical prefab with unique colors");
            Check(players.All(p=>p.GetComponentsInChildren<Rigidbody>().Length==3),"Each passenger has a torso and two physical hands");
            human.InputEnabled=false;r.StageEnds.Value=r.Clock+90;r.enabled=false;r.GetComponent<WeaponSystem>().ResetAll();
            foreach(var ai in players.Where(p=>p.IsBot.Value)){ai.AI.enabled=false;ai.AI.ResetBrain();ai.ResetForRound(new Vector3(15+ai.Slot.Value*2,1,15));}
            Vector3 original=human.Body.position;
            drive=()=>human.SetMoveInput(Vector2.up,0,false,false);
            yield return .45;
            Check(human.Body.position.z-original.z>.3f,"Shared motor moves from input");
            drive=null;human.SetMoveInput(Vector2.zero,0,false,false);yield return .4;
            human.ResetForRound(new Vector3(0,1.15f,1.1f));yield return .65;
            while(!human.CanDrive)yield return .05;
            float beforeJump=human.Body.position.y;human.SetMoveInput(Vector2.zero,0,true,false);yield return .18;
            Check(human.Body.position.y>beforeJump+.25f,"Shared motor jumps: before="+beforeJump+" after="+human.Body.position.y+" stunned="+human.Stunned);
            r.StageEnds.Value=r.Clock+40;yield return .8;
            human.ResetForRound(new Vector3(-2.45f,1,-1.8f));
            drive=()=>human.SetMoveInput(Vector2.zero,-90,false,true);
            yield return 1.3;
            Check(human.HeldHands.Value>0,"Hold-to-grab acquires a cabin wall with physical hands");
            var grip=human.GetComponentsInChildren<ConfigurableJoint>().FirstOrDefault(j=>j.connectedBody!=human.Body);
            Check(grip&&grip.angularXMotion==ConfigurableJointMotion.Free&&grip.angularYMotion==ConfigurableJointMotion.Free,"Grip locks hand contact but leaves rotation free");
            var position=human.Body.position;human.Body.AddForce(Vector3.forward*3+Vector3.up,ForceMode.VelocityChange);
            yield return .3;
            Check(Vector3.Distance(position,human.Body.position)>.1f,"Torso moves while hand remains anchored");
            drive=()=>human.SetMoveInput(Vector2.zero,0,false,false);yield return .2;
            Check(human.HeldHands.Value==0,"Release frees both hands");drive=null;
            human.ResetForRound(new Vector3(0,1.15f,1));
            drive=()=>human.SetMoveInput(Vector2.zero,0,false,true,-55);
            yield return .8;
            Check(human.AimDirection.y>.7f,"Look pitch reaches shared motor");
            Check(human.Grab.leftHand.position.y>human.Body.position.y+.45f&&human.Grab.rightHand.position.y>human.Body.position.y+.45f,"Both physical hands reach upward with view");
            drive=()=>human.SetMoveInput(Vector2.zero,0,false,true,55);
            yield return .8;
            Check(human.Grab.leftHand.position.y<human.Body.position.y+.2f&&human.Grab.rightHand.position.y<human.Body.position.y+.2f,"Both hands follow downward view: torso="+human.Body.position+" left="+human.Grab.leftHand.position+" right="+human.Grab.rightHand.position+" held="+human.HeldHands.Value);
            drive=null;human.SetMoveInput(Vector2.zero,0,false,false);
            r.events.Cleanup(true);
            foreach(var p in players)p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));
            r.enabled=true;r.Stage.Value=ElevatorStage.Event;r.StageEnds.Value=r.Clock+60;
            foreach(var ai in players.Where(p=>p.IsBot.Value))ai.AI.enabled=true;
            r.events.Force(EventKind.Wind);r.events.Prepare(1);r.events.Begin();
            yield return 2;
            Check(players.Any(p=>p.IsBot.Value&&p.AI.SafeDirection.z<-.2f),"AI senses wind and chooses cabin-back direction");
            r.events.Cleanup(true);
            foreach(var p in players)p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));
            var bot=players.First(p=>p.IsBot.Value);bot.ResetForRound(new Vector3(2.65f,1,-2.4f));bot.AI.DebugGrab=true;
            r.events.Force(EventKind.Vacuum);r.events.Prepare(1);r.events.Begin();
            yield return 2;
            Check(players.Any(p=>p.IsBot.Value&&p.HeldHands.Value>0),"AI can acquire a grab under suction");
            bot.AI.DebugGrab=false;r.events.Cleanup(true);
            // Every event uses the same manager and physical scene in offline mode.
            r.Phase.Value=RoundPhase.Lobby;
            foreach(EventKind kind in Enum.GetValues(typeof(EventKind)))
            {
                foreach(var p in players)p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));
                r.events.Force(kind);r.events.Prepare(8);r.events.Begin();
                yield return kind==EventKind.Dinosaur?5:kind==EventKind.FakeEmpty?4:1.2;
                if(kind==EventKind.Dinosaur)
                {
                    var d=UnityEngine.Object.FindObjectsByType<NetworkProp>(FindObjectsSortMode.None).First(p=>p.name.StartsWith("Dinosaur"));
                    Check(d.transform.position.z<3,"Dinosaur visibly runs from corridor into cabin");
                }
                Check(UnityEngine.Object.FindObjectsByType<FloorEvent>(FindObjectsSortMode.None).Length>0,"Offline event runs: "+kind);
                r.events.Cleanup(true);yield return .1;
                Check(UnityEngine.Object.FindObjectsByType<NetworkProp>(FindObjectsSortMode.None).Count(p=>!p.GetComponent<WeaponPickup>())==0,"Offline event cleans up: "+kind);
            }
            r.StartRound();yield return .2;
            r.elimination.Eliminate(human);yield return 2;
            Check(r.Phase.Value==RoundPhase.Playing&&r.AliveCount.Value==5,"AI continue competing after human elimination");
            Check(CameraRig.Instance.Target&&CameraRig.Instance.Target.IsBot.Value,"Camera follows a living AI after human elimination");
            foreach(var p in players.Where(p=>p!=bot&&p.Alive.Value))r.elimination.Eliminate(p);
            yield return .2;
            Check(r.Phase.Value==RoundPhase.Results&&r.Winner.Value==bot.Slot.Value,"AI winner creates human loss result");
            r.StageEnds.Value=r.Clock;r.StartRound();yield return .2;
            Check(players.All(p=>p.Alive.Value)&&r.Floor.Value==1,"Play Again preserves settings and restores all six passengers");
            foreach(var p in players.Where(p=>p!=human))r.elimination.Eliminate(p);
            yield return .2;
            Check(r.Winner.Value==human.Slot.Value,"Human last survivor produces win result");
            Check(errors.Count==0,"No runtime errors or missing references");
        }
    }
}















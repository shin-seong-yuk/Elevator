using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ElevatorGame.Editor
{
    [InitializeOnLoad] public static class DynamicFloorTests
    {
        static IEnumerator routine;static double until;static readonly List<string> checks=new();
        static DynamicFloorTests(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("DynamicFloorTests",false)){SessionState.SetBool("DynamicFloorTests",false);checks.Clear();until=0;routine=Run();EditorApplication.update+=Tick;}};}
        [MenuItem("Elevator/Test Dynamic Floors")]
        public static void Start(){if(EditorApplication.isPlaying)throw new Exception("Exit Play mode first");SessionState.SetBool("DynamicFloorTests",true);EditorApplication.isPlaying=true;}
        static void Tick(){EditorApplication.QueuePlayerLoopUpdate();if(Time.time<until)return;try{if(routine.MoveNext()){until=Time.time+Convert.ToDouble(routine.Current);return;}Finish("PASS");}catch(Exception e){Finish("FAIL "+e);}}
        static void Finish(string verdict){EditorApplication.update-=Tick;Directory.CreateDirectory("TestResults");File.WriteAllText("TestResults/dynamic-floors.txt",string.Join("\n",checks)+"\n"+verdict);Debug.Log("DYNAMIC_FLOORS_"+verdict);EditorApplication.isPlaying=false;}
        static void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add("PASS "+label);}
        static NetworkProp[] Props(int index)=>UnityEngine.Object.FindObjectsByType<NetworkProp>(FindObjectsSortMode.None).Where(p=>p.PrefabIndex==index&&p.IsActive).ToArray();
        static IEnumerator Run()
        {
            yield return .5;GameSession.Instance.AICount=5;GameSession.Instance.StartSinglePlayer();GameSession.Instance.DismissTutorial();yield return .2;
            var round=RoundManager.Instance;round.enabled=false;round.doors.SetAperture(1);round.Stage.Value=ElevatorStage.Event;
            Physics.SyncTransforms();
            var cabin=GameObject.Find("Cabin");var zone=cabin.transform.Find("SafeZone").GetComponent<BoxCollider>();
            Check(Mathf.Abs(zone.bounds.size.x-10.05f)<.02f&&Mathf.Abs(zone.bounds.size.z-10.2f)<.02f&&Mathf.Abs(zone.bounds.size.y-6.2f)<.02f,"Cabin grows 1.5x horizontally while height stays unchanged");
            Check(cabin.GetComponentsInChildren<CabinPanel>().Length==40,"All breakable floor and wall sections remain connected");
            round.doors.SetAperture(0);yield return .06;Physics.SyncTransforms();
            Check(Mathf.Abs(round.doors.left.position.x+2.22f)<.02f&&Mathf.Abs(round.doors.left.position.z-4.5f)<.02f,"Expanded doors close at the new front wall");
            round.doors.SetAperture(1);yield return .06;
            var hall=GameObject.Find("Odd Hours Hotel / Floor Lobby").transform.Find("Corridor floor").GetComponent<Collider>();
            Check(Mathf.Abs(hall.bounds.min.z-4.875f)<.02f,"Lobby floor meets the expanded cabin without overlap");
            var lobby=GameObject.Find("Odd Hours Hotel / Floor Lobby").transform;
            var lintel=lobby.Find("Entry lintel").GetComponent<Collider>();
            Check(lintel.bounds.min.y<4.5f&&lintel.bounds.max.y>5.5f,"Solid lintel seals the space above the elevator door");
            var entranceText=lobby.Find("Entry plaque");var serviceText=lobby.Find("Service sign");
            Check(Mathf.Abs(entranceText.position.x)>=5.79f&&Mathf.Abs(serviceText.position.x)>=5.79f&&entranceText.position.z>4.8f&&entranceText.GetComponent<TextMesh>().characterSize<.1f,"Entrance signs fit beside the widened door frame");
            var far=lobby.Find("Lobby far wall").GetComponent<Collider>();var ceiling=lobby.Find("Lobby ceiling").GetComponent<Collider>();
            Check(far.bounds.max.y>ceiling.bounds.min.y+.05f,"Far lobby wall overlaps the ceiling to close the upper seam");
            Check(Mathf.Abs(CameraRig.MenuPosition.x)<6.8f&&CameraRig.MenuFocus.x>0,"Menu camera frames the elevator from inside the hallway");
            foreach(var panel in cabin.GetComponentsInChildren<CabinPanel>().Where(p=>p.index==30||p.index==31||p.index==38||p.index==39))
                Check(panel.GetComponent<Collider>().bounds.max.z<4.14f,"Door-side wall tile ends behind jamb: "+panel.index);
            var surrounds=lobby.GetComponentsInChildren<Transform>().Where(t=>t.name=="Entry surround").ToArray();
            Check(surrounds.Length==2&&surrounds.All(t=>Mathf.Abs(t.position.x)-t.lossyScale.x*.5f>=4.99f),"Both entrance surrounds clear the widened cabin walls");
            var runnerPrefab=round.events.propPrefabs[8];
            var runnerBody=new SerializedObject(runnerPrefab.GetComponent<Rigidbody>());
            Check(runnerBody.FindProperty("m_CenterOfMass").vector3Value.y<-.5f,"Late passenger uses a low center of mass");
            Check(round.events.definitions.Length==18&&round.events.propPrefabs.Length==18,"Hwacha event and both physical prefabs are wired into the scene");
            Check(WeaponSystem.Cooldown(WeaponKind.Hammer)>WeaponSystem.Cooldown(WeaponKind.BoxingGlove)&&WeaponSystem.Cooldown(WeaponKind.BoxingGlove)>WeaponSystem.Cooldown(WeaponKind.Bat),"Melee cooldown follows knockback strength");
            Check(Mathf.Abs(PlayerController.InputPauseSeconds-.2f)<.001f,"Input pause shortened to 0.2 seconds");
            var human=RoundManager.Players().First(p=>!p.IsBot.Value);
            human.InputEnabled=false;human.SetMoveInput(Vector2.zero,0,false,false,-48);yield return .25;
            Vector3 handAim=human.Grab.leftHand.position-human.Body.position;
            Check(handAim.y>.3f,"Free hand points along elevated view before grabbing (height="+handAim.y+", aim="+human.AimDirection+")");
            round.events.Force(EventKind.Flood);round.events.Prepare(8);round.events.Begin();yield return .2;
            var water=Props(6).Single().GetComponentInChildren<Renderer>();
            Check(water.bounds.size.x>9.3f&&water.bounds.size.z>9.3f,"Flood surface covers the widened cabin");
            round.events.Cleanup(true);yield return .15;
            human.ResetForRound(new Vector3(4.1f,1.15f,0));
            round.events.Force(EventKind.Wind);round.events.Prepare(8);round.events.Begin();yield return .4;
            Check(human.Body.linearVelocity.z>1.5f,"Wind pushes a passenger standing near the widened side wall");
            round.events.Cleanup(true);yield return .15;
            var players=RoundManager.Players();
            foreach(var p in players){p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));if(p.AI)p.AI.enabled=false;if(p!=human&&p!=players[1])p.Alive.Value=false;}
            human.ResetForRound(new Vector3(0,1.2f,-1));players[1].ResetForRound(new Vector3(4,1.2f,-3.8f));
            round.events.Force(EventKind.Dinosaur);round.events.Prepare(8);round.events.Begin();
            var dino=UnityEngine.Object.FindFirstObjectByType<DinosaurEvent>();
            yield return 7.5;
            Check(dino.SweepsStarted>=2&&(dino.HasHit(human)||dino.HasHit(players[1])),"Moving dinosaur repeatedly sweeps its tail toward living passengers");
            Check(Props(2).Single().GetComponents<SpringJoint>().Length==0,"Dinosaur never bites or attaches a player");
            round.events.Cleanup(true);yield return .15;
            human.ResetForRound(new Vector3(0,1.2f,1));players[1].ResetForRound(new Vector3(1.2f,1.2f,.5f));
            round.events.Force(EventKind.Gorilla);round.events.Prepare(8);round.events.Begin();yield return 5.5;
            var gorilla=UnityEngine.Object.FindFirstObjectByType<GorillaEvent>();
            Check(Props(3).Single().Body.position.z<3&&gorilla.Strikes>0,"Gorilla enters cabin and swings at passengers");
            round.events.Cleanup(true);yield return .15;
            foreach(var p in players)p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));
            round.events.Force(EventKind.Hwacha);round.events.Prepare(8);round.events.Begin();yield return 1.8;
            Check(Props(16).Length==1&&Props(17).Length==0,"Hwacha waits visibly for two seconds before firing");
            yield return 2.7;
            var hwacha=UnityEngine.Object.FindFirstObjectByType<HwachaEvent>();
            Check(hwacha.Fired==50&&Props(17).Length==50,"Hwacha fires fifty separate physical arrows");
            var arrow=Props(17).First().GetComponent<HwachaArrow>();
            arrow.Stick(human,human.Body.position+Vector3.up*.3f,Quaternion.identity);
            Vector3 beforeArrow=arrow.transform.position;
            human.Body.position+=Vector3.right;
            yield return .08;
            Check(arrow.StuckSlot.Value==human.Slot.Value&&Vector3.Distance(beforeArrow,arrow.transform.position)>.6f,"Arrow stays embedded while passenger continues moving");
            round.events.Cleanup(true);yield return .15;
            foreach(var p in players)p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));
            round.events.Force(EventKind.FlyingFish);round.events.Prepare(8);round.events.Begin();yield return .12;
            Check(Props(11).Length==10,"Ten separate colliding fish spawn");
            yield return 3.7;
            Check(Props(11).Any(p=>p.Body.position.z<2),"Flopping fish leap through elevator doorway (closest="+Props(11).Min(p=>p.Body.position.z)+", height="+Props(11).Min(p=>p.Body.position.y)+", speed="+Props(11).Max(p=>p.Body.linearVelocity.magnitude)+")");
            var fishEvent=UnityEngine.Object.FindFirstObjectByType<FlyingFishEvent>();int fishFlaps=fishEvent.Flaps;
            yield return 1.5;Check(fishEvent.Flaps>fishFlaps+10,"Fish keep flapping and chasing throughout the floor");
            round.events.Cleanup(true);yield return .15;
            Check(Props(11).Length==0,"Fish clean up at floor end");
            round.events.Force(EventKind.Chicken);round.events.Prepare(8);round.events.Begin();yield return 2;
            var chickenEvent=UnityEngine.Object.FindFirstObjectByType<ChickenEvent>();int chickenFlaps=chickenEvent.Flaps;
            yield return 1.5;Check(chickenEvent.Flaps>chickenFlaps+5&&Props(4).Length==6,"Chickens keep flapping and chasing throughout the floor");
            round.events.Cleanup(true);yield return .15;
            round.events.Force(EventKind.Tank);round.events.Prepare(8);round.events.Begin();yield return 2.6;
            Check(Props(12).Length==1&&Props(12)[0].Action.Value>=1,"Tank drives up and fires a physical shell");
            Check(Props(13).Length>0||CombatFeedback.ImpactCount>0,"Shell exists or has exploded with feedback");
            round.events.Cleanup(true);yield return .15;
            Check(Props(12).Length==0&&Props(13).Length==0,"Tank and shell clean up");
            round.events.Force(EventKind.Ufo);round.events.Prepare(8);round.events.Begin();yield return 3;
            Check(Props(14).Length==1&&Props(14)[0].Body.position.z<3,"UFO flies into the lift entrance");
            Check(Props(14)[0].Action.Value>0,"UFO beam pulses during attack");
            round.events.Cleanup(true);yield return .15;
            round.events.Force(EventKind.GrandPiano);round.events.Prepare(8);round.events.Begin();yield return 3.5;
            Check(Props(15).Length>=2,"Multiple physical grand pianos enter the floor");
            Check(Props(15).Any(p=>p.Body.position.z<5),"A piano travels toward the elevator");
            round.events.Cleanup(true);yield return .15;
            var runner=round.events.SpawnProp(8,new Vector3(0,2,8),Vector3.zero,false);
            runner.Body.useGravity=false;runner.Body.rotation=Quaternion.Euler(0,0,65);runner.Body.angularVelocity=Vector3.zero;
            yield return .8;
            Check(Vector3.Angle(runner.transform.up,Vector3.up)<25,"Late passenger rights itself after a hard lean");
            runner.Remove();yield return .1;
            foreach(var p in RoundManager.Players())p.ResetForRound(RoundManager.SpawnPoint(p.Slot.Value));
            round.Stage.Value=ElevatorStage.Moving;round.StageEnds.Value=round.Clock+8;
            round.enabled=true;round.RestoreDrop(.1,1.5,false,false);
            yield return .32;Check(round.DropLaunched,"Travel drop lifts passengers");
            yield return 1.45;Check(round.DropLanded,"Travel drop ends with a landing impulse");
            Check(round.Phase.Value==RoundPhase.Playing,"Drop remains part of the shared round");
        }
    }
}

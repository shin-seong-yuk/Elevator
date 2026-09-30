using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ElevatorGame.Editor
{
    [InitializeOnLoad] public static class LobbyCharacterTests
    {
        static IEnumerator routine;static double until;static readonly List<string> checks=new();
        static LobbyCharacterTests(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("LobbyCharacter",false)){SessionState.SetBool("LobbyCharacter",false);checks.Clear();until=0;routine=Run();EditorApplication.update+=Tick;}};}
        [MenuItem("Elevator/Test Lobby And Character")]public static void Start(){if(EditorApplication.isPlaying)throw new Exception("Exit play mode first");SessionState.SetBool("LobbyCharacter",true);EditorApplication.isPlaying=true;}
        static void Tick(){EditorApplication.QueuePlayerLoopUpdate();if(Time.time<until)return;try{if(routine.MoveNext()){until=Time.time+Convert.ToDouble(routine.Current);return;}Finish("PASS");}catch(Exception e){Finish("FAIL "+e);}}
        static void Finish(string result){EditorApplication.update-=Tick;Directory.CreateDirectory("TestResults");File.WriteAllText("TestResults/lobby-character.txt",string.Join("\n",checks)+"\n"+result);EditorApplication.isPlaying=false;}
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
        static IEnumerator Run()
        {
            yield return .5;NetworkGameManager.Instance.CreateRoom();yield return 1;
            var r=RoundManager.Instance;var p=RoundManager.Players().Single();p.InputEnabled=false;
            Check(p.IsLobbyHost&&!p.Ready.Value,"Host role does not require ready flag");
            p.ToggleReady();Check(r.Phase.Value==RoundPhase.Lobby&&!r.CanStart,"Host R cannot start alone");
            Check(CameraRig.Instance.Target==p,"Lobby camera follows local passenger");
            p.ResetForRound(new Vector3(0,1.2f,0));var start=p.Body.position;
            for(int i=0;i<12;i++){p.SetMoveInput(Vector2.right,0,false,false);yield return .08;}
            Check(p.Body.position.x>start.x+.25f,"Shared physical motor moves in waiting room");
            Check(r.Floor.Value==0&&UnityEngine.Object.FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None).Length==0,"Waiting does not start floors or weapons");
            p.Body.position=new Vector3(0,-4,6);yield return .2;
            Check(p.Alive.Value&&p.Body.position.y>0,"Escaped lobby player returns alive");
            NetworkGameManager.Instance.AddDummy();yield return .2;var bot=RoundManager.Players().First(a=>a.IsBot.Value);bot.AI.enabled=false;bot.Ready.Value=false;
            p.ToggleReady();Check(r.Phase.Value==RoundPhase.Lobby&&!r.CanStart,"Host R blocked by unready passenger");
            bot.Ready.Value=true;Check(r.CanStart&&r.Phase.Value==RoundPhase.Lobby,"Readiness alone never auto-starts");
            p.ToggleReady();Check(r.Phase.Value==RoundPhase.Playing,"Host R starts when all others ready");r.enabled=false;
            p.ResetForRound(new Vector3(0,1.2f,0));p.Body.isKinematic=true;
            foreach(var panel in UnityEngine.Object.FindObjectsByType<CabinPanel>(FindObjectsSortMode.None).Where(a=>a.index==0||a.index==24))
            {
                panel.ResetHealth();Check(panel.Health==500,"Panel starts at 5x health: "+panel.index);
                panel.Damage(499);yield return .05;Check(panel.GetComponent<Collider>().enabled&&panel.Health==1,"499 damage preserves section: "+panel.index);
                panel.Damage(1);yield return .05;Check(!panel.GetComponent<Collider>().enabled,"500 damage opens section: "+panel.index);
            }
            r.BrokenPanels.Value=0;yield return .1;
            Check(Mathf.Abs(p.Grab.reach-.725f)<.001f,"Serialized grab reach halved");
            foreach(var hand in new[]{p.Grab.leftHand,p.Grab.rightHand})Check(Mathf.Abs(hand.GetComponent<ConfigurableJoint>().linearLimit.limit-.475f)<.001f,"Physical arm length halved");
            r.GetComponent<WeaponSystem>().ResetAll();yield return .1;
            var w=r.events.SpawnProp(9,new Vector3(0,1,0),Vector3.zero,true).GetComponent<WeaponPickup>();w.Holder.Value=p.Slot.Value;w.Kind.Value=(int)WeaponKind.Pistol;
            p.SetMoveInput(Vector2.zero,40,false,true,-25);
            yield return .25;
            p.SetMoveInput(Vector2.zero,40,false,true,-25);yield return .05;
            var arm=p.Grab.rightHand.GetComponent<ConfigurableJoint>();
            Check(arm.xMotion==ConfigurableJointMotion.Locked&&!p.Grab.RightHeld,"Weapon hand holds forward instead of grabbing");
            Check(Vector3.Distance(p.Grab.rightHand.position,p.Grab.WeaponHandPosition)<.12f,"Weapon hand tracks aimed forward position physically");
            Check(Vector3.Distance(w.GripPosition,p.Grab.WeaponHandPosition)<.1f,"Weapon handle aligns with shortened hand");
            WeaponSystem.Drop(p,false);yield return .05;
            Check(arm.xMotion==ConfigurableJointMotion.Limited,"Dropping weapon restores flexible arm");
            p.Body.isKinematic=false;r.Phase.Value=RoundPhase.Results;r.StageEnds.Value=r.Clock;r.ReturnToLobby();yield return .1;
            Check(r.Phase.Value==RoundPhase.Lobby&&!p.Ready.Value&&bot.Ready.Value,"Return resets human readiness and preserves bot readiness");
        }
    }
}

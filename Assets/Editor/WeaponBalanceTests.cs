using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace ElevatorGame.Editor
{
    [InitializeOnLoad] public static class WeaponBalanceTests
    {
        static IEnumerator routine;static double until;static readonly List<string> checks=new();
        static WeaponBalanceTests(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("WeaponBalance",false)){SessionState.SetBool("WeaponBalance",false);checks.Clear();until=0;routine=Run();EditorApplication.update+=Tick;}};}
        [MenuItem("Elevator/Test Weapon Balance")]public static void Start(){SessionState.SetBool("WeaponBalance",true);EditorApplication.isPlaying=true;}
        static void Tick(){EditorApplication.QueuePlayerLoopUpdate();if(Time.time<until)return;try{if(routine.MoveNext()){until=Time.time+Convert.ToDouble(routine.Current);return;}Finish("PASS");}catch(Exception e){Finish("FAIL "+e);}}
        static void Finish(string result){EditorApplication.update-=Tick;File.WriteAllText("TestResults/weapon-balance.txt",string.Join("\n",checks)+"\n"+result);EditorApplication.isPlaying=false;}
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
        static IEnumerator Run()
        {
            yield return .5;GameSession.Instance.AICount=3;GameSession.Instance.StartSinglePlayer();GameSession.Instance.DismissTutorial();
            var r=RoundManager.Instance;r.enabled=false;var weapons=r.GetComponent<WeaponSystem>();
            var p=RoundManager.Players()[0];foreach(var a in RoundManager.Players()){a.InputEnabled=false;a.AI.enabled=false;a.enabled=false;a.Body.isKinematic=true;}
            p.SetMoveInput(Vector2.zero,0,false,false,-65);
            foreach(WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                weapons.ResetAll();r.events.Cleanup(true);yield return .1;
                var w=r.events.SpawnProp(9,new Vector3(0,1,0),Vector3.zero,true).GetComponent<WeaponPickup>();w.Kind.Value=(int)kind;w.Holder.Value=p.Slot.Value;
                int capacity=w.Capacity;WeaponSystem.Use(p);int first=w.Prop.Action.Value;
                Check(first==1,"First use accepted: "+kind);
                Check(w.RemainingAmmo==(capacity<0?-1:capacity-1),"One charge per attack including shotgun pellets: "+kind);
                WeaponSystem.Use(p);Check(w.Prop.Action.Value==first&&w.RemainingAmmo==(capacity<0?-1:capacity-1),"Cooldown prevents duplicate attack/ammo drain: "+kind);
                yield return WeaponSystem.Cooldown(kind)+.04f;
                if(capacity>0)
                {
                    w.RestoreAmmo(1);WeaponSystem.Use(p);Check(w.RemainingAmmo==0,"Last round consumed: "+kind);
                    int last=w.Prop.Action.Value;yield return WeaponSystem.Cooldown(kind)+.04f;
                    WeaponSystem.Use(p);Check(w.Prop.Action.Value==last&&w.RemainingAmmo==0,"Empty weapon cannot attack: "+kind);
                    WeaponSystem.Drop(p,true);Check(!w.HasAmmo&&w.RemainingAmmo==0,"Throwing does not refill: "+kind);
                    w.Prop.Body.position=p.Body.position;WeaponSystem.PickupOrThrow(p);Check(WeaponSystem.Held(p)==w&&w.RemainingAmmo==0,"Picking up does not refill: "+kind);
                }
                else
                {
                    Check(WeaponSystem.Cooldown(kind)==1,"Melee interval is one second: "+kind);
                    WeaponSystem.Use(p);Check(w.Prop.Action.Value==first+1&&w.RemainingAmmo==-1,"Held melee repeats after one second without ammo: "+kind);
                }
            }
        }
    }
}

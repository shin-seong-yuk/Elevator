using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ElevatorGame.Editor
{
    [InitializeOnLoad] public static class WeaponPresentationSmokeTests
    {
        static IEnumerator routine;static double until;static readonly List<string> checks=new(),errors=new();
        static WeaponPresentationSmokeTests(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("WeaponPresentationTests",false)){SessionState.SetBool("WeaponPresentationTests",false);checks.Clear();errors.Clear();until=0;routine=Run();EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}};}
        [MenuItem("Elevator/Test Weapon Presentation And AI")]public static void Start(){SessionState.SetBool("WeaponPresentationTests",true);EditorApplication.isPlaying=true;}
        static void Log(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error)&&!stack.Contains("Unity.AI.")&&!message.Contains("Subscription"))errors.Add(message);}
        static void Tick(){if(!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();try{if(Time.time<until)return;if(routine.MoveNext()){until=Time.time+Convert.ToDouble(routine.Current);return;}Finish(true,"");}catch(Exception e){Finish(false,e.ToString());}}
        static void Check(bool pass,string message){if(!pass)throw new Exception(message);checks.Add("PASS "+message);Debug.Log("WEAPON_AI_CHECK "+message);}
        static void Finish(bool pass,string message){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;File.WriteAllText("TestResults/weapon-presentation-smoke.txt",string.Join("\n",checks)+"\n"+(pass?"PASS":"FAIL "+message)+"\nRuntime errors: "+string.Join(" | ",errors));EditorApplication.isPlaying=false;}
        static IEnumerator Run()
        {
            yield return .4;var session=GameSession.Instance;session.AICount=3;session.Difficulty=AIDifficulty.Hard;session.StartSinglePlayer();session.DismissTutorial();
            var round=RoundManager.Instance;round.enabled=false;
            var actors=RoundManager.Players();var target=actors[0];
            foreach(var p in actors){p.InputEnabled=false;p.AI.enabled=false;p.ResetForRound(new Vector3(15+p.Slot.Value*2,1,15));}
            session.AddAI();yield return .1;
            var bot=RoundManager.Players().Last();Check(bot.IsBot.Value,"Debug-added dummy uses shared AI controller");
            var weapons=round.GetComponent<WeaponSystem>();
            foreach(WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                bot.AI.enabled=false;weapons.ResetAll();round.events.Cleanup(true);round.BrokenPanels.Value=0;
                foreach(var panel in UnityEngine.Object.FindObjectsByType<CabinPanel>(FindObjectsSortMode.None))panel.ResetHealth();
                yield return .1;
                bool ranged=kind==WeaponKind.Bazooka||kind==WeaponKind.GrenadeLauncher||kind==WeaponKind.Pistol||kind==WeaponKind.Shotgun||kind==WeaponKind.Grappler;
                bot.ResetForRound(new Vector3(-1.3f,1.15f,-1));target.ResetForRound(new Vector3(ranged?1.8f:.35f,1.15f,-1));
                target.Body.isKinematic=true;
                var item=round.events.SpawnProp(9,bot.Body.position+Vector3.forward*.65f,Vector3.zero,true).GetComponent<WeaponPickup>();item.Kind.Value=(int)kind;item.BornFloor.Value=round.Floor.Value;
                yield return .15;
                Check(item.transform.Find("Weapon model").childCount>=8,"Detailed model generated: "+kind);
                bot.AI.enabled=true;
                float deadline=Time.time+6;while(bot.AI.WeaponUses==0&&Time.time<deadline)yield return .1;
                Check(bot.AI.WeaponsPickedUp>0,"AI finds and picks up: "+kind);
                Check(bot.AI.WeaponUses>0&&item.Prop.Action.Value>0,"AI aims and activates: "+kind);
                Check(Vector3.Dot(bot.AimDirection,(target.Body.position-bot.Body.position).normalized)>.75f,"AI faces opponent when using: "+kind);
                bot.AI.enabled=false;bot.AI.ResetBrain();target.Body.isKinematic=false;
            }
            Check(CombatFeedback.ImpactCount>0,"Confirmed physics impacts generate feedback");
            // Controlled miss must not be presented as a confirmed impact.
            weapons.ResetAll();round.events.Cleanup(true);yield return .1;
            bot.ResetForRound(new Vector3(0,1.15f,0));target.ResetForRound(new Vector3(15,1,15));
            var bat=round.events.SpawnProp(9,new Vector3(0,1.2f,.4f),Vector3.zero,true).GetComponent<WeaponPickup>();bat.Kind.Value=0;
            WeaponSystem.PickupOrThrow(bot);bot.SetMoveInput(Vector2.zero,0,false,false);int hits=CombatFeedback.ImpactCount;WeaponSystem.Use(bot);
            Check(CombatFeedback.ImpactCount==hits,"Miss does not trigger hit feedback");
            yield return .6;
            Check(UnityEngine.Object.FindObjectsByType<CombatFeedback>(FindObjectsSortMode.None).Length==0,"Transient effects clean up");
            Check(errors.Count==0,"No runtime exceptions");round.enabled=true;
        }
    }
}

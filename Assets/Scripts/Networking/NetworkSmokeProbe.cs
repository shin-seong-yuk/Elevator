#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
namespace ElevatorGame
{
    public sealed class NetworkSmokeProbe : MonoBehaviour
    {
        string report;
        float next, started=-1, installedAt;
        int step,weaponStep;
        bool readySent,connectedOnce; int lobbyStep; float lobbyAt=-1;
        static MethodInfo input;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-elevatorReport");
            if(index<0||index+1>=args.Length)return;
            var probe=new GameObject("Network Test Probe").AddComponent<NetworkSmokeProbe>();
            probe.installedAt=Time.realtimeSinceStartup;probe.report=args[index+1];Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(probe.report)));
            File.WriteAllText(probe.report,"");
            Application.runInBackground=true;Application.targetFrameRate=60;
            input=typeof(PlayerController).GetMethod("InputServerRpc",BindingFlags.Instance|BindingFlags.NonPublic);
        }
        void Update()
        {
            if(Time.realtimeSinceStartup-installedAt>120){File.AppendAllText(report,"COMPLETE\n");Application.Quit();return;}
            var manager=NetworkManager.Singleton;var round=RoundManager.Instance;
            if(manager&&manager.IsConnectedClient)connectedOnce=true;
            if(connectedOnce&&manager&&!manager.IsListening){File.AppendAllText(report,"COMPLETE DISCONNECTED\n");Application.Quit();return;}
            if(!manager||!round||!round.IsSpawned)return;
            var players=RoundManager.Players();var local=players.FirstOrDefault(p=>p.ControlledLocally);
            if(local&&round.Phase.Value==RoundPhase.Lobby&&step==0)
            {
                local.InputEnabled=false;
                if(players.Length>=2&&lobbyAt<0)lobbyAt=Time.time;
                if(lobbyAt>=0)
                {
                    float elapsed=Time.time-lobbyAt;
                    if(manager.IsHost&&lobbyStep==0&&elapsed>.2f)
                    {local.ReadyServerRpc();if(round.Phase.Value!=RoundPhase.Lobby)throw new Exception("Host started before client readiness");File.AppendAllText(report,"PASS HOST BLOCKED BEFORE READY\n");lobbyStep++;}
                    if(!manager.IsHost&&((lobbyStep==0&&elapsed>1)||(lobbyStep==1&&elapsed>2)||(lobbyStep==2&&elapsed>3)))
                    {local.ReadyServerRpc();lobbyStep++;File.AppendAllText(report,"READY ACTION "+lobbyStep+"\n");}
                    if(manager.IsHost&&lobbyStep==1&&elapsed>2.6f&&elapsed<3&&players.Any(p=>!p.IsLobbyHost&&!p.Ready.Value))
                    {File.AppendAllText(report,"PASS CANCEL REPLICATED\n");lobbyStep++;}
                }
            }
            if(manager.IsHost&&step==0&&players.Length>=2&&round.CanStart&&lobbyAt>=0&&Time.time-lobbyAt>3.5f)
            {local.ReadyServerRpc();File.AppendAllText(report,"PASS HOST R START\n");started=Time.time;step=1;}
            if(manager.IsHost&&started>=0)
            {
                float elapsed=Time.time-started;
                if(step==1&&elapsed>2)
                {
                    round.events.Cleanup(true);round.events.Force(EventKind.ShoppingCart);round.events.Prepare(8);
                    round.EventTitle.Value=new Unity.Collections.FixedString128Bytes(round.events.Title);
                    round.Stage.Value=ElevatorStage.Event;round.StageEnds.Value=manager.ServerTime.Time+20;round.events.Begin();step=2;
                }
                if(weaponStep==0&&elapsed>3&&local)
                {var w=FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None).FirstOrDefault();if(w){w.Kind.Value=4;w.Prop.Body.position=local.Body.position+Vector3.forward*.5f;}weaponStep=1;}
                if(weaponStep==1&&elapsed>3.4f&&local&&local.CanDrive){local.WeaponAction(true,false);if(WeaponSystem.Held(local))weaponStep=2;}
                if(weaponStep==2&&elapsed>4&&local&&local.CanDrive){local.WeaponAction(false,true);foreach(var panel in FindObjectsByType<CabinPanel>(FindObjectsSortMode.None))if(panel.index==17||panel.index==15)panel.Damage(CabinPanel.MaxHealth);weaponStep=3;}
                if(step==2&&elapsed>8)
                {
                    foreach(var p in players.Where(p=>p!=local))round.elimination.Eliminate(p);
                    step=3;
                }
                if(step==3&&elapsed>15){round.StartRound();step=4;}
                if(step==4&&elapsed>19)
                {
                    File.AppendAllText(report,"COMPLETE\n");manager.Shutdown();Application.Quit();step=5;
                }
            }
            if(Time.time>=next)
            {
                next=Time.time+.2f;
                string state=$"STATE server={manager.IsServer} t={manager.ServerTime.Time:F2} phase={round.Phase.Value} floor={round.Floor.Value} stage={round.Stage.Value} seed={round.Seed.Value} alive={round.AliveCount.Value} winner={round.Winner.Value} props={FindObjectsByType<NetworkProp>(FindObjectsSortMode.None).Length} weapons={FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None).Length} held={string.Join(",",FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None).Select(w=>w.Holder.Value))} broken={round.BrokenPanels.Value} feedback={CombatFeedback.ImpactCount} projectiles={FindObjectsByType<WeaponProjectile>(FindObjectsSortMode.None).Length} players={players.Length}";
                string positions=string.Join(";",players.Select(p=>$"{p.Slot.Value}:{p.transform.position.x:F2},{p.transform.position.y:F2},{p.transform.position.z:F2}:alive={p.Alive.Value}:kinematic={p.Body.isKinematic}"));
                File.AppendAllText(report,state+" "+positions+"\n");
            }
            if(!manager.IsHost&&Time.realtimeSinceStartup-installedAt>120){File.AppendAllText(report,"COMPLETE\n");Application.Quit();}
        }
        void LateUpdate()
        {
            var p=RoundManager.Players().FirstOrDefault(p=>p.ControlledLocally);
            if(p&&RoundManager.Instance.Phase.Value!=RoundPhase.Results)
            {
                // Exercise the exact owning-client RPC used by normal keyboard input.
                var axis=new Vector2(Mathf.Sin(Time.time)*.35f,0);
                input?.Invoke(p,new object[]{axis,0f,false,false,false,-25f});
            }
        }
    }
}
#endif








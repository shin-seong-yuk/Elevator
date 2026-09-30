#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ElevatorGame
{
    // Three real NGO processes exercise checkpoint transfer across a killed host using LAN.
    // Steam membership / relay remain a separate multi-account integration test.
    public sealed class MigrationNetworkProbe : MonoBehaviour
    {
        int node;string report;float deadline;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-migrationNode");
            if(index<0||index+1>=args.Length||FindFirstObjectByType<MigrationNetworkProbe>())return;
            var p=new GameObject("Migration network test").AddComponent<MigrationNetworkProbe>();DontDestroyOnLoad(p.gameObject);p.node=int.Parse(args[index+1]);p.report="TestResults/migration-node-"+p.node+".txt";File.WriteAllText(p.report,"");p.deadline=Time.realtimeSinceStartup+180;p.StartCoroutine(p.Run());
        }
        void Log(string value){File.AppendAllText(report,value+"\n");}
        void Check(bool ok,string label){Log((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);}
        void Update(){if(Time.realtimeSinceStartup>deadline){Log("FAIL timeout");Application.Quit();}}
        IEnumerator Run()
        {
            yield return null;var net=NetworkGameManager.Instance;net.port=7790;
            if(node==0)net.CreateRoom();else net.JoinRoom();
            while(!NetworkManager.Singleton.IsConnectedClient)yield return null;
            Log("CONNECTED "+NetworkManager.Singleton.LocalClientId);
            if(node==0)
            {
                while(RoundManager.Players().Length<3)yield return null;
                var r=RoundManager.Instance;foreach(var p in RoundManager.Players()){p.Ready.Value=true;p.SteamId.Value=(ulong)(101+p.Slot.Value);p.InputEnabled=false;}
                r.StartRound();r.enabled=false;yield return new WaitForSeconds(.3f);
                var players=RoundManager.Players();players[1].ResetForRound(new Vector3(-2.35f,1.15f,-.8f));players[2].ResetForRound(new Vector3(-1.2f,1.15f,-.8f));
                foreach(var p in players)p.InputEnabled=false;
                var rail=FindObjectsByType<GrabAnchor>(FindObjectsSortMode.None).First(a=>a.name=="Side handrail"&&a.GetComponentInParent<CabinPanel>().index==26).GetComponent<Collider>();
                players[1].enabled=false;players[1].Grab.Attach(0,rail,rail.ClosestPoint(players[1].Grab.leftHand.position));
                r.GetComponent<WeaponSystem>().ResetAll();yield return null;
                var w=r.events.SpawnProp(9,players[1].Body.position+Vector3.forward*.5f,Vector3.zero,true).GetComponent<WeaponPickup>();w.Kind.Value=2;w.RestoreAmmo(3);w.Holder.Value=1;w.BornFloor.Value=6;
                r.Floor.Value=8;r.BrokenPanels.Value=(1UL<<3)|(1UL<<24);r.StageEnds.Value=r.Clock+30;
                var state=MigrationSnapshot.Capture(50,1);File.WriteAllBytes("TestResults/migration-transfer.bin",SteamSession.Pack(state));Log("READY_TO_KILL");
                while(true)yield return null;
            }
            while(NetworkManager.Singleton&&NetworkManager.Singleton.IsConnectedClient)yield return null;
            Log("OLD_HOST_DISCONNECTED");var old=NetworkManager.Singleton;if(old){old.Shutdown();while(old&&old.ShutdownInProgress)yield return null;if(old)Destroy(old.gameObject);}yield return null;
            yield return SceneManager.LoadSceneAsync("Elevator");yield return null;
            net=NetworkGameManager.Instance;net.port=7791;net.SuppressAutomaticSpawning=true;
            if(node==1){net.CreateRoom();RoundManager.Instance.enabled=false;File.WriteAllText("TestResults/migration-new-host.ready","ready");}
            else {while(!File.Exists("TestResults/migration-new-host.ready"))yield return null;net.JoinRoom();}
            while(!NetworkManager.Singleton.IsConnectedClient)yield return null;
            if(node==1)
            {
                while(NetworkManager.Singleton.ConnectedClientsIds.Count<2)yield return null;
                var state=SteamSession.Unpack(File.ReadAllBytes("TestResults/migration-transfer.bin"));state.Restore(id=>id==102?0UL:id==103?1UL:(ulong?)null);
                var r=RoundManager.Instance;Check(r.Floor.Value==8&&r.BrokenPanels.Value==state.broken,"New authority restores floor and holes");
                Check(RoundManager.Players().Length==2&&!RoundManager.Players().Any(p=>p.SteamId.Value==101),"Departed host removed; two survivors retained");
                Check(RoundManager.Players().First(p=>p.SteamId.Value==102).OwnerClientId==0,"New host owns its original character");
                Check(WeaponSystem.Held(RoundManager.Players().First(p=>p.SteamId.Value==102)).BornFloor.Value==6,"Held weapon age restored");
                Check(RoundManager.Players().First(p=>p.SteamId.Value==102).Grab.LeftHeld,"Grab reconstructed on new authority");
                net.SuppressAutomaticSpawning=false;foreach(var p in RoundManager.Players()){p.InputEnabled=false;p.enabled=false;}r.enabled=true;
                yield return new WaitForSeconds(6);Log("COMPLETE");NetworkManager.Singleton.Shutdown();Application.Quit();
            }
            else
            {
                while(RoundManager.Players().Length<2||RoundManager.Instance.Floor.Value!=8)yield return null;
                var players=RoundManager.Players();Check(players.Any(p=>p.ControlledLocally&&p.SteamId.Value==103),"Reconnected client owns original character");
                Check(RoundManager.Instance.BrokenPanels.Value==((1UL<<3)|(1UL<<24)),"Restored holes reach client");
                yield return new WaitForSeconds(.3f);
                Check(FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None).Any(w=>w.Holder.Value==1&&w.BornFloor.Value==6&&w.RemainingAmmo==3),"Restored weapon ownership and ammo reach client");
                Check(players.All(p=>p.Body.isKinematic),"Client remains non-authoritative after migration");
                Log("COMPLETE");while(NetworkManager.Singleton.IsListening)yield return null;Application.Quit();
            }
        }
    }
}
#endif

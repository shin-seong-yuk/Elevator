using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ElevatorGame
{
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance {get;private set;}
        public float Yaw {get;private set;}
        public float Pitch => pitch;
        float pitch=18, deathAt=-1;
        int spectator;
        bool free;
        bool following;
        RoundPhase previousPhase;
        Vector3 freePosition;
        public PlayerController Target {get;private set;}
        void Awake(){Instance=this;}
        public void ResetView(){Yaw=0;pitch=18;deathAt=-1;free=false;}
        void LateUpdate()
        {
            var round=RoundManager.Instance;
            var all=RoundManager.Players();
            var local=all.FirstOrDefault(p=>p.ControlledLocally);
            bool playing=round && round.IsActive && local;
            if(!playing)
            {
                following=false;Target=null;
                transform.position=Vector3.Lerp(transform.position,new Vector3(7.2f,4.2f,14),Time.deltaTime*3);
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(new Vector3(0,1.8f,1)-transform.position),Time.deltaTime*3);
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return;
            }
            if(!following||(previousPhase!=RoundPhase.Lobby&&round.Phase.Value==RoundPhase.Lobby))
            {ResetView();Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            following=true;previousPhase=round.Phase.Value;
            var keyboard=Keyboard.current;
            if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame)
            {bool locked=Cursor.lockState==CursorLockMode.Locked;Cursor.lockState=locked?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=locked;}
            if(round.Phase.Value==RoundPhase.Results){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            if(local && !local.Alive.Value)
            {
                if(deathAt<0) {deathAt=Time.time;AudioManager.Instance?.Play("out");}
                if(Time.time-deathAt>1.6f)
                {
                    var living=all.Where(p=>p.Alive.Value).ToArray();
                    if(keyboard!=null)
                    {
                        if(keyboard.eKey.wasPressedThisFrame)spectator++;
                        if(keyboard.qKey.wasPressedThisFrame)spectator--;
                        if(keyboard.fKey.wasPressedThisFrame){free=!free;freePosition=transform.position;}
                    }
                    Target=living.Length>0?living[(spectator%living.Length+living.Length)%living.Length]:null;
                }
                else Target=local;
            }
            else {Target=local;deathAt=-1;}
            if(Mouse.current!=null && Cursor.lockState==CursorLockMode.Locked)
            {
                var delta=Mouse.current.delta.ReadValue();
                Yaw+=delta.x*.12f;pitch=Mathf.Clamp(pitch-delta.y*.1f,-65,75);
            }
            Quaternion orbit=Quaternion.Euler(pitch,Yaw,0);
            if(free && keyboard!=null)
            {
                Vector3 movement=new((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.spaceKey.isPressed?1:0)-(keyboard.leftCtrlKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
                freePosition+=orbit*movement*Time.deltaTime*5;transform.SetPositionAndRotation(freePosition,orbit);return;
            }
            if(!Target)return;
            Vector3 focus=Target.transform.position+Vector3.up*.92f+orbit*Vector3.forward*.45f;
            Vector3 desired=focus-orbit*Vector3.forward*3.5f;
            if(round.DropActive)
                desired+=new Vector3(Mathf.Sin(Time.unscaledTime*37),Mathf.Sin(Time.unscaledTime*49),0)*(.035f+.06f*round.DropProgress);
            Vector3 direction=(desired-focus).normalized;
            // Cabin/props only: own and other character colliders do not push the camera into the head.
            if(UnityEngine.Physics.SphereCast(focus,.16f,direction,out var hit,3.5f,1<<8|1<<10,QueryTriggerInteraction.Ignore))
                desired=focus+direction*Mathf.Max(.25f,hit.distance-.12f);
            transform.position=Vector3.Lerp(transform.position,desired,1-Mathf.Exp(-Time.deltaTime*14));
            transform.rotation=Quaternion.LookRotation(focus-transform.position,Vector3.up);
        }
    }
}







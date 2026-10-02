using UnityEngine;
namespace ElevatorGame
{
    public sealed class GrandPianoEvent : FloorEvent
    {
        readonly System.Collections.Generic.Dictionary<NetworkProp,PlayerController> targets=new();
        readonly System.Collections.Generic.Dictionary<NetworkProp,float> retargetAt=new();
        float next=.8f;int count;
        public override void StartEvent(){base.StartEvent();RoundManager.Instance.PlayCue("piano");}
        public override void UpdateEvent()
        {
            if(Elapsed>=next&&count<3)
            {
                float x=count==0?-2.4f:count==1?2.4f:0;
                var victim=RandomLiving(new Vector3(x,1,0));
                float lateral=victim?Mathf.Clamp((victim.Body.position.x-x)*1.2f,-3,3):Manager.Random(-1,1);
                var piano=Spawn(15,new Vector3(x,1.8f,6.1f+count*1.15f),new Vector3(lateral,0,-12*Power));
                piano.Body.angularVelocity=new Vector3(Manager.Random(-2,2),Manager.Random(-3,3),Manager.Random(-3,3));
                targets[piano]=victim;retargetAt[piano]=Elapsed+2;
                count++;next+=2.4f;RoundManager.Instance.PlayCue("piano");
            }
            foreach(var piano in Spawned)
            {
                if(!piano||piano.Body.position.z>4.5f)continue;
                if(!targets.TryGetValue(piano,out var victim)||!victim||!victim.Alive.Value||Elapsed>=retargetAt[piano])
                {targets[piano]=victim=RandomLiving(piano.Body.position);retargetAt[piano]=Elapsed+Manager.Random(1.5f,2.4f);}
                if(!victim)continue;
                Vector3 direction=victim.Body.position-piano.Body.position;direction.y=0;
                if(direction.sqrMagnitude<.1f)continue;
                Vector3 horizontal=new(piano.Body.linearVelocity.x,0,piano.Body.linearVelocity.z);
                piano.Body.AddForce(Vector3.ClampMagnitude(direction.normalized*10-horizontal,16)*1.8f,ForceMode.Acceleration);
            }
        }
        public override Vector3 GetRecommendedSafeDirection(Vector3 position)
        {
            foreach(var p in Spawned)if(p&&Vector3.Distance(p.Body.position,position)<3)
                return new Vector3(position.x-p.Body.position.x,0,-1).normalized;
            return Vector3.back;
        }
    }
}

using UnityEngine;
namespace ElevatorGame
{
    public sealed class GrandPianoEvent : FloorEvent
    {
        float next=.8f;int count;
        public override void StartEvent(){base.StartEvent();RoundManager.Instance.PlayCue("piano");}
        public override void UpdateEvent()
        {
            if(Elapsed<next||count>=3)return;
            float x=count==0?-2.4f:count==1?2.4f:0;
            var piano=Spawn(15,new Vector3(x,1.8f,6.1f+count*1.15f),new Vector3(Manager.Random(-1,1),0,-12*Power));
            piano.Body.angularVelocity=new Vector3(Manager.Random(-2,2),Manager.Random(-3,3),Manager.Random(-3,3));
            count++;next+=2.4f;RoundManager.Instance.PlayCue("piano");
        }
        public override Vector3 GetRecommendedSafeDirection(Vector3 position)
        {
            foreach(var p in Spawned)if(p&&Vector3.Distance(p.Body.position,position)<3)
                return new Vector3(position.x-p.Body.position.x,0,-1).normalized;
            return Vector3.back;
        }
    }
}

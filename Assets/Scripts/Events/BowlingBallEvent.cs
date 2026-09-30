using UnityEngine;
namespace ElevatorGame
{
    public sealed class BowlingBallEvent : FloorEvent
    {
        float next=.55f;
        public override void StartEvent(){base.StartEvent();RoundManager.Instance.PlayCue("warning");}
        public override void UpdateEvent()
        {
            if(Elapsed<next)return;next+=1.25f;
            Vector3 velocity=new Vector3(Manager.Random(-2.5f,2.5f),0,-Mathf.Min(32,22*Power));
            var ball=Spawn(0,new Vector3(Manager.Random(-1.8f,1.8f),1.02f,22),velocity,Definition.persistent);
            ball.Body.maxAngularVelocity=40;ball.Body.angularVelocity=Vector3.Cross(Vector3.up,velocity)/.875f;
            RoundManager.Instance.PlayCue("rumble");
        }
    }
}



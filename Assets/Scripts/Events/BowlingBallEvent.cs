using UnityEngine;
namespace ElevatorGame
{
    public sealed class BowlingBallEvent : FloorEvent
    {
        const int BallCount=5;
        static readonly float[] Lanes={0,-2.4f,2.4f,-1.2f,1.2f};
        float next=.55f;
        int launched;
        public int BallsLaunched=>launched;
        public override void StartEvent(){base.StartEvent();next=.55f;launched=0;RoundManager.Instance.PlayCue("warning");}
        public override void UpdateEvent()
        {
            if(launched>=BallCount||Elapsed<next)return;
            float lane=Lanes[launched++];
            next+=1.45f;
            Vector3 velocity=new Vector3(Manager.Random(-1.1f,1.1f),0,-Mathf.Min(32,22*Power));
            var ball=Spawn(0,new Vector3(lane,1.02f,22),velocity,Definition.persistent);
            ball.Body.maxAngularVelocity=40;ball.Body.angularVelocity=Vector3.Cross(Vector3.up,velocity)/.875f;
            RoundManager.Instance.PlayCue("rumble");
        }
    }
}



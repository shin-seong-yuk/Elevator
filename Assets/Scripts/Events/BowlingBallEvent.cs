using UnityEngine;
namespace ElevatorGame
{
    public sealed class BowlingBallEvent : FloorEvent
    {
        const int BallCount=5;
        static readonly float[] Lanes={0,-2.4f,2.4f,-1.2f,1.2f};
        readonly NetworkProp[] balls=new NetworkProp[BallCount];
        readonly PlayerController[] targets=new PlayerController[BallCount];
        readonly float[] retargetAt=new float[BallCount];
        float next=.55f;
        int launched;
        public int BallsLaunched=>launched;
        public override void StartEvent(){base.StartEvent();next=.55f;launched=0;RoundManager.Instance.PlayCue("warning");}
        public override void UpdateEvent()
        {
            if(launched<BallCount&&Elapsed>=next)
            {
                int index=launched++;
                float lane=Lanes[index];
                next+=.3f;
                targets[index]=RandomLiving(new Vector3(lane,1,0));
                retargetAt[index]=Elapsed+Manager.Random(.7f,1.3f);
                float steer=targets[index]?Mathf.Clamp((targets[index].Body.position.x-lane)*1.1f,-3.5f,3.5f):0;
                Vector3 velocity=new Vector3(steer+Manager.Random(-.5f,.5f),0,-Mathf.Min(32,22*Power));
                var ball=balls[index]=Spawn(0,new Vector3(lane,1.02f,22),velocity,Definition.persistent);
                ball.Body.maxAngularVelocity=40;ball.Body.angularVelocity=Vector3.Cross(Vector3.up,velocity)/.875f;
                RoundManager.Instance.PlayCue("rumble");
            }
            for(int i=0;i<launched;i++)
            {
                var ball=balls[i];if(!ball||ball.Body.position.z< -4)continue;
                if(!targets[i]||!targets[i].Alive.Value||Elapsed>=retargetAt[i])
                {targets[i]=RandomLiving(ball.Body.position);retargetAt[i]=Elapsed+Manager.Random(.7f,1.3f);}
                if(!targets[i])continue;
                float acceleration=Mathf.Clamp((targets[i].Body.position.x-ball.Body.position.x)*5-ball.Body.linearVelocity.x*2,-12,12);
                ball.Body.AddForce(Vector3.right*acceleration,ForceMode.Acceleration);
            }
        }
    }
}



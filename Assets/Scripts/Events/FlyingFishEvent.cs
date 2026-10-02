using UnityEngine;
namespace ElevatorGame
{
    public sealed class FlyingFishEvent : FloorEvent
    {
        const int Count=10;
        readonly NetworkProp[] fish=new NetworkProp[Count];
        readonly PlayerController[] targets=new PlayerController[Count];
        readonly bool[] launched=new bool[Count];
        readonly float[] nextFlap=new float[Count],retargetAt=new float[Count];
        public int Flaps {get;private set;}
        public override void StartEvent()
        {
            base.StartEvent();RoundManager.Instance.PlayCue("fish");
            for(int i=0;i<Count;i++)
            {
                float lane=(i%5-2)*1.45f;
                fish[i]=Spawn(11,new Vector3(lane,.36f,7+(i/5)*2.5f+Manager.Random(-.4f,.4f)),Vector3.back*2.5f);
                fish[i].Body.angularVelocity=new Vector3(Manager.Random(-3,3),Manager.Random(-3,3),Manager.Random(-3,3));
                nextFlap[i]=Manager.Random(0,.18f);
            }
        }
        public override Vector3 GetRecommendedSafeDirection(Vector3 position)
        {
            NetworkProp closest=null;float distance=25;
            foreach(var f in fish)if(f){float d=(f.Body.position-position).sqrMagnitude;if(d<distance){distance=d;closest=f;}}
            if(!closest)return Vector3.back;
            var away=position-closest.Body.position;away.y=0;return away.normalized;
        }
        public override void UpdateEvent()
        {
            for(int i=0;i<Count;i++)
            {
                var f=fish[i];if(!f)continue;
                float launch=1.15f+i*.15f;
                if(!launched[i]&&Elapsed>=launch)
                {
                    launched[i]=true;
                    targets[i]=RandomLiving(f.Body.position);
                    retargetAt[i]=Elapsed+Manager.Random(1.1f,2f);
                    f.Animate(1);RoundManager.Instance.PlayCue("splash");
                }
                if(launched[i]&&(!targets[i]||!targets[i].Alive.Value||Elapsed>=retargetAt[i]))
                {
                    targets[i]=RandomLiving(f.Body.position);
                    retargetAt[i]=Elapsed+Manager.Random(1.1f,2f);
                }
                Vector3 destination=launched[i]&&targets[i]
                    ?targets[i].Body.position
                    :new Vector3(Mathf.Clamp(f.Body.position.x,-3.8f,3.8f),.8f,-1);
                Vector3 direction=destination-f.Body.position;direction.y=0;
                if(direction.sqrMagnitude>.01f)direction.Normalize();else direction=Vector3.back;
                var horizontal=new Vector3(f.Body.linearVelocity.x,0,f.Body.linearVelocity.z);
                f.Body.AddForce(Vector3.ClampMagnitude(direction*(launched[i]?16:6)-horizontal,22)*2.2f,ForceMode.Acceleration);
                if(Elapsed<nextFlap[i])continue;
                nextFlap[i]=Elapsed+Manager.Random(launched[i] ? .26f : .19f,launched[i] ? .42f : .3f);
                float lift=f.Body.position.y<1.35f?Manager.Random(3.4f,4.8f):1.1f;
                f.Body.AddForce(direction*(launched[i]?3.5f:1.2f)+Vector3.up*lift,ForceMode.VelocityChange);
                f.Body.AddTorque(new Vector3(Manager.Random(-8,8),Manager.Random(-12,12),Manager.Random(-18,18)),ForceMode.VelocityChange);
                f.Body.linearVelocity=Vector3.ClampMagnitude(f.Body.linearVelocity,20);
                Flaps++;
            }
        }
    }
}

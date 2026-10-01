using UnityEngine;
namespace ElevatorGame
{
    public sealed class FlyingFishEvent : FloorEvent
    {
        const int Count=10;
        readonly NetworkProp[] fish=new NetworkProp[Count];
        readonly bool[] launched=new bool[Count];
        public override void StartEvent()
        {
            base.StartEvent();RoundManager.Instance.PlayCue("fish");
            for(int i=0;i<Count;i++)
            {
                float lane=(i%5-2)*1.45f;
                fish[i]=Spawn(11,new Vector3(lane,.36f,7+(i/5)*2.5f+Manager.Random(-.4f,.4f)),Vector3.back*2.5f);
                fish[i].Body.angularVelocity=new Vector3(Manager.Random(-3,3),Manager.Random(-3,3),Manager.Random(-3,3));
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
                float launch=1.35f+i*.24f;
                if(!launched[i]&&Elapsed>=launch)
                {
                    launched[i]=true;var target=Nearest(new Vector3(f.Body.position.x,1,0),8);
                    float x=target?target.Body.position.x:Manager.Random(-3f,3f);
                    var direction=(new Vector3(x,1,-1.8f)-f.Body.position).normalized;
                    f.Body.AddForce((direction*15+Vector3.up*5)*Mathf.Min(Power,1.65f),ForceMode.VelocityChange);
                    f.Body.AddTorque(new Vector3(0,Manager.Random(-10,10),Manager.Random(-16,16)),ForceMode.VelocityChange);
                    f.Animate(1);RoundManager.Instance.PlayCue("splash");
                }
                else if(!launched[i]&&Elapsed<launch&&Mathf.Sin((Elapsed+i*.19f)*21)>0.92f)
                    f.Body.AddForce(Vector3.up*3.4f+Vector3.back*1.3f,ForceMode.VelocityChange);
                else if(launched[i]&&Elapsed<launch+5)
                {
                    // Keep swimming toward the lift after the first collision with the sill or a passenger.
                    if(f.Body.position.z> -2.2f&&f.Body.linearVelocity.z> -18)
                        f.Body.AddForce(Vector3.back*42,ForceMode.Acceleration);
                    if(f.Body.position.y<.72f)
                        f.Body.AddForce(Vector3.up*38,ForceMode.Acceleration);
                }
            }
        }
    }
}

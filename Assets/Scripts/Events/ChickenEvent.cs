using UnityEngine;
namespace ElevatorGame
{
    public sealed class ChickenEvent : FloorEvent
    {
        readonly NetworkProp[] birds=new NetworkProp[6];
        readonly PlayerController[] targets=new PlayerController[6];
        readonly float[] nextFlap=new float[6],retargetAt=new float[6],nextHit=new float[6];
        public int Flaps {get;private set;}
        public override void StartEvent()
        {
            base.StartEvent();
            for(int i=0;i<birds.Length;i++)
            {
                birds[i]=Spawn(4,new Vector3((i%3-1)*2.4f,1.3f,10+i*1.35f),Vector3.back*(10+i));
                nextFlap[i]=Manager.Random(0,.25f);
            }
        }
        public override void UpdateEvent()
        {
            for(int i=0;i<birds.Length;i++)
            {
                var bird=birds[i];
                if(!bird)continue;
                if(!targets[i]||!targets[i].Alive.Value||Elapsed>=retargetAt[i])
                {
                    targets[i]=RandomLiving(bird.Body.position);
                    retargetAt[i]=Elapsed+Manager.Random(1.1f,2.2f);
                }
                Vector3 destination=targets[i]?targets[i].Body.position:new Vector3(0,1,-1.5f);
                Vector3 direction=destination-bird.Body.position;direction.y=0;
                if(direction.sqrMagnitude>.01f)direction.Normalize();else direction=Vector3.back;
                var horizontal=new Vector3(bird.Body.linearVelocity.x,0,bird.Body.linearVelocity.z);
                bird.Body.AddForce(Vector3.ClampMagnitude(direction*9-horizontal,17)*2,ForceMode.Acceleration);
                if(targets[i]&&Elapsed>=nextHit[i]&&Vector3.Distance(bird.Body.position,targets[i].Body.position)<1.5f)
                {
                    nextHit[i]=Elapsed+.8f;
                    targets[i].Knock(direction*5+Vector3.up*3,.55f);
                    RoundManager.Instance.PlayCue("impact");
                }
                if(Elapsed<nextFlap[i])continue;
                nextFlap[i]=Elapsed+Manager.Random(.32f,.48f);
                float lift=bird.Body.position.y<1.45f?Manager.Random(2.8f,4.1f):.8f;
                bird.Body.AddForce(direction*2.5f+Vector3.up*lift,ForceMode.VelocityChange);
                bird.Body.AddTorque(new Vector3(Manager.Random(-6,6),Manager.Random(-7,7),Manager.Random(-6,6)),ForceMode.VelocityChange);
                bird.Body.linearVelocity=Vector3.ClampMagnitude(bird.Body.linearVelocity,17);
                bird.Animate(1);
                Flaps++;
                if(i==0)RoundManager.Instance.PlayCue("chicken");
            }
        }
    }
}

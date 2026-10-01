using UnityEngine;
namespace ElevatorGame
{
    public sealed class ChickenEvent : FloorEvent
    {
        readonly NetworkProp[] birds=new NetworkProp[6];float next;
        public override void StartEvent(){base.StartEvent();for(int i=0;i<birds.Length;i++)birds[i]=Spawn(4,new Vector3((i%3-1)*2.4f,1.3f,10+i*1.35f),Vector3.back*(10+i));}
        public override void UpdateEvent()
        {
            if(Elapsed<next)return;next=Elapsed+.48f;
            foreach(var bird in birds)
            {
                if(!bird)continue;
                var victim=Nearest(bird.Body.position,9);
                Vector3 target=victim?victim.Body.position:new Vector3(Manager.Random(-2,2),1,-1.5f);
                Vector3 direction=(target-bird.Body.position).normalized;
                bird.Body.AddForce(direction*4.6f+Vector3.up*3.7f,ForceMode.VelocityChange);
                bird.Body.AddTorque(new Vector3(Manager.Random(-6,6),Manager.Random(-7,7),Manager.Random(-6,6)),ForceMode.VelocityChange);
            }
            RoundManager.Instance.PlayCue("chicken");
        }
    }
}

using UnityEngine;
namespace ElevatorGame
{
    public sealed class ChickenEvent : FloorEvent
    {
        readonly NetworkProp[] birds=new NetworkProp[3];float next;
        public override void StartEvent(){base.StartEvent();for(int i=0;i<3;i++)birds[i]=Spawn(4,new Vector3((i-1)*1.8f,1.3f,14+i*2),Vector3.back*8);}
        public override void UpdateEvent()
        {
            if(Elapsed<next)return;next=Elapsed+.75f;
            foreach(var bird in birds)
            {
                if(!bird)continue;
                Vector3 direction=(new Vector3(Manager.Random(-2,2),1,Manager.Random(-2,2))-bird.transform.position).normalized;
                bird.Body.AddForce(direction*7+Vector3.up*2.5f,ForceMode.VelocityChange);bird.Body.AddTorque(Vector3.up*8,ForceMode.VelocityChange);
            }
            RoundManager.Instance.PlayCue("chicken");
        }
    }
}

using UnityEngine;
namespace ElevatorGame
{
    public sealed class GiantHandEvent : FloorEvent
    {
        readonly NetworkProp[] hands=new NetworkProp[3];readonly FixedJoint[] holds=new FixedJoint[3];
        readonly float[] next=new float[3],releaseAt=new float[3];readonly bool[] pulling=new bool[3];
        public override void StartEvent()
        {
            base.StartEvent();for(int i=0;i<3;i++){hands[i]=Spawn(5,new Vector3((i-1)*1.7f,2.2f,6+i),Vector3.zero);hands[i].Body.isKinematic=true;next[i]=1+i*.65f;}
        }
        public override void UpdateEvent()
        {
            for(int i=0;i<3;i++)
            {
                var hand=hands[i];if(!hand)continue;
                if(pulling[i])
                {
                    hand.Body.MovePosition(Vector3.MoveTowards(hand.Body.position,new Vector3((i-1)*1.7f,2.7f,8),Time.fixedDeltaTime*2.8f));
                    if(!holds[i]||Elapsed>releaseAt[i]){if(holds[i])Destroy(holds[i]);pulling[i]=false;next[i]=Elapsed+1.3f;}continue;
                }
                if(Elapsed<next[i])continue;var victim=Nearest(hand.Body.position);if(!victim)continue;
                hand.Body.MovePosition(Vector3.MoveTowards(hand.Body.position,victim.Body.position+Vector3.up*.3f,Time.fixedDeltaTime*2.8f));
                if(Vector3.Distance(hand.Body.position,victim.Body.position)<1.3f)
                {
                    holds[i]=hand.gameObject.AddComponent<FixedJoint>();holds[i].connectedBody=victim.Body;holds[i].breakForce=1800;holds[i].breakTorque=1800;
                    victim.Grab.ApplyImpact(300);pulling[i]=true;releaseAt[i]=Elapsed+2.2f;
                }
            }
        }
        public override void EndEvent(){foreach(var hold in holds)if(hold)Destroy(hold);base.EndEvent();}
    }
}

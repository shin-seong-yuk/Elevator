using UnityEngine;
namespace ElevatorGame
{
    public sealed class StrangerEvent : FloorEvent
    {
        NetworkProp runner;float next;
        public override void StartEvent(){base.StartEvent();runner=Spawn(8,new Vector3(-1.2f,1.2f,18),Vector3.back*8);runner.Animate(5);RoundManager.Instance.PlayCue("runner");}
        public override void UpdateEvent()
        {
            if(!runner)return;
            Vector3 target=Elapsed<3?new Vector3(-1.2f,1,-1.6f):new Vector3(Mathf.Sin(Elapsed)*2,1,Mathf.Cos(Elapsed*1.3f)*1.5f);
            var direction=target-runner.Body.position;direction.y=0;
            runner.Body.AddForce(Vector3.ClampMagnitude(direction.normalized*8-runner.Body.linearVelocity,12)*2,ForceMode.Acceleration);
            if(Elapsed>next){next=Elapsed+.32f;RoundManager.Instance.PlayCue("step");}
        }
    }
}


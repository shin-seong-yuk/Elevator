using System.Collections.Generic;
using UnityEngine;
namespace ElevatorGame
{
    public sealed class ShoppingCartEvent : FloorEvent
    {
        readonly Dictionary<NetworkProp,int> passes=new();
        float next=.45f;int count;
        public override void StartEvent(){base.StartEvent();RoundManager.Instance.PlayCue("warning");}
        public override void UpdateEvent()
        {
            if(Elapsed>=next&&count<7)
            {
                next+=1.1f;count++;
                var cart=Spawn(1,new Vector3(Manager.Random(-3.1f,3.1f),.5f,18),new Vector3(Manager.Random(-1.5f,1.5f),0,-Mathf.Min(21,14*Power)),count%2==1);
                passes[cart]=0;RoundManager.Instance.PlayCue("cart");
            }
            foreach(var entry in new List<NetworkProp>(passes.Keys))
            {
                if(!entry)continue;
                int pass=passes[entry];
                if(pass==0&&entry.Body.position.z< -1.05f)
                {
                    passes[entry]=1;
                    entry.Body.AddTorque(Vector3.up*7,ForceMode.VelocityChange);
                    entry.Body.AddForce(new Vector3(-entry.Body.position.x*2,.65f,16*Mathf.Min(Power,1.6f)-entry.Body.linearVelocity.z),ForceMode.VelocityChange);
                    RoundManager.Instance.PlayCue("cartCrash");
                }
                else if(pass==1&&entry.Body.position.z>5.5f)
                {
                    passes[entry]=2;
                    // Keep outgoing momentum: this cart has completed its pass.
                }
                else if(pass<2)
                {
                    Vector3 direction=pass==0?Vector3.back:Vector3.forward;
                    float targetSpeed=Mathf.Min(21,14*Power);
                    float acceleration=Mathf.Clamp((targetSpeed-Vector3.Dot(entry.Body.linearVelocity,direction))*5,0,24);
                    entry.Body.AddForce(direction*acceleration,ForceMode.Acceleration);
                    if(pass==1)entry.Body.AddForce(Vector3.right*Mathf.Clamp(-entry.Body.position.x*8-entry.Body.linearVelocity.x*3,-12,12),ForceMode.Acceleration);
                }
            }
        }
    }
}









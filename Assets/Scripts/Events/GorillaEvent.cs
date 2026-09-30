using UnityEngine;
namespace ElevatorGame
{
    public sealed class GorillaEvent : FloorEvent
    {
        NetworkProp gorilla; PlayerController victim; SpringJoint hold; float next=1,release;
        public override void StartEvent() { base.StartEvent(); gorilla=Spawn(3,new Vector3(0,1.4f,16f),Vector3.back*5); }
        public override void UpdateEvent()
        {
            if (!gorilla) return;
            if (hold)
            {
                gorilla.Body.AddForce(new Vector3(Mathf.Sin(Elapsed*18)*7,3,0),ForceMode.Acceleration);
                if(Elapsed>release)
                {
                    Destroy(hold); hold=null;
                    if(victim) victim.Knock(new Vector3(Manager.Random(-6,6),5,Manager.Random(-2,10))*Power);
                    next=Elapsed+2;
                }
                return;
            }
            if(Elapsed<next)return;
            victim=Nearest(gorilla.transform.position);
            if(!victim)return;
            Vector3 direction=victim.transform.position-gorilla.transform.position; direction.y=0;
            gorilla.Body.AddForce(direction.normalized*12,ForceMode.Acceleration);
            if(direction.magnitude<1.6f)
            {
                hold=gorilla.gameObject.AddComponent<SpringJoint>(); hold.connectedBody=victim.Body; hold.spring=1600; hold.damper=90; hold.maxDistance=.5f; hold.breakForce=2500;
                release=Elapsed+1.4f; RoundManager.Instance.PlayCue("roar");
            }
        }
        public override void EndEvent(){if(hold)Destroy(hold);base.EndEvent();}
    }
}




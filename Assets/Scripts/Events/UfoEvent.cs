using UnityEngine;
namespace ElevatorGame
{
    public sealed class UfoEvent : FloorEvent
    {
        NetworkProp saucer;PlayerController victim;float nextPulse=2.5f,retargetAt;int pulses;
        public override void StartEvent()
        {
            base.StartEvent();saucer=Spawn(14,new Vector3(0,2.35f,8),Vector3.zero);
            saucer.Body.isKinematic=true;RoundManager.Instance.PlayCue("ufo");
        }
        public override Vector3 GetRecommendedSafeDirection(Vector3 position)
        {
            if(!saucer)return Vector3.back;
            return new Vector3(position.x-saucer.Body.position.x,0,-1).normalized;
        }
        public override void UpdateEvent()
        {
            if(!saucer)return;
            if(!victim||!victim.Alive.Value||Elapsed>=retargetAt)
            {victim=RandomLiving(saucer.Body.position);retargetAt=Elapsed+Manager.Random(1.5f,2.5f);}
            float z=Elapsed<2?Mathf.Lerp(8,1.9f,Elapsed*.5f):1.9f+Mathf.Sin(Elapsed*.8f)*.55f;
            Vector3 destination=victim&&Elapsed>=2
                ?new Vector3(Mathf.Clamp(victim.Body.position.x,-3.4f,3.4f),2.35f+Mathf.Sin(Elapsed*3)*.16f,Mathf.Clamp(victim.Body.position.z,-2.5f,2.2f))
                :new Vector3(Mathf.Sin(Elapsed*1.3f)*2.5f,2.35f+Mathf.Sin(Elapsed*3)*.16f,z);
            saucer.Body.MovePosition(Vector3.MoveTowards(saucer.Body.position,destination,Time.fixedDeltaTime*4.2f));
            if(Elapsed<2)return;
            foreach(var player in RoundManager.Players())if(player.Alive.Value)
            {
                var delta=saucer.Body.position-player.Body.position;
                if(Mathf.Abs(delta.x)<1.8f&&Mathf.Abs(delta.z)<2.7f)
                    player.Body.AddForce((Vector3.up*25+Vector3.forward*9)*Power,ForceMode.Acceleration);
            }
            if(Elapsed<nextPulse)return;
            nextPulse+=2.2f;pulses++;saucer.Animate(pulses);
            foreach(var player in RoundManager.Players())if(player.Alive.Value&&Vector3.Distance(player.Body.position,saucer.Body.position)<3.5f)
                player.Knock((player.Body.position-saucer.Body.position).normalized*4+Vector3.up*4,1.05f);
            RoundManager.Instance.PlayCue("ufo");
        }
    }
}

using UnityEngine;

namespace ElevatorGame
{
    public sealed class GorillaEvent : FloorEvent
    {
        NetworkProp gorilla;
        PlayerController victim;
        float nextStrike=1,windupUntil,lastStrikeAt,retargetAt;
        int lastVictimSlot=-1;
        bool windingUp;
        public int Strikes {get;private set;}
        public int LastVictimSlot=>lastVictimSlot;

        public override void StartEvent()
        {
            base.StartEvent();
            gorilla=Spawn(3,new Vector3(0,1.4f,16),Vector3.back*5);
            gorilla.Animate(5);
            RoundManager.Instance.PlayCue("runner");
        }

        public override float GetDangerStrength()=>Running?(windingUp?2.1f:1.2f)*Power:0;

        public override void UpdateEvent()
        {
            if(!gorilla)return;
            if(windingUp)
            {
                if(Elapsed>=windupUntil)Strike();
                return;
            }
            if(gorilla.Action.Value==7&&Elapsed-lastStrikeAt>.35f)gorilla.Animate(5);
            if(!victim||!victim.Alive.Value||Elapsed>=retargetAt)
            {victim=ChooseVictim();retargetAt=Elapsed+1.4f;}
            Vector3 destination=victim?victim.Body.position:new Vector3(0,1,0);
            Vector3 direction=destination-gorilla.Body.position;direction.y=0;
            Vector3 horizontal=new(gorilla.Body.linearVelocity.x,0,gorilla.Body.linearVelocity.z);
            gorilla.Body.AddForce(Vector3.ClampMagnitude(direction.normalized*9-horizontal,14)*4,ForceMode.Acceleration);
            if(!victim||Elapsed<nextStrike||direction.magnitude>2.65f)return;
            windingUp=true;
            windupUntil=Elapsed+.42f;
            gorilla.Animate(6);
            RoundManager.Instance.PlayCue("warning");
        }

        PlayerController ChooseVictim()
        {
            // Randomly choose a living passenger, preferring someone other than the last victim.
            return RandomLiving(gorilla.Body.position,40,lastVictimSlot)??RandomLiving(gorilla.Body.position);
        }

        void Strike()
        {
            windingUp=false;
            nextStrike=Elapsed+1.65f;
            lastStrikeAt=Elapsed;
            Strikes++;
            gorilla.Animate(7);
            Vector3 center=gorilla.Body.position;
            Vector3 toward=victim&&victim.Alive.Value?victim.Body.position-center:Vector3.back;
            toward.y=0;
            if(toward.sqrMagnitude<.01f)toward=Vector3.back;
            toward.Normalize();
            foreach(var player in RoundManager.Players())
            {
                if(!player.Alive.Value)continue;
                Vector3 offset=player.Body.position-center;offset.y=0;
                if(offset.magnitude>2.9f||Vector3.Dot(offset.normalized,toward)<.15f)continue;
                player.Knock((offset.normalized*12+Vector3.up*4)*Power,.9f);
                lastVictimSlot=player.Slot.Value;
            }
            if(victim)lastVictimSlot=victim.Slot.Value;
            RoundManager.Instance.PlayCue("slam");
            RoundManager.Instance.WeaponEffect(center+Vector3.up*.6f,center+toward*2.3f,103);
            victim=null;retargetAt=Elapsed;
        }
    }
}

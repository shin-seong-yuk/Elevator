using UnityEngine;
namespace ElevatorGame
{
    public sealed class DinosaurEvent : FloorEvent
    {
        NetworkProp dinosaur;PlayerController victim;SpringJoint bite;
        int attack;float next,release,attackAt,tailStart;bool arrived,telegraph,sweeping;
        Vector3 previousTailTip;
        readonly System.Collections.Generic.List<PlayerController> tailHits=new();
        public override void StartEvent()
        {
            base.StartEvent();dinosaur=Spawn(2,new Vector3(0,1.55f,18),Vector3.zero);
            dinosaur.Body.isKinematic=true;dinosaur.Animate(5);attack=Manager.RandomInt(0,4);next=2;
            RoundManager.Instance.PlayCue("dinoRun");
        }
        public override float GetDangerStrength()=>Running?(telegraph||sweeping?2.4f:arrived?1.1f:.45f)*Power:0;
        public override void UpdateEvent()
        {
            if(!dinosaur)return;
            if(!arrived)
            {
                Vector3 goal=new Vector3(.4f,1.55f,1.65f);
                dinosaur.Body.MovePosition(Vector3.MoveTowards(dinosaur.Body.position,goal,Time.fixedDeltaTime*4.6f));
                if(Vector3.Distance(dinosaur.Body.position,goal)<.08f){arrived=true;next=Elapsed+1;dinosaur.Animate(0);RoundManager.Instance.PlayCue("roar");}
                return;
            }
            if(bite)
            {
                if(Elapsed>release)
                {
                    Destroy(bite);bite=null;
                    if(victim&&victim.Alive.Value)victim.Knock(new Vector3(Manager.Random(-2,2),5,12)*Power);
                    RoundManager.Instance.PlayCue("throw");next=Elapsed+1.5f;dinosaur.Animate(0);
                }
                return;
            }
            if(sweeping){SweepTail();return;}
            if(!telegraph&&Elapsed>next)
            {telegraph=true;attackAt=Elapsed+.95f;victim=Nearest(dinosaur.Body.position+Vector3.back,9);dinosaur.Animate(attack%4+1);RoundManager.Instance.PlayCue("warning");}
            if(telegraph)
            {
                float x=victim&&victim.Alive.Value?Mathf.Clamp(victim.Body.position.x,-2.9f,2.9f):Mathf.Sin(Elapsed*2)*1.3f;
                float z=attack%4==0&&victim&&victim.Alive.Value?Mathf.Clamp(victim.Body.position.z+1.55f,-1.8f,2.4f):
                    attack%4==1&&victim&&victim.Alive.Value?Mathf.Clamp(victim.Body.position.z+2.25f,-1.8f,2.1f):1.25f;
                dinosaur.Body.MovePosition(Vector3.MoveTowards(dinosaur.Body.position,new Vector3(x,1.55f,z),Time.fixedDeltaTime*5.2f));
                dinosaur.Body.MoveRotation(Quaternion.Euler(0,Mathf.Sin(Elapsed*7)*8,0));
            }
            if(!telegraph||Elapsed<attackAt)return;
            telegraph=false;next=Elapsed+2.6f;
            switch(attack++%4)
            {
                case 0:
                    Vector3 mouth=dinosaur.Body.position+dinosaur.Body.rotation*new Vector3(0,.12f,-1.65f);
                    victim=null;float best=1.15f;
                    foreach(var player in RoundManager.Players())
                    {
                        float distance=Vector3.Distance(player.Body.position+Vector3.up*.3f,mouth);
                        if(player.Alive.Value&&distance<best){victim=player;best=distance;}
                    }
                    if(victim)
                    {
                        bite=dinosaur.gameObject.AddComponent<SpringJoint>();bite.connectedBody=victim.Body;bite.autoConfigureConnectedAnchor=false;
                        bite.anchor=dinosaur.transform.InverseTransformPoint(mouth);bite.connectedAnchor=new Vector3(0,.2f,0);bite.spring=1300;bite.damper=80;bite.maxDistance=.45f;bite.breakForce=2100;
                        release=Elapsed+1.6f;RoundManager.Instance.PlayCue("bite");
                    }
                    break;
                case 1:
                    sweeping=true;tailStart=Elapsed;tailHits.Clear();previousTailTip=TailTip(Quaternion.identity);
                    RoundManager.Instance.PlayCue("whoosh");break;
                case 2:RoarForward();break;
                case 3:StompNearby();break;
            }
        }
        void SweepTail()
        {
            float time=Elapsed-tailStart;
            float yaw=time<1.05f?Mathf.SmoothStep(0,220,time/1.05f):Mathf.SmoothStep(220,360,(time-1.05f)/.5f);
            Quaternion rotation=Quaternion.Euler(0,yaw,0);
            dinosaur.Body.MoveRotation(rotation);
            Vector3 tailBase=dinosaur.Body.position+rotation*new Vector3(0,-.1f,.8f);
            Vector3 tip=TailTip(rotation);
            foreach(var player in RoundManager.Players())
            {
                if(!player.Alive.Value||tailHits.Contains(player))continue;
                Vector3 point=player.Body.position+Vector3.up*.2f;
                if(SegmentDistance(point,tailBase,tip)>.95f&&SegmentDistance(point,previousTailTip,tip)>.95f)continue;
                tailHits.Add(player);
                Vector3 lateral=Vector3.Cross(Vector3.up,tip-previousTailTip).normalized;
                player.Knock((Vector3.forward*8+lateral*4+Vector3.up*3)*Power,1.1f);
                RoundManager.Instance.PlayCue("impact");
            }
            previousTailTip=tip;
            if(time<1.55f)return;
            dinosaur.Body.MoveRotation(Quaternion.identity);sweeping=false;next=Elapsed+1.3f;dinosaur.Animate(0);
        }
        Vector3 TailTip(Quaternion rotation)
        {
            float actionTime=(float)(RoundManager.Instance.Clock-dinosaur.ActionAt.Value);
            float sway=Mathf.Sin(Mathf.Clamp01((actionTime-.4f)/1.3f)*Mathf.PI*2)*95;
            return dinosaur.Body.position+rotation*(new Vector3(0,-.1f,.8f)+Quaternion.Euler(0,sway,0)*Vector3.forward*1.85f);
        }
        static float SegmentDistance(Vector3 point,Vector3 a,Vector3 b)
        {
            Vector3 axis=b-a;float t=Mathf.Clamp01(Vector3.Dot(point-a,axis)/Mathf.Max(.001f,axis.sqrMagnitude));
            return Vector3.Distance(point,a+axis*t);
        }
        void RoarForward()
        {
            Vector3 forward=dinosaur.Body.rotation*Vector3.back;
            foreach(var player in RoundManager.Players())
            {
                Vector3 toward=player.Body.position-dinosaur.Body.position;
                if(player.Alive.Value&&toward.magnitude<4.8f&&Vector3.Dot(toward.normalized,forward)>.55f)
                    player.Knock((forward*7+Vector3.up*2)*Power,.8f);
            }
            RoundManager.Instance.PlayCue("roar");
        }
        void StompNearby()
        {
            Vector3 foot=dinosaur.Body.position+dinosaur.Body.rotation*Vector3.back*1.1f;
            foreach(var player in RoundManager.Players())
                if(player.Alive.Value&&Vector3.Distance(player.Body.position,foot)<2.1f)
                    player.Knock((Vector3.forward*4+Vector3.up*6)*Power);
            RoundManager.Instance.PlayCue("slam");
        }
        public override void EndEvent(){if(bite)Destroy(bite);sweeping=telegraph=false;tailHits.Clear();base.EndEvent();}
    }
}


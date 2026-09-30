using UnityEngine;
namespace ElevatorGame
{
    public sealed class DinosaurEvent : FloorEvent
    {
        NetworkProp dinosaur;PlayerController victim;SpringJoint bite;
        int attack;float next,release,attackAt;bool arrived,telegraph;
        public override void StartEvent()
        {
            base.StartEvent();dinosaur=Spawn(2,new Vector3(0,1.55f,18),Vector3.zero);
            dinosaur.Body.isKinematic=true;dinosaur.Animate(5);attack=Manager.RandomInt(0,4);next=2;
            RoundManager.Instance.PlayCue("dinoRun");
        }
        public override float GetDangerStrength()=>Running?(telegraph?2.4f:arrived?1.1f:.45f)*Power:0;
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
                dinosaur.Body.MovePosition(new Vector3(.4f+Mathf.Sin(Elapsed*11)*.22f,1.7f+Mathf.Sin(Elapsed*14)*.1f,1.65f));
                if(Elapsed>release)
                {
                    Destroy(bite);bite=null;
                    if(victim)victim.Knock(new Vector3(Manager.Random(-2,2),5,10)*Power);
                    RoundManager.Instance.PlayCue("throw");next=Elapsed+1.5f;dinosaur.Animate(0);
                }
                return;
            }
            if(!telegraph&&Elapsed>next)
            {telegraph=true;attackAt=Elapsed+.9f;dinosaur.Animate(attack%4+1);RoundManager.Instance.PlayCue("warning");}
            if(!telegraph||Elapsed<attackAt)return;
            telegraph=false;next=Elapsed+2.6f;
            switch(attack++%4)
            {
                case 0:
                    victim=Nearest(dinosaur.Body.position+Vector3.back,3.5f);
                    if(victim)
                    {
                        bite=dinosaur.gameObject.AddComponent<SpringJoint>();bite.connectedBody=victim.Body;bite.autoConfigureConnectedAnchor=false;
                        bite.anchor=new Vector3(0,.35f,-1.2f);bite.connectedAnchor=new Vector3(0,.2f,0);bite.spring=1300;bite.damper=80;bite.maxDistance=.45f;bite.breakForce=2100;
                        release=Elapsed+1.6f;RoundManager.Instance.PlayCue("bite");
                    }
                    break;
                case 1:Blast(dinosaur.Body.position,4.5f,new Vector3(3,2,8));RoundManager.Instance.PlayCue("whoosh");break;
                case 2:Blast(dinosaur.Body.position+Vector3.back,4,new Vector3(0,2,7));RoundManager.Instance.PlayCue("roar");break;
                case 3:
                    Blast(dinosaur.Body.position+Vector3.back,3.6f,new Vector3(0,6,4));RoundManager.Instance.PlayCue("slam");break;
            }
        }
        public override void EndEvent(){if(bite)Destroy(bite);base.EndEvent();}
    }
}


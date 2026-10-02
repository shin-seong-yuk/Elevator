using System.Collections.Generic;
using UnityEngine;

namespace ElevatorGame
{
    public sealed class DinosaurEvent : FloorEvent
    {
        NetworkProp dinosaur;
        float nextSweep,tailStart;
        bool arrived,sweeping;
        PlayerController victim;
        float retargetAt;
        Vector3 previousTailTip;
        readonly List<PlayerController> tailHits=new();
        [System.NonSerialized] readonly HashSet<PlayerController> allHits=new();
        public int SweepsStarted {get;private set;}
        public bool HasHit(PlayerController player)=>allHits.Contains(player);

        public override void StartEvent()
        {
            base.StartEvent();
            dinosaur=Spawn(2,new Vector3(0,1.55f,18),Vector3.zero);
            dinosaur.Body.isKinematic=true;
            dinosaur.Animate(5);
            RoundManager.Instance.PlayCue("dinoRun");
        }

        public override float GetDangerStrength()=>Running?(sweeping?2.4f:arrived?1.2f:.45f)*Power:0;

        public override void UpdateEvent()
        {
            if(!dinosaur)return;
            if(!arrived)
            {
                Vector3 goal=new(.4f,1.55f,1.65f);
                dinosaur.Body.MovePosition(Vector3.MoveTowards(dinosaur.Body.position,goal,Time.fixedDeltaTime*4.6f));
                if(Vector3.Distance(dinosaur.Body.position,goal)<.08f)
                {
                    arrived=true;
                    StartSweep();
                }
                return;
            }

            MoveTowardPlayers(sweeping?2.7f:3.6f);
            if(sweeping)SweepTail();
            else if(Elapsed>=nextSweep)StartSweep();
        }

        void MoveTowardPlayers(float speed)
        {
            if(!victim||!victim.Alive.Value||Elapsed>=retargetAt)
            {
                victim=RandomLiving(dinosaur.Body.position);
                retargetAt=Elapsed+Manager.Random(1.6f,2.5f);
            }
            Vector3 target=victim
                ?new Vector3(Mathf.Clamp(victim.Body.position.x,-3.5f,3.5f),1.55f,Mathf.Clamp(victim.Body.position.z+1.35f,-2.25f,2.35f))
                :new Vector3(Mathf.Sin(Elapsed)*1.5f,1.55f,.65f);
            dinosaur.Body.MovePosition(Vector3.MoveTowards(dinosaur.Body.position,target,Time.fixedDeltaTime*speed));
        }

        void StartSweep()
        {
            sweeping=true;tailStart=Elapsed;nextSweep=Elapsed+2f;SweepsStarted++;
            tailHits.Clear();
            dinosaur.Body.MoveRotation(Quaternion.identity);
            dinosaur.Animate(2);
            previousTailTip=TailTip(Quaternion.identity);
            RoundManager.Instance.PlayCue("whoosh");
        }

        void SweepTail()
        {
            float time=Elapsed-tailStart;
            float yaw=Mathf.SmoothStep(0,360,Mathf.Clamp01(time/1.35f));
            Quaternion rotation=Quaternion.Euler(0,yaw,0);
            dinosaur.Body.MoveRotation(rotation);
            Vector3 tailBase=dinosaur.Body.position+rotation*new Vector3(0,-.1f,.8f);
            Vector3 tip=TailTip(rotation);
            foreach(var player in RoundManager.Players())
            {
                if(!player.Alive.Value||tailHits.Contains(player))continue;
                Vector3 point=player.Body.position+Vector3.up*.2f;
                if(SegmentDistance(point,tailBase,tip)>.95f&&SegmentDistance(point,previousTailTip,tip)>.95f)continue;
                tailHits.Add(player);allHits.Add(player);
                Vector3 lateral=Vector3.Cross(Vector3.up,tip-previousTailTip).normalized;
                player.Knock((Vector3.forward*8+lateral*4+Vector3.up*3)*Power,1.1f);
                RoundManager.Instance.PlayCue("impact");
            }
            previousTailTip=tip;
            if(time<1.35f)return;
            dinosaur.Body.MoveRotation(Quaternion.identity);
            dinosaur.Animate(0);
            sweeping=false;
        }

        Vector3 TailTip(Quaternion rotation)
        {
            float actionTime=(float)(RoundManager.Instance.Clock-dinosaur.ActionAt.Value);
            float sway=Mathf.Sin(Mathf.Clamp01((actionTime-.4f)/1.3f)*Mathf.PI*2)*95;
            return dinosaur.Body.position+rotation*(new Vector3(0,-.1f,.8f)+Quaternion.Euler(0,sway,0)*Vector3.forward*1.85f);
        }

        static float SegmentDistance(Vector3 point,Vector3 a,Vector3 b)
        {
            Vector3 axis=b-a;
            float t=Mathf.Clamp01(Vector3.Dot(point-a,axis)/Mathf.Max(.001f,axis.sqrMagnitude));
            return Vector3.Distance(point,a+axis*t);
        }

        public override void EndEvent(){sweeping=false;tailHits.Clear();allHits.Clear();base.EndEvent();}
    }
}

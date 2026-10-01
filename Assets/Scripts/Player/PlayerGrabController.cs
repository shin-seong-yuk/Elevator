using System.Collections.Generic;
using UnityEngine;
namespace ElevatorGame
{
    public sealed class PlayerGrabController : MonoBehaviour
    {
        public Transform leftHand,rightHand;
        public Transform leftUpperArm,rightUpperArm,leftForearm,rightForearm;
        public float reach=.725f;
        public const float ArmLength=.475f;
        readonly ConfigurableJoint[] shoulders=new ConfigurableJoint[2];
        readonly bool[] carryingLastTick=new bool[2];
        readonly ConfigurableJoint[] grips=new ConfigurableJoint[2];
        readonly Collider[] targets=new Collider[2];
        readonly Collider[] nearby=new Collider[48];
        readonly Rigidbody[] hands=new Rigidbody[2];
        PlayerController player;
        bool wasHeld;
        public float GripStrength {get;private set;}=100;
        float nextImpact,releaseUntil;
        public void ResetGrip(){GripStrength=100;nextImpact=releaseUntil=0;}
        public void ApplyImpact(float impulse){if(!player.IsAuthority||impulse<240||Time.time<nextImpact)return;nextImpact=Time.time+.3f;GripStrength=Mathf.Max(0,GripStrength-60);if(GripStrength<=0){ReleaseAll();player.HeldHands.Value=0;releaseUntil=Time.time+.8f;player.PlaySound("release");}}
        public bool LeftHeld=>grips[0];
        public bool RightHeld=>grips[1];
        public float HeldMass
        {
            get{float mass=0;for(int i=0;i<2;i++)if(grips[i]&&grips[i].connectedBody&&!grips[i].connectedBody.isKinematic)mass+=grips[i].connectedBody.mass;return mass;}
        }
        void Awake()
        {
            player=GetComponent<PlayerController>();
            if(leftHand)hands[0]=leftHand.GetComponent<Rigidbody>();if(rightHand)hands[1]=rightHand.GetComponent<Rigidbody>();
            for(int i=0;i<2;i++)if(hands[i])
            {
                shoulders[i]=hands[i].GetComponent<ConfigurableJoint>();
                if(shoulders[i])shoulders[i].linearLimit=new SoftJointLimit{limit=ArmLength};
            }
            foreach(var hand in hands)if(hand)hand.gameObject.AddComponent<HandImpactRelay>().grab=this;
        }
        public void ConfigureAuthority(bool authority)
        {
            var colliders=GetComponentsInChildren<Collider>();
            foreach(var a in colliders)foreach(var b in colliders)if(a!=b)UnityEngine.Physics.IgnoreCollision(a,b);
            for(int i=0;i<2;i++)if(hands[i])hands[i].isKinematic=!authority;
        }
        public void Tick(bool left,bool right)
        {
            GripStrength=Mathf.Min(100,GripStrength+5*Time.fixedDeltaTime);
            bool canGrab=Time.time>=releaseUntil&&GripStrength>=10;
            Hand(0,left&&canGrab);Hand(1,right&&canGrab);
            player.HeldHands.Value=(byte)((LeftHeld?1:0)|(RightHeld?2:0));
            bool held=LeftHeld||RightHeld;
            if(held&&!wasHeld)player.PlaySound("grab");
            if(!held&&wasHeld)player.PlaySound("release",.5f);
            wasHeld=held;
        }
        Vector3 Shoulder(int i)=>transform.TransformPoint(new Vector3(i==0?-.32f:.32f,.3f,0));
        public Vector3 WeaponHandPosition=>Shoulder(1)+player.AimDirection*ArmLength;
        void Hand(int i,bool pressed)
        {
            if(!hands[i])return;
            bool carrying=i==1&&WeaponSystem.Held(player);
            if(carrying&&!carryingLastTick[i])
            {
                // Establish the held pose without the joint pulling the torso across the room.
                Release(i);hands[i].position=WeaponHandPosition;
                hands[i].linearVelocity=player.Body.GetPointVelocity(WeaponHandPosition);
                hands[i].angularVelocity=player.Body.angularVelocity;
            }
            carryingLastTick[i]=carrying;
            var arm=shoulders[i];
            if(arm)
            {
                arm.connectedAnchor=carrying?transform.InverseTransformPoint(WeaponHandPosition):new Vector3(i==0?-.32f:.32f,.3f,0);
                arm.xMotion=arm.yMotion=arm.zMotion=carrying?ConfigurableJointMotion.Locked:ConfigurableJointMotion.Limited;
            }
            if(carrying){Release(i);return;}
            if(!pressed){Release(i);ReachRest(i,false);return;}
            if(grips[i]&&(!targets[i]||!targets[i].enabled||!targets[i].gameObject.activeInHierarchy))Release(i);
            if(grips[i])return;
            ReachRest(i,true);
            Vector3 origin=hands[i].position;
            int count=UnityEngine.Physics.OverlapSphereNonAlloc(origin,.72f,nearby,~0,QueryTriggerInteraction.Ignore);
            Collider best=null;float score=float.MaxValue;
            for(int n=0;n<count;n++)
            {
                var c=nearby[n];if(c.transform.IsChildOf(transform))continue;
                if(!c.attachedRigidbody&&!c.GetComponent<GrabAnchor>())continue;
                var actor=c.GetComponentInParent<PlayerController>();if(actor&&!actor.Alive.Value)continue;
                Vector3 point=c.ClosestPoint(origin);
                if(Vector3.Distance(Shoulder(i),point)>reach)continue;
                Vector3 fromShoulder=point-Shoulder(i);
                float alignment=Vector3.Dot(fromShoulder.normalized,player.AimDirection);
                if(!player.IsBot.Value&&alignment<.55f)continue;
                float distance=Vector3.Distance(origin,point);
                if(UnityEngine.Physics.Linecast(origin,point,out var hit,~0,QueryTriggerInteraction.Ignore)&&hit.collider!=c&&!hit.transform.IsChildOf(transform))continue;
                float bias=player.IsBot.Value?(c.GetComponent<GrabAnchor>()?-.25f:0):(1-alignment)*.5f;
                if(distance+bias<score){best=c;score=distance+bias;}
            }
            if(best)
            {
                Vector3 point=best.ClosestPoint(origin);
                hands[i].AddForce(Vector3.ClampMagnitude((point-origin)*65-hands[i].linearVelocity*5,80),ForceMode.Acceleration);
                if(Vector3.Distance(point,origin)<.34f)Attach(i,best,point);
            }
        }
        void ReachRest(int i,bool reaching)
        {
            // Both free hands follow the view even before LMB. A held hand remains attached to its contact point.
            Vector3 destination=Shoulder(i)+player.AimDirection*(reaching?ArmLength:ArmLength*.83f);
            hands[i].AddForce(Vector3.ClampMagnitude((destination-hands[i].position)*125-hands[i].linearVelocity*12,90),ForceMode.Acceleration);
        }
        public void Attach(int i,Collider target,Vector3 point)
        {
            Release(i);if(!hands[i])return;
            var joint=hands[i].gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;
            joint.connectedBody=target.attachedRigidbody;
            joint.connectedAnchor=joint.connectedBody?joint.connectedBody.transform.InverseTransformPoint(point):point;
            joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Limited;
            joint.linearLimit=new SoftJointLimit{limit=.045f};
            joint.linearLimitSpring=new SoftJointLimitSpring{spring=5000,damper=110};
            joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Free;
            joint.breakForce=Mathf.Infinity;joint.breakTorque=Mathf.Infinity;
            joint.enableCollision=false;joint.projectionMode=JointProjectionMode.PositionAndRotation;joint.projectionDistance=.15f;
            grips[i]=joint;targets[i]=target;
        }
        public void Release(int i)
        {
            if(grips[i]){grips[i].xMotion=grips[i].yMotion=grips[i].zMotion=ConfigurableJointMotion.Free;Destroy(grips[i]);}
            grips[i]=null;targets[i]=null;
        }
        public float ReleaseRemaining=>Mathf.Max(0,releaseUntil-Time.time);
        public float ImpactRemaining=>Mathf.Max(0,nextImpact-Time.time);
        public void RestoreStrength(float strength,float release,float impact){GripStrength=strength;releaseUntil=Time.time+release;nextImpact=Time.time+impact;}
        public MigrationGrip[] CaptureGrips(MigrationContext context)
        {
            var result=new List<MigrationGrip>();for(int i=0;i<2;i++)if(grips[i]&&targets[i])result.Add(new MigrationGrip{hand=i,target=context.Reference(targets[i]),point=grips[i].connectedBody?grips[i].connectedBody.transform.TransformPoint(grips[i].connectedAnchor):grips[i].connectedAnchor});return result.ToArray();
        }
        public void RestoreGrips(MigrationGrip[] saved,MigrationContext context)
        {foreach(var s in saved){var target=context.Resolve(s.target,typeof(Collider)) as Collider;if(target&&target.enabled)Attach(s.hand,target,s.point);}player.HeldHands.Value=(byte)((LeftHeld?1:0)|(RightHeld?2:0));}
        public void ReleaseAll(){Release(0);Release(1);}
        public void ResetHands()
        {
            for(int i=0;i<2;i++)if(hands[i])
            {
                carryingLastTick[i]=false;
                if(shoulders[i]){shoulders[i].connectedAnchor=new Vector3(i==0?-.32f:.32f,.3f,0);shoulders[i].xMotion=shoulders[i].yMotion=shoulders[i].zMotion=ConfigurableJointMotion.Limited;}
                hands[i].position=Shoulder(i)+Vector3.down*.175f;hands[i].rotation=Quaternion.identity;
                hands[i].linearVelocity=Vector3.zero;hands[i].angularVelocity=Vector3.zero;
                if(!GameSession.Offline&&player.IsServer)
                    hands[i].GetComponent<Unity.Netcode.Components.NetworkTransform>()?.Teleport(hands[i].position,hands[i].rotation,Vector3.one*.23f);
            }
        }
        public bool HasStructuralAnchor(){for(int i=0;i<2;i++)if(grips[i]&&targets[i]&&targets[i].GetComponent<GrabAnchor>())return true;return false;}
        public IEnumerable<Rigidbody> ConnectedBodies()
        {
            for(int i=0;i<2;i++)if(grips[i]&&grips[i].connectedBody)
            {var body=grips[i].connectedBody;var other=body.GetComponentInParent<PlayerController>();yield return other?other.Body:body;}
        }
        void LateUpdate()
        {
            DrawArm(0,leftUpperArm,leftForearm);DrawArm(1,rightUpperArm,rightForearm);
        }
        void DrawArm(int i,Transform upper,Transform fore)
        {
            if(!hands[i]||!upper||!fore)return;
            Vector3 shoulder=Shoulder(i),hand=hands[i].position;
            bool carrying=i==1&&WeaponSystem.Held(player);
            Vector3 elbow=Vector3.Lerp(shoulder,hand,.5f)+(carrying?Vector3.zero:transform.right*(i==0?-.055f:.055f)+Vector3.down*.035f);
            Segment(upper,shoulder,elbow,.20f);Segment(fore,elbow,hand,.16f);
        }
        static void Segment(Transform t,Vector3 a,Vector3 b,float width)
        {t.position=(a+b)*.5f;t.rotation=Quaternion.FromToRotation(Vector3.up,b-a);t.localScale=new Vector3(width,Vector3.Distance(a,b)*.5f,width);}
    }
}









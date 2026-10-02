using UnityEngine;
namespace ElevatorGame
{
    public sealed class PropAnimator : MonoBehaviour
    {
        public Transform tail,jaw,head,leftLeg,rightLeg;
        public bool stabilize;
        NetworkProp prop;
        Transform gorillaLeftArm,gorillaRightArm;
        void Awake()
        {
            prop=GetComponent<NetworkProp>();
            foreach(var part in GetComponentsInChildren<Transform>())
                if(part.name=="Arm")
                {
                    if(part.localPosition.x<0)gorillaLeftArm=part;
                    else gorillaRightArm=part;
                }
        }
        void FixedUpdate()
        {
            if(prop&&prop.IsAuthority&&!prop.Body.isKinematic&&stabilize)
            {
                bool runner=prop.PrefabIndex==8;
                float spring=runner?130:35, damping=runner?18:5;
                prop.Body.AddTorque(Vector3.Cross(transform.up,Vector3.up)*spring-prop.Body.angularVelocity*damping,ForceMode.Acceleration);
            }
        }
        void LateUpdate()
        {
            if(!prop||!prop.IsActive)return;
            float now=(float)RoundManager.Instance.Clock;
            float t=now-(float)prop.ActionAt.Value;
            bool running=prop.Action.Value==5;
            float stride=running?Mathf.Sin(now*13)*28:Mathf.Sin(now*3)*3;
            if(leftLeg)leftLeg.localRotation=Quaternion.Euler(stride,0,0);
            if(rightLeg)rightLeg.localRotation=Quaternion.Euler(-stride,0,0);
            if(tail)tail.localRotation=Quaternion.Euler(0,prop.Action.Value==2?Mathf.Sin(Mathf.Clamp01((t-.4f)/1.3f)*Mathf.PI*2)*95:Mathf.Sin(now*2)*12,0);
            if(jaw)jaw.localRotation=Quaternion.Euler(prop.Action.Value==1||prop.Action.Value==3?Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI)*30:4,0,0);
            if(head)head.localRotation=Quaternion.Euler(prop.Action.Value==4?Mathf.Sin(Mathf.Clamp01(t/1.5f)*Mathf.PI)*28:Mathf.Sin(now*2)*2,0,0);
            if(prop.PrefabIndex==3)
            {
                float lift=prop.Action.Value==6?Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.35f)):
                    prop.Action.Value==7?1-Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.32f)):0;
                if(gorillaLeftArm){gorillaLeftArm.localPosition=new Vector3(-.83f,-.15f+lift*.7f,0);gorillaLeftArm.localRotation=Quaternion.Euler(0,0,-lift*68);}
                if(gorillaRightArm){gorillaRightArm.localPosition=new Vector3(.83f,-.15f+lift*.7f,0);gorillaRightArm.localRotation=Quaternion.Euler(0,0,lift*68);}
            }
        }
    }
}


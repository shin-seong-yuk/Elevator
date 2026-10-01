using UnityEngine;
namespace ElevatorGame
{
    public sealed class PropAnimator : MonoBehaviour
    {
        public Transform tail,jaw,head,leftLeg,rightLeg;
        public bool stabilize;
        NetworkProp prop;
        void Awake(){prop=GetComponent<NetworkProp>();}
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
        }
    }
}


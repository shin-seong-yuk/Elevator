using UnityEngine;
namespace ElevatorGame
{
    // Presentation uses the shared round clock, so every client sees the same tail and fin motion.
    public sealed class FishAnimator : MonoBehaviour
    {
        public Transform tail,leftFin,rightFin;
        NetworkProp prop;
        void Awake(){prop=GetComponent<NetworkProp>();}
        void LateUpdate()
        {
            if(!prop||!prop.IsActive||!RoundManager.Instance)return;
            float t=(float)RoundManager.Instance.Clock;
            float speed=prop.Action.Value>0?25:15;
            if(tail)tail.localRotation=Quaternion.Euler(0,Mathf.Sin(t*speed)*42,0);
            if(leftFin)leftFin.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*speed+1)*32);
            if(rightFin)rightFin.localRotation=Quaternion.Euler(0,0,-Mathf.Sin(t*speed+1)*32);
        }
    }
}

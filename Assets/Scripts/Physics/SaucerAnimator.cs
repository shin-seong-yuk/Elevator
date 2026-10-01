using UnityEngine;
namespace ElevatorGame
{
    public sealed class SaucerAnimator : MonoBehaviour
    {
        public Transform lightRing;
        public Renderer beam;
        NetworkProp prop;
        MaterialPropertyBlock block;
        void Awake(){prop=GetComponent<NetworkProp>();block=new MaterialPropertyBlock();}
        void LateUpdate()
        {
            if(!prop||!prop.IsActive||!RoundManager.Instance)return;
            float clock=(float)RoundManager.Instance.Clock;
            if(lightRing)lightRing.localRotation=Quaternion.Euler(0,clock*100,0);
            if(!beam)return;
            beam.GetPropertyBlock(block);
            block.SetColor("_BaseColor",new Color(.18f,.95f,.86f,.14f+.12f*(.5f+.5f*Mathf.Sin(clock*9))));
            beam.SetPropertyBlock(block);
        }
    }
}

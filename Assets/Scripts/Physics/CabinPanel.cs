using UnityEngine;
namespace ElevatorGame
{
    public sealed class CabinPanel : MonoBehaviour
    {
        public int index;
        public const float MaxHealth=500;
        float health=MaxHealth,nextHit;bool broken;
        void Update()
        {
            var r=RoundManager.Instance;if(!r)return;
            bool next=(r.BrokenPanels.Value&(1UL<<index))!=0;
            if(next==broken)return;broken=next;
            foreach(var c in GetComponentsInChildren<Collider>())c.enabled=!broken;
            foreach(var visual in GetComponentsInChildren<Renderer>())visual.enabled=!broken;
            if(!broken)health=MaxHealth;
        }
        public float Health=>health; public void RestoreHealth(float value){health=value;nextHit=0;}
        public void ResetHealth(){health=MaxHealth;nextHit=0;}
        void OnCollisionEnter(Collision c)
        {
            if(c.impulse.magnitude<700||Time.time<nextHit)return;nextHit=Time.time+.3f;
            Damage(Mathf.Clamp(c.impulse.magnitude/22,20,90));
        }
        public void Damage(float amount)
        {
            var r=RoundManager.Instance;if(!r||!r.IsAuthority||r.Phase.Value!=RoundPhase.Playing||broken)return;
            health-=Mathf.Max(0,amount);if(health>0)return;
            // Limit holes separately so a few heavy hits cannot remove the entire cabin.
            ulong mask=r.BrokenPanels.Value;int count=0;ulong section=index<16?mask&65535UL:mask>>16;for(ulong n=section;n!=0;n>>=1)count+=(int)(n&1);
            if(count>=(index<16?4:6))return;r.BrokenPanels.Value=mask|(1UL<<index);r.PlayCue("cartCrash");
        }
        public static void DamageNear(Vector3 point,float radius,float amount)
        {foreach(var panel in FindObjectsByType<CabinPanel>(FindObjectsSortMode.None))if(Vector3.Distance(panel.GetComponent<Collider>().ClosestPoint(point),point)<radius)panel.Damage(amount);}
    }
}



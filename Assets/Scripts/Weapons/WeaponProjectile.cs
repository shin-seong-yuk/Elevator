using UnityEngine;
namespace ElevatorGame
{
    public sealed class WeaponProjectile : MonoBehaviour
    {
        NetworkProp prop;float expires;bool grenade,armed,exploded;
        public int MigrationMode=>armed?(grenade?2:1):0;public float MigrationRemaining=>Mathf.Max(0,expires-Time.time);
        public void RestoreMigration(int mode,float remaining){prop=GetComponent<NetworkProp>();grenade=mode==2;armed=mode>0;expires=Time.time+remaining;exploded=false;}
        public void Initialize(PlayerController owner,bool bounce)
        {
            prop=GetComponent<NetworkProp>();grenade=bounce;armed=true;expires=Time.time+(bounce?2:4);prop.Body.useGravity=bounce;prop.Animate(bounce?2:1);
            foreach(var a in GetComponents<Collider>())foreach(var b in owner.GetComponentsInChildren<Collider>())UnityEngine.Physics.IgnoreCollision(a,b);
        }
        public void InitializeEvent(float fuse)
        {
            prop=GetComponent<NetworkProp>();grenade=false;armed=true;expires=Time.time+fuse;
            prop.Body.useGravity=false;prop.Animate(1);
        }
        bool presented;
        void Update()
        {
            if(!prop)prop=GetComponent<NetworkProp>();
            if(presented||prop.Action.Value==0)return;presented=true;
            var renderer=GetComponentInChildren<Renderer>();if(!renderer)return;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",prop.Action.Value==2?new Color(.3f,1,.3f):new Color(1,.55f,.1f));renderer.SetPropertyBlock(block);
            var trail=gameObject.AddComponent<TrailRenderer>();var environment=FindFirstObjectByType<EnvironmentEffects>();trail.sharedMaterial=environment&&environment.airStreakMaterial?environment.airStreakMaterial:renderer.sharedMaterial;trail.time=prop.Action.Value==2?.12f:.24f;trail.startWidth=prop.Action.Value==2?.12f:.22f;trail.endWidth=0;trail.minVertexDistance=.08f;trail.SetPropertyBlock(block);
        }
        void FixedUpdate(){if(armed&&prop&&prop.IsAuthority&&Time.time>=expires)Explode();}
        void OnCollisionEnter(Collision collision){if(armed&&prop.IsAuthority&&!grenade)Explode();}
        public void Explode()
        {
            if(exploded||!prop||!prop.IsAuthority)return;exploded=true;Vector3 center=transform.position;
            foreach(var p in RoundManager.Players())
            {float d=Vector3.Distance(center,p.Body.position);if(p.Alive.Value&&d<4)p.Knock((p.Body.position-center).normalized*(14*(1-d/5))+Vector3.up*4,1.5f);}
            foreach(var body in FindObjectsByType<NetworkProp>(FindObjectsSortMode.None))if(body!=prop&&!body.Body.isKinematic)body.Body.AddExplosionForce(850,center,4,1.5f,ForceMode.Impulse);
            CabinPanel.DamageNear(center,3,85);RoundManager.Instance.WeaponEffect(center,center,20);

            prop.Remove();
        }
    }
}






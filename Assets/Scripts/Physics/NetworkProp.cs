using Unity.Netcode;
using UnityEngine;
namespace ElevatorGame
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NetworkProp : NetworkBehaviour
    {
        public int PrefabIndex=-1;public int MigrationKey=-1; public bool persistent; public float impactBoost; private float nextImpact;
        readonly NetworkVariable<int> netAction=new(0); SessionValue<int> localAction; public SessionValue<int> Action=>localAction??=new SessionValue<int>(netAction,0);
        readonly NetworkVariable<double> netActionAt=new(0); SessionValue<double> localActionAt; public SessionValue<double> ActionAt=>localActionAt??=new SessionValue<double>(netActionAt,0);
        public void Animate(int action){Action.Value=action;ActionAt.Value=RoundManager.Instance.Clock;}
        public bool IsActive=>GameSession.Offline||IsSpawned;
        public bool IsAuthority=>GameSession.Offline||IsServer;
        public Rigidbody Body{get;private set;}
        void Awake(){Body=GetComponent<Rigidbody>();}
        public override void OnNetworkSpawn(){Body.isKinematic=!IsServer;}
        public void InitializeOffline(){GetComponent<Unity.Netcode.Components.NetworkTransform>().enabled=false;Body.isKinematic=false;}
        public void Remove(){if(GameSession.Offline)Destroy(gameObject);else if(IsSpawned&&IsServer)NetworkObject.Despawn();}
        void FixedUpdate(){if(IsAuthority&&IsActive&&(transform.position.y< -18||transform.position.sqrMagnitude>40000))Remove();}
        void OnCollisionEnter(Collision c)
        {
            float speed=c.relativeVelocity.magnitude;
            if(speed>3)AudioManager.Instance?.PlayAt("impact",transform.position,.45f);
            var thrownWeapon=GetComponent<WeaponPickup>();
            if(thrownWeapon&&thrownWeapon.ActiveThrow)return;
            if(!IsAuthority||impactBoost<=0||speed<4||Time.time<nextImpact)return;
            var actor=c.collider.GetComponentInParent<PlayerController>();
            if(!actor||!actor.Alive.Value)return;
            nextImpact=Time.time+.15f;
            Vector3 away=actor.Body.position-Body.position;away.y=0;
            actor.Knock(away.normalized*Mathf.Min(11,speed*.4f)*impactBoost+Vector3.up*3.5f,1.5f);
            AudioManager.Instance?.PlayAt("cartCrash",transform.position,.65f);
        }
    }
}






using Unity.Netcode;
using UnityEngine;

namespace ElevatorGame
{
    // A physical, host-owned arrow becomes a harmless visible attachment on impact.
    public sealed class HwachaArrow : NetworkBehaviour
    {
        readonly NetworkVariable<int> netStuckSlot=new(-1);
        readonly NetworkVariable<Vector3> netLocalPosition=new(Vector3.zero);
        readonly NetworkVariable<Quaternion> netLocalRotation=new(Quaternion.identity);
        SessionValue<int> stuckSlot;
        SessionValue<Vector3> localPosition;
        SessionValue<Quaternion> localRotation;
        NetworkProp prop;
        PlayerController target;
        float bornAt;

        public SessionValue<int> StuckSlot=>stuckSlot??=new(netStuckSlot,-1);
        public SessionValue<Vector3> LocalPosition=>localPosition??=new(netLocalPosition,Vector3.zero);
        public SessionValue<Quaternion> LocalRotation=>localRotation??=new(netLocalRotation,Quaternion.identity);

        void Awake(){prop=GetComponent<NetworkProp>();bornAt=Time.time;}

        void FixedUpdate()
        {
            if(!prop.IsAuthority||!prop.IsActive)return;
            if(StuckSlot.Value<0&&Time.time-bornAt>6)prop.Remove();
            else if(StuckSlot.Value>=0&&(!FindTarget()||!target.Alive.Value))prop.Remove();
        }

        void LateUpdate()
        {
            if(StuckSlot.Value<0)return;
            if(!FindTarget())return;
            transform.SetPositionAndRotation(target.transform.TransformPoint(LocalPosition.Value),target.transform.rotation*LocalRotation.Value);
            var collider=GetComponent<Collider>();if(collider)collider.enabled=false;
        }

        bool FindTarget()
        {
            if(target&&target.Slot.Value==StuckSlot.Value)return true;
            target=null;
            foreach(var player in RoundManager.Players())if(player.Slot.Value==StuckSlot.Value){target=player;break;}
            return target;
        }

        void OnCollisionEnter(Collision collision)
        {
            if(!prop.IsAuthority||StuckSlot.Value>=0)return;
            var player=collision.collider.GetComponentInParent<PlayerController>();
            if(!player||!player.Alive.Value)return;
            Vector3 direction=prop.Body.linearVelocity.normalized;
            Vector3 point=collision.contactCount>0?collision.GetContact(0).point:transform.position;
            Stick(player,point-direction*.25f,transform.rotation);
            player.Knock(direction*1.6f+Vector3.up*.3f,.08f);
            player.PlaySound("hit",.4f);
        }

        public void Stick(PlayerController player,Vector3 position,Quaternion rotation)
        {
            if(!prop.IsAuthority||!player||!player.Alive.Value)return;
            target=player;
            LocalPosition.Value=player.transform.InverseTransformPoint(position);
            LocalRotation.Value=Quaternion.Inverse(player.transform.rotation)*rotation;
            StuckSlot.Value=player.Slot.Value;
            prop.persistent=true;
            prop.Body.linearVelocity=Vector3.zero;
            prop.Body.angularVelocity=Vector3.zero;
            prop.Body.isKinematic=true;
            var collider=GetComponent<Collider>();if(collider)collider.enabled=false;
            transform.SetPositionAndRotation(position,rotation);
        }

        public void RestoreStuck(int slot,Vector3 offset,Quaternion rotation)
        {
            if(slot<0)return;
            LocalPosition.Value=offset;LocalRotation.Value=rotation;StuckSlot.Value=slot;
            prop.persistent=true;prop.Body.isKinematic=true;
            var collider=GetComponent<Collider>();if(collider)collider.enabled=false;
        }
    }
}

using Unity.Netcode;
using UnityEngine;
namespace ElevatorGame
{
    public enum WeaponKind { Bat, Hammer, Pistol, Shotgun, Bazooka, GrenadeLauncher, BoxingGlove, ShockStaff, Blower, Grappler }
    public sealed class WeaponPickup : NetworkBehaviour
    {
        readonly NetworkVariable<int> netKind=new(0),netHolder=new(-1),netFloor=new(0);
        readonly NetworkVariable<int> netAmmo=new(-2);
        SessionValue<int> kind,holder,born,ammo;
        int ammoKind=-1;
        public SessionValue<int> Ammo=>ammo??=new(netAmmo,-2);
        public int Capacity=>AmmunitionCapacity((WeaponKind)Kind.Value);
        public int RemainingAmmo=>Ammo.Value==-2?Capacity:Ammo.Value;
        public bool HasAmmo=>Capacity<0||RemainingAmmo>0;
        public static int AmmunitionCapacity(WeaponKind type)=>type switch
        {
            WeaponKind.Pistol=>12,WeaponKind.Shotgun=>6,WeaponKind.Bazooka=>3,
            WeaponKind.GrenadeLauncher=>4,WeaponKind.Grappler=>6,WeaponKind.Blower=>20,_=>-1
        };
        public void RestoreAmmo(int remaining){ammoKind=Kind.Value;Ammo.Value=Capacity<0?-1:Mathf.Clamp(remaining,0,Capacity);}
        public bool ConsumeAmmo()
        {
            if(!Prop.IsAuthority)return false;
            if(ammoKind!=Kind.Value||Ammo.Value==-2){ammoKind=Kind.Value;Ammo.Value=Capacity;}
            if(!HasAmmo)return false;
            if(Capacity>0)Ammo.Value--;
            return true;
        }
        public SessionValue<int> Kind=>kind??=new(netKind,0);
        public SessionValue<int> Holder=>holder??=new(netHolder,-1);
        public SessionValue<int> BornFloor=>born??=new(netFloor,0);
        public Material visualMaterial;
        public NetworkProp Prop {get;private set;}
        int painted=-1; GameObject model;
        public string DisplayName=>((WeaponKind)Kind.Value).ToString();
        void Awake(){Prop=GetComponent<NetworkProp>();}
        void FixedUpdate()
        {
            if(!Prop.IsActive)return;
            var owner=FindHolder();
            foreach(var c in GetComponents<Collider>())c.enabled=!owner;
            if(!Prop.IsAuthority)return;
            if(Holder.Value>=0&&(!owner||!owner.Alive.Value)){Holder.Value=-1;owner=null;}
            Prop.Body.isKinematic=owner;
            if(owner)
            {
                Prop.Body.MovePosition(owner.Grab.WeaponHandPosition+Quaternion.LookRotation(owner.AimDirection)*Vector3.up*.13f);
                Prop.Body.MoveRotation(Quaternion.LookRotation(owner.AimDirection));
            }
        }
        public PlayerController FindHolder(){if(Holder.Value<0)return null;foreach(var p in RoundManager.Players())if(p.Slot.Value==Holder.Value)return p;return null;}
        void Update()
        {
            if(painted==Kind.Value)return;painted=Kind.Value;
            if(model)Destroy(model);model=new GameObject("Weapon model");model.transform.SetParent(transform,false);
            WeaponVisuals.Build(model.transform,(WeaponKind)painted,visualMaterial);
        }
        public Vector3 GripPosition=>model?model.transform.TransformPoint(new Vector3(0,-.13f,0)):transform.position;
        void LateUpdate()
        {
            if(!model)return;
            model.transform.localRotation=Quaternion.identity;model.transform.localPosition=Vector3.zero;
            if(Holder.Value<0||Prop.Action.Value<=0||!RoundManager.Instance)return;
            float t=(float)(RoundManager.Instance.Clock-Prop.ActionAt.Value);
            if(t<0||t>.48f)return;
            float hit=Mathf.Exp(-t*11),sweep=Mathf.Sin(Mathf.Clamp01(t/.4f)*Mathf.PI);
            switch((WeaponKind)painted)
            {
                case WeaponKind.Bat: model.transform.localRotation=Quaternion.Euler(-70*sweep,65*sweep,-35*sweep);break;
                case WeaponKind.Hammer: model.transform.localRotation=Quaternion.Euler(-105*sweep,0,-15*sweep);break;
                case WeaponKind.ShockStaff: model.transform.localRotation=Quaternion.Euler(-65*sweep,35*sweep,0);break;
                case WeaponKind.BoxingGlove: model.transform.localPosition=Vector3.forward*.65f*Mathf.Sin(Mathf.Clamp01(t/.25f)*Mathf.PI);break;
                default: model.transform.localPosition=Vector3.back*hit*.23f;model.transform.localRotation=Quaternion.Euler(-18*hit,0,3*hit);break;
            }
        }
    }
}

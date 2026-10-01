using System.Collections.Generic;
using UnityEngine;
namespace ElevatorGame
{
    public sealed class WeaponSystem : MonoBehaviour
    {
        readonly Dictionary<int,float> useAt=new(),interactAt=new();
        static WeaponSystem Instance=>RoundManager.Instance?RoundManager.Instance.GetComponent<WeaponSystem>():null;
        public static WeaponPickup Held(PlayerController p){foreach(var w in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))if(w.Prop.IsActive&&w.Holder.Value==p.Slot.Value)return w;return null;}
        public void ResetAll(){foreach(var w in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))if(w.Prop.IsAuthority){w.Holder.Value=-1;foreach(var c in w.GetComponents<Collider>())c.enabled=false;w.Prop.Body.isKinematic=true;w.Prop.Remove();}useAt.Clear();interactAt.Clear();}
        public void CaptureCooldowns(out int[] slots,out float[] times){slots=new int[useAt.Count];times=new float[useAt.Count];int i=0;foreach(var pair in useAt){slots[i]=pair.Key;times[i++]=Mathf.Max(0,pair.Value-Time.time);}}
        public void RestoreCooldowns(int[] slots,float[] times){useAt.Clear();interactAt.Clear();for(int i=0;i<slots.Length;i++)useAt[slots[i]]=Time.time+times[i];}
        public void OnFloor(int floor)
        {
            foreach(var w in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))if(floor-w.BornFloor.Value>=4)w.Prop.Remove();
            var r=RoundManager.Instance;
            var prop=r.events.SpawnProp(9,new Vector3(r.events.Random(-1.5f,1.5f),1.6f,r.events.Random(-.7f,1.4f)),Vector3.zero,true);
            var weapon=prop.GetComponent<WeaponPickup>();weapon.Kind.Value=r.events.RandomInt(0,10);weapon.BornFloor.Value=floor;
        }
        public static void PickupOrThrow(PlayerController p)
        {
            if(!Instance||!p.IsAuthority)return;int slot=p.Slot.Value;
            if(Instance.interactAt.TryGetValue(slot,out float next)&&Time.time<next)return;
            Instance.interactAt[slot]=Time.time+.35f;
            if(Held(p)){Drop(p,true);return;}
            WeaponPickup nearest=null;float distance=1.8f;
            foreach(var w in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
            {
                if(!w.Prop.IsActive||w.Holder.Value>=0)continue;float d=Vector3.Distance(p.Body.position,w.transform.position);
                if(d<distance){distance=d;nearest=w;}
            }
            if(nearest){nearest.Holder.Value=slot;nearest.Prop.Body.linearVelocity=Vector3.zero;nearest.Prop.Body.angularVelocity=Vector3.zero;p.PlaySound("grab");}
        }
        public static void Drop(PlayerController p,bool thrown)
        {
            if(!p.IsAuthority)return;var w=Held(p);if(!w)return;w.Holder.Value=-1;w.Prop.Body.isKinematic=false;
            w.Prop.Body.position=p.Body.position+Vector3.up*.6f+p.AimDirection*1.1f;
            Vector3 velocity=p.Body.linearVelocity+(thrown?p.AimDirection*12+Vector3.up*2:Vector3.zero);
            if(thrown)
            {
                // A blast may knock the carrier backward; F still has to throw forward.
                float forward=Vector3.Dot(velocity,p.AimDirection);
                if(forward<10)velocity+=p.AimDirection*(10-forward);
            }
            w.Prop.Body.linearVelocity=velocity;
            w.Prop.Body.angularVelocity=thrown?p.transform.right*12:Vector3.zero;
            foreach(var c in w.GetComponents<Collider>())c.enabled=true;
            if(thrown)p.PlaySound("throw");
        }
        public static void Disarm(PlayerController p,Vector3 impactVelocity)
        {
            if(!p.IsAuthority)return;
            var w=Held(p);if(!w)return;
            w.Holder.Value=-1;
            w.Prop.Body.isKinematic=false;
            w.Prop.Body.position=p.Grab.WeaponHandPosition+impactVelocity.normalized*.25f;
            w.Prop.Body.linearVelocity=p.Body.linearVelocity+impactVelocity*.4f+Vector3.up*1.5f;
            w.Prop.Body.angularVelocity=Vector3.Cross(Vector3.up,impactVelocity.normalized)*9f+p.transform.right*3f;
            foreach(var c in w.GetComponents<Collider>())c.enabled=true;
            p.PlaySound("drop");
        }
        public static void Use(PlayerController p)
        {
            if(!Instance||!p.IsAuthority||!p.Alive.Value)return;var w=Held(p);if(!w)return;
            if(Instance.useAt.TryGetValue(p.Slot.Value,out float next)&&Time.time<next)return;
            var kind=(WeaponKind)w.Kind.Value;
            if(!w.ConsumeAmmo())return;
            float cooldown=Cooldown(kind);
            Instance.useAt[p.Slot.Value]=Time.time+cooldown;
            w.Prop.Animate(w.Prop.Action.Value+1);p.PlaySound(CombatFeedback.AttackCue(kind));
            Vector3 start=p.Body.position+Vector3.up*.55f,aim=p.AimDirection;
            var r=RoundManager.Instance;
            if(kind==WeaponKind.Bazooka||kind==WeaponKind.GrenadeLauncher)
            {
                var prop=r.events.SpawnProp(10,start+aim*1.05f,aim*(kind==WeaponKind.Bazooka?22:12)+(kind==WeaponKind.GrenadeLauncher?Vector3.up*4:Vector3.zero),false);
                var projectile=prop.GetComponent<WeaponProjectile>();projectile.Initialize(p,kind==WeaponKind.GrenadeLauncher);r.WeaponEffect(start,start+aim*.7f,(int)kind);return;
            }
            if(kind==WeaponKind.Bat||kind==WeaponKind.Hammer||kind==WeaponKind.BoxingGlove||kind==WeaponKind.ShockStaff||kind==WeaponKind.Blower)
            {
                float range=kind==WeaponKind.Blower?4:kind==WeaponKind.ShockStaff?3:2.2f;
                foreach(var target in RoundManager.Players())
                {
                    var offset=target.Body.position-p.Body.position;
                    if(target==p||!target.Alive.Value||offset.magnitude>range||Vector3.Dot(offset.normalized,aim)<(kind==WeaponKind.ShockStaff?-.3f:.25f))continue;
                    if(UnityEngine.Physics.Linecast(start,target.Body.position,1<<8,QueryTriggerInteraction.Ignore))continue;
                    float force=kind==WeaponKind.Hammer?13:kind==WeaponKind.BoxingGlove?10:kind==WeaponKind.Blower?3:8;
                    target.Knock(aim*force+Vector3.up*(kind==WeaponKind.BoxingGlove?6:2),kind==WeaponKind.Blower?.15f:1);
                }
                if(kind==WeaponKind.Hammer)CabinPanel.DamageNear(start+aim*1.5f,1.7f,65);
                r.WeaponEffect(start,start+aim*range,(int)kind);return;
            }
            int shots=kind==WeaponKind.Shotgun?6:1;
            for(int i=0;i<shots;i++)
            {
                Vector3 direction=(aim+(kind==WeaponKind.Shotgun?Random.insideUnitSphere*.14f:Vector3.zero)).normalized;
                Vector3 end=start+direction*25;RaycastHit chosen=default;float closest=25;
                foreach(var hit in UnityEngine.Physics.SphereCastAll(start,.08f,direction,25,~0,QueryTriggerInteraction.Ignore))
                {if(hit.collider.transform.IsChildOf(p.transform)||hit.collider.GetComponentInParent<WeaponPickup>()==w)continue;if(hit.distance<closest){closest=hit.distance;chosen=hit;}}
                if(chosen.collider)
                {
                    end=chosen.point;var target=chosen.collider.GetComponentInParent<PlayerController>();
                    if(target&&target.Alive.Value)target.Knock((kind==WeaponKind.Grappler?-direction:direction)*(kind==WeaponKind.Shotgun?3.5f:8)+Vector3.up*1.5f,.7f);
                    else if(chosen.rigidbody&&!chosen.rigidbody.isKinematic)chosen.rigidbody.AddForceAtPosition(direction*150,chosen.point,ForceMode.Impulse);
                    var panel=chosen.collider.GetComponentInParent<CabinPanel>();if(panel)panel.Damage(kind==WeaponKind.Shotgun?8:25);
                }
                r.WeaponEffect(start,end,(int)kind);
            }

        }
        // Longer reach/impulse takes longer to wind up again. Shared by humans, bots and UI/tests.
        public static float MeleeCooldown(float knockback,float stun)=>.45f+knockback*.065f+stun*.4f;
        public static float Cooldown(WeaponKind kind)=>kind switch
        {
            WeaponKind.Bat or WeaponKind.ShockStaff=>MeleeCooldown(Mathf.Sqrt(8*8+2*2),1),
            WeaponKind.BoxingGlove=>MeleeCooldown(Mathf.Sqrt(10*10+6*6),1),
            WeaponKind.Hammer=>MeleeCooldown(Mathf.Sqrt(13*13+2*2),1),
            WeaponKind.Bazooka=>2.4f,WeaponKind.GrenadeLauncher=>1.8f,
            WeaponKind.Blower=>.25f,WeaponKind.Pistol=>.45f,_=>.8f
        };
        public static void ShowEffect(Vector3 start,Vector3 end,int kind)
        {
            var all=FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
            Material material=all.Length>0?all[0].visualMaterial:null;
            if(!material){var p=FindFirstObjectByType<PlayerController>();if(p){var renderer=p.GetComponentInChildren<Renderer>();if(renderer)material=renderer.sharedMaterial;}}
            var environment=FindFirstObjectByType<EnvironmentEffects>();
            if(environment&&environment.airStreakMaterial)material=environment.airStreakMaterial;
            if(material)CombatFeedback.Show(start,end,kind,material);
        }
    }
}



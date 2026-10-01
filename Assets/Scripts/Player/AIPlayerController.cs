using UnityEngine;
namespace ElevatorGame
{
    public interface IAIHazard
    {
        Vector3 GetDangerDirection();
        float GetDangerStrength();
        Vector3 GetRecommendedSafeDirection(Vector3 position);
    }
    public enum AIState { Idle, MoveToSafeArea, AvoidHazard, GrabObject, Recover, Hanging, Eliminated, SeekWeapon, Combat }
    public sealed class AIPlayerController : MonoBehaviour
    {
        public AIState State{get;private set;}
        public Vector3 DangerDirection{get;private set;}
        public Vector3 SafeDirection{get;private set;}
        public Vector3 Target{get;private set;}
        [Range(0,1)] public float bravery,clumsiness,grabFrequency;
        public float reactionSpeed,movementAggressiveness;
        PlayerController actor;
        Vector3 desired;bool grab;float nextSense,nextDecision,nextWander,gripStarted=-1;
        readonly Collider[] nearby=new Collider[32];
        public bool DebugGrab;
        Vector3 combatAim; bool aiming;
        public int WeaponsPickedUp {get;private set;}
        public int WeaponUses {get;private set;}
        public void Initialize()
        {
            actor=GetComponent<PlayerController>();
            var random=new System.Random(7919+actor.Slot.Value*479);
            bravery=(float)random.NextDouble();clumsiness=.1f+(float)random.NextDouble()*.6f;
            grabFrequency=.5f+(float)random.NextDouble()*.45f;
            reactionSpeed=.85f+(float)random.NextDouble()*.3f;movementAggressiveness=.7f+(float)random.NextDouble()*.3f;
            ResetBrain();
        }
        public void ResetBrain(){nextSense=nextDecision=nextWander=0;gripStarted=-1;grab=false;aiming=false;desired=Vector3.zero;WeaponsPickedUp=WeaponUses=0;State=AIState.Idle;}
        void Update()
        {
            if(!actor)actor=GetComponent<PlayerController>();
            if(!actor.IsActive||!actor.IsAuthority||!actor.IsBot.Value)return;
            if(!actor.Alive.Value){State=AIState.Eliminated;return;}
            if(Time.time<nextSense)return;
            nextSense=Time.time+.2f;
            Sense();
            if(Time.time<nextDecision)return;
            var difficulty=GameSession.Instance?GameSession.Instance.Difficulty:AIDifficulty.Normal;
            float delay=difficulty==AIDifficulty.Easy?Random.Range(1,1.5f):difficulty==AIDifficulty.Hard?Random.Range(.15f,.5f):Random.Range(.5f,.9f);
            nextDecision=Time.time+delay*reactionSpeed;
            Decide(difficulty);
        }
        void Sense()
        {
            DangerDirection=SafeDirection=Vector3.zero;
            var round=RoundManager.Instance;
            if(round&&round.Stage.Value==ElevatorStage.Event)
                foreach(var h in round.events.Hazards)
                {DangerDirection+=h.GetDangerDirection()*h.GetDangerStrength();SafeDirection+=h.GetRecommendedSafeDirection(transform.position)*h.GetDangerStrength();}
            int count=UnityEngine.Physics.OverlapSphereNonAlloc(transform.position,5,nearby,1<<10,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var body=nearby[i].attachedRigidbody;if(!body||body.isKinematic)continue;
                Vector3 velocity=body.linearVelocity;Vector3 toMe=transform.position-body.position;toMe.y=0;velocity.y=0;
                if(velocity.sqrMagnitude<4||Vector3.Dot(toMe,velocity)<0)continue;
                float eta=Mathf.Clamp(Vector3.Dot(toMe,velocity)/velocity.sqrMagnitude,0,1.3f);
                if((body.position+velocity*eta-transform.position).sqrMagnitude<3.2f)
                {
                    Vector3 lateral=Vector3.Cross(Vector3.up,velocity.normalized);
                    if(Vector3.Dot(lateral,transform.position)>0)lateral=-lateral;
                    SafeDirection+=lateral*2.3f;DangerDirection+=velocity.normalized*1.5f;
                }
            }
        }
        void Decide(AIDifficulty difficulty)
        {
            aiming=false;Vector3 pos=transform.position;
            if(!PlayerEliminationController.Inside(pos))
            {
                State=AIState.Hanging;desired=new Vector3(-pos.x,0,-1-pos.z).normalized;grab=true;
                if(gripStarted<0)gripStarted=Time.time;
                if(Time.time-gripStarted>7+bravery*5&&Random.value<.2f)grab=false;
                return;
            }
            gripStarted=-1;
            if(actor.Stunned){State=AIState.Recover;desired=Vector3.zero;grab=DebugGrab;return;}
            float error=difficulty==AIDifficulty.Easy?.24f:difficulty==AIDifficulty.Hard?.05f:.12f;
            if(DangerDirection.magnitude>.3f)
            {
                State=AIState.AvoidHazard;
                desired=SafeDirection.normalized;
                bool suction=Vector3.Dot(DangerDirection.normalized,Vector3.forward)>.6f;
                if(suction)
                {
                    float side=pos.x>=0?1:-1;
                    Target=new Vector3(side*4.1f,1,-3.7f);
                    desired=(Target-pos);desired.y=0;desired.Normalize();
                    if(pos.z< -3.25f)desired=new Vector3(side,0,0);
                }
                bool closeWall=Mathf.Abs(pos.x)>3.5f||pos.z< -3.4f;
                grab=(closeWall||pos.z>3.15f||actor.Body.linearVelocity.z>2)&&Random.value<grabFrequency;
                if(grab)State=AIState.GrabObject;
                if(Random.value<error*clumsiness){desired=Quaternion.Euler(0,Random.Range(-100,100),0)*desired;grab=false;}
            }
            else if(Mathf.Abs(pos.x)>3.65f||pos.z>2.8f||pos.z< -3.65f)
            {State=AIState.MoveToSafeArea;desired=new Vector3(-pos.x,0,-.65f-pos.z).normalized;grab=pos.z>3.6f;}
            else if(TryWeapons(difficulty)) { }
            else
            {
                grab=false;
                if(Time.time>nextWander)
                {
                    nextWander=Time.time+Random.Range(2,5);
                    Target=new Vector3(Random.Range(-2.6f,2.6f),pos.y,Random.Range(-2.8f,1.3f));
                }
                desired=Target-pos;desired.y=0;
                if(desired.magnitude<.4f){desired=Vector3.zero;State=AIState.Idle;}else{desired.Normalize();State=AIState.MoveToSafeArea;}
            }
            grab|=DebugGrab;
        }
        bool TryWeapons(AIDifficulty difficulty)
        {
            var round=RoundManager.Instance;
            if(!round||round.Phase.Value!=RoundPhase.Playing)return false;
            var held=WeaponSystem.Held(actor);Vector3 pos=actor.Body.position;
            if(held&&!held.HasAmmo){actor.WeaponAction(true,false);return false;}
            if(!held)
            {
                WeaponPickup nearest=null;float best=5;
                foreach(var w in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                {
                    if(!w.Prop.IsActive||!w.HasAmmo||w.Holder.Value>=0||!PlayerEliminationController.Inside(w.transform.position))continue;
                    float distance=Vector3.Distance(pos,w.transform.position);
                    if(distance>=best||UnityEngine.Physics.Linecast(pos,w.transform.position,1<<8,QueryTriggerInteraction.Ignore))continue;
                    nearest=w;best=distance;
                }
                if(!nearest)return false;
                State=AIState.SeekWeapon;Target=nearest.transform.position;grab=DebugGrab;
                desired=Target-pos;desired.y=0;desired=Vector3.ClampMagnitude(desired,1);
                if(best<1.65f){actor.WeaponAction(true,false);if(WeaponSystem.Held(actor)){WeaponsPickedUp++;desired=Vector3.zero;}}
                return true;
            }
            PlayerController target=null;float closest=9;
            foreach(var p in RoundManager.Players())
            {
                if(p==actor||!p.Alive.Value||!PlayerEliminationController.Inside(p.Body.position))continue;
                float distance=Vector3.Distance(pos,p.Body.position);
                if(distance<closest&&!UnityEngine.Physics.Linecast(pos+Vector3.up*.55f,p.Body.position+Vector3.up*.2f,1<<8,QueryTriggerInteraction.Ignore)){closest=distance;target=p;}
            }
            if(!target)return false;
            var kind=(WeaponKind)held.Kind.Value;
            bool explosive=kind==WeaponKind.Bazooka||kind==WeaponKind.GrenadeLauncher;
            float range=kind==WeaponKind.Bat||kind==WeaponKind.Hammer||kind==WeaponKind.BoxingGlove?1.85f:kind==WeaponKind.ShockStaff?2.6f:kind==WeaponKind.Blower?3.5f:kind==WeaponKind.Shotgun?4.5f:7;
            State=AIState.Combat;Target=target.Body.position;grab=DebugGrab;
            Vector3 offset=Target-pos;offset.y=0;
            desired=closest>range?offset.normalized:explosive&&closest<3?-offset.normalized:Vector3.zero;
            // Aim is independent of movement: a bot can back away while facing its opponent.
            float error=difficulty==AIDifficulty.Easy?12:difficulty==AIDifficulty.Hard?3:7;
            combatAim=Quaternion.Euler(Random.Range(-error,error)*.35f,Random.Range(-error,error)*(1+clumsiness),0)*(Target+Vector3.up*.2f-(pos+Vector3.up*.55f)).normalized;
            aiming=true;ApplyInput();
            if(closest<=range&&(!explosive||closest>=2.7f)&&Random.value>clumsiness*.2f)
            {int before=held.Prop.Action.Value;actor.WeaponAction(false,true);if(held.Prop.Action.Value!=before)WeaponUses++;}
            return true;
        }
        public void ApplyInput()
        {
            if(!actor)return;
            float yaw=aiming?Mathf.Atan2(combatAim.x,combatAim.z)*Mathf.Rad2Deg:desired.sqrMagnitude>.02f?Mathf.Atan2(desired.x,desired.z)*Mathf.Rad2Deg:actor.transform.eulerAngles.y;
            float pitch=aiming?-Mathf.Asin(Mathf.Clamp(combatAim.y,-1,1))*Mathf.Rad2Deg:0;
            Vector3 local=Quaternion.Euler(0,-yaw,0)*desired;
            actor.SetMoveInput(new Vector2(local.x,local.z)*movementAggressiveness,yaw,false,grab,pitch);
        }        void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.red;Gizmos.DrawRay(transform.position,DangerDirection);
            Gizmos.color=Color.green;Gizmos.DrawRay(transform.position,SafeDirection);
            Gizmos.color=Color.cyan;Gizmos.DrawLine(transform.position,Target);Gizmos.DrawWireSphere(Target,.2f);
        }
    }
}



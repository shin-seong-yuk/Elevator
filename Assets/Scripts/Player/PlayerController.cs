using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElevatorGame
{
    [RequireComponent(typeof(Rigidbody), typeof(PlayerGrabController))]
    public sealed class PlayerController : NetworkBehaviour
    {
        public readonly NetworkVariable<ulong> SteamId=new(0);
        readonly NetworkVariable<bool> netAlive = new(true);
        SessionValue<bool> localAlive;
        public SessionValue<bool> Alive => localAlive ??= new SessionValue<bool>(netAlive,true);
        readonly NetworkVariable<bool> netReady = new(false);
        SessionValue<bool> localReady;
        public SessionValue<bool> Ready => localReady ??= new SessionValue<bool>(netReady,false);
        readonly NetworkVariable<bool> netIsBot = new(false);
        SessionValue<bool> localIsBot;
        public SessionValue<bool> IsBot => localIsBot ??= new SessionValue<bool>(netIsBot,false);
        readonly NetworkVariable<int> netSlot = new(0);
        SessionValue<int> localSlot;
        public SessionValue<int> Slot => localSlot ??= new SessionValue<int>(netSlot,0);
        readonly NetworkVariable<byte> netHeldHands = new(0);
        SessionValue<byte> localHeldHands;
        public SessionValue<byte> HeldHands => localHeldHands ??= new SessionValue<byte>(netHeldHands,0);
        public bool IsActive => GameSession.Offline || IsSpawned;
        public bool IsAuthority => GameSession.Offline || IsServer;
        public bool ControlledLocally => IsActive && (GameSession.Offline || IsOwner) && !IsBot.Value;
        public bool IsLobbyHost => !GameSession.Offline && !IsBot.Value && IsSpawned && OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;
        public string DisplayName => IsBot.Value ? GameSession.Names[Mathf.Clamp(Slot.Value,1,7)] : GameSession.Offline ? "YOU" : SteamId.Value!=0&&SteamSession.Instance?SteamSession.Instance.NameFor(SteamId.Value):"PLAYER "+(Slot.Value+1);
        public Rigidbody Body {get;private set;}
        public PlayerGrabController Grab {get;private set;}
        public AIPlayerController AI {get;private set;}
        public Vector3 AimDirection => Quaternion.Euler(aimPitch,aimYaw,0)*Vector3.forward;
        public bool Stunned => Time.time<stunnedUntil;
        public static readonly Color[] Colors={new(1,.42f,.27f),new(.27f,.66f,1),new(1,.76f,.22f),new(.24f,.81f,.62f),new(.67f,.46f,.93f),new(1,.46f,.67f),new(.27f,.83f,.89f),new(.9f,.58f,.26f)};
        Vector2 movement;float aimYaw,aimPitch,inputAt,nextInput,nextJump,stunnedUntil,stepAt,nextHitFeedback,migrationGripUntil;
        bool jump,grabInput,localJump;int painted=-1;
        Renderer[] visuals;
        public Transform leftBoot,rightBoot;
        public bool InputEnabled=true;
        public const float DriveSeconds=2.5f, InputPauseSeconds=.2f;
        public bool CanDrive => RoundManager.Instance && ((RoundManager.Instance.Clock + Slot.Value*.19) % (DriveSeconds+InputPauseSeconds)) < DriveSeconds;
        void Awake()
        {
            Body=GetComponent<Rigidbody>();Grab=GetComponent<PlayerGrabController>();
            AI=GetComponent<AIPlayerController>();visuals=GetComponentsInChildren<Renderer>();
        }
        public void InitializeOffline(int slot,bool bot)
        {
            Slot.Value=slot;IsBot.Value=bot;Ready.Value=true;Alive.Value=true;
            foreach(var sync in GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>())sync.enabled=false;
            SetAuthorityPhysics(true);name=DisplayName;
            AI?.Initialize();
        }
        public override void OnNetworkSpawn(){SetAuthorityPhysics(IsServer);inputAt=Time.time;AI?.Initialize();}
        void SetAuthorityPhysics(bool authority)
        {
            Body.isKinematic=!authority;
            foreach(var rb in GetComponentsInChildren<Rigidbody>())rb.isKinematic=!authority;
            Grab.ConfigureAuthority(authority);
        }
        void Update()
        {
            if(!IsActive)return;
            if(painted!=Slot.Value)
            {
                painted=Slot.Value;
                foreach(var r in visuals)if(r.name.StartsWith("Suit")||r.name.StartsWith("Sleeve")||r.name.StartsWith("Helmet")||r.name.StartsWith("Glove"))
                    r.material.color=Colors[painted%8];
            }
            if(!InputEnabled||!ControlledLocally||!Alive.Value||!RoundManager.Instance||RoundManager.Instance.Phase.Value==RoundPhase.Results)return;
            var key=Keyboard.current;var mouse=Mouse.current;if(key==null)return;
            bool captured=Cursor.lockState==CursorLockMode.Locked && !(GameSession.Instance&&GameSession.Instance.ShowTutorial);
            if(captured&&key.rKey.wasPressedThisFrame&&RoundManager.Instance.Phase.Value==RoundPhase.Lobby)ToggleReady();
            bool controls=captured&&CanDrive;
            var axis=controls?new Vector2((key.dKey.isPressed?1:0)-(key.aKey.isPressed?1:0),(key.wKey.isPressed?1:0)-(key.sKey.isPressed?1:0)):Vector2.zero;
            localJump|=controls&&key.spaceKey.wasPressedThisFrame;
            if(controls&&(key.fKey.wasPressedThisFrame||(mouse!=null&&mouse.rightButton.isPressed)))
            {if(GameSession.Offline)WeaponAction(key.fKey.wasPressedThisFrame,mouse!=null&&mouse.rightButton.isPressed);else WeaponServerRpc(key.fKey.wasPressedThisFrame,mouse!=null&&mouse.rightButton.isPressed);}
            if(Time.unscaledTime>=nextInput)
            {
                nextInput=Time.unscaledTime+1f/60;
                bool held=controls&&mouse!=null&&mouse.leftButton.isPressed;
                float yaw=CameraRig.Instance?CameraRig.Instance.Yaw:0;
                float pitch=CameraRig.Instance?CameraRig.Instance.Pitch:0;
                if(GameSession.Offline)SetMoveInput(axis,yaw,localJump,held,pitch);
                else InputServerRpc(axis,yaw,localJump,held,held,pitch);
                localJump=false;
            }
        }
        public void SetMoveInput(Vector2 axis,float yaw,bool pressJump,bool grab,float pitch=0)
        {
            if(!IsAuthority||!float.IsFinite(axis.x)||!float.IsFinite(axis.y)||!float.IsFinite(yaw)||!float.IsFinite(pitch))return;
            bool driving=CanDrive;
            movement=driving?Vector2.ClampMagnitude(axis,1):Vector2.zero;
            aimYaw=yaw%360;aimPitch=Mathf.Clamp(pitch,-80,80);
            if(driving){jump|=pressJump;grabInput=grab;}else jump=false;
            inputAt=Time.time;
        }
        [ServerRpc] void InputServerRpc(Vector2 axis,float yaw,bool pressJump,bool left,bool right,float pitch)=>SetMoveInput(axis,yaw,pressJump,left||right,pitch);
        [ServerRpc] void WeaponServerRpc(bool pickup,bool use)=>WeaponAction(pickup,use);
        public void WeaponAction(bool pickup,bool use){if(!IsAuthority||!Alive.Value||!CanDrive||RoundManager.Instance.Phase.Value!=RoundPhase.Playing)return;if(pickup)WeaponSystem.PickupOrThrow(this);if(use)WeaponSystem.Use(this);}
        public void ToggleReady(){if(GameSession.Offline)Ready.Value=!Ready.Value;else ReadyServerRpc();}
        [ServerRpc] public void ReadyServerRpc()
        {
            var round=RoundManager.Instance;
            if(!round||round.Phase.Value!=RoundPhase.Lobby||IsBot.Value)return;
            if(IsLobbyHost){if(round.CanStart)round.StartRound();}
            else Ready.Value=!Ready.Value;
        }
        void FixedUpdate()
        {
            if(!IsActive||!IsAuthority||(SteamSession.Instance&&SteamSession.Instance.Migrating))return;
            bool lobby=RoundManager.Instance&&RoundManager.Instance.Phase.Value==RoundPhase.Lobby;
            bool playing=RoundManager.Instance&&RoundManager.Instance.Phase.Value!=RoundPhase.Results;
            if(!Alive.Value||!playing){Grab.ReleaseAll();HeldHands.Value=0;return;}
            if(lobby&&(Body.position.y<-.5f||Body.position.y>5||Mathf.Abs(Body.position.x)>4.95f||Mathf.Abs(Body.position.z)>4.95f))
            {ResetForRound(RoundManager.SpawnPoint(Slot.Value));return;}
            if(IsBot.Value&&AI)AI.ApplyInput();
            else if(Time.time-inputAt>.3f){movement=Vector2.zero;grabInput=jump=false;}
            bool restoredHold=Time.time<migrationGripUntil;Grab.Tick(grabInput||(restoredHold&&Grab.LeftHeld),grabInput||(restoredHold&&Grab.RightHeld));
            bool grounded=UnityEngine.Physics.SphereCast(Body.position+Vector3.up*.15f,.23f,Vector3.down,out _,.95f,1<<8|1<<10,QueryTriggerInteraction.Ignore);
            float speed=3.5f/(1+Grab.HeldMass/240);
            var desired=Quaternion.Euler(0,aimYaw,0)*new Vector3(movement.x,0,movement.y)*speed;
            var horizontal=new Vector3(Body.linearVelocity.x,0,Body.linearVelocity.z);
            bool hanging=Grab.LeftHeld||Grab.RightHeld;
            if(!Stunned&&CanDrive)Body.AddForce(Vector3.ClampMagnitude((desired-horizontal)*(grounded?7:1.7f),grounded?22:8),ForceMode.Acceleration);
            if(jump&&grounded&&!Stunned&&CanDrive&&Time.time>=nextJump)
            {Body.AddForce(Vector3.up*5.2f,ForceMode.VelocityChange);nextJump=Time.time+.55f;PlaySound("jump");}
            jump=false;
            if(!Stunned)
            {
                // Keep the body upright during the brief input pause while preserving physical pulls.
                float spring=hanging?22:CanDrive?58:82;float damping=hanging?3.5f:CanDrive?7.5f:11;
                var wobble=Quaternion.Euler(Mathf.Sin(Time.time*7+Slot.Value)*movement.magnitude*5,0,-movement.x*6)*Vector3.up;
                Body.AddTorque(Vector3.Cross(transform.up,wobble)*spring-Body.angularVelocity*damping,ForceMode.Acceleration);
                Body.AddTorque(Vector3.up*Mathf.Clamp(Mathf.DeltaAngle(transform.eulerAngles.y,aimYaw)*.1f,-7,7),ForceMode.Acceleration);
            }
            Body.linearVelocity=Vector3.ClampMagnitude(Body.linearVelocity,24);
            if(grounded&&horizontal.magnitude>1&&Time.time>stepAt){stepAt=Time.time+.34f;PlaySound("step",.25f);}
        }
        void LateUpdate()
        {
            if(!IsActive)return;
            float speed=new Vector2(Body.linearVelocity.x,Body.linearVelocity.z).magnitude;
            if(!IsAuthority)speed=movement.magnitude*3;
            if(leftBoot)leftBoot.localRotation=Quaternion.Euler(Mathf.Sin(Time.time*9)*Mathf.Min(speed*9,25),0,0);
            if(rightBoot)rightBoot.localRotation=Quaternion.Euler(-Mathf.Sin(Time.time*9)*Mathf.Min(speed*9,25),0,0);
        }
        public void ResetForRound(Vector3 position)
        {
            Grab.ReleaseAll();Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;
            transform.SetPositionAndRotation(position,Quaternion.identity);Body.position=position;Body.rotation=Quaternion.identity;aimYaw=aimPitch=0;
            if(!GameSession.Offline)GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(position,Quaternion.identity,Vector3.one);
            Grab.ResetHands();Grab.ResetGrip();WeaponSystem.Drop(this,false);
            Alive.Value=true;HeldHands.Value=0;movement=Vector2.zero;grabInput=jump=false;stunnedUntil=0;inputAt=Time.time;
            AI?.ResetBrain();
        }
        public float StunRemaining=>Mathf.Max(0,stunnedUntil-Time.time);
        public void RestoreControl(Vector3 aim,float stun){aimYaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;aimPitch=-Mathf.Asin(Mathf.Clamp(aim.y,-1,1))*Mathf.Rad2Deg;stunnedUntil=Time.time+stun;migrationGripUntil=Time.time+1;}
        public void Knock(Vector3 velocity,float stun=1.4f)
        {
            if(!IsAuthority||!Alive.Value)return;
            Grab.ApplyImpact(velocity.magnitude*Body.mass);
            if(Time.time>=nextHitFeedback&&velocity.magnitude>2&&RoundManager.Instance){nextHitFeedback=Time.time+.12f;RoundManager.Instance.WeaponEffect(Body.position+Vector3.up*.25f,velocity,100+Slot.Value);}
            Body.AddForce(velocity,ForceMode.VelocityChange);stunnedUntil=Time.time+stun;
            Body.AddTorque(UnityEngine.Random.insideUnitSphere*2,ForceMode.VelocityChange);
        }
        public void PlaySound(string cue,float volume=1)
        {
            if(GameSession.Offline)AudioManager.Instance?.PlayAt(cue,transform.position,volume);
            else if(IsServer)SoundClientRpc(cue,volume);
        }
        [ClientRpc] void SoundClientRpc(string cue,float volume)=>AudioManager.Instance?.PlayAt(cue,transform.position,volume);
        void OnCollisionEnter(Collision c)
        {
            if(IsAuthority)Grab.ApplyImpact(c.impulse.magnitude);
            if(IsAuthority&&c.relativeVelocity.magnitude>5){stunnedUntil=Time.time+1.25f;PlaySound("fall",.6f);}
            else if(c.relativeVelocity.magnitude>2.5f)AudioManager.Instance?.PlayAt("bump",transform.position,.3f);
        }
    }
}











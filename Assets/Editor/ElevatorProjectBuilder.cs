using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

namespace ElevatorGame.Editor
{
    public static class ElevatorProjectBuilder
    {
        const string Root="Assets/Elevator";
        static Material cream, teal, dark, mint, gold, coral, purple, white;
        static PhysicsMaterial rubber;
        [MenuItem("Elevator/Build Playable Project")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before rebuilding.");
            foreach(string folder in new[]{"Materials","Prefabs/Players","Prefabs/Props","Prefabs/Events","Prefabs/Elevator","Scenes","ScriptableObjects","Audio","Animations"})
                Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh();
            SetLayers();
            InitializePalette();
            var player=BuildPlayer();
            var props=new NetworkProp[16];
            for(int i=0;i<props.Length;i++)props[i]=BuildProp(i);
            var definitions=BuildEvents();
            var previous=EditorSceneManager.GetActiveScene();
            if(previous.isDirty)EditorSceneManager.SaveScene(previous);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.53f,.65f,.7f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.06f,.13f,.19f);RenderSettings.fogDensity=.012f;
            var cabin=BuildCabin();SurfaceOverlapFix.FixCabin(cabin);BuildCorridor();CabinResize.ApplyToScene(cabin);
            PrefabUtility.SaveAsPrefabAsset(cabin,Root+"/Prefabs/Elevator/Cabin.prefab");
            var doors=cabin.GetComponent<ElevatorDoorController>();
            var systems=new GameObject("Elevator Game");
            systems.AddComponent<NetworkObject>();
            var round=systems.AddComponent<RoundManager>();
            round.doors=doors;round.events=systems.AddComponent<FloorEventManager>();
            round.events.definitions=definitions;round.events.propPrefabs=props;
            round.elimination=systems.AddComponent<PlayerEliminationController>();
            round.floorDisplay=cabin.GetComponentsInChildren<TextMesh>().First(t=>t.name=="Floor Display");
            systems.AddComponent<GameSession>();systems.AddComponent<AudioManager>();systems.AddComponent<UIManager>();
            var net=new GameObject("Network Session");
            var manager=net.AddComponent<NetworkManager>();
            var transport=net.AddComponent<UnityTransport>();
            manager.NetworkConfig=new NetworkConfig {NetworkTransport=transport,TickRate=60,ConnectionApproval=true,EnableSceneManagement=true};
            var session=net.AddComponent<NetworkGameManager>();session.playerPrefab=player;
            session.networkPrefabs=props.Select(p=>p.GetComponent<NetworkObject>()).Concat(new[]{player.GetComponent<NetworkObject>()}).ToArray();
            var camera=new GameObject("Main Camera");camera.tag="MainCamera";camera.transform.position=new Vector3(7.2f,4.2f,14);
            camera.transform.LookAt(new Vector3(0,1.8f,1));
            var cam=camera.AddComponent<Camera>();cam.fieldOfView=73;cam.nearClipPlane=.07f;cam.farClipPlane=130;
            cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.06f,.13f,.19f);
            camera.AddComponent<AudioListener>();camera.AddComponent<CameraRig>();
            var sun=new GameObject("Softbox Key");sun.transform.rotation=Quaternion.Euler(42,-32,0);
            var light=sun.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.35f;light.color=new Color(1,.93f,.78f);light.shadows=LightShadows.Soft;
            var fill=new GameObject("Cabin Fill");fill.transform.position=new Vector3(0,3.7f,0);
            var lamp=fill.AddComponent<Light>();lamp.type=LightType.Point;lamp.range=12;lamp.intensity=3;lamp.color=new Color(.7f,.9f,1);
            EditorSceneManager.SaveScene(scene,Root+"/Scenes/Elevator.unity");
            var existing=EditorBuildSettings.scenes.Where(s=>s.path!=Root+"/Scenes/Elevator.unity").ToArray();
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/Elevator.unity",true)}.Concat(existing).ToArray();
            UnityEngine.Physics.defaultSolverIterations=12;UnityEngine.Physics.defaultSolverVelocityIterations=4;
            Time.fixedDeltaTime=1f/60;
            var dynamics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
            var iterations=dynamics.FindProperty("m_DefaultSolverIterations");if(iterations!=null)iterations.intValue=12;
            var velocities=dynamics.FindProperty("m_DefaultSolverVelocityIterations");if(velocities!=null)velocities.intValue=4;dynamics.ApplyModifiedPropertiesWithoutUndo();
            var timing=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            var fixedTime=timing.FindProperty("Fixed Timestep");if(fixedTime!=null && fixedTime.propertyType==SerializedPropertyType.Generic){fixedTime.FindPropertyRelative("m_Count").longValue=1;fixedTime.FindPropertyRelative("m_Rate.m_Numerator").longValue=60;fixedTime.FindPropertyRelative("m_Rate.m_Denominator").longValue=1;}timing.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.runInBackground=true;
            SetupPostProcessing();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("ELEVATOR_BUILD_OK: scene, props, events and network references generated.");
        }
        static void InitializePalette()
        {
            cream=Mat("Porcelain",new Color(.86f,.88f,.8f));
            teal=Mat("DeepTeal",new Color(.035f,.21f,.24f));
            dark=Mat("Ink",new Color(.035f,.065f,.10f));
            mint=Mat("Mint",new Color(.56f,.96f,.72f));
            gold=Mat("Amber",new Color(1,.69f,.18f));
            coral=Mat("Coral",new Color(1,.32f,.25f));
            purple=Mat("Violet",new Color(.38f,.24f,.68f));
            white=Mat("White",new Color(.98f,.99f,1));
            rubber=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Root+"/Materials/Bouncy.asset");
            if(!rubber){rubber=new PhysicsMaterial("Bouncy"){bounciness=.9f,dynamicFriction=.18f,staticFriction=.2f,bounceCombine=PhysicsMaterialCombine.Maximum};AssetDatabase.CreateAsset(rubber,Root+"/Materials/Bouncy.asset");}
        }
        static void SetLayers()
        {
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tags.FindProperty("layers");
            layers.GetArrayElementAtIndex(8).stringValue="Cabin";
            layers.GetArrayElementAtIndex(9).stringValue="Players";
            layers.GetArrayElementAtIndex(10).stringValue="Props";
            tags.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Physics.IgnoreLayerCollision(9,9,false);UnityEngine.Physics.IgnoreLayerCollision(9,10,false);
            UnityEngine.Physics.IgnoreLayerCollision(8,9,false);UnityEngine.Physics.IgnoreLayerCollision(8,10,false);
        }
        static Material Mat(string name,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Smoothness",.28f);EditorUtility.SetDirty(m);return m;
        }
        static GameObject Shape(string name,PrimitiveType shape,Transform parent,Vector3 position,Vector3 scale,Material material,bool collider=true,int layer=8)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.layer=layer;
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static GameObject Cube(string n,Transform p,Vector3 pos,Vector3 size,Material m,bool collider=true,int layer=8)=>Shape(n,PrimitiveType.Cube,p,pos,size,m,collider,layer);
        static Rigidbody Rigid(GameObject go,float mass,bool kinematic=false)
        {
            var rb=go.AddComponent<Rigidbody>();rb.mass=mass;rb.isKinematic=kinematic;
            rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=kinematic?CollisionDetectionMode.ContinuousSpeculative:CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping=.3f;rb.angularDamping=2;rb.maxAngularVelocity=12;
            rb.solverIterations=12;rb.solverVelocityIterations=4;return rb;
        }
        static void Network(GameObject go)
        {
            go.AddComponent<NetworkObject>();
            var sync=go.AddComponent<NetworkTransform>();sync.Interpolate=true;
            sync.SyncScaleX=false;sync.SyncScaleY=false;sync.SyncScaleZ=false;
        }
        static PlayerController BuildPlayer()
        {
            var root=new GameObject("Passenger");root.layer=9;
            var collider=root.AddComponent<CapsuleCollider>();collider.radius=.31f;collider.height=1.65f;
            var body=Rigid(root,55);body.centerOfMass=new Vector3(0,-.15f,0);body.angularDamping=.5f;
            Shape("Suit Torso",PrimitiveType.Capsule,root.transform,new Vector3(0,-.03f,0),new Vector3(.66f,.43f,.48f),coral,false,9);
            Cube("Vest panel",root.transform,new Vector3(0,.05f,.24f),new Vector3(.35f,.38f,.035f),cream,false,9);
            Cube("Zipper",root.transform,new Vector3(0,.04f,.266f),new Vector3(.025f,.35f,.02f),gold,false,9);
            Shape("Helmet",PrimitiveType.Sphere,root.transform,new Vector3(0,.58f,0),new Vector3(.72f,.65f,.66f),coral,false,9);
            Shape("Visor",PrimitiveType.Sphere,root.transform,new Vector3(0,.58f,.295f),new Vector3(.56f,.38f,.18f),dark,false,9);
            for(int i=-1;i<=1;i+=2)
            {
                Shape("Eye",PrimitiveType.Sphere,root.transform,new Vector3(i*.13f,.61f,.392f),new Vector3(.105f,.13f,.03f),white,false,9);
                Shape("Eye glint",PrimitiveType.Sphere,root.transform,new Vector3(i*.13f-.018f,.645f,.408f),Vector3.one*.025f,mint,false,9);
                Shape("Helmet ear",PrimitiveType.Cylinder,root.transform,new Vector3(i*.34f,.57f,0),new Vector3(.2f,.055f,.2f),gold,false,9).transform.localRotation=Quaternion.Euler(0,0,90);
            }
            var leftBoot=Shape("Boot left",PrimitiveType.Capsule,root.transform,new Vector3(-.19f,-.56f,.065f),new Vector3(.27f,.25f,.35f),dark,false,9);
            var rightBoot=Shape("Boot right",PrimitiveType.Capsule,root.transform,new Vector3(.19f,-.56f,.065f),new Vector3(.27f,.25f,.35f),dark,false,9);
            Network(root);
            var hands=new Transform[2];
            for(int i=0;i<2;i++)
            {
                var hand=Shape("Glove "+i,PrimitiveType.Sphere,root.transform,new Vector3(i==0?-.32f:.32f,.125f,0),Vector3.one*.24f,cream,true,9);
                var rb=Rigid(hand,3.5f);rb.linearDamping=.2f;rb.angularDamping=.3f;
                var arm=hand.AddComponent<ConfigurableJoint>();arm.connectedBody=body;arm.autoConfigureConnectedAnchor=false;arm.anchor=Vector3.zero;
                arm.connectedAnchor=new Vector3(i==0?-.32f:.32f,.3f,0);
                arm.xMotion=arm.yMotion=arm.zMotion=ConfigurableJointMotion.Limited;
                arm.linearLimit=new SoftJointLimit{limit=PlayerGrabController.ArmLength};arm.linearLimitSpring=new SoftJointLimitSpring{spring=1600,damper=65};
                arm.angularXMotion=arm.angularYMotion=arm.angularZMotion=ConfigurableJointMotion.Free;
                arm.enableCollision=false;arm.projectionMode=JointProjectionMode.PositionAndRotation;arm.projectionDistance=.15f;
                var sync=hand.AddComponent<NetworkTransform>();sync.Interpolate=true;sync.SyncScaleX=sync.SyncScaleY=sync.SyncScaleZ=false;
                hands[i]=hand.transform;
            }
            var grab=root.AddComponent<PlayerGrabController>();grab.leftHand=hands[0];grab.rightHand=hands[1];
            grab.leftUpperArm=Shape("Sleeve left upper",PrimitiveType.Capsule,root.transform,Vector3.zero,Vector3.one,coral,false,9).transform;
            grab.rightUpperArm=Shape("Sleeve right upper",PrimitiveType.Capsule,root.transform,Vector3.zero,Vector3.one,coral,false,9).transform;
            grab.leftForearm=Shape("Sleeve left lower",PrimitiveType.Capsule,root.transform,Vector3.zero,Vector3.one,coral,false,9).transform;
            grab.rightForearm=Shape("Sleeve right lower",PrimitiveType.Capsule,root.transform,Vector3.zero,Vector3.one,coral,false,9).transform;
            var actor=root.AddComponent<PlayerController>();actor.leftBoot=leftBoot.transform;actor.rightBoot=rightBoot.transform;
            root.AddComponent<AIPlayerController>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Players/Passenger.prefab");
            UnityEngine.Object.DestroyImmediate(root);return prefab.GetComponent<PlayerController>();
        }
        static NetworkProp BuildProp(int kind)
        {
            string[] names={"BowlingBall","ShoppingCart","Dinosaur","Gorilla","GiantChicken","GiantHand","FloodSurface","Crate","LatePassenger","Weapon","Projectile","FlyingFish","Tank","TankShell","Ufo","GrandPiano"};
            var root=new GameObject(names[kind]);root.layer=10;var t=root.transform;
            Transform tail=null,head=null,jaw=null,leftLeg=null,rightLeg=null,fishLeftFin=null,fishRightFin=null,saucerRing=null;
            Renderer beamRenderer=null;
            switch(kind)
            {
                case 0:
                    var ball=Shape("Ball",PrimitiveType.Sphere,t,Vector3.zero,Vector3.one*1.75f,purple,true,10);ball.GetComponent<Collider>().sharedMaterial=rubber;
                    for(int i=0;i<3;i++)Shape("Finger hole",PrimitiveType.Sphere,t,new Vector3((i-1)*.22f,.68f,.47f),Vector3.one*.17f,dark,false,10);
                    Shape("Ball band",PrimitiveType.Cylinder,t,Vector3.zero,new Vector3(1.765f,.04f,1.765f),gold,false,10);break;
                case 1:
                    Cube("Chassis",t,Vector3.zero,new Vector3(1.2f,.13f,1.4f),teal,true,10);
                    for(int i=-1;i<=1;i+=2)
                    {
                        for(int j=0;j<6;j++)Cube("Basket slat",t,new Vector3(i*.55f,.45f,-.6f+j*.24f),new Vector3(.065f,.65f,.055f),cream,false,10);
                        Cube("Basket rim",t,new Vector3(i*.55f,.79f,0),new Vector3(.09f,.1f,1.4f),gold,true,10);
                        Cube("Basket bumper",t,new Vector3(i*.55f,.12f,0),new Vector3(.08f,.14f,1.4f),teal,true,10);
                        for(int j=-1;j<=1;j+=2)
                        {
                            var wheel=Shape("Wheel",PrimitiveType.Cylinder,t,new Vector3(i*.55f,-.19f,j*.48f),new Vector3(.31f,.06f,.31f),dark,false,10);wheel.transform.localRotation=Quaternion.Euler(0,0,90);
                        }
                    }
                    Cube("Handle",t,new Vector3(0,.91f,.77f),new Vector3(1.32f,.1f,.12f),coral,true,10);
                    Cube("Groceries",t,new Vector3(0,.29f,-.1f),new Vector3(.8f,.4f,.65f),gold,true,10);break;
                case 2:
                    Shape("Body",PrimitiveType.Capsule,t,Vector3.zero,new Vector3(1.55f,.78f,1.8f),mint,true,10);
                    Shape("Belly",PrimitiveType.Sphere,t,new Vector3(0,-.2f,-.4f),new Vector3(1.19f,1.2f,1.25f),cream,false,10);
                    head=new GameObject("Head pivot").transform;head.SetParent(t,false);head.localPosition=new Vector3(0,.65f,-.68f);
                    Shape("Skull",PrimitiveType.Sphere,head,Vector3.zero,new Vector3(1.55f,1.12f,1.6f),mint,true,10);
                    Cube("Snout",head,new Vector3(0,-.08f,-.69f),new Vector3(1.1f,.49f,1.0f),mint,true,10);
                    jaw=new GameObject("Jaw pivot").transform;jaw.SetParent(head,false);jaw.localPosition=new Vector3(0,-.3f,-.2f);
                    Cube("Lower jaw",jaw,new Vector3(0,-.03f,-.55f),new Vector3(1.03f,.17f,1.06f),teal,false,10);
                    for(int i=-1;i<=1;i+=2)
                    {
                        Shape("Eye",PrimitiveType.Sphere,head,new Vector3(i*.62f,.27f,-.39f),Vector3.one*.35f,white,false,10);
                        Shape("Pupil",PrimitiveType.Sphere,head,new Vector3(i*.63f,.28f,-.55f),Vector3.one*.17f,dark,false,10);
                        Shape("Nostril",PrimitiveType.Sphere,head,new Vector3(i*.3f,.07f,-1.185f),Vector3.one*.1f,teal,false,10);
                        for(int n=0;n<3;n++)Cube("Tooth",head,new Vector3(i*(.15f+n*.14f),-.31f,-1.08f),new Vector3(.09f,.16f,.09f),white,false,10);
                        Shape("Tiny arm",PrimitiveType.Capsule,t,new Vector3(i*.8f,.13f,-.54f),new Vector3(.25f,.27f,.27f),mint,false,10).transform.localRotation=Quaternion.Euler(-35,0,i*30);
                    }
                    leftLeg=Shape("Left foot",PrimitiveType.Capsule,t,new Vector3(-.48f,-.94f,.12f),new Vector3(.55f,.47f,.64f),teal,true,10).transform;
                    rightLeg=Shape("Right foot",PrimitiveType.Capsule,t,new Vector3(.48f,-.94f,.12f),new Vector3(.55f,.47f,.64f),teal,true,10).transform;
                    tail=new GameObject("Tail pivot").transform;tail.SetParent(t,false);tail.localPosition=new Vector3(0,-.1f,.8f);
                    for(int n=0;n<4;n++)Shape("Tail segment",PrimitiveType.Sphere,tail,new Vector3(0,.05f-n*.07f,n*.48f),new Vector3(.9f-n*.2f,.8f-n*.16f,.8f),mint,true,10);
                    for(int n=0;n<4;n++)Shape("Back ridge",PrimitiveType.Cube,t,new Vector3(0,.72f,-.15f+n*.44f),new Vector3(.22f,.32f,.27f),gold,false,10).transform.localRotation=Quaternion.Euler(0,0,45);
                    break;
                case 3:
                    Shape("Body",PrimitiveType.Capsule,t,Vector3.zero,new Vector3(1.25f,.7f,1),purple,true,10);
                    Shape("Chest",PrimitiveType.Sphere,t,new Vector3(0,.15f,-.42f),new Vector3(.85f,.9f,.25f),cream,false,10);
                    Shape("Head",PrimitiveType.Sphere,t,new Vector3(0,.83f,-.12f),Vector3.one*.86f,purple,true,10);
                    Shape("Muzzle",PrimitiveType.Sphere,t,new Vector3(0,.75f,-.5f),new Vector3(.59f,.36f,.24f),cream,false,10);
                    for(int i=-1;i<=1;i+=2)
                    {
                        Shape("Eye",PrimitiveType.Sphere,t,new Vector3(i*.18f,.97f,-.49f),Vector3.one*.1f,dark,false,10);
                        Shape("Arm",PrimitiveType.Capsule,t,new Vector3(i*.83f,-.15f,0),new Vector3(.49f,.69f,.5f),purple,true,10);
                    }
                    break;
                case 4:
                    Shape("Body",PrimitiveType.Sphere,t,Vector3.zero,new Vector3(1.3f,1.4f,1.2f),white,true,10);
                    Shape("Head",PrimitiveType.Sphere,t,new Vector3(0,.8f,-.3f),Vector3.one*.65f,white,false,10);
                    Cube("Beak",t,new Vector3(0,.8f,-.7f),new Vector3(.35f,.2f,.4f),gold,false,10);
                    for(int n=0;n<3;n++)Shape("Comb",PrimitiveType.Sphere,t,new Vector3(0,1.12f,-.45f+n*.2f),new Vector3(.2f,.33f,.22f),coral,false,10);
                    for(int i=-1;i<=1;i+=2)
                    {Shape("Wing",PrimitiveType.Sphere,t,new Vector3(i*.65f,.1f,0),new Vector3(.3f,.65f,.8f),cream,false,10);Shape("Eye",PrimitiveType.Sphere,t,new Vector3(i*.26f,.9f,-.49f),Vector3.one*.12f,dark,false,10);}
                    break;
                case 5:
                    Shape("Palm",PrimitiveType.Sphere,t,Vector3.zero,new Vector3(1.4f,.6f,1.45f),coral,true,10);
                    for(int i=0;i<4;i++)Shape("Finger",PrimitiveType.Capsule,t,new Vector3((i-1.5f)*.34f,0,-.9f),new Vector3(.3f,.65f,.3f),coral,true,10).transform.localRotation=Quaternion.Euler(90,0,0);
                    Shape("Thumb",PrimitiveType.Capsule,t,new Vector3(-.8f,0,-.25f),new Vector3(.35f,.47f,.35f),coral,true,10).transform.localRotation=Quaternion.Euler(0,0,-65);break;
                case 6:Cube("Water",t,Vector3.zero,new Vector3(9.45f,.12f,9.45f),Mat("Water",new Color(.1f,.65f,.9f)),false,10);break;
                case 7:
                    Cube("Box",t,Vector3.zero,Vector3.one*.8f,gold,true,10);Cube("Tape",t,new Vector3(0,.405f,0),new Vector3(.15f,.02f,.8f),cream,false,10);break;
                case 8:
                    Shape("Suit",PrimitiveType.Capsule,t,Vector3.zero,new Vector3(.8f,.57f,.65f),teal,true,10);
                    Shape("Face",PrimitiveType.Sphere,t,new Vector3(0,.85f,0),Vector3.one*.63f,cream,false,10);
                    Cube("Tie",t,new Vector3(0,.2f,-.33f),new Vector3(.16f,.5f,.04f),coral,false,10);
                    Shape("Hat",PrimitiveType.Cylinder,t,new Vector3(0,1.15f,0),new Vector3(.69f,.09f,.69f),gold,false,10);
                    for(int i=-1;i<=1;i+=2)
                    {
                        Shape("Eye",PrimitiveType.Sphere,t,new Vector3(i*.14f,.88f,-.29f),Vector3.one*.09f,dark,false,10);
                        Shape("Arm",PrimitiveType.Capsule,t,new Vector3(i*.55f,.13f,0),new Vector3(.25f,.45f,.3f),teal,true,10);
                    }
                    leftLeg=Shape("Leg left",PrimitiveType.Capsule,t,new Vector3(-.24f,-.69f,0),new Vector3(.3f,.37f,.35f),dark,true,10).transform;
                    rightLeg=Shape("Leg right",PrimitiveType.Capsule,t,new Vector3(.24f,-.69f,0),new Vector3(.3f,.37f,.35f),dark,true,10).transform;
                    Cube("Briefcase",t,new Vector3(.65f,-.32f,0),new Vector3(.3f,.5f,.75f),gold,true,10);break;
                case 11:
                    Shape("Fish body",PrimitiveType.Sphere,t,Vector3.zero,new Vector3(.58f,.36f,.92f),Mat("FishAzure",new Color(.08f,.63f,.91f)),true,10);
                    Shape("Fish belly",PrimitiveType.Sphere,t,new Vector3(0,-.13f,-.06f),new Vector3(.47f,.2f,.64f),cream,false,10);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Shape("Fish eye",PrimitiveType.Sphere,t,new Vector3(side*.27f,.12f,-.27f),Vector3.one*.13f,white,false,10);
                        Shape("Fish pupil",PrimitiveType.Sphere,t,new Vector3(side*.34f,.12f,-.31f),Vector3.one*.06f,dark,false,10);
                    }
                    tail=new GameObject("Flapping tail").transform;tail.SetParent(t,false);tail.localPosition=new Vector3(0,0,.42f);
                    Shape("Tail fin",PrimitiveType.Sphere,tail,new Vector3(0,0,.38f),new Vector3(.15f,.52f,.48f),coral,false,10);
                    fishLeftFin=Shape("Left fin",PrimitiveType.Sphere,t,new Vector3(-.43f,.03f,.15f),new Vector3(.18f,.1f,.38f),gold,false,10).transform;
                    fishRightFin=Shape("Right fin",PrimitiveType.Sphere,t,new Vector3(.43f,.03f,.15f),new Vector3(.18f,.1f,.38f),gold,false,10).transform;
                    break;
                case 12:
                    Cube("Armored chassis",t,Vector3.zero,new Vector3(2.5f,.72f,2.75f),Mat("TankOlive",new Color(.25f,.33f,.18f)),true,10);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Cube("Tank track",t,new Vector3(side*1.19f,-.4f,0),new Vector3(.55f,.65f,3.04f),dark,true,10);
                        for(int i=-2;i<=2;i++)Shape("Track wheel",PrimitiveType.Cylinder,t,new Vector3(side*1.51f,-.52f,i*.55f),new Vector3(.33f,.045f,.33f),gold,false,10).transform.localRotation=Quaternion.Euler(0,0,90);
                    }
                    Shape("Turret",PrimitiveType.Cylinder,t,new Vector3(0,.63f,-.2f),new Vector3(.82f,.3f,.82f),teal,false,10);
                    Shape("Cannon",PrimitiveType.Cylinder,t,new Vector3(0,.77f,-1.52f),new Vector3(.23f,1.01f,.23f),dark,false,10).transform.localRotation=Quaternion.Euler(90,0,0);
                    Shape("Muzzle",PrimitiveType.Cylinder,t,new Vector3(0,.77f,-2.52f),new Vector3(.31f,.12f,.31f),gold,false,10).transform.localRotation=Quaternion.Euler(90,0,0);
                    break;
                case 13:
                    Shape("Explosive shell",PrimitiveType.Sphere,t,Vector3.zero,Vector3.one*.36f,coral,true,10);
                    Shape("Shell tip",PrimitiveType.Sphere,t,new Vector3(0,0,-.22f),new Vector3(.21f,.21f,.25f),gold,false,10);
                    root.AddComponent<WeaponProjectile>();break;
                case 14:
                    Shape("Saucer hull",PrimitiveType.Sphere,t,Vector3.zero,new Vector3(2.3f,.46f,2.3f),Mat("UfoSilver",new Color(.57f,.75f,.84f)),true,10);
                    Shape("Glass dome",PrimitiveType.Sphere,t,new Vector3(0,.36f,0),new Vector3(1.05f,.73f,1.05f),Mat("UfoGlass",new Color(.24f,.92f,.88f)),false,10);
                    saucerRing=new GameObject("Rotating lights").transform;saucerRing.SetParent(t,false);
                    for(int i=0;i<8;i++){float angle=i*Mathf.PI/4;Shape("Saucer light",PrimitiveType.Sphere,saucerRing,new Vector3(Mathf.Cos(angle)*1.04f,-.11f,Mathf.Sin(angle)*1.04f),Vector3.one*.19f,i%2==0?gold:coral,false,10);}
                    Shape("Tractor emitter",PrimitiveType.Cylinder,t,new Vector3(0,-.34f,0),new Vector3(.55f,.1f,.55f),mint,false,10);
                    var beamMaterial=Mat("TractorBeam",new Color(.18f,.95f,.86f,.22f));
                    beamMaterial.SetFloat("_Surface",1);beamMaterial.SetFloat("_Blend",0);
                    beamMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);beamMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                    beamMaterial.SetInt("_ZWrite",0);beamMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    beamMaterial.SetOverrideTag("RenderType","Transparent");beamMaterial.renderQueue=3000;EditorUtility.SetDirty(beamMaterial);
                    beamRenderer=Shape("Visible tractor beam",PrimitiveType.Cylinder,t,new Vector3(0,-1.15f,0),new Vector3(.77f,1.1f,.77f),beamMaterial,false,10).GetComponent<Renderer>();
                    break;
                case 15:
                    Cube("Piano case",t,Vector3.zero,new Vector3(2.16f,1.04f,1.3f),dark,true,10);
                    Cube("Piano lid",t,new Vector3(0,.58f,.25f),new Vector3(2.25f,.09f,1.7f),dark,false,10);
                    Cube("Keyboard",t,new Vector3(0,.08f,-.75f),new Vector3(2.04f,.15f,.56f),white,false,10);
                    for(int i=0;i<11;i++)Cube("Black key",t,new Vector3((i-5)*.17f,.17f,-.76f),new Vector3(.09f,.09f,.3f),dark,false,10);
                    for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)
                        Cube("Piano leg",t,new Vector3(side*.84f,-.78f,end*.4f),new Vector3(.18f,.65f,.18f),gold,true,10);
                    break;
            }
            if(kind==9){var collider=root.AddComponent<BoxCollider>();collider.size=new Vector3(.28f,.3f,.65f);root.AddComponent<WeaponPickup>().visualMaterial=gold;}
            if(kind==10){Shape("Projectile shell",PrimitiveType.Sphere,t,Vector3.zero,Vector3.one*.25f,coral,true,10);root.AddComponent<WeaponProjectile>();}
            var rb=Rigid(root,kind==15?175:kind==12?240:kind==11?13:kind>=9?8:kind==0?180:kind==1?45:kind==3?100:kind==4?50:kind==8?85:70);rb.angularDamping=kind==0?.08f:2;
            Network(root);var prop=root.AddComponent<NetworkProp>();prop.impactBoost=kind==15?1.5f:kind==13?1.1f:kind==12?1.1f:kind==11?.75f:kind==0?1.2f:kind==1?1:kind==9?.8f:0;
            if(kind==11){var animator=root.AddComponent<FishAnimator>();animator.tail=tail;animator.leftFin=fishLeftFin;animator.rightFin=fishRightFin;}
            if(kind==14){var animator=root.AddComponent<SaucerAnimator>();animator.lightRing=saucerRing;animator.beam=beamRenderer;}
            if(kind==2||kind==3||kind==4||kind==8)
            {var animator=root.AddComponent<PropAnimator>();animator.tail=tail;animator.head=head;animator.jaw=jaw;animator.leftLeg=leftLeg;animator.rightLeg=rightLeg;animator.stabilize=kind!=4;}
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Props/"+names[kind]+".prefab");
            UnityEngine.Object.DestroyImmediate(root);return prefab.GetComponent<NetworkProp>();
        }
        static FloorEventDefinition[] BuildEvents()
        {
            Type[] types={typeof(WindEvent),typeof(BowlingBallEvent),typeof(DinosaurEvent),typeof(ShoppingCartEvent),typeof(GorillaEvent),typeof(ChickenEvent),typeof(GiantHandEvent),typeof(FloodEvent),typeof(VacuumEvent),typeof(EarthquakeEvent),typeof(EmptyFloorEvent),typeof(FakeEmptyEvent),typeof(StrangerEvent),typeof(FlyingFishEvent),typeof(TankEvent),typeof(UfoEvent),typeof(GrandPianoEvent)};
            string[] titles={"HOLD ON TIGHT","STRIKE!","JURASSIC SERVICE","CART TRAFFIC","UNINVITED GUEST","POULTRY IN MOTION","HELPING HAND?","HIGH WATER","NO ATMOSPHERE","SHAKEN, NOT STIRRED","NOTHING TO SEE","NOTHING TO SEE","RUNNING LATE","FISH OUT OF WATER","ARMORED ROOM SERVICE","UNIDENTIFIED FLOOR OBJECT","GRAND FINALE"};
            string[] hints={"Grab the rail. Gusts come in waves.","Watch the rebound.","Keep clear of the head. Save anyone bitten.","Carts can stay between floors.","Grab your friend before the throw.","Do not trust the chicken.","Break its grip by holding the cabin.","Grab high and keep your feet down.","Everything is being pulled outside.","Hold the rails.","Enjoy the silence.","Enjoy the silence.","Make room for the late passenger.","Ten fish flop, then leap into the lift.","Dodge the cannon and its blast.","Do not stand under the tractor beam.","Mind the falling piano."};
            var definitions=new FloorEventDefinition[types.Length];
            for(int i=0;i<types.Length;i++)
            {
                definitions[i]=CreateEventDefinition(i,types[i],titles[i],hints[i]);
            }
            return definitions;
        }
        static FloorEventDefinition CreateEventDefinition(int i,Type type,string title,string hint)
        {
            var go=new GameObject(type.Name);go.AddComponent(type);
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/Events/"+type.Name+".prefab");UnityEngine.Object.DestroyImmediate(go);
            string path=Root+"/ScriptableObjects/"+type.Name+".asset";
            var d=AssetDatabase.LoadAssetAtPath<FloorEventDefinition>(path);
            if(!d){d=ScriptableObject.CreateInstance<FloorEventDefinition>();AssetDatabase.CreateAsset(d,path);}
            d.kind=(EventKind)i;d.eventName=title;d.hint=hint;
            d.weight=i==10?.65f:i==14?.72f:i==15?.75f:i==16?.82f:1;
            d.duration=i==10?5:i==2?22:14;
            d.minimumFloor=i==14?6:i==15?8:i==16?5:i==13?3:(i==4||i==6||i==8)?4:1;
            d.maximumFloor=999;d.difficulty=1;d.canCombine=i!=10&&i!=11;d.persistent=i==3;
            d.prefab=prefab.GetComponent<FloorEvent>();EditorUtility.SetDirty(d);return d;
        }
        [MenuItem("Elevator/Install New Floor Events")]
        public static void InstallNewFloorEvents()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before adding events.");
            const string scenePath=Root+"/Scenes/Elevator.unity";
            var current=EditorSceneManager.GetActiveScene();
            if(current.isDirty)EditorSceneManager.SaveScene(current);
            var scene=current.path==scenePath?current:EditorSceneManager.OpenScene(scenePath);
            InitializePalette();
            var round=UnityEngine.Object.FindFirstObjectByType<RoundManager>();
            var session=UnityEngine.Object.FindFirstObjectByType<NetworkGameManager>();
            if(!round||!session||round.events.propPrefabs.Length<11)throw new InvalidOperationException("Playable Elevator scene is missing its event or network references.");
            var props=new NetworkProp[16];Array.Copy(round.events.propPrefabs,props,11);
            for(int i=11;i<props.Length;i++)props[i]=BuildProp(i);
            var definitions=new FloorEventDefinition[17];Array.Copy(round.events.definitions,definitions,13);
            definitions[13]=CreateEventDefinition(13,typeof(FlyingFishEvent),"FISH OUT OF WATER","Ten fish flop, then leap into the lift.");
            definitions[14]=CreateEventDefinition(14,typeof(TankEvent),"ARMORED ROOM SERVICE","Dodge the cannon and its blast.");
            definitions[15]=CreateEventDefinition(15,typeof(UfoEvent),"UNIDENTIFIED FLOOR OBJECT","Do not stand under the tractor beam.");
            definitions[16]=CreateEventDefinition(16,typeof(GrandPianoEvent),"GRAND FINALE","Mind the falling piano.");
            round.events.propPrefabs=props;round.events.definitions=definitions;
            session.networkPrefabs=props.Select(p=>p.GetComponent<NetworkObject>()).Concat(new[]{session.playerPrefab.GetComponent<NetworkObject>()}).ToArray();
            EditorUtility.SetDirty(round.events);EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("ELEVATOR_NEW_EVENTS_OK: four events and five props installed without rebuilding the cabin.");
        }
        static GameObject BuildCabin()
        {
            var root=new GameObject("Cabin");var t=root.transform;
            var wood=Mat("Walnut",new Color(.34f,.19f,.12f));
            var sage=Mat("Sage",new Color(.4f,.57f,.48f));
            var tile=Mat("Travertine",new Color(.76f,.75f,.64f));
            var glow=Mat("WarmLight",new Color(1,.88f,.58f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,.75f,.38f)*3);
            gold.SetFloat("_Metallic",.65f);gold.SetFloat("_Smoothness",.7f);
                        for(int x=0;x<4;x++)for(int z=0;z<4;z++)
            {
                var panel=Cube("Breakable floor "+(x*4+z),t,new Vector3((x-1.5f)*1.625f,-.18f,(z-1.5f)*1.625f),new Vector3(1.625f,.36f,1.625f),(x+z)%2==0?tile:sage);
                Anchor(panel);panel.AddComponent<CabinPanel>().index=x*4+z;
            }
            BuildWallSections(t,cream,teal,gold,glow);
            Anchor(Cube("Ceiling",t,new Vector3(0,4.52f,0),new Vector3(6.5f,.18f,6.5f),cream));

            Cube("Ceiling recess",t,new Vector3(0,4.39f,0),new Vector3(4.9f,.07f,4.9f),teal,false);
            Cube("Ceiling diffuser",t,new Vector3(0,4.30f,0),new Vector3(2.6f,.09f,2.6f),glow,false);
            for(int i=-1;i<=1;i+=2)
            {
                Anchor(Cube("Door frame",t,new Vector3(i*3.08f,2.1f,3),new Vector3(.26f,4.3f,.47f),gold));
                for(int z=-1;z<=1;z+=2)
                {
                }
            }
            var left=Cube("Left Door",t,new Vector3(-1.48f,1.95f,3),new Vector3(2.96f,3.9f,.16f),teal);
            var right=Cube("Right Door",t,new Vector3(1.48f,1.95f,3),new Vector3(2.96f,3.9f,.16f),teal);
            foreach(var door in new[]{left,right})
            {
                Cube("Door inlay",door.transform,new Vector3(0,0,.53f),new Vector3(.86f,.87f,.1f),gold,false);
                Cube("Door inner",door.transform,new Vector3(0,0,.61f),new Vector3(.846f,.86f,.06f),teal,false);
                for(int n=-1;n<=1;n+=2)Cube("Door rib",door.transform,new Vector3(n*.34f,0,.66f),new Vector3(.007f,.78f,.04f),gold,false);
            }
            var doors=root.AddComponent<ElevatorDoorController>();doors.left=Rigid(left,100,true);doors.right=Rigid(right,100,true);Anchor(left);Anchor(right);
            Cube("Header",t,new Vector3(0,4.18f,3),new Vector3(6.5f,.59f,.5f),teal);
            Cube("Display frame",t,new Vector3(0,4.18f,3.27f),new Vector3(1.15f,.45f,.07f),gold,false);
            Cube("Display glass",t,new Vector3(0,4.18f,3.32f),new Vector3(1.06f,.36f,.03f),dark,false);
            Text("Floor Display",t,new Vector3(0,4.18f,3.345f),"00",.075f,mint.color,Quaternion.Euler(0,180,0));
            for(int i=0;i<5;i++)
            {
                var button=Shape("Control button",PrimitiveType.Cylinder,t,new Vector3(2.995f,1.5f+i*.24f,1.8f),new Vector3(.13f,.025f,.13f),i==0?glow:gold,false);
                button.transform.localRotation=Quaternion.Euler(0,0,90);
                var near=t.GetComponentsInChildren<CabinPanel>().Where(p=>p.index>=16).OrderBy(p=>Vector3.Distance(p.transform.position,button.transform.position)).First();button.transform.SetParent(near.transform,true);
            }
            var zone=new GameObject("SafeZone");zone.transform.SetParent(t);zone.transform.localPosition=new Vector3(0,2.4f,.05f);
            var trigger=zone.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(6.7f,6.2f,6.8f);
            return root;
        }
        static void BuildWallSections(Transform cabin,Material wall,Material trim,Material brass,Material light)
        {
            for(int side=0;side<3;side++)for(int col=0;col<4;col++)for(int row=0;row<2;row++)
            {
                float along=(col-1.5f)*1.625f,y=.975f+row*2.35f;int sign=side==1?-1:1;
                Vector3 center=side==0?new Vector3(along,y,-3.2f):new Vector3(sign*3.2f,y,along);
                Vector3 size=side==0?new Vector3(1.625f,2.35f,.25f):new Vector3(.25f,2.35f,1.625f);
                var panel=Cube("Breakable wall "+(16+side*8+col*2+row),cabin,center,size,wall);
                Anchor(panel);Rigid(panel,1000,true);panel.AddComponent<CabinPanel>().index=16+side*8+col*2+row;
                if(row==0)
                {
                    var dado=Cube("Wall panel trim",cabin,side==0?new Vector3(along,.58f,-3.035f):new Vector3(sign*3.035f,.58f,along),side==0?new Vector3(1.625f,1.1f,.12f):new Vector3(.12f,1.1f,1.625f),trim,false);
                    dado.transform.SetParent(panel.transform,true);
                    var rail=Cube(side==0?"Back handrail":"Side handrail",cabin,side==0?new Vector3(along,1.18f,-2.87f):new Vector3(sign*2.87f,1.18f,along),side==0?new Vector3(1.625f,.12f,.12f):new Vector3(.12f,.12f,1.625f),brass);
                    Anchor(rail);rail.transform.SetParent(panel.transform,true);
                }
                else
                {
                    var mould=Cube("Wall crown",cabin,side==0?new Vector3(along,3.85f,-3.03f):new Vector3(sign*3.03f,3.85f,along),side==0?new Vector3(1.625f,.09f,.15f):new Vector3(.15f,.09f,1.625f),brass,false);mould.transform.SetParent(panel.transform,true);
                    if(side!=0&&(col==0||col==3))
                    {
                        var lamp=Shape("Sconce globe",PrimitiveType.Sphere,cabin,new Vector3(sign*2.83f,2.7f,along),new Vector3(.3f,.5f,.3f),light,false);lamp.transform.SetParent(panel.transform,true);
                    }
                    if(side==0&&col==2)
                    {
                        var plaque=Cube("Cabin plaque",cabin,new Vector3(along,2.9f,-2.99f),new Vector3(1.35f,.9f,.06f),trim,false);plaque.transform.SetParent(panel.transform,true);
                        var label=Text("Cabin rules",cabin,new Vector3(along,2.9f,-2.945f),"ODD HOURS\nHOLD ON",.052f,cream.color,Quaternion.Euler(0,180,0));label.transform.SetParent(panel.transform,true);
                    }
                }
            }
        }
        static void BuildCorridor()
        {
            var root=new GameObject("Odd Hours Hotel / Floor Lobby");var t=root.transform;
            var stone=Mat("LobbyStone",new Color(.83f,.8f,.7f));
            var wall=Mat("LobbyPlaster",new Color(.89f,.85f,.74f));
            var carpet=Mat("Carpet",new Color(.19f,.36f,.31f));
            var wood=Mat("Walnut",new Color(.34f,.19f,.12f));
            var glow=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/WarmLight.mat");
            Cube("Corridor floor",t,new Vector3(0,-.22f,15.3f),new Vector3(14,.44f,24.1f),stone);
            Cube("Carpet runner",t,new Vector3(0,.015f,15.4f),new Vector3(3.9f,.025f,23.7f),carpet,false);
            for(int sign=-1;sign<=1;sign+=2)
            {
                Cube("Carpet edging",t,new Vector3(sign*1.85f,.031f,15.4f),new Vector3(.07f,.02f,23.7f),gold,false);
                Cube("Lobby side wall",t,new Vector3(sign*7,2.75f,15.3f),new Vector3(.3f,5.5f,24.4f),wall);
                Cube("Lobby dado",t,new Vector3(sign*6.8f,.7f,15.3f),new Vector3(.17f,1.4f,24.2f),teal,false);
                Cube("Lobby cornice",t,new Vector3(sign*6.75f,4.9f,15.3f),new Vector3(.3f,.18f,24.2f),gold,false);
                Cube("Entry surround",t,new Vector3(sign*5.1f,2.5f,3.02f),new Vector3(3.6f,5,.45f),wall);
                for(int n=0;n<4;n++)
                {
                    float z=7+n*5.4f;
                    Cube("Room door surround",t,new Vector3(sign*6.77f,1.8f,z),new Vector3(.21f,3.6f,1.8f),gold,false);
                    Cube("Room door",t,new Vector3(sign*6.63f,1.76f,z),new Vector3(.12f,3.43f,1.64f),wood,false);
                    Shape("Room handle",PrimitiveType.Sphere,t,new Vector3(sign*6.51f,1.4f,z-.5f),Vector3.one*.12f,gold,false);
                    Cube("Wall lamp",t,new Vector3(sign*6.6f,3.4f,z+2.1f),new Vector3(.2f,.65f,.32f),glow,false);
                }
                for(int n=0;n<2;n++)
                {
                    float z=10+n*10;
                    Cube("Bench seat",t,new Vector3(sign*5.6f,.65f,z),new Vector3(1.35f,.25f,2.5f),teal);
                    Cube("Bench back",t,new Vector3(sign*6.2f,1.25f,z),new Vector3(.17f,1.1f,2.5f),teal);
                    for(int leg=-1;leg<=1;leg+=2)Cube("Bench leg",t,new Vector3(sign*5.6f,.25f,z+leg*.9f),new Vector3(.9f,.5f,.12f),gold);
                    Plant(t,new Vector3(sign*5.7f,0,z+2.7f));
                }
            }
            Cube("Lobby ceiling",t,new Vector3(0,5.6f,15.3f),new Vector3(14,.2f,24.4f),wall);
            Cube("Lobby far wall",t,new Vector3(0,2.7f,27.5f),new Vector3(14,5.4f,.3f),teal);
            Cube("Far double door",t,new Vector3(0,1.8f,27.28f),new Vector3(3,3.6f,.1f),wood);
            Text("Lobby title",t,new Vector3(0,4.6f,27.1f),"ODD HOURS HOTEL",.16f,gold.color,Quaternion.identity);
            Text("Lobby caption",t,new Vector3(0,3.95f,27.1f),"UNEXPECTED GUESTS WELCOME",.053f,cream.color,Quaternion.identity);
            for(int n=0;n<4;n++)
            {
                float z=6.2f+n*6;
                Shape("Pendant",PrimitiveType.Cylinder,t,new Vector3(0,5.1f,z),new Vector3(1.3f,.07f,1.3f),gold,false);
                Shape("Pendant diffuser",PrimitiveType.Cylinder,t,new Vector3(0,5.01f,z),new Vector3(1.1f,.025f,1.1f),glow,false);
                var light=new GameObject("Warm lobby light");light.transform.SetParent(t);light.transform.position=new Vector3(0,4.6f,z);
                var lamp=light.AddComponent<Light>();lamp.type=LightType.Point;lamp.range=10;lamp.intensity=4;lamp.color=new Color(1,.84f,.61f);
            }
            for(int n=0;n<20;n++)for(int side=-1;side<=1;side+=2)
                Cube("Floor seam",t,new Vector3(side*4.45f,.008f,4+n*1.18f),new Vector3(5,.008f,.012f),gold,false);
            Text("Entry plaque",t,new Vector3(-4.9f,2.9f,3.28f),"ODD\nHOURS",.18f,teal.color,Quaternion.Euler(0,180,0));
            Text("Service sign",t,new Vector3(4.9f,2.6f,3.28f),"LIFT 01\nMAX 8\nGOOD LUCK",.065f,teal.color,Quaternion.Euler(0,180,0));
            var air=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/AirStreaks.mat");
            if(!air){air=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(air,Root+"/Materials/AirStreaks.mat");}
            root.AddComponent<EnvironmentEffects>().airStreakMaterial=air;
        }
        static void Plant(Transform parent,Vector3 p)
        {
            Shape("Planter",PrimitiveType.Cylinder,parent,p+Vector3.up*.4f,new Vector3(.85f,.4f,.85f),cream);
            Shape("Soil",PrimitiveType.Cylinder,parent,p+Vector3.up*.81f,new Vector3(.77f,.025f,.77f),dark,false);
            for(int i=0;i<7;i++)
            {
                float angle=i*2.4f;
                var leaf=Shape("Leaf",PrimitiveType.Capsule,parent,p+new Vector3(Mathf.Sin(angle)*.3f,1.35f+i*.035f,Mathf.Cos(angle)*.3f),new Vector3(.28f,.65f,.13f),mint,false);
                leaf.transform.localRotation=Quaternion.Euler(Mathf.Cos(angle)*35,angle*Mathf.Rad2Deg,Mathf.Sin(angle)*35);
            }
        }
        static void SetupPostProcessing()
        {
            string path=Root+"/Materials/HotelAtmosphere.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
            if(!profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom)){bloom=profile.Add<UnityEngine.Rendering.Universal.Bloom>();AssetDatabase.AddObjectToAsset(bloom,profile);}
            bloom.intensity.Override(.24f);bloom.threshold.Override(1);
            if(!profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var color)){color=profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>();AssetDatabase.AddObjectToAsset(color,profile);}
            color.postExposure.Override(.3f);color.contrast.Override(8);color.saturation.Override(5);
            if(!profile.TryGet<UnityEngine.Rendering.Universal.Tonemapping>(out var tone)){tone=profile.Add<UnityEngine.Rendering.Universal.Tonemapping>();AssetDatabase.AddObjectToAsset(tone,profile);}
            tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
            var go=new GameObject("Hotel Atmosphere");var volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
            rubber.bounciness=.9f;EditorUtility.SetDirty(rubber);EditorUtility.SetDirty(profile);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        static void Anchor(GameObject go)=>go.AddComponent<GrabAnchor>();
        static TextMesh Text(string name,Transform parent,Vector3 position,string value,float size,Color color,Quaternion rotation)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
            var text=go.AddComponent<TextMesh>();text.text=value;text.fontSize=64;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=color; var fontMat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/WorldText.mat");if(!fontMat){fontMat=new Material(Shader.Find("Elevator/WorldText"));fontMat.mainTexture=text.GetComponent<Renderer>().sharedMaterial.mainTexture;AssetDatabase.CreateAsset(fontMat,Root+"/Materials/WorldText.mat");}text.GetComponent<Renderer>().sharedMaterial=fontMat;return text;
        }
        [MenuItem("Elevator/Build Windows Player")]
        public static void BuildPlayerExecutable()
        {
            Directory.CreateDirectory("Builds/Windows");
            var steamConfig=JsonUtility.FromJson<SteamConfiguration>(Resources.Load<TextAsset>("SteamConfig").text);
            File.WriteAllText("Builds/Windows/steam_appid.txt",steamConfig.appId.ToString());
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{Root+"/Scenes/Elevator.unity"},locationPathName="Builds/Windows/Elevator.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded){var config=JsonUtility.FromJson<SteamConfiguration>(Resources.Load<TextAsset>("SteamConfig").text);File.WriteAllText("Builds/Windows/steam_appid.txt",config.appId.ToString());}
            Debug.Log("ELEVATOR_PLAYER_BUILD: "+report.summary.result);
        }
    }
}













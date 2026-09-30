using UnityEngine;
namespace ElevatorGame
{
    // Uses the prefab material so the shader is also included in standalone builds.
    public static class WeaponVisuals
    {
        static readonly Color Dark=new(.075f,.105f,.14f), Steel=new(.6f,.72f,.79f), Gold=new(1,.65f,.12f), Red=new(.95f,.12f,.18f), Cyan=new(.1f,.95f,1);
        public static void Build(Transform root,WeaponKind kind,Material material)
        {
            void Part(string name,PrimitiveType shape,Vector3 position,Vector3 scale,Color color,Vector3 rotation=default)
            {
                var go=GameObject.CreatePrimitive(shape);go.name=name;go.layer=10;go.transform.SetParent(root,false);
                go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=Quaternion.Euler(rotation);
                var collider=go.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider);
                var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
                var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",color);tint.SetFloat("_Smoothness",.55f);renderer.SetPropertyBlock(tint);
            }
            void Box(string n,Vector3 p,Vector3 s,Color c)=>Part(n,PrimitiveType.Cube,p,s,c);
            void Ball(string n,Vector3 p,Vector3 s,Color c)=>Part(n,PrimitiveType.Sphere,p,s,c);
            void Tube(string n,Vector3 p,float radius,float length,Color c,bool forward=false)=>Part(n,PrimitiveType.Cylinder,p,new Vector3(radius,length*.5f,radius),c,forward?new Vector3(90,0,0):Vector3.zero);
            Box("Rubber grip",new(0,-.13f,0),new(.16f,.34f,.19f),Dark);
            for(int i=0;i<4;i++)Box("Grip wrap",new(0,-.25f+i*.065f,-.005f),new(.17f,.02f,.2f),Steel);
            switch(kind)
            {
                case WeaponKind.Bat:
                    Tube("Ash shaft",new(0,.32f,0),.11f,.8f,new(.57f,.28f,.09f));
                    Part("Wide bat barrel",PrimitiveType.Capsule,new(0,.67f,0),new(.26f,.4f,.26f),new(.94f,.69f,.32f));
                    Tube("End cap",new(0,-.31f,0),.23f,.055f,Gold);
                    Tube("Red team stripe",new(0,.49f,0),.268f,.095f,Red);break;
                case WeaponKind.Hammer:
                    Tube("Long handle",new(0,.31f,0),.12f,.78f,Gold);
                    Box("Forged head",new(0,.72f,0),new(.84f,.4f,.4f),Dark);
                    foreach(float x in new[]{-.43f,.43f})Box("Steel striking face",new(x,.72f,0),new(.12f,.46f,.46f),Steel);
                    Box("Hazard stripe",new(0,.72f,-.21f),new(.45f,.12f,.018f),Gold);break;
                case WeaponKind.BoxingGlove:
                    Tube("Spring cuff",new(0,0,.12f),.29f,.35f,Gold,true);
                    for(int i=0;i<5;i++)Tube("Spring rings",new(0,0,i*.065f),.34f,.025f,Steel,true);
                    Ball("Oversized leather fist",new(0,.02f,.49f),new(.62f,.52f,.66f),Red);
                    Ball("Thumb",new(.29f,-.09f,.32f),new(.23f,.29f,.31f),Red);
                    Box("White fist patch",new(0,.274f,.49f),new(.28f,.035f,.3f),Color.white);break;
                case WeaponKind.ShockStaff:
                    Tube("Insulated staff",new(0,.35f,0),.14f,1,Dark);
                    for(int i=0;i<5;i++)Tube("Copper coil",new(0,.62f+i*.065f,0),.3f,.028f,Gold);
                    Ball("Charged core",new(0,1,0),new(.3f,.3f,.3f),Cyan);
                    foreach(float x in new[]{-.2f,.2f})Box("Electrode",new(x,.99f,0),new(.065f,.37f,.08f),Steel);break;
                case WeaponKind.Pistol:
                    Box("Orange receiver",new(0,.07f,.17f),new(.25f,.24f,.53f),Gold);
                    Box("Slide",new(0,.22f,.19f),new(.26f,.1f,.6f),Steel);
                    Tube("Muzzle",new(0,.11f,.51f),.16f,.14f,Dark,true);
                    Box("Rear sight",new(0,.31f,-.04f),new(.12f,.08f,.07f),Dark);
                    Box("Trigger guard",new(0,-.18f,.18f),new(.09f,.05f,.25f),Gold);break;
                case WeaponKind.Shotgun:
                    Box("Wood stock",new(0,-.025f,-.3f),new(.22f,.27f,.46f),new(.55f,.24f,.095f));
                    Box("Breech",new(0,.07f,.03f),new(.4f,.26f,.36f),Dark);
                    foreach(float x in new[]{-.11f,.11f}){Tube("Long steel barrel",new(x,.1f,.53f),.17f,.92f,Steel,true);Tube("Dark muzzle bore",new(x,.1f,1),.12f,.014f,Dark,true);}
                    Box("Pump foregrip",new(0,-.075f,.49f),new(.41f,.17f,.34f),Gold);break;
                case WeaponKind.Bazooka:
                    Tube("Launcher tube",new(0,.12f,.16f),.45f,1.25f,new(.25f,.43f,.19f),true);
                    foreach(float z in new[]{-.5f,.8f}){Tube("Reinforced rim",new(0,.12f,z),.55f,.13f,Steel,true);Tube("Open dark bore",new(0,.12f,z+(z>0?.07f:-.07f)),.42f,.02f,Dark,true);}
                    Box("Shoulder pad",new(0,-.17f,-.3f),new(.35f,.19f,.4f),Dark);
                    Box("Sight mount",new(.19f,.41f,.07f),new(.1f,.24f,.09f),Gold);break;
                case WeaponKind.GrenadeLauncher:
                    Tube("Heavy barrel",new(0,.13f,.53f),.32f,.62f,Steel,true);
                    Tube("Muzzle bore",new(0,.13f,.85f),.25f,.02f,Dark,true);
                    Tube("Rotating drum",new(0,-.07f,.12f),.57f,.4f,Dark,true);
                    for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Tube("Loaded chamber",new(Mathf.Cos(a)*.22f,-.07f+Mathf.Sin(a)*.22f,.13f),.12f,.43f,Gold,true);}
                    Box("Receiver",new(0,.28f,.05f),new(.3f,.17f,.5f),Red);break;
                case WeaponKind.Blower:
                    Ball("Turbine housing",new(0,.06f,.06f),new(.65f,.61f,.63f),new(.12f,.65f,.83f));
                    Tube("Air nozzle",new(0,.06f,.5f),.37f,.5f,Steel,true);
                    Tube("Nozzle throat",new(0,.06f,.76f),.29f,.015f,Dark,true);
                    Tube("Rear fan",new(0,.06f,-.27f),.48f,.03f,Dark,true);
                    for(int i=0;i<3;i++)Part("Fan blade",PrimitiveType.Cube,new(0,.06f,-.29f),new(.43f,.055f,.035f),Gold,new(0,0,i*60));break;
                case WeaponKind.Grappler:
                    Box("Winch body",new(0,.08f,.08f),new(.38f,.32f,.5f),Gold);
                    Tube("Cable spool",new(0,.32f,.02f),.29f,.18f,Dark);
                    Tube("Launch rail",new(0,.12f,.52f),.13f,.5f,Steel,true);
                    for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;Vector3 offset=new(Mathf.Cos(a)*.22f,Mathf.Sin(a)*.22f,0);Box("Hook prong",new Vector3(0,.12f,.69f)+offset,new(.065f,.065f,.37f),Steel);Ball("Hook tip",new Vector3(0,.12f,.88f)+offset*.6f,new(.1f,.1f,.14f),Cyan);}break;
            }
        }
    }
}

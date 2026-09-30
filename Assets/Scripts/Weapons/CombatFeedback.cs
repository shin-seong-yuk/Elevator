using System.Collections.Generic;
using UnityEngine;
namespace ElevatorGame
{
    // Presentation only. All calls originate from accepted server actions / confirmed impacts.
    public sealed class CombatFeedback : MonoBehaviour
    {
        readonly List<Transform> pieces=new();
        readonly List<Vector3> sizes=new();
        float born,lifetime; Vector3 velocity;
        public static int ImpactCount {get;private set;}
        public static string AttackCue(WeaponKind kind)=>kind switch
        {
            WeaponKind.Pistol=>"pistol",WeaponKind.Shotgun=>"shotgun",WeaponKind.Bazooka=>"rocket",
            WeaponKind.GrenadeLauncher=>"launcher",WeaponKind.ShockStaff=>"zap",WeaponKind.Blower=>"blower",
            WeaponKind.Grappler=>"grappler",WeaponKind.Hammer=>"heavySwing",_=>"swing"
        };
        public static void Show(Vector3 start,Vector3 end,int kind,Material material)
        {
            bool impact=kind>=100,explosion=kind==20;
            var go=new GameObject(impact?"Confirmed hit burst":explosion?"Explosion shock ring":"Weapon action trail");
            var fx=go.AddComponent<CombatFeedback>();fx.born=Time.time;fx.lifetime=explosion?.5f:impact?.28f:.16f;
            Color color=impact?new Color(1,.9f,.38f):kind==7||kind==9?new Color(.15f,1,1):new Color(1,.62f,.15f);
            if(impact||explosion)
            {
                ImpactCount++;
                int count=explosion?18:9;
                for(int i=0;i<count;i++)
                {
                    Vector3 d=Quaternion.Euler(i*137.5f,i*222.5f,0)*Vector3.forward;
                    fx.Line(start+d*.12f,start+d*(explosion?2.4f:.52f),explosion?.12f:.07f,color,material);
                }
                AudioManager.Instance?.PlayAt(explosion?"explosion":"hit",start,explosion?1:.75f);
                if(impact)foreach(var p in RoundManager.Players())if(p.Slot.Value==kind-100)
                {
                    var flash=p.GetComponent<HitFlash>();if(!flash)flash=p.gameObject.AddComponent<HitFlash>();flash.Pulse();
                    fx.velocity=end*.025f;break;
                }
            }
            else if(kind==0||kind==1||kind==6||kind==7||kind==8)
            {
                Vector3 forward=(end-start).normalized;
                Vector3 side=Vector3.Cross(Vector3.up,forward).normalized;
                if(side.sqrMagnitude<.1f)side=Vector3.right;
                if(kind==8)
                    for(int i=0;i<5;i++){Vector3 offset=side*(i-2)*.17f;fx.Line(start+offset,end+offset*4,.05f,new Color(.65f,.92f,1),material);}
                else
                    for(int i=0;i<7;i++){float a=(i/7f-.5f)*1.8f,b=((i+1)/7f-.5f)*1.8f;float radius=kind==1?1.7f:1.3f;fx.Line(start+(forward*Mathf.Cos(a)+side*Mathf.Sin(a))*radius,start+(forward*Mathf.Cos(b)+side*Mathf.Sin(b))*radius,.075f,color,material);}
            }
            else
            {
                fx.Line(start,end,kind==9?.045f:.045f,color,material);
                Vector3 d=(end-start).normalized;
                for(int i=0;i<4;i++){Vector3 side=Quaternion.LookRotation(d==Vector3.zero?Vector3.forward:d)*Quaternion.Euler(0,0,i*90)*Vector3.right;fx.Line(start,start+d*.35f+side*.14f,.055f,color,material);}
            }
            Destroy(go,fx.lifetime);
        }
        void Line(Vector3 from,Vector3 to,float width,Color color,Material material)
        {
            var go=new GameObject("Streak");go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=2;line.SetPosition(0,from);line.SetPosition(1,to);
            line.startWidth=width;line.endWidth=width*.12f;line.numCapVertices=3;line.sharedMaterial=material;
            var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);line.SetPropertyBlock(block);
            pieces.Add(go.transform);sizes.Add(new Vector3(width,0,0));
        }
        void Update()
        {
            float fade=1-Mathf.Clamp01((Time.time-born)/lifetime);
            transform.position+=velocity*Time.deltaTime;
            for(int i=0;i<pieces.Count;i++)if(pieces[i]){var line=pieces[i].GetComponent<LineRenderer>();line.startWidth=sizes[i].x*fade;line.endWidth=sizes[i].x*.12f*fade;}
        }
    }
    public sealed class HitFlash : MonoBehaviour
    {
        Renderer[] visuals;MaterialPropertyBlock[] original;float until;bool active;PlayerController actor;
        public void Pulse()
        {
            actor=GetComponent<PlayerController>();until=Time.time+.2f;
            if(active)return;active=true;
            var all=GetComponentsInChildren<Renderer>();var list=new List<Renderer>();
            foreach(var r in all)if(r.name.StartsWith("Suit")||r.name.StartsWith("Helmet")||r.name.StartsWith("Glove"))list.Add(r);
            visuals=list.ToArray();original=new MaterialPropertyBlock[visuals.Length];
            for(int i=0;i<visuals.Length;i++)
            {original[i]=new MaterialPropertyBlock();visuals[i].GetPropertyBlock(original[i]);var flash=new MaterialPropertyBlock();visuals[i].GetPropertyBlock(flash);flash.SetColor("_BaseColor",new Color(1,.94f,.64f));visuals[i].SetPropertyBlock(flash);}
        }
        void Update(){if(active&&Time.time>=until)Restore();}
        void OnDisable(){Restore();}
        void Restore(){if(!active)return;active=false;for(int i=0;i<visuals.Length;i++)if(visuals[i])visuals[i].SetPropertyBlock(original[i]);}
        void OnGUI()
        {
            if(!active||!actor||!actor.ControlledLocally)return;
            Color previous=GUI.color;GUI.color=new Color(1,.3f,.13f,Mathf.Clamp01((until-Time.time)/.2f)*.38f);
            float edge=Mathf.Max(5,Screen.height*.014f);
            GUI.DrawTexture(new Rect(0,0,Screen.width,edge),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,Screen.height-edge,Screen.width,edge),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,0,edge,Screen.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(Screen.width-edge,0,edge,Screen.height),Texture2D.whiteTexture);GUI.color=previous;
        }
    }
}

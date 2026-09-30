using System.Collections.Generic;
using UnityEngine;
namespace ElevatorGame
{
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance{get;private set;}
        readonly Dictionary<string,AudioClip> clips=new();
        readonly Dictionary<string,float> cooldown=new();
        AudioSource[] voices;AudioSource ui,motor,air;int voice;
        public float MasterVolume=.7f;
        void Awake()
        {
            Instance=this;ui=gameObject.AddComponent<AudioSource>();
            voices=new AudioSource[16];
            for(int i=0;i<voices.Length;i++)
            {var go=new GameObject("Spatial voice "+i);go.transform.SetParent(transform);voices[i]=go.AddComponent<AudioSource>();voices[i].spatialBlend=.7f;voices[i].minDistance=2;voices[i].maxDistance=35;voices[i].rolloffMode=AudioRolloffMode.Linear;}
            foreach(var cue in new[]{"ding","door","doorClose","grab","release","jump","step","fall","bump","impact","wind","roar","rumble","chicken","out","win","warning","throw","bite","whoosh","slam","dinoRun","runner","click","motor","cart","cartCrash","swing","heavySwing","hit","pistol","shotgun","rocket","launcher","zap","blower","grappler","explosion"})
                clips[cue]=Synthesize(cue);
            motor=gameObject.AddComponent<AudioSource>();motor.clip=clips["motor"];motor.loop=true;motor.volume=0;motor.Play();
            air=gameObject.AddComponent<AudioSource>();air.clip=clips["wind"];air.loop=true;air.volume=0;air.Play();
            var reverb=gameObject.AddComponent<AudioReverbFilter>();reverb.reverbPreset=AudioReverbPreset.Room;
        }
        void Update()
        {
            var r=RoundManager.Instance;bool running=r&&r.Phase.Value==RoundPhase.Playing;
            motor.volume=Mathf.Lerp(motor.volume,running&&r.Stage.Value==ElevatorStage.Moving?.11f*MasterVolume:0,Time.deltaTime*4);
            string title=r?r.EventTitle.Value.ToString():"";
            bool windy=running&&r.Stage.Value==ElevatorStage.Event&&(title.Contains("HOLD")||title.Contains("ATMOSPHERE")||title.Contains("WATER"));
            air.volume=Mathf.Lerp(air.volume,windy?.25f*MasterVolume:0,Time.deltaTime*2);
        }
        public void Play(string cue,float volume=1)
        {
            if(!Allowed(cue)||!clips.TryGetValue(cue,out var clip))return;
            ui.pitch=cue=="ding"||cue=="win"?1:Random.Range(.95f,1.05f);
            ui.PlayOneShot(clip,volume*MasterVolume*.6f);
        }
        public void PlayAt(string cue,Vector3 position,float volume=1)
        {
            if(!Allowed(cue)||!clips.TryGetValue(cue,out var clip))return;
            var source=voices[voice++%voices.Length];source.transform.position=position;source.pitch=Random.Range(.93f,1.08f);source.clip=clip;source.volume=volume*MasterVolume*.65f;source.Play();
        }
        bool Allowed(string cue)
        {
            float delay=cue=="step"?.06f:cue=="bump"||cue=="impact"?.07f:.015f;
            if(cooldown.TryGetValue(cue,out float until)&&Time.unscaledTime<until)return false;
            cooldown[cue]=Time.unscaledTime+delay;return true;
        }
        AudioClip Synthesize(string cue)
        {
            const int rate=44100;
            float length=cue=="ding"?1.8f:cue=="door"||cue=="doorClose"?1.85f:cue=="wind"||cue=="motor"?3:cue=="win"?2.4f:cue=="roar"?1.5f:cue=="out"?.9f:cue=="grab"?.16f:cue=="step"?.12f:.5f;
            if(cue=="pistol")length=.22f;else if(cue=="shotgun")length=.4f;else if(cue=="explosion")length=.9f;else if(cue=="hit")length=.22f;else if(cue=="swing")length=.24f;
            float[] data=new float[(int)(length*rate)];var random=new System.Random(cue.GetHashCode());float filtered=0,phase=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate,n=t/length;
                float noise=(float)random.NextDouble()*2-1;filtered=Mathf.Lerp(filtered,noise,.09f);
                float env=Mathf.Min(1,t*60)*Mathf.Pow(1-n,2),v=0;
                switch(cue)
                {
                    case "pistol":v=noise*Mathf.Exp(-t*55)*.65f+Mathf.Sin(2*Mathf.PI*130*t)*Mathf.Exp(-t*27)*.5f;break;
                    case "shotgun":v=noise*Mathf.Exp(-t*30)*.65f+filtered*Mathf.Exp(-t*9)+Mathf.Sin(2*Mathf.PI*62*t)*Mathf.Exp(-t*12)*.5f;break;
                    case "explosion":v=(filtered*1.5f+Mathf.Sin(2*Mathf.PI*42*t)*.55f)*Mathf.Exp(-t*5)*Mathf.Min(t*150,1);break;
                    case "hit":phase+=2*Mathf.PI*Mathf.Lerp(250,65,n)/rate;v=(Mathf.Sin(phase)*.65f+noise*Mathf.Exp(-t*70)*.3f)*env;break;
                    case "swing":case "heavySwing":v=(filtered*1.4f+Mathf.Sin(2*Mathf.PI*90*t)*.1f)*Mathf.Pow(Mathf.Sin(n*Mathf.PI),2);break;
                    case "rocket":v=(filtered*1.6f+noise*.16f)*env;break;
                    case "launcher":phase+=2*Mathf.PI*Mathf.Lerp(150,42,n)/rate;v=(Mathf.Sin(phase)*.65f+filtered*.65f)*env;break;
                    case "zap":phase+=2*Mathf.PI*(780+180*Mathf.Sin(t*75))/rate;v=(Mathf.Sin(phase)*.3f+noise*.27f)*env*(.55f+.45f*Mathf.Sin(t*130));break;
                    case "blower":v=(filtered+Mathf.Sin(2*Mathf.PI*220*t)*.12f)*env;break;
                    case "grappler":phase+=2*Mathf.PI*Mathf.Lerp(900,190,n)/rate;v=(Mathf.Sin(phase)*.32f+filtered*.5f)*env;break;
                    case "ding":
                        v=Mathf.Sin(2*Mathf.PI*880*t)*Mathf.Exp(-t*3)*.5f+Mathf.Sin(2*Mathf.PI*1320*t)*Mathf.Exp(-t*5)*.22f+Mathf.Sin(2*Mathf.PI*1764*t)*Mathf.Exp(-t*7)*.08f;break;
                    case "door":case "doorClose":
                        v=filtered*Mathf.Sin(n*Mathf.PI)*.8f+Mathf.Sin(2*Mathf.PI*(cue=="door"?130:110)*t)*Mathf.Sin(n*Mathf.PI)*.065f;
                        if(n>.93f)v+=noise*Mathf.Exp(-(n-.93f)*75)*.18f;break;
                    case "grab":phase+=2*Mathf.PI*Mathf.Lerp(360,140,n)/rate;v=Mathf.Sin(phase)*env*.65f+filtered*env*.25f;break;
                    case "release":case "click":v=Mathf.Sin(2*Mathf.PI*600*t)*env*.2f;break;
                    case "step":v=(filtered*.7f+Mathf.Sin(2*Mathf.PI*85*t)*.3f)*env;break;
                    case "wind":v=filtered*(.45f+.15f*Mathf.Sin(2*Mathf.PI*t/length));break;
                    case "motor":v=Mathf.Sin(2*Mathf.PI*60*t)*.12f+Mathf.Sin(2*Mathf.PI*120*t)*.04f;break;
                    case "win":
                        float f=t<.28f?523:t<.56f?659:t<.84f?784:1047;
                        v=(Mathf.Sin(2*Mathf.PI*f*t)+Mathf.Sin(2*Mathf.PI*f*1.5f*t)*.25f)*env*.35f;break;
                    case "roar":case "dinoRun":case "runner":
                        phase+=2*Mathf.PI*(cue=="runner"?220:95+30*Mathf.Sin(t*5))/rate;
                        v=(Mathf.Sin(phase)+Mathf.Sin(phase*2)*.3f+filtered*.8f)*env*(.28f+.1f*Mathf.Sin(t*22));break;
                    case "out":case "throw":case "jump":
                        phase+=2*Mathf.PI*Mathf.Lerp(cue=="jump"?180:650,cue=="jump"?500:70,n)/rate;v=Mathf.Sin(phase)*env*.4f;break;
                    case "chicken":phase+=2*Mathf.PI*(650+250*Mathf.Sin(t*40))/rate;v=Mathf.Sin(phase)*env*.35f;break;
                    case "warning":v=Mathf.Sin(2*Mathf.PI*680*t)*env*.23f*(Mathf.Sin(t*35)>0?1:0);break;
                    case "whoosh":v=filtered*Mathf.Sin(n*Mathf.PI)*.9f;break;
                    default:v=(Mathf.Sin(2*Mathf.PI*65*t)*.4f+filtered*.8f)*env;break;
                }
                data[i]=Mathf.Clamp(v,-.85f,.85f);
            }
            var clip=AudioClip.Create(cue,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void OnDestroy(){foreach(var clip in clips.Values)Destroy(clip);if(Instance==this)Instance=null;}
    }
}





using UnityEngine;
namespace ElevatorGame
{
    public sealed class EnvironmentEffects : MonoBehaviour
    {
        public Material airStreakMaterial;
        ParticleSystem dust;
        Light[] lights;
        Material particleMaterial;
        void Start()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null){enabled=false;return;}
            lights=GetComponentsInChildren<Light>();
            var go=new GameObject("Air streaks");go.transform.SetParent(transform);go.transform.position=new Vector3(0,2,0);
            dust=go.AddComponent<ParticleSystem>();dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=dust.main;main.startLifetime=.7f;main.startSpeed=0;main.startSize=.035f;main.maxParticles=180;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startColor=new Color(.83f,.96f,1,.65f);
            var emission=dust.emission;emission.rateOverTime=0;
            var shape=dust.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(5.5f,3,4);
            var velocity=dust.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.z=13;
            var renderer=dust.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=3;renderer.velocityScale=.06f;
            if(!airStreakMaterial){enabled=false;return;}
            particleMaterial=new Material(airStreakMaterial);particleMaterial.SetColor("_BaseColor",new Color(.7f,.9f,1,.6f));renderer.material=particleMaterial;
            dust.Play();
        }
        void Update()
        {
            var r=RoundManager.Instance;if(!r)return;
            bool eventActive=r.Phase.Value==RoundPhase.Playing&&r.Stage.Value==ElevatorStage.Event;
            var title=r.EventTitle.Value.ToString();
            bool wind=eventActive&&(title.Contains("HOLD")||title.Contains("ATMOSPHERE"));
            var emission=dust.emission;emission.rateOverTime=wind?90:0;
            bool tremor=eventActive&&title.Contains("SHAKEN");
            for(int i=0;i<lights.Length;i++)
            {
                lights[i].intensity=tremor?2.8f+Mathf.Sin(Time.time*23+i)*1.4f:3.4f;
                lights[i].color=wind?new Color(.53f,.75f,1):new Color(1,.84f,.61f);
            }
        }
        void OnDestroy(){if(particleMaterial)Destroy(particleMaterial);}
    }
}



using UnityEngine;
namespace ElevatorGame
{
    public sealed class TankEvent : FloorEvent
    {
        NetworkProp tank;PlayerController victim;float nextShot=2.2f,retargetAt;int shots;
        public override void StartEvent()
        {
            base.StartEvent();tank=Spawn(12,new Vector3(0,1.05f,10),Vector3.zero);
            tank.Body.isKinematic=true;RoundManager.Instance.PlayCue("tank");
        }
        public override Vector3 GetRecommendedSafeDirection(Vector3 position)
        {
            float side=victim&&victim.Body.position.x>=0?-1:1;
            return new Vector3(side,0,-.35f).normalized;
        }
        public override void UpdateEvent()
        {
            if(!tank)return;
            if(!victim||!victim.Alive.Value||Elapsed>=retargetAt)
            {victim=RandomLiving(tank.Body.position);retargetAt=Elapsed+Manager.Random(1.6f,2.6f);}
            float z=Elapsed<2?Mathf.Lerp(10,5.4f,Elapsed*.5f):Elapsed>10?Mathf.Lerp(5.4f,9,Mathf.Clamp01((Elapsed-10)/3)):5.4f+Mathf.Sin(Elapsed*1.8f)*.35f;
            float x=victim?Mathf.MoveTowards(tank.Body.position.x,Mathf.Clamp(victim.Body.position.x,-2.8f,2.8f),Time.fixedDeltaTime*1.6f):Mathf.Sin(Elapsed*.7f)*.5f;
            tank.Body.MovePosition(new Vector3(x,1.05f,z));
            if(Elapsed<nextShot)return;
            nextShot+=2.35f;shots++;
            var target=victim?victim.Body.position+Vector3.up*.2f:new Vector3(Manager.Random(-1.8f,1.8f),1,-1.5f);
            Vector3 muzzle=tank.Body.position+new Vector3(0,.78f,-1.55f);
            var aim=(target-muzzle).normalized;
            var shell=Spawn(13,muzzle,aim*(19+Power*3));
            shell.GetComponent<WeaponProjectile>().InitializeEvent(3.2f);
            tank.Animate(shots);RoundManager.Instance.PlayCue("rocket");
            RoundManager.Instance.WeaponEffect(muzzle,muzzle+aim*1.5f,4);
        }
    }
}

using UnityEngine;

namespace ElevatorGame
{
    public sealed class HwachaEvent : FloorEvent
    {
        public const int ArrowCount=50;
        NetworkProp hwacha;
        float nextArrow=2f;
        int fired;
        public int Fired=>fired;

        public override void StartEvent()
        {
            base.StartEvent();
            hwacha=Spawn(16,new Vector3(0,1.05f,8),Vector3.zero);
            hwacha.Body.isKinematic=true;
            RoundManager.Instance.PlayCue("warning");
        }

        public override float GetDangerStrength()=>Running?(Elapsed>=2&&fired<ArrowCount?2.2f:.8f)*Power:0;
        public override Vector3 GetRecommendedSafeDirection(Vector3 position)
            =>new Vector3(position.x>=0?1:-1,0,-.7f).normalized;

        public override void UpdateEvent()
        {
            if(!hwacha)return;
            if(Elapsed<2)
                hwacha.Body.MovePosition(Vector3.MoveTowards(hwacha.Body.position,new Vector3(0,1.05f,6.3f),Time.fixedDeltaTime*.9f));
            while(Elapsed>=nextArrow&&fired<ArrowCount)
            {
                FireArrow(fired++);
                nextArrow+=.045f;
            }
        }

        void FireArrow(int shot)
        {
            int column=shot%10,row=(shot/10)%5;
            Vector3 muzzle=hwacha.Body.position+new Vector3((column-4.5f)*.21f,.75f+(row-2)*.17f,-.9f);
            var player=shot%3==0?RandomLiving(new Vector3(0,1,0)):null;
            Vector3 target=player&&player.Alive.Value
                ?player.Body.position+new Vector3(Manager.Random(-.3f,.3f),Manager.Random(-.1f,.5f),Manager.Random(-.35f,.35f))
                :new Vector3(Manager.Random(-3.5f,3.5f),Manager.Random(.6f,2.1f),Manager.Random(-2.5f,1.7f));
            Vector3 direction=(target-muzzle).normalized;
            var arrow=Spawn(17,muzzle,direction*(24+Power*3));
            arrow.Body.rotation=Quaternion.LookRotation(direction);
            arrow.Body.useGravity=false;
            if(shot%5==0)
            {
                RoundManager.Instance.PlayCue("hwacha");
                RoundManager.Instance.WeaponEffect(muzzle,muzzle+direction*1.2f,2);
            }
        }
    }
}

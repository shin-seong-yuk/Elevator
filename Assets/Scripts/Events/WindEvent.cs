using UnityEngine;
namespace ElevatorGame
{
    public sealed class WindEvent : FloorEvent
    {
        public override void StartEvent() { base.StartEvent(); RoundManager.Instance.PlayCue("wind"); }
        public override void UpdateEvent()
        {
            float pulse = .75f + Mathf.Pow(Mathf.Sin(Elapsed * 1.4f) * .5f + .5f, 2) * 1.25f;
            foreach (var b in Bodies())
            {
                // Pull the gust across the whole widened cabin and funnel it through the door.
                Vector3 direction=new Vector3(-b.position.x*.12f,0,1).normalized;
                b.AddForce(direction*(16*pulse*Power),ForceMode.Acceleration);
            }
        }
    }
}



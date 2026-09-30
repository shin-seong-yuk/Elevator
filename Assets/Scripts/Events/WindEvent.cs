using UnityEngine;
namespace ElevatorGame
{
    public sealed class WindEvent : FloorEvent
    {
        public override void StartEvent() { base.StartEvent(); RoundManager.Instance.PlayCue("wind"); }
        public override void UpdateEvent()
        {
            float pulse = .4f + Mathf.Pow(Mathf.Sin(Elapsed * 1.4f) * .5f + .5f, 2) * 1.5f;
            foreach (var b in Bodies()) b.AddForce(Vector3.forward * (10 * pulse * Power), ForceMode.Acceleration);
        }
    }
}



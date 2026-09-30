using UnityEngine;
namespace ElevatorGame
{
    public sealed class VacuumEvent : FloorEvent
    {
        public override void StartEvent() { base.StartEvent(); RoundManager.Instance.PlayCue("wind"); }
        public override void UpdateEvent()
        {
            foreach (var b in Bodies())
            {
                Vector3 direction = (new Vector3(0, 2.5f, 7) - b.position).normalized;
                b.AddForce(direction * (14 + Elapsed * .6f) * Power, ForceMode.Acceleration);
            }
        }
    }
}



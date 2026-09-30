using UnityEngine;
namespace ElevatorGame
{
    public sealed class EarthquakeEvent : FloorEvent
    {
        public override void StartEvent() { base.StartEvent(); RoundManager.Instance.PlayCue("rumble"); }
        public override void UpdateEvent()
        {
            var force = new Vector3(Mathf.Sin(Elapsed * 13)*14, Mathf.Max(0,Mathf.Sin(Elapsed*9))*5, Mathf.Cos(Elapsed*11)*8) * Power;
            foreach (var b in Bodies()) b.AddForce(force, ForceMode.Acceleration);
        }
    }
}



using UnityEngine;
namespace ElevatorGame
{
    public sealed class FakeEmptyEvent : FloorEvent
    {
        bool surprised;
        public override void UpdateEvent()
        {
            if (Elapsed < 3.5f || surprised) return;
            surprised = true;
            Spawn(0, new Vector3(0,1.2f,5), Vector3.back * 13 * Power);
            RoundManager.Instance.PlayCue("impact");
        }
    }
}



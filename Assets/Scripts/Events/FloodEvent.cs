using UnityEngine;
namespace ElevatorGame
{
    public sealed class FloodEvent : FloorEvent
    {
        NetworkProp water;
        public override void StartEvent()
        {
            base.StartEvent(); water = Spawn(6, new Vector3(0,-.1f,0), Vector3.zero);
            water.Body.isKinematic = true; RoundManager.Instance.PlayCue("wind");
        }
        public override void UpdateEvent()
        {
            float level = Mathf.Min(2, Elapsed * .35f);
            if (water) water.Body.MovePosition(new Vector3(0,level-.15f,0));
            foreach (var b in Bodies())
                if (Mathf.Abs(b.position.x) < 4.8f && Mathf.Abs(b.position.z) < 4.8f && b.position.y < level + .8f)
                    b.AddForce(Vector3.up * Mathf.Clamp((level + .8f - b.position.y)*12 - b.linearVelocity.y*2,0,20) + Vector3.forward * 9 * Power, ForceMode.Acceleration);
        }
    }
}



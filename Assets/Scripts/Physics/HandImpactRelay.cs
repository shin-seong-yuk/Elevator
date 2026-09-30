using UnityEngine;
namespace ElevatorGame
{
    public sealed class HandImpactRelay : MonoBehaviour
    {
        public PlayerGrabController grab;
        void OnCollisionEnter(Collision collision){if(grab)grab.ApplyImpact(collision.impulse.magnitude);}
    }
}

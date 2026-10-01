using UnityEngine;
namespace ElevatorGame
{
    public sealed class ElevatorDoorController : MonoBehaviour
    {
        public Rigidbody left, right;
        public float Aperture { get; private set; }
        public void SetAperture(float amount)
        {
            Aperture = Mathf.Clamp01(amount);
            Set(left, new Vector3((-1.48f - Aperture * 2.95f) * 1.5f, 1.95f, 4.5f));
            Set(right, new Vector3((1.48f + Aperture * 2.95f) * 1.5f, 1.95f, 4.5f));
        }
        void Set(Rigidbody body, Vector3 target)
        {
            if (!body) return;
            if (Application.isPlaying) body.MovePosition(target);
            else body.position = target;
        }
    }
}


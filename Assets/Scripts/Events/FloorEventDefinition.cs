using UnityEngine;

namespace ElevatorGame
{
    // Append only: serialized definitions and migration snapshots store these integer values.
    public enum EventKind { Wind, BowlingBall, Dinosaur, ShoppingCart, Gorilla, Chicken, GiantHand, Flood, Vacuum, Earthquake, EmptyFloor, FakeEmpty, Stranger, FlyingFish, Tank, Ufo, GrandPiano }

    [CreateAssetMenu(menuName = "Elevator/Floor Event")]
    public sealed class FloorEventDefinition : ScriptableObject
    {
        public EventKind kind;
        public string eventName;
        [TextArea] public string hint;
        [Min(0)] public float weight = 1;
        public float duration = 12;
        public float difficulty = 1;
        public int minimumFloor = 1;
        public int maximumFloor = 999;
        public bool canCombine = true;
        public bool persistent;
        public FloorEvent prefab;
    }
}



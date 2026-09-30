using System.Collections.Generic;
using UnityEngine;
namespace ElevatorGame
{
    public abstract class FloorEvent : MonoBehaviour, IAIHazard
    {
        public FloorEventDefinition Definition { get; private set; }
        protected FloorEventManager Manager;
        protected float Elapsed;
        protected float Power;
        protected bool Running;
        protected readonly List<NetworkProp> Spawned = new();
        public void Configure(FloorEventManager manager, FloorEventDefinition definition, int floor)
        { Manager = manager; Definition = definition; Power = Mathf.Min(2.6f, 1 + (floor - 1) * .055f) * definition.difficulty; }
        public virtual void PrepareEvent() { }
        public virtual void StartEvent() { Running = true; }
        public virtual void UpdateEvent() { }
        public virtual Vector3 GetDangerDirection() => Definition && (Definition.kind==EventKind.Wind||Definition.kind==EventKind.Vacuum||Definition.kind==EventKind.Flood) ? Vector3.forward : Vector3.back;
        public virtual float GetDangerStrength()=>Running&&Definition&&Definition.kind!=EventKind.EmptyFloor&&Definition.kind!=EventKind.FakeEmpty?Power:0;
        public virtual Vector3 GetRecommendedSafeDirection(Vector3 position)
        {
            if(Definition.kind==EventKind.Wind||Definition.kind==EventKind.Vacuum||Definition.kind==EventKind.Flood)return Vector3.back;
            if(Spawned.Count>0&&Spawned[0]){Vector3 away=position-Spawned[0].transform.position;away.y=0;return away.normalized;}
            return Vector3.zero;
        }
        protected virtual void FixedUpdate() { if (Running && !(SteamSession.Instance&&SteamSession.Instance.Migrating)) { Elapsed += Time.fixedDeltaTime; UpdateEvent(); } }
        public virtual void EndEvent() { Running = false; }
        public void Cleanup(bool all)
        {
            EndEvent();
            foreach (var p in Spawned)
                if (p && p.IsActive && (all || !p.persistent || !PlayerEliminationController.Inside(p.transform.position))) p.Remove();
            Spawned.Clear();
        }
        protected NetworkProp Spawn(int prefab, Vector3 position, Vector3 velocity, bool persistent = false)
        {
            var p = Manager.SpawnProp(prefab, position, velocity, persistent);
            Spawned.Add(p); return p;
        }
        protected IEnumerable<Rigidbody> Bodies()
        {
            foreach (var p in RoundManager.Players()) if (p.Alive.Value) yield return p.Body;
            foreach (var p in FindObjectsByType<NetworkProp>(FindObjectsSortMode.None)) if (p.IsActive && !p.Body.isKinematic) yield return p.Body;
        }
        protected PlayerController Nearest(Vector3 point, float range = 12)
        {
            PlayerController chosen = null;
            foreach (var p in RoundManager.Players())
            {
                float d = Vector3.Distance(point, p.transform.position);
                if (p.Alive.Value && d < range) { chosen = p; range = d; }
            }
            return chosen;
        }
        protected void Blast(Vector3 center, float radius, Vector3 force)
        {
            foreach (var p in RoundManager.Players()) if (p.Alive.Value && Vector3.Distance(center, p.transform.position) < radius) p.Knock(force * Power);
            RoundManager.Instance.PlayCue("impact");
        }
    }
}





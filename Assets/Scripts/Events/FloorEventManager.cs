using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ElevatorGame
{
    public sealed class FloorEventManager : MonoBehaviour
    {
        public FloorEventDefinition[] definitions;
        public int NextMigrationKey {get;set;}
        public NetworkProp[] propPrefabs; // Ball, Cart, Dino, Gorilla, Chicken, Hand, Water, Crate
        readonly List<FloorEvent> active = new();
        public IEnumerable<IAIHazard> Hazards => active.Where(e=>e).Cast<IAIHazard>();
        System.Random random = new();
        EventKind? forced;
        bool forceCombined;
        EventKind previous = EventKind.EmptyFloor;
        public float Duration { get; private set; }
        public string Title { get; private set; }
        public int RandomSeed {get;private set;} public int RandomCalls {get;private set;} public EventKind PreviousEvent=>previous;
        public void SetSeed(int seed) {RandomSeed=seed;RandomCalls=0;random=new System.Random(seed);}
        double NextUnit(){RandomCalls++;return random.NextDouble();}
        public float Random(float min, float max) => min + (float)NextUnit() * (max - min);
        public int RandomInt(int min, int max) => min+(int)(NextUnit()*(max-min));
        public void Force(EventKind kind, bool combined = false) { forced = kind; forceCombined = combined; }
        public void Prepare(int floor)
        {
            var eligible = definitions.Where(d => d && d.weight > 0 && floor >= d.minimumFloor && floor <= d.maximumFloor).ToList();
            var first = forced.HasValue ? definitions.First(d => d.kind == forced.Value) : Pick(eligible.Where(d => d.kind != previous).ToList());
            Add(first, floor); previous = first.kind;
            if ((floor >= 20 && NextUnit() < .45 || forceCombined) && first.canCombine)
            {
                var second = Pick(eligible.Where(d => d != first && d.canCombine).ToList());
                if (second) Add(second, floor);
            }
            forced = null; forceCombined = false;
            Duration = active.Max(e => e.Definition.duration);
            Title = string.Join(" + ", active.Select(e => e.Definition.eventName));
        }
        FloorEventDefinition Pick(List<FloorEventDefinition> choices)
        {
            if (choices.Count == 0) return definitions.FirstOrDefault();
            double value = NextUnit() * choices.Sum(d => d.weight);
            foreach (var d in choices) { value -= d.weight; if (value <= 0) return d; }
            return choices[^1];
        }
        void Add(FloorEventDefinition d, int floor)
        {
            var e = Instantiate(d.prefab, transform); e.Configure(this, d, floor); e.PrepareEvent(); active.Add(e);
        }
        public void Begin() { foreach (var e in active) e.StartEvent(); }
        public void End() { foreach (var e in active) e.EndEvent(); }
        public NetworkProp SpawnProp(int type, Vector3 position, Vector3 velocity, bool persistent)
        {
            var p = Instantiate(propPrefabs[type], position, Quaternion.identity);
            p.PrefabIndex=type;p.MigrationKey=NextMigrationKey++;p.persistent = persistent; if(GameSession.Offline)p.InitializeOffline();else p.NetworkObject.Spawn();
            p.Body.linearVelocity = velocity;
            return p;
        }
        public MigrationEvent[] CaptureMigration(MigrationContext context)=>active.Where(e=>e).Select(e=>new MigrationEvent{kind=(int)e.Definition.kind,fields=context.CaptureFields(e)}).ToArray();
        public void RestoreMigration(MigrationEvent[] saved,MigrationContext context,int floor,int seed,int calls,EventKind previousKind)
        {
            active.Clear();SetSeed(seed);for(int i=0;i<calls;i++)NextUnit();previous=previousKind;
            foreach(var state in saved){var definition=definitions.First(d=>(int)d.kind==state.kind);var e=Instantiate(definition.prefab,transform);e.Configure(this,definition,floor);context.RestoreFields(e,state.fields);active.Add(e);}
            Duration=active.Count>0?active.Max(e=>e.Definition.duration):0;Title=string.Join(" + ",active.Select(e=>e.Definition.eventName));
        }
        public void Cleanup(bool all)
        {
            foreach (var e in active) if (e) { e.Cleanup(all); Destroy(e.gameObject); }
            active.Clear();
            var props = FindObjectsByType<NetworkProp>(FindObjectsSortMode.None);
            var retained = props.Where(p => p.IsActive && p.persistent && !p.GetComponent<WeaponPickup>()).ToArray();
            int excess = Mathf.Max(0, retained.Length - 14);
            foreach (var p in props)
            {
                if (!p || !p.IsActive || p.GetComponent<WeaponPickup>()) continue;
                if (all || !p.persistent || !PlayerEliminationController.Inside(p.transform.position) || (p.persistent && excess-- > 0)) p.Remove();
            }
        }
    }
}







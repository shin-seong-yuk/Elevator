using System.Collections.Generic;
using UnityEngine;

namespace ElevatorGame
{
    public sealed class PlayerEliminationController : MonoBehaviour
    {
        readonly Dictionary<PlayerController, float> outsideSince = new();
        public static bool Inside(Vector3 p) => Mathf.Abs(p.x) <= 5.025f && p.z >= -5.025f && p.z <= 5.175f && p.y >= -.7f && p.y <= 5.5f;

        // Traverse an undirected live-joint graph. A floating cycle does NOT count as safety.
        // Incoming grabs matter: someone inside can save a passive falling player.
        public static bool Supported(PlayerController start, PlayerController[] players)
        {
            var visited = new HashSet<Rigidbody>();
            var queue = new Queue<Rigidbody>();
            queue.Enqueue(start.Body);
            while (queue.Count > 0)
            {
                var body = queue.Dequeue();
                if (!body || !visited.Add(body)) continue;
                var actor = body.GetComponent<PlayerController>();
                if (actor && actor.Alive.Value && Inside(body.position)) return true;
                if (actor && actor.Alive.Value && actor.Grab.HasStructuralAnchor()) return true;
                if (actor && actor.Alive.Value) foreach (var target in actor.Grab.ConnectedBodies()) queue.Enqueue(target);
                foreach (var p in players)
                {
                    if (!p || !p.Alive.Value) continue;
                    foreach (var target in p.Grab.ConnectedBodies()) if (target == body) queue.Enqueue(p.Body);
                }
            }
            return false;
        }
        public void Check(PlayerController[] players)
        {
            foreach (var p in players)
            {
                if (!p || !p.Alive.Value) continue;
                if (Supported(p, players)) { outsideSince.Remove(p); continue; }
                if (!outsideSince.TryGetValue(p, out float since)) outsideSince[p] = Time.time;
                else if (Time.time - since > .65f) Eliminate(p);
            }
        }
        public void Eliminate(PlayerController p)
        {
            if (!p || !p.IsAuthority || !p.Alive.Value) return;
            if(p.ControlledLocally)GameSession.Instance?.RecordSurvival();
            p.Alive.Value = false; p.Grab.ReleaseAll(); p.HeldHands.Value = 0;
            RoundManager.Instance.PlayCue("out");
        }
        public void ResetState() => outsideSince.Clear();
    }
}


# Elevator project guidance

This is a Unity 6000.3.19f1 URP project. Keep the original SampleScene and template assets.
Gameplay code lives in Assets/Scripts. Generated assets live in Assets/Elevator.
Use the connected Unity official MCP tools to inspect, compile, run and test the project; verify Application.dataPath before modifying a scene.
Do not install a duplicate Unity bridge while the existing official connection works.
The playable entry scene is Assets/Elevator/Scenes/Elevator.unity.
Host authority is required: clients send input; only the host changes physics, joint links, events, elimination and results.
A hanging player survives only when the live grab graph reaches an inside living player or a marked cabin anchor. Incoming grabs count; unsupported cycles do not.
Elevator/Build Playable Project regenerates the generated scene and prefabs. Preserve manual edits to these assets before rerunning it.
Use Elevator/Run Gameplay Smoke Tests for relevant gameplay changes. Reports are written under TestResults.
Read Docs/README.ko.md for controls, networking limitations and extension points.
Single-player gameplay uses GameSession local state and must never start a host merely to simulate offline play. Keep event and motor implementations shared. Human control uses LMB for both hands; do not restore independent left/right mouse controls. Hands use free-angular grip joints and flexible shoulder connections; do not attach fixed whole-body joints for ordinary grabbing.
Weapon pickups are spawned once per floor, expire when current floor minus birth floor reaches four, and are excluded from event-only cleanup. Round reset clears them explicitly. Server authority owns pickup, throw, damage and projectile simulation.
Cabin damage uses the RoundManager BrokenPanels bitmask: floor 0-15, walls 16-39. Disable colliders and visuals together, including attached handrails; restore on a new round. Preserve unique stable section indices in the builder.
Run Elevator/Test Weapons Grip And Destruction for grip, weapon lifetime/use/throw, wall/floor holes and travel combat changes. Grip strength is intentionally hidden from the HUD.

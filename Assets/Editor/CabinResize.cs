using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ElevatorGame.Editor
{
    // Apply the same horizontal footprint to generated and already customized cabins.
    public static class CabinResize
    {
        const float Factor = 1.5f;
        const string PrefabPath = "Assets/Elevator/Prefabs/Elevator/Cabin.prefab";

        [MenuItem("Elevator/Widen Existing Cabin")]
        public static void WidenExisting()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (!File.Exists(Path.Combine(Application.dataPath, "Scripts/Core/RoundManager.cs")))
                throw new InvalidOperationException("Open the Elevator project first: " + Application.dataPath);
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Elevator/Scenes/Elevator.unity")
                throw new InvalidOperationException("Open Assets/Elevator/Scenes/Elevator.unity first.");
            var cabin = GameObject.Find("Cabin");
            if (!cabin) throw new InvalidOperationException("Cabin is missing.");
            ApplyToScene(cabin);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                WidenCabin(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            WidenWaterPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("ELEVATOR_CABIN_WIDENED: 9.75 x 9.75 footprint, original height preserved.");
        }

        public static void ApplyToScene(GameObject cabin)
        {
            WidenCabin(cabin);
            var lobby = GameObject.Find("Odd Hours Hotel / Floor Lobby");
            if (lobby) WidenLobbyEntrance(lobby.transform);
        }

        static void WidenCabin(GameObject cabin)
        {
            var zone = cabin.transform.Find("SafeZone");
            if (!zone) throw new InvalidOperationException("SafeZone is missing.");
            if (zone.localScale.x > 1.4f) return; // safe to rerun after an interrupted install
            foreach (Transform child in cabin.transform)
            {
                var p = child.localPosition;
                child.localPosition = new Vector3(p.x * Factor, p.y, p.z * Factor);
                if (child.GetComponent<TextMesh>()) continue; // retain readable display text
                var s = child.localScale;
                child.localScale = new Vector3(s.x * Factor, s.y, s.z * Factor);
                EditorUtility.SetDirty(child);
            }
        }

        static void WidenLobbyEntrance(Transform lobby)
        {
            Material wallMaterial = null;
            foreach (Transform child in lobby)
            {
                var p = child.localPosition;
                var s = child.localScale;
                switch (child.name)
                {
                    case "Corridor floor": SetXZ(child, 0, 16.1125f, s.x, 22.475f); break;
                    case "Carpet runner": SetXZ(child, 0, 16.075f, s.x, 22.35f); break;
                    case "Carpet edging": SetXZ(child, p.x, 16.075f, s.x, 22.35f); break;
                    case "Entry surround":
                        wallMaterial = child.GetComponent<Renderer>().sharedMaterial;
                        SetXZ(child, Mathf.Sign(p.x) * 5.7f, 4.5f, 2.4f, s.z);
                        p = child.localPosition; p.y = 2.8f; child.localPosition = p;
                        s = child.localScale; s.y = 5.6f; child.localScale = s;
                        break;
                    case "Entry plaque":
                    case "Service sign":
                        p.x = Mathf.Sign(p.x) * (child.name == "Entry plaque" ? 6f : 5.85f);
                        p.z = 4.82f;
                        child.localPosition = p;
                        var label = child.GetComponent<TextMesh>();
                        if (label) label.characterSize = child.name == "Entry plaque" ? .09f : .045f;
                        break;
                    case "Lobby far wall":
                    case "Lobby side wall":
                        p.y = 2.8f; child.localPosition = p;
                        s.y = 5.6f; child.localScale = s;
                        break;
                    case "Floor seam":
                        if (p.z < 4.875f) { p.z = 4.91f; child.localPosition = p; }
                        break;
                }
                EditorUtility.SetDirty(child);
            }
            // The expanded door header ends below the hotel ceiling. Seal the full-width
            // space above it so the facade is solid from both the hall and the cabin.
            if (!lobby.Find("Entry lintel"))
            {
                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "Entry lintel";
                lintel.layer = 8;
                lintel.transform.SetParent(lobby, false);
                lintel.transform.localPosition = new Vector3(0, 5.01f, 4.5f);
                lintel.transform.localScale = new Vector3(10.1f, 1.18f, .5f);
                lintel.GetComponent<Renderer>().sharedMaterial = wallMaterial;
                lintel.AddComponent<GrabAnchor>();
            }
        }

        static void SetXZ(Transform t, float x, float z, float width, float depth)
        {
            var p = t.localPosition; p.x = x; p.z = z; t.localPosition = p;
            var s = t.localScale; s.x = width; s.z = depth; t.localScale = s;
        }

        static void WidenWaterPrefab()
        {
            const string path = "Assets/Elevator/Prefabs/Props/FloodSurface.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var water = prefab.transform.Find("Water");
                if (!water) throw new InvalidOperationException("Flood water visual is missing.");
                var size = water.localScale; size.x = size.z = 9.45f; water.localScale = size;
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
    }
}

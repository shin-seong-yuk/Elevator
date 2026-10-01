using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ElevatorGame.Editor
{
    public static class HotelEntranceFix
    {
        const string ScenePath="Assets/Elevator/Scenes/Elevator.unity";
        const string CabinPath="Assets/Elevator/Prefabs/Elevator/Cabin.prefab";
        const string RunnerPath="Assets/Elevator/Prefabs/Props/LatePassenger.prefab";

        [MenuItem("Elevator/Fix Hotel Entrance And Runner")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            if(!string.Equals(Path.GetFullPath(Application.dataPath),Path.GetFullPath("Assets"),StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Open the Elevator project first: "+Application.dataPath);
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=ScenePath)throw new InvalidOperationException("Open "+ScenePath+" first.");
            var cabin=GameObject.Find("Cabin");
            var lobby=GameObject.Find("Odd Hours Hotel / Floor Lobby");
            var camera=Camera.main;
            if(!cabin||!lobby||!camera)throw new InvalidOperationException("Cabin, lobby or main camera is missing.");

            Undo.RegisterFullObjectHierarchyUndo(cabin,"Clear doorway overlaps");
            Undo.RegisterFullObjectHierarchyUndo(lobby,"Clear doorway overlaps");
            Undo.RecordObject(camera.transform,"Reveal elevator on main menu");
            FixCabin(cabin);
            FixLobby(lobby.transform);
            camera.transform.position=CameraRig.MenuPosition;
            camera.transform.rotation=Quaternion.LookRotation(CameraRig.MenuFocus-CameraRig.MenuPosition);
            EditorUtility.SetDirty(camera.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var prefab=PrefabUtility.LoadPrefabContents(CabinPath);
            try{FixCabin(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,CabinPath);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var runner=PrefabUtility.LoadPrefabContents(RunnerPath);
            try
            {
                var body=runner.GetComponent<Rigidbody>();
                body.centerOfMass=new Vector3(0,-.55f,0);
                EditorUtility.SetDirty(body);
                PrefabUtility.SaveAsPrefabAsset(runner,RunnerPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(runner);}
            AssetDatabase.SaveAssets();
            Debug.Log("HOTEL_ENTRANCE_FIXED: menu camera, both doorway sides and runner balance.");
        }

        public static void FixCabin(GameObject cabin)
        {
            foreach(Transform child in cabin.transform)
            {
                if(child.name=="Door frame")
                {
                    var p=child.localPosition;p.x=Mathf.Sign(p.x)*4.8f;child.localPosition=p;
                }
            }
            foreach(var panel in cabin.GetComponentsInChildren<CabinPanel>())
            {
                if(panel.index!=30&&panel.index!=31&&panel.index!=38&&panel.index!=39)continue;
                // Stop the last side-wall tile behind the door jamb instead of letting
                // two solid surfaces occupy the same space on either side of the door.
                var parts=panel.GetComponentsInChildren<Transform>();
                var positions=new Vector3[parts.Length];
                var sizes=new Vector3[parts.Length];
                for(int i=0;i<parts.Length;i++){positions[i]=parts[i].position;sizes[i]=parts[i].lossyScale;}
                for(int i=0;i<parts.Length;i++)
                {
                    var part=parts[i];
                    if(part!=panel.transform&&part.name!="Wall panel trim"&&part.name!="Side handrail"&&part.name!="Wall crown")continue;
                    float back=positions[i].z-sizes[i].z*.5f;
                    float front=positions[i].z+sizes[i].z*.5f;
                    if(front<4.2f)continue;
                    float depth=4.13f-back;
                    var p=positions[i];p.z=back+depth*.5f;part.position=p;
                    var local=part.localScale;local.z*=depth/part.lossyScale.z;part.localScale=local;
                    EditorUtility.SetDirty(part);
                }
            }
        }

        static void FixLobby(Transform lobby)
        {
            foreach(Transform child in lobby)
            {
                if(child.name!="Entry surround")continue;
                var p=child.localPosition;p.x=Mathf.Sign(p.x)*5.925f;child.localPosition=p;
                var s=child.localScale;s.x=1.85f;child.localScale=s;
                EditorUtility.SetDirty(child);
            }
        }
    }
}

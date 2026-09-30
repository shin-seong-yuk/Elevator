using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ElevatorGame.Editor
{
    public static class CabinSeamFix
    {
        [MenuItem("Elevator/Fix Cabin Seams")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first.");
            var cabin=GameObject.Find("Cabin");if(!cabin)throw new System.InvalidOperationException("Open the Elevator scene.");
            Undo.RegisterFullObjectHierarchyUndo(cabin,"Close cabin seams");Fix(cabin);EditorSceneManager.MarkSceneDirty(cabin.scene);EditorSceneManager.SaveScene(cabin.scene);
            const string path="Assets/Elevator/Prefabs/Elevator/Cabin.prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
            try{Fix(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
            AssetDatabase.SaveAssets();Debug.Log("CABIN_SEAMS_FIXED");
        }
        static void Fix(GameObject root)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>())
            {
                Vector3 size=t.lossyScale;
                if(t.name.StartsWith("Breakable floor ")){size.x=size.z=1.625f;Resize(t,size);}
                else if(t.name=="Left Door"||t.name=="Right Door"){size.x=2.96f;Resize(t,size);}
                else if(t.name=="Wall panel trim"||t.name=="Wall crown"||t.name=="Back handrail"||t.name=="Side handrail")
                {if(size.x>size.z)size.x=1.625f;else size.z=1.625f;Resize(t,size);}
            }
        }
        static void Resize(Transform t,Vector3 size){Vector3 old=t.lossyScale,local=t.localScale;t.localScale=new Vector3(local.x*size.x/old.x,local.y*size.y/old.y,local.z*size.z/old.z);EditorUtility.SetDirty(t);}
    }
}

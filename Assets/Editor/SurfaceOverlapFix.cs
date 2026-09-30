using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ElevatorGame.Editor
{
    public static class SurfaceOverlapFix
    {
        [MenuItem("Elevator/Fix Overlapping Surfaces")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first.");
            var cabin=GameObject.Find("Cabin");if(!cabin)throw new System.InvalidOperationException("Open Elevator scene.");
            Undo.RegisterFullObjectHierarchyUndo(cabin,"Remove coplanar surfaces");FixCabin(cabin);
            EditorSceneManager.MarkSceneDirty(cabin.scene);EditorSceneManager.SaveScene(cabin.scene);
            const string path="Assets/Elevator/Prefabs/Elevator/Cabin.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try{FixCabin(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
            AssetDatabase.SaveAssets();
        }
        public static void FixCabin(GameObject cabin)
        {
            foreach(var t in cabin.GetComponentsInChildren<Transform>())
            {
                if(t.name=="Header")Resize(t,2,.54f); // Front surface clears ceiling and side wall caps.
                var panel=t.GetComponentInParent<CabinPanel>();
                if(!panel||panel.index<24||panel.index>39)continue;
                bool firstColumn=(panel.index-24)%8<2;
                if(!firstColumn)continue;
                // Side strips meet the front edge of back strips instead of crossing them.
                float start=t.name=="Side handrail"?-2.81f:t.name=="Wall panel trim"?-2.975f:t.name=="Wall crown"?-2.955f:float.NaN;
                if(float.IsNaN(start))continue;
                const float end=-1.625f;var p=t.position;p.z=(start+end)*.5f;t.position=p;Resize(t,2,end-start);
            }
        }
        static void Resize(Transform t,int axis,float size)
        {var s=t.localScale;s[axis]*=size/t.lossyScale[axis];t.localScale=s;EditorUtility.SetDirty(t);}
    }
}

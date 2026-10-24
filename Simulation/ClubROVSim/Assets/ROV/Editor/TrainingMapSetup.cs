using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ClubROV.Simulation.Editor
{
    public static class TrainingMapSetup
    {
        public static string Install()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
            var vehicle=Object.FindAnyObjectByType<RovDynamics>();
            if(vehicle.GetComponent<RovTrainingMaps>()!=null) return "Maps already installed";
            var original=GameObject.Find("Training course");
            var gates=new Transform[3]; for(int i=0;i<3;i++) gates[i]=original.transform.Find("Gate "+(i+1));
            var hover=original.transform.Find("Cyan hover target — heading 000");
            var dock=original.transform.Find("Green dock target — heading 180");
            var yellow=AssetDatabase.LoadAssetAtPath<Material>("Assets/ROV/Generated/Gate_yellow.mat");
            var cyan=AssetDatabase.LoadAssetAtPath<Material>("Assets/ROV/Generated/Course_cyan.mat");
            var layouts=new RovMapLayout[4]; layouts[0]=new RovMapLayout{root=original,gates=gates,hover=hover,dock=dock};
            var pre=new GameObject("Map - Gate and pillar");
            var bar=new GameObject("Gate bar 2m at 1m depth").transform; bar.SetParent(pre.transform); bar.position=new Vector3(0,-1,-5);
            Cube(bar,"Horizontal gate",Vector3.zero,new Vector3(2,0.06f,0.06f),yellow);
            Cube(bar,"Left anchor",new Vector3(-1,-1.25f,0),new Vector3(0.04f,2.5f,0.04f),yellow);
            Cube(bar,"Right anchor",new Vector3(1,-1.25f,0),new Vector3(0.04f,2.5f,0.04f),yellow);
            var pillar=GameObject.CreatePrimitive(PrimitiveType.Cylinder); pillar.name="Vertical marker - floor to surface";
            pillar.transform.SetParent(pre.transform); pillar.transform.position=new Vector3(0,-1.7f,5); pillar.transform.localScale=new Vector3(0.16f,1.8f,0.16f); pillar.GetComponent<Renderer>().sharedMaterial=cyan;
            layouts[1]=new RovMapLayout{root=pre,gates=new[]{bar},marker=pillar.transform};
            layouts[2]=new RovMapLayout{root=new GameObject("Map - Empty pool"),gates=new Transform[0]};
            var slalom=new GameObject("Map - Slalom"); var sg=new Transform[5];
            for(int i=0;i<5;i++) {sg[i]=Object.Instantiate(gates[0],slalom.transform); sg[i].name="Slalom gate "+(i+1);sg[i].position=new Vector3(i%2==0?-2:2,-1.3f-(i%3)*0.4f,1+i*1.8f);}
            var sh=Object.Instantiate(hover,slalom.transform); sh.position=new Vector3(-2,-1.5f,10.5f);
            var sd=Object.Instantiate(dock,slalom.transform);
            layouts[3]=new RovMapLayout{root=slalom,gates=sg,hover=sh,dock=sd};
            // Keep shared floor grid visible independently of the active course.
            var grid=new GameObject("Shared pool grid");
            for(int i=original.transform.childCount-1;i>=0;i--) {var t=original.transform.GetChild(i);if(t.name.StartsWith("Grid ")) t.SetParent(grid.transform,true);}
            for(int i=1;i<4;i++) layouts[i].root.SetActive(false);
            vehicle.gameObject.AddComponent<RovTrainingMaps>().Configure(layouts);
            EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            return "Four training maps installed";
        }
        private static void Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=position;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}

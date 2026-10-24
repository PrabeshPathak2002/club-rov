using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ClubROV.Simulation.Editor
{
    // Normalized station centers traced from handbook pp80/82, not surveyed coordinates.
    public static class RoboSub2026Setup
    {
        private const float Floor=-2.1f;
        private static Material white, red, black, orange, blue, pink, green, grey;
        public static string Install()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            var vehicle=UnityEngine.Object.FindAnyObjectByType<RovDynamics>();
            if(vehicle==null || vehicle.gameObject.scene.path!="Assets/Scenes/PoolTraining.unity") throw new InvalidOperationException("Open PoolTraining.");
            var maps=vehicle.GetComponent<RovTrainingMaps>();var old=maps.CopyLayouts();
            if(old.Length!=4) throw new InvalidOperationException("Expected the original four layouts; preserve existing course data.");
            if(!AssetDatabase.IsValidFolder("Assets/ROV/Generated/RoboSub2026")) AssetDatabase.CreateFolder("Assets/ROV/Generated","RoboSub2026");
            LoadMaterials();
            var environment=new GameObject("Original training pool environment");
            foreach(var name in new[]{"Pool floor","Port wall","Starboard wall","Far wall","Near wall","Shared pool grid"})
            {var item=GameObject.Find(name);if(item==null) throw new InvalidOperationException("Missing "+name);item.transform.SetParent(environment.transform,true);}
            var layouts=new List<RovMapLayout>(old);foreach(var layout in layouts)layout.environment=environment;
            layouts.Add(new RovMapLayout{root=Build(false),gates=Array.Empty<Transform>()});
            layouts.Add(new RovMapLayout{root=Build(true),gates=Array.Empty<Transform>()});
            maps.Configure(layouts.ToArray());
            foreach(var layout in layouts.GetRange(4,2))layout.root.SetActive(false);
            EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            return "Installed handbook practice/finals layouts and separate 2.1m-deep arenas.";
        }
        private static void LoadMaterials()
        {
            white=Mat("White PVC",new Color(.92f,.94f,.9f));red=Mat("Red",new Color(.85f,.025f,.03f));black=Mat("Black",new Color(.025f,.03f,.04f));
            orange=Mat("Orange path",new Color(1,.32f,.015f));blue=Mat("Pool blue",new Color(.14f,.54f,.69f));pink=Mat("Rescue pink",new Color(.95f,.08f,.4f));green=Mat("Pinger green",new Color(.05f,.7f,.18f));grey=Mat("Rope grey",new Color(.3f,.35f,.35f));
        }
        public static string RebuildGeneratedArenas()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
            var maps=UnityEngine.Object.FindAnyObjectByType<RovTrainingMaps>();var layouts=maps.CopyLayouts();
            if(layouts.Length!=6 || layouts[4].root.name!="Map - RoboSub 2026 practice" || layouts[5].root.name!="Map - RoboSub 2026 finals")throw new InvalidOperationException("Unexpected generated arenas; preserve scene.");
            LoadMaterials();
            for(int i=4;i<6;i++)
            {
                Undo.DestroyObjectImmediate(layouts[i].root);layouts[i].root=Build(i==5);layouts[i].root.SetActive(false);
            }
            maps.Configure(layouts);EditorSceneManager.MarkSceneDirty(maps.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();return "Rebuilt only the two generated handbook arenas.";
        }
        public static string Check()
        {
            if(!EditorApplication.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            var vehicle=UnityEngine.Object.FindAnyObjectByType<RovDynamics>();var maps=vehicle.GetComponent<RovTrainingMaps>();var settings=vehicle.GetComponent<RovTrainingSettings>();var course=vehicle.GetComponent<RovTrainingCourse>();
            var results=new List<string>();bool record=course.RecordResults;course.RecordResults=false;
            void Assert(string name,bool pass) {results.Add((pass?"PASS: ":"FAIL: ")+name);}
            try
            {
                Assert("Six named layouts",maps.CopyLayouts().Length==6 && RovTrainingMaps.Names.Length==6);
                for(int index=4;index<=5;index++)
                {
                    settings.SelectMap(index);var layout=maps.CopyLayouts()[index];var all=layout.root.GetComponentsInChildren<Transform>();
                    int Count(Func<Transform,bool> match) {int count=0;foreach(var t in all)if(match(t))count++;return count;}
                    Assert("Map "+index+" disables original environment",!maps.CopyLayouts()[0].environment.activeSelf);
                    Assert("Map "+index+" activates only selected arena",layout.root.activeSelf && !maps.CopyLayouts()[index==4?5:4].root.activeSelf);
                    Assert("Map "+index+" has expected gate count",Count(t=>t.name.EndsWith("Begin Assessment"))==(index==4?5:3));
                    Assert("Map "+index+" has three-row slaloms",Count(t=>t.name=="Slalom pole")==9*(index==4?5:3));
                    Assert("Map "+index+" has three octagons",Count(t=>t.name=="Octagon edge")==24);
                    Assert("Map "+index+" has three deploy stations",Count(t=>t.name.EndsWith(" Deploy"))==3);
                    Assert("Map "+index+" has only gate/slalom path segments",Count(t=>t.name.EndsWith(" path"))==(index==4?10:6));
                    var floor=layout.root.transform.Find("Arena floor 50 x 22.86 venue footprint").GetComponent<Collider>();
                    Assert("Map "+index+" floor is 2.1m below surface",Mathf.Abs(floor.bounds.max.y+2.1f)<.001f);
                    Assert("Map "+index+" exploration is unscored",!course.Configured && course.Objective.Contains("ROBOSUB 2026"));
                    settings.Current.Validate();Assert("Map "+index+" start is valid",true);
                    settings.Apply(2);Assert("Map "+index+" retains layout with murky preset",maps.Current==index && settings.Current.map==index && settings.Current.visibility==3);
                    settings.Apply(0);
                }
                settings.SelectMap(4);var old=settings.Current;old.startPosition.y=-2.5f;bool rejected=false;try{old.Validate();}catch(ArgumentException){rejected=true;}Assert("Shallow-pool scenarios reject deep starts",rejected);
                vehicle.ExternalPhysics=true;bool blocked=false;try{settings.SelectMap(5);}catch(InvalidOperationException){blocked=true;}Assert("SITL cannot change arena",blocked && maps.Current==4);vehicle.ExternalPhysics=false;
                settings.SelectMap(0);Assert("Original pool environment restores",maps.CopyLayouts()[0].environment.activeSelf && !maps.CopyLayouts()[4].root.activeSelf && !maps.CopyLayouts()[5].root.activeSelf);
            }
            finally {vehicle.ExternalPhysics=false;settings.SelectMap(0);settings.Apply(0);course.RecordResults=record;}
            string report=string.Join("\n",results);System.IO.File.WriteAllText("Docs/RoboSub2026CheckResults.txt",report);if(results.Exists(s=>s.StartsWith("FAIL")))throw new Exception(report);return report;
        }
        private static GameObject Build(bool finals)
        {
            var root=new GameObject(finals?"Map - RoboSub 2026 finals":"Map - RoboSub 2026 practice");var t=root.transform;
            Cube(t,"Arena floor 50 x 22.86 venue footprint",new Vector3(0,Floor-.15f,0),new Vector3(50.4f,.3f,23.26f),blue);
            Cube(t,"West wall",new Vector3(-25.15f,-1.05f,0),new Vector3(.3f,2.1f,23.26f),white);
            Cube(t,"East wall",new Vector3(25.15f,-1.05f,0),new Vector3(.3f,2.1f,23.26f),white);
            Cube(t,"North wall",new Vector3(0,-1.05f,11.58f),new Vector3(50,2.1f,.3f),white);
            Cube(t,"South wall",new Vector3(0,-1.05f,-11.58f),new Vector3(50,2.1f,.3f),white);
            for(int x=-24;x<=24;x+=2) Cube(t,"Lane marking",new Vector3(x,Floor+.006f,0),new Vector3(.035f,.006f,22.86f),black,false);
            foreach(float x in finals?new[]{0f,13.8f}:new[]{-12f,0f,13.8f}) Cube(t,"Surface field boundary",new Vector3(x,.015f,0),new Vector3(.045f,.025f,22.86f),red,false);
            if(!finals)for(float x=-12;x<13.8f;x+=2)Cube(t,"Practice half-field divider",new Vector3(x,.02f,0),new Vector3(1,.025f,.04f),orange,false);
            Label(t,finals?"A + B FINALS":"A",new Vector3(-20,.1f,12.1f),white,.035f);
            if(!finals){Label(t,"B1",new Vector3(-6,.1f,12.1f),white,.035f);Label(t,"B2",new Vector3(-6,.1f,-12.1f),white,.035f);}
            Label(t,"C1",new Vector3(6,.1f,12.1f),white,.035f);Label(t,"C2",new Vector3(6,.1f,-12.1f),white,.035f);Label(t,"D",new Vector3(20,.1f,12.1f),white,.035f);
            if(finals) Field(t,"AB",new Vector2(.095f,.17f),new Vector2(.22f,.40f),new Vector2(.40f,.49f),new Vector2(.20f,.72f),new Vector2(.40f,.85f),150);
            else
            {
                Field(t,"A",new Vector2(.095f,.16f),new Vector2(.06f,.40f),new Vector2(.205f,.52f),new Vector2(.20f,.72f),new Vector2(.08f,.85f),180);
                Field(t,"B1",new Vector2(.395f,.175f),new Vector2(.31f,.35f),new Vector2(.46f,.43f),null,null,180);
                Field(t,"B2",new Vector2(.395f,.85f),new Vector2(.38f,.72f),new Vector2(.30f,.60f),null,null,0);
            }
            Field(t,"C1",new Vector2(.625f,.17f),new Vector2(.685f,.33f),new Vector2(.56f,.43f),null,null,180);
            Torpedoes(Group(t,"C2 Deploy",Point(new Vector2(.56f,.65f),-1.05f),25));
            Octagon(Group(t,"C2 Restore",Point(new Vector2(.70f,.86f),0),0));
            Field(t,"D",new Vector2(.845f,.17f),new Vector2(.89f,.36f),new Vector2(.81f,.51f),new Vector2(.845f,.72f),new Vector2(.93f,.86f),180);
            RoboSub2026Artwork.Apply(root);
            return root;
        }
        private static void Field(Transform root,string name,Vector2 gate,Vector2 slalom,Vector2 bins,Vector2? deploy,Vector2? restore,float heading)
        {
            Gate(Group(root,name+" Begin Assessment",Point(gate,-.15f),heading));
            Slalom(Group(root,name+" Avoid Debris",Point(slalom,-1.1f),heading));
            Bins(Group(root,name+" Recon",Point(bins,Floor),25));
            Path(root,name+" Gate to slalom path",gate,slalom);Path(root,name+" Slalom to bins path",slalom,bins);
            if(deploy.HasValue)Torpedoes(Group(root,name+" Deploy",Point(deploy.Value,-1.05f),-25));
            if(restore.HasValue)Octagon(Group(root,name+" Restore",Point(restore.Value,0),0));
        }
        private static Vector3 Point(Vector2 normalized,float y) => new Vector3((normalized.x-.5f)*50,y,(.5f-normalized.y)*22.86f);
        private static Transform Group(Transform parent,string name,Vector3 position,float heading)
        {var g=new GameObject(name).transform;g.SetParent(parent,false);g.localPosition=position;g.localRotation=Quaternion.Euler(0,heading,0);return g;}
        private static void Gate(Transform t)
        {
            Cube(t,"Gate bar 3.048m",Vector3.zero,new Vector3(3.048f,.03f,.03f),white);
            for(int side=-1;side<=1;side+=2)
            {
                Cube(t,"Upper panel",new Vector3(side*1.524f,-.381f,0),new Vector3(.0762f,.762f,.0762f),side>0?red:black);
                Cube(t,"Lower panel",new Vector3(side*1.524f,-1.143f,0),new Vector3(.0762f,.762f,.0762f),side>0?black:red);
                Cube(t,"Gate mooring",new Vector3(side*1.524f,-1.737f,0),new Vector3(.008f,.426f,.008f),grey,false);
                Cube(t,"Role sign",new Vector3(side*.8f,-.18f,0),new Vector3(.305f,.305f,.015f),white);
                Label(t,side<0?"S&R":"SOS",new Vector3(side*.8f,-.18f,-.015f),side<0?black:pink,.008f);
            }
            Cube(t,"Red divider .6096m",new Vector3(0,-.3048f,0),new Vector3(.0508f,.6096f,.01f),red);
        }
        private static void Slalom(Transform t)
        {
            for(int row=0;row<3;row++)for(int col=-1;col<=1;col++)
            {
                float x=col*1.524f+(row==1?.25f:0),z=row-1;
                Cylinder(t,"Slalom pole",new Vector3(x,(row-1)*.16f,z),.0254f,.9144f,col==0?red:white);
                float baseY=t.position.y+(row-1)*.16f-.4572f;
                Cube(t,"Pole tether",new Vector3(x,((Floor-baseY)/2)+(row-1)*.16f-.4572f,z),new Vector3(.006f,baseY-Floor,.006f),grey,false);
            }
        }
        private static void Bins(Transform t)
        {
            Cube(t,"Raised pipeline",new Vector3(0,.45f,0),new Vector3(1.7f,.0254f,.0254f),white);
            for(int end=-1;end<=1;end+=2)Cube(t,"Pipeline support",new Vector3(end*.8f,.225f,0),new Vector3(.0254f,.45f,.0254f),white);
            for(int i=0;i<4;i++)
            {
                float x=i<2?-.48f:.48f,z=i%2==0?-.35f:.35f;
                var bin=Group(t,"Open crate "+i,new Vector3(x,.02f,z),0);Material role=i<2?orange:pink;
                Cube(bin,"Bin bottom",new Vector3(0,.015f,0),new Vector3(.35f,.03f,.35f),role);
                for(int side=-1;side<=1;side+=2){Cube(bin,"Crate side",new Vector3(side*.175f,.15f,0),new Vector3(.018f,.3f,.37f),white);Cube(bin,"Crate side",new Vector3(0,.15f,side*.175f),new Vector3(.35f,.3f,.018f),white);}
                Cube(t,"Magnetic light housing",new Vector3(x,.49f,z*.25f),new Vector3(.08f,.08f,.08f),green);
            }
        }
        private static void Torpedoes(Transform t)
        {
            // Real openings in the collider mesh; opening sizes are practice estimates.
            float[] xs={-.3048f,-.24f,-.04f,.11f,.23f,.3048f};float[] ys={-.3048f,-.18f,-.06f,0,.2f,.3048f};
            for(int x=0;x<xs.Length-1;x++)for(int y=0;y<ys.Length-1;y++)
            {
                float cx=(xs[x]+xs[x+1])/2,cy=(ys[y]+ys[y+1])/2;
                if(cx>-.24f && cx<-.04f && cy>0 && cy<.2f || cx>.11f && cx<.23f && cy>-.18f && cy<-.06f)continue;
                Cube(t,"Board section",new Vector3(cx,cy,0),new Vector3(xs[x+1]-xs[x],ys[y+1]-ys[y],.015f),white);
            }
            Cube(t,"Deploy red side marking",new Vector3(-.282f,0,-.012f),new Vector3(.035f,.6f,.01f),red,false);
            Cube(t,"Deploy rescue marking",new Vector3(.282f,0,-.012f),new Vector3(.035f,.6f,.01f),pink,false);
            Pinger(t,new Vector3(0,Floor-t.position.y+.03f,.25f));
            Cube(t,"Board mooring",new Vector3(0,-.68f,0),new Vector3(.008f,.72f,.008f),grey,false);
        }
        private static void Octagon(Transform t)
        {
            const float radius=1.3716f;
            for(int i=0;i<8;i++)
            {
                Vector3 a=new Vector3(Mathf.Cos(i*Mathf.PI/4)*radius,0,Mathf.Sin(i*Mathf.PI/4)*radius);
                Vector3 b=new Vector3(Mathf.Cos((i+1)*Mathf.PI/4)*radius,0,Mathf.Sin((i+1)*Mathf.PI/4)*radius);
                var edge=Cube(t,"Octagon edge",(a+b)/2,new Vector3(.035f,.035f,Vector3.Distance(a,b)),white);edge.transform.localRotation=Quaternion.LookRotation(b-a);
            }
            Cube(t,"Resupply table .6096m",new Vector3(0,-1.55f,0),new Vector3(.6096f,.025f,.6096f),white);
            for(int i=0;i<4;i++)Cylinder(t,"Sample jar",new Vector3((i%2-.5f)*.2f,-1.5f,(i/2-.5f)*.2f),.0701f,.050f,i<2?orange:pink);
            for(int side=-1;side<=1;side+=2)
            {
                Cube(t,"Collection basket",new Vector3(side*.95f,-.5f,0),new Vector3(.3f,.2f,.3f),side<0?orange:pink);
                Cube(t,"Hanging role panel",new Vector3(0,-.25f,side*1.2f),new Vector3(.305f,.305f,.015f),side<0?orange:pink);
            }
            Pinger(t,new Vector3(0,Floor+.03f,0));
        }
        private static void Pinger(Transform t,Vector3 p) => Cylinder(t,"Pinger location (visual only)",p,.08f,.06f,green);
        private static void Path(Transform t,string name,Vector2 from,Vector2 to)
        {
            Vector3 a=Point(from,Floor+.012f),b=Point(to,Floor+.012f),direction=(b-a).normalized;
            var mark=Cube(t,name,a+direction*2,new Vector3(.1524f,.008f,1.2192f),orange,false);mark.transform.rotation=Quaternion.LookRotation(direction);
        }
        private static Material Mat(string name,Color color)
        {string path="Assets/ROV/Generated/RoboSub2026/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};AssetDatabase.CreateAsset(m,path);return m;}
        private static GameObject Cube(Transform t,string name,Vector3 p,Vector3 scale,Material mat,bool solid=true)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(t,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;if(!solid)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        private static void Cylinder(Transform t,string name,Vector3 p,float diameter,float height,Material mat)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(t,false);g.transform.localPosition=p;g.transform.localScale=new Vector3(diameter,height/2,diameter);g.GetComponent<Renderer>().sharedMaterial=mat;}
        private static void Label(Transform t,string text,Vector3 p,Material color,float size)
        {var g=new GameObject(text);g.transform.SetParent(t,false);g.transform.localPosition=p;var mesh=g.AddComponent<TextMesh>();mesh.text=text;mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");mesh.fontSize=48;mesh.characterSize=size;mesh.anchor=TextAnchor.MiddleCenter;mesh.color=color.color;g.GetComponent<MeshRenderer>().sharedMaterial=mesh.font.material;}
    }
}

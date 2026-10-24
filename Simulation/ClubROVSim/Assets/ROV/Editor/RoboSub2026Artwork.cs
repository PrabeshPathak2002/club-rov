using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ClubROV.Simulation.Editor
{
    public static class RoboSub2026Artwork
    {
        private const string Art="Assets/ROV/Art/RoboSub2026/", Generated="Assets/ROV/Generated/RoboSub2026/";
        private static readonly string[] Files={"Task1_SurveyRepair","Task1_SearchRescue","Task3_Fire","Task3_Blood","Task4_ver1","Task4_ver2","Task5_Warning","Task5_Search","Task5_Survey","Task5_Rescue","Task5_Repair","Task5_Pill","Task5_RedCross","Task5_BandAid","Task5_NutBolt","Task5_Electric"};
        private static readonly Dictionary<string,Material> Materials=new();
        private static Mesh quad;
        private static Material white;
        // From the official SVG ellipse centers after its parent translation; radii end at the inner red rim.
        private static readonly Vector3[][] Holes={
            new[]{new Vector3(-.2062609f,.05406182f,.0634521f),new Vector3(.2165358f,-.21599818f,.06345221f),new Vector3(.0085433f,.19019329f,.05063588f),new Vector3(.000412f,-.19072217f,.05063588f)},
            new[]{new Vector3(-.2104093f,.06352587f,.06345221f),new Vector3(.2138725f,-.21572422f,.06345221f),new Vector3(-.0026588f,.19202172f,.05063588f),new Vector3(-.0056282f,-.19984882f,.05063588f)}};
        public static void Prepare()
        {
            Materials.Clear();white=AssetDatabase.LoadAssetAtPath<Material>(Generated+"White PVC.mat");
            if(white==null)throw new InvalidOperationException("Install handbook materials first.");
            foreach(string file in Files)
            {
                string path=Art+file+".png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)throw new InvalidOperationException("Import official PNG: "+path);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaIsTransparency=false;
                importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                var mat=AssetDatabase.LoadAssetAtPath<Material>(Generated+file+".mat");
                if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=file};AssetDatabase.CreateAsset(mat,Generated+file+".mat");}
                mat.color=Color.white;mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);mat.SetFloat("_Smoothness",0);EditorUtility.SetDirty(mat);Materials.Add(file,mat);
            }
            quad=AssetDatabase.LoadAssetAtPath<Mesh>(Generated+"Official print quad.asset");
            if(quad==null)
            {
                quad=new Mesh{name="Official print quad"};quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};quad.triangles=new[]{0,2,1,0,3,2};quad.RecalculateNormals();quad.RecalculateBounds();AssetDatabase.CreateAsset(quad,Generated+"Official print quad.asset");
            }
        }
        public static string Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
            var maps=UnityEngine.Object.FindAnyObjectByType<RovTrainingMaps>();if(maps==null || maps.gameObject.scene.path!="Assets/Scenes/PoolTraining.unity")throw new InvalidOperationException("Open PoolTraining.");
            Prepare();var layouts=maps.CopyLayouts();Apply(layouts[4].root);Apply(layouts[5].root);
            EditorSceneManager.MarkSceneDirty(maps.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();return "Official artwork applied to both handbook maps.";
        }
        public static void Apply(GameObject root)
        {
            if(quad==null || Materials.Count!=16)Prepare();
            // Snapshot station roots before adding print children.
            foreach(var station in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("Begin Assessment") || t.name.EndsWith(" Recon") || t.name.EndsWith(" Deploy") || t.name.EndsWith(" Restore")).ToArray())
            {
                if(station.name.EndsWith("Begin Assessment")) Gate(station);
                else if(station.name.EndsWith(" Recon")) Bins(station);
                else if(station.name.EndsWith(" Deploy")) Board(station);
                else if(station.name.EndsWith(" Restore")) Octagon(station);
            }
        }
        public static string Check()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Run in Play Mode.");
            var maps=UnityEngine.Object.FindAnyObjectByType<RovTrainingMaps>();var settings=maps.GetComponent<RovTrainingSettings>();var results=new List<string>();
            void Assert(string name,bool pass){results.Add((pass?"PASS: ":"FAIL: ")+name);}
            foreach(string file in Files)
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+file+".png");var material=AssetDatabase.LoadAssetAtPath<Material>(Generated+file+".mat");
                Assert(file+" imports and binds official texture",texture!=null && material!=null && material.mainTexture==texture && material.color==Color.white && !material.shader.name.Contains("Error"));
            }
            var used=new HashSet<string>();int printCount=0;
            try
            {
                foreach(int index in new[]{4,5})
                {
                    settings.SelectMap(index);Physics.SyncTransforms();var root=maps.CopyLayouts()[index].root;
                    var all=root.GetComponentsInChildren<Transform>();Assert("Map "+index+" has one artwork root per station",all.Count(t=>t.name=="Official task artwork")== (index==4?16:12));
                    Assert("Map "+index+" has no S&R/SOS text substitutes",!all.Any(t=>t.name=="S&R" || t.name=="SOS"));
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())foreach(var mat in renderer.sharedMaterials)
                        if(mat.mainTexture!=null && mat.name.StartsWith("Task")){used.Add(mat.name);printCount++;}
                    var boards=root.GetComponentsInChildren<MeshCollider>().Where(c=>c.name.StartsWith("Official board v")).ToArray();Assert("Map "+index+" has three textured collider boards",boards.Length==3);
                    foreach(var board in boards)
                    {
                        int v=board.name.Contains("v2")?2:1;int hindex=0;
                        foreach(var h in Holes[v-1])
                        {
                            Vector3 origin=board.transform.TransformPoint(new Vector3(h.x,h.y,-1));Vector3 direction=board.transform.TransformDirection(Vector3.forward);
                            Assert("Map "+index+" "+board.transform.parent.parent.name+" opening "+(++hindex)+" is clear",!board.Raycast(new Ray(origin,direction),out _,2));
                            origin=board.transform.TransformPoint(new Vector3(h.x+h.z+.005f,h.y,-1));
                            Assert("Map "+index+" board rim "+hindex+" remains solid",board.Raycast(new Ray(origin,direction),out _,2));
                        }
                    }
                }
                Assert("All 16 official images appear in the course",used.Count==16);Assert("114 textured task surfaces across both maps",printCount==114);
            }
            finally {settings.SelectMap(0);}
            string report=string.Join("\n",results);System.IO.File.WriteAllText("Docs/OfficialArtworkCheckResults.txt",report);if(results.Any(r=>r.StartsWith("FAIL")))throw new Exception(report);return report;
        }
        private static Transform PrintRoot(Transform station)
        {
            var existing=station.Find("Official task artwork");if(existing!=null)Undo.DestroyObjectImmediate(existing.gameObject);
            var root=new GameObject("Official task artwork").transform;root.SetParent(station,false);return root;
        }
        private static void RemoveNamed(Transform station,params string[] names)
        {foreach(var child in station.Cast<Transform>().ToArray())if(names.Contains(child.name))Undo.DestroyObjectImmediate(child.gameObject);}
        private static GameObject Print(Transform parent,string file,Vector3 position,float size,Quaternion rotation)
        {
            var g=new GameObject(file);g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localRotation=rotation;g.transform.localScale=Vector3.one*size;
            g.AddComponent<MeshFilter>().sharedMesh=quad;g.AddComponent<MeshRenderer>().sharedMaterial=Materials[file];return g;
        }
        private static void Gate(Transform station)
        {
            RemoveNamed(station,"S&R","SOS");var prints=PrintRoot(station);
            Print(prints,"Task1_SurveyRepair",new Vector3(-.8f,-.18f,-.0086f),.305f,Quaternion.identity);
            Print(prints,"Task1_SearchRescue",new Vector3(.8f,-.18f,-.0086f),.305f,Quaternion.identity);
        }
        private static void Bins(Transform station)
        {
            var prints=PrintRoot(station);
            for(int i=0;i<4;i++)Print(prints,i<2?"Task3_Fire":"Task3_Blood",new Vector3(i<2?-.48f:.48f,.051f,i%2==0?-.35f:.35f),.305f,Quaternion.Euler(90,0,0));
        }
        private static void Board(Transform station)
        {
            RemoveNamed(station,"Board section","Deploy red side marking","Deploy rescue marking");var prints=PrintRoot(station);
            int version=station.name.StartsWith("C2")?2:1;var g=new GameObject("Official board v"+version+" - four openings");g.transform.SetParent(prints,false);
            var mesh=BoardMesh(version);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterials=new[]{Materials["Task4_ver"+version],white};g.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        private static void Octagon(Transform station)
        {
            RemoveNamed(station,"Hanging role panel","Collection basket");var prints=PrintRoot(station);
            string[] signs={"Task5_Survey","Task5_Repair","Task5_Search","Task5_Rescue"};
            Vector3[] locations={new Vector3(0,-.25f,1.2f),new Vector3(1.2f,-.25f,0),new Vector3(0,-.25f,-1.2f),new Vector3(-1.2f,-.25f,0)};
            float[] yaw={0,90,180,270};
            for(int i=0;i<4;i++)
            {
                var mount=new GameObject("Inward sign "+i).transform;mount.SetParent(prints,false);mount.localPosition=locations[i];mount.localRotation=Quaternion.Euler(0,yaw[i],0);
                Cube(mount,"Sign backing",Vector3.zero,new Vector3(.305f,.305f,.015f));Print(mount,signs[i],new Vector3(0,0,-.0086f),.305f,Quaternion.identity);
            }
            for(int side=-1;side<=1;side+=2)
            {
                var basket=new GameObject("Open collection basket").transform;basket.SetParent(prints,false);basket.localPosition=new Vector3(side*.95f,-.6f,0);
                Cube(basket,"Basket bottom",new Vector3(0,.01f,0),new Vector3(.3f,.02f,.3f));
                for(int edge=-1;edge<=1;edge+=2){Cube(basket,"Basket wall",new Vector3(edge*.15f,.1f,0),new Vector3(.015f,.2f,.315f));Cube(basket,"Basket wall",new Vector3(0,.1f,edge*.15f),new Vector3(.3f,.2f,.015f));}
                Print(basket,side<0?"Task5_Warning":"Task5_RedCross",new Vector3(0,.021f,0),.27f,Quaternion.Euler(90,0,0));
            }
            string[] samples={"Task5_NutBolt","Task5_Electric","Task5_Pill","Task5_BandAid"};
            for(int i=0;i<4;i++)Print(prints,samples[i],new Vector3((i%2-.5f)*.2f,-1.474f,(i/2-.5f)*.2f),.055f,Quaternion.Euler(90,0,0));
        }
        private static void Cube(Transform parent,string name,Vector3 p,Vector3 scale)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=white;}
        private static Mesh BoardMesh(int version)
        {
            string path=Generated+"Official board v"+version+" mesh.asset";var cached=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(cached!=null)return cached;
            const float half=.3048f,z=.0075f;var holes=Holes[version-1];var levels=new SortedSet<float>{-half,half};
            foreach(var h in holes)for(int i=0;i<=64;i++)levels.Add(h.y+h.z*Mathf.Sin(-Mathf.PI/2+i*Mathf.PI/64));
            var ys=levels.ToArray();var vertices=new List<Vector3>();var uv=new List<Vector2>();var front=new List<int>();var rear=new List<int>();
            void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,List<int> triangles)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});foreach(var v in new[]{a,b,c,d})uv.Add(new Vector2((v.x+half)/(2*half),(v.y+half)/(2*half)));triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
            float Edge(Vector3 h,float y,int sign) => h.x+sign*Mathf.Sqrt(Mathf.Max(0,h.z*h.z-(y-h.y)*(y-h.y)));
            for(int row=0;row<ys.Length-1;row++)
            {
                float a=ys[row],b=ys[row+1],mid=(a+b)/2;if(b-a<.000001f)continue;
                var crossing=holes.Where(h=>Mathf.Abs(mid-h.y)<h.z).OrderBy(h=>h.x).ToArray();float leftA=-half,leftB=-half;
                foreach(var h in crossing.Concat(new[]{new Vector3(half,0,0)}))
                {
                    float rightA=h.z==0?half:Edge(h,a,-1),rightB=h.z==0?half:Edge(h,b,-1);
                    Face(new Vector3(leftA,a,-z),new Vector3(rightA,a,-z),new Vector3(rightB,b,-z),new Vector3(leftB,b,-z),front);
                    Face(new Vector3(rightA,a,z),new Vector3(leftA,a,z),new Vector3(leftB,b,z),new Vector3(rightB,b,z),rear);
                    leftA=Edge(h,a,1);leftB=Edge(h,b,1);
                }
            }
            foreach(var h in holes)for(int i=0;i<128;i++)
            {
                float a=i*2*Mathf.PI/128,b=(i+1)*2*Mathf.PI/128;Vector2 p=new Vector2(h.x+h.z*Mathf.Cos(a),h.y+h.z*Mathf.Sin(a)),q=new Vector2(h.x+h.z*Mathf.Cos(b),h.y+h.z*Mathf.Sin(b));
                Face(new Vector3(p.x,p.y,-z),new Vector3(q.x,q.y,-z),new Vector3(q.x,q.y,z),new Vector3(p.x,p.y,z),rear);
            }
            var corners=new[]{new Vector2(-half,-half),new Vector2(half,-half),new Vector2(half,half),new Vector2(-half,half)};
            for(int i=0;i<4;i++){var p=corners[i];var q=corners[(i+1)%4];Face(new Vector3(q.x,q.y,-z),new Vector3(p.x,p.y,-z),new Vector3(p.x,p.y,z),new Vector3(q.x,q.y,z),rear);}
            var mesh=new Mesh{name="Official torpedo board v"+version};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=2;mesh.SetTriangles(front,0);mesh.SetTriangles(rear,1);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}

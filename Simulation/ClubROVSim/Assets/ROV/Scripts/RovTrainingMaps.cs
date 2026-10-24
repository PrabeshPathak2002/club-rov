using System;
using UnityEngine;
namespace ClubROV.Simulation
{
    [Serializable] public sealed class RovMapLayout
    {
        public GameObject root;
        public GameObject environment;
        public Transform[] gates;
        public Transform hover, dock, marker;
    }
    [RequireComponent(typeof(RovTrainingCourse))]
    public sealed class RovTrainingMaps : MonoBehaviour
    {
        public static readonly string[] Names={"Pool course","Gate & pillar","Empty pool","Slalom","RoboSub 2026 practice","RoboSub 2026 finals"};
        public static readonly string[] Descriptions={
            "Three gates, cyan hover ring, green dock. Original course.",
            "RoboSub practice: 2 m bar at 1 m depth; pillar 10 m beyond. Start 3 m behind, pass under, circle pillar, return under. Practice only; no autonomy certification.",
            "Open water inside the pool for tuning, camera checks and free piloting. No score completion.",
            "Five offset gates at changing depths, followed by hover and dock. Precision maneuvering practice.",
            "2026 handbook p80: A/B1/B2/C1/C2/D arrangement. Diagram spacing approximated; object sizes follow task drawings. Static practice stations; no official judging.",
            "2026 handbook p82: combined A+B finals field, with C/D alongside. Diagram spacing approximated. Static practice stations; no official judging."};
        [SerializeField] private RovMapLayout[] layouts;
        private int current=-1;
        public int Current=>current;
        public RovMapLayout[] CopyLayouts() => (RovMapLayout[])layouts.Clone();
        public float StartHeading(int index) => index==5 ? 150 : index==4 ? 180 : 0;
        public void Configure(RovMapLayout[] values) => layouts=values;
        public Vector3 StartPosition(int index) => index==1 ? new Vector3(0,-1.6f,-8) : index==4 ? new Vector3(-20.25f,-0.35f,10.7724f) : index==5 ? new Vector3(-21.75f,-0.35f,10.142f) : new Vector3(0,-1.2f,-1);
        public void Activate(int index)
        {
            if(index<0 || layouts==null || index>=layouts.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if(current==index) return;
            if(GetComponent<RovDynamics>().ExternalPhysics) throw new InvalidOperationException("Disconnect SITL before changing maps.");
            for(int i=0;i<layouts.Length;i++)
            { layouts[i].root.SetActive(i==index); if(layouts[i].environment!=null) layouts[i].environment.SetActive(false); }
            var m=layouts[index];
            if(m.environment!=null) m.environment.SetActive(true);
            GetComponent<RovTrainingCourse>().PracticeObjective=index>=4 ? Names[index].ToUpperInvariant()+" | task exploration (unscored)" : "FREE SWIM | no scored objective";
            GetComponent<RovTrainingCourse>().ConfigureMap(m.gates,m.hover,m.dock,m.marker,index==2 || index>=4);
            current=index;
        }
    }
}

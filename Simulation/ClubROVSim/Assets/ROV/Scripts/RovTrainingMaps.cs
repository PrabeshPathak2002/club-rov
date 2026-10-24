using System;
using UnityEngine;
namespace ClubROV.Simulation
{
    [Serializable] public sealed class RovMapLayout
    {
        public GameObject root;
        public Transform[] gates;
        public Transform hover, dock, marker;
    }
    [RequireComponent(typeof(RovTrainingCourse))]
    public sealed class RovTrainingMaps : MonoBehaviour
    {
        public static readonly string[] Names={"Pool course","Gate & pillar","Empty pool","Slalom"};
        public static readonly string[] Descriptions={
            "Three gates, cyan hover ring, green dock. Original course.",
            "RoboSub practice: 2 m bar at 1 m depth; pillar 10 m beyond. Start 3 m behind, pass under, circle pillar, return under. Practice only; no autonomy certification.",
            "Open water inside the pool for tuning, camera checks and free piloting. No score completion.",
            "Five offset gates at changing depths, followed by hover and dock. Precision maneuvering practice."};
        [SerializeField] private RovMapLayout[] layouts;
        private int current=-1;
        public int Current=>current;
        public void Configure(RovMapLayout[] values) => layouts=values;
        public Vector3 StartPosition(int index) => index==1 ? new Vector3(0,-1.6f,-8) : new Vector3(0,-1.2f,-1);
        public void Activate(int index)
        {
            if(index<0 || layouts==null || index>=layouts.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if(current==index) return;
            if(GetComponent<RovDynamics>().ExternalPhysics) throw new InvalidOperationException("Disconnect SITL before changing maps.");
            for(int i=0;i<layouts.Length;i++) layouts[i].root.SetActive(i==index);
            var m=layouts[index];
            GetComponent<RovTrainingCourse>().ConfigureMap(m.gates,m.hover,m.dock,m.marker,index==2);
            current=index;
        }
    }
}

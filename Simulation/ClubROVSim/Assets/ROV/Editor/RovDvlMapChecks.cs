using System;
using UnityEngine;
namespace ClubROV.Simulation.Editor
{
    public static class RovDvlMapChecks
    {
        public static string Run()
        {
            int count=0;
            void Check(bool condition,string label){if(!condition)throw new Exception(label);count++;}
            string Json(string session,long stamp,float x,float y,bool valid=true) => JsonUtility.ToJson(new RovDvlMap.Packet {schema=1,kind="dvl_track",session=session,stamp_ns=stamp,x=x,y=y,yaw=0,valid=valid,reason=valid?"live":"bottom lock lost"});
            var t=new RovDvlMap.Track();
            Check(t.Accept(Json("a",1,0,0),"a",0),"accept local odometry");
            Check(t.Live(.5),"fresh pose live");
            Check(!t.Live(1),"clock stall becomes stale");
            Check(t.Accept(Json("a",2,1,0),"a",1),"forward sample accepted");
            Check(RovDvlMap.Track.Project(t.Position)==new Vector2(0,-1),"launch-forward projects up");
            Check(RovDvlMap.Track.Project(new Vector2(0,1))==new Vector2(-1,0),"port projects left");
            Check(Vector2.Distance(RovDvlMap.Track.Forward(Mathf.PI/2),Vector2.left)<1e-5,"positive ROS yaw points left");
            int before=t.Points.Count;
            Check(t.Accept(Json("a",3,5,5,false),"a",1.1)&&!t.Live(1.1),"invalid DVL freezes marker");
            Check(t.Points.Count==before && t.Position==new Vector2(1,0),"loss does not add a fake path point");
            t.Accept(Json("a",4,1.1f,0),"a",1.2);
            Check(t.Points[t.Points.Count-1].Break,"reacquisition starts a separate trail segment");
            Check(!t.Accept(Json("old",100,0,0),"a",2),"old sensor session rejected");
            Check(!t.Accept(Json("a",1,0,0),"a",2),"replayed timestamp rejected");
            Check(!t.Accept("{}","a",2),"malformed packet rejected");
            t.Accept(Json("b",1,0,0),"b",3);
            Check(t.Points.Count==1 && t.Session=="b","new mission clears old trail");
            for(int i=2;i<700;i++)t.Accept(Json("b",i,i*.1f,0),"b",3+i*.01);
            Check(t.Points.Count==512,"trail memory bounded");
            t.Clear();Check(t.Points.Count==0&&t.HasPose,"clear trail retains estimated marker");
            return count+" DVL map checks passed";
        }
    }
}

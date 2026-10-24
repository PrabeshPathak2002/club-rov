using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    // Native-resolution instruments over the FPV frame, independent of video delay/noise.
    public sealed class RovFlightOverlay : VisualElement, IDisposable
    {
        private readonly RovDynamics vehicle;
        private readonly RovCameraRig rig;
        private readonly RovSitlBridge sitl;
        private readonly VisualElement root;
        private readonly Button toggle;
        private float refresh;
        private float pitch, roll, heading, depth, speed;
        private string control="SIM";
        public bool InstrumentsEnabled { get; private set; } = true;
        public bool Visible => InstrumentsEnabled && rig!=null && rig.PilotView;
        public static Vector3 Attitude(Quaternion rotation)
        {
            var forward=rotation*Vector3.forward;
            var right=rotation*Vector3.right;
            var up=rotation*Vector3.up;
            return new Vector3(Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg,
                Mathf.Atan2(-right.y,up.y)*Mathf.Rad2Deg,
                Mathf.Repeat(Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg,360));
        }
        public RovFlightOverlay(VisualElement document,RovDynamics dynamics,RovCameraRig cameraRig,RovSitlBridge bridge)
        {
            vehicle=dynamics;rig=cameraRig;sitl=bridge;root=document.Q("root");
            name="flight-osd";pickingMode=PickingMode.Ignore;
            style.position=Position.Absolute;style.left=0;style.right=0;style.top=0;style.bottom=0;
            generateVisualContent+=Draw;root.Insert(0,this);
            toggle=new Button(Toggle){name="osd-toggle",focusable=false};
            document.Q("panel-content-control").Add(toggle);UpdateButton();
        }
        public void Toggle() { InstrumentsEnabled=!InstrumentsEnabled;UpdateButton();root.Focus(); }
        private void UpdateButton() => toggle.text=InstrumentsEnabled?"FPV INSTRUMENTS · ON · O / LEFT-STICK CLICK":"FPV INSTRUMENTS · OFF · O / LEFT-STICK CLICK";
        public void Tick()
        {
            var focused=root.panel?.focusController?.focusedElement as VisualElement;
            bool editing=focused is TextField || focused?.GetFirstAncestorOfType<TextField>()!=null;
            if(!editing && ((Keyboard.current?.oKey.wasPressedThisFrame??false) ||
                (Gamepad.current?.leftStickButton.wasPressedThisFrame??false))) Toggle();
            style.display=Visible?DisplayStyle.Flex:DisplayStyle.None;
            if(!Visible || vehicle==null || Time.unscaledTime<refresh)return;
            refresh=Time.unscaledTime+1f/30;
            Vector3 attitude=Attitude(vehicle.transform.rotation);
            pitch=attitude.x;roll=attitude.y;heading=attitude.z;
            depth=vehicle.Depth;speed=vehicle.Speed;
            control=sitl==null?"SIM":sitl.ControlState;
            MarkDirtyRepaint();
        }
        private void Draw(MeshGenerationContext ctx)
        {
            float height=contentRect.height,width=contentRect.width;
            if(width<1 || height<1)return;
            // Match the letterboxed 4:3 video rather than the monitor edges.
            float h=Mathf.Min(height,width*0.75f),w=h*4f/3;
            float left=(width-w)/2,top=(height-h)/2;
            Vector2 center=new(width/2,height/2);
            float scale=h/720f;
            var painter=ctx.painter2D;
            Color ink=new(0.68f,1f,0.81f,0.95f);
            void Line(Vector2 a,Vector2 b,Color color,float thickness=1.5f)
            {
                painter.lineWidth=thickness*scale;painter.strokeColor=new Color(0,0,0,0.85f);
                painter.BeginPath();painter.MoveTo(a);painter.LineTo(b);painter.Stroke();
                painter.lineWidth=Mathf.Max(1,thickness*scale-1);painter.strokeColor=color;
                painter.BeginPath();painter.MoveTo(a);painter.LineTo(b);painter.Stroke();
            }
            void Text(string text,float x,float y,float size=15)
            {
                var pos=new Vector2(x,y);
                ctx.DrawText(text,pos+Vector2.one, size*scale,Color.black,null);
                ctx.DrawText(text,pos,size*scale,ink,null);
            }
            // Heading tape spans 90 degrees, with wraparound through north.
            float tapeY=top+64*scale;
            for(int i=-9;i<=9;i++)
            {
                int tick=Mathf.FloorToInt(heading/5)*5+i*5;
                float delta=Mathf.DeltaAngle(heading,Mathf.Repeat(tick,360));
                if(Mathf.Abs(delta)>45)continue;
                float x=center.x+delta*5*scale;
                bool major=tick%30==0;
                Line(new Vector2(x,tapeY),new Vector2(x,tapeY+(major?12:6)*scale),ink);
                if(major)
                {
                    int value=(int)Mathf.Repeat(tick,360);
                    string label=value==0?"N":value==90?"E":value==180?"S":value==270?"W":value.ToString("000");
                    Text(label,x-10*scale,tapeY-22*scale,13);
                }
            }
            Text($"HDG {heading:000}°",center.x-45*scale,top+12*scale,17);
            Line(new Vector2(center.x-5*scale,tapeY-5*scale),new Vector2(center.x,tapeY),ink,3);
            Line(new Vector2(center.x+5*scale,tapeY-5*scale),new Vector2(center.x,tapeY),ink,3);
            float radians=-roll*Mathf.Deg2Rad;
            Vector2 Project(float x,float y) => center+new Vector2(x*Mathf.Cos(radians)-y*Mathf.Sin(radians),x*Mathf.Sin(radians)+y*Mathf.Cos(radians))*scale;
            for(int angle=-80;angle<=80;angle+=10)
            {
                float y=(pitch-angle)*3;
                if(Mathf.Abs(y)>100)continue;
                float length=angle==0?85:40;
                Line(Project(-length,y),Project(-12,y),ink,angle==0?3:2);
                Line(Project(12,y),Project(length,y),ink,angle==0?3:2);
            }
            // Fixed aircraft reference remains level with the camera image.
            Line(center+new Vector2(-48,0)*scale,center+new Vector2(-16,0)*scale,Color.yellow,3);
            Line(center+new Vector2(16,0)*scale,center+new Vector2(48,0)*scale,Color.yellow,3);
            Line(center+new Vector2(-16,0)*scale,center+new Vector2(0,7)*scale,Color.yellow,3);
            Line(center+new Vector2(0,7)*scale,center+new Vector2(16,0)*scale,Color.yellow,3);
            Text($"DEPTH\n{depth:0.00} m",left+35*scale,center.y-18*scale,18);
            Text($"SPEED\n{speed:0.00} m/s",left+w-155*scale,center.y-18*scale,18);
            Text($"PITCH {pitch:+0.0;-0.0;0.0}°    ROLL {roll:+0.0;-0.0;0.0}°",center.x-145*scale,top+h-90*scale);
            Text("SIM ATTITUDE · NORTH REFERENCE · O: OSD",left+30*scale,top+h-36*scale,12);
            Text(control,left+30*scale,top+h-60*scale,13);
        }
        public void Dispose()
        {
            generateVisualContent-=Draw;toggle.clicked-=Toggle;toggle.RemoveFromHierarchy();RemoveFromHierarchy();
        }
    }
}

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
        private readonly DropdownField styleChoice;
        public enum HudStyle { CompactDials, FlightOverlay, BetaflightOsd }
        public HudStyle InstrumentStyle { get; private set; }
        private readonly System.Collections.Generic.List<Vector2> ground = new(68);
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
            styleChoice=new DropdownField("Instrument style",new System.Collections.Generic.List<string>{"Compact dials","Flight overlay","Betaflight-style OSD"},0){name="osd-style"};
            InstrumentStyle=(HudStyle)Mathf.Clamp(PlayerPrefs.GetInt("osd.style",0),0,2);
            styleChoice.SetValueWithoutNotify(styleChoice.choices[(int)InstrumentStyle]);
            styleChoice.RegisterValueChangedCallback(StyleChanged);
            document.Q("panel-content-control").Add(styleChoice);
        }
        private void StyleChanged(ChangeEvent<string> evt) => SetStyle((HudStyle)styleChoice.index);
        public void SetStyle(HudStyle value)
        {
            if(!Enum.IsDefined(typeof(HudStyle),value))throw new ArgumentOutOfRangeException(nameof(value));
            InstrumentStyle=value;styleChoice.SetValueWithoutNotify(styleChoice.choices[(int)value]);
            PlayerPrefs.SetInt("osd.style",(int)value);PlayerPrefs.Save();MarkDirtyRepaint();root.Focus();
        }
        public void Toggle() { InstrumentsEnabled=!InstrumentsEnabled;UpdateButton();root.Focus(); }
        private void UpdateButton() => toggle.text=InstrumentsEnabled?"FPV INSTRUMENTS · ON · O / LEFT-STICK CLICK":"FPV INSTRUMENTS · OFF · O / LEFT-STICK CLICK";
        public void Tick()
        {
            var focused=root.panel?.focusController?.focusedElement as VisualElement;
            bool editing=focused is TextField || focused?.GetFirstAncestorOfType<TextField>()!=null;
            if(!editing && ((Keyboard.current?.oKey.wasPressedThisFrame??false) ||
                (Gamepad.current?.leftStickButton.wasPressedThisFrame??false))) Toggle();
            if(!editing && (Keyboard.current?.iKey.wasPressedThisFrame??false))
                SetStyle((HudStyle)(((int)InstrumentStyle+1)%3));
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
            if(InstrumentStyle==HudStyle.BetaflightOsd) { DrawBetaflight(ctx,left,top,w,h,scale);return; }
            if(InstrumentStyle==HudStyle.CompactDials)
            {
                DrawCompact(ctx,left,top,w,h,scale);return;
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
        private void DrawBetaflight(MeshGenerationContext ctx,float left,float top,float width,float height,float scale)
        {
            var p=ctx.painter2D;float pixel=2.4f*scale;Vector2 center=new(left+width/2,top+height/2);
            void Text(string text,float x,float y,bool centered=false)
            {
                if(centered)x-=text.Length*6*pixel/2;
                RovOsdLettering.Draw(p,text,new Vector2(x,y),pixel);
            }
            void Line(Vector2 a,Vector2 b,float size=2)
            {
                p.strokeColor=Color.black;p.lineWidth=(size+2)*scale;p.BeginPath();p.MoveTo(a);p.LineTo(b);p.Stroke();
                p.strokeColor=Color.white;p.lineWidth=size*scale;p.BeginPath();p.MoveTo(a);p.LineTo(b);p.Stroke();
            }
            Text($"HDG {(int)Mathf.Repeat(Mathf.Round(heading),360):000}",center.x,top+28*scale,true);
            for(int tick=-9;tick<=9;tick++)
            {
                int bearing=Mathf.FloorToInt(heading/10)*10+tick*10;
                float delta=Mathf.DeltaAngle(heading,Mathf.Repeat(bearing,360));
                if(Mathf.Abs(delta)>55)continue;
                float x=center.x+delta*4*scale;
                int value=(int)Mathf.Repeat(bearing,360);
                string label=value%90==0?(value==0?"N":value==90?"E":value==180?"S":"W"):".";
                Text(label,x,top+65*scale,true);
            }
            Line(new Vector2(center.x,top+88*scale),new Vector2(center.x,top+96*scale));
            float angle=-roll*Mathf.Deg2Rad;
            Vector2 along=new(Mathf.Cos(angle),Mathf.Sin(angle)),normal=new(-Mathf.Sin(angle),Mathf.Cos(angle));
            float shift=Mathf.Clamp(pitch*2.5f,-110,110)*scale;
            for(int i=-4;i<=4;i++)
            {
                Vector2 a=center+normal*shift+along*(i*18-6)*scale;
                Line(a,a+along*12*scale);
            }
            Line(center+new Vector2(-18,0)*scale,center+new Vector2(-6,0)*scale);
            Line(center+new Vector2(6,0)*scale,center+new Vector2(18,0)*scale);
            Line(center+new Vector2(0,-6)*scale,center+new Vector2(0,6)*scale);
            for(int side=-1;side<=1;side+=2)
            {
                float x=center.x+side*112*scale;
                Line(new Vector2(x,center.y-18*scale),new Vector2(x,center.y+18*scale),1.5f);
                Line(new Vector2(x-side*5*scale,center.y),new Vector2(x+side*5*scale,center.y),1.5f);
            }
            Text($"DEPTH {depth:0.0}M",left+28*scale,top+height-110*scale);
            string velocity=$"{speed:0.0}M/S";
            Text(velocity,left+width-28*scale-velocity.Length*6*pixel,top+height-110*scale);
            Text($"P {pitch:+0;-0;0} R {roll:+0;-0;0}",center.x,top+height-74*scale,true);
            Text(control.ToUpperInvariant(),center.x,top+height-40*scale,true);
            Text("SIM",left+28*scale,top+height-40*scale);
        }
        private void DrawCompact(MeshGenerationContext ctx,float left,float top,float width,float height,float scale)
        {
            var p=ctx.painter2D;
            float radius=55*scale;
            Vector2 attitudeCenter=new(left+width-224*scale,top+height-110*scale);
            Vector2 compassCenter=new(left+width-88*scale,attitudeCenter.y);
            Color white=new(0.92f,0.96f,1f);
            void Circle(Vector2 center,float r,Color fill)
            {
                p.fillColor=fill;p.BeginPath();p.Arc(center,r,Angle.Degrees(0),Angle.Degrees(360));p.ClosePath();p.Fill();
            }
            void Line(Vector2 a,Vector2 b,Color color,float thickness=2)
            {
                p.strokeColor=color;p.lineWidth=thickness*scale;p.BeginPath();p.MoveTo(a);p.LineTo(b);p.Stroke();
            }
            void Text(string text,Vector2 position,float size=12)
            {
                ctx.DrawText(text,position+Vector2.one,size*scale,Color.black,null);
                ctx.DrawText(text,position,size*scale,white,null);
            }
            Circle(attitudeCenter,radius+4*scale,new Color(0.03f,0.06f,0.08f,0.95f));
            Circle(compassCenter,radius+4*scale,new Color(0.03f,0.06f,0.08f,0.95f));
            Circle(attitudeCenter,radius+1.5f*scale,new Color(0.60f,0.65f,0.69f));
            Circle(compassCenter,radius+1.5f*scale,new Color(0.60f,0.65f,0.69f));
            Circle(attitudeCenter,radius,new Color(0.19f,0.44f,0.64f));
            float angle=-roll*Mathf.Deg2Rad;
            Vector2 along=new(Mathf.Cos(angle),Mathf.Sin(angle));
            Vector2 normal=new(-along.y,along.x);
            float offset=Mathf.Clamp(pitch/45f,-2,2)*radius;
            // Clip the circular face against the moving horizon half-plane.
            ground.Clear();
            Vector2 previous=new(radius,0);float previousDistance=Vector2.Dot(previous,normal)-offset;
            for(int i=1;i<=64;i++)
            {
                float a=i*Mathf.PI*2/64;Vector2 current=new(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius);
                float distance=Vector2.Dot(current,normal)-offset;
                if((distance>=0)!=(previousDistance>=0))ground.Add(previous+(current-previous)*(previousDistance/(previousDistance-distance)));
                if(distance>=0)ground.Add(current);
                previous=current;previousDistance=distance;
            }
            if(ground.Count>2)
            {
                p.fillColor=new Color(0.40f,0.25f,0.12f);p.BeginPath();p.MoveTo(attitudeCenter+ground[0]);
                for(int i=1;i<ground.Count;i++)p.LineTo(attitudeCenter+ground[i]);p.ClosePath();p.Fill();
            }
            for(int mark=-30;mark<=30;mark+=10)
            {
                float y=offset-mark/45f*radius;
                if(Mathf.Abs(y)>radius*0.9f)continue;
                float extent=mark==0?Mathf.Sqrt(radius*radius-y*y):18*scale;
                Line(attitudeCenter+normal*y-along*extent,attitudeCenter+normal*y+along*extent,white,mark==0?2:1);
            }
            for(int bank=-60;bank<=60;bank+=30)
            {
                float a=(bank-90)*Mathf.Deg2Rad;var axis=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                Line(attitudeCenter+axis*(radius-7*scale),attitudeCenter+axis*radius,white,1);
            }
            Line(attitudeCenter+new Vector2(-28,0)*scale,attitudeCenter+new Vector2(-9,0)*scale,Color.yellow,3);
            Line(attitudeCenter+new Vector2(9,0)*scale,attitudeCenter+new Vector2(28,0)*scale,Color.yellow,3);
            Line(attitudeCenter+new Vector2(-9,0)*scale,attitudeCenter+new Vector2(0,5)*scale,Color.yellow,3);
            Line(attitudeCenter+new Vector2(0,5)*scale,attitudeCenter+new Vector2(9,0)*scale,Color.yellow,3);
            Circle(compassCenter,radius,new Color(0.06f,0.10f,0.14f));
            for(int bearing=0;bearing<360;bearing+=15)
            {
                float a=(bearing-heading-90)*Mathf.Deg2Rad;
                Vector2 axis=new(Mathf.Cos(a),Mathf.Sin(a));
                Line(compassCenter+axis*(radius-6*scale),compassCenter+axis*(radius-2*scale),white,1);
                if(bearing%90==0)
                {
                    string text=bearing==0?"N":bearing==90?"E":bearing==180?"S":"W";
                    Text(text,compassCenter+axis*(radius-18*scale)-new Vector2(4,7)*scale,13);
                }
            }
            Line(compassCenter+new Vector2(-5,-50)*scale,compassCenter+new Vector2(0,-42)*scale,Color.red,3);
            Line(compassCenter+new Vector2(5,-50)*scale,compassCenter+new Vector2(0,-42)*scale,Color.red,3);
            Text($"{heading:000}°",compassCenter-new Vector2(21,9)*scale,17);
            Text("ATTITUDE",attitudeCenter+new Vector2(-29,-76)*scale,11);
            Text("COMPASS",compassCenter+new Vector2(-28,-76)*scale,11);
            Text($"P {pitch:+0;-0;0}°  R {roll:+0;-0;0}°",attitudeCenter+new Vector2(-46,61)*scale,11);
            Text("SIM · I: STYLE · O: HIDE",attitudeCenter+new Vector2(-30,82)*scale,10);
        }
        public void Dispose()
        {
            generateVisualContent-=Draw;toggle.clicked-=Toggle;styleChoice.UnregisterValueChangedCallback(StyleChanged);styleChoice.RemoveFromHierarchy();toggle.RemoveFromHierarchy();RemoveFromHierarchy();
        }
    }
}

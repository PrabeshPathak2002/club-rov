using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    // Small original bitmap alphabet for the outlined, fixed-cell OSD style.
    // No firmware font assets or device-specific glyph codes are required.
    public static class RovOsdLettering
    {
        private static readonly Dictionary<char,byte[]> glyphs=new();
        static RovOsdLettering()
        {
            string[] rows={
                "A:0E11111F111111","B:1E11111E11111E","C:0E11101010110E","D:1E11111111111E",
                "E:1F10101E10101F","F:1F10101E101010","G:0E11101711110F","H:1111111F111111",
                "I:0E04040404040E","J:0702020212120C","K:11121418141211","L:1010101010101F",
                "M:111B1515111111","N:11191915131311","O:0E11111111110E","P:1E11111E101010",
                "Q:0E11111115120D","R:1E11111E141211","S:0F10100E01011E","T:1F040404040404",
                "U:1111111111110E","V:11111111110A04","W:11111115151B11","X:11110A040A1111",
                "Y:11110A04040404","Z:1F01020408101F",
                "0:0E11131519110E","1:040C040404040E","2:0E11010204081F","3:1E01010E01011E",
                "4:02060A121F0202","5:1F10101E01011E","6:0E10101E11110E","7:1F010204080808",
                "8:0E11110E11110E","9:0E11110F01010E",
                ".:00000000000C0C","::000C0C000C0C00","-:0000001F000000","+:0004041F040400",
                "/:01010204081010","°:06090906000000","|:04040404040404"};
            foreach(var row in rows)
            {
                var bits=new byte[7];for(int i=0;i<7;i++)bits[i]=System.Convert.ToByte(row.Substring(2+i*2,2),16);
                glyphs[row[0]]=bits;
            }
        }
        public static void Draw(Painter2D p,string text,Vector2 position,float pixel)
        {
            void Rect(float x,float y,float w,float h,Color color)
            {
                p.fillColor=color;p.BeginPath();p.MoveTo(new Vector2(x,y));p.LineTo(new Vector2(x+w,y));
                p.LineTo(new Vector2(x+w,y+h));p.LineTo(new Vector2(x,y+h));p.ClosePath();p.Fill();
            }
            // First pass outlines all runs, second pass paints their white cores.
            for(int pass=0;pass<2;pass++)
            for(int index=0;index<text.Length;index++)
            {
                if(!glyphs.TryGetValue(char.ToUpperInvariant(text[index]),out var rows))continue;
                for(int y=0;y<7;y++)
                for(int x=0;x<5;)
                {
                    if((rows[y]&(1<<(4-x)))==0){x++;continue;}
                    int start=x;while(x<5 && (rows[y]&(1<<(4-x)))!=0)x++;
                    float border=pass==0?pixel*0.45f:0;
                    Rect(position.x+(index*6+start)*pixel-border,position.y+y*pixel-border,
                        (x-start)*pixel+2*border,pixel+2*border,pass==0?new Color(0,0,0,0.9f):Color.white);
                }
            }
        }
    }
}

"""Build the TDR PDF from Technical_Design_Report.md.
Requires Python 3, reportlab and Pillow. Run: python build_report.py
Final PDF: output/pdf/Club_ROV_TDR.pdf. Research references are not bundled.
"""
from pathlib import Path
import re,html
from reportlab.pdfgen import canvas
from reportlab.platypus import BaseDocTemplate,PageTemplate,Frame,Paragraph,Spacer,Image,KeepTogether,Table,TableStyle,FrameBreak
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_JUSTIFY,TA_CENTER,TA_LEFT
from reportlab.lib import colors
from reportlab.graphics.shapes import Drawing,Rect,String,Line,Polygon
from PIL import Image as PILImage

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
SOURCE=HERE/'Technical_Design_Report.md'
OUT=HERE/'output/pdf/Club_ROV_TDR.pdf'
W,H=612,792
M=40
GAP=18
CW=(W-2*M-GAP)/2
INK=colors.HexColor('#142b3a')
TEAL=colors.HexColor('#176b76')
LIGHT=colors.HexColor('#eef4f6')
styles={
 'body':ParagraphStyle('body',fontName='Times-Roman',fontSize=10,leading=12,alignment=TA_JUSTIFY,spaceAfter=5,splitLongWords=True,allowWidows=0,allowOrphans=0),
 'abstract':ParagraphStyle('abstract',fontName='Times-Bold',fontSize=9.5,leading=11.5,alignment=TA_JUSTIFY,spaceAfter=7),
 'h2':ParagraphStyle('h2',fontName='Times-Bold',fontSize=10.8,leading=13,alignment=TA_CENTER,spaceBefore=9,spaceAfter=6,keepWithNext=True,textColor=INK),
 'h3':ParagraphStyle('h3',fontName='Times-Italic',fontSize=10.3,leading=12.5,spaceBefore=6,spaceAfter=4,keepWithNext=True),
 'caption':ParagraphStyle('caption',fontName='Times-Italic',fontSize=8.4,leading=10,alignment=TA_CENTER,spaceBefore=4,spaceAfter=9),
 'reference':ParagraphStyle('reference',fontName='Times-Roman',fontSize=8.4,leading=10.1,alignment=TA_LEFT,spaceAfter=5,splitLongWords=True,allowWidows=0,allowOrphans=0),
 'cell':ParagraphStyle('cell',fontName='Times-Roman',fontSize=8.3,leading=9.8,spaceAfter=0),
 'headcell':ParagraphStyle('headcell',fontName='Times-Bold',fontSize=8.3,leading=9.8,textColor=colors.white),
}

def ptext(s):
 def link(m):
  label,target=m.group(1),m.group(2)
  if not target.startswith(('http://','https://')):
   from urllib.parse import unquote,quote
   target='https://github.com/PrabeshPathak2002/club-rov/blob/e9bd47d/'+quote(str((HERE/unquote(target)).resolve().relative_to(ROOT)).replace('\\','/'),safe='/#')
  return f'<link href="{html.escape(target,quote=True)}" color="#176b76">{html.escape(label)}</link>'
 links=[]
 def save(m):links.append(link(m));return f'ZZLINK{len(links)-1}ZZ'
 s=re.sub(r'\[([^\]]+)\]\(([^)]+)\)',save,s)
 s=html.escape(s)
 s=re.sub(r'\*\*(.+?)\*\*',r'<b>\1</b>',s)
 s=re.sub(r'`([^`]+)`',r'<font name="Courier" size="8.4">\1</font>',s)
 for i,value in enumerate(links):s=s.replace(f'ZZLINK{i}ZZ',value)
 s=s.replace('F_i = T_i u_i d_i','<i>F</i><sub>i</sub> = <i>T</i><sub>i</sub> <i>u</i><sub>i</sub> <i>d</i><sub>i</sub>').replace('r_i x F_i','<i>r</i><sub>i</sub> x <i>F</i><sub>i</sub>').replace('0.008 m3','0.008 m<super>3</super>').replace('1000 kg/m3','1000 kg/m<super>3</super>')
 return s

def box(d,x,y,w,h,lines,planned=False):
 d.add(Rect(x,y,w,h,rx=3,ry=3,fillColor=LIGHT,strokeColor=TEAL,strokeWidth=.7,strokeDashArray=[3,2] if planned else None))
 if isinstance(lines,str):lines=[lines]
 for n,line in enumerate(lines):d.add(String(x+w/2,y+h/2+(len(lines)-1)*5-n*10-3,line,fontName='Helvetica',fontSize=8.2,textAnchor='middle',fillColor=INK))

def arrow(d,x1,y1,x2,y2):
 d.add(Line(x1,y1,x2,y2,strokeColor=INK,strokeWidth=.8))
 import math
 a=math.atan2(y2-y1,x2-x1);length=4
 pts=[x2,y2,x2-length*math.cos(a-.5),y2-length*math.sin(a-.5),x2-length*math.cos(a+.5),y2-length*math.sin(a+.5)]
 d.add(Polygon(pts,fillColor=INK,strokeColor=INK))

def architecture():
 d=Drawing(CW,237)
 for y,label in [(195,['Surface QGroundControl','Pilot commands / telemetry / video']),(130,['ESP32-P4 companion','USB/UDP bridge + H.264 + maintenance']),(65,['ARK FPV / ArduSub','Stabilization, mixing, failsafes']),(6,['ESCs and six thrusters','4 horizontal + 2 vertical'])]:box(d,57,y,198,34,label)
 box(d,0,130,49,34,['IMX219','camera']);arrow(d,49,147,57,147)
 for y1,y2,label in [(195,164,'Ethernet tether'),(130,99,'USB CDC MAVLink'),(65,40,'DShot / PWM')]:
  arrow(d,151,y1,151,y2);d.add(String(160,(y1+y2)/2-3,label,fontName='Helvetica',fontSize=7.8,fillColor=INK))
 return d

def power():
 d=Drawing(CW,195)
 box(d,2,154,117,35,['4S propulsion battery','14.8 V nominal'])
 box(d,139,154,116,35,['PoE supply','Companion electronics'])
 box(d,2,85,117,39,['Insulated + / - bars','ARK VBAT branch','Protection: to be selected'],True)
 box(d,139,85,116,39,['ESP32-P4 + C6','Video / bridge / Wi-Fi'])
 box(d,2,15,117,36,['Four-in-one + 2 ESCs','Six U01 motors'])
 box(d,139,15,116,36,['IMX219 camera','CSI image path'])
 arrow(d,60,154,60,124);arrow(d,60,85,60,51);arrow(d,197,154,197,124);arrow(d,197,85,197,51)
 return d

def ota():
 d=Drawing(CW,180)
 labels=[['Fresh disarmed heartbeat + password'],['Validate compatible application image'],['Write inactive slot / recheck state'],['Reboot and evaluate startup checks'],['Confirm image, or roll back on failure']]
 for i,label in enumerate(labels):
  y=148-i*35;box(d,9,y,CW-18,25,label)
  if i<4:arrow(d,CW/2,y,CW/2,y-10)
 return d

figure_num=0

def figure(name):
 global figure_num
 figure_num+=1
 assets={
 'vehicle':('Models/preview.png',161,'Earlier assembly visual reference; current mount, stack and endcap revisions are documented separately.'),
 'mounts':('Hardware/ThrusterMounts/preview.png',132,'Current integral rail-slot mount. CAD fit is not printed-part load qualification.'),
 'stack':('Hardware/StackAdapter/adapter_preview.png',126,'ARK/ESC adapter and spacer geometry. The board pattern is 20 x 20 mm above 30.5 x 30.5 mm.'),
 'fpv':('Simulation/ClubROVSim/Assets/Screenshots/betaflight-style-osd.png',145,'Unity FPV with Betaflight-style instruments. Readouts are simulation truth, not physical sensor validation.'),
 }
 if name in assets:
  path,maxh,cap=assets[name]
  with PILImage.open(ROOT/path) as im:iw,ih=im.size
  scale=min(CW/iw,maxh/ih);obj=Image(str(ROOT/path),iw*scale,ih*scale);obj.hAlign='CENTER'
 else:
  obj={'architecture':architecture,'power':power,'ota':ota}[name]()
  cap={'architecture':'Functional architecture. Commands travel downward; telemetry and video return to the surface. The IMX219 connects through CSI.','power':'Proposed power domains. Dashed distribution block remains provisional; this is not a fabrication wiring diagram.','ota':'Application-update state sequence. Unknown/armed state or invalid input prevents a normal update.'}[name]
 return KeepTogether([Spacer(1,4),obj,Paragraph(f'Fig. {figure_num}. {cap}',styles['caption'])])

def evidence_table():
 rows=[['Requirement','Current evidence','Remaining qualification'],
 ['R1 motion','CAD axes; simulated motion and SITL checks','Physical neutral, mapping and reversing thrust'],
 ['R2 tether/video','Bench telemetry; about 25 fps H.264 reception','Latency, endurance and failure response'],
 ['R3 service','Adapter fit; CAD packaging review','Screw/battery clearance and full harness fit'],
 ['R4 failsafe','OTA guards; simulated ownership/timeout','Physical input/link/power-loss tests'],
 ['R5 containment','Endcap/coupon geometry only','Seal/connector fit and pressure qualification'],
 ['R6 training','Local/SITL separation; maps and UI checks','Dynamics calibration and full-course trials']]
 data=[[Paragraph(html.escape(t),styles['headcell' if i==0 else 'cell']) for t in row] for i,row in enumerate(rows)]
 table=Table(data,colWidths=[58,98,CW-156],repeatRows=1,hAlign='CENTER')
 table.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),INK),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),4),('RIGHTPADDING',(0,0),(-1,-1),4),('TOPPADDING',(0,0),(-1,-1),5),('BOTTOMPADDING',(0,0),(-1,-1),5),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,LIGHT]),('LINEBELOW',(0,0),(-1,0),.5,INK),('LINEBELOW',(0,-1),(-1,-1),.5,INK)]))
 return [Paragraph('TABLE I. REQUIREMENT-TO-EVIDENCE MAP',styles['caption']),table,Spacer(1,8)]

def measurement_table():
 rows=[['Quantity','Recorded value / evidence'],
 ['Envelope L x W x H','361.920 x 377.472 x 260.000 mm; CAD snapshot only'],
 ['Dry mass / balance','Pending as-built weighing and center-of-mass measurement'],
 ['Displacement / trim','Pending water measurements with final accessories'],
 ['Depth envelope','Unqualified; initial shallow-water procedure required'],
 ['Nominal pack energy','48.84 Wh calculation at 14.8 V; usable energy unmeasured'],
 ['Power / endurance','Pending propulsion and PoE logs plus delivered energy'],
 ['Thrust curves','Pending both-direction command/force measurements'],
 ['Temperature','Pending enclosure/component logs under representative load'],
 ['Video throughput','About 25 frames/s at 1632 x 1232 in a short bench run'],
 ['Video latency','Pending glass-to-glass measurement'],
 ['Installed cost','Pending verified purchases and reused-equipment accounting']]
 data=[[Paragraph(html.escape(t),styles['headcell' if i==0 else 'cell']) for t in row] for i,row in enumerate(rows)]
 t=Table(data,colWidths=[85,CW-85],repeatRows=1,hAlign='CENTER')
 t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),INK),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),5),('RIGHTPADDING',(0,0),(-1,-1),5),('TOPPADDING',(0,0),(-1,-1),6),('BOTTOMPADDING',(0,0),(-1,-1),6),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,LIGHT]),('LINEBELOW',(0,-1),(-1,-1),.5,INK)]))
 return [FrameBreak(),Paragraph('APPENDIX A. AS-BUILT CLOSEOUT RECORD',styles['h2']),Paragraph('TABLE II. MEASUREMENTS AND EVIDENCE STATES',styles['caption']),t,Spacer(1,9)]

class ReportDoc(BaseDocTemplate):
 def afterFlowable(self,flowable):
  if isinstance(flowable,Paragraph) and flowable.style.name=='h2':
   title=flowable.getPlainText();key='section-'+re.sub('[^a-z0-9]','',title.lower());self.canv.bookmarkPage(key);self.canv.addOutlineEntry(title,key,0,False)

def chrome(c,doc):
 c.saveState();c.setFillColor(INK);c.setFont('Helvetica',8)
 c.drawString(M,H-25,'CLUB ROV  |  TECHNICAL DESIGN REPORT')
 c.drawRightString(W-M,H-25,str(doc.page))
 c.setStrokeColor(colors.HexColor('#a4b4bd'));c.setLineWidth(.45);c.line(M,35,W-M,35)
 c.setFont('Helvetica',7.2);c.drawString(M,24,'Rev 0.1 - Integration draft  |  26 October 2026');c.drawRightString(W-M,24,'Evidence baseline: e9bd47d')
 if doc.page==1:
  c.setFont('Times-Bold',21);c.drawCentredString(W/2,H-68,'Club ROV: A Modular Six-Thruster');c.drawCentredString(W/2,H-93,'Training Platform')
  c.setFont('Times-Roman',11);c.drawCentredString(W/2,H-113,'Technical Design Report - Club ROV project')
  c.setFont('Helvetica',8.2);c.drawCentredString(W/2,H-131,'Revision 0.1 | 26 October 2026 | Hardware and simulator evidence: 20-24 October 2026')
 c.restoreState()

def build():
 OUT.parent.mkdir(parents=True,exist_ok=True)
 doc=ReportDoc(str(OUT),pagesize=(W,H),leftMargin=M,rightMargin=M,topMargin=44,bottomMargin=46,title='Club ROV: A Modular Six-Thruster Training Platform',author='Club ROV project',subject='Technical Design Report, revision 0.1 integration draft',pageCompression=1)
 frames=lambda top:[Frame(M,46,CW,top-46,leftPadding=0,rightPadding=0,topPadding=0,bottomPadding=0,id='left'),Frame(M+CW+GAP,46,CW,top-46,leftPadding=0,rightPadding=0,topPadding=0,bottomPadding=0,id='right')]
 doc.addPageTemplates([PageTemplate(id='first',frames=frames(H-153),onPage=chrome,autoNextPageTemplate='normal'),PageTemplate(id='normal',frames=frames(H-44),onPage=chrome)])
 source=SOURCE.read_text(encoding='utf8');source=source[source.index('<!-- page:'):]
 source=re.sub(r'<!-- page: .*? -->','',source)
 story=[];abstract=False;references=False
 for block in re.split(r'\n\s*\n',source.strip()):
  block=block.strip()
  if not block:continue
  m=re.fullmatch(r'<!-- figure: (\w+) -->',block)
  if m:story.append(figure(m.group(1)));continue
  if block=='<!-- table: evidence -->':story.extend(evidence_table());continue
  if block=='<!-- appendix: measurements -->':story.extend(measurement_table());references=False;continue
  if block.startswith('### '):story.append(Paragraph(ptext(block[4:]),styles['h3']));abstract=False;continue
  if block.startswith('## '):
   title=block[3:];references=title=='References and evidence';abstract=title=='Abstract';story.append(Paragraph(ptext(title.upper() if not abstract else title),styles['h2']));continue
  style=styles['abstract' if abstract else 'reference' if references else 'body']
  story.append(Paragraph(ptext(' '.join(block.splitlines())),style))
 doc.build(story)
 print(OUT)

if __name__=='__main__':build()

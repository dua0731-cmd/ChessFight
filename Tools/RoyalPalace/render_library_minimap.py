"""Top-down layout diagram from the saved prefab anchors; not a Unity minimap HUD."""
from pathlib import Path
import sys,re
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tests/Gameplay'))
from test_obstacle_assets import blocks,fields
from test_royal_palace_courses import number,transform
records=blocks((ROOT/'Assets/Prefabs/Obstacles/WarRoomComplete.prefab').read_text())
names={int(k):fields(v)['m_Name'] for k,v in records.items() if v.startswith('--- !u!1 ')}
trans={number(fields(v)['m_GameObject']):int(k) for k,v in records.items() if v.startswith('--- !u!4 ')}
image=Image.new('RGB',(1450,1350),'#101b29');d=ImageDraw.Draw(image)
font='/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc'
def f(n):return ImageFont.truetype(font,n)
def point(x,z):return (520+x*3,175+(170-z)*3)
def rect(x0,z0,x1,z1,fill,outline=None,width=2):
 a=point(x0,z1);b=point(x1,z0);d.rectangle((*a,*b),fill=fill,outline=outline,width=width)
def text(p,s,n=20,color='#dce5ee'):d.text(p,s,font=f(n),fill=color)
text((60,35),'왕의 서재 · 코스 미니맵',40)
text((62,96),'저장된 프리팹 배치 기준  |  아래에서 진입 → 위쪽 출구',21,'#a9bed2')
rect(-124,-10,124,170,'#263342','#72869a',4)
rect(-38,-150,38,-10,'#263342','#72869a',4)
for name in ('Approach floor -146','Dispatch table after jump','Approach floor -60','Approach floor -27','Fixed seal island','Typewriter furniture base','Final exit walkway'):
 go=next(k for k,v in names.items() if v==name);tid=trans[go]
 p=transform(records,tid,(-.5,0,-.5));q=transform(records,tid,(.5,0,.5));rect(p[0],p[2],q[0],q[2],'#85634b','#c7a47b')
for z in (-100,-76):
 rect(-9,z,9,z+16,'#e7dec3','#bfa77b');rect(-27,z, -9,z+16,'#ab8bb5','#d3bde0')
rect(-8,-51,8,-27,'#161b24','#93a4b2')
for z in range(8):
 for x in range(8):rect(-64+x*16,z*16,-48+x*16,z*16+16,'#c8c2ae' if (x+z)%2==0 else '#485568')
for x in (-86,86):
 rect(x-11,51,x+11,77,'#866048','#c4a474');text((point(x,64)[0]-19,point(x,64)[1]-13),'GM',18)
for go,name in names.items():
 p=transform(records,trans[go]);x,z=p[0],p[2]
 if name.startswith('Royal library bookcase'):rect(x-1.8,z-7,x+1.8,z+7,'#c19152','#ead0a0')
 elif name.startswith(('Giant chess conservatory plant','Library gallery houseplant')):
  px,py=point(x,z)
  for dx,dy in ((-5,0),(5,0),(0,-5),(0,5)):d.ellipse((px+dx-8,py+dy-8,px+dx+8,py+dy+8),fill='#4a9775')
 elif name.startswith('Random GM replay preview'):
  px,py=point(x,z);d.rectangle((px-6,py-11,px+6,py+11),fill='#b9d8e5',outline='#f6ce72',width=2)
rect(95,47,118,82,'#705b7d','#b79cc5')
# Route denotes direction, not safe chess squares or correct keyboard letters.
for z in range(-140,165,7):d.line((point(0,z),point(0,z+3)),fill='#f5cf76',width=4)
for z in (-130,-55,0,64,157):
 px,py=point(0,z);d.polygon(((px,py-8),(px-7,py+6),(px+7,py+6)),fill='#f5cf76')
px,py=point(0,-136.4);d.ellipse((px-23,py-11,px+23,py+11),outline='#74e4eb',width=4)
stages=[(-136.4,'차원문 · 좁은 통로에서 서재 공개'),(-122,'도장 탁자'),(-92,'덮히는 책 ①'),(-80,'인장 휴식 탁자'),(-68,'덮히는 책 ②'),(-43,'기억 타자기'),(64,'왕의 거대 체스 탁자'),(163,'다음 구간 출구')]
for i,(z,label) in enumerate(stages,1):
 px,py=point(0,z);x=px+54 if i%2 else px-54
 d.line((px,py,x,py),fill='#f5cf76',width=2);d.ellipse((x-13,py-13,x+13,py+13),fill='#e7bd63');text((x-6,py-12),str(i),17,'#182333')
 text((990,220+(i-1)*64),f'{i:02}  {label}',19)
text((990,155),'진행 순서',28)
text((990,790),'비대칭 벽면 구성',27)
for i,line in enumerate(['서쪽: 연속 책장 · 서고','동쪽: 창문 · 독서 의자 · 화초','액자 4개: 선택된 기보 예고','대형 화초 7개: 동선 바깥 배치']):text((990,845+i*39),line,19)
text((990,1040),'표기',25)
for y,c,label in [(1090,'#c19152','책장'),(1130,'#4a9775','화초'),(1170,'#b9d8e5','기보 액자')]:
 d.rectangle((990,y,1008,y+18),fill=c);text((1022,y-5),label,19)
text((60,1250),'※ 화살표는 진행 방향입니다. 체스판의 안전 칸이나 타자기 정답 경로를 표시하지 않습니다.',19,'#a9bed2')
image.save(ROOT/'Docs/PawnRush/ROYAL_LIBRARY_MINIMAP.png')

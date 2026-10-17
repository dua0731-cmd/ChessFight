"""Build the Korean chess timer design PDF. Requires reportlab and Noto Sans KR TTFs.
Usage: python3 Tools/Documents/build_chess_timer_design.py FONT_DIRECTORY
FONT_DIRECTORY contains NotoSansKR-400.ttf and NotoSansKR-700.ttf (SIL OFL).
"""
from pathlib import Path
import sys
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor, Color, white
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph, Table, TableStyle
from reportlab.lib.styles import ParagraphStyle

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Docs/KingRush/CHESS_TIMER_DESIGN.pdf'
FONT = Path(sys.argv[1])
for name, weight in [('KR',400),('KB',700)]:
    pdfmetrics.registerFont(TTFont(name, str(FONT/f'NotoSansKR-{weight}.ttf')))
W,H = 595.276,841.89
INK=HexColor('#172B36'); MUTED=HexColor('#566773'); BLUE=HexColor('#2A83AE')
CORAL=HexColor('#CE6653'); PAPER=HexColor('#F8F5EE'); LINE=HexColor('#DEDCD4')
c=canvas.Canvas(str(OUT),pagesize=(W,H))
c.setTitle('체스 타이머 폭탄 | 미니게임 기획서')
c.setAuthor('ChessFight · 기획 정리')
style=ParagraphStyle('body',fontName='KR',fontSize=10.5,leading=17,textColor=INK,wordWrap='CJK')
page_no=0
def text(x,y,s,size=11,bold=False,color=INK):
    c.setFont('KB' if bold else 'KR',size);c.setFillColor(color);c.drawString(x,H-y-size,s)
def para(y,s,x=42,w=511,size=10.5,color=INK):
    st=ParagraphStyle('p',parent=style,fontSize=size,leading=size*1.62,textColor=color)
    p=Paragraph(s,st);pw,ph=p.wrap(w,H)
    assert y+ph<790,(page_no,s[:50],y+ph)
    p.drawOn(c,x,H-y-ph);return y+ph
def box(x,y,w,h,fill=PAPER):
    c.setFillColor(fill);c.roundRect(x,H-y-h,w,h,9,stroke=0,fill=1)
def heading(y,n,title):
    text(42,y,n,10,True,BLUE);text(76,y-4,title,18,True);return y+34
def begin(kicker,title,sub):
    global page_no
    page_no+=1
    c.setFillColor(PAPER);c.rect(0,0,W,H,fill=1,stroke=0)
    c.setFillColor(BLUE);c.rect(42,H-43,28,4,fill=1,stroke=0)
    text(80,31,'CHESS FIGHT / DESIGN NOTE',9,True,MUTED)
    text(42,68,kicker,10,True,BLUE);text(42,90,title,26,True)
    para(132,sub,size=10,color=MUTED)
def finish():
    c.setStrokeColor(LINE);c.line(42,45,W-42,45)
    text(42,H-34,'체스 타이머 미니게임 · v1.0 · 2026.10.18',8,color=MUTED)
    text(W-75,H-35,f'{page_no:02d} / 06',9,True,MUTED)
    c.showPage()
def card(y,title,body,h=91,tag=''):
    box(42,y,511,h,white);text(57,y+12,title,13,True)
    para(y+38,body,x=57,w=481,size=10)
    if tag:text(440,y+12,tag,8,True,BLUE)
def table(y,headers,rows,widths):
    def p(s,b=False):return Paragraph(s,ParagraphStyle('cell',parent=style,fontName='KB' if b else 'KR',fontSize=9.3,leading=14.5))
    data=[[p(s,True) for s in headers]]+[[p(s) for s in row] for row in rows]
    t=Table(data,colWidths=widths,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),HexColor('#E3EDF0')),
      ('ROWBACKGROUNDS',(0,1),(-1,-1),[white,HexColor('#F1F1EB')]),
      ('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),10),
      ('RIGHTPADDING',(0,0),(-1,-1),10),('TOPPADDING',(0,0),(-1,-1),10),('BOTTOMPADDING',(0,0),(-1,-1),10)]))
    _,h=t.wrap(511,700); assert y+h<790;(t.drawOn(c,42,H-y-h));return y+h

begin('01 / CONCEPT','체스 타이머 폭탄','상단에서는 시간을 빼앗고, 하단에서는 정답을 찾는다.')
c.drawImage(str(ROOT/'Docs/KingRush/Concepts/CHESS_TIMER_CONCEPT.png'),42,H-183-213,width=511,height=213)
para(408,'AI 생성 컨셉아트 · 왼쪽: 단일 타이머 위 난투 / 오른쪽: 삼색 전선 해체 장치',size=8.5,color=MUTED)
para(430,'외형과 분위기를 위한 예시 이미지다. 실제 스테이지 구조와 정답 판정은 본문의 도식·규칙을 기준으로 한다.',size=9,color=MUTED)
heading(482,'01','한 줄 기획')
para(518,'거대한 체스 타이머 위에서 상대를 밀어 떨어뜨리고, 아래에서는 폭탄을 해체하는 팀 경쟁 미니게임. 발판을 지킬 인원과 퍼즐을 풀 인원의 배분이 핵심이다.',size=12)
card(590,'공격에는 대가가 있다','상대 버튼으로 넘어가면 그쪽 인원이 늘어난다. 상대를 방해하는 동안 우리 타이머가 흐를 수 있으므로, 공격과 시간 보호 사이에서 선택해야 한다.',97)
para(710,'문서 범위: 체스 타이머 미니게임만 정리. 일반 장애물·다른 기물 코스는 제외.<br/>표기: [확정] 사용자 합의 규칙 / [시험값] 현재 프로토타입 수치 / [기획안] 실제 퍼즐 제안.',size=9,color=MUTED)
finish()

begin('02 / SPACE & TEAMPLAY','하나의 시계, 두 개의 역할','[확정] 한 몸체와 연동되는 좌우 버튼. 공용 상단에서 싸우고, 하부 해체 공간은 벽으로 나눈다.')
# Conceptual plan, deliberately not a construction drawing.
box(42,180,511,282,white)
text(58,191,'평면 배치 개념도 · 축척 없음',9,True,MUTED)
box(88,220,178,32,HexColor('#D9EAF2'));text(132,226,'청팀 출구',11,True)
box(329,220,178,32,HexColor('#F6DED6'));text(372,226,'홍팀 출구',11,True)
box(88,267,178,56,HexColor('#EAF3F7'));text(120,285,'청팀 해체 구역',11,True)
box(329,267,178,56,HexColor('#FBECE6'));text(362,285,'홍팀 해체 구역',11,True)
c.setFillColor(INK);c.rect(291,H-326,12,106,fill=1,stroke=0)
box(107,340,381,64,INK);text(128,350,'타이머 상단: 서로 건너가 난투',12,True,white)
text(142,376,'왼쪽 버튼',10,color=white);text(351,376,'오른쪽 버튼',10,color=white)
box(58,339,38,82,HexColor('#D9EAF2'));box(500,339,38,82,HexColor('#F6DED6'))
text(140,432,'입구 → 양쪽 경사로 → 공용 상단',11,True)
y=heading(485,'02','인원으로 스위치를 누른다')
y=para(y,'현재 6 대 6 시험. 소속과 무관하게 각 버튼에 접촉한 사람 수를 센다. 넘어진 사람도 포함하고, 여러 팔다리가 닿아도 한 명으로 계산한다.')
y=para(y+10,'많은 쪽은 내려가며 그쪽 타이머가 정지한다. 반대쪽은 올라가며 시간이 흐른다. 동수면 수평으로 돌아가고 양쪽 시간이 흐른다.')
table(610,['배치 변화','시간에 생기는 결과'],[
 ['청측 4명 : 홍측 2명','청측 타이머 정지'],
 ['청팀 1명이 홍측으로 이동 → 3 : 3','양쪽 시간 감소'],
 ['청팀 1명 더 이동 → 2 : 4','홍측 정지, 청측 시간 감소']], [245,266])
finish()

begin('03 / RESOLUTION','해체와 시간 만료의 결말','[확정] 폭발해도 길은 열린다. 폭발·입구 복귀가 다음 구간으로 향하는 시간 손실을 만든다.')
y=table(182,['발생 조건','폭발 대상','문 / 결과'],[
 ['한 팀이 먼저 해체','맵 안 상대 팀','해체한 팀 문 열림 · 먼저 해체한 팀 승리'],
 ['동시 해체, 잔여 시간 다름','시간이 적은 팀의 맵 안 인원','양쪽 문 열림 · 시간이 많은 팀 승리'],
 ['동시 해체, 잔여 시간 동일','맵 안 양 팀','양쪽 문 열림 · 동률'],
 ['한 팀 타이머가 0초','해당 팀 전원','해당 팀 문 즉시 열림 · 타이머 재충전 없음'],
 ['양 팀 타이머가 0초','양 팀 전원','양쪽 문 즉시 열림']], [160,139,212])
y=heading(y+29,'03','폭발 → 비행 → 입구 복귀')
y=para(y,'눈에 띄는 폭발 이펙트와 함께 멀리 날아가고, 폭발부터 리스폰까지 3초 이내로 끝낸다. 현재 기본값은 2.4초. 성공팀은 직접 폭발·기절·강제 리스폰을 받지 않는다.')
y=para(y+12,'예외: 폭발하는 적을 손으로 잡고 있었다면 함께 날아간다. 잡은 사람은 폭발 대상이 아니므로 별도 강제 리스폰을 적용하지 않는다. 충돌과 낙사는 일반 물리 처리를 따른다.')
y=para(y+14,'[현재 판정] 동시는 같은 물리 업데이트에서 완료된 경우. 잔여 시간 차이 0.001초 이하는 계산 오차를 고려해 동률로 본다. 시간 만료와 해체 완료가 겹치면 해당 팀의 만료가 우선이다.',size=9.5,color=MUTED)
para(y+12,'[현재 시험 흐름] 자동 종료나 다음 씬 전환 없이 계속 플레이한다. 먼저 결정된 승자는 이후 상대의 늦은 해체로 뒤집히지 않는다. 실제 코스 연결은 후속 작업이다.',size=9.5,color=MUTED)
finish()

begin('04 / DEFUSAL PUZZLE','삼색 전선 × 체스','[기획안 · 미구현] 체스판을 읽고, 공격받지 않는 칸에 연결된 전선을 자른다.')
para(180,'빨강·파랑·노랑 전선이 작은 4×4 체스판의 서로 다른 칸에 연결된다. 기물이 공격하는 칸을 피한 전선 하나가 정답이다. 색을 외우는 대신 위치와 공격 범위를 판단한다.',size=11)
# Exact, solvable example: rook B2; red B4, blue D2, yellow D4.
sx,sy,cell=70,284,53
for row in range(4):
 for col in range(4):
  c.setFillColor(HexColor('#F1E9D9') if (row+col)%2==0 else HexColor('#B5A58F'))
  c.rect(sx+col*cell,H-sy-(row+1)*cell,cell,cell,fill=1,stroke=0)
  rank=4-row
  if col==1 or rank==2:
   c.setFillColor(Color(.83,.36,.28,alpha=.25))
   c.rect(sx+col*cell,H-sy-(row+1)*cell,cell,cell,fill=1,stroke=0)
for col,label in enumerate('ABCD'):text(sx+col*cell+21,sy+4*cell+9,label,10,True)
for row in range(4):text(sx-18,sy+row*cell+18,str(4-row),10,True)
def marker(col,rank,label,color):
 x=sx+col*cell+cell/2;y=sy+(4-rank)*cell+cell/2
 c.setFillColor(color);c.circle(x,H-y,15,fill=1,stroke=0);text(x-5,y-9,label,11,True,white)
marker(1,4,'1',CORAL);marker(3,2,'2',BLUE);marker(3,4,'3',HexColor('#A77F11'))
text(sx+cell+12,sy+2*cell+17,'룩',15,True)
text(315,290,'예시 정답: 노랑 ③',15,True)
para(329,'룩의 위치는 B2.<br/>① 빨강 B4: 같은 열 → 위험<br/>② 파랑 D2: 같은 행 → 위험<br/>③ 노랑 D4: 공격 밖 → 안전',x=315,w=224,size=10.5)
para(426,'붉은 음영은 설명용 공격 범위다. 실제 플레이에서는 기물을 선택해 힌트를 확인하도록 제안한다.',x=315,w=224,size=9,color=MUTED)
y=heading(553,'04','읽기 → 선택 → 절단 → 확인')
para(y,'기물과 연결 칸을 관찰하고 전선을 선택한다. 1초간 누르면 절단을 확정한다. 정답이면 다음 단계, 마지막 단계면 해체 완료 판정으로 연결한다.')
card(640,'한 명도 풀 수 있고, 둘이면 함께 판단한다','체스 초보에게는 기물 이동 방향을 시각적으로 안내한다. 색 외에 ①·②·③ 번호와 도형도 함께 표시한다. 실제 퍼즐에서는 인원수에 따른 작업 속도 배율을 없애는 방향을 제안한다.',106)
finish()

begin('05 / DIFFICULTY','단순한 입력, 점점 깊어지는 판단','[기획안] 세 단계 구성. 배치와 색은 바꾸되, 정답은 항상 하나만 존재하도록 만든다.')
card(180,'1단계 · 안전한 칸 찾기','룩 한 개의 가로·세로 공격을 피한다. 4×4 판, 전선 세 개, 정답 하나. 기물 선택 시 공격 방향을 보여주어 학습과 첫 성공을 돕는다.',103,'우선 제작')
card(297,'2단계 · 두 기물의 공격 조합','룩과 비숍 또는 나이트가 함께 등장한다. 두 기물 모두에게 공격받지 않는 전선을 찾는다. 가림과 공격 경로는 실제 체스 규칙에 맞춰 일관되게 표시한다.',103,'확장')
card(414,'3단계 · 한 수 뒤를 예측','“나이트가 표시된 칸으로 이동한 뒤” 안전한 전선을 찾는다. 이동 전·후 미리보기를 제공해 지식 시험보다 관찰·판단 문제가 되게 한다.',103,'확장')
y=heading(546,'05','오답은 긴장을 주고, 정답은 납득되게')
y=para(y,'오답 시 팀 타이머 5초 차감 + 해당 장치 2초 잠금. 이미 완료한 단계는 유지한다. 발판으로 시간을 멈춘 상태에서도 오답 차감은 적용하며, 0초가 되면 만료 폭발 규칙을 따른다.')
y=para(y+12,'동일 단계의 틀린 전선은 표시하고, 왜 틀렸는지 공격 경로를 짧게 보여준다. 색·기물·연결 칸을 바꾸는 문제 은행으로 반복 암기를 줄인다. 무작위 배치 뒤에는 정답 수가 정확히 하나인지 검증한다.')
para(y+15,'추가 검토: 5초 차감만으로 찍기 풀이를 충분히 억제하는지 측정한다. 부족할 경우 단계 내 연속 오답 페널티 증가를 검토한다. 두 팀에는 풀이 난이도가 동등한 문제를 제공한다.',size=9.5,color=MUTED)
finish()

begin('06 / PROTOTYPE & NEXT','현재 시험 버전과 다음 제작 범위','규칙·공간·역할 분배는 시험 중이다. 실제 삼색 전선 퍼즐은 다음 구현 대상이다.')
y=table(180,['항목','현재 상태 / 기준'],[
 ['플레이와 공간','기존 래그돌 12명, 6 대 6 오프라인 시험. 54×54m 경기장, 양쪽 경사로와 하부 중앙벽.'],
 ['타이머 상판','Coupled white rocker Scale (1, 0.7, 1). 중심 Y=5m, 실제 두께 1.4m. 좌우 연동 최대 ±3°.'],
 ['임시 해체','F 길게 누르기 3단계. 단계당 30 인·초, 최대 3명 합산. 1명 90초 / 3명 30초. 실제 퍼즐은 미구현.'],
 ['시간과 폭발','타이머 기본 45초. 시간 정지 전환 안정 시간 0.35초. 폭발 후 기본 2.4초 입구 복귀.'],
 ['실제 퍼즐로 전환 시','임시 작업량·인원 배율을 그대로 옮기지 않는다. 3개 문제의 풀이 시간과 오답 비용을 기준으로 다시 조절한다.'],
 ['정식 연결','온라인 동기화·실제 다음 구간 이동은 미연결. 테스트는 문이 열린 뒤에도 계속된다.']], [112,399])
y=heading(y+28,'06','첫 제작 범위')
y=para(y,'4×4 판 + 룩 한 개 + 삼색 전선 + 안전한 칸 하나 찾기를 먼저 만든다. 전선 선택·1초 절단·정답/오답 피드백·타이머 차감·문/폭발 연결까지 한 흐름으로 시험한다.')
y=para(y+12,'확인할 것: 처음 보는 사람이 설명 없이 풀 수 있는가, 정답을 납득하는가, 위쪽 난투와 함께해도 집중할 수 있는가, 1인/2인 협력의 차이가 적절한가, 두 팀의 문제 난이도가 공정한가.')
para(y+16,'기준 문서: CHESS_TIMER_MINIGAME.md / 규칙 구현 R90.<br/>현재 자동 검사: 구조 26 · Core 94 · 모의 세션 39 · 런타임 7개 컴파일 통과.<br/>자동 검사는 Unity에서의 실제 연출·물리·플레이 감각 검증을 대신하지 않는다.',size=8.5,color=MUTED)
finish()
c.save()
print(OUT)

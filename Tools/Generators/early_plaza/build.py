# -*- coding: utf-8 -*-
# Builds the early mini-game section sheet (R153): Docs/KingRush/PAWN_RUSH_EARLY_PLAZA.html, or the path given.
#   python Tools/Generators/early_plaza/build.py [out.html]
# The page is also published as an Artifact; its skeleton adds <!doctype>, charset and viewport, so the file
# starts at <title>. Open it in a browser as UTF-8.
import io, os, sys
import figs

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Docs", "KingRush", "PAWN_RUSH_EARLY_PLAZA.html")

CSS = r"""
:root{
  /* Layout: a drawing set - title block, then sheets top to bottom, plans at true scale with keyed callouts. */
  --paper:#EEF0EB; --panel:#F7F8F4; --ink:#1C2028; --ink2:#5B616C; --rule:#D3D6CE;
  --accent:#4C55A8; --on-accent:#FFFFFF;
  --gold:#E2B23C; --gold-ink:#8E6610; --wt:#3FC1D9; --bt:#F08A2C; --wt-ink:#157E97; --bt-ink:#AE5810;
  --pave:#E4DCC7; --pave2:#D3C9AF; --lawn:#B9C79E; --cypress:#6E8656; --cut:#7B7462; --cut2:#958D78;
  --wall:#C3B9A1; --wall2:#B4A98F; --dais:#2C303B; --board:#4A5060; --water:#3A9894; --walnut:#6B4428;
  --maple:#E3C391; --lane:#565C6B; --iron:#73777F; --marble:#ECE8DD; --sky:#E1E7EA; --door:#3A3530;
  --banner:#EFE6D2; --window:#56606E;
  --good-bg:#DCEAD3; --good-fg:#24572F; --fix-bg:#F3E2B3; --fix-fg:#674700;
  --f-disp:"Hahmlet","Noto Serif KR","Batang",serif;
  --f-body:"IBM Plex Sans KR","Apple SD Gothic Neo","Malgun Gothic",sans-serif;
  --f-mono:"IBM Plex Mono",ui-monospace,"SFMono-Regular",Consolas,monospace;
}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){
  --paper:#13161C; --panel:#1A1E26; --ink:#E6E2D7; --ink2:#A1A6AE; --rule:#2D323C;
  --accent:#A3AAF2; --on-accent:#14172A; --gold-ink:#E2B23C; --wt-ink:#66D4E8; --bt-ink:#F6A45E;
  --pave:#4A463C; --pave2:#5B5649; --lawn:#3B4832; --cypress:#5C7449; --cut:#8F8772; --cut2:#A39B85;
  --wall:#6A6454; --wall2:#7A7362; --water:#2E7F7B; --walnut:#835A39; --maple:#CFAF7F; --lane:#646B79;
  --iron:#8C9098; --marble:#3B3A35; --sky:#1B2129; --door:#2A2622; --banner:#CFC5AE; --window:#2A313B;
  --good-bg:#21382A; --good-fg:#A9DAB3; --fix-bg:#45380F; --fix-fg:#F2D58A; color-scheme:dark}}
:root[data-theme="dark"]{
  --paper:#13161C; --panel:#1A1E26; --ink:#E6E2D7; --ink2:#A1A6AE; --rule:#2D323C;
  --accent:#A3AAF2; --on-accent:#14172A; --gold-ink:#E2B23C; --wt-ink:#66D4E8; --bt-ink:#F6A45E;
  --pave:#4A463C; --pave2:#5B5649; --lawn:#3B4832; --cypress:#5C7449; --cut:#8F8772; --cut2:#A39B85;
  --wall:#6A6454; --wall2:#7A7362; --water:#2E7F7B; --walnut:#835A39; --maple:#CFAF7F; --lane:#646B79;
  --iron:#8C9098; --marble:#3B3A35; --sky:#1B2129; --door:#2A2622; --banner:#CFC5AE; --window:#2A313B;
  --good-bg:#21382A; --good-fg:#A9DAB3; --fix-bg:#45380F; --fix-fg:#F2D58A; color-scheme:dark}

*{box-sizing:border-box}
body{background:var(--paper);color:var(--ink);font:15px/1.68 var(--f-body);-webkit-font-smoothing:antialiased}
.wrap{max-width:1200px;margin:0 auto;padding-left:max(16px,env(safe-area-inset-left,0px));
  padding-right:max(16px,env(safe-area-inset-right,0px));padding-block:30px 72px}
h1,h2,h3{font-family:var(--f-disp);text-wrap:balance;margin:0;font-weight:700;letter-spacing:-.01em}
code{font-family:var(--f-mono);font-size:.88em;background:var(--panel);border:1px solid var(--rule);padding:0 .3em;border-radius:3px}
b{font-weight:600}
.mono{font-family:var(--f-mono)}

header.sheet{display:grid;grid-template-columns:minmax(0,1.55fr) minmax(0,1fr);gap:32px;
  padding-bottom:26px;border-bottom:2px solid var(--ink)}
.eyebrow{font:500 12px/1.4 var(--f-mono);letter-spacing:.08em;color:var(--ink2)}
h1{font-size:clamp(36px,5.4vw,56px);line-height:1.08;margin:10px 0 14px}
.lede{font-size:16.5px;max-width:60ch;margin:0}
.tb{border:1.5px solid var(--ink);align-self:end;font-size:13px;background:var(--panel)}
.tb div{display:grid;grid-template-columns:86px minmax(0,1fr)}
.tb div+div{border-top:1px solid var(--rule)}
.tb dt{padding:7px 10px;color:var(--ink2);font-size:12px;border-right:1px solid var(--rule)}
.tb dd{margin:0;padding:7px 10px}
.tb dd.state{font-weight:600}

section{padding-block:38px;border-bottom:1px solid var(--rule)}
.sh{display:flex;align-items:baseline;gap:14px;flex-wrap:wrap;margin-bottom:12px}
.sh .no{font:500 12px/1 var(--f-mono);letter-spacing:.06em;color:var(--accent)}
h2{font-size:27px;line-height:1.25}
.intro{max-width:72ch;margin:0 0 20px}
figure{margin:0}
.scroll{overflow-x:auto;-webkit-overflow-scrolling:touch;border:1px solid var(--rule);background:var(--panel);border-radius:4px}
.scroll>svg{min-width:720px}
.scroll.slim>svg{min-width:300px}
figcaption{font-size:13px;color:var(--ink2);margin-top:9px;max-width:84ch}
.points{margin:18px 0 0;padding-left:1.1em;max-width:80ch;display:grid;gap:6px}

.grid2{display:grid;grid-template-columns:minmax(0,1.45fr) minmax(0,1fr);gap:30px;align-items:start}
.stack{display:grid;gap:18px;min-width:0}
.grid2>*,.stack>*,figure{min-width:0}
@media (max-width:920px){header.sheet,.grid2{grid-template-columns:minmax(0,1fr)}}

ol.keys{list-style:none;counter-reset:k;padding:0;margin:0;display:grid;gap:7px;font-size:13.5px;line-height:1.55}
ol.keys li{counter-increment:k;display:grid;grid-template-columns:26px minmax(0,1fr);gap:6px}
ol.keys li::before{content:counter(k);width:19px;height:19px;border-radius:50%;background:var(--ink);color:var(--paper);
  font:700 10.5px/19px var(--f-mono);text-align:center;margin-top:1px}

.game{padding-block:34px 8px}
.game+.game{border-top:1px dashed var(--rule)}
.game-head{display:flex;gap:16px;align-items:center;margin-bottom:16px}
.tag{flex:none;font-family:var(--f-disp);font-weight:700;font-size:28px;width:52px;height:52px;display:grid;place-items:center;
  background:#2C303B;color:#E2B23C;border-radius:4px;box-shadow:inset 0 0 0 2px #E2B23C}
.game-head h3{font-size:22px;line-height:1.3}
.meta{font:500 12.5px/1.5 var(--f-mono);color:var(--ink2)}
.game .lede2{margin:0 0 16px;max-width:72ch}
.note{margin:0;font-size:14px;display:grid;grid-template-columns:86px minmax(0,1fr);gap:10px;padding-top:10px;border-top:1px solid var(--rule)}
.note span{font:600 11.5px/1.9 var(--f-mono);letter-spacing:.04em;color:var(--accent)}

.tablewrap{overflow-x:auto;-webkit-overflow-scrolling:touch}
table.pairs{width:100%;min-width:900px;border-collapse:collapse;table-layout:fixed;font-size:13.5px;line-height:1.55;background:var(--panel)}
.pairs th,.pairs td{border:1px solid var(--rule);padding:11px 12px;vertical-align:top;text-align:left}
.pairs thead th{font-weight:600;background:var(--paper)}
.pairs thead th small,.pairs tbody th small{display:block;font-weight:400;color:var(--ink2);font-size:12px;margin-top:3px}
.pairs tbody th{font-weight:600}
.pairs col.c0{width:19%}
.chip{display:inline-block;font:600 11.5px/1 var(--f-body);padding:4px 9px;border-radius:999px;margin-bottom:6px}
.chip.good{background:var(--good-bg);color:var(--good-fg)}
.chip.ok{box-shadow:inset 0 0 0 1px var(--ink2);color:var(--ink2)}
.chip.fix{background:var(--fix-bg);color:var(--fix-fg)}
.legend{display:flex;gap:16px;flex-wrap:wrap;font-size:13px;color:var(--ink2);margin:0 0 14px;align-items:center}

dl.rules{display:grid;grid-template-columns:minmax(0,15em) minmax(0,1fr);gap:0;margin:0;border-top:1px solid var(--rule)}
dl.rules dt,dl.rules dd{padding:14px 0;border-bottom:1px solid var(--rule);margin:0}
dl.rules dt{font-family:var(--f-disp);font-weight:700;font-size:16.5px;padding-right:20px}
@media (max-width:720px){dl.rules{grid-template-columns:minmax(0,1fr)}dl.rules dt{border-bottom:0;padding-bottom:0}}

ul.todo{list-style:none;padding:0;margin:0;display:grid;gap:10px;max-width:84ch}
ul.todo li{display:grid;grid-template-columns:22px minmax(0,1fr);gap:8px}
ul.todo li::before{content:"";width:13px;height:13px;border:1.5px solid var(--ink);border-radius:2px;margin-top:6px}
footer{padding-top:26px;font-size:13px;color:var(--ink2);max-width:90ch}

/* ---- drawing classes ---- */
.dg{display:block;width:100%;height:auto;color:var(--ink);font-family:var(--f-body)}
.dg text{fill:var(--ink);font-size:12px}
.dg .sm{font-size:10.5px;fill:var(--ink2)}
.dg .mono{font-family:var(--f-mono)}
.dg .acc{fill:var(--accent)}
.dg .goldtxt{fill:var(--gold-ink)}
.dg .lb-inv{fill:#F1EDE2;font-weight:600}
.dg .sm-inv{fill:#C3C7CF;font-size:10.5px}
.dg .wt-f{fill:var(--wt)} .dg .bt-f{fill:var(--bt)}
.dg .lb-on-accent{fill:var(--on-accent);font-weight:600}
.mk-ink{fill:var(--ink)} .mk-wt{fill:var(--wt)} .mk-bt{fill:var(--bt)} .mk-light{fill:#C9CCD3} .mk-brass{fill:#C8A04A}
.co circle{fill:var(--ink);stroke:var(--panel);stroke-width:2}
.co text{fill:var(--panel)!important;font:700 10.5px var(--f-mono)!important}
.f-pave{fill:var(--pave)} .f-pave2{fill:var(--pave2)} .f-lawn{fill:var(--lawn)} .f-cypress{fill:var(--cypress)}
.f-cut{fill:var(--cut)} .f-cut2{fill:var(--cut2)} .f-wall{fill:var(--wall)} .f-wall2{fill:var(--wall2)}
.f-dais{fill:#2C303B} .f-dais-bg{fill:#2C303B;opacity:.3} .f-board{fill:#4A5060} .f-water{fill:var(--water)}
.f-walnut{fill:var(--walnut)} .f-maple{fill:var(--maple)} .f-lane{fill:#565C6B} .f-iron{fill:var(--iron)}
.f-marble{fill:var(--marble)} .f-sky{fill:var(--sky)} .f-gold{fill:#E2B23C} .f-ink{fill:var(--ink)}
.f-accent{fill:var(--accent)} .f-none{fill:none} .f-bg{fill:var(--wall);opacity:.5} .f-bg2{fill:var(--wall);opacity:.22}
.f-slot{fill:#2C303B;opacity:.8} .f-door{fill:var(--door)} .f-portcullis{fill:#3E4148} .f-groove{fill:#1C1F26}
.f-banner{fill:var(--banner)} .f-window{fill:var(--window)} .f-slate{fill:#6F737B} .f-buoy{fill:#E9E2D0}
.f-carpet{fill:#8A6F5A} .f-ramp{fill:#3A3F4C}
.pad{fill:none;stroke:#E2B23C;stroke-width:2}
.obst{fill:var(--pave2);fill-opacity:.5;stroke:var(--ink2);stroke-width:1;stroke-dasharray:4 3}
.cone{fill:var(--accent);fill-opacity:.1}
.pw{fill:#F3EADB;stroke:#3FC1D9;stroke-width:2} .pb{fill:#231E1B;stroke:#F08A2C;stroke-width:2}
.dot-w{fill:#F3EADB} .dot-b{fill:#15110F}
.sh-top{fill:#FFFFFF;fill-opacity:.14} .sh-side{fill:#000000;fill-opacity:.16}
.s-ink{stroke:var(--ink);fill:none;stroke-width:1.4}
.s-faint{stroke:var(--ink2);fill:none;stroke-width:1}
.s-acc{stroke:var(--accent);stroke-width:1.6;fill:none}
.dash{stroke-dasharray:5 4}
.thin{stroke-width:1}
.thick{stroke-width:2.6}
.ray{stroke:var(--accent);stroke-width:1.3;stroke-dasharray:6 4;fill:none}
.route{stroke-width:1.6;stroke-dasharray:2 3}
.s-bank{stroke:var(--wall);stroke-width:2}
.s-goldfill{stroke:#E2B23C;stroke-width:1.6}
.s-arrow{stroke:#C9CCD3;stroke-width:1.4}
.s-rope{stroke:#C9A57A;stroke-width:2.6;stroke-linecap:round}
.s-knot{stroke:#1C1F26;stroke-width:1}
.dash-light{stroke:#C9CCD3;stroke-width:1.2;stroke-dasharray:4 3}
.dash-ink{stroke:var(--ink2);stroke-width:1.1;stroke-dasharray:4 3}
.s-chain{stroke:#9A9EA6;stroke-width:1.3;stroke-dasharray:2 2}
.s-ink-thin{stroke:var(--ink);stroke-width:.8}
.s-brass{stroke:#C8A04A;stroke-width:1.8;fill:none}
.s-bar{stroke:var(--maple);stroke-width:3.4;stroke-linecap:round}
.s-bartip{stroke:#E2B23C;stroke-width:3.4;stroke-linecap:round}
.s-chainhi{stroke:#AEB2BA;stroke-width:1.6;stroke-dasharray:3 3}
.s-grill{stroke:#1F2125;stroke-width:1}
.s-bar-v{stroke:var(--maple);stroke-width:2.4;stroke-linecap:round}
.s-bartip-v{stroke:#E2B23C;stroke-width:2.4;stroke-linecap:round}
.s-chainhi-v{stroke:#9A9EA6;stroke-width:1.3;stroke-dasharray:2 2}
.s-pole{stroke:var(--ink2);stroke-width:1.2}
.wt-s{stroke:#3FC1D9} .bt-s{stroke:#F08A2C}
.s-obst{fill:var(--panel);stroke:var(--rule);stroke-width:1.2}
.s-focus{fill:var(--panel);stroke:var(--accent);stroke-width:2.4}
.s-mg{fill:var(--panel);stroke:var(--rule);stroke-width:1.2;stroke-dasharray:4 3}
.own-obst{fill:var(--rule)} .own-mg{fill:var(--accent)} .own-mg-soft{fill:var(--accent);fill-opacity:.25}
.view{border-radius:3px}
@media (prefers-reduced-motion:reduce){*{scroll-behavior:auto}}
"""

PAIRS = [
    ("1-1 리본 화단 정원", "마지막 비트: 꽃테라스 낮은 회전봉 2", "테라스: 꽃 화분 · 물뿌리개 · 돌벤치 · 담쟁이 화단", [
        ("fix", "손볼 곳", "수로의 청록 물이 해자로 이어져 좋다. 그런데 1-1에서 연꽃은 타고 오르는 발판이었다. 해자에 수련을 띄우면 밟으러 뛰어든다. 수련은 빼거나 둑에 붙은 작은 잎만 둔다."),
        ("fix", "손볼 곳", "바로 앞 비트가 낮은 회전봉(0.2–0.52 m)이다. 비슷한 높이(0.4–0.62 m)로 누운 캡스턴 막대가 장애물로 읽힌다. 막대는 스스로 돌지 않고, 끝 30 cm 금색과 원판의 황동 화살표로 '밀 것'임을 보인다."),
        ("good", "잘 맞음", "화단에서 의장 광장으로. 꽃 화분이 앞뜰 돌 화분으로 이어진다. 판 둘레 1 m에는 화분을 들이지 않는다."),
    ]),
    ("1-2 왕실 마차뜰", "마지막 비트: 차고 미닫이벽 2", "테라스: 고정 마차 · 바퀴 정비대 · 빈 상자 · 등불", [
        ("fix", "손볼 곳", "비트 2의 적재다리(#12)가 '스스로 들리는 다리'를 가르쳤다. 세워진 도개교를 보고 내려오길 기다릴 수 있다. 도개교는 주기 없이 멈춰 있고, 금 매듭이 놓인 줄 길과 앞뜰 밧줄 더미가 '당겨라'를 먼저 말한다. 적재다리에는 금색을 쓰지 않는다."),
        ("good", "잘 맞음", "바퀴 정비대가 캡스턴의 참고 그림이다(배치 기획서 3절). 스스로 도는 수직 바퀴(위험) 다음에 가만히 서 있는 수평 캡스턴(할 일)이 대비된다."),
        ("ok", "무난", "넓은 석재 진입 뜰에서 크림 포장 광장으로. 바닥 재질이 그대로 이어진다."),
    ]),
    ("1-3 깃발 연무뜰", "마지막 비트: 낮은 회전봉 2", "테라스: 표적판 · 무기 거치대 · 접은 크림 깃발 · 돌 화분", [
        ("ok", "무난", "훈련뜰에서 해자 앞으로. 겹치는 장치가 없다."),
        ("fix", "손볼 곳", "1-1과 같다. 마지막 비트가 낮은 회전봉이라 캡스턴 막대 규칙(스스로 안 돎, 금색 끝, 원판 화살표)을 그대로 지킨다."),
        ("good", "잘 맞음", "접어 둔 깃발이 판 둘레 깃대 열로 펼쳐진다. 훈련을 마치고 의장 행사장으로 들어가는 흐름."),
    ]),
    ("1-4 나이트 마구간", "마지막 비트: 마구 보관문 미닫이 2", "테라스: 건초 묶음 · 마구 걸이 · 급수통 · 등불", [
        ("ok", "무난", "비트 5의 진흙 수로 다음 해자. 진흙은 갈색, 해자는 청록으로 물빛을 가른다. 비트 5의 적재다리는 1-2와 같이 금색 없이."),
        ("ok", "무난", "나무와 건초에서 쇠와 돌로. 재질 대비가 분명하다."),
        ("ok", "무난", "마른 목재 정리 동선에서 석재 광장으로."),
    ]),
    ("1-5 룩 성벽길", "마지막 비트: 내려가는 승강대로 경계 높이 맞춤", "테라스: 보초 방패 · 망원경 거치대 · 작은 왕관 깃발 · 돌 화분", [
        ("good", "잘 맞음", "성벽에서 내려와 담장의 문(아치)을 나서면 해자 앞이다. '해자는 성 바깥 경계에만'이라는 C의 이유와 맞는다. 문루 탑 지붕은 회색 슬레이트(파란 지붕은 백 팀으로 읽힘)."),
        ("fix", "손볼 곳", "비트 3의 시계 쌍문과 문루 바로 뒤에 또 문루가 나온다. 1-5는 나무 쌍문(시계로 열림), D는 쇠살문(사슬로 위로 오름)으로 재질과 움직이는 방향을 가른다."),
        ("ok", "무난", "망루에서 광장으로. 왕관 깃발이 깃대 열로 이어진다."),
    ]),
]

GAMES = [
    dict(key="C", title="도개교 · 해자 도개교", meta="끌기 · 혼자 약 1:29 · 4명 약 0:24 (미니게임 v1.0 9.3)",
         lede="광장 폭 전체를 해자가 가로지른다. 매듭을 쥐고 남쪽으로 7 m 걸어 나오면 다리가 내려와 팀 문으로 건너간다.",
         plan=figs.game_plan("plan-c", "C 해자 도개교 배치 평면. 해자가 광장 폭 전체를 가로지르고, 스테이션마다 줄 길·권양기·세워진 도개교·문루 탑 둘이 있다.", figs.draw_C),
         view=figs.view("view-c", "테라스 카메라에서 본 C. 물 띠와 세워진 다리 둘이 먼저 보인다.", "C"),
         keys=["줄 길 u 0.2–10.9, 폭 2.4 m. 1 m마다 금 눈금 8개, 남쪽을 가리키는 화살표.",
               "매듭 4개(금 고리, <code>GrabHandle</code>). 당기는 사람은 입구 쪽으로 걸어 나오므로 띠에서 들어오는 동선과 엇갈리지 않는다.",
               "권양기 u 11.3. 금 톱니와 걸쇠는 가운데 통로 쪽을 본다. 걸쇠 등 3개가 25·50·75%에 켜진다.",
               "해자 u 12–16(z 16–20), 광장 폭 전체(가운데 통로까지 한 해자). 청록 물, 수면은 광장 바닥 −0.5 m. 빠지면 1.5초 뒤 체크포인트. 낙사 영역은 해자 안에서 끝난다.",
               "도개교 4 × 4.1 m, 세워진 채 시작. 점선이 다 내려온 자리.",
               "문루 탑 둘(v ±2.4, 높이 6 m). 지붕은 회색 슬레이트.",
               "팀 문 u 20. 다리를 건너야 닿는다.",
               "가운데 통로 끝의 낮은 난간. 그 너머 정문 앞에는 올린 채 고정한 장식 도개교.",
               "앞뜰 소품: 감아 둔 밧줄 · 예비 톱니 · 구명 부표 · 돌 화분.",
               "승격 발판."],
         read="광장을 가로막는 물과 세워진 다리 둘. '건너야 한다, 그러려면 다리를 내려야 한다'가 그림만으로 나온다.",
         diff="해자의 수련을 뺀다(1-1에서 연꽃은 타는 발판). 구명 부표는 크림·회색으로(빨강은 위험·체크 색). 회색 박스(R154): 스테이션 해자의 양 끝벽을 빼고 통로 해자와 이었다."),
    dict(key="D", title="캡스턴 · 정문 쇠살문 누각", meta="끌기 · 8바퀴 · 혼자 약 1:00 · 4명 약 0:15 (R92 구현)",
         lede="팀마다 캡스턴 하나가 머리 위 사슬로 그 팀의 쇠살문과 이어진다. 막대를 몸으로 밀어 돌리면 사슬이 감기고 쇠살문이 오른다.",
         plan=figs.game_plan("plan-d", "D 캡스턴 배치 평면. 스테이션 가운데 캡스턴과 슬레이트 원판, 머리 위 사슬이 팀 문 쇠살문으로 이어진다.", figs.draw_D),
         view=figs.view("view-d", "테라스 카메라에서 본 D. 쇠창살 문 둘과 그쪽으로 올라가는 사슬이 보인다.", "D"),
         keys=["캡스턴 u 10. 쇠띠 두른 참나무 드럼과 금 왕관 뚜껑.",
               "막대 4개. 허리 높이 0.4–0.62 m, 드럼에서 0.75–3 m, 끝 30 cm 금색. 아무도 밀지 않으면 가만히 있다.",
               "슬레이트 원판(반지름 3.6 m)에 박은 황동 화살표. 백은 위에서 시계 방향, 흑은 거울.",
               "머리 위 사슬, 높이 2.5 m, 충돌 없음. 캡스턴에서 쇠살문 도르래까지 한 줄로 보인다.",
               "쇠살문이 곧 팀 문. 진척만큼 0.4 m까지 들리고 완료하면 다 오른다.",
               "누각 가운데 아치와 닫힌 정문. 가운데 통로의 끝.",
               "앞뜰 소품: 예비 막대 걸이 · 사슬 더미 · 위병 투구를 올린 벤치 · 돌 화분.",
               "승격 발판."],
         read="세로로 선 쇠창살 둘과 그쪽으로 올라가는 사슬. 원인(캡스턴)과 결과(쇠살문)가 한 줄로 이어진다.",
         diff="없음. 1-1·1-3 뒤에 올 때를 위해 '막대는 스스로 돌지 않는다'를 지킨다(5번 짝 표)."),
    dict(key="A", title="체스판 칠하기 · 정문 의장 광장", meta="서서 칠하기 · 혼자 약 1:44 · 4 대 4 약 0:27 (미니게임 v1.0 3.3)",
         lede="두 스테이션과 가운데 통로를 한 단으로 덮고 그 위에 18 × 18 m 판 하나를 깐다. 백은 흰 칸 32개, 흑은 검은 칸 32개를 칠한다.",
         plan=figs.game_plan("plan-a", "A 체스판 칠하기 배치 평면. 광장 가운데 18 × 18 m 공유 판, 둘레 관람석과 깃대 열, 판 밖 3 m에 팀 문.", figs.draw_A),
         view=figs.view("view-a", "테라스 카메라에서 본 A. 크림 포장 가운데 넓은 회청색 판이 보인다.", "A"),
         keys=["공유 판 x −9~9, u 2~20(z 6~24). 칸 2.15 m와 금 줄눈 0.1 m. 처음에는 64칸이 모두 회청색.",
               "a1은 검은 칸. 파일 a→h는 백 쪽에서 흑 쪽, 랭크 1→8은 남에서 북. 글자는 금 40% 밝기.",
               "칠한 칸 예시. 백 = 흰 석회암과 하늘색 테두리, 흑 = 검은 현무암과 주황 테두리. 가운데 작은 네모는 칠하는 중.",
               "판 둘레 1 m는 비운다(칠하러 뛰는 길). 화분·벤치 없음.",
               "낮은 돌 관람석과 깃대 열. 깃발은 크림, 팀색 없음.",
               "팀 문 x ±12, 판 가장자리에서 3 m 밖. 문 위에 '18 / 32' 팀색 숫자.",
               "정문이 d8·e8 바로 뒤에 선다. 퀸과 킹 자리 뒤가 왕궁 정문.",
               "앞뜰 소품: 흰·검은 칠통 · 붓 걸이 · 말아 둔 카펫 · 돌 화분.",
               "승격 발판."],
         read="크림 포장 한가운데 넓은 회청색 판. 앞길을 건너 단에 오르면 2 m 앞이 랭크 1이라, 첫 행동이 '판 위로 뛰어든다'가 된다.",
         diff="없음. 판은 스테이션 입구 띠 예외(u 2부터)를 그대로 쓴다."),
]


def game_block(g):
    keys = "".join(f"<li><span>{k}</span></li>" for k in g["keys"])
    return f"""
<article class="game" id="game-{g['key'].lower()}">
  <div class="game-head"><span class="tag">{g['key']}</span>
    <div><h3>{g['title']}</h3><div class="meta">{g['meta']}</div></div></div>
  <p class="lede2">{g['lede']}</p>
  <div class="grid2">
    <figure><div class="scroll">{g['plan']}</div>
      <figcaption>위에서 본 배치. 왼쪽이 백(서쪽), 오른쪽이 흑(동쪽, 백의 거울). 점선은 장애물 구간 몫.</figcaption></figure>
    <div class="stack">
      <figure><div class="scroll slim">{g['view']}</div>
        <figcaption>테라스 카메라(발 위 약 4 m, 아래로 12°)에서 아치 너머 처음 보이는 장면.</figcaption></figure>
      <ol class="keys">{keys}</ol>
      <p class="note"><span>테라스에서</span>{g['read']}</p>
      <p class="note"><span>기획서와 차이</span>{g['diff']}</p>
    </div>
  </div>
</article>"""


def pairs_table():
    head = ("<thead><tr><th>앞 장애물 구간</th>"
            "<th>C 해자 도개교<small>앞뜰: 밧줄 더미 · 예비 톱니 · 구명 부표 · 돌 화분</small></th>"
            "<th>D 쇠살문 누각<small>앞뜰: 예비 막대 걸이 · 사슬 더미 · 투구 벤치 · 돌 화분</small></th>"
            "<th>A 의장 광장<small>앞뜰: 흰·검은 칠통 · 붓 걸이 · 말아 둔 카펫 · 돌 화분</small></th></tr></thead>")
    rows = []
    for name, beat, props, cells in PAIRS:
        tds = "".join(f'<td><span class="chip {c}">{lab}</span><br>{t}</td>' for c, lab, t in cells)
        rows.append(f"<tr><th>{name}<small>{beat}</small><small>{props}</small></th>{tds}</tr>")
    return f'<table class="pairs"><colgroup><col class="c0"><col><col><col></colgroup>{head}<tbody>{"".join(rows)}</tbody></table>'


def page():
    games = "".join(game_block(g) for g in GAMES)
    return f"""<title>정문 앞 광장</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Hahmlet:wght@600;700&family=IBM+Plex+Mono:wght@400;500;700&family=IBM+Plex+Sans+KR:wght@400;500;600;700&display=swap">
<style>{CSS}</style>
<div class="wrap">
<header class="sheet">
  <div>
    <div class="eyebrow">폰 러시 레벨 디자인 · 슬롯 2 · 초반 미니게임 구간</div>
    <h1>정문 앞 광장</h1>
    <p class="lede">C 도개교, D 캡스턴, A 체스판 칠하기가 들어가는 구간이다. 앞에는 초반 장애물 다섯 곳(1-1~1-5) 중 하나가, 뒤에는 중반 장애물(2-x)이 붙는다. 어떤 짝이 뽑혀도 이음매가 같아 보이도록 광장 껍데기 하나를 고정하고, 게임과 앞뜰 소품만 바꿔 끼운다.</p>
  </div>
  <dl class="tb">
    <div><dt>구간</dt><dd>슬롯 2 · 미니게임 초반 · 바깥뜰</dd></div>
    <div><dt>입구 → 출구</dt><dd class="mono">(0, 0, 0) → (0, 0, 36), 폭 12</dd></div>
    <div><dt>크기</dt><dd>앞뜰 z −6.4~0 · 앞길 44 × 4 m · 광장 44 × 20 m · 문 뒤 12 m</dd></div>
    <div><dt>좌표</dt><dd>원점 = 입구 문턱 가운데, +z 북, +x 동, y = 광장 바닥</dd></div>
    <div><dt>팀</dt><dd>백 서쪽(하늘색) · 흑 동쪽(주황)</dd></div>
    <div><dt>상태</dt><dd class="state">C 회색 박스 프리팹 있음(R154) · Unity에서 본 적 없음</dd></div>
  </dl>
</header>

<section id="slot">
  <div class="sh"><span class="no">도면 1</span><h2>한 판 속 자리</h2></div>
  <p class="intro">초반 미니게임은 앞의 장애물 다섯 곳, 게임 세 가지와 만나 15가지 짝이 생긴다. 뒤쪽은 지금 2-1 대계단 현관홀 하나뿐이다. 그래서 먼저 이음매 ①·② 두 곳의 규격을 고정하고, 그 안에서 게임을 바꾼다.</p>
  <figure><div class="scroll">{figs.fig_slots()}</div>
    <figcaption>슬롯마다 목록에서 하나를 뽑아 입구를 앞 구간 출구에 붙인다(<code>PawnRushRound</code>, R149). 이야기로는 바깥뜰을 지나 정문을 열고 왕궁 현관으로 들어간다.</figcaption></figure>
</section>

<section id="joint">
  <div class="sh"><span class="no">도면 2</span><h2>이음매 단면</h2></div>
  <p class="intro">가운데 선(x = 0)을 따라 자른 단면이다. 막대 위쪽은 어느 프리팹이 무엇을 만드는지 나눈 것이다.</p>
  <figure><div class="scroll">{figs.fig_section()}</div>
    <figcaption>가로세로 같은 축척. 연한 면은 단면 뒤에 보이는 것(아치 양옆 담장, 팀 문 통로, 회랑 벽). 점선 두 줄은 테라스 카메라에서 파사드 아래·위 끝으로 가는 시선.</figcaption></figure>
  <ul class="points">
    <li>장애물 구간은 지금 2-1이 만든 그대로 입구 띠 끝까지 만든다. 정리 동선, 테라스 +1.0 m(12 × 6, CP), 열린 아치, 0.25 m × 4단, 띠 12 × 4 m. 그 끝이 미니게임 구간의 입구 (0, 0, 0)이다.</li>
    <li>미니게임 구간은 폭 44 m 앞길(z 0~4)로 시작한다. 장애물 쪽 띠는 폭이 12 m뿐이라, 스테이션의 경사로·깃발·간판(스테이션 앞 1.6 m)과 두 팀이 갈라지는 자리가 여기에 놓인다. 기획서의 44 × 25 m = 앞길 4 + 스테이션 20 + 북쪽 벽. 띠 바깥 양옆(|x| &gt; 6.3, z −6.4~0)의 앞뜰 담장과 잔디도 미니게임 구간이 만든다.</li>
    <li>테라스 카메라에서 아치 상인방 밑으로 파사드 0~16 m와 문 셋이 다 보인다. 게임을 읽는 시간 15초(컨셉아트 v2 시간 예산)는 테라스에서 쓴다. 아치 위 표시는 랭크 3, 정문 위 표시는 랭크 4를 제안한다(중반 대문 6랭크, 후반 알현실 8랭크 사이).</li>
  </ul>
</section>

<section id="shell">
  <div class="sh"><span class="no">도면 3</span><h2>공통 껍데기</h2></div>
  <p class="intro">벽·바닥·빛·간판·문틀은 하나로 고정하고 게임 소품만 바꿔 낀다(배치 기획서 2절). 문 뒤 통로 둘이 현관 회랑에서 만나 출구 하나로 나가므로, 뒤에 어떤 2-x가 와도 가운데 한 점에 붙는다.</p>
  <div class="grid2">
    <figure><div class="scroll">{figs.fig_shell()}</div>
      <figcaption>점선 = 장애물 구간 프리팹 몫. 연한 쐐기 = 테라스 카메라에서 아치 안쪽 모서리를 지나 보이는 범위. 두 팀 문, 간판, 승격 발판이 모두 그 안에 든다.</figcaption></figure>
    <ol class="keys">
      <li><span>장애물 구간 몫: 테라스, 아치, 4단, 입구 띠. 1-1~1-5가 모두 같은 규격.</span></li>
      <li><span>앞뜰 담장(높이 7 m), 잔디, 사이프러스. 장애물 구간의 아치는 기둥 둘뿐이라, 이 담장이 있어야 아치가 '광장 담장의 문'이 된다. 앞에 어떤 1-x가 와도 같은 문턱.</span></li>
      <li><span>앞뜰 소품 자리 4곳. 뽑힌 게임의 소품 3~4개를 놓는다. 나머지는 고정.</span></li>
      <li><span>앞길 44 × 4 m(CP0). 띠가 끝나는 바닥에 백 ←, → 흑 팀색 화살표.</span></li>
      <li><span>승격 발판 (±16.5, 5), 2 × 2 m. 스테이션 u 1, 바깥쪽.</span></li>
      <li><span>스테이션 앞 경사로(4.4 × 1.6 m)와 팀색 깃발 둘, 그 위 간판 <code>PF_ArenaSign</code>(높이 3.9 m, 5.6 × 1.4 m). 테라스에서 보인다.</span></li>
      <li><span>스테이션 20 × 20 m, z 4~24. u 0~2.5는 비우고, 바닥은 게임이 만든다(+0.4 m 단).</span></li>
      <li><span>가운데 통로 4 m. 닫힌 정문으로 향하는 축이자, 두 팀이 서로 방해하러 오가는 길.</span></li>
      <li><span>파사드 12 m와 누각 16 m, 닫힌 정문 6 × 7 m(장식, 못 오름).</span></li>
      <li><span>팀 문 x ±12, 4 × 4.6 m(열리면 벽 속으로 오름). 바로 뒤에 팀 장벽, 문 위 벽에 팀색 띠와 진척 막대.</span></li>
      <li><span>문 통로 6 m. 북쪽 덩어리를 뚫고 지나 회랑으로.</span></li>
      <li><span>현관 회랑 28 × 6 m, CP1. 두 통로가 만나는 곳이자 낮빛에서 실내로 넘어가는 그늘.</span></li>
      <li><span>출구 (0, 0, 36), 폭 12 m. 다음 구간 2-x의 입구가 여기에 붙는다.</span></li>
    </ol>
  </div>
</section>

<section id="games">
  <div class="sh"><span class="no">도면 4</span><h2>세 게임 배치</h2></div>
  <p class="intro">같은 껍데기에 게임만 바꿔 끼운 모습이다. 세 게임은 테라스에서 실루엣이 갈린다. C는 가로(물 띠와 다리), D는 세로(쇠살문과 사슬), A는 바닥(넓은 회청색 판). 수치는 미니게임 기획서 v1.0과 지금 구현 그대로이고, 바뀐 것은 장소와 외형뿐이다.</p>
  {games}
</section>

<section id="pairs">
  <div class="sh"><span class="no">도면 5</span><h2>앞 짝 15가지</h2></div>
  <p class="intro">1-x의 마지막 비트와 테라스 소품을 세 게임과 맞대 보았다. 손볼 곳 다섯은 모두 '앞에서 배운 그림이 여기서 다른 뜻으로 읽히는' 경우다. 고치는 법은 게임 외형 쪽에 있어서, 장애물 구간은 거의 그대로 둔다.</p>
  <div class="legend"><span><span class="chip good">잘 맞음</span> 4</span><span><span class="chip ok">무난</span> 6</span><span><span class="chip fix">손볼 곳</span> 5</span></div>
  <div class="tablewrap">{pairs_table()}</div>
</section>

<section id="rules">
  <div class="sh"><span class="no">도면 6</span><h2>이음매 약속</h2></div>
  <p class="intro">어떤 짝이 뽑혀도 어색하지 않게 하는 조건이다. 장애물 구간과 미니게임 구간이 서로 지킨다.</p>
  <dl class="rules">
    <dt>규격은 하나</dt><dd>테라스 +1.0 m · 12 × 6 m · 아치 안쪽 12 × 5 m 이상 · 0.25 m × 4단 · 띠 12 × 4 m. 입구 (0, 0, 0). 앞길 44 × 4 m. 출구 (0, 0, 36), 폭 12 m, 바닥 높이 0.</dd>
    <dt>담장이 아치를 문으로</dt><dd>미니게임 구간이 아치 양옆에 높이 7 m 담장과 잔디를 깐다. 그 대신 장애물 구간은 출구 앞 6.4 m 안에서 |x| &gt; 7을 비워 둔다. 2-1의 지금 출구가 이미 그렇다.</dd>
    <dt>소품만 넘어간다</dt><dd>테라스는 장애물 장소의 소품, 앞뜰은 뽑힌 게임의 소품 3~4개. 돌 화분이 C·D·A 세트에 모두 있어서 어느 짝이든 한 소품이 겹쳐 이어진다.</dd>
    <dt>빛나는 금은 아치 뒤에서</dt><dd>정리 동선과 테라스(출구 앞 약 24 m)에는 자체발광 금을 두지 않는다. 플레이어가 처음 보는 빛나는 금이 곧 할 일(매듭, 막대 끝, 줄눈)이 된다. 1-2·1-4 적재다리와 마차 바퀴 허브의 금도 무광으로.</dd>
    <dt>물은 한 가지 약속</dt><dd>청록 물은 어디서나 '빠지면 1.5초 뒤 체크포인트'. 해자 낙사 영역은 해자 안에서 끝나고 리스폰은 이 구간 CP0(광장 입구). 장애물 구간의 물은 정리 동선 전에 끝낸다.</dd>
    <dt>넉백은 광장 쪽으로 쏘지 않는다</dt><dd>마지막 장치의 밀어내기는 옆이나 뒤로. 아치 앞 6 m는 비운다(경계 설계 그대로).</dd>
  </dl>
</section>

<section id="decide">
  <div class="sh"><span class="no">도면 7</span><h2>정할 것</h2></div>
  <ul class="todo">
    <li>앞뜰 담장과 잔디를 미니게임 프리팹이 만들지(이 안), 장애물 프리팹마다 만들지.</li>
    <li>문 뒤 현관 회랑(28 × 6 m, CP1)을 미니게임 구간 끝에 둘지. 두면 출구가 늘 가운데 한 점이라 어떤 2-x든 그대로 붙는다.</li>
    <li>C 해자의 수련을 뺄지, 구명 부표 색을 크림·회색으로 바꿀지.</li>
    <li>캡스턴 막대와 회전봉을 외형으로만 가를지, 막대 높이도 바꿀지(지금 막대 0.4–0.62 m, 회전봉 0.2–0.52 m).</li>
    <li>흑 팀색을 주황으로 확정할지(배치 기획서 2.1). 이 도면은 주황으로 그렸다.</li>
    <li>아치 랭크 3 · 정문 랭크 4 표시를 쓸지.</li>
  </ul>
</section>

<footer>근거: 폰 러시 미니게임 구역 배치·디자인 기획서 v0.1(2026-11-01) · 미니게임 구현 기획서 v1.0(<code>Docs/KingRush/PAWN_RUSH_MINIGAMES_v1.md</code>) · 장소별 4분 코스 설계(<code>PAWN_RUSH_PLACES_4MIN.md</code>) · 컨셉아트 v2 · 경계 설계 SVG · 구간 프리팹 R149~R152(2-1 출구 규격 <code>GrandStairHallBuilder.Beats.cs</code>) · 코스 01 광장(<code>Course01v4Builder.cs</code>, 승격 발판 <code>StationPromotion</code>). C 회색 박스: <code>EarlyPlazaBuilder.cs</code>, 프리팹 <code>Early_C_MoatDrawbridge.prefab</code>(R154). 도면은 그 좌표로 같은 축척에 그렸고, Unity에서 사람이 본 것이 아니다.</footer>
</div>
"""


if __name__ == "__main__":
    html = page()
    with io.open(OUT, "w", encoding="utf-8") as fh:
        fh.write(html)
    print(OUT, len(html))

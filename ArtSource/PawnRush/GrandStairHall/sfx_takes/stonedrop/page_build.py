import base64, io, os, sys, wave
import miniaudio
import numpy as np

SR = 44100
SRC, RES, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
# The falling piece (FallingChessPiece): warning 1.4 s, fall 0.65 s, lands at 2.05 s.
FALL, LAND, END = 1.4, 2.05, 3.9
# Where each take's impact starts (s into the take, read off its low-band loudness) and whether it has its own fall.
TAKES = {
    "A1": (.035, False), "A2": (.70, True),
    "B1": (.88, True), "B2": (.97, True),
    "C1": (.04, False), "C2": (.57, True),
    "D1": (1.22, True), "D2": (.02, False),
}
ROWS = [
    ("A", "대리석 석상", {"A1": "'쿵' 한 번, 깨끗하고 묵직한 대리석 울림 (떨어지는 바람은 지금 소리)",
                          "A2": "'쉬이익' 떨어지다 '쾅' + 대리석 '쩍' 갈라짐"}),
    ("B", "돌 깨짐 · 잔해", {"B1": "'휘이익' 떨어지다 '콰직', 돌 조각이 '와르르' 굴러감",
                            "B2": "'쉬이익' 떨어지다 '콰직', 조금 뒤 큰 조각 하나 더 '쿵'"}),
    ("C", "땅울림 저음", {"C1": "아주 낮은 '두웅' 지진 같은 울림, 길게 (떨어지는 바람은 지금 소리)",
                          "C2": "낮은 울림이 '우우웅' 차올라 '두웅' (자기 바람 소리)"}),
    ("D", "거대 돌 체스 말", {"D1": "짧은 '휙' 다음 단단한 '탁 · 쿵', 받침이 잠깐 '덜컥'",
                              "D2": "'쿵' 내려앉고 받침이 '덜컥 덜컥' 흔들리다 섬 (떨어지는 바람은 지금 소리)"}),
]


def load(p):
    a = miniaudio.decode_file(p, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    x = np.frombuffer(a.samples, dtype=np.float32).astype(np.float64)
    return x - x.mean()


def peak(x, db):
    return x * (10 ** (db / 20) / (np.abs(x).max() + 1e-12))


def wav_uri(x):
    b = io.BytesIO()
    w = wave.open(b, "wb"); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes()); w.close()
    return "data:audio/wav;base64," + base64.b64encode(b.getvalue()).decode()


warn = load(os.path.join(RES, "HallKnightWarn.wav"))
fall = load(os.path.join(RES, "HallKnightFall.wav"))
land = load(os.path.join(RES, "HallKnightLand.wav"))


def track():
    total = np.zeros(int(END * SR))

    def put(x, at, gain):
        j = int(round(at * SR))
        if j < 0:
            x, j = x[-j:], 0
        n = min(len(x), len(total) - j)
        total[j:j + n] += x[:n] * gain
    put(warn, LAND - len(warn) / SR, .5)   # the chosen "나무 시계" ticks, ending at the landing (game volume 0.5)
    return total, put


def finish(total):
    n = int(.3 * SR)
    total[-n:] *= np.linspace(1, 0, n)
    return peak(total, -1)


def now():
    total, put = track()
    put(fall, LAND - len(fall) / SR, .7)
    put(land, LAND, .9)
    return finish(total)


def build(name):
    x = load(os.path.join(SRC, name + ".mp3"))
    at, own = TAKES[name]
    if name == "D1":
        # its whoosh comes 0.9 s before the hit with silence between: close the gap to 0.25 s
        cut0, cut1 = int(.25 * SR), int(.92 * SR)
        x = np.concatenate([x[:cut0], x[cut1:]])
        at -= (cut1 - cut0) / SR
    x = peak(x, -1)
    total, put = track()
    if not own:
        put(fall, LAND - len(fall) / SR, .7)
    pre = int(at * SR)
    fade = min(pre, int(.12 * SR))
    if fade > 0:
        x[:fade] *= np.linspace(0, 1, fade)
    put(x, LAND - at, .9)
    return finish(total)


span = END
marks = [(0, "예고"), (FALL, "떨어짐"), (LAND, "착지")]
ticks = "".join(f'<span class="mark" style="left:{t / span * 100:.1f}%">{n}</span>' for t, n in marks)
NOW_DESC = "떨어짐 = 바람을 거꾸로 튼 '우우웅', 착지 = '쿵' + 낮은 울림"
rows = ['<article class="take now"><div class="meta"><span class="key">지금</span><h3>R543 소리</h3>'
        f'<p>{NOW_DESC}</p></div><div class="btns"><button type="button" data-a="NOW">지금 소리 듣기</button></div>'
        f'<audio preload="auto" id="NOW" src="{wav_uri(now())}"></audio></article>']
for key, title, takes in ROWS:
    btns = "".join(f'<button type="button" data-a="{n}">{n} 듣기</button>' for n in takes)
    desc = "".join(f'<li><b>{n}</b> {d}</li>' for n, d in takes.items())
    auds = "".join(f'<audio preload="auto" id="{n}" src="{wav_uri(build(n))}"></audio>' for n in takes)
    rows.append(f'<article class="take"><div class="meta"><span class="key">{key}</span><h3>{title}</h3><ul>{desc}</ul></div>'
                f'<div class="btns">{btns}</div>{auds}</article>')

page = """<title>떨어지는 돌 기사 소리</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Gowun+Batang:wght@700&family=IBM+Plex+Sans+KR:wght@400;600&display=swap">
<style>
:root{--bg:#efe9df;--panel:#f8f4ec;--ink:#2a2420;--soft:#6f655b;--line:#d6cbbb;--iron:#3b3632;--gold:#a8792a;
 --display:"Gowun Batang",Georgia,serif;--body:"IBM Plex Sans KR","Malgun Gothic",system-ui,sans-serif}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--bg:#1d1a17;--panel:#27231f;--ink:#efe7da;--soft:#a99d8e;--line:#3d3630;--iron:#cfc5b6;--gold:#d3a54d;color-scheme:dark}}
:root[data-theme="dark"]{--bg:#1d1a17;--panel:#27231f;--ink:#efe7da;--soft:#a99d8e;--line:#3d3630;--iron:#cfc5b6;--gold:#d3a54d;color-scheme:dark}
body{background:var(--bg);color:var(--ink);font-family:var(--body);line-height:1.6}
main{max-width:820px;margin:0 auto;padding-block:32px 48px;padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px));display:grid;gap:16px}
h1{font-family:var(--display);font-size:1.9rem;margin:0;text-wrap:balance}
.lead{margin:0;color:var(--soft);max-width:64ch}.lead b{color:var(--ink)}
.bar{position:relative;height:30px;border-bottom:2px solid var(--line);margin:4px 0 6px}
.mark{position:absolute;bottom:4px;transform:translateX(-50%);font-size:.8rem;color:var(--soft);white-space:nowrap}
.mark:first-child{transform:none}
.mark::after{content:"";position:absolute;left:50%;bottom:-8px;width:2px;height:8px;background:var(--gold)}
.head{position:absolute;bottom:-5px;width:8px;height:8px;border-radius:50%;background:var(--gold);transform:translateX(-50%);left:0}
.take{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:18px;align-items:center;background:var(--panel);border:1px solid var(--line);border-radius:6px;padding:14px 18px}
.take.now{border-style:dashed}
.meta{min-width:0}.key{font-family:var(--display);color:var(--gold);font-size:1.1rem}
.meta h3{display:inline;font-size:1.05rem;margin:0 0 0 8px}.meta p{margin:4px 0 0;color:var(--soft)}
.meta ul{margin:6px 0 0;padding-left:18px;color:var(--soft)}.meta li b{color:var(--ink)}
.btns{display:flex;gap:8px;flex-wrap:wrap}
button{font:600 .92rem var(--body);background:var(--iron);color:var(--panel);border:0;border-radius:4px;padding:9px 14px;cursor:pointer;white-space:nowrap}
button:focus-visible{outline:3px solid var(--gold);outline-offset:2px}
.note{color:var(--soft);font-size:.88rem;margin:0}
@media (max-width:560px){.take{grid-template-columns:1fr}}
</style>
<main>
<h1>떨어지는 돌 기사 소리</h1>
<p class="lead">대계단 홀의 돌 기사는 <b>바닥 원이 1.4초 차오른 뒤 9 m 위에서 0.65초 만에 떨어져 착지</b>합니다. 예고 소리는 고르신 <b>A 나무 시계 째깍</b> 그대로 두고, 떨어지는 소리 · 착지 소리만 네 갈래로 다시 만들었습니다. 버튼을 누르면 예고부터 착지 뒤 울림까지 게임 박자대로 들립니다.</p>
<div class="bar" aria-hidden="true">%TICKS%<span class="head" id="head"></span></div>
<p class="note">크기만 맞췄고, 게임에서는 돌 홀 울림이 더해집니다. 자기 바람 소리가 없는 것(A1 · C1 · D2)은 지금 떨어지는 소리를 앞에 붙였습니다. 벌을 섞어도 됩니다(예: A2 떨어짐 + C1 착지).</p>
%ROWS%
</main>
<script>
let cur=null;const head=document.getElementById('head');
function run(){if(!cur)return;head.style.left=Math.min(100,cur.currentTime/%SPAN%*100)+'%';if(!cur.paused&&!cur.ended)requestAnimationFrame(run)}
document.querySelectorAll('button[data-a]').forEach(b=>b.addEventListener('click',()=>{
 if(cur){cur.pause();cur.currentTime=0}cur=document.getElementById(b.dataset.a);cur.currentTime=0;cur.play().then(run).catch(()=>{})}));
</script>
""".replace("%ROWS%", "\n".join(rows)).replace("%TICKS%", ticks).replace("%SPAN%", f"{span:.3f}")
open(OUT, "w", encoding="utf-8").write(page)
print(len(page) // 1024, "KB")

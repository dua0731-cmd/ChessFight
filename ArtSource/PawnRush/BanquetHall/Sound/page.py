# R844: the listening page for the grand banquet hall's sounds - every take (takes/, trimmed by bake.py) and the one each
# kind uses in the game (Assets/Resources/BanquetHallSfx), embedded as 16 kHz WAV data (loops cut to 4 s here
# only), with bake.py's numbers (report.json).
#   python page.py <this folder> <project root> <out.html>
import os, sys, json, base64, io, wave, numpy as np

S, ROOT, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
report = json.load(open(os.path.join(S, 'report.json'), encoding='utf-8'))
GROUPS = [
    ('신호음 — 귀로 골라 주세요', [
        ('drum_roll', '식탁보 북', '식탁보가 빨개질 때 2초 동안, 마술사 쪽에서 (식탁 어디서나 "접시로")'),
        ('cloth_pull', '식탁보 빼기', '마술사가 천을 빼는 순간 — 심벌 + 천 촤악'),
        ('service_bell', '은 뚜껑 호출 종', '뚜껑이 닫히기 0.6초 전 "지금 타라 / 내려라" (단에서는 조금 낮게)'),
        ('cloche_shut', '은 뚜껑 닫힘', '뚜껑이 접시에 닿는 순간 — 탔는지 놓쳤는지'),
        ('lamp_chime', '왈츠 초록 등', '여왕이 멈추기 1.2초 전 등이 켜질 때 (무대 앞 등은 높은 음)'),
        ('curtain_cue', '막 지휘봉', '막이 열리기 1초 전 딱 딱 딱')]),
    ('장치', [
        ('kitchen_door', '부엌 반문', '쟁반이 부엌 문을 나올 때마다 (3.3초마다)'),
        ('tray_clink', '쟁반 접시 달그락', '웨이터 넷의 쟁반에서 늘 (반복음, 웨이터마다 음높이 조금씩 다름)'),
        ('cloth_thrown', '날아가는 폰', '식탁보에 서 있다 던져지는 폰마다'),
        ('cloth_lay', '다시 까는 천', '천을 다시 깔 때 (2초)'),
        ('cloche_open', '은 뚜껑 열림', '단과 식탁에서 뚜껑이 들릴 때'),
        ('hoist_loop', '승강 도르래', '접시가 오르내리는 동안만, 빠를수록 크게 (반복음)'),
        ('gown_loop', '춤추는 쌍', '가까운 세 쌍에서 오르골 태엽 + 드레스 (반복음)'),
        ('curtain_draw', '막 레일', '막이 움직이는 동안만, 닫힐 때는 높게 (반복음)'),
        ('chocolate_loop', '초콜릿 분수', '분수에서 늘 (반복음)'),
        ('chandelier_swing', '샹들리에 짤랑', '줄마다 흔들림의 양 끝 — 옮겨 잡을 때 (긴 줄은 낮게)')]),
    ('공간', [
        ('hall_ambience', '홀 공간음', '대연회장 안 어디서나 — 먼 식기 · 잔 소리 (반복음)'),
        ('arrival', '발코니 도착 나팔', '내 폰이 왕의 발코니에 처음 설 때, 나만')]),
]


def wav_uri(path, cap=None, sr=16000):
    with wave.open(path) as w:
        n, r = w.getnframes(), w.getframerate()
        x = np.frombuffer(w.readframes(n), dtype='<i2').astype(np.float64)
    if cap: x = x[:int(cap * r)]
    if r != sr:
        t = np.arange(0, len(x) * sr / r) * r / sr
        x = np.interp(t, np.arange(len(x)), x)
    if cap and len(x) > sr * .2:
        f = int(sr * .15); x[-f:] *= np.linspace(1, 0, f)
    b = io.BytesIO()
    with wave.open(b, 'wb') as o:
        o.setnchannels(1); o.setsampwidth(2); o.setframerate(sr)
        o.writeframes(np.clip(x, -32768, 32767).astype('<i2').tobytes())
    return 'data:audio/wav;base64,' + base64.b64encode(b.getvalue()).decode()


groups = []
for title, kinds in GROUPS:
    cards = []
    for key, label, where in kinds:
        r = report[key]
        cap = 4 if r['loop'] else None
        takes = []
        for t in r['takes']:
            n = t['take']
            flags = []
            if t['rings'] and False: flags.append('삐 울림')
            if t['peak'] < -30: flags.append('작게 나옴')
            nums = f"{t['length']:.1f} s" if r['loop'] else f"{round(t['hit'] * 1000)} · {round(t['tail'] * 1000)} ms"
            takes.append(dict(n=n, src=wav_uri(os.path.join(S, 'takes', f'{key}_{n}.wav'), cap), nums=nums, flags=flags,
                              on=r['picked'] == f'{key}_{n}'))
        cards.append(dict(key=key, label=label, where=where, loop=r['loop'], picked=r['picked'], file=r['file'],
                          game=wav_uri(os.path.join(ROOT, 'Assets/Resources/BanquetHallSfx', r['file'] + '.wav'), cap),
                          takes=takes))
    groups.append(dict(title=title, cards=cards))

TEMPLATE = open(os.path.join(S, 'page_template.html'), encoding='utf-8').read()
open(OUT, 'w', encoding='utf-8').write(TEMPLATE.replace('/*DATA*/[]', json.dumps(groups, ensure_ascii=False)))
print('wrote', OUT, os.path.getsize(OUT) // 1024, 'KB')

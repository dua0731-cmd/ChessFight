# R742: the listening page for the Royal Observatory's sounds - every take (takes/, trimmed by bake.py) and the one each
# kind uses in the game (Assets/Resources/RoyalObservatorySfx), embedded as 16 kHz WAV data (loops cut to 6 s here
# only), with bake.py's numbers (report.json).
#   python page.py <this folder> <project root> <out.html>
import os, sys, json, base64, io, wave, numpy as np

S, ROOT, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
report = json.load(open(os.path.join(S, 'report.json'), encoding='utf-8'))
GROUPS = [
    ('유성우', [('meteor_warn', '경고 고리', '금빛 시계판이 나타날 때 (유성 착지 약 1초 전)'),
              ('meteor_fall', '떨어짐', '돌이 26 m 위에서 내려오기 시작할 때'),
              ('meteor_impact', '착지', '돌이 별자리 다리에 떨어지는 순간')]),
    ('태양 플레어', [('flare_charge', '차오름', '예고 1.3초 동안 태양이 부풀 때'),
                 ('flare_burst', '폭발', '불길이 터지는 순간, 대천구 어디서나'),
                 ('flare_hit_me', '내가 맞음', '그림자 밖에서 맞은 사람만 (화면 테두리 섬광과 함께)')]),
    ('초점 광선', [('beam_hum', '빛기둥 웅웅', '늘 켜져 있음, 두 빛 원 사이에서 (반복음)'),
                ('beam_strike', '기물 맞음', '빛 원에 맞아 기물이 날아갈 때, 모든 PC'),
                ('beam_hit_me', '내가 맞음', '맞은 사람만')]),
    ('조리개', [('iris_warn', '경고 째깍', '날개가 붉게 맥박칠 때 (0.8초)'),
             ('iris_scrape', '닫히며 긁힘', '날개가 갑판을 긁으며 닫힐 때 (0.5초)'),
             ('iris_clash', '맞닿음', '두 날개가 쾅 닫히는 순간')]),
    ('변광성 꽃잎', [('petal_warn', '경고 덜컥', '꽃잎이 접히기 전 깜빡일 때 (0.8초)'),
                 ('petal_fold', '접힘 딸깍', '꽃잎 다섯 장이 0.25초 간격으로 떨어질 때마다'),
                 ('petal_rise', '펴짐', '다섯 장이 함께 올라올 때')]),
    ('움직이는 장치', [('orbit_loop', '궤도 발판', '도는 궤도 3개 (반복음)'),
                  ('gear_loop', '카시오페이아 톱니', '맞물려 도는 톱니 4개 (반복음). 11-21 "장난감 같다" → 묵직한 시계탑 톱니로 새로 만든 8개, 게임에서는 조금 낮게(0.84 ~ 0.96배)'),
                  ('planet_roll', '구르는 행성', '구를 때만, 빠를수록 크게 (반복음)'),
                  ('planet_dock', '행성 멈춤', '양쪽 정거장에 닿을 때'),
                  ('globe_creak', '대천구 고리', '흔들리는 놋쇠 고리, 아주 작게 (반복음)')]),
    ('공간', [('hall_ambience', '홀 밤 공기', '천문대 안 어디서나 (반복음)'),
            ('balcony_wind', '발코니 바람', '돔 밖 발코니에 나가면 홀 소리 대신 (반복음)')]),
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
        cap = 6 if r['loop'] else None
        takes = []
        for t in r['takes']:
            n = t['take']
            flags = []
            if t['rings'] and key not in ('meteor_warn', 'meteor_impact', 'iris_warn', 'iris_clash', 'beam_hum'): flags.append('삐 울림')
            if t['peak'] < -30: flags.append('작게 나옴')
            nums = f"{t['length']:.1f} s" if r['loop'] else f"{round(t['hit'] * 1000)} · {round(t['tail'] * 1000)} ms"
            takes.append(dict(n=n, src=wav_uri(os.path.join(S, 'takes', f'{key}_{n}.wav'), cap), nums=nums, flags=flags,
                              on=r['picked'] == f'{key}_{n}'))
        cards.append(dict(key=key, label=label, where=where, loop=r['loop'], picked=r['picked'], file=r['file'],
                          game=wav_uri(os.path.join(ROOT, 'Assets/Resources/RoyalObservatorySfx', r['file'] + '.wav'), cap),
                          takes=takes))
    groups.append(dict(title=title, cards=cards))

TEMPLATE = open(os.path.join(S, 'page_template.html'), encoding='utf-8').read()
open(OUT, 'w', encoding='utf-8').write(TEMPLATE.replace('/*DATA*/[]', json.dumps(groups, ensure_ascii=False)))
print('wrote', OUT, os.path.getsize(OUT) // 1024, 'KB')

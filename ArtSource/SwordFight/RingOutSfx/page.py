# R869: the listening page for the 장외! stamp sound takes (raw/, numbers from measure.py in report.json): each take
# embedded as MP3 data and played through Web Audio at the same peak (-3 dB), with the stamp popping in as the game does
# (SwordFightMatch.Hud BigStamp: 1.8x -> 0.95x -> 1x in the first 0.2 of 1.1 s, tilted -6 degrees, fading at the end).
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I page.py <this folder> <out.html>
import os, sys, json, base64

S, OUT = sys.argv[1], sys.argv[2]
report = json.load(open(os.path.join(S, 'report.json'), encoding='utf-8'))
DIRS = [
    ('A', '나무 도장 쾅', '두꺼운 나무 탁자에 나무 도장을 세게 찍는 소리 + 위에 얹힌 밝은 황동 짤랑. 체스판 · 나무 무대와 가장 잘 맞는 쪽.'),
    ('B', '북 + 심벌 스팅', '큰 북 한 번에 짧은 심벌. 아케이드 · 파티 게임에서 점수가 날 때의 익숙한 소리.'),
    ('C', '코믹 휙 + 쿵', '휙 날아가는 소리 뒤에 통통 튀는 나무 쿵. 장외당한 기물이 멀리 날아가는 우스운 느낌.'),
    ('D', '징 + 칼 울림', '짧은 황동 징에 칼이 부딪는 쨍. 결투장 · 중세 분위기, 가장 무겁고 오래 울림.'),
]
cards = []
for key, name, note in DIRS:
    takes = []
    for n in range(1, 5):
        k = f'{key}_{n}'
        p = os.path.join(S, 'raw', k + '.mp3')
        if not os.path.exists(p): continue
        m = report[k]
        flags = []
        if m['hit'] > .2: flags.append('늦게 침')
        if m['peak'] < -15: flags.append('작게 생성됨')
        if m['tail'] > .6: flags.append('길게 울림')
        takes.append(dict(n=n, src='data:audio/mpeg;base64,' + base64.b64encode(open(p, 'rb').read()).decode(),
                          peak=m['peak'], hit=int(round(m['hit'] * 1000)), tail=int(round(m['tail'] * 1000)), flags=flags))
    cards.append(dict(key=key, name=name, note=note, takes=takes))

TEMPLATE = open(os.path.join(S, 'page_template.html'), encoding='utf-8').read()
open(OUT, 'w', encoding='utf-8').write(TEMPLATE.replace('/*DATA*/[]', json.dumps(cards, ensure_ascii=False)))
print('wrote', OUT, os.path.getsize(OUT) // 1024, 'KB')

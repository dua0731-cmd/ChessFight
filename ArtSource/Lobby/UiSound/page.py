# R738: the listening page for the menu sounds - every take (takes/, trimmed by bake.py) and the one each kind uses in the
# game (Assets/Resources/UiSound), embedded as WAV data, with bake.py's numbers (report.json).
#   python page.py <this folder> <project root> <out.html>
import os, sys, json, base64, wave, numpy as np

S, ROOT, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
report = json.load(open(os.path.join(S, 'report.json'), encoding='utf-8'))
KINDS = [
    ('hover', '마우스 올림', 'MenuHover', '버튼 위에 마우스를 올릴 때. 4개 모두 삐 하는 울림이 있거나 거의 안 들려서 토글 1번을 작게 씀'),
    ('click', '클릭', 'MenuClick', '보통 버튼: 모드 카드, 친구 초대, 복사, 이름, 키 바꾸기, 기본값 복원'),
    ('tab', '탭', 'MenuTab', '플레이 · 기물 도감 · 전적, 설정 창의 탭과 왼쪽 쪽, 도감의 "전적 보기"'),
    ('open', '창 열기', 'MenuOpen', '설정 창, 파티 · 친구 · 정보 · 코드로 참가, 모드 카드가 올라올 때, 게임 나가기 질문'),
    ('close', '창 닫기', 'MenuClose', '✕, 뒤로(Esc), 매칭 취소, 파티 나가기, 초대 거절, 질문의 취소'),
    ('toggle', '토글', 'MenuToggle', '켬 / 끔, ‹ ›, 드롭다운, 슬라이더 한 칸, 백 / 흑, 봇 − +'),
    ('confirm', '확정', 'MenuConfirm', '랜덤 매칭, 방 생성, 경기 시작, 파티 · 방 참가, 경기로 돌아가기, 게임 나가기(확인)'),
    ('piece', '기물 고르기', 'MenuPiece', '기물 도감에서 기물을 누를 때'),
    ('page', '쪽 넘김', 'MenuPage', '전적의 전체 / 폰 러쉬 / 소드 파이트와 ‹ ›, 친구 목록 쪽'),
]


def wav_uri(path, sr=22050):
    with wave.open(path) as w:
        n, r = w.getnframes(), w.getframerate()
        x = np.frombuffer(w.readframes(n), dtype='<i2').astype(np.float64)
    if r != sr:
        t = np.arange(0, len(x) * sr / r) * r / sr
        x = np.interp(t, np.arange(len(x)), x)
    import io
    b = io.BytesIO()
    with wave.open(b, 'wb') as o:
        o.setnchannels(1); o.setsampwidth(2); o.setframerate(sr)
        o.writeframes(np.clip(x, -32768, 32767).astype('<i2').tobytes())
    return 'data:audio/wav;base64,' + base64.b64encode(b.getvalue()).decode()


cards = []
for key, label, game, where in KINDS:
    r = report[key]
    takes = []
    for t in r['takes']:
        n = t['take']
        flags = []
        if t['rings']: flags.append('울림')
        if t['peak'] < -30: flags.append('거의 안 들림')
        takes.append(dict(n=n, src=wav_uri(os.path.join(S, 'takes', f'{key}_{n}.wav')),
                          hit=round(t['hit'] * 1000), tail=round(t['tail'] * 1000), flags=flags,
                          on=r['picked'] == f'{key}_{n}'))
    cards.append(dict(key=key, label=label, where=where, picked=r['picked'], by=r['by'],
                      game=wav_uri(os.path.join(ROOT, 'Assets/Resources/UiSound', game + '.wav')), takes=takes))

TEMPLATE = open(os.path.join(S, 'page_template.html'), encoding='utf-8').read()
open(OUT, 'w', encoding='utf-8').write(TEMPLATE.replace('/*DATA*/[]', json.dumps(cards, ensure_ascii=False)))
print('wrote', OUT, os.path.getsize(OUT) // 1024, 'KB')

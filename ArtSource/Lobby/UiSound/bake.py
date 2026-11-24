# R738 (user 11-21: "설정창, 플레이, 기물도감, 전적 UI 사운드도 넣어야 해"): the lobby's and the settings window's menu sounds.
# The takes are ElevenLabs sound effects (flow "R738 로비 · 설정 UI 소리" 2Rq0SFKq7Wg1BJSBEbw9, eleven_text_to_sound_v2,
# prompt_influence 0.45, 4 takes a prompt; prompts.md) in raw/<kind>_<1-4>.mp3. This script measures every take, picks one
# per kind by the numbers (the AI cannot listen: a clean early hit, no ringing tone, a short tail), or the one picks.json
# names, trims and peak-normalises it into Assets/Resources/UiSound/Menu<Kind>.wav (MenuSound.cs plays them), and writes
# every take, trimmed the same way, to takes/ for the listening page (page.py).
#   uv run --no-project --managed-python --python 3.12 --with miniaudio --with numpy python -I bake.py <this folder> <project root>
import os, sys, json, wave, numpy as np, miniaudio

S, ROOT = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(ROOT, 'ArtSource/Loading/SwordPick/Sound'))
from coin2 import rings

SR = 44100
OUT = os.path.join(ROOT, 'Assets/Resources/UiSound')
TAKES = os.path.join(S, 'takes')
KINDS = ['hover', 'click', 'tab', 'open', 'close', 'toggle', 'confirm', 'piece', 'page']
NAME = dict(hover='MenuHover', click='MenuClick', tab='MenuTab', open='MenuOpen', close='MenuClose', toggle='MenuToggle',
            confirm='MenuConfirm', piece='MenuPiece', page='MenuPage')
# Each kind's peak (dBFS): how loud it is next to the others. MenuSound plays them at the settings' 버튼 소리 volume.
PEAK = dict(hover=-17, click=-7, tab=-7, open=-6, close=-8, toggle=-9, confirm=-3, piece=-5, page=-8)
# Longest each may ring on after its hit (s): a menu sound must not linger.
TAIL = dict(hover=.12, click=.18, tab=.25, open=.45, close=.3, toggle=.15, confirm=.4, piece=.3, page=.25)
# Sounds whose start is a swoosh building up (their onset, not their loudest point, is where they begin).
SWELL = {'tab', 'open', 'close', 'page'}


def load_mp3(p):
    d = miniaudio.decode_file(p, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    return np.frombuffer(d.samples, dtype=np.float32).astype(np.float64)


def env(x, win=0.005):
    n = max(1, int(SR * win))
    return np.sqrt(np.convolve(x * x, np.ones(n) / n, mode='same') + 1e-12)


def db(v):
    return 20 * np.log10(max(v, 1e-9))


def measure(x, kind):
    e = env(x)
    top = e.max()
    onset = int(np.where(e > top * 0.1)[0][0])
    hit = int(np.argmax(e))
    after = np.where(e[hit:] < top * 10 ** (-40 / 20))[0]
    tail = (after[0] if len(after) else len(x) - hit) / SR
    spec = np.abs(np.fft.rfft(x * np.hanning(len(x))))
    f = np.fft.rfftfreq(len(x), 1 / SR)
    centroid = float((spec * f).sum() / max(spec.sum(), 1e-9))
    return dict(peak=db(np.abs(x).max()), onset=onset / SR, hit=hit / SR, tail=tail, rings=len(rings(x)),
                centroid=centroid, rms=db(np.sqrt((x * x).mean())))


def score(m, kind):
    # lower is better
    s = 0.0
    if m['peak'] < -30: s += 100            # near silent
    s += 8 * m['rings']                      # a ringing tone (a beep or chime)
    late = m['onset'] if kind in SWELL else m['hit']
    s += 40 * max(0.0, late - (0.12 if kind in SWELL else 0.03))   # a late start feels laggy on a click
    s += 20 * max(0.0, m['tail'] - TAIL[kind])                     # lingering
    s += 3 * m['hit']                                              # sooner is snappier (breaks ties)
    s += 0.05 * max(0.0, -12 - m['peak'])                          # a faint take brings its noise up when normalised
    if kind == 'hover': s += m['centroid'] / 4000                  # softer, darker
    return s


def trim(x, m, kind):
    start = max(0, int(m['onset'] * SR) - int(0.003 * SR))
    e = env(x)
    top = e.max()
    hit = int(np.argmax(e))
    below = np.where(e[hit:] < top * 10 ** (-48 / 20))[0]
    end = min(len(x), hit + (below[0] if len(below) else len(x) - hit) + int(0.02 * SR))
    end = min(end, hit + int((TAIL[kind] + 0.15) * SR))
    y = x[start:end].copy()
    fin = min(len(y), int(0.002 * SR))
    y[:fin] *= np.linspace(0, 1, fin)
    fout = min(len(y), int(0.03 * SR))
    y[-fout:] *= np.linspace(1, 0, fout)
    return y * (10 ** (PEAK[kind] / 20) / max(np.abs(y).max(), 1e-9))


def write_wav(p, y, sr=SR):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    if sr != SR:
        t = np.arange(0, len(y) * sr / SR) * SR / sr
        y = np.interp(t, np.arange(len(y)), y)
    with wave.open(p, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())


picks_path = os.path.join(S, 'picks.json')
# picks.json: kind -> a take of its own ("3") or of another kind ("toggle_1"); a kind left out is picked by the numbers.
# The hover takes all carried a ringing tone or were near silent, so hover is a quiet toggle take (11-21).
chosen = json.load(open(picks_path)) if os.path.exists(picks_path) else {}
measured = {}
for kind in KINDS:
    for n in range(1, 5):
        p = os.path.join(S, 'raw', f'{kind}_{n}.mp3')
        if not os.path.exists(p): continue
        x = load_mp3(p)
        m = measure(x, kind)
        m['take'] = n
        m['score'] = round(score(m, kind), 3)
        measured[f'{kind}_{n}'] = (m, x)
        write_wav(os.path.join(TAKES, f'{kind}_{n}.wav'), trim(x, m, kind), 22050)
report = {}
for kind in KINDS:
    rows = [measured[k] for k in measured if k.startswith(kind + '_')]
    best = min(rows, key=lambda r: r[0]['score'])
    want = str(chosen.get(kind, best[0]['take']))
    source = want if '_' in want else f'{kind}_{want}'
    m, x = measured[source]
    y = trim(x, measure(x, kind), kind)
    write_wav(os.path.join(OUT, NAME[kind] + '.wav'), y)
    report[kind] = dict(picked=source, by='picks.json' if kind in chosen else 'numbers', length=round(len(y) / SR, 3),
                        takes=[{k: (round(v, 3) if isinstance(v, float) else v) for k, v in r[0].items()} for r in rows])
    print(f"{kind:8s} {source} ({report[kind]['by']}), {len(y) / SR:.3f} s  " +
          '  '.join(f"{r[0]['take']}: score {r[0]['score']:.2f} hit {r[0]['hit']:.3f} tail {r[0]['tail']:.3f} rings {r[0]['rings']} peak {r[0]['peak']:.1f}"
                    for r in rows))
json.dump(report, open(os.path.join(S, 'report.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

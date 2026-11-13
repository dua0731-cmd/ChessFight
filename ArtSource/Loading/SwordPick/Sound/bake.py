# R631 / R634 (11-13): bakes Sword Fight's entrance-screen sounds, set A2 of the mockup page (mockup_page.html, made by mix2.py):
# the game-UI matchmaking takes (M_*) in front with the blade takes (A_*) layered under at 0.4, ring-cut and peak
# normalised, into Assets/Resources/Loading/SwordPick/Sound/Sword*.wav, and prints each clip's hit (Lead*) and the
# gains LoadingScreenView.SwordPickSound.cs uses so the game sounds like the page.
#   uv run --no-project --managed-python --python 3.12 --with miniaudio --with numpy python -I bake.py <this folder> <project root>
import os, sys, json, wave, numpy as np, miniaudio
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from coin2 import rings

S, ROOT = sys.argv[1], sys.argv[2]
OUT = os.path.join(ROOT, 'Assets/Resources/Loading/SwordPick/Sound')
PR = os.path.join(ROOT, 'Assets/Resources/Loading/CourseReveal/Sound')
SR = 44100
FRONT = dict(open='M_open_1', plate='M_plate_1', vs='M_vs_1', tick='M_tick_2', close='M_close_1', card='M_card_1', clash='M_clash_1', bed='M_bed_2')
UNDER = dict(open='A_open_2', plate='A_plate_1', vs='A_vs_2', tick='A_tick_1', close='A_close_1', card='A_card_2', clash='A_clash_2', bed='A_bed_2')
UG = 0.4
BY = dict(open='onset', tick='onset')
# the page's peak per role (dBFS, mix.py PEAK); clash 0 dB is volume 1 in the game
PEAK = dict(open=-1, plate=-7, vs=-2, tick=-5, close=-1.5, card=-6, clash=0, mark=-11, lock=-5, mate=-14, dot=-15)
NAME = dict(open='SwordOpen', plate='SwordPlate', vs='SwordVs', tick='SwordTick', close='SwordClose', card='SwordCard', clash='SwordClash')
NORM = 10 ** (-1 / 20)

def load_mp3(p):
    d = miniaudio.decode_file(p, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    return np.frombuffer(d.samples, dtype=np.float32).astype(np.float64)

def load_wav(p):
    w = wave.open(p); n = w.getnframes(); ch = w.getnchannels(); sr = w.getframerate()
    x = np.frombuffer(w.readframes(n), dtype=np.int16).astype(np.float64) / 32768
    if ch > 1: x = x.reshape(-1, ch).mean(1)
    return x, sr

def env(x, win=0.01):
    n = max(1, int(SR * win)); return np.sqrt(np.convolve(x * x, np.ones(n) / n, mode='same') + 1e-12)

def notch(x, f0, depth_db=-14, q=6):
    A = 10 ** (depth_db / 40); w = 2 * np.pi * f0 / SR; al = np.sin(w) / (2 * q)
    b = np.array([1 + al * A, -2 * np.cos(w), 1 - al * A]); a = np.array([1 + al / A, -2 * np.cos(w), 1 - al / A])
    b /= a[0]; a /= a[0]; y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        o = b[0] * v + b[1] * x1 + b[2] * x2 - a[1] * y1 - a[2] * y2
        x2, x1, y2, y1 = x1, v, y1, o; y[i] = o
    return y

def tame(x):
    for _ in range(4):
        r = rings(x)
        if not r: break
        for c in r: x = notch(x, c['f'])
    return x

def fade_out(x, s=0.04):
    n = min(len(x), int(SR * s)); x = x.copy(); x[-n:] *= np.linspace(1, 0, n); return x

def mark_of(x, by='hit'):
    e = env(x)
    if by == 'onset': return int(np.where(e > e.max() * 0.1)[0][0])
    return int(np.argmax(e))

def prep(take, role):
    x = load_mp3(os.path.join(S, 'raw', take + '.mp3')); x = x - np.mean(x)
    if role != 'bed': x = fade_out(tame(x))
    return x

def rms(x): return np.sqrt(np.mean(x ** 2)) + 1e-9

def stack(a, b, g, by):
    b = b * (np.abs(a).max() / (np.abs(b).max() + 1e-9))
    d = mark_of(a, by) - mark_of(b, by)
    if d < 0: a = np.concatenate([np.zeros(-d), a]); d = 0
    out = np.zeros(max(len(a), d + len(b))); out[:len(a)] += a; out[d:d + len(b)] += b * g
    return out

def write(p, x):
    w = wave.open(p, 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype('<i2').tobytes()); w.close()

os.makedirs(OUT, exist_ok=True)
info = {}
for role in FRONT:
    if role == 'bed': continue
    by = BY.get(role, 'hit')
    x = stack(prep(FRONT[role], role), prep(UNDER[role], role), UG, by)
    # start a hair before the onset, so the clip has no silence ahead of it
    i0 = max(0, mark_of(x, 'onset') - int(.004 * SR)); x = x[i0:]
    x[:int(.002 * SR)] *= np.linspace(0, 1, int(.002 * SR))
    left = len(rings(x))
    x = x * (NORM / np.abs(x).max())
    write(os.path.join(OUT, NAME[role] + '.wav'), x)
    info[role] = dict(lead=round(mark_of(x, by) / SR, 4), gain=round(10 ** (PEAK[role] / 20) / NORM, 4), len=round(len(x) / SR, 3), rings=left)

# R634 the music: O1 "칼싸움 게임 비트" of the OST page (승규 first said O4, then "아니야 1번으로") (ost/mockup_page.html, ost/mix3.py), in place of R631's pulse bed.
# Like the page, it is cut there and loops back a whole number of bars (ost/mix3.py
# music_take: bar 3.75 s by autocorrelation, back to 3.25 s). SwordMusic = the take up to the loop point, SwordMusicLoop =
# the six bars from there, its last .35 s crossfaded into the .35 s before its start so it runs on into its own first
# sample (and the intro runs into it), both stereo.
def load_st(p):
    d = miniaudio.decode_file(p, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=2, sample_rate=SR)
    return np.frombuffer(d.samples, dtype=np.float32).astype(np.float64).reshape(-1, 2)

def write_st(p, x):
    w = wave.open(p, 'wb'); w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype('<i2').tobytes()); w.close()

x = load_st(os.path.join(S, 'ost', 'raw', 'O1_beat_1.mp3')); x = x - x.mean(0)
mono = x.mean(1); h = int(.25 * SR)
r = np.array([np.sqrt(np.mean(mono[i:i + h] ** 2)) + 1e-9 for i in range(0, len(mono) - h, h)])
db = 20 * np.log10(r); live = np.where(db > np.median(db) - 8)[0]; end = int((live[-1] + 1) * h)
hop = 441; m = mono[:end]; e = np.sqrt(np.add.reduceat(m * m, np.arange(0, len(m), hop)) / hop); o = np.maximum(np.diff(e), 0); o -= o.mean()
ac = np.correlate(o, o, 'full')[len(o) - 1:]; lag = np.arange(len(ac)) * hop / SR; w = (lag > 1.4) & (lag < 4.2)
bar = lag[w][np.argmax(ac[w])]
k = int((end / SR - 2.5) // bar); start = int(round(end - k * bar * SR))
xf = int(.35 * SR)
intro = x[:start].copy()
loop = x[start:end].copy(); ramp = np.linspace(0, 1, xf)[:, None]
loop[-xf:] = loop[-xf:] * (1 - ramp) + x[start - xf:start] * ramp
peak = max(np.abs(intro).max(), np.abs(loop).max()); intro *= NORM / peak; loop *= NORM / peak
write_st(os.path.join(OUT, 'SwordMusic.wav'), intro)
write_st(os.path.join(OUT, 'SwordMusicLoop.wav'), loop)
# page: music RMS -24 dB (its first 20 s) where the clash peaks at 0 dB; in the game the clash plays at Effects (.8) x its
# clip's peak (NORM), the music follows Music (.7)
first = np.concatenate([intro, loop])[:20 * SR].mean(1)
music_gain = 10 ** (-24 / 20) * .8 * NORM / rms(first) / .7
info['music'] = dict(intro=round(len(intro) / SR, 4), loop=round(len(loop) / SR, 4), bar=round(bar, 3), bars=k,
                     gain=round(music_gain, 4), rms_db=round(20 * np.log10(rms(first)), 1))

# Pawn Rush's clips reused for the pick: their own peak sets the gain
for role, f in dict(mark='RevealTick0', lock='RevealLock', mate='RevealTick1', dot='RevealDot').items():
    x, sr = load_wav(os.path.join(PR, f + '.wav'))
    info[role] = dict(gain=round(10 ** (PEAK[role] / 20) / (np.abs(x).max() + 1e-9), 4))
json.dump(info, open(os.path.join(S, 'leads.json'), 'w'), indent=1)
for k, v in info.items(): print(k, v)

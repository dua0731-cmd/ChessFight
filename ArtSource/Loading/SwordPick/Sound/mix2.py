# Mix each candidate set onto Sword Fight's entrance-screen timeline (LoadingScreenView.SwordPick.cs numbers).
import os, sys, json, wave, numpy as np, miniaudio
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from coin2 import rings

S = sys.argv[1]
PR = sys.argv[2]          # Pawn Rush's processed clips (Resources/Loading/CourseReveal/Sound)
SR = 44100; OUT_SR = 32000
LEAD = 0.6                # silence before the host's start, so the opening hit has room

A = dict(open='A_open_2', plate='A_plate_1', vs='A_vs_2', tick='A_tick_1', close='A_close_1', card='A_card_2', clash='A_clash_2', bed='A_bed_2')
M = dict(open='M_open_1', plate='M_plate_1', vs='M_vs_1', tick='M_tick_2', close='M_close_1', card='M_card_1', clash='M_clash_1', bed='M_bed_2')
# each set: (front takes, takes layered underneath, gain of the layer underneath)
PICKS = {'A': (A, None, 0), 'A1': (A, M, 0.5), 'A2': (M, A, 0.4)}
BY = dict(open='onset', tick='onset')
# peak each role is set to before mixing (dBFS)
PEAK = dict(open=-1, plate=-7, vs=-2, tick=-5, close=-1.5, card=-6, clash=0, mark=-11, lock=-5, mate=-14, dot=-15)

def load_mp3(p):
    d = miniaudio.decode_file(p, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    return np.frombuffer(d.samples, dtype=np.float32).astype(np.float64)

def load_wav(p):
    w = wave.open(p); n = w.getnframes(); ch = w.getnchannels(); sr = w.getframerate()
    x = np.frombuffer(w.readframes(n), dtype=np.int16).astype(np.float64) / 32768
    if ch > 1: x = x.reshape(-1, ch).mean(1)
    if sr != SR: x = np.interp(np.arange(int(len(x) * SR / sr)) * sr / SR, np.arange(len(x)), x)
    return x

def env(x, win=0.01):
    n = max(1, int(SR * win)); return np.sqrt(np.convolve(x * x, np.ones(n) / n, mode='same') + 1e-12)

def notch(x, f0, depth_db=-14, q=6):
    # biquad peaking cut at a ringing pitch (keeps the steel, chokes the coin-like tone)
    A = 10 ** (depth_db / 40); w = 2 * np.pi * f0 / SR; al = np.sin(w) / (2 * q)
    b = np.array([1 + al * A, -2 * np.cos(w), 1 - al * A]); a = np.array([1 + al / A, -2 * np.cos(w), 1 - al / A])
    b /= a[0]; a /= a[0]; y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        o = b[0] * v + b[1] * x1 + b[2] * x2 - a[1] * y1 - a[2] * y2
        x2, x1, y2, y1 = x1, v, y1, o; y[i] = o
    return y

def tame(x):
    # cut every ring the detector finds, up to 4 passes
    for _ in range(4):
        r = rings(x)
        if not r: break
        for c in r: x = notch(x, c['f'])
    return x

def fade_out(x, s=0.04):
    n = min(len(x), int(SR * s)); x = x.copy(); x[-n:] *= np.linspace(1, 0, n); return x

def at_peak(x, db):
    return x * (10 ** (db / 20) / (np.abs(x).max() + 1e-9))

def mark_of(x, by='hit'):
    e = env(x)
    if by == 'onset': return int(np.where(e > e.max() * 10 ** (-20 / 20))[0][0])
    return int(np.argmax(e))

def place(buf, x, t, gain=1.0, pan=0.0, by='hit'):
    i0 = int((LEAD + t) * SR) - mark_of(x, by)
    if i0 < 0: x = x[-i0:]; i0 = 0
    end = min(len(buf), i0 + len(x))
    l = np.cos((pan + 1) * np.pi / 4) * np.sqrt(2); r = np.sin((pan + 1) * np.pi / 4) * np.sqrt(2)
    buf[i0:end, 0] += x[:end - i0] * gain * l; buf[i0:end, 1] += x[:end - i0] * gain * r

def loop_to(x, n, xf=1.0, start=2.0):
    # fill n samples by crossfading the take back to `start` seconds
    out = x.copy(); k = int(xf * SR); s = int(start * SR)
    while len(out) < n:
        seg = x[s:]; ramp = np.linspace(0, 1, k)
        out[-k:] = out[-k:] * (1 - ramp) + seg[:k] * ramp; out = np.concatenate([out, seg[k:]])
    return out[:n]

def write(p, st, sr=OUT_SR):
    y = st if st.ndim == 2 else st[:, None]
    m = np.stack([np.interp(np.arange(int(len(y) * sr / SR)) * SR / sr, np.arange(len(y)), y[:, c]) for c in range(y.shape[1])], 1)
    w = wave.open(p, 'wb'); w.setnchannels(m.shape[1]); w.setsampwidth(2); w.setframerate(sr)
    w.writeframes((np.clip(m, -1, 1) * 32767).astype('<i2').tobytes()); w.close()

# the timeline (seconds since the host's start)
FOUND_PLATES = [0.45, 0.53, 0.61]          # FoundPlateAt .15 + .08k, 0.3 s slide
VS = 0.73                                  # FoundVsAt .55 + .18 slam
DOTS = [5 / 3, 10 / 3, 5.0]                # three squares over FoundSeconds 5
CARDS_UP = 5.3                             # pick cards rise from 5.0, .05 apart, 0.3 s
MARK, CONFIRM, MATE = 8.0, 9.0, 11.0       # demo: I mark a card, confirm it, a teammate picks
TICKS = [15, 16, 17, 18, 19]               # last five seconds of PickSeconds 15
CLOSE = 20.0                               # CloseAt
TEAM_TITLE, TEAM_CARDS = 20.24, [20.30, 20.37, 20.44]
CLASH_HIT, CLASH_VS, CLASH_TITLE = 23.45, 23.70, 24.0   # TeamSeconds 3 + ClashHit .45 / VsAt .5+.2 / TitleAt 1.0
END = 26.5
TOTAL = int((LEAD + END + 1.2) * SR)

pr = {k: load_wav(os.path.join(PR, f)) for k, f in dict(mark='RevealTick0.wav', lock='RevealLock.wav', mate='RevealTick1.wav', dot='RevealDot.wav').items()}
pr = {k: at_peak(v, PEAK[k]) for k, v in pr.items()}

os.makedirs(os.path.join(S, 'out'), exist_ok=True)
report = {}
def prep(take, role):
    x = load_mp3(os.path.join(S, 'raw', take + '.mp3')); x = x - np.mean(x)
    if role != 'bed': x = fade_out(tame(x))
    return x

def rms(x): return np.sqrt(np.mean(x ** 2)) + 1e-9

def stack(a, b, g, by):
    # line b up on a's hit (or onset) and add it underneath at gain g, a's peak level as reference
    b = b * (np.abs(a).max() / (np.abs(b).max() + 1e-9))
    d = mark_of(a, by) - mark_of(b, by)
    if d < 0: a = np.concatenate([np.zeros(-d), a]); d = 0
    out = np.zeros(max(len(a), d + len(b))); out[:len(a)] += a; out[d:d + len(b)] += b * g
    return out

for name, (front, under, ug) in PICKS.items():
    clip = {}
    for role, take in front.items():
        x = prep(take, role)
        if under:
            y = prep(under[role], role)
            if role == 'bed':
                n = min(len(x), len(y)); x = x[:n] / rms(x[:n]) + ug * y[:n] / rms(y[:n])
            else:
                x = stack(x, y, ug, BY.get(role, 'hit'))
        if role != 'bed': x = at_peak(x, PEAK[role])
        clip[role] = x
    left = {r: len(rings(v)) for r, v in clip.items() if r != 'bed'}
    report[name] = left
    buf = np.zeros((TOTAL, 2))
    # bed: in at the start, a little louder through the last five seconds, cut dead on the clash
    bed = loop_to(clip['bed'], int((LEAD + CLASH_HIT) * SR))
    bed = bed * (10 ** (-25 / 20) / (np.sqrt(np.mean(bed ** 2)) + 1e-9))
    t = np.arange(len(bed)) / SR - LEAD
    g = np.clip(t / 0.6 + 1, 0, 1) * (1 + 0.6 * np.clip((t - 14.5) / 5.5, 0, 1)) * np.where(t < CLOSE, 1, 0.8)
    bed = bed * g; bed[-int(.012 * SR):] *= np.linspace(1, 0, int(.012 * SR))
    buf[:len(bed), 0] += bed; buf[:len(bed), 1] += bed
    # ① found
    place(buf, clip['open'], 0.0, 1, 0, 'onset')
    for k, tt in enumerate(FOUND_PLATES):
        place(buf, clip['plate'], tt, [1, .8, .65][k], -.55); place(buf, clip['plate'], tt, [1, .8, .65][k], .55)
    place(buf, clip['vs'], VS)
    for tt in DOTS: place(buf, pr['dot'], tt, 1, 0, 'onset')
    # ② pick
    place(buf, clip['card'], CARDS_UP, .7)
    place(buf, pr['mark'], MARK, 1, -.2, 'onset'); place(buf, pr['lock'], CONFIRM, 1, -.2)
    place(buf, pr['mate'], MATE, 1, .3, 'onset')
    for k, tt in enumerate(TICKS): place(buf, clip['tick'], tt, .55 + .45 * k / 4, 0, 'onset')
    place(buf, clip['close'], CLOSE)
    # ③ team
    place(buf, clip['plate'], TEAM_TITLE, .7, -.3)
    for k, tt in enumerate(TEAM_CARDS): place(buf, clip['card'], tt, [.8, .6, .5][k], -.4 + .3 * k)
    # ④ clash
    place(buf, clip['clash'], CLASH_HIT)
    place(buf, clip['vs'], CLASH_VS, .45)
    place(buf, clip['plate'], CLASH_TITLE, .35)
    # level-match the sets, then a soft limiter
    buf *= 10 ** (-17 / 20) / (np.sqrt(np.mean(buf ** 2)) + 1e-9)
    buf = np.tanh(buf * 1.1) / np.tanh(1.1)
    buf *= 10 ** (-1 / 20) / np.abs(buf).max()
    write(os.path.join(S, 'out', f'mix_{name}.wav'), buf)
    for role, x in clip.items():
        y = x if role != 'bed' else at_peak(x, -3)
        write(os.path.join(S, 'out', f'{name}_{role}.wav'), np.stack([y, y], 1) * (10 ** (-1 / 20) / (np.abs(y).max() + 1e-9)))
print('rings left after notch:', report)
print('length', round(TOTAL / SR, 2), 's, lead', LEAD)

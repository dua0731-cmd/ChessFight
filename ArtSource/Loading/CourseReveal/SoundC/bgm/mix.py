"""R599: mixes each tense music candidate under the course reveal's real sound cues (same numbers as
LoadingScreenView.Reveal.cs / RevealSound.cs BuildRevealCues, which do not depend on the round's draw) and writes mp3s
for the listening page: <id>_with.mp3 (reveal + music) and <id>_bgm.mp3 (music only), plus now_with.mp3 (no music)."""
import os, wave, json
import numpy as np, miniaudio, lameenc
from scipy import signal

here = os.path.dirname(os.path.abspath(__file__))
snd = 'C:/Work/ChessFight/Assets/Resources/Loading/CourseReveal/Sound'
SR = 44100
T0, T1 = -.3, 11.6          # mix window (reveal seconds)
ONE = 9.8                   # the 1: the music stops dead here, like the countdown drone

# ---- the reveal's numbers (LoadingScreenView.Reveal.cs, R597) ----
PanelEnter, RollStart, RollLand = [.2, .28, .36], [.55, .65, .75], [2.4, 4.2, 6.0]
PanelSlide, TitleAt, TitleSlide, BackPull = .22, .1, .24, 2.1
DotAt, DotFill = [1.5, 2.4, 3.3, 4.2, 5.1, 6.0, 6.4], .1
CountAt, CountIn = [7.8, 8.8, 9.8], .25
StageWide, PanelLeft, PanelStep, PanelWide, DotLeft, DotStep, DotWide = 1280, 36, 400, 420, 96, 70, 58
LeadSharp, LeadTitle, LeadSlam, LeadCountIn = .002, .06, .0596, .045
MINI = {1, 3, 5}            # PawnRushSections: slots 1, 3, 5 are the mini-games (no dot tap)


def hop_times(start, land, first=.065, grow=1.15):
    hops, s = 1, first
    while s < land - start:
        s += first * grow ** hops; hops += 1
    k, at, t = (land - start) / s, start, [start]
    for j in range(hops):
        at += first * grow ** j * k; t.append(at)
    t[-1] = land
    return t


def pan_of(x): return float(np.clip(.9 * (x - StageWide / 2) / (StageWide / 2), -1, 1))


def wav(name):
    with wave.open(os.path.join(snd, name + '.wav')) as w:
        return np.frombuffer(w.readframes(w.getnframes()), '<i2').astype(float) / 32768


cues = []
def add(clip, hit, lead, gain, pan=0., pitch=1.):
    cues.append((clip, hit - lead / pitch, gain, pan, pitch))

add('RevealOpen', 0, LeadSharp, .8)
add('RevealTitle', TitleAt + TitleSlide * (1 - BackPull / (BackPull + 1)), LeadTitle, .55, pan_of(200))
for i in range(3):
    pan = pan_of(PanelLeft + i * PanelStep + PanelWide / 2)
    add('RevealSlam', PanelEnter[i] + PanelSlide, LeadSlam, .6, pan)
    h = hop_times(RollStart[i], RollLand[i]); last = len(h) - 1
    for j in range(last):
        slow = j / (last - 1) if last > 1 else 1
        wob = ((j * 7 + i * 3) % 5 - 2) * .012
        add('RevealTick%d' % ((j + i) % 3), h[j], LeadSharp, .5 + .3 * slow * slow, pan * .9, 1 + wob)
    add('RevealLock', RollLand[i], LeadSharp, .85, pan * .8)
for k in range(7):
    if k not in MINI: add('RevealDot', DotAt[k] - DotFill + .012, LeadSharp, .4, pan_of(DotLeft + k * DotStep + DotWide / 2))
add('RevealCountIn', CountAt[0] - CountIn, LeadCountIn, .5)
bed = wav('RevealCountBed'); add('RevealCountBed', CountAt[-1], len(bed) / SR, .32)
for q in range(3): add('RevealCountLast' if q == 2 else 'RevealCount', CountAt[q], LeadSharp, 1. if q == 2 else .9)

N = int((T1 - T0) * SR)
def place(buf, x, at, gain, pan):
    l, r = np.cos((pan + 1) * np.pi / 4) * np.sqrt(2), np.sin((pan + 1) * np.pi / 4) * np.sqrt(2)
    s = int(round((at - T0) * SR))
    if s < 0: x = x[-s:]; s = 0
    e = min(N, s + len(x)); x = x[:e - s] * gain
    buf[0, s:e] += x * min(l, 1.); buf[1, s:e] += x * min(r, 1.)

clips = {}
sfx = np.zeros((2, N))
for clip, start, gain, pan, pitch in cues:
    x = clips.setdefault(clip, wav(clip))
    if pitch != 1: x = np.interp(np.arange(0, len(x) - 1, pitch), np.arange(len(x)), x)
    place(sfx, x, start, gain, pan)


def load(name):
    d = miniaudio.mp3_read_file_f32(os.path.join(here, 'raw', name + '.mp3'))
    a = np.frombuffer(d.samples, np.float32).reshape(-1, d.nchannels).astype(float).T
    if a.shape[0] == 1: a = np.vstack([a, a])
    return a


def rms_db(x): return 20 * np.log10(np.sqrt((x ** 2).mean()) + 1e-12)


def fill(a, need, force=None):
    """The take, looped on its own beat where it fades out early: repeat a period P from its steady part with a short
    crossfade until it is `need` seconds long."""
    mono = a.mean(0)
    sec = [rms_db(mono[i:i + SR // 4]) for i in range(0, len(mono) - SR // 4, SR // 4)]
    med = np.median(sec[:28])
    # the take holds until its last quarter second still within 6 dB of its usual level (some start quiet, so not the first dip)
    end = (max(k for k in range(len(sec)) if sec[k] >= med - 6) + 1) * SR // 4
    if end >= need * SR: return a[:, :int(need * SR)], None
    # loop period: envelope autocorrelation over the steady part, 1.2 - 4 s
    b, aa = signal.butter(2, 150 / (SR / 2), 'high')
    env = np.convolve(np.abs(signal.lfilter(b, aa, mono[:end])), np.ones(441) / 441, 'same')[::441]
    e = env - env.mean(); ac = np.correlate(e, e, 'full')[len(e) - 1:]
    xf = int(.12 * SR)
    lo, hi = int(1.2 * 100), min(int(4 * 100), len(ac) - 1, (end - xf) // 441 - 1)
    P = (lo + int(np.argmax(ac[lo:hi]))) * 441
    if force: P = int(round(force * 100)) * 441   # a pick written 'take@seconds': loop on that period (a whole bar)
    print('  fill', round(end / SR, 2), 's steady, period', round(P / SR, 3))
    seg = a[:, end - P - xf:end]           # one period (plus the crossfade) ending where the take still holds
    out = a[:, :end].copy()
    while out.shape[1] < need * SR:
        ramp = np.linspace(0, 1, xf)
        tail = out[:, -xf:] * (1 - ramp) + seg[:, :xf] * ramp
        out = np.concatenate([out[:, :-xf], tail, seg[:, xf:]], axis=1)
    return out[:, :int(need * SR)], P / SR


def mp3(path, x):
    pk = np.abs(x).max(); ceil = 10 ** (-1 / 20)
    if pk > ceil: x = ceil * np.tanh(x / ceil) if pk < ceil * 1.3 else x / pk * ceil
    e = lameenc.Encoder(); e.set_bit_rate(160); e.set_in_sample_rate(SR); e.set_channels(2); e.set_quality(2)
    pcm = (np.clip(x.T, -1, 1) * 32767).astype('<i2').tobytes()
    open(path, 'wb').write(e.encode(pcm) + e.flush())


import sys
# R601: a second set (scene-fitting, not tense) comes from picks_scene.json into out_scene/: mix.py picks_scene.json out_scene
pick_file, out_dir = (sys.argv[1:3] + ['picks.json', 'out'][len(sys.argv[1:3]):])
PICKS = json.load(open(os.path.join(here, pick_file)))
out = os.path.join(here, out_dir); os.makedirs(out, exist_ok=True)
mp3(os.path.join(out, 'now_with.mp3'), sfx.copy())
report = {}
for key, take in PICKS.items():
    take, _, force = take.partition('@')
    a = load(take)
    m, period = fill(a, ONE + .05, float(force) if force else None)
    m = m / (np.sqrt((m ** 2).mean()) + 1e-12) * 10 ** (-25 / 20)   # music bed level: RMS -25 dBFS under the cues (at -21 it covered the ticks)
    k = int(.4 * SR); m[:, :k] *= np.linspace(0, 1, k) ** 2           # comes in with the dark
    s = int(ONE * SR); k = int(.012 * SR); m[:, s - k:s] *= np.linspace(1, 0, k); m = m[:, :s]   # cut dead on the 1
    bgm = np.zeros((2, N)); bgm[:, int(-T0 * SR):int(-T0 * SR) + m.shape[1]] = m
    mp3(os.path.join(out, key + '_with.mp3'), sfx + bgm)
    mp3(os.path.join(out, key + '_bgm.mp3'), bgm)
    report[key] = {'take': take, 'loop_period_s': None if period is None else round(period, 3)}
json.dump(report, open(os.path.join(out, 'report.json'), 'w'), indent=1)
print(json.dumps(report), 'cues', len(cues))

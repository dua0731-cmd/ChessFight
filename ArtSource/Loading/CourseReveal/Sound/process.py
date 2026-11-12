"""Builds the course-reveal sound clips (mono 44.1 kHz WAV) from the chosen ElevenLabs takes and writes each clip's
'hit' offset (seconds from the clip's start to the moment that must meet the picture) to leads.json."""
import json, os, sys, wave
import numpy as np
import miniaudio

here = os.path.dirname(os.path.abspath(__file__))
raw = os.path.join(here, "raw")
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "final")
os.makedirs(out, exist_ok=True)
SR = 44100


def load(name):
    d = miniaudio.mp3_read_file_f32(os.path.join(raw, name + ".mp3"))
    a = np.frombuffer(d.samples, dtype=np.float32).reshape(-1, d.nchannels).mean(axis=1).astype(np.float64)
    assert d.sample_rate == SR, d.sample_rate
    return a


def onset(a, frac=.2, start=0):
    env = np.abs(a[start:])
    pk = env.max()
    i = int(np.argmax(env > pk * frac))
    # walk back to where the rise begins (2 % of the peak), at most 4 ms
    j = i
    while j > 0 and i - j < int(.004 * SR) and env[j - 1] > pk * .02:
        j -= 1
    return start + j


def peak_at(a):
    return int(np.argmax(np.abs(a)))


def cut(a, s, length):
    s = max(0, s)
    seg = a[s:s + int(length * SR)]
    if len(seg) < int(length * SR):
        seg = np.pad(seg, (0, int(length * SR) - len(seg)))
    return seg.copy()


def fade(a, fade_in=0.0, out_from=None, out_curve=2.0):
    a = a.copy()
    n = len(a)
    if fade_in > 0:
        k = int(fade_in * SR)
        a[:k] *= np.linspace(0, 1, k)
    if out_from is not None:
        k0 = int(out_from * SR)
        if k0 < n:
            t = np.linspace(0, 1, n - k0)
            a[k0:] *= (1 - t) ** out_curve
    # never end on a step
    k = min(int(.003 * SR), n)
    a[n - k:] *= np.linspace(1, 0, k)
    return a


def norm(a, peak_db):
    return a / np.abs(a).max() * 10 ** (peak_db / 20)


def save(name, a):
    a = np.clip(a, -1, 1)
    with wave.open(os.path.join(out, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((a * 32767).astype("<i2").tobytes())


leads = {}
pre = int(.002 * SR)  # 2 ms kept before every transient

# the dark curtain lifts (0-0.45 s): the swell peaks with it
a = load("Open_2")
save("RevealOpen", norm(fade(cut(a, 0, 1.0), out_from=.6), -9))
leads["RevealOpen"] = 0.0

# a mini-game frame's reel lands: a chunky latch
a = load("ReelStop_2")
s = onset(a) - pre
save("RevealReelStop", norm(fade(cut(a, s, .55), out_from=.22), -4))
leads["RevealReelStop"] = pre / SR

# an obstacle's medallion flips: a flutter, then the tock on the landing
a = load("Flip_2")
o = onset(a)
lead = .100
save("RevealFlip", norm(fade(cut(a, o - int(lead * SR), .5), fade_in=.02, out_from=.25), -5))
leads["RevealFlip"] = lead

# the war room's crown
a = load("Crown_1")
s = onset(a) - pre
save("RevealCrown", norm(fade(cut(a, s, .62), out_from=.22), -3))
leads["RevealCrown"] = pre / SR

# the stamp (R559, pick B): a big wooden chess piece slammed onto the board over a sub boom, both from their
# transients, then a soft tanh limiter whose drive matches the first 0.3 s to the loudness of the R557 stamp - 0.5 dB
st = load("Chess_1")
bo = load("Boom_1")
s1, s2 = onset(st) - pre, onset(bo) - pre
mix = norm(cut(st, s1, .85), 0) * 1.0 + norm(cut(bo, s2, .85), 0) * .6
mix = norm(fade(mix, out_from=.3, out_curve=2.4), 0)
ceil, drive = .93, 1.2313
save("RevealStamp", ceil * np.tanh(drive * mix / ceil))
leads["RevealStamp"] = pre / SR

# the riser into the stamp: a reversed boom (low) and a reversed burst (air) that end hard on the hit
L = .30
low = cut(bo, onset(bo), L)[::-1]
bu = load("Burst_3")
air = cut(bu, peak_at(bu), L)[::-1]
r = norm(low, 0) * .7 + norm(air, 0) * .55
r *= np.linspace(0, 1, len(r)) ** 1.6
k = int(.002 * SR)
r[-k:] *= np.linspace(1, 0, k)
save("RevealRiser", norm(r, -5))
leads["RevealRiser"] = L  # its end meets the hit

# air bursting out with the rays
p = peak_at(bu)
save("RevealBurst", norm(fade(cut(bu, p - int(.05 * SR), .6), fade_in=.01, out_from=.12), -13))
leads["RevealBurst"] = .05

# the frame's "?" cover pops off as the reel starts
a = load("Swish_2")
s = onset(a) - pre
save("RevealCover", norm(fade(cut(a, s, .3), out_from=.08), -16))
leads["RevealCover"] = pre / SR

# the glint across the stamp: a soft swish whose loudest point meets the glint's middle
a = load("Swish_3")
p = peak_at(a)
s = max(0, p - int(.12 * SR))
save("RevealGlint", norm(fade(cut(a, s, .32), fade_in=.04, out_from=.16), -15))
leads["RevealGlint"] = (p - s) / SR

# reel ticks: single clicks cut out of the slowing prize wheel
a = load("ReelClicks_1")
env = np.abs(a)
pk = env.max()
hits, i = [], 0
while i < len(a):
    if env[i] > pk * .25:
        w = env[i:i + int(.01 * SR)]
        top = i + int(np.argmax(w))
        hits.append(top)
        i = top + int(.03 * SR)
    else:
        i += 1
clicks = []
for top in hits:
    s = top
    while s > 0 and top - s < int(.004 * SR) and env[s - 1] > env[top] * .03:
        s -= 1
    seg = cut(a, s - pre, .07)
    level = 20 * np.log10(np.abs(seg).max() + 1e-9)
    spec = np.abs(np.fft.rfft(seg * np.hanning(len(seg))))
    fr = np.fft.rfftfreq(len(seg), 1 / SR)
    centroid = float((spec * fr).sum() / (spec.sum() + 1e-9))
    clicks.append((top / SR, level, centroid, seg))
print("clicks", len(clicks), [(round(t, 3), round(l, 1), int(c)) for t, l, c, _ in clicks])
# keep the four nearest the median brightness, isolated from the next click by 45 ms+
times = [c[0] for c in clicks]
iso = [c for k, c in enumerate(clicks) if (k + 1 >= len(clicks) or times[k + 1] - c[0] > .045) and (k == 0 or c[0] - times[k - 1] > .045)]
med = np.median([c[2] for c in iso])
iso.sort(key=lambda c: abs(c[2] - med))
for n, c in enumerate(iso[:4]):
    save("RevealTick%d" % n, norm(fade(c[3], out_from=.02, out_curve=3), -11))
leads["RevealTick"] = pre / SR
print("ticks from", [round(c[0], 3) for c in iso[:4]])

json.dump(leads, open(os.path.join(out, "leads.json"), "w"), indent=1)
print(json.dumps(leads))

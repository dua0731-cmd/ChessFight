# R871 (user 11-27: "A2로 해서 bgm이랑 이질감 없이 적용해"): take A2 of the 장외! stamp sound (raw/A_2.mp3, prompts.md) made
# into Assets/Resources/SwordFightSkillSfx/RingOut.wav, which SwordFightSkillSfx loads with the other match sounds (and
# matches to their loudness, Prepare) and SwordFightMatch.Hud plays with the stamp.
# To sit with the match music (Assets/Resources/SwordFightMusic/MatchMusicLoop.wav) instead of on top of it, dry and
# close-miked as the take is:
#   - its top end is eased down towards the music's brightness (a gentle one-pole low-pass, only as far as the take is
#     brighter than the music),
#   - a short warm room is put under it (a 0.45 s decaying noise tail, low-passed, about -15 dB), so it rings in a space
#     as the music does, and
#   - the lead-in is cut to 5 ms before the hit (the stamp pops in within 0.13 s), the end faded out.
#   uv run --no-project --python "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" --with miniaudio --with numpy python -I bake.py <this folder> <project root>
import os, sys, wave, numpy as np, miniaudio

S, ROOT = sys.argv[1], sys.argv[2]
SR = 44100
OUT = os.path.join(ROOT, 'Assets/Resources/SwordFightSkillSfx/RingOut.wav')
MUSIC = os.path.join(ROOT, 'Assets/Resources/SwordFightMusic/MatchMusicLoop.wav')


def load(p):
    d = miniaudio.decode_file(p, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    return np.frombuffer(d.samples, dtype=np.float32).astype(np.float64)


def centroid(x):
    spec = np.abs(np.fft.rfft(x * np.hanning(len(x))))
    f = np.fft.rfftfreq(len(x), 1 / SR)
    return float((spec * f).sum() / max(spec.sum(), 1e-9))


def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


x = load(os.path.join(S, 'raw', 'A_2.mp3'))
music = load(MUSIC)
# the music's brightness over its loudest stretch (a 2 s window every second)
win = 2 * SR
best = max(range(0, max(1, len(music) - win), SR), key=lambda i: float((music[i:i + win] ** 2).mean()))
mc, tc = centroid(music[best:best + win]), centroid(x)

# lead-in: 5 ms before the first sample over 1/16 of the peak
top = np.abs(x).max()
first = max(0, int(np.argmax(np.abs(x) > top / 16)) - SR // 200)
x = x[first:]

# the top end, eased towards the music's (never brighter than the take was; at most halfway to a 5 kHz roll-off)
cut = 16000.0 if tc <= mc else float(np.clip(16000 * mc / tc, 5000, 16000))
x = 0.65 * x + 0.35 * lowpass(x, cut) if cut >= 16000 else lowpass(x, cut)

# a short warm room under it
rng = np.random.default_rng(870)
n = int(0.45 * SR)
ir = rng.standard_normal(n) * np.exp(-np.arange(n) / (0.11 * SR))
ir = lowpass(ir, 3500)
ir /= np.sqrt((ir ** 2).sum())
wet = np.convolve(x, ir)[:len(x) + n]
wet = np.concatenate([wet, np.zeros(len(x) + n - len(wet))])
dry = np.concatenate([x, np.zeros(n)])
y = dry + wet * 10 ** (-15 / 20) * (np.abs(dry).max() / max(np.abs(wet).max(), 1e-9))

# end: cut where it has fallen 50 dB under its peak (plus a little), a 60 ms fade
e = np.sqrt(np.convolve(y * y, np.ones(220) / 220, mode='same'))
last = int(np.where(e > e.max() * 10 ** (-50 / 20))[0][-1]) + SR // 50
y = y[:min(len(y), last)]
fade = min(len(y), int(0.06 * SR))
y[-fade:] *= np.linspace(1, 0, fade)
y *= 10 ** (-1 / 20) / np.abs(y).max()

with wave.open(OUT, 'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())
print(f'music brightness {mc:.0f} Hz, take {tc:.0f} Hz -> low-pass {cut:.0f} Hz; {len(y) / SR:.2f} s -> {OUT}')

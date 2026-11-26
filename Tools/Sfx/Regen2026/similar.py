"""2026-11-26 (R846 / R847): how far a clip sounds from the old clip it replaces, in numbers.

  python3 -I Tools/Sfx/Regen2026/similar.py OLD.mp3 NEW.mp3 [NEW2.mp3 ...]

The old clips (made on the free plan, R845) are only a reference: never shipped again. Two measures, both in dB
and both normalised to the clip's own loudest point, so the overall level (set at load) does not count:
  bands  level in nine octave bands 30 Hz - 16 kHz (tone: low thud vs bright rattle), floor -40 dB
  env    loudness every 20 ms from the first sound (shape: swell, hit, decay), floor -40 dB, over the old length
distance = mean |band difference| + mean |envelope difference| (lower is closer; 0 = same).
For steady loops (STEADY) only the bands count: where a loop starts says nothing.
"""
import subprocess, sys
import numpy as np

SR = 44100
EDGES = [30, 60, 120, 250, 500, 1000, 2000, 4000, 8000, 16000]
STEADY = {'ChainRun', 'BishopRopeStrain', 'KnightAir'}


def load(path):
    raw = subprocess.run(['ffmpeg', '-v', 'quiet', '-i', path, '-ac', '1', '-ar', str(SR), '-f', 'f32le', '-'],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)


def bands(x):
    X = np.abs(np.fft.rfft(x)) ** 2
    f = np.fft.rfftfreq(len(x), 1 / SR)
    lv = np.array([10 * np.log10(X[(f >= a) & (f < b)].sum() + 1e-12) for a, b in zip(EDGES, EDGES[1:])])
    return np.maximum(lv - lv.max(), -40)


def envelope(x, frames):
    fr = int(0.02 * SR)
    a = np.abs(x)
    start = int(np.argmax(a > a.max() * 0.03))
    x = x[start:]
    e = np.array([10 * np.log10((x[i:i + fr] ** 2).mean() + 1e-12) if i < len(x) else -200
                  for i in range(0, frames * fr, fr)])
    return np.maximum(e - e.max(), -40)


def distance(old, new, name=''):
    b = float(np.abs(bands(old) - bands(new)).mean())
    if name in STEADY:
        return b, b, 0.0
    frames = max(5, int(len(old) / SR / 0.02))
    e = float(np.abs(envelope(old, frames) - envelope(new, frames)).mean())
    return b + e, b, e


if __name__ == '__main__':
    old = load(sys.argv[1])
    name = sys.argv[1].split('/')[-1].rsplit('.', 1)[0]
    for p in sys.argv[2:]:
        d, b, e = distance(old, load(p), name)
        print(f'{p.split("/")[-1]:48s} distance {d:5.1f}  (bands {b:4.1f}, envelope {e:4.1f})')

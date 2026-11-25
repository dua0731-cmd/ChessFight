"""2026-11-25: the tutorial's ambience. Takes from the ElevenLabs flow "튜토리얼 독수리 오프닝 소리" (92W4uG7w7gbSbvADmfh7,
eleven_text_to_sound_v2), the user's picks 32 and 33, saved in sfx_raw/:
  meadow_wind.mp3  32 "Gentle outdoor breeze over a sunny meadow of tall grass ..." (2 s)
      -> Assets/Resources/TutorialSfx/WindLoop.wav: a 12 s loop that joins end to start. The take is only 2 s, so the
         loop is woven from it: 1 s Hann grains from random places in the take, half overlapping (the overlap of two
         Hann windows sums to one, so the level holds), the last grain's tail laid over the start.
  courtyard.mp3    33 "Peaceful sunny castle courtyard ambience in the morning: distant small songbirds ..." (1 s)
      -> Assets/Resources/TutorialSfx/Birds.wav: one short burst of chirps, trimmed and faded, played now and then
         (TutorialAmbience) - a 1 s loop of birds would be heard repeating.
Mono 44.1 kHz 16 bit, peaks at -3 dB (the game sets the level).

  uv run --no-project --with miniaudio --with numpy python -I ArtSource/Tutorial/bake_ambience.py
"""
import os, wave

import miniaudio
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'sfx_raw')
OUT = os.path.normpath(os.path.join(HERE, '../../Assets/Resources/TutorialSfx'))
SR = 44100


def load(name):
    d = miniaudio.decode_file(os.path.join(RAW, name), output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    return np.asarray(d.samples, dtype=np.float64)


def write(name, y, peak_db=-3.0):
    y = y * 10 ** (peak_db / 20) / (np.abs(y).max() or 1.0)
    with wave.open(os.path.join(OUT, name), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(np.rint(np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())
    print('%s: %.2f s' % (name, len(y) / SR))


def weave(x, seconds, grain=1.0, seed=7):
    rng = np.random.default_rng(seed)
    g = int(grain * SR)
    hop = g // 2
    n = int(seconds * SR) // hop * hop
    win = np.hanning(g + 1)[:-1]   # periodic Hann: two at half overlap sum to one
    body = x[int(.05 * SR):len(x) - int(.05 * SR)]
    out = np.zeros(n + g)
    for start in range(0, n, hop):
        at = rng.integers(0, len(body) - g)
        out[start:start + g] += body[at:at + g] * win
    out[:g] += out[n:n + g]        # the tail past the end goes over the start: the loop joins
    return out[:n]


os.makedirs(OUT, exist_ok=True)
write('WindLoop.wav', weave(load('meadow_wind.mp3'), 12.0))

b = load('courtyard.mp3')
a = np.abs(b)
on = np.nonzero(a > a.max() * 10 ** (-40 / 20))[0]
b = b[max(0, on[0] - int(.01 * SR)):on[-1] + int(.05 * SR)].copy()
fi, fo = int(.02 * SR), min(len(b) // 3, int(.25 * SR))
b[:fi] *= np.linspace(0, 1, fi)
b[-fo:] *= np.linspace(1, 0, fo) ** 1.5
write('Birds.wav', b)

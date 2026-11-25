"""2026-11-25: the tutorial opening's eagle sounds. Takes from the ElevenLabs flow "튜토리얼 독수리 오프닝 소리"
(92W4uG7w7gbSbvADmfh7, eleven_text_to_sound_v2), the user's picks 1, 4, 6, 7, saved in sfx_raw/:
  wing.mp3   one wing beat      -> Assets/Resources/TutorialSfx/EagleWing.wav
  cry.mp3    the eagle's cry    -> EagleCry.wav
  crash.mp3  hops, belly, skid  -> EagleCrash.wav
  ruffle.mp3 feathers and pants -> EagleRuffle.wav
Each take: the silence before the first sound and after the last is cut, a short fade in and a longer fade out, mono
44.1 kHz 16 bit, peak set per sound (Game code sets the volume).

  uv run --no-project --python 3.12 --with miniaudio --with numpy python -I ArtSource/Tutorial/bake_eagle_sfx.py
"""
import os, wave

import numpy as np
import miniaudio

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'sfx_raw')
OUT = os.path.normpath(os.path.join(HERE, '../../Assets/Resources/TutorialSfx'))
RATE = 44100

# name: (take, floor dB under the peak that counts as sound, fade in s, fade out s, peak dB)
JOBS = {
    'EagleWing':   ('wing.mp3',   -38, 0.004, 0.12, -3.0),
    'EagleCry':    ('cry.mp3',    -40, 0.006, 0.25, -3.0),
    'EagleCrash':  ('crash.mp3',  -40, 0.003, 0.30, -2.0),
    'EagleRuffle': ('ruffle.mp3', -40, 0.020, 0.30, -6.0),
}


def load(path):
    d = miniaudio.decode_file(path, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=RATE)
    return np.asarray(d.samples, dtype=np.float32)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, (take, floor_db, fin, fout, peak_db) in JOBS.items():
        x = load(os.path.join(RAW, take))
        peak = float(np.max(np.abs(x))) or 1.0
        loud = np.nonzero(np.abs(x) > peak * 10 ** (floor_db / 20))[0]
        a, b = max(0, loud[0] - int(0.005 * RATE)), min(len(x), loud[-1] + int(0.03 * RATE))
        if name == 'EagleWing': b = min(b, a + int(0.55 * RATE))   # the take's stray second flutter at its end is dropped
        y = x[a:b].copy()
        n_in, n_out = min(len(y) // 4, int(fin * RATE)), min(len(y) // 2, int(fout * RATE))
        if n_in > 0: y[:n_in] *= np.linspace(0, 1, n_in, dtype=np.float32)
        if n_out > 0: y[-n_out:] *= np.linspace(1, 0, n_out, dtype=np.float32) ** 1.5
        y *= 10 ** (peak_db / 20) / (float(np.max(np.abs(y))) or 1.0)
        path = os.path.join(OUT, name + '.wav')
        with wave.open(path, 'wb') as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
            w.writeframes((np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())
        print('%s: %.2f s (cut %.2f s head, %.2f s tail of %.2f s)' % (name, len(y) / RATE, a / RATE, (len(x) - b) / RATE, len(x) / RATE))


main()

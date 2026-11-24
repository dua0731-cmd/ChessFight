"""2026-11-23: footsteps on the grand stair hall's glass stepping stones (사용자 "대계단 코스에서 유리 밟을 때 나는 소리
넣어줘"). Takes from the ElevenLabs flow "대계단 유리 발소리" (taIwcW5WbzmoZtzcjVbx, eleven_text_to_sound_v2, prompt
influence 0.3), four of each (each came back 1 - 2 s long), saved as takes_glass/<Kind>_<n>.mp3:
  GlassWalk  "Footsteps of a small light figure walking at a steady quick pace across a thick glass floor panel: about
              nine separate steps, each one a short crisp hard tap with a faint bright glassy ring, like a firm heel on a
              thick pane of glass. Clean and close-mic, dry, no cracking, no breaking, no shattering, no music, no voice."
  GlassRun   "Fast light running footsteps of a small figure across a thick glass floor panel: about ten quick separate
              steps, each one a short crisp glassy tap with a faint bright ring. ..."

Cut as bake_wade.py cuts the water steps (the same code, these takes and lengths), the six that stand out most kept, all
matched to one short loudness, peak -1 dB:
  -> Assets/Resources/PawnRushStepSfx/GlassWalk0..5.wav, GlassRun0..5.wav (PawnRushStepSfx plays them on a GlassFloor).

  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python ArtSource/PawnRush/Sfx/bake_glass.py
"""
import json, os, wave

import numpy as np
try:
    import miniaudio
except ImportError:
    miniaudio = None
    import aud

HERE = os.path.dirname(os.path.abspath(__file__))
TAKES = os.path.join(HERE, 'takes_glass')
STEPS = os.path.normpath(os.path.join(HERE, '../../../Assets/Resources/PawnRushStepSfx'))
SR = 44100
# kind -> (longest step in seconds, most kept)
KINDS = {'GlassWalk': (.16, 6), 'GlassRun': (.12, 6)}


def load(path):
    if miniaudio is not None:
        d = miniaudio.decode_file(path, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=2, sample_rate=SR)
        x = np.frombuffer(d.samples, np.float32).astype(np.float64).reshape(-1, 2)
    else:
        x = np.asarray(aud.Sound(path).resample(SR, False).data(), np.float64)
        if x.ndim == 1 or x.shape[1] == 1:
            x = np.repeat(x.reshape(-1, 1), 2, axis=1)
    corr = np.corrcoef(x[:, 0], x[:, 1])[0, 1]
    return x.mean(1) if corr >= .5 else x[:, int((x[:, 1] ** 2).sum() > (x[:, 0] ** 2).sum())]


def opening(y):
    """From 2 ms before the step first comes within 12 dB of its loudest 2 ms (bake_moves.opening, without the lift)."""
    k = int(.002 * SR)
    e = np.array([np.mean(y[i:i + k] ** 2) for i in range(0, len(y) - k, k)])
    db = 10 * np.log10(e + 1e-12)
    db -= db.max()
    first = max(0, (int(np.argmax(db >= -12)) - 1) * k)
    return y[first:].copy(), first


def short_db(y, span=.05):
    k = int(span * SR)
    if len(y) <= k:
        return 20 * np.log10(np.sqrt((y ** 2).mean()) + 1e-12)
    return 10 * np.log10(np.convolve(y ** 2, np.ones(k) / k, 'valid').max() + 1e-12)


def write(path, y):
    with wave.open(path, 'wb') as out:
        out.setnchannels(1); out.setsampwidth(2); out.setframerate(SR)
        out.writeframes(np.rint(np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())


def steps_in(m, longest):
    hop = int(.005 * SR)
    env = np.array([np.sqrt((m[i:i + hop] ** 2).mean()) for i in range(0, len(m) - hop, hop)])
    db = 20 * np.log10(env + 1e-9)
    floor = np.percentile(db, 20)
    peaks = []
    for i in range(8, len(db) - 1):
        lo, hi = max(0, i - 16), min(len(db), i + 17)
        if db[i] < db[lo:hi].max() or db[i] < floor + 10:
            continue
        if db[max(0, i - 8):i].min() > db[i] - 6:            # soft water swishes rise gentler than dry steps
            continue
        if peaks and i - peaks[-1] < 16:
            continue
        peaks.append(i)
    out = []
    onsets = []
    for p in peaks:
        j = p
        while j > 0 and db[j - 1] > db[p] - 20 and p - j < 12:
            j -= 1
        onsets.append(j * hop)
    for n, on in enumerate(onsets):
        nxt = onsets[n + 1] if n + 1 < len(onsets) else len(m)
        a = max(0, on - int(.003 * SR))
        b = min(nxt - int(.01 * SR), on + int(longest * SR))
        if b - a < int(.04 * SR):
            continue
        y, cut = opening(m[a:b])
        f = min(len(y) // 3, int(.03 * SR))                  # glass rings on a little: the water steps' fade
        y[-f:] *= np.linspace(1, 0, f) ** 2
        y[:int(.001 * SR)] *= np.linspace(0, 1, int(.001 * SR))
        out.append(((a + cut) / SR, y))
    return out


report = {}
for kind, (longest, most) in KINDS.items():
    clips = []
    for fname in sorted(os.listdir(TAKES)):
        if fname.startswith(kind + '_') and fname.endswith('.mp3'):
            clips += [(fname, t, y) for t, y in steps_in(load(os.path.join(TAKES, fname)), longest)]
    clips.sort(key=lambda c: -short_db(c[2]))
    clips = clips[:most]
    target = np.median([short_db(y) for _, _, y in clips])
    clips = [(f, t, y * 10 ** ((target - short_db(y)) / 20)) for f, t, y in clips]
    g = 10 ** (-1 / 20) / max(np.abs(y).max() for _, _, y in clips)
    for old in os.listdir(STEPS):
        if old.startswith(kind) and old[len(kind):len(kind) + 1].isdigit() and old.endswith('.wav'):
            os.remove(os.path.join(STEPS, old))
    for i, (f, t, y) in enumerate(clips):
        write(os.path.join(STEPS, '%s%d.wav' % (kind, i)), y * g)
    report[kind] = {'kept': [(f, round(t, 3), round(len(y) / SR, 3)) for f, t, y in clips],
                    'step_loudness_db': round(short_db(clips[0][2] * g), 1)}
print(json.dumps(report, indent=1, ensure_ascii=False))

"""R652: bakes the Pawn Rush movement sounds from the user's ElevenLabs takes in raw/ (prompts written 2026-11-14 from the
user's screen recordings of a dive and a grab: dive '휙', grab '햡', jump '퐁', light landing '톡', belly flop '털썩',
walk '총총', sprint '다다다').

  raw/Dive.wav  Grab.wav  Jump.wav  LandLight.wav  LandFlop.wav  ->  Assets/Resources/PawnRushSkillSfx/Course*.wav
      one moment each, played by PawnRushSkillSfx on the host's movement events (CourseDive, CourseGrab, CourseJump,
      CourseLand; the landing's count picks LandLight or LandFlop)
  raw/Walk.wav (about nine steps)  raw/Run.wav (nine)  ->  Assets/Resources/PawnRushStepSfx/Walk0..n.wav, Run0..n.wav
      single footsteps cut from the takes, taken in turn by PawnRushStepSfx so no two steps in a row are the same

Every clip: mono (the mid of the two channels, or the louder channel where the two are not alike - the dive's are
nearly opposite, and their sum would thin it), started on its hit (R654, `opening`: 2 ms before it is within 12 dB
of its loudest, a slow rise lifted), the tail cut where it falls 50 dB under the peak (and at most MAX seconds), a
10 ms fade out, peak at -1 dBFS. The one-shots are matched again at load (PawnRushSkillSfx.Prepare: the loudest 0.3 s to -21 dB, at most x8),
so their Level numbers set how loud each is; the footsteps are matched here, every step to the same short loudness,
and PawnRushStepSfx sets their level.

Needs only numpy: run with Blender's Python from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.1/5.1/python/bin/python.exe" -I ArtSource/PawnRush/Sfx/bake_moves.py
"""
import json, os, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, '../../..')
ONE = os.path.join(ROOT, 'Assets/Resources/PawnRushSkillSfx')
STEPS = os.path.join(ROOT, 'Assets/Resources/PawnRushStepSfx')
# take -> (clip name, longest it may run in seconds)
MOMENTS = {'Dive': ('CourseDive', .25), 'Grab': ('CourseGrab', .12), 'Jump': ('CourseJump', .2),
           'LandLight': ('CourseLandLight', .2), 'LandFlop': ('CourseLandFlop', .42)}
# take -> (clip prefix, longest step in seconds, most steps kept)
# R657: shorter steps (were .16 / .11 s) now that they come 5.2 and 6.6 times a second ('탁탁탁탁', '타다다다닷')
STEP_TAKES = {'Walk': ('Walk', .12, 6), 'Run': ('Run', .09, 6)}


def load(name):
    w = wave.open(os.path.join(HERE, 'raw', name + '.wav'))
    x = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float64).reshape(-1, w.getnchannels()) / 32768
    return x, w.getframerate()


def mono(x):
    if x.shape[1] == 1: return x[:, 0], 'mono'
    corr = np.corrcoef(x[:, 0], x[:, 1])[0, 1]
    if corr >= .5: return x.mean(1), f'mid (L/R likeness {corr:.2f})'
    louder = int((x[:, 1] ** 2).sum() > (x[:, 0] ** 2).sum())
    return x[:, louder], f'{"LR"[louder]} channel (L/R likeness {corr:.2f})'


def opening(y, sr):
    """R654 (사용자 "앞 쪽에 빈소리가 있어서 그런지 바로 출력되지 않는 것처럼 보여"): the clip starts 2 ms before it first
    comes within 12 dB of its loudest 2 ms (was: the first sample over 1/16 of the peak, which kept a quiet lead-in), and a
    body that only comes more than 20 ms in (the jump's rising 'fwup') has its opening lifted to the body's level (at
    most +12 dB, smoothed over 2 ms), so the sound is there the moment it starts. Returns the clip and the cut, s."""
    k = int(.002 * sr)
    e = np.array([np.mean(y[i:i + k] ** 2) for i in range(0, len(y) - k, k)])
    db = 10 * np.log10(e + 1e-12)
    db -= db.max()
    i12 = int(np.argmax(db >= -12))
    first = max(0, (i12 - 1) * k)
    y = y[first:].copy()
    db = db[max(0, i12 - 1):]
    i3 = int(np.argmax(db >= -3))
    if i3 * 2 > 20:
        body = np.median(db[i3:i3 + 25])
        lift = np.clip(body - 1 - db[:i3], 0, 12)
        centres = (np.arange(i3) + .5) * k
        g_db = np.interp(np.arange(len(y)), np.concatenate([centres, [i3 * k + k]]), np.concatenate([lift, [0]]), right=0)
        g_db = np.convolve(g_db, np.ones(k) / k, 'same')
        y *= 10 ** (g_db / 20)
    return y, first / sr


def trim(m, sr, longest):
    y, start = opening(m, sr)
    a = np.abs(y)
    top = a.max()
    over = np.where(a > top * 10 ** (-50 / 20))[0]
    last = min(over[-1] + 1, int(longest * sr))
    y = y[:last].copy()
    f = min(len(y) // 3, int(.010 * sr))
    y[-f:] *= np.linspace(1, 0, f) ** 2
    k = int(.001 * sr)
    y[:k] *= np.linspace(0, 1, k)
    return y, start


def write(path, y, sr):
    with wave.open(path, 'wb') as out:
        out.setnchannels(1); out.setsampwidth(2); out.setframerate(sr)
        out.writeframes(np.rint(np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())


def short_db(y, sr, span=.05):
    k = int(span * sr)
    if len(y) <= k: return 20 * np.log10(np.sqrt((y ** 2).mean()) + 1e-12)
    c = np.convolve(y ** 2, np.ones(k) / k, 'valid')
    return 10 * np.log10(c.max() + 1e-12)


def prepared_db(y, sr):
    """Short loudness after PawnRushSkillSfx.Prepare (loudest 0.3 s to -21 dB, at most x8, peaks under 0.98)."""
    k = int(.3 * sr)
    pad = np.concatenate([y, np.zeros(max(0, k - len(y)))])
    loud = np.sqrt(np.convolve(pad ** 2, np.ones(k) / k, 'valid').max())
    g = min(8.0, 10 ** (-21 / 20) / max(loud, 1e-9), .98 / np.abs(y).max())
    return short_db(y * g, sr)


report = {}
os.makedirs(ONE, exist_ok=True)
for take, (name, longest) in MOMENTS.items():
    x, sr = load(take)
    m, how = mono(x)
    y, start = trim(m, sr, longest)
    y *= 10 ** (-1 / 20) / np.abs(y).max()
    write(os.path.join(ONE, name + '.wav'), y, sr)
    report[name] = {'from': how, 'starts_at_s': round(start, 3), 'length_s': round(len(y) / sr, 3),
                    'loudness_after_prepare_db': round(prepared_db(y, sr), 1)}

os.makedirs(STEPS, exist_ok=True)
for take, (prefix, longest, most) in STEP_TAKES.items():
    x, sr = load(take)
    m, how = mono(x)
    hop = int(.005 * sr)
    env = np.array([np.sqrt((m[i:i + hop] ** 2).mean()) for i in range(0, len(m) - hop, hop)])
    db = 20 * np.log10(env + 1e-9)
    floor = np.percentile(db, 20)
    # a step: the loudest 5 ms within 80 ms either side, at least 15 dB over the take's quiet floor, coming up from
    # at least 10 dB lower in the 40 ms before it
    peaks = []
    for i in range(8, len(db) - 1):
        lo, hi = max(0, i - 16), min(len(db), i + 17)
        if db[i] < db[lo:hi].max() or db[i] < floor + 15: continue
        if db[max(0, i - 8):i].min() > db[i] - 10: continue
        if peaks and i - peaks[-1] < 16: continue
        peaks.append(i)
    loudest = max(db[p] for p in peaks)
    kept = [p for p in peaks if db[p] > loudest - 8]
    onsets = []
    for p in kept:
        j = p
        while j > 0 and db[j - 1] > db[p] - 20 and p - j < 12: j -= 1
        onsets.append(j * hop)
    clips = []
    for n_, on in enumerate(onsets):
        nxt = onsets[n_ + 1] if n_ + 1 < len(onsets) else len(m)
        a = max(0, on - int(.003 * sr))
        b = min(nxt - int(.015 * sr), on + int(longest * sr))
        y, cut = opening(m[a:b], sr)   # R654: from the step's hit (the sprint's came 10 ms in)
        a += int(round(cut * sr))
        f = min(len(y) // 3, int(.015 * sr))
        y[-f:] *= np.linspace(1, 0, f) ** 2
        y[:int(.001 * sr)] *= np.linspace(0, 1, int(.001 * sr))
        clips.append((a / sr, y))
    # the steps that stand out least from their neighbours' tails go first when there are more than `most`
    clips.sort(key=lambda c: -short_db(c[1], sr))
    clips = sorted(clips[:most], key=lambda c: c[0])
    target = np.median([short_db(y, sr) for _, y in clips])
    clips = [(t, y * 10 ** ((target - short_db(y, sr)) / 20)) for t, y in clips]
    g = 10 ** (-1 / 20) / max(np.abs(y).max() for _, y in clips)
    for old in os.listdir(STEPS):
        if old.startswith(prefix) and old[len(prefix):len(prefix) + 1].isdigit() and old.endswith('.wav'):
            os.remove(os.path.join(STEPS, old))
    for i, (t, y) in enumerate(clips):
        write(os.path.join(STEPS, f'{prefix}{i}.wav'), y * g, sr)
    report[prefix] = {'from': how, 'steps_found': len(peaks), 'kept': len(clips),
                      'at_s': [round(t, 3) for t, _ in clips], 'lengths_s': [round(len(y) / sr, 3) for _, y in clips],
                      'step_loudness_db': round(short_db(clips[0][1] * g, sr), 1)}
print(json.dumps(report, indent=1, ensure_ascii=False))

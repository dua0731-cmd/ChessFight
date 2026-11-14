"""R652: bakes the Pawn Rush race music, the user's ElevenLabs Music v2.5 take in raw/ (150 BPM, F major, 128 s, from the
2026-11-14 "comedic brass band chase" prompt: oom-pah tuba, staccato trumpets, trombone slides, marching snare) into

  Assets/Resources/PawnRushMusic/RaceMusic.wav      the take from its start up to the loop point (once)
  Assets/Resources/PawnRushMusic/RaceMusicLoop.wav  the bars after it, whose end runs into its own start

for PawnRush/PawnRushRoundMusic.cs, which plays the first and schedules the second right where it ends, then repeats
the second (the lobby music does the same: ArtSource/Lobby/Music/bake.py, whose method this is).

The take keeps one tempo (150.000 BPM, a bar is 1.6 s) and its sections come back. The join was picked from every pair
of bar lines by how alike the two bars before and after are (numpy-only analysis in the 2026-11-14 session):
START/END below. The end is put on the start to the sample by lining up the drum hits, the cut is made 3 ms ahead of
the downbeat's hit, and the last 15 ms of the loop fade into the 15 ms before its start, so the wrap is continuous.
Both clips share one peak normalisation to -1 dBFS; RACE_GAIN (printed) is the code gain that plays them at an RMS of
-28 dBFS at the default music volume (0.7): under the course's effects, 2 dB below the lobby music.

  bake.py [preview.wav]          bake (and, given a path, write the loop's last 8 s and first 8 s back to back there)
  bake.py --try START END ...    only measure joins (pairs of seconds), nothing written

Needs only numpy: run with Blender's Python from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.1/5.1/python/bin/python.exe" -I ArtSource/PawnRush/Music/bake.py
"""
import json, os, sys, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw', 'PawnRushRace_2026-11-14.wav')
DEST = os.path.join(HERE, '../../../Assets/Resources/PawnRushMusic')
# Bar lines on the take's grid (150 BPM, bars from 1.183 s): 56 bars, 89.6 s, the longest of the measured joins
# (--try: drum hits match 0.90 on the sample, waveform likeness 0.88 before the join and 0.91 after; the 51 to 77 s
# loops measured alike, the lobby's join 0.78 / 0.87).
START, END = 28.383, 117.983
LEAD, FADE, IN_FADE = .003, .015, .002
TARGET_RMS, DEFAULT_MUSIC = -28.0, .7

w = wave.open(RAW)
SR, CH = w.getframerate(), w.getnchannels()
x = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float64).reshape(-1, CH) / 32768
mono = x.mean(1)
hits = np.abs(np.concatenate([[0], np.diff(mono)]))      # the slope: drum hits stand out


def envelope(t0, t1):
    k = int(.002 * SR)
    return np.convolve(hits[int(round(t0 * SR)):int(round(t1 * SR))], np.ones(k) / k, 'same')


def line_up(s, e, search=.03):
    """Samples to move `e` by so the hits around it fall where they do around `s`."""
    a = envelope(s - .25, s + .5)
    best = (-2.0, 0)
    for d in range(-int(search * SR), int(search * SR) + 1, 2):
        b = envelope(e - .25 + d / SR, e + .5 + d / SR)
        m = min(len(a), len(b))
        c = np.corrcoef(a[:m], b[:m])[0, 1]
        if c > best[0]: best = (c, d)
    c0, d0 = best
    for d in (d0 - 1, d0 + 1):
        b = envelope(e - .25 + d / SR, e + .5 + d / SR)
        m = min(len(a), len(b))
        c = np.corrcoef(a[:m], b[:m])[0, 1]
        if c > best[0]: best = (c, d)
    return best


def first_hit(t, around=.03):
    e = envelope(t - around, t + around)
    return int(round((t - around) * SR)) + int(np.argmax(np.diff(e))) + 1


def join(start, end):
    match, shift = line_up(start, end)
    s = first_hit(start) - int(round(LEAD * SR))
    e = s + int(round((end - start) * SR)) + shift
    before = np.corrcoef(x[e - SR // 2:e].ravel(), x[s - SR // 2:s].ravel())[0, 1]
    after = np.corrcoef(x[e:e + SR // 2].ravel(), x[s:s + SR // 2].ravel())[0, 1]
    return s, e, match, shift, before, after


if len(sys.argv) > 1 and sys.argv[1] == '--try':
    v = list(map(float, sys.argv[2:]))
    for a, b in zip(v[::2], v[1::2]):
        s, e, match, shift, before, after = join(a, b)
        print(f'{a:8.3f} -> {b:8.3f} ({(e - s) / SR:6.2f}s)  hit match {match:.3f} at {shift / SR * 1000:+.2f} ms  '
              f'waveform likeness before {before:.3f} after {after:.3f}')
    sys.exit(0)

s, e, match, shift, before, after = join(START, END)
intro = x[:s].copy()
loop = x[s:e].copy()
f = int(round(FADE * SR))
r = (np.sin(np.linspace(0, np.pi / 2, f)) ** 2)[:, None]
loop[-f:] = loop[-f:] * (1 - r) + x[s - f:s] * r      # the end fades into what plays just before the start
k = int(round(IN_FADE * SR))
intro[:k] *= np.linspace(0, 1, k)[:, None]

peak = max(np.abs(intro).max(), np.abs(loop).max())
scale = 10 ** (-1 / 20) / peak
intro *= scale
loop *= scale
rms = 20 * np.log10(np.sqrt((loop ** 2).mean()))
gain = 10 ** ((TARGET_RMS - rms) / 20) / DEFAULT_MUSIC
step = np.abs(np.diff(loop, axis=0)).mean(1)
wrap = np.abs(loop[0] - loop[-1]).mean()


def write(path, a):
    with wave.open(path, 'wb') as out:
        out.setnchannels(CH); out.setsampwidth(2); out.setframerate(SR)
        out.writeframes(np.rint(np.clip(a, -1, 1) * 32767).astype('<i2').tobytes())


os.makedirs(DEST, exist_ok=True)
write(os.path.join(DEST, 'RaceMusic.wav'), intro)
write(os.path.join(DEST, 'RaceMusicLoop.wav'), loop)
if len(sys.argv) > 1:
    write(sys.argv[1], np.concatenate([loop[-8 * SR:], loop[:8 * SR]]))
print(json.dumps({
    'sample_rate': SR,
    'intro_s': round(len(intro) / SR, 5), 'intro_samples': len(intro),
    'loop_s': round(len(loop) / SR, 5), 'loop_samples': len(loop),
    'end_shift_ms': round(shift / SR * 1000, 2), 'hit_match': round(match, 3),
    'waveform_likeness_before_join': round(before, 3), 'after_join': round(after, 3),
    'wrap_step_vs_median_step': round(wrap / np.median(step), 2), 'wrap_step_percentile': round(float((step < wrap).mean() * 100), 1),
    'loop_rms_dbfs': round(rms, 2), 'RACE_GAIN': round(gain, 4)}, indent=1))

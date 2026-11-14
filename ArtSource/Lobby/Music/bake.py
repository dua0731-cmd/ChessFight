"""Bakes the lobby music: the ElevenLabs Music v2.5 take in raw/ (120 BPM, G minor, 164 s, made as a Pawn Rush race
track; the user picked it for the lobby, 2026-11-14; R652 swapped in the user's export of it, "로비BGM.wav", the same
music about 1 ms earlier, so the same loop points) into

  Assets/Resources/Lobby/Music/LobbyMusic.wav      the take from its start up to the loop point (once)
  Assets/Resources/Lobby/Music/LobbyMusicLoop.wav  the 96 s (48 bars) after it, whose end runs into its own start

for Game/LobbyMusic.cs, which plays the first and schedules the second right where it ends, then repeats the second.

The take keeps one tempo and its sections come back: bars 58 to 62 (116 to 124 s) are the same music as bars 10 to 14
(20 to 28 s), so the loop goes from the downbeat at 28 s to the one at 124 s and the end leads into the start as it
does in the take. The join was picked from every pair of downbeats by how alike the two bars before and after are
(numpy-only analysis in the 2026-11-14 session); here the end is put on the start to the sample by lining up the
drum hits, the cut is made 3 ms ahead of the downbeat's hit, and the last 15 ms of the loop fade into the 15 ms that
come before its start, so the wrap is continuous. Both clips share one peak normalisation to -1 dBFS; LOBBY_GAIN
(printed) is the code gain that plays them at an RMS of -26 dBFS at the default music volume (0.7), about where the
Sword Fight entrance music sits.

With a path as its argument it also writes there the loop's last 8 s and first 8 s back to back, for listening to
the join (kept out of the repository). Needs only numpy: run with Blender's Python, from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.1/5.1/python/bin/python.exe" -I ArtSource/Lobby/Music/bake.py [preview.wav]
"""
import json, os, sys, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw', 'LobbyBGM_2026-11-14.wav')
DEST = os.path.join(HERE, '../../../Assets/Resources/Lobby/Music')
START, END = 27.995, 124.001        # the two downbeats on the take's beat grid (119.992 BPM, a downbeat at 0 s)
LEAD, FADE, IN_FADE = .003, .015, .002
TARGET_RMS, DEFAULT_MUSIC = -26.0, .7

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
    for d in range(-int(search * SR), int(search * SR) + 1):
        b = envelope(e - .25 + d / SR, e + .5 + d / SR)
        m = min(len(a), len(b))
        c = np.corrcoef(a[:m], b[:m])[0, 1]
        if c > best[0]: best = (c, d)
    return best


def first_hit(t, around=.03):
    """The sample where the downbeat's hit near `t` rises."""
    e = envelope(t - around, t + around)
    i = int(np.argmax(np.diff(e))) + 1
    return int(round((t - around) * SR)) + i


match, shift = line_up(START, END)
s = first_hit(START) - int(round(LEAD * SR))
length = int(round((END - START) * SR)) + shift
e = s + length

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

# how big the jump at the wrap is next to an ordinary step between samples
step = np.abs(np.diff(loop, axis=0)).mean(1)
wrap = np.abs(loop[0] - loop[-1]).mean()
before = np.corrcoef(x[e - SR // 2:e].ravel(), x[s - SR // 2:s].ravel())[0, 1]
after = np.corrcoef(x[e:e + SR // 2].ravel(), x[s:s + SR // 2].ravel())[0, 1]


def write(path, a):
    with wave.open(path, 'wb') as out:
        out.setnchannels(CH); out.setsampwidth(2); out.setframerate(SR)
        out.writeframes(np.rint(np.clip(a, -1, 1) * 32767).astype('<i2').tobytes())


os.makedirs(DEST, exist_ok=True)
write(os.path.join(DEST, 'LobbyMusic.wav'), intro)
write(os.path.join(DEST, 'LobbyMusicLoop.wav'), loop)
if len(sys.argv) > 1:
    write(sys.argv[1], np.concatenate([loop[-8 * SR:], loop[:8 * SR]]))
print(json.dumps({
    'sample_rate': SR,
    'intro_s': round(len(intro) / SR, 5), 'intro_samples': len(intro),
    'loop_s': round(len(loop) / SR, 5), 'loop_samples': len(loop),
    'end_shift_ms': round(shift / SR * 1000, 2), 'hit_match': round(match, 3),
    'waveform_likeness_before_join': round(before, 3), 'after_join': round(after, 3),
    'wrap_step_vs_median_step': round(wrap / np.median(step), 2), 'wrap_step_percentile': round(float((step < wrap).mean() * 100), 1),
    'loop_rms_dbfs': round(rms, 2), 'LOBBY_GAIN': round(gain, 4)}, indent=1))

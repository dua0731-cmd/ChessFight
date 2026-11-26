"""R854: bakes the tutorial's music from the user's two ElevenLabs Music takes in raw/ (the "storybook woodwinds" plan):

  TutorialFlight   the opening: the eagle's lap and the stop at the crash (10 s), in F
  TutorialLessons  the lessons' theme, in G and much darker than the flight

into Assets/Resources/TutorialMusic/ (played by RagdollLab/Tutorial/TutorialMusic.cs):

  TutorialMusic.wav      the flight, the theme fading in under its tail, up to the theme's loop end (once)
  TutorialMusicLoop.wav  the theme's 14.4 s (of the take) that repeat while the lessons go on

So that the two sit together (user 11-26 "비행곡과 조작곡 사이에 뭔가 이질감이 느껴져"): the theme is brought down two
semitones into F (resampled, so it is also 11% slower), brightened above 500 Hz, and faded in from THEME_IN; the flight
is cut in its silence, before the next phrase's pickup (it was heard as a stray clipped note).

  bake.py [folder]   bake; given a folder, also write listening previews there
Needs only numpy, run from the repository root with Blender's Python:
  "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I ArtSource/Tutorial/Music/bake.py
"""
import json, os, sys, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(HERE, '../../../Assets/Resources/TutorialMusic')
FLIGHT_END, THEME_IN, THEME_FADE = 11.6, 11.2, 1.8      # seconds of the opening
START, END = 1.25, 15.65                                # the loop, in the take's own seconds (the best-matching pair)
SEMITONES = -2
SHELF_DB, SHELF_HZ = 5.0, 600.0
FLIGHT_OUT, RIDE_IN, JOIN = .1, .005, .12
TARGET_RMS, DEFAULT_MUSIC = -28.0, .7


def load(name):
    w = wave.open(os.path.join(HERE, 'raw', name))
    a = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float64).reshape(-1, w.getnchannels()) / 32768
    return a, w.getframerate()


def rms(a):
    return np.sqrt((a ** 2).mean())


flight, SR = load('TutorialFlight_2026-11-26.wav')
take, sr2 = load('TutorialLessons_2026-11-26.wav')
assert SR == sr2 and flight.shape[1] == take.shape[1]


def at(t):
    return int(round(t * SR))


# Down into F (a longer signal, played at the same rate) and a gentle high shelf, both in the spectrum.
ratio = 2 ** (SEMITONES / 12)
pad = at(.5)
x = np.concatenate([take, np.zeros((pad, take.shape[1]))])
n = len(x)
m = int(round(n / ratio))
X = np.fft.rfft(x, axis=0)
Y = np.zeros((m // 2 + 1, x.shape[1]), complex)
k = min(len(X), len(Y))
Y[:k] = X[:k]
f = np.fft.rfftfreq(m, 1 / SR)
Y *= (10 ** (SHELF_DB / 20 / (1 + (SHELF_HZ / np.maximum(f, 1)) ** 4)))[:, None]
ride = np.fft.irfft(Y, m, axis=0) * (m / n)

# The loop's end put on its start by the note onsets (within 30 ms), so the beat does not stumble at the wrap.
s, e = at(START / ratio), at(END / ratio)
W = at(.25)
onsets = np.convolve(np.abs(np.concatenate([[0], np.diff(ride.mean(1))])), np.ones(at(.005)) / at(.005), 'same')
ref = onsets[s - W:s + 2 * W]
shift = max(range(-at(.03), at(.03), 4), key=lambda d: np.corrcoef(ref, onsets[e + d - W:e + d + 2 * W])[0, 1])
e += shift
f_ = at(JOIN)
r = np.linspace(0, 1, f_)[:, None]
r = np.sin(r * np.pi / 2) ** 2
loop = ride[s:e].copy()
loop[-f_:] = loop[-f_:] * (1 - r) + ride[s - f_:s] * r

theme = ride[:e].copy()
theme[-f_:] = loop[-f_:]                                 # the same end as the loop's, so it runs into START
theme[:at(THEME_FADE)] *= (np.sin(np.linspace(0, np.pi / 2, at(THEME_FADE))) ** 2)[:, None]

head = flight[:at(FLIGHT_END)].copy()
head[-at(FLIGHT_OUT):] *= np.linspace(1, 0, at(FLIGHT_OUT))[:, None]
head *= rms(loop) / rms(flight[:at(10.0)])               # the flight as loud as the theme

opening = np.zeros((at(THEME_IN) + len(theme), take.shape[1]))
opening[:len(head)] += head
opening[at(THEME_IN):] += theme

scale = 10 ** (-1 / 20) / max(np.abs(opening).max(), np.abs(loop).max())
opening *= scale
loop *= scale
gain = 10 ** (TARGET_RMS / 20) / rms(loop) / DEFAULT_MUSIC


def write(path, a):
    with wave.open(path, 'wb') as out:
        out.setnchannels(a.shape[1]); out.setsampwidth(2); out.setframerate(SR)
        out.writeframes(np.rint(np.clip(a, -1, 1) * 32767).astype('<i2').tobytes())


os.makedirs(DEST, exist_ok=True)
write(os.path.join(DEST, 'TutorialMusic.wav'), opening)
write(os.path.join(DEST, 'TutorialMusicLoop.wav'), loop)
if len(sys.argv) > 1:
    write(os.path.join(sys.argv[1], 'preview_transition.wav'), opening[at(4):at(24)])
    write(os.path.join(sys.argv[1], 'preview_loop_join.wav'), np.concatenate([loop[-at(6):], loop[:at(6)]]))
step = np.abs(np.diff(loop, axis=0)).mean(1)
print(json.dumps({'opening_s': round(len(opening) / SR, 3), 'loop_s': round(len(loop) / SR, 3),
                  'loop_shift_ms': round(shift / SR * 1000, 2),
                  'flight_rms_db': round(20 * np.log10(rms(opening[:at(10.0)])), 2), 'loop_rms_db': round(20 * np.log10(rms(loop)), 2),
                  'wrap_step_vs_median': round(float(np.abs(loop[0] - loop[-1]).mean() / np.median(step)), 2),
                  'GAIN': round(float(gain), 4)}, indent=1))

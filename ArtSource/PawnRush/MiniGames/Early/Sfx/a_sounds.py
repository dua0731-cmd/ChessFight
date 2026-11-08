# A ceremonial court sounds (R506, user 11-07: "sfx랑 vfx도 해줘"): picks one of the two ElevenLabs takes of each sound
# (Sfx/Source/*_take1/2.mp3, flow "KII97KGTTXxncvpKyjzY", model eleven_text_to_sound_v2), trims and shapes it for its
# moment in PaintBoard, and writes a mono 16-bit WAV with its .meta into Assets/Resources/PawnRushMiniGameSfx (ArenaFx
# loads sounds there by name). Takes are picked by shape, not by ear (as Library/rook_sounds.py did):
#   paint_charge   held while a pawn paints a square (1.5 s, cut when it stops). History (user, 11-07): a rising
#                  shimmer was too sharp ("채워지는 소리가 날카로워"), then a low hum and a filling bottle were not liked
#                  ("다른 걸로 다 바꿔", "다 마음에 안들어"); the user chose a brush stroke ("붓질 스윽"). Takes
#                  paint_charge_brush_take1..4: the one sounding longest (30 dB under its peak) up to 1.5 s, with the
#                  least energy over 4 kHz; its first 1.6 s, softened over 6 kHz (a brush is a hiss), faded in
#   paint_done     a square painted, the user's choice: a stamp ("도장 쾅", paint_done_stamp_take1..3): the strongest
#                  first 60 ms against the whole; trimmed to its ring-out
#   paint_blocked  a rival stands on the square: as paint_done
#   rank_complete  a full rank: the take with the most note onsets (an ascending run)
#   team_complete  a team's 32 squares (new in R506): the louder take
# Every sound: leading silence cut (40 dB under its peak), tail cut where it stays 40 dB under, 15 ms fade-in after
# the cut, 40 ms fade-out, peak at -1 dBFS.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python a_sounds.py
import aud, glob, hashlib, os, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", ".."))
SRC = os.path.join(HERE, "Source")
OUT_REL = "Assets/Resources/PawnRushMiniGameSfx"
RATE = 44100
META = """fileFormatVersion: 2
guid: %s
AudioImporter:
  externalObjects: {}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def load(name):
    d = aud.Sound(os.path.join(SRC, name)).resample(RATE, False).data()
    return d.mean(1).astype(np.float64)


def env(x, win=.01):
    n = max(1, int(win * RATE))
    k = len(x) // n
    return np.sqrt((x[:k * n].reshape(k, n) ** 2).mean(1) + 1e-12), n


def db(a):
    return 20 * np.log10(np.maximum(a, 1e-9))


def trim(x, floor=-40.):
    e, n = env(x)
    rel = db(e) - db(e.max())
    on = np.nonzero(rel > floor)[0]
    if not len(on):
        return x
    a, b = on[0] * n, min(len(x), (on[-1] + 1) * n)
    return x[a:b]


def fades(x, fin=.015, fout=.04):
    x = x.copy()
    a, b = int(fin * RATE), int(fout * RATE)
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)
    return x


def onsets(x):
    e, _ = env(x, .02)
    d = db(e)
    rise = np.diff(d)
    return int(((rise > 4.) & (d[1:] > d.max() - 24.)).sum())


def score(kind, x):
    e, n = env(x)
    q = max(1, len(e) // 4)
    if kind == "charge":
        active = (db(e) > db(e.max()) - 30.).sum() * n / RATE
        return min(1., active / 1.5) * (1. - high_share(x))
    if kind in ("done", "blocked"):
        t = trim(x)
        head = t[:int(.06 * RATE)]
        return np.sqrt((head ** 2).mean()) / (np.sqrt((t ** 2).mean()) + 1e-9) / max(.3, len(t) / RATE)
    if kind == "rank":
        return onsets(x)
    return np.sqrt((x ** 2).mean())


def high_share(x, cut=4000.):
    X = np.abs(np.fft.rfft(x)) ** 2
    f = np.fft.rfftfreq(len(x), 1. / RATE)
    return X[f > cut].sum() / (X.sum() + 1e-12)


def soften(x, corner=6000.):
    """Gentle top roll-off (first-order: -3 dB at `corner`, -6 dB an octave up)."""
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1. / RATE)
    return np.fft.irfft(X / np.sqrt(1. + (f / corner) ** 2), len(x))


def shape(kind, x):
    x = trim(x)
    if kind == "charge":
        e, n = env(x, .02)
        peak = (int(np.argmax(e)) + 1) * n
        want = int(1.6 * RATE)
        a = max(0, peak - want)
        x = x[a:a + want] if peak >= want else x[:want]
        return fades(soften(x), .06, .05)
    return fades(x)


def write(rel, x):
    x = x / (np.abs(x).max() + 1e-9) * 10 ** (-1 / 20)
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    if not os.path.exists(path + ".meta"):
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(META % hashlib.md5(("chessfight:" + rel).encode()).hexdigest())


SOUNDS = [("done", "paint_done_stamp", "sfx_paint_done"),
          ("blocked", "paint_blocked", "sfx_paint_blocked"), ("rank", "rank_complete", "sfx_rank_complete"),
          ("team", "team_complete", "sfx_paint_team_complete")]

for kind, stem, out in SOUNDS:
    n = len(glob.glob(os.path.join(SRC, stem + "_take*.mp3")))
    takes = [(t, load("%s_take%d.mp3" % (stem, t))) for t in range(1, n + 1)]
    scores = [(score(kind, x), t, x) for t, x in takes]
    best = max(scores, key=lambda s: s[0])
    y = shape(kind, best[2])
    write(OUT_REL + "/" + out + ".wav", y)
    print("SOUND %-24s take %d (scores %s) %.2f s -> %.2f s" % (out, best[1], " / ".join("%.2f" % s[0] for s in scores),
                                                            len(best[2]) / RATE, len(y) / RATE))

# The painting sound is single brush strokes (user, 11-07: "샤갈샤갈샤갈 하는 소리 빼고 붓질 소리 8개 놔 무작위로 순서도
# 무작위"): the continuous scrubbing takes and their sfx_paint_charge*.wav are gone. 8 takes of one stroke each
# (Source/paint_stroke_take1..8), each cut to its first stroke: from the onset to where it stays 30 dB under its peak
# for 80 ms after that peak (at most 0.9 s) and before any second stroke, softened over 6 kHz, faded -> sfx_paint_stroke_1..8. PaintBoard plays
# them one after another while a square charges, in a shuffled order.
def first_stroke(x):
    x = trim(x)
    e, n = env(x)
    d = db(e) - db(e.max())
    peak = int(np.argmax(e))
    quiet, end = 0, len(e)
    for i in range(peak, len(e)):
        quiet = quiet + 1 if d[i] < -30. else 0
        if quiet * n >= .08 * RATE:
            end = i - quiet + 1
            break
    end = min(end * n, int(.9 * RATE))
    # a take that strokes more than once (a fresh 4 dB rise in 20 ms frames, after 0.2 s) ends before its second stroke
    e2, n2 = env(x, .02)
    d2 = db(e2)
    for i in range(1, len(e2)):
        if i * n2 > .2 * RATE and d2[i] - d2[i - 1] > 4. and d2[i] > d2.max() - 24.:
            end = min(end, i * n2 - int(.01 * RATE))
            break
    return fades(soften(x[:max(end, int(.15 * RATE))]), .01, .08)


for stale in glob.glob(os.path.join(ROOT, OUT_REL, "sfx_paint_charge*.wav")):
    os.remove(stale)
    if os.path.exists(stale + ".meta"): os.remove(stale + ".meta")
for i in range(1, 9):
    y = first_stroke(load("paint_stroke_take%d.mp3" % i))
    write(OUT_REL + "/sfx_paint_stroke_%d.wav" % i, y)
    print("SOUND sfx_paint_stroke_%d  %.2f s, %d onsets, >4k %.0f %%" % (i, len(y) / RATE, onsets(y), high_share(y) * 100))

# The team door opening (user, 11-08: "문이 열리는 vfx랑 사운드 넣어줘"; PaintGateFx.Opened): 4 takes of an oak door
# lifting into a stone arch (Source/paint_door_open_take1..4, same flow and model). The pick: the take sounding
# longest (30 dB under its peak, wanted ~1.3 s, the door's lift time) with the least energy over 4 kHz (a heavy
# wooden groan, not a hiss); trimmed, softened over 6 kHz, faded -> sfx_paint_door_open.
door = []
for t in range(1, 5):
    x = load("paint_door_open_take%d.mp3" % t)
    e, n = env(x)
    active = (db(e) > db(e.max()) - 30.).sum() * n / RATE
    door.append((min(1., active / 1.3) * (1. - high_share(x)), t, x))
best = max(door, key=lambda d: d[0])
y = fades(soften(trim(best[2])), .02, .12)
write(OUT_REL + "/sfx_paint_door_open.wav", y)
print("SOUND sfx_paint_door_open  take %d (scores %s) %.2f s" % (best[1], " / ".join("%.2f" % d[0] for d in door), len(y) / RATE))

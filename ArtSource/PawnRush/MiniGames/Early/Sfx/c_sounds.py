# C 해자 도개교 sounds (R502, user 11-07: "이제 사운드 입혀줘"). The sounds MgDrawbridge already plays had no files, so
# nothing was heard. ElevenLabs eleven_text_to_sound_v2, flow "JgQIeR98Y8z1yg82kbYy" (two takes each, Sfx/Source/C):
#   winch_take*    (3 s, loop) a big wooden winch drum turning, wood groan, no metal  -> sfx_winch       (held while pulled)
#   creak_take*    (3 s, loop) a thick rope running taut over pulleys                  -> sfx_rope_creak  (held while pulled)
#   strain_take*   (1 s) a rope snapping taut - replaced: the user didn't like its hiss (11-08, "이런 소리 말고"); of four
#                  sketches (strainalt_1 a low tug thump, _2 a short wooden creak of the knot and pulley, _3 rope through
#                  gloved hands, _4 a cartoon "hup") the user picked strainalt_2 -> sfx_strain (a gripped knot, 2 s apart)
#   ratchet_take*  (0.8 s) one wooden pawl clack on a wooden gear, no metal ring      -> sfx_ratchet_click (every metre)
#   slam_take*     (2.5 s) the wooden deck slamming down, a short chain rattle, a splash -> sfx_bridge_slam (deck lands)
# No 25 / 50 / 75 % sound (user 11-07). Takes are picked by shape, not by ear: the loops by the least energy over 4 kHz
# (no metal) and kept whole (they loop as made, MgDrawbridge restarts them); strain by the least over 4 kHz; the click by
# the sharpest attack (first 60 ms against the whole); the slam by the louder. One-shots: leading silence cut (40 dB
# under the peak), tail cut where it stays under, 15 ms fade-in, 40 ms fade-out. All mono 16-bit WAV, peak -1 dBFS, into
# Assets/Resources/PawnRushMiniGameSfx (ArenaFx loads sounds there by name).
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_sounds.py
import aud, glob, hashlib, os, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", ".."))
SRC = os.path.join(HERE, "Source", "C")
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


def load(path):
    d = aud.Sound(path).resample(RATE, False).data()
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
    return x[on[0] * n:min(len(x), (on[-1] + 1) * n)]


def fades(x, fin=.015, fout=.04):
    x = x.copy()
    a, b = int(fin * RATE), int(fout * RATE)
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)
    return x


def high_share(x, cut=4000.):
    X = np.abs(np.fft.rfft(x)) ** 2
    f = np.fft.rfftfreq(len(x), 1. / RATE)
    return X[f > cut].sum() / (X.sum() + 1e-12)


def attack(x):
    t = trim(x)
    head = t[:int(.06 * RATE)]
    return np.sqrt((head ** 2).mean()) / (np.sqrt((t ** 2).mean()) + 1e-9)


def write(rel, x):
    x = x / (np.abs(x).max() + 1e-9) * 10 ** (-1 / 20)
    path = os.path.join(ROOT, rel)
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    if not os.path.exists(path + ".meta"):
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(META % hashlib.md5(("chessfight:" + rel).encode()).hexdigest())


# (stem, out, score (higher wins), loop: kept whole)
SOUNDS = [("winch", "sfx_winch", lambda x: -high_share(x), True),
          ("creak", "sfx_rope_creak", lambda x: -high_share(x), True),
          ("strainalt_2", "sfx_strain", lambda x: 0., False),   # the user's pick (11-08, "이걸로"): the wooden creak
          ("ratchet", "sfx_ratchet_click", attack, False),
          ("slam", "sfx_bridge_slam", lambda x: np.sqrt((x ** 2).mean()), False)]

for stem, out, score, loop in SOUNDS:
    takes = [(int(p[-5]), load(p)) for p in sorted(glob.glob(os.path.join(SRC, stem + ("_take*.mp3" if "alt" not in stem else ".mp3"))))]
    scored = [(score(x), t, x) for t, x in takes]
    best = max(scored, key=lambda s: s[0])
    y = best[2] if loop else fades(trim(best[2]))
    write(OUT_REL + "/" + out + ".wav", y)
    print("SOUND %-18s take %d (scores %s) %.2f s -> %.2f s, >4k %.0f %%" % (
        out, best[1], " / ".join("%.3f" % s[0] for s in scored), len(best[2]) / RATE, len(y) / RATE, high_share(y) * 100))

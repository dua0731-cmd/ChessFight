"""The promotion pad's sounds, synthesised (no takes), one mallet voice for all three:
  sfx_promo_charge  the 2 s on the pad (below);
  sfx_promo_ready   the charge resolving as the choice cards open (PromotionChooser, 2D): a soft mallet chord rolled
                    up from the run's top, in place of the coin-like sfx_mg_quarter at 1.5x;
  sfx_promo_change  the pawn becoming its piece (the pick, PromotionChooser, 2D): a quick pentatonic sweep up
                    landing on a warm C major chord over a soft low bloom, in place of sfx_mg_complete there
                    (user 11-22 "기물 바뀌는 소리도 지금 차오르는 소리랑 맞춰 동전 소리 말고").

11-22 (user "승격 발판 차징 사운드 넣어", then of the first take "이 소리가 아니야 청량하게 우리 게임 컨셉에 맞는걸로
해야 해"): the promotion pad's charge, synthesised (no take): what a pawn hears while it stands its 2 s on the pad
(StationPromotion, ArenaFx.Hold at the pad, cut when it steps off), ending where the choice cards open with
sfx_mg_quarter. v3 (v2 was a glockenspiel run, C5 to E7; user "저 소리보다는 좀 더 청량하지 않게"): the same run
an octave lower and warmer, a soft music box or mallet rather than glass (v1, a droning rising chord, was dropped):

  - a run up the major pentatonic, C4 to E6, one note per two of the frame's 24 lamps (every 1/6 s), each a soft
    mallet tone (a gentle FM bell, little metal in it, a round octave), growing a little as it climbs;
  - a few faint glints near the top only;
  - no breath of noise; a small warm room (a few short, dark echoes);
  - the whole through a gentle low-pass (about 4.5 kHz) so nothing sparkles.

Level by the R654 yardstick (Docs/PawnRush/PAWN_RUSH_AUDIO.md section 0): heard -23 dB close by with the call's
volume 0.7 at default effects 0.8, the loudest 400 ms measured, peaks under -1 dBFS.

Writes Assets/Resources/PawnRushMiniGameSfx/<name>.wav and each .meta the first time (deterministic GUID,
mono, PCM, preloaded, no normalise). Needs only numpy: run with Blender's Python from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I ArtSource/PawnRush/Sfx/bake_promotion.py
"""
import hashlib, os, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '../../..'))
OUT_REL = 'Assets/Resources/PawnRushMiniGameSfx'
NAME = 'sfx_promo_charge'
SR = 48000
SECONDS = 2.0          # PawnRushPromotion.HoldSeconds
LAMPS = 24             # the frame's studs (StationPromotion, PR_PromoPad_Lamp_00~23)
HEARD, VOL, EFFECTS = -23., .7, .8

N = int(SECONDS * SR)
t = np.arange(N) / SR
rng = np.random.default_rng(722)
y = np.zeros(N)


def bell(f, length, bright):
    """A struck bar: FM bell (modulator 3.5x, its depth dying fast) plus a pure octave, quick to start, ringing out."""
    n = int(length * SR)
    s = np.arange(n) / SR
    index = .9 * bright * np.exp(-s / .035)
    tone = np.sin(2 * np.pi * f * s + index * np.sin(2 * np.pi * 3.5 * f * s))
    tone += .25 * np.sin(2 * np.pi * 2 * f * s) * np.exp(-s / .09)
    tone += .04 * np.sin(2 * np.pi * 4.07 * f * s) * np.exp(-s / .02)
    env = np.exp(-s / .32) * (1 - np.exp(-s / .004))
    return tone * env


# the run: major pentatonic from C5, one note per two lamps
steps = [0, 2, 4, 7, 9]
notes = [261.63 * 2 ** ((12 * (k // 5) + steps[k % 5]) / 12) for k in range(LAMPS // 2)]
for k, f in enumerate(notes):
    at = int(k * SECONDS / len(notes) * SR + .004 * SR)
    b = bell(f, .7, .6 + .2 * k / len(notes)) * (.6 + .4 * k / len(notes))
    end = min(N, at + len(b))
    y[at:end] += b[:end - at]

# sparkles: tiny high glints, more towards the top
for k in range(8):
    u = rng.uniform(.65, 1) 
    at = int(u * (N - .05 * SR))
    f = rng.uniform(2200, 3200)
    n = int(.05 * SR)
    s = np.arange(n) / SR
    y[at:at + n] += .025 * np.sin(2 * np.pi * f * s) * np.exp(-s / .012)

# an airy breath, high-passed so it stays light
noise = rng.standard_normal(N)
air = noise - np.convolve(noise, np.ones(12) / 12, 'same')
air = np.convolve(air, np.ones(3) / 3, 'same')
y += 0 * air   # v3: no breath



def warm(y):
    """The small room (a few short, dark echoes) and the gentle 4.5 kHz low-pass every promotion sound goes through."""
    wet = np.zeros(len(y))
    for delay, gain in ((.029, .24), (.043, .18), (.067, .13), (.101, .08), (.149, .05)):
        d = int(delay * SR)
        wet[d:] += gain * y[:-d]
    wet = np.convolve(wet, np.ones(14) / 14, 'same')
    y = y + wet
    alpha = 1 - np.exp(-2 * np.pi * 4500 / SR)
    out = np.zeros(len(y))
    acc = 0.
    for i in range(len(y)):
        acc += alpha * (y[i] - acc)
        out[i] = acc
    return out


def fades(y, attack=.004, release=.03):
    a, r = int(attack * SR), int(release * SR)
    y[:a] *= np.linspace(0, 1, a)
    y[-r:] *= np.cos(np.linspace(0, np.pi / 2, r)) ** 2
    return y


def loudness(v):
    k = int(.4 * SR)
    if len(v) <= k: return 10 * np.log10(np.mean(v ** 2) + 1e-12)
    return 10 * np.log10(np.convolve(v ** 2, np.ones(k) / k, 'valid').max() + 1e-12)


META = """fileFormatVersion: 2
guid: {g}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 48000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def save(name, y, heard, vol):
    want = heard - 20 * np.log10(vol * EFFECTS)
    y = y * 10 ** ((want - loudness(y)) / 20)
    peak = np.abs(y).max()
    if peak > 10 ** (-1 / 20):
        y *= 10 ** (-1 / 20) / peak
    with wave.open(os.path.join(ROOT, OUT_REL, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(np.rint(np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())
    rel = f'{OUT_REL}/{name}.wav'
    meta = os.path.join(ROOT, rel) + '.meta'
    state = 'kept'
    if not os.path.exists(meta):
        with open(meta, 'w', newline=chr(10)) as f:
            f.write(META.format(g=hashlib.md5(('chessfight:' + rel).encode()).hexdigest()))
        state = 'new'
    to_heard = 20 * np.log10(vol * EFFECTS)
    print(f'{name}: {len(y) / SR:.2f} s, loudest 400 ms {loudness(y):.1f} dBFS (heard {loudness(y) + to_heard:.1f} dB), '
          f'peak {20 * np.log10(np.abs(y).max()):.1f} dBFS, meta {state}')


save(NAME, fades(warm(y)), HEARD, VOL)

# ---- sfx_promo_ready: the run's top resolving - a soft chord rolled up (C5 E5 G5 C6), 25 ms apart
ready = np.zeros(int(1.0 * SR))
for k, f in enumerate((523.25, 659.25, 783.99, 1046.5)):
    at = int((.004 + .025 * k) * SR)
    b = bell(f, .9, .55) * (1 - .12 * k)
    ready[at:at + len(b)] += b[:len(ready) - at]
save('sfx_promo_ready', fades(warm(ready), release=.12), -24., 1.)

# ---- sfx_promo_change: the change - a quick sweep up the pentatonic (8 notes in 0.24 s) landing on a warm
# C major chord (C4 E4 G4 C5) over a soft low bloom
change = np.zeros(int(1.3 * SR))
sweep = [261.63 * 2 ** ((12 * (k // 5) + steps[k % 5]) / 12) for k in range(3, 11)]
for k, f in enumerate(sweep):
    at = int((.004 + .03 * k) * SR)
    b = bell(f, .35, .5) * (.35 + .35 * k / len(sweep))
    change[at:at + len(b)] += b[:len(change) - at]
land = int(.27 * SR)
for k, f in enumerate((261.63, 329.63, 392.0, 523.25)):
    b = bell(f, 1.0, .5) * (.9 - .1 * k)
    at = land + int(.012 * k * SR)
    change[at:at + len(b)] += b[:len(change) - at]
s_ = np.arange(len(change) - land) / SR
change[land:] += .45 * np.sin(2 * np.pi * 130.81 * s_) * np.exp(-s_ / .22) * (1 - np.exp(-s_ / .01))
save('sfx_promo_change', fades(warm(change), release=.15), -21., 1.)

"""Pawn Rush library (작전 회의실): the goal rooks' attack sounds - charge, shot, burst - near and far layers (R463).

    blender --background --factory-startup --python rook_sounds.py -- <folder of takes>              (writes the wavs)
    blender --background --factory-startup --python rook_sounds.py -- <folder> --out D:/check          (a copy elsewhere)
    blender --background --factory-startup --python rook_sounds.py -- <folder> --report                (measures only)

승규 님 (11-05): "이제 기물 공격 sfx 다 찾아서 넣어". The takes were made in the ElevenLabs flow "PawnRush Library Conveyor
SFX R289" (SsseNEjgrzO8OYi98PaU, where the war room's other sounds come from), six sfx nodes x 2 takes
(eleven_text_to_sound_v2, prompt influence 0.5), named <Kind>_<take>.mp3 in the folder:
  ChargeSide   0.7 s  "Short rising magical energy charge-up: a quick bright whine climbing fast in pitch with a crackle
                       of tiny sparks and a faint stone hum, ending abruptly at its peak ..."
  ChargeHeavy  3.2 s  "Long ominous magical energy charge-up building for three seconds: a deep stone resonance and low
                       hum swelling, a whine rising steadily in pitch, crackling sparks ... cutting off sharply at the peak"
  FireSide     0.8 s  "A quick sharp magical energy bolt fired: a punchy bright zap with a fast whoosh ..."
  FireHeavy    1.6 s  "A huge magical energy cannon blast fired: a deep booming thump with a crackling electric discharge
                       and the powerful whoosh of a heavy bolt leaving, a short low rumble after ..."
  ImpactSide   1.0 s  "A small energy bolt hitting a stone floor and bursting: a sharp crack and pop with fizzling sparks ..."
  ImpactHeavy  2.0 s  "A powerful energy blast exploding on a stone floor: a deep explosive boom with a sharp crack, sparks
                       and embers scattering, stone debris rattling down, a short rumble tail ..."
R464 (승규 11-05 "가운데 폭발음만 괜찮고 나머지는 다 별로야 장난 스러워"): the magical whine-and-zap takes sounded like
a toy. Remade as serious, realistic artillery - new nodes <Kind>_v2, three takes each, prompt influence 0.65, "no beeps,
no whine, no sci-fi": ChargeSide_v2 / ChargeHeavy_v2 "ominous build-up ... deep (sub-bass) rumble and grinding stone
resonance swelling with rising air pressure", FireSide_v2 "realistic short artillery shot: a sharp heavy cannon crack with
a deep punch and a quick air-ripping whoosh", FireHeavy_v2 "realistic massive siege cannon firing: a huge deep boom ...
rolling low rumble echoing in a stone hall", ImpactSide_v2 (1.2 s) a smaller take on the middle's burst; the middle's
burst (ImpactHeavy take 2) kept. Every kind is now also weighed by its weight under 200 Hz.
R469 (승규 11-05 "타격 소리가 다 마음에 안들어 3개 시안 줘봐" -> "A 포탄 쿵"): three impact drafts, side and middle, two
takes each (prompt influence 0.7) - A cannonball (RookImpact_A_Cannonball_Side/Heavy: "a heavy iron cannonball slamming
into a stone floor: one massive deep thud with a hard punch and a short low boom felt in the chest, dry and solid. No
debris ..."), B gunpowder blast, C cinematic trailer hit; A chosen. Its takes go in the folder as ImpactSide_<n>.mp3 /
ImpactHeavy_<n>.mp3 and are made with --only Impact (used: A_Side take 1, A_Heavy take 2).
R470 ("b로 해서 소리 키워"): B (gunpowder blast) instead - RookImpact_B_Powder_Side/Heavy ("a gunpowder explosion on a
stone floor: a sharp loud bang followed by a deep rolling boom and a short airy blast tail ... No debris ...") - and the
bursts louder (louder(): a harder soft clip, peak -0.4 dB; their far layers -1 dB).
Of each set the script keeps the one that does its job best (printed): a charge whose loudness climbs most from its
first to its last quarter, a shot or a burst with the hardest attack (peak over the first 50 ms' RMS) - for the heavy
ones weighed by how much of it lies under 200 Hz. Then, mono 44.1 kHz:
  charge  rumble and DC off, cut to the rook's charge (side 0.55 s, middle 3.0 s - LibraryRookTurret.ChargeTime, so it
          ends as the shot leaves) with a 25 ms fade, peak -2 dB (side) / -1 dB (middle)
  shot, burst  rumble off, started 3 ms before the hit, cut 50 dB under its peak (1.6 / 2.2 s at most) with a fade, a
          light saturation, peak -1 dB
  far     the near one dulled (three 700 Hz one-pole low-passes) in a stone-hall tail (1.0 s, 1.4 s for the middle's)
-> Assets/Resources/PawnRushLibrarySfx/Rook{Charge,Fire,Impact}{Side,Heavy}[Far].wav (LibraryRookTurret plays them).
"""
import glob
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SFX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrarySfx"))
SR = 44100
rng = np.random.default_rng(463)
CHARGE = {"Side": .55, "Heavy": 3.0}


def decode(path):
    import aud
    tmp = path + ".decoded.wav"
    aud.Sound(path).rechannel(1).resample(SR, False).write(
        tmp, rate=SR, channels=aud.CHANNELS_MONO, format=aud.FORMAT_S16, container=aud.CONTAINER_WAV, codec=aud.CODEC_PCM)
    w = wave.open(tmp)
    a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768
    w.close()
    os.remove(tmp)
    return a


def write(path, x):
    w = wave.open(path, "wb")
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    w.close()
    print("WROTE", os.path.basename(path), round(len(x) / SR, 3), "s")


def onepole(x, hz):
    a = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    s = 0.0
    for i, v in enumerate(x):
        s = (1 - a) * v + a * s
        y[i] = s
    return y


def low_share(x):
    spec = np.abs(np.fft.rfft(x)) ** 2
    f = np.fft.rfftfreq(len(x), 1 / SR)
    return spec[f < 200].sum() / max(spec.sum(), 1e-12)


def rms(x):
    return np.sqrt(np.mean(x ** 2) + 1e-12)


def score(kind, x):
    x = x - onepole(x, 30)
    # R464 ("장난 스러워"): every kind weighed by its weight under 200 Hz, so the dark take wins over the bright one
    if kind.startswith("Charge"):
        q = len(x) // 4
        return min(rms(x[-q:]) / rms(x[:q]), 4.0) * (.3 + low_share(x))
    peak = np.abs(x).max()
    at = int(np.argmax(np.abs(x) > .05 * peak))
    head = x[at:at + int(.05 * SR)]
    s = np.abs(head).max() / rms(head)
    return s * (.5 + low_share(x)) / (1 + 6 * debris(x))


def debris(x):
    """R466: how much the take rattles after its hit - its clicks over 2.5 kHz from 0.12 s past the onset on, against
    the hit's own loudness (stone chips, gravel, fittings: 승규 "파편 튀기는 소리 들려 안돼")."""
    peak = np.abs(x).max()
    at = int(np.argmax(np.abs(x) > .05 * peak))
    hp = x - onepole(x, 2500)
    tail = hp[at + int(.12 * SR):]
    if len(tail) < SR // 50:
        return 0.0
    w = int(.005 * SR)
    env = np.array([np.abs(tail[i:i + w]).max() for i in range(0, len(tail) - w, w)])
    clicks = np.maximum(env - np.median(env) * 2, 0)
    return float(clicks.mean() / max(rms(x[at:at + int(.12 * SR)]), 1e-6))


def charge(x, side):
    x = x - onepole(x, 30)
    peak = np.abs(x).max()
    start = int(np.argmax(np.abs(x) > .02 * peak))
    n = int(CHARGE[side] * SR)
    x = x[start:].copy()
    # where it stops being loud (its last 10 ms within 12 dB of the peak): the takes whine, then die before their end,
    # so that is where the charge must meet the shot - the side take's loud part is about 0.4 s, laid at the end of
    # the 0.55 s charge with silence before it
    w = int(.01 * SR)
    loud = [i for i in range(0, len(x) - w, w) if np.sqrt(np.mean(x[i:i + w] ** 2)) > peak * 10 ** (-12 / 20) / 1.4]
    end = min(len(x), (loud[-1] + w if loud else len(x)), n)
    x = x[:end]
    x[:int(.004 * SR)] *= np.linspace(0, 1, int(.004 * SR))
    x *= np.linspace(.45, 1, len(x)) ** 1.5                      # and swelling toward the shot
    if len(x) < n:
        x = np.concatenate([np.zeros(n - len(x)), x])
    fade = int(.025 * SR)
    x[-fade:] *= np.linspace(1, 0, fade)
    return x / np.abs(x).max() * (.79 if side == "Side" else .89)


def hit(x, longest, under=50):
    x = x - onepole(x, 30)
    peak = np.abs(x).max()
    start = max(0, int(np.argmax(np.abs(x) > .05 * peak)) - int(.003 * SR))
    x = x[start:]
    env = np.maximum.accumulate(np.abs(x)[::-1])[::-1]
    end = min(len(x), int(longest * SR), int(np.argmax(env < peak * 10 ** (-under / 20))) or len(x))
    x = x[:end].copy()
    fade = min(int(.12 * SR), len(x) // 3)
    x[-fade:] *= np.linspace(1, 0, fade) ** 2
    x /= np.abs(x).max()
    x = np.tanh(1.5 * x) / np.tanh(1.5)
    return x / np.abs(x).max() * .89


# R468 (승규 11-05 "좀 더 묵직하게 가운데 도 더 묵직하게"): every take made heavier after it is chosen - pitched down
# (played slower), its lows raised under 150 Hz, and a sub-bass weight added under it: under a shot or a burst a
# thump (a sine falling from f0 to f1 Hz, gone in `decay` s), under a charge a rumble (a sine and noise under 60 Hz)
# swelling with the take itself. Then the same peak and saturation as before.
WEIGHT = {"Side": dict(pitch=.88, shelf=1.0, sub=.55, decay=.22, f0=78, f1=44),
          "Heavy": dict(pitch=.84, shelf=1.4, sub=.75, decay=.45, f0=66, f1=34)}


def repitch(x, ratio):
    """Played at `ratio` speed (under 1: lower and longer)."""
    n = int(len(x) / ratio)
    return np.interp(np.arange(n) * ratio, np.arange(len(x)), x)


def shelf(x, gain):
    return x + gain * onepole(onepole(x, 150), 150)


def thump(n, w):
    t = np.arange(n) / SR
    f = w["f1"] + (w["f0"] - w["f1"]) * np.exp(-t / .08)
    env = (1 - np.exp(-t / .004)) * np.exp(-t / w["decay"])
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * env


def rumble(x, w):
    env = onepole(np.abs(x), 8)
    env /= max(env.max(), 1e-9)
    t = np.arange(len(x)) / SR
    noise = onepole(onepole(rng.standard_normal(len(x)), 60), 60)
    noise /= max(np.abs(noise).max(), 1e-9)
    return (.6 * np.sin(2 * np.pi * (w["f1"] + 6) * t) + .8 * noise) * env


def heavier(near, w, charge_):
    peak = np.abs(near).max()
    y = shelf(near, w["shelf"])
    y = y + w["sub"] * np.abs(y).max() * (rumble(near, w) if charge_ else thump(len(y), w))
    y /= np.abs(y).max()
    if not charge_:
        y = np.tanh(1.4 * y) / np.tanh(1.4)
    else:
        fade = int(.025 * SR)
        y[-fade:] *= np.linspace(1, 0, fade)
    return y / np.abs(y).max() * peak


# R470 (승규 11-05 "b로 해서 소리 키워"): the burst driven harder - a soft clip that lifts its body and tail toward
# its peak (about +5 dB of loudness), the peak just under full scale.
def louder(x, drive=3.2):
    y = np.tanh(drive * x / np.abs(x).max()) / np.tanh(drive)
    return y / np.abs(y).max() * .95


def far(x, tail):
    y = onepole(onepole(onepole(x, 700), 700), 700)
    n = int(SR * tail)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 6.9, n))
    ir = onepole(ir, 2200)
    ir /= np.sqrt((ir ** 2).sum())
    pad = np.concatenate([y, np.zeros(n)])
    size = 2 ** int(np.ceil(np.log2(len(pad) + n)))
    wet = np.real(np.fft.ifft(np.fft.fft(pad, size) * np.fft.fft(ir, size)))[:len(pad)]
    z = .55 * pad + .6 * wet
    return z / np.abs(z).max() * .79


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    if not argv:
        raise SystemExit(__doc__)
    folder = argv[0]
    out = argv[argv.index("--out") + 1] if "--out" in argv else SFX
    report = "--report" in argv
    only = argv[argv.index("--only") + 1] if "--only" in argv else ""      # R466: e.g. Side - the others left as they are
    os.makedirs(out, exist_ok=True)
    for kind in ("ChargeSide", "ChargeHeavy", "FireSide", "FireHeavy", "ImpactSide", "ImpactHeavy"):
        if only and only not in kind:
            continue
        takes = sorted(glob.glob(os.path.join(folder, kind + "_*.mp3")))
        if not takes:
            print("MISSING", kind)
            continue
        scored = []
        for t in takes:
            x = decode(t)
            s = score(kind, x)
            scored.append((s, t, x))
            print("TAKE", kind, os.path.basename(t), "score", round(s, 2), "len", round(len(x) / SR, 2),
                  "low", round(low_share(x), 2), "debris", round(debris(x), 3))
        s, t, x = max(scored, key=lambda r: r[0])
        print("USE", kind, os.path.basename(t))
        if report:
            continue
        side = "Heavy" if kind.endswith("Heavy") else "Side"
        name = "Rook" + kind
        w = WEIGHT[side]
        x = repitch(x, w["pitch"])
        if kind.startswith("Charge"):
            near = heavier(charge(x, side), w, True)
        else:
            # R466: the side ones cut 40 dB under their peak and short, so no settling tail is left to rattle
            near = hit(x, (1.0 if side == "Side" else 2.5) if kind.startswith("Impact") else (.9 if side == "Side" else 2.0),
                       40 if side == "Side" else 50)
            near = heavier(near, w, False)
            if kind.startswith("Impact"):
                # R471 ("가운데 사운드 폭발음 더 크게"): the middle's burst driven harder still, its boom held up longer
                near = louder(near, 6.0 if side == "Heavy" else 3.2)
        write(os.path.join(out, name + ".wav"), near)
        layer = far(near, 1.4 if side == "Heavy" else 1.0)
        if kind.startswith("Impact"):
            layer = layer / np.abs(layer).max() * .89        # R470: the burst's far layer as loud as can be too
        write(os.path.join(out, name + "Far.wav"), layer)


main()

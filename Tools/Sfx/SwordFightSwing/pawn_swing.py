# pawn_swing.py: click-cut whooshes made in code (10-29) - Resources/SwordFightSkillSfx/Swing<Piece>.wav.
# 승규 님 asked for them from code after ElevenLabs stayed busy: the pawn first, then the bishop (its generated takes
# sounded like a whip). Air noise through a band-pass whose centre sweeps up as the blade passes and falls away after
# (the pass-by), over a little low body for weight. Wide filters only, so there is no ringing tone (coin.py should
# find nothing).
#   python pawn_swing.py out.wav            the pawn (pawn_blade: the one in use)
#   python pawn_swing.py out.wav bishop     the bishop (bishop_a in use; rook_c for the rook)
import sys, wave
import numpy as np

SR = 44100

# length (s), when the blade passes (s), rise curve, fall (s), filter centre start / top / end (Hz), filter Q,
# low body cutoff (Hz) and amount, seed.
PIECES = {
    # A short, tight thrust.
    "pawn": dict(length=0.42, peak=0.11, rise=1.6, fall=0.075, f0=900, f1=3200, f2=1300, fall_f=0.06, q=1.1,
                 body_cut=380, body=2.2, seed=1029),
    # 10-29 (승규 님: "폰을 휙휙 하는 소리로 바꿔"): two quick passes, a small flick then the thrust.
    "pawn_double": dict(length=0.45, peak=0.2, rise=4.0, fall=0.075, f0=900, f1=3200, f2=1300, fall_f=0.06, q=1.1,
                        body_cut=380, body=0.6, seed=1030, pre=(0.06, 0.7)),
    # 10-29 (승규 님: the ElevenLabs take "칼 휘두르는 소리가 아니잖아", then "코드로 만들어줘"): one blade cut - a thin
    # steel edge whipped past the ear, so high bright air with a fast sharp sweep and almost no low body.
    "pawn_blade": dict(length=0.4, peak=0.15, rise=3.0, fall=0.05, f0=1500, f1=5500, f2=2000, fall_f=0.04, q=1.6,
                       body_cut=400, body=0.5, seed=1031),
    # 10-29 (승규 님: the ElevenLabs rook take "가볍고 빠른 검한테 어울려 룩은 크고 묵직한 검이라", then "코드로"): a
    # huge greatsword swung hard - low thick air that only reaches the mids as the blade passes (0.18 s, the rook's cut
    # going through), a heavy low body, a slow tail.
    "rook_heavy": dict(length=0.8, peak=0.18, rise=2.0, fall=0.2, f0=220, f1=900, f2=300, fall_f=0.16, q=0.75,
                       body_cut=160, body=3.4, seed=1032),
    # 10-29 (승규 님: "이건 또 너무 바람 소리 같아"): less wind, more blade - a narrower band swept fast so the edge
    # is heard cutting, over a short heavy push. Three to pick from:
    # A a broad heavy blade: the edge in the low mids, a big push.
    "rook_a": dict(length=0.6, peak=0.17, rise=2.6, fall=0.08, f0=350, f1=1500, f2=500, fall_f=0.07, q=2.0,
                   body_cut=140, body=2.2, seed=1033),
    # B the same blade a touch sharper: the edge higher, still heavy.
    "rook_b": dict(length=0.55, peak=0.16, rise=3.0, fall=0.07, f0=500, f1=2200, f2=700, fall_f=0.06, q=2.4,
                   body_cut=150, body=1.8, seed=1034),
    # C the heaviest: a slow low edge and a strong low "whump" at the pass.
    "rook_c": dict(length=0.65, peak=0.18, rise=2.2, fall=0.1, f0=250, f1=1100, f2=350, fall_f=0.09, q=1.8,
                   body_cut=120, body=3.0, seed=1035),
    # A light curved blade swung round: a softer, longer swell (no sharp front, so no whip crack), lower and rounder
    # air, and a gentle tail.
    "bishop": dict(length=0.6, peak=0.2, rise=2.2, fall=0.12, f0=600, f1=2000, f2=800, fall_f=0.1, q=0.9,
                   body_cut=300, body=2.6, seed=2910),
    # 10-29 (승규 님: "다른 소리"), three more to pick from:
    # A thin and quick - a slender blade flicked past, high air, little body.
    "bishop_a": dict(length=0.45, peak=0.12, rise=1.8, fall=0.08, f0=1200, f1=4500, f2=1800, fall_f=0.06, q=1.3,
                     body_cut=450, body=1.0, seed=3101),
    # B a double swish - the curved blade turned over in the hand: a small pass, then the cut.
    "bishop_b": dict(length=0.55, peak=0.24, rise=2.0, fall=0.09, f0=800, f1=3000, f2=1100, fall_f=0.07, q=1.1,
                     body_cut=350, body=1.6, seed=3102, pre=(0.08, 0.45)),
    # C deep and round - a broad, slow "whum", mostly low air.
    "bishop_c": dict(length=0.65, peak=0.22, rise=2.0, fall=0.13, f0=300, f1=1200, f2=400, fall_f=0.12, q=0.8,
                     body_cut=220, body=3.2, seed=3103),
    # 10-29 later (승규 님: "비숍 칼 휘두르는 사운드 다시"): A was mostly air; these are more blade, the way the rook's
    # pick went - a narrower band swept fast so the edge is heard cutting. Three to pick from:
    # D a mid-weight blade: the edge sweeps through the mids, a light push under it.
    "bishop_d": dict(length=0.5, peak=0.14, rise=2.6, fall=0.07, f0=700, f1=2800, f2=900, fall_f=0.06, q=2.2,
                     body_cut=260, body=1.4, seed=3104),
    # E quick and keen: the same blade higher and faster, a crisp short cut.
    "bishop_e": dict(length=0.4, peak=0.11, rise=3.0, fall=0.055, f0=1000, f1=3600, f2=1300, fall_f=0.045, q=2.6,
                     body_cut=320, body=0.9, seed=3105),
    # F broad and full: a longer swing with a lower edge and more weight (lighter than the rook's C).
    "bishop_f": dict(length=0.55, peak=0.16, rise=2.4, fall=0.085, f0=450, f1=1900, f2=650, fall_f=0.075, q=2.0,
                     body_cut=200, body=2.0, seed=3106),
}


def svf_bandpass(x, centre, q):
    """State-variable band-pass whose centre (Hz, per sample) moves."""
    y = np.zeros_like(x)
    low = band = 0.0
    damp = 1.0 / q
    for i in range(len(x)):
        f = 2.0 * np.sin(np.pi * min(centre[i], SR / 6) / SR)
        high = x[i] - low - damp * band
        band += f * high
        low += f * band
        y[i] = band
    return y


def one_pole_low(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.zeros_like(x)
    s = 0.0
    for i in range(len(x)):
        s = (1 - a) * x[i] + a * s
        y[i] = s
    return y


def swing(p):
    rng = np.random.default_rng(p["seed"])
    n = int(SR * p["length"])
    t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    peak = p["peak"]
    # The pass-by: a swell to the blade's closest point, then away.
    env = np.where(t < peak, (t / peak) ** p["rise"], np.exp(-(t - peak) / p["fall"]))
    if "pre" in p:
        # An earlier, smaller pass (when, how loud) before the main one.
        at, gain = p["pre"]
        env = env + gain * np.exp(-((t - at) / 0.035) ** 2)
    env *= np.clip(t / 0.004, 0, 1)
    # Bright air: the centre sweeps up into the peak, then back down.
    centre = np.where(t < peak, p["f0"] + (p["f1"] - p["f0"]) * (t / peak) ** 1.3,
                      p["f2"] + (p["f1"] - p["f2"]) * np.exp(-(t - peak) / p["fall_f"]))
    air = svf_bandpass(noise, centre, q=p["q"])
    # Low body: the blade's push, darker and a touch later.
    body = one_pole_low(one_pole_low(noise, p["body_cut"]), p["body_cut"])
    late = peak + 0.01
    body_env = np.where(t < late, (t / late) ** 2, np.exp(-(t - late) / (p["fall"] * 0.8)))
    x = air * env + p["body"] * body * body_env
    x -= np.mean(x)
    # Soft end so it never clicks off.
    x *= np.clip((p["length"] - t) / 0.03, 0, 1)
    return x / (np.max(np.abs(x)) / 0.9)


out = sys.argv[1] if len(sys.argv) > 1 else "SwingPawn.wav"
piece = sys.argv[2] if len(sys.argv) > 2 else "pawn"
x = swing(PIECES[piece])
with wave.open(out, "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes((x * 32767).astype("<i2").tobytes())
print(out, piece, f"{len(x) / SR:.2f}s")

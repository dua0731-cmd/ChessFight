"""R559 mockups: four takes on the chandelier / knight floor-circle warning, same clock as now
(ticks every LEAD/8 s until the last 0.35 s, then a blink accent 7 a second). Knight = 4 semitones lower, 2.05 s."""
import os, sys, wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
rng = np.random.default_rng(7)


def t(sec):
    return np.arange(int(sec * SR)) / SR


def onepole(x, hz):
    k = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x); p = 0.0
    for i, v in enumerate(x):
        p = (1 - k) * v + k * p; y[i] = p
    return y


def reson(x, f, decay):
    # two-pole resonator: a struck body ringing at f for about `decay` s
    r = np.exp(-1 / (decay * SR)); w = 2 * np.pi * f / SR
    a1, a2 = -2 * r * np.cos(w), r * r
    y = np.zeros_like(x); y1 = y2 = 0.0
    for i, v in enumerate(x):
        y0 = v - a1 * y1 - a2 * y2; y[i] = y0; y2, y1 = y1, y0
    return y * (1 - r)


def burst(sec, hz):
    n = int(sec * SR)
    return onepole(rng.standard_normal(n), hz) * np.exp(-np.arange(n) / (SR * sec / 5))


def pad(x, sec):
    out = np.zeros(int(sec * SR)); out[:len(x)] = x[:len(out)]; return out


def norm(x, db=-3):
    return x * (10 ** (db / 20) / (np.abs(x).max() + 1e-12))


# ---- one hit for each take: (kind, index 0..7 for ticks / j 0..2 for blinks, pitch factor)
def wood_tick(k, p):
    x = pad(burst(.004, 6000), .09)
    b = reson(x, 1150 * p * 2 ** (k / 24), .018) * 3 + reson(x, 420 * p, .03) * 2
    return norm(onepole(b, 3500), -1) * (.4 + .35 * k / 8)


def wood_blink(j, p):
    a = pad(wood_tick(8, p * .82), .16)
    x = pad(burst(.006, 3000), .16)
    body = reson(x, 260 * p, .05) * 3
    return (norm(a, -1) * .7 + np.roll(norm(a, -1), int(.035 * SR)) * .6 + norm(body, -1) * .8) * (.85 + .1 * j)


def clock_tick(k, p):
    # chess clock escapement: tik / tok alternating, dark
    f = (1500 if k % 2 == 0 else 1150) * p
    x = pad(burst(.003, 7000), .07)
    b = reson(x, f, .012) * 3 + reson(x, f * 1.47, .008) * 1.5
    return norm(onepole(b, 3000), -1) * (.35 + .3 * k / 8)


def clock_blink(j, p):
    # the clock's button pressed: a thick wooden clack with a low knock under it
    x = pad(burst(.008, 2500), .2)
    clack = reson(x, 700 * p, .025) * 3 + reson(x, 180 * p, .06) * 2.5
    tt = t(.2)
    knock = np.sin(2 * np.pi * 95 * p * tt) * np.exp(-tt / .05)
    return norm(onepole(norm(clack) + .6 * knock, 2800), -1) * (.9 + .05 * j)


def drum_tick(k, p):
    tt = t(.18)
    f0 = (150 + 30 * k / 8) * p
    ph = 2 * np.pi * np.cumsum(f0 * (1 + .6 * np.exp(-tt / .015))) / SR
    body = np.sin(ph) * np.exp(-tt / .06)
    skin = onepole(rng.standard_normal(len(tt)), 1200) * np.exp(-tt / .006) * .4
    return norm(onepole(onepole(body + skin, 1400), 1400), -1) * (.4 + .35 * k / 8)


def drum_blink(j, p):
    tt = t(.3)
    f0 = 95 * p
    ph = 2 * np.pi * np.cumsum(f0 * (1 + 1.0 * np.exp(-tt / .02))) / SR
    body = np.sin(ph) * np.exp(-tt / .11)
    skin = onepole(rng.standard_normal(len(tt)), 900) * np.exp(-tt / .01) * .5
    rim = pad(reson(pad(burst(.003, 5000), .05), 900 * p, .01) * 3, .3)
    return norm(onepole(onepole(norm(body) + skin + .25 * norm(rim), 1600), 1600), -1) * (.9 + .05 * j)


def beep_tick(k, p):
    # warm, soft-edged tone with a tiny wooden click in front (closest to now)
    tt = t(.1)
    f = 260 * p * 2 ** (k / 12 * 1.5)
    e = np.minimum(1, tt / .006) * np.exp(-tt / .035)
    tone = (np.sin(2 * np.pi * f * tt) + .25 * np.sin(4 * np.pi * f * tt)) * e
    click = pad(reson(pad(burst(.002, 5000), .03), 1800 * p, .006) * 2, .1)
    return norm(onepole(tone + .5 * norm(click), 2200), -1) * (.4 + .35 * k / 8)


def beep_blink(j, p):
    tt = t(.14)
    f = 392 * p * (1, 1, 2 ** (3 / 12))[j]
    e = np.minimum(1, tt / .008) * np.exp(-tt / .06)
    tone = (np.sin(2 * np.pi * f * tt) + .3 * np.sin(4 * np.pi * f * tt) + .12 * np.sin(6 * np.pi * f * tt)) * e
    tone += .5 * np.sin(2 * np.pi * f / 2 * tt) * e
    return norm(onepole(tone, 2000), -1) * (.9 + .05 * j)


def now_tick(k, p):
    tt = t(.06 if p > .9 else .07)
    f = (260 if p > .9 else 196) * 2 ** (k / 12 * 1.5)
    e = np.ones(len(tt)); e[:int(.003 * SR)] = np.linspace(0, 1, int(.003 * SR)); nr = int(.04 * SR); e[-nr:] *= np.linspace(1, 0, nr) ** 2
    return sum(g * np.sin(2 * np.pi * f * (i + 1) * tt) for i, g in enumerate((1, .35, .1))) * e * (.45 + .4 * k / 8)


def now_blink(j, p):
    tt = t(.07 if p > .9 else .08)
    f = 523 if p > .9 else 392
    e = np.ones(len(tt)); e[:int(.003 * SR)] = np.linspace(0, 1, int(.003 * SR)); nr = int(.04 * SR); e[-nr:] *= np.linspace(1, 0, nr) ** 2
    return sum(g * np.sin(2 * np.pi * f * (i + 1) * tt) for i, g in enumerate((1, .35, .1))) * e * (.9 + .1 * j)


TAKES = {"A": (wood_tick, wood_blink), "B": (clock_tick, clock_blink), "C": (drum_tick, drum_blink),
         "D": (beep_tick, beep_blink), "Now": (now_tick, now_blink)}


def build(tick, blink, lead, p):
    buf = np.zeros(int((lead + .35) * SR))

    def place(x, at):
        i = int(at * SR); buf[i:i + len(x)] += x[:max(0, len(buf) - i)]
    for k in range(8):
        at = k * lead / 8
        if at >= lead - .35: break
        place(tick(k, p), at)
    k = j = 0
    while k / 7 < lead - .02:
        if k / 7 >= lead - .35:
            place(blink(j, p), k / 7); j += 1
        k += 1
    return buf


def room(x):
    # a short stone-hall tail only for listening here (Unity adds the real hall ring)
    n = int(.9 * SR)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / (SR * .22))
    ir = onepole(ir, 2500); ir[0] = 0
    wet = np.convolve(x, ir)[:len(x)]
    return x + .12 * wet / (np.abs(wet).max() + 1e-9) * np.abs(x).max()


def write(name, x):
    x = norm(room(x), -3)
    w = wave.open(os.path.join(OUT, name + ".wav"), "wb")
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes()); w.close()
    print(name, round(len(x) / SR, 2))


os.makedirs(OUT, exist_ok=True)
for key, (tk, bl) in TAKES.items():
    write(key + "_chandelier", build(tk, bl, 1.6, 1.0))
    write(key + "_knight", build(tk, bl, 2.05, 2 ** (-4 / 12)))

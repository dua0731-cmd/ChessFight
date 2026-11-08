"""Pawn Rush library (작전 회의실): the air cannons' charge sound, 2.6 s to match the longer charge (R418).

    python cannon_charge_sound.py                 (writes CannonCharge.wav + CannonChargeFar.wav into Resources)
    python cannon_charge_sound.py --out D:/check  (writes them there instead)

Needs numpy only (Blender's bundled Python has it). 승규 님 (11-05): "차징 할 때 더 길고 멋있게 해주고 그에 맞게 사운드도
길이 맞춰" - LibraryAirCannon's charge is now 2.6 s (1.4 .. 4.0 of its 5 s round, the shot still at 4.0), so its whine
is rebuilt the way R292 / R294 made the 1.4 s one (the fan's own loop played faster and faster, rising air), longer and
with more going on, every layer following the charge c = t / 2.6 as the VFX does:
  fan       CannonFanLoop.wav (the D whirr the cannons idle on) read 1 -> 2.7 times as fast (c^2, as the fan's spin)
  air       noise through a band rising 300 Hz -> 2.6 kHz, louder as it goes: the air drawn in
  whine     a soft, detuned rising sweep 160 -> 900 Hz with a slow vibrato, coming in over the second half (it always
            moves, so no fixed ring - coin.py)
  flutter   the last third: the air layer trembles 14 -> 34 times a second (the flicker of the glow) and the rim
            crackles (sparse clicks, denser to the end)
  breath    a last breath in over the final 0.3 s, cut off 15 ms before the shot so CannonFire lands on silence
The far layer is the same low-passed with a hall tail (1.2 s), as the R292 far layers. Peak -2 dB like the old take.
"""
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SFX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrarySfx"))
SR, T = 44100, 2.6
rng = np.random.default_rng(418)


def read(path):
    w = wave.open(path)
    a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768
    if w.getnchannels() > 1:
        a = a.reshape(-1, w.getnchannels()).mean(1)
    return a


def write(path, x):
    x = np.clip(x, -1, 1)
    w = wave.open(path, "wb")
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes((x * 32767).astype(np.int16).tobytes())
    w.close()
    print("WROTE", path, round(len(x) / SR, 3), "s")


def onepole(x, hz):
    a = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    s = 0.0
    for i, v in enumerate(x):
        s = (1 - a) * v + a * s
        y[i] = s
    return y


def bandpass(x, centre, q=1.2):
    """State-variable band-pass with a moving centre (Hz per sample)."""
    y = np.empty_like(x)
    low = band = 0.0
    for i, v in enumerate(x):
        f = 2 * np.sin(np.pi * min(centre[i], SR / 6) / SR)
        high = v - low - band / q
        band += f * high
        low += f * band
        y[i] = band
    return y


def charge():
    n = int(SR * T)
    t = np.arange(n) / SR
    c = t / T
    # fan: the loop read faster and faster
    fan = read(os.path.join(SFX, "CannonFanLoop.wav"))
    speed = 1 + 1.7 * c ** 2
    pos = np.cumsum(speed) % (len(fan) - 1)
    i0 = pos.astype(int)
    fr = pos - i0
    fan_l = (fan[i0] * (1 - fr) + fan[i0 + 1] * fr) * (.55 + .45 * c)
    # air: a rising band of noise
    noise = rng.standard_normal(n)
    air = bandpass(noise, 300 * (2600 / 300) ** (c ** 1.3), 1.4)
    air /= np.abs(air).max()
    air *= .15 + .55 * c ** 1.6
    # flutter on the air in the last third
    late = np.clip((c - .62) / .38, 0, 1)
    rate = 14 + 20 * late
    trem = 1 - .45 * late * (.5 + .5 * np.sin(2 * np.pi * np.cumsum(rate) / SR))
    air *= trem
    # whine: two detuned rising sweeps with a slow vibrato, in over the second half
    f = 160 * (900 / 160) ** (c ** 1.5) * (1 + .012 * np.sin(2 * np.pi * 5.5 * t))
    ph = 2 * np.pi * np.cumsum(f) / SR
    whine = (np.sin(ph) + .7 * np.sin(ph * 1.007 + 1) + .25 * np.sin(2 * ph * 1.003)) / 1.95
    whine *= .2 * np.clip((c - .35) / .65, 0, 1) ** 1.5
    # crackle: short filtered clicks, denser to the end
    crackle = np.zeros(n)
    k = 0
    while k < n:
        lc = max(0.0, (k / n - .62) / .38)
        if lc > 0 and rng.random() < 60 * lc / SR:      # up to 60 clicks a second at the end
            m = int(SR * rng.uniform(.002, .006))
            burst = rng.standard_normal(min(m, n - k)) * np.exp(-np.linspace(0, 6, min(m, n - k)))
            crackle[k:k + len(burst)] += burst * rng.uniform(.15, .4) * lc
            k += m
        k += 1
    crackle = crackle - onepole(crackle, 1500)          # bright clicks only
    # breath in over the last 0.3 s
    breath_env = np.clip((t - (T - .3)) / .3, 0, 1) ** 2
    breath = bandpass(rng.standard_normal(n), 900 + 3000 * breath_env, 1.0)
    breath = breath / np.abs(breath).max() * .35 * breath_env
    x = .9 * fan_l + air + whine + crackle + breath
    x = x - onepole(x, 40)                               # no DC or rumble
    # in over 60 ms, off 15 ms before the shot
    env = np.clip(t / .06, 0, 1) * np.clip((T - .015 - t) / .01, 0, 1)
    x *= env
    # a soft saturation so the build sits as loud as the old take (RMS about -15 dB), the peak at -2 dB
    x = np.tanh(2.6 * x / np.abs(x).max()) / np.tanh(2.6)
    return x / np.abs(x).max() * .79


def far(x):
    y = onepole(onepole(onepole(x, 800), 800), 800)   # far: duller (no ring above 1.8 kHz in the tail, coin.py)
    ir_n = int(SR * 1.2)
    ir = rng.standard_normal(ir_n) * np.exp(-np.linspace(0, 6.9, ir_n))
    ir = onepole(ir, 2500)
    ir /= np.sqrt((ir ** 2).sum())
    pad = np.concatenate([y, np.zeros(ir_n)])
    wet = np.real(np.fft.ifft(np.fft.fft(pad, 2 ** int(np.ceil(np.log2(len(pad) + ir_n)))) *
                              np.fft.fft(ir, 2 ** int(np.ceil(np.log2(len(pad) + ir_n))))))[:len(pad)]
    z = .55 * pad + .6 * wet
    return z / np.abs(z).max() * .79


def main():
    argv = sys.argv[1:]
    out = argv[argv.index("--out") + 1] if "--out" in argv else SFX
    os.makedirs(out, exist_ok=True)
    x = charge()
    write(os.path.join(out, "CannonCharge.wav"), x)
    write(os.path.join(out, "CannonChargeFar.wav"), far(x))


if __name__ == "__main__":
    main()

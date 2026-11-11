"""The falling chandelier's and knight's floor-circle warning, take A "나무 시계 째깍" (R560).

승규 님 (11-10): "샹들리에 기사 A로" - from the R559 mockups (https://claude.ai/artifact/SSMzB2Ct9vPCPjZjg9DK4N,
sfx_takes/fallwarn_mockup/warn4.py). Same clock as before (R533 / R543): a tick every lead / 8 s until the last
0.35 s, then an accent on each blink (7 a second). Only the material changed, from sine beeps to wood:
  tick   a 4 ms noise strike through two wooden bodies (1150 Hz rising a quarter tone a tick, 420 Hz), 3.5 kHz lowpass,
         louder tick by tick
  blink  that strike a little lower, doubled 35 ms later ("톡톡"), over a dull 260 Hz knock
The knight is 4 semitones lower. Used by hall_chandelier_sounds.py and hall_knight_sounds.py; the noise is seeded and
drawn in the mockup's order, so both come out as heard on the page (without the page's room tail: Unity adds the ring).
"""
import numpy as np

SR = 44100
URGENT, BLINK = .35, 7.0


def _onepole(x, hz):
    k = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    p = 0.0
    for i, v in enumerate(x):
        p = (1 - k) * v + k * p
        y[i] = p
    return y


def _reson(x, f, decay):
    # a struck body ringing at f for about `decay` s (two-pole resonator)
    r = np.exp(-1 / (decay * SR))
    w = 2 * np.pi * f / SR
    a1, a2 = -2 * r * np.cos(w), r * r
    y = np.zeros_like(x)
    y1 = y2 = 0.0
    for i, v in enumerate(x):
        y0 = v - a1 * y1 - a2 * y2
        y[i] = y0
        y2, y1 = y1, y0
    return y * (1 - r)


def _pad(x, sec):
    out = np.zeros(int(sec * SR))
    out[:len(x)] = x[:len(out)]
    return out


def _norm(x, db):
    return x * (10 ** (db / 20) / (np.abs(x).max() + 1e-12))


class _Wood:
    def __init__(self, rng):
        self.rng = rng

    def burst(self, sec, hz):
        n = int(sec * SR)
        return _onepole(self.rng.standard_normal(n), hz) * np.exp(-np.arange(n) / (SR * sec / 5))

    def tick(self, k, p):
        x = _pad(self.burst(.004, 6000), .09)
        b = _reson(x, 1150 * p * 2 ** (k / 24), .018) * 3 + _reson(x, 420 * p, .03) * 2
        return _norm(_onepole(b, 3500), -1) * (.4 + .35 * k / 8)

    def blink(self, j, p):
        a = _pad(self.tick(8, p * .82), .16)
        x = _pad(self.burst(.006, 3000), .16)
        body = _reson(x, 260 * p, .05) * 3
        return (_norm(a, -1) * .7 + np.roll(_norm(a, -1), int(.035 * SR)) * .6 + _norm(body, -1) * .8) * (.85 + .1 * j)

    def warn(self, lead, p):
        buf = np.zeros(int((lead + .35) * SR))

        def place(x, at):
            i = int(at * SR)
            buf[i:i + len(x)] += x[:max(0, len(buf) - i)]
        for k in range(8):
            at = k * lead / 8
            if at >= lead - URGENT:
                break
            place(self.tick(k, p), at)
        k = j = 0
        while k / BLINK < lead - .02:
            if k / BLINK >= lead - URGENT:
                place(self.blink(j, p), k / BLINK)
                j += 1
            k += 1
        # Exactly `lead` long (the knight's warning ends at the impact), the last blink's tail cut over 10 ms.
        out = buf[:int(lead * SR)].copy()
        n = int(.01 * SR)
        out[-n:] *= np.linspace(1, 0, n)
        return _norm(out, -3)


def warnings():
    """(chandelier 1.6 s, knight 2.05 s), peak -3 dB, dry."""
    wood = _Wood(np.random.default_rng(7))
    chandelier = wood.warn(1.6, 1.0)
    wood.rng.standard_normal(int(.9 * SR))   # the mockup drew its room tail here
    return chandelier, wood.warn(2.05, 2 ** (-4 / 12))

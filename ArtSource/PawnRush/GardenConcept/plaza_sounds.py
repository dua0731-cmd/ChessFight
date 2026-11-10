"""Royal water garden beat 1 (the fountain plaza): its sounds from ElevenLabs takes, near and far layers (R530).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I plaza_sounds.py              (writes)
    uv run ... python -I plaza_sounds.py --report                                                          (measures only)
    uv run ... python -I plaza_sounds.py --pick SpinHit=2                                                  (another take)

승규 11-09: "정원 sfx 넣으려고 ... 1번 부터 시작". The takes come from the ElevenLabs flow "R530 정원 광장 소리"
(WH5yYXhvQMCgDfUArH2Q), five sfx nodes x 2 takes (eleven_text_to_sound_v2, prompt influence 0.5), saved as
sfx_takes/R530/<Kind>_<take>.mp3:
  SpinHub      6 s loop  "A heavy wooden beam slowly turning on a greased bronze pivot outdoors: a low steady wooden creak
                          and soft grinding bearing rumble, continuous mechanical rotation, close-mic ..."
  SpinWhoosh   1 s       "A heavy wooden beam swinging fast past the listener: one deep short air whoosh, dull and full ..."
  SpinHit      1 s       "A heavy wooden beam knocking into a wooden figure: one loud dull solid wooden thud ..."
  FountainJet  8 s loop  "A small garden fountain jet arcing into a stone pond: a steady gentle stream of water splashing
                          and pattering into the pool ..."
  MoatLap     10 s loop  "Calm water gently lapping against a stone canal wall: soft slow small waves and quiet trickles ..."
Every prompt ends "no music, no voice" (and no bells / ringing / metal / whistle where it could creep in).

Which take: each is measured - its level (a near-silent take is out), a narrow bright ring (the "coin" the user does not
want: a peak 1.8 - 12 kHz 20 dB over the band's median held 90 ms), voice-like periodicity (80 - 500 Hz, autocorrelation
over 0.6) - and of those left a loop takes the steadier one (the smaller spread of its 100 ms loudness), a whoosh or a
hit the one with more of itself under 250 Hz (deeper). Then, mono 44.1 kHz:
  loops     rumble off (30 Hz), the loop's seam crossfaded over 0.4 s so it runs round without a click, levelled to an
            RMS (hub -19, fountain -21, moat -22 dBFS), peak no higher than -2 dB
  one-shots rumble off, started 3 ms before the onset, cut 50 dB under the peak with a fade, peak -1 dB
  far       three 900 Hz one-pole low-passes (the air and the garden's walls take the highs) in a 0.7 s outdoor tail
            (for a loop wrapped round, so it still loops), peak 3 dB under the near one's
-> Assets/Resources/PawnRushGardenSfx/<Kind>.wav + <Kind>Far.wav (GardenSpinBarSound, GardenPlazaSound).
"""
import os
import sys
import wave

import miniaudio
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TAKES = os.path.join(HERE, "sfx_takes", "R530")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGardenSfx"))
SR = 44100
rng = np.random.default_rng(530)

KINDS = {   # kind: (loop, target RMS dBFS for loops)
    "SpinHub": (True, -19.0),
    "SpinWhoosh": (False, None),
    "SpinHit": (False, None),
    "FountainJet": (True, -21.0),
    "MoatLap": (True, -22.0),
}


def decode(path):
    d = miniaudio.decode_file(path, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    return np.asarray(d.samples, dtype=np.float64)


def write(path, x):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    print("WROTE", os.path.relpath(path, HERE), round(len(x) / SR, 3), "s")


def onepole(x, hz):
    # y[n] = (1 - a) x[n] + a y[n-1], done as a first-order IIR via cumulative trick in blocks (plain loop is fine here)
    a = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    s = 0.0
    for i, v in enumerate(x):
        s = (1 - a) * v + a * s
        y[i] = s
    return y


def db(v):
    return 20 * np.log10(max(v, 1e-12))


def frames_rms(x, ms=100):
    n = int(SR * ms / 1000)
    k = len(x) // n
    return np.sqrt((x[:k * n].reshape(k, n) ** 2).mean(1) + 1e-12)


def ring(x):
    """The longest run (s) of a narrow bright peak 1.8-12 kHz, 20 dB over the band median (coin.py's test)."""
    n, hop = 2048, 512
    win = np.hanning(n)
    f = np.fft.rfftfreq(n, 1 / SR)
    band = (f > 1800) & (f < 12000)
    best = run = 0
    last = -1
    for s in range(0, len(x) - n, hop):
        whole = np.abs(np.fft.rfft(x[s:s + n] * win))
        m = whole[band]
        # an all-low sound leaves the band nearly empty, where any speck stands 20 dB over the median: only a peak
        # within 30 dB of the frame's loudest counts as heard
        if m.max() < 1e-6 or db(m.max()) < db(whole.max()) - 30:
            run = 0
            last = -1
            continue
        hit = db(m.max()) - db(np.median(m)) > 20
        k = int(np.argmax(m))
        run = run + 1 if hit and (last < 0 or abs(k - last) <= 2) else (1 if hit else 0)
        last = k if hit else -1
        best = max(best, run)
    return best * hop / SR


def voiced(x):
    """Highest autocorrelation peak for a 80-500 Hz period over the loudest 0.5 s (a voice would be over 0.6)."""
    n = int(.5 * SR)
    if len(x) < n:
        seg = x
    else:
        r = frames_rms(x, 50)
        i = int(np.argmax(np.convolve(r, np.ones(10), "valid"))) * int(.05 * SR)
        seg = x[i:i + n]
    # Rumble under 80 Hz is smooth, so it correlates at every short lag: off first, and only a true peak (a local
    # maximum, not the slope down from lag 0) counts.
    seg = highpass(seg - seg.mean(), 100)
    c = np.correlate(seg, seg, "full")[len(seg) - 1:]
    if c[0] <= 0:
        return 0.0
    c /= c[0]
    lo, hi = SR // 500, SR // 80
    w = c[lo - 1:hi + 1]
    peaks = [w[i] for i in range(1, len(w) - 1) if w[i] >= w[i - 1] and w[i] >= w[i + 1]]
    return float(max(peaks)) if peaks else 0.0


def low_share(x, hz=250):
    x = highpass(x, 30)   # DC and sub-rumble would count as "low"
    p = np.abs(np.fft.rfft(x)) ** 2
    f = np.fft.rfftfreq(len(x), 1 / SR)
    return float(p[f < hz].sum() / (p.sum() + 1e-12))


def measure(kind, take):
    x = decode(os.path.join(TAKES, f"{kind}_{take}.mp3"))
    r = frames_rms(x)
    return dict(take=take, x=x, rms=db(np.sqrt((x ** 2).mean())), peak=db(np.abs(x).max()),
                spread=float(np.std(20 * np.log10(r[r > 1e-5]))) if (r > 1e-5).any() else 99.0,
                ring=ring(x), voice=voiced(x), low=low_share(x))


def fine(m):
    return m["rms"] > -40 and m["ring"] < .09 and m["voice"] < .6


def choose(kind, ms, forced):
    if forced:
        return next(m for m in ms if m["take"] == forced)
    ok = [m for m in ms if fine(m)] or ms
    loop = KINDS[kind][0]
    return min(ok, key=lambda m: m["spread"]) if loop else max(ok, key=lambda m: m["low"])


def highpass(x, hz):
    return x - onepole(x, hz)


def seamless(x, fade_s=.4):
    """Cross-fades the end into the start: the result loops without a click (length - fade)."""
    n = int(fade_s * SR)
    head, body, tail = x[:n], x[n:len(x) - n], x[len(x) - n:]
    t = np.linspace(0, 1, n)
    mix = tail * np.cos(t * np.pi / 2) + head * np.sin(t * np.pi / 2)    # equal power
    return np.concatenate([body, mix])


def level(x, rms_db, peak_db=-2.0):
    x = x * 10 ** (rms_db / 20) / np.sqrt((x ** 2).mean())
    p = np.abs(x).max()
    lim = 10 ** (peak_db / 20)
    if p > lim:   # soft-clip only the loudest bits
        x = np.tanh(x / lim) * lim
    return x


def shot(x):
    x = highpass(x, 30)
    peak = np.abs(x).max()
    start = max(0, int(np.argmax(np.abs(x) > .05 * peak)) - int(.003 * SR))
    x = x[start:]
    env = np.maximum.accumulate(np.abs(x)[::-1])[::-1]
    cut = np.argmax(env < peak * 10 ** (-50 / 20))
    x = x[:cut if cut > 0 else len(x)].copy()
    fade = min(int(.08 * SR), len(x) // 3)
    x[-fade:] *= np.linspace(1, 0, fade) ** 2
    return x / np.abs(x).max() * 10 ** (-1 / 20)


def tail_ir(seconds=.7):
    n = int(SR * seconds)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 6.9, n))
    ir = onepole(ir, 2500)
    return ir / np.sqrt((ir ** 2).sum())


def far(x, loop):
    y = onepole(onepole(onepole(x, 900), 900), 900)
    ir = tail_ir()
    if loop:   # circular: the tail of the end rings into the start, so it still loops
        size = len(y)
        wet = np.real(np.fft.ifft(np.fft.fft(y) * np.fft.fft(ir, size)))
        z = .6 * y + .55 * wet
    else:
        pad = np.concatenate([y, np.zeros(len(ir))])
        size = 2 ** int(np.ceil(np.log2(len(pad) + len(ir))))
        wet = np.real(np.fft.ifft(np.fft.fft(pad, size) * np.fft.fft(ir, size)))[:len(pad)]
        z = .6 * pad + .55 * wet
    return z / np.abs(z).max() * np.abs(x).max() * 10 ** (-3 / 20)


def main():
    argv = sys.argv[1:]
    report = "--report" in argv
    forced = {}
    if "--pick" in argv:
        for p in argv[argv.index("--pick") + 1].split(","):
            k, v = p.split("=")
            forced[k] = int(v)
    os.makedirs(OUT, exist_ok=True)
    for kind, (loop, target) in KINDS.items():
        ms = [measure(kind, t) for t in (1, 2) if os.path.exists(os.path.join(TAKES, f"{kind}_{t}.mp3"))]
        for m in ms:
            print(f"{kind}_{m['take']}: rms {m['rms']:.1f} peak {m['peak']:.1f} spread {m['spread']:.2f} dB "
                  f"ring {m['ring']:.3f} s voice {m['voice']:.2f} low<250 {m['low'] * 100:.0f}%"
                  f"{'' if fine(m) else '  (rejected)'}")
        m = choose(kind, ms, forced.get(kind))
        print(f"  -> {kind}: take {m['take']}")
        if report:
            continue
        x = m["x"]
        if loop:
            x = level(seamless(highpass(x, 30)), target)
        else:
            x = shot(x)
        write(os.path.join(OUT, kind + ".wav"), x)
        write(os.path.join(OUT, kind + "Far.wav"), far(x, loop))


main()

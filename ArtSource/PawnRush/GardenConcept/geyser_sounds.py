"""Royal water garden beat 2 (the canal walk's floor geysers): the burst a geyser makes when it fires (R534).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I geyser_sounds.py              (writes)
    uv run ... python -I geyser_sounds.py --report                                                          (measures only)
    uv run ... python -I geyser_sounds.py --pick 3                                                          (another take)

승규 11-10: "물 소리가 팍! 튀어나오는 소리로 다시 해". The takes come from the ElevenLabs flow "ChessFight geyser burst
R534" (PaB2r5tvsfy80ziAKBO9), two sfx nodes x 2 takes (eleven_text_to_sound_v2, 1.5 s, prompt influence 0.6), saved as
sfx_takes/R534/GeyserBurst_<take>.mp3:
  1, 2  "Loud sudden burst of pressurized water shooting straight up from a fountain nozzle, sharp explosive whoosh-splash
         attack, then a short hissing spray that cuts off, close-mic, no voice, no music"
  3, 4  "Powerful water jet erupting out of the ground, punchy thump of air and water, explosive splash pop, quick rushing
         water column, close-mic, no voice, no music"

Which take: the plaza's checks (level, a bright "coin" ring, a voice) and of those left the punchiest: the loudest 30 ms
in the first 0.15 s after the onset against the level of the rest (a 팍 is a hit, then less). Then, mono 44.1 kHz:
  near  rumble off, started 3 ms before the onset; the first 0.12 s kept whole and the rest let down to -6 dB over
        0.25 s and faded out by 1.1 s (the hit lasts 0.75 s: the sound should be over when it is), peak -1 dB
  far   the plaza's far layer (three 900 Hz low-passes in a 0.7 s outdoor tail), peak 3 dB under the near one's
-> Assets/Resources/PawnRushGardenSfx/GeyserBurst.wav + GeyserBurstFar.wav (GardenGeyserSound), and the time from the
   start to the loudest 10 ms is printed: GardenGeyserSound.Lead plays the file that much early.
"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
# The plaza's helpers (decode, write, highpass, ring, voiced, far ...): its file ends by running itself, so it is read
# without that last line.
_src = open(os.path.join(HERE, "plaza_sounds.py"), encoding="utf-8").read()
_src = _src[:_src.rstrip().rfind("main()")]
_ns = {"__file__": os.path.join(HERE, "plaza_sounds.py"), "__name__": "plaza_sounds"}
exec(compile(_src, "plaza_sounds.py", "exec"), _ns)
SR, decode, write, highpass, ring, voiced, far, db = (_ns[k] for k in
    ("SR", "decode", "write", "highpass", "ring", "voiced", "far", "db"))

TAKES = os.path.join(HERE, "sfx_takes", "R534")
OUT = _ns["OUT"]


def env(x, ms):
    n = max(1, int(SR * ms / 1000))
    return np.sqrt(np.convolve(x ** 2, np.ones(n) / n, "same") + 1e-12)


def onset(x):
    return int(np.argmax(np.abs(x) > .05 * np.abs(x).max()))


def punch(x):
    o = onset(x)
    e = env(x, 30)
    hit = e[o:o + int(.15 * SR)].max()
    rest = np.sqrt((x[o + int(.3 * SR):] ** 2).mean() + 1e-12)
    return db(hit) - db(rest)


def measure(take):
    x = decode(os.path.join(TAKES, f"GeyserBurst_{take}.mp3"))
    return dict(take=take, x=x, rms=db(np.sqrt((x ** 2).mean())), peak=db(np.abs(x).max()),
                ring=ring(x), voice=voiced(x), punch=punch(x))


def fine(m):
    return m["rms"] > -40 and m["ring"] < .09 and m["voice"] < .6


def shape(x):
    x = highpass(x, 30)
    start = max(0, onset(x) - int(.003 * SR))
    x = x[start:start + int(1.1 * SR)].copy()
    t = np.arange(len(x)) / SR
    # whole for 0.12 s, down to -6 dB by 0.37 s, then out by 1.1 s
    g = np.ones_like(t)
    k = (t > .12) & (t <= .37)
    g[k] = 10 ** (-6 / 20 * (t[k] - .12) / .25)
    late = t > .37
    g[late] = 10 ** (-6 / 20) * np.clip((1.1 - t[late]) / (1.1 - .37), 0, 1) ** 2
    x *= g
    return x / np.abs(x).max() * 10 ** (-1 / 20)


def main():
    argv = sys.argv[1:]
    forced = int(argv[argv.index("--pick") + 1]) if "--pick" in argv else None
    ms = [measure(t) for t in (1, 2, 3, 4) if os.path.exists(os.path.join(TAKES, f"GeyserBurst_{t}.mp3"))]
    for m in ms:
        print(f"GeyserBurst_{m['take']}: rms {m['rms']:.1f} peak {m['peak']:.1f} punch {m['punch']:.1f} dB "
              f"ring {m['ring']:.3f} s voice {m['voice']:.2f}{'' if fine(m) else '  (rejected)'}")
    ok = [m for m in ms if fine(m)] or ms
    m = next(m for m in ms if m["take"] == forced) if forced else max(ok, key=lambda m: m["punch"])
    print(f"  -> take {m['take']}")
    if "--report" in argv:
        return
    x = shape(m["x"])
    lead = int(np.argmax(env(x, 10))) / SR
    print(f"  loudest 10 ms at {lead:.3f} s (GardenGeyserSound.Lead)")
    write(os.path.join(OUT, "GeyserBurst.wav"), x)
    write(os.path.join(OUT, "GeyserBurstFar.wav"), far(x, False))


main()

"""Royal water garden beats 2 .. 4: the sounds that were still missing (R767, 2026-11-22).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I waterworks_sounds.py              (writes)
    uv run ... python -I waterworks_sounds.py --report                                                          (measures only)
    uv run ... python -I waterworks_sounds.py --pick SluiceSlam=3,LakeBoat=2                                    (other takes)

승규 11-22: "코스 정원 사운드 없는 곳 확인해" then "1 2 3 4 해봐" (beats 1 .. 4; beat 1 already had all its sounds). The
takes come from the ElevenLabs flow "정원 비트 2 ~ 4 소리" (1uaf81nd0hcmIOSdTdDm), nine sfx nodes x 4 takes
(eleven_text_to_sound_v2), saved as sfx_takes/R767/<Kind>_<take>.mp3. Every prompt ends "No music, no voice":
  GeyserWarn   1.2 s, influence 0.6  "Water gurgling and bubbling up quickly through a metal floor grate just before a
                                      fountain jet fires: rising pressurized bubbles, a hollow glugging burble ..."
  GeyserFall   1.5 s, 0.6            "A tall column of fountain water suddenly collapsing: a heavy rush of water falling
                                      back down and pattering hard onto wet stone and a shallow basin ..."
  GeyserHit    1 s, 0.6              "A small wooden figure struck and thrown upward by a powerful jet of water: a hard wet
                                      water slap with a dull wooden knock under it and a short spray, punchy ..."
  WeirFall     8 s loop, 0.5         "A wide low garden weir: a long sheet of water pouring steadily over a stone edge and
                                      falling a little more than a metre into a pond, constant soft roar ..."
  DrumLand     1 s, 0.6              "A small figure jumping down onto a large hollow wooden barrel floating on water: one
                                      deep hollow wooden thump with the barrel bobbing and water sloshing ..."
  LakeBoat     8 s loop, 0.5         "A wooden boat gliding slowly across a calm lake: soft water lapping and parting at
                                      the bow, a gentle wake, now and then a quiet creak of the wooden hull ..."
  SluiceRise   1.5 s, 0.6            "A heavy wooden sluice gate hauled up quickly out of shallow running water by an iron
                                      chain: a fast chain rattle over a pulley, a wooden creak, water streaming ..."
  SluiceSlam   1.5 s, 0.6            "A heavy wooden sluice gate dropping fast and slamming down onto a stone floor under
                                      shallow water: a deep heavy wooden boom with a big wet splash ..."
  LockFlow     8 s loop, 0.5         "Shallow clear water flowing quickly over a smooth stone channel floor a few
                                      centimetres deep: a steady bright rushing trickle and gurgle ..."

Which take: plaza_sounds.py's checks (level, a bright "coin" ring, a voice), then a loop the steadiest (smallest spread
of its 100 ms loudness), a hit (GeyserHit, DrumLand, SluiceSlam) the punchiest (geyser_sounds.py: its loudest 30 ms
in the first 0.15 s against the rest), anything else the deepest (most under 250 Hz). Then, as plaza_sounds.py does:
  loops     rumble off, seam crossfaded over 0.4 s, levelled to RMS -22 dBFS (peak no higher than -2 dB)
  one-shots rumble off, started 3 ms before the onset, cut 50 dB under the peak, no longer than its cap (a fade over
            the last 0.12 s): the warning fits in the jet's 0.6 s warn, the fall in its last 0.25 s and after
  far       three 900 Hz low-passes in a 0.7 s outdoor tail, 3 dB under the near one
-> Assets/Resources/PawnRushGardenSfx/<Kind>.wav + <Kind>Far.wav (GardenGeyserSound, GardenFloatSound,
   GardenWaterworksSound). The time from a shot's start to its loudest 10 ms is printed: SluiceSlam's (0.031 s) is
   GardenWaterworksSound.SlamLead. GeyserFall stays level all through (its 50 ms loudness within 2 dB), so it starts as
   the column starts to fall (FallLead 0.05). SluiceRise is a chain rattle growing to a clunk at 0.9 .. 1.06 s: it starts
   RiseLead 0.3 s before the gate leaves the floor, so the clunk comes as it reaches the top (0.51 s up).
2026-11-22 picks: GeyserWarn 1, GeyserFall 3, GeyserHit 3, WeirFall 4, DrumLand 1, LakeBoat 4, SluiceRise 1,
SluiceSlam 4, LockFlow 4.

R768 (승규 11-22 "다 확인 했고 5 6 시작": beats 5 and 6), the flow "정원 비트 5 · 6 소리" (PHUoNyTlxNN5ykJEPuUS), eight
more sfx nodes x 4 takes in sfx_takes/R767/:
  RobotWind      1.2 s, 0.6   "A heavy iron ball on a thick wooden arm being slowly winched back by a big wooden axle: a
                               deep wooden creak and a low heavy chain tightening and grinding ..."
  RobotSwoosh    1.2 s, 0.6   "A huge heavy iron wrecking ball swinging fast past the listener on a pendulum arm: one deep
                               powerful low whoosh of air ..."
  RobotStop      1 s, 0.6     "A heavy swinging wooden arm with an iron ball jerking to a sudden stop at the end of its
                               swing: a dull heavy wooden clunk and a short low chain jolt ..."
  RobotHit       1 s, 0.6     "A huge heavy iron ball slamming into a small wooden figure and knocking it flying: one very
                               heavy deep dull thud with a wooden knock ..."
  ReservoirBoat  8 s loop     "A sturdy flat wooden boat moving steadily through a calm reservoir: water pushed and
                               splashing softly along the hull, a gurgling wake, the wooden planks creaking ..."
  DeckLand       1 s, 0.6     "A small wooden figure jumping down onto the wooden deck of a floating boat: one solid hollow
                               wooden deck thump, the boat rocking with a short water slosh ..."
  TunnelFall     8 s loop     "A curtain of water pouring steadily from a stone archway a few metres down into water inside
                               a short boathouse tunnel ..."
  SpillwayFall   8 s loop     "A wide garden spillway: a broad sheet of water pouring over a stone wall and falling three
                               and a half metres into a lake below, a full deep steady waterfall roar ..."
Picks: RobotWind 3, RobotSwoosh 4 (loudest at 0.194 s: GardenRobotSound.SwooshLead), RobotStop 4 (0.024 s: StopLead),
RobotHit 3, ReservoirBoat 1, DeckLand 3, TunnelFall 4, SpillwayFall 2. Beats 2 .. 4's files come out unchanged.
"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
_src = open(os.path.join(HERE, "plaza_sounds.py"), encoding="utf-8").read()
_src = _src[:_src.rstrip().rfind("main()")]
_ns = {"__file__": os.path.join(HERE, "plaza_sounds.py"), "__name__": "plaza_sounds"}
exec(compile(_src, "plaza_sounds.py", "exec"), _ns)
SR, decode, write, highpass, seamless, level, shot, far, db, measure_plaza = (_ns[k] for k in
    ("SR", "decode", "write", "highpass", "seamless", "level", "shot", "far", "db", "measure"))
fine = _ns["fine"]

TAKES = os.path.join(HERE, "sfx_takes", "R767")
_ns["TAKES"] = TAKES
OUT = _ns["OUT"]

# kind: (loop, choose by, cap in seconds for a shot or RMS dBFS for a loop - LOOP_RMS if None)
KINDS = {
    "GeyserWarn": (False, "low", .75),
    "GeyserFall": (False, "low", 1.2),
    "GeyserHit": (False, "punch", .8),
    "WeirFall": (True, "steady", None),
    "DrumLand": (False, "punch", .8),
    "LakeBoat": (True, "steady", None),
    "SluiceRise": (False, "low", 1.3),
    "SluiceSlam": (False, "punch", 1.3),
    "LockFlow": (True, "steady", None),
    # R768 (승규 11-22 "5 6 시작"): beats 5 and 6, appended so the kinds above come out as before (far's random tail)
    "RobotWind": (False, "low", 1.0),
    "RobotSwoosh": (False, "low", 1.2),
    "RobotStop": (False, "punch", .8),
    "RobotHit": (False, "punch", .8),
    "ReservoirBoat": (True, "steady", None),
    "DeckLand": (False, "punch", .8),
    "TunnelFall": (True, "steady", None),
    "SpillwayFall": (True, "steady", None),
    # R769 (승규 11-22 "정원 전체 분위기 소리 넣어줘" + the birds): the garden bed (birdsong is wanted here, so the ring check
    # only orders the takes) and the flocks' wings
    "GardenAmbience": (True, "steady", -28.0),   # a loop's third value: its RMS (chirps peak 26 dB over the bed)
    "WingFlutter": (True, "steady", None),
    # R771 (승규 11-22 "섬 분수만 하자"): the island fountain; only take 2 is long (16 s, the others 2 .. 3 s): forced below
    "IslandFountain": (True, "steady", None),
}
LOOP_RMS = -22.0


def env(x, ms):
    n = max(1, int(SR * ms / 1000))
    return np.sqrt(np.convolve(x ** 2, np.ones(n) / n, "same") + 1e-12)


def punch(x):
    o = int(np.argmax(np.abs(x) > .05 * np.abs(x).max()))
    e = env(x, 30)
    hit = e[o:o + int(.15 * SR)].max()
    rest = np.sqrt((x[o + int(.3 * SR):] ** 2).mean() + 1e-12)
    return db(hit) - db(rest)


def cap(x, seconds):
    n = int(seconds * SR)
    if len(x) <= n:
        return x
    x = x[:n].copy()
    fade = int(.12 * SR)
    x[-fade:] *= np.linspace(1, 0, fade) ** 2
    return x


def main():
    argv = sys.argv[1:]
    report = "--report" in argv
    # Every LakeBoat take holds a narrow ring a little over the check's 90 ms; take 4 the least (93 ms, voice 0.19).
    forced = {"LakeBoat": 4, "IslandFountain": 2}
    if "--pick" in argv:
        for p in argv[argv.index("--pick") + 1].split(","):
            k, v = p.split("=")
            forced[k] = int(v)
    for kind, (loop, by, most) in KINDS.items():
        ms = []
        for t in (1, 2, 3, 4):
            if os.path.exists(os.path.join(TAKES, f"{kind}_{t}.mp3")):
                m = measure_plaza(kind, t)
                m["punch"] = punch(m["x"])
                ms.append(m)
        for m in ms:
            print(f"{kind}_{m['take']}: rms {m['rms']:.1f} peak {m['peak']:.1f} spread {m['spread']:.2f} "
                  f"punch {m['punch']:.1f} dB ring {m['ring']:.3f} s voice {m['voice']:.2f} "
                  f"low<250 {m['low'] * 100:.0f}%{'' if fine(m) else '  (rejected)'}")
        ok = [m for m in ms if fine(m)] or ms
        if kind in forced:
            m = next(m for m in ms if m["take"] == forced[kind])
        elif by == "steady":
            m = min(ok, key=lambda m: m["spread"])
        elif by == "punch":
            m = max(ok, key=lambda m: m["punch"])
        else:
            m = max(ok, key=lambda m: m["low"])
        print(f"  -> {kind}: take {m['take']}")
        if report:
            continue
        x = m["x"]
        if loop:
            x = level(seamless(highpass(x, 30)), most if most is not None else LOOP_RMS)
        else:
            x = cap(shot(x), most)
            print(f"     {len(x) / SR:.2f} s, loudest 10 ms at {int(np.argmax(env(x, 10))) / SR:.3f} s")
        write(os.path.join(OUT, kind + ".wav"), x)
        far_x = far(x, loop)   # made even when not written: its random tail keeps the later kinds' files as they were
        if kind != "GardenAmbience":   # a 2D bed: no far layer
            write(os.path.join(OUT, kind + "Far.wav"), far_x)


main()

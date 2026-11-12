"""Mixes the course reveal's sound on its own (0 → 11.4 s) from a cue list, and draws the mix under the picture's events.
cues.json (exported from Unity) is used when given; otherwise the cues are worked out here from the same numbers."""
import json, math, os, sys, wave
import numpy as np
from PIL import Image, ImageDraw

here = os.path.dirname(os.path.abspath(__file__))
final = os.path.join(here, "final")
if not os.path.isdir(final):
    # in the repository (ArtSource/Loading/CourseReveal/Sound): the clips Unity plays
    final = os.path.normpath(os.path.join(here, "..", "..", "..", "..", "Assets", "Resources", "Loading", "CourseReveal", "Sound"))
SR = 44100
# The leads below are process.py's leads.json; the times are LoadingScreenView.Reveal.cs / RevealSound.cs's numbers.

RevealLand = [1.35, 2.75, 3.85, 5.65, 6.75, 8.55, 9.75]  # R564: .6 s rest after a mini-game
RollSeconds, StampAfter, StampFall = 1.1, .22, .12
RollSteps = [0, 14, 0, 15, 0, 14, 0]
Kinds = ["obstacle", "game", "obstacle", "game", "obstacle", "game", "final"]
StampAt = RevealLand[-1] + StampAfter
Hit = StampAt + StampFall
GlintMiddle = .2 + .45 * (145 - 55 + 120) / 420
MedalFlat = -.02 + (.34 + .02) / (1.6 + 1)


def reel_left(u, steps, slow=3, split=.55):
    u = min(max(u, 0), 1)
    if steps <= 0:
        return 0
    if steps <= slow:
        return steps * (1 - u) ** 3
    if u < split:
        return steps - (steps - slow) * (u / split)
    k = (steps - slow) * (1 - split) / (split * slow)
    return slow * (1 - (u - split) / (1 - split)) ** k


def reaches(left, steps):
    lo, hi = 0.0, 1.0
    for _ in range(40):
        mid = (lo + hi) / 2
        if reel_left(mid, steps) > left:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def own_cues():
    cues = []

    def add(clip, hit, lead, pitch=1.0, gain=1.0, kind=""):
        cues.append(dict(clip=clip, hit=hit, start=hit - lead / pitch, pitch=pitch, gain=gain, kind=kind))
    add("RevealOpen", 0, 0)
    ob = ga = 0
    for slot, kind in enumerate(Kinds):
        land = RevealLand[slot]
        if kind == "final":
            add("RevealCrown", land, .002, kind="crown")
        elif kind == "game":
            start = land - RollSeconds
            add("RevealCover", start - .05, .002, kind="cover")
            steps = RollSteps[slot]
            for k in range(steps - 1, 0, -1):
                u = reaches(k, steps)
                add("RevealTick%d" % (k % 4), start + u * RollSeconds, .002, 1 + .03 * ((k * 7) % 3 - 1), 1.0 if k < 3 else .55, kind="tick")
            add("RevealReelStop", land, .002, 1 + .04 * ga, kind="reelstop")
            ga += 1
        else:
            add("RevealFlip", land + MedalFlat, .1, 1 + .05 * ob, kind="flip")
            ob += 1
    add("RevealRiser", Hit, .3, gain=.8, kind="riser")
    add("RevealStamp", Hit, .002, kind="stamp")
    add("RevealBurst", Hit + .05, .05, kind="burst")
    add("RevealGlint", Hit + GlintMiddle, .108, kind="glint")
    return sorted(cues, key=lambda c: c["start"])


def load(name):
    with wave.open(os.path.join(final, name + ".wav")) as w:
        return np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32767


cues = json.load(open(sys.argv[1])) if len(sys.argv) > 1 else own_cues()
total = 11.4
mix = np.zeros(int(total * SR))
for c in cues:
    a = load(c["clip"])
    if abs(c["pitch"] - 1) > 1e-6:
        n = int(len(a) / c["pitch"])
        a = np.interp(np.arange(n) * c["pitch"], np.arange(len(a)), a)
    s = int(round(c["start"] * SR))
    e = min(len(mix), s + len(a))
    mix[s:e] += a[:e - s] * c["gain"]
peak = np.abs(mix).max()
print("cues", len(cues), "mix peak dB", round(20 * math.log10(peak), 1))
if peak > .98:
    mix *= .98 / peak
with wave.open(os.path.join(here, "reveal_sound_preview.wav"), "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((mix * 32767).astype("<i2").tobytes())

# picture: the mix's envelope with every picture event as a line, and each cue's hit as a dot
W, H = 2000, 360
img = Image.new("RGB", (W, H + 60), (16, 14, 12))
d = ImageDraw.Draw(img)
px = lambda t: int(t / total * W)
step = len(mix) // W
for x in range(W):
    seg = np.abs(mix[x * step:(x + 1) * step])
    v = seg.max() if len(seg) else 0
    d.line([(x, H // 2 - v * H // 2), (x, H // 2 + v * H // 2)], fill=(110, 200, 240))
events = [(0.45, "dark gone")] + [(t, k) for t, k in zip(RevealLand, Kinds)] + [(StampAt, "drop"), (Hit, "STAMP")]
events += [(t + MedalFlat, "flat") for t, k in zip(RevealLand, Kinds) if k == "obstacle"]
for t, label in events:
    d.line([(px(t), 0), (px(t), H)], fill=(230, 160, 60))
    d.text((px(t) + 3, H + 4), label, fill=(240, 200, 120))
for c in cues:
    d.ellipse([px(c["hit"]) - 2, H - 8, px(c["hit"]) + 2, H - 4], fill=(255, 255, 255))
for s in range(11):
    d.text((px(s), H + 40), "%ds" % s, fill=(150, 150, 150))
img.save(os.path.join(here, "reveal_sound_timeline.png"))
json.dump(cues, open(os.path.join(here, "cues_py.json"), "w"), indent=0)
print("ticks", sum(1 for c in cues if c.get("kind") == "tick" or "Tick" in c["clip"]))

"""R583: builds the course reveal C's sound clips (mono 44.1 kHz WAV) from the chosen ElevenLabs takes in raw/ and writes
each clip's 'hit' offset (seconds from the clip's start to the moment that must meet the picture) to leads.json.

  uv run --no-project --with miniaudio --with numpy --with scipy python -I process.py [out folder]

Default out folder: Assets/Resources/Loading/CourseReveal/Sound. The takes come from the ElevenLabs flow
"R583 코스 공개 매칭 UI 소리" (game UI matchmaking style, prompts in prompts.md); sessions.json maps take names to their
generation sessions. 승규 님 asked for game-UI matching sounds after the code-made set: "둔하고 먹먹함 · 소리 종류가 안 맞음 ·
너무 싸 보임", then "일레븐 랩스로 쫀득하게 만들어봐"."""
import json, os, sys, wave
import numpy as np
import miniaudio
from scipy import signal

here = os.path.dirname(os.path.abspath(__file__))
raw = os.path.join(here, "raw")
root = os.path.abspath(os.path.join(here, "..", "..", "..", ".."))
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, "Assets", "Resources", "Loading", "CourseReveal", "Sound")
os.makedirs(out, exist_ok=True)
SR = 44100
PRE = .002  # kept before every transient


def load(name):
    d = miniaudio.mp3_read_file_f32(os.path.join(raw, name + ".mp3"))
    a = np.frombuffer(d.samples, dtype=np.float32).reshape(-1, d.nchannels).mean(axis=1).astype(np.float64)
    assert d.sample_rate == SR, d.sample_rate
    # no rumble under 30 Hz (nothing a speaker plays, only eats headroom)
    b, a2 = signal.butter(2, 30 / (SR / 2), "highpass")
    return signal.lfilter(b, a2, a)


def onset(a, frac=.2):
    env = np.abs(a)
    pk = env.max()
    i = int(np.argmax(env > pk * frac))
    j = i
    while j > 0 and i - j < int(.004 * SR) and env[j - 1] > pk * .02:
        j -= 1
    return j


def peak_at(a):
    return int(np.argmax(np.abs(a)))


def cut(a, s, length):
    s = max(0, s)
    n = int(length * SR)
    seg = a[s:s + n]
    return np.pad(seg, (0, n - len(seg))) if len(seg) < n else seg.copy()


def fade(a, out_from, curve=2.0, fade_in=0.0):
    a = a.copy()
    n = len(a)
    if fade_in > 0:
        k = int(fade_in * SR)
        a[:k] *= np.linspace(0, 1, k)
    k0 = int(out_from * SR)
    if k0 < n:
        a[k0:] *= (1 - np.linspace(0, 1, n - k0)) ** curve
    k = min(int(.003 * SR), n)
    a[n - k:] *= np.linspace(1, 0, k)
    return a


def norm(a, peak_db):
    return a / np.abs(a).max() * 10 ** (peak_db / 20)


def soft(a, ceil=.93, drive=1.2):
    return ceil * np.tanh(drive * a / ceil)


def save(name, a):
    a = np.clip(a, -1, 1)
    with wave.open(os.path.join(out, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((a * 32767).astype("<i2").tobytes())


def from_onset(take, length, out_from, peak_db, curve=2.0):
    """A clip whose transient is its hit, kept PRE before it."""
    a = load(take)
    s = onset(a) - int(PRE * SR)
    save_as = norm(fade(cut(a, s, length), out_from, curve), peak_db)
    return save_as, PRE


def lead_in(take, before, length, out_from, peak_db, curve=2.0):
    """A clip whose loudest moment is its hit, with up to `before` seconds of what leads into it."""
    a = load(take)
    p = peak_at(a)
    s = max(0, p - int(before * SR))
    clip = norm(fade(cut(a, s, length), out_from, curve, fade_in=.004 if s > 0 else 0), peak_db)
    return clip, (p - s) / SR


leads = {}

# 0 s, the screen darkens: a deep whoosh in with a crack (take open_2, its transient on the dark's start)
c, leads["RevealOpen"] = from_onset("open_2", .85, .35, -1)
save("RevealOpen", c)

# the title swipes in: a quick swipe, its loudest point as the title first reaches its place (title_1)
c, leads["RevealTitle"] = lead_in("title_1", .06, .2, .08, -3)
save("RevealTitle", c)

# each panel lands: swoosh into a hit (slam_3), the hit on the landing
c, leads["RevealSlam"] = lead_in("slam_3", .08, .42, .14, -1)
save("RevealSlam", c)

# each picture cut: three different crisp UI ticks, used in turn so a run of cuts never machine-guns one sample
for n, take in enumerate(("tick_2", "tick_4", "tick_1")):
    c, leads["RevealTick"] = from_onset(take, .085, .03, -1, 3)
    save("RevealTick%d" % n, c)

# each stop: the lock-in. 승규 님 found lock_1 (mostly a low thump) lacking and picked option B from the lock
# candidates page: a heavy steel bolt clunk (lockB_2). Peak -2 dB is the level he heard it at on the page.
c, leads["RevealLock"] = from_onset("lockB_2", .4, .12, -2)
save("RevealLock", c)

# an obstacle's or the final's square fills: a soft short tap (dot_4)
c, leads["RevealDot"] = from_onset("dot_4", .08, .03, -1, 3)
save("RevealDot", c)

# the countdown plate drops in: a short whoosh onto a thud (countin_3), the thud as the plate appears
c, leads["RevealCountIn"] = lead_in("countin_3", .045, .26, .1, -1)
save("RevealCountIn", c)

# R594 countdown C, set D from the countdown sound page (asked for "긴박한 분위기 속 매칭같은 분위기", then "D로 하자"):
# the count_1 / countlast_1 set before it is still in raw/ (R583).
# 3 and 2: a heavy steel lever clunk (cdHitClunk_2)
c, leads["RevealCount"] = from_onset("cdHitClunk_2", .45, .15, -1)
save("RevealCount", c)

# 1: a deep boom with a crack on top (cdFinal_1)
c, leads["RevealCountLast"] = from_onset("cdFinal_1", 1.1, .4, -1)
save("RevealCountLast", c)

# under the countdown: a dark low drone throbbing fast (cdPulse_1 from 0.3 s), from the number's first showing to the
# 1, cut dead on it. Its hit is its end; the code lines that end up with the 1. Its length is the countdown's, from
# LoadingScreenView.Reveal.cs: CountAt[2] - (CountAt[0] - CountIn). R596 slowed the numbers to 1.0 s apart
# (6.6 / 7.6 / 8.6, CountIn .25): 2.25 s (was 1.85 s for 6.2 / 7.0 / 7.8).
COUNT_AT, COUNT_IN = (10.2, 11.2, 12.2), .25  # R597 moved the countdown 1.2 s later, R611 0.9 s, R613 1.5 s more (same length)
bed = round(COUNT_AT[-1] - (COUNT_AT[0] - COUNT_IN), 3)
a = load("cdPulse_1")
c = norm(fade(cut(a, int(.3 * SR), bed), bed - .01, 1.0, fade_in=.15), -1)
save("RevealCountBed", c)
leads["RevealCountBed"] = bed

json.dump(leads, open(os.path.join(here, "leads.json"), "w"), indent=1)
print(json.dumps(leads, indent=1))

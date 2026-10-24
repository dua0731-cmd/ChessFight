"""Rook chain SFX (R153): pick a take per moment, make it heavy (low-pass, 3 semitones down), cut the silence in front,
set the level, export mono 44.1 kHz mp3 for Unity. Run check.py on out/ afterwards."""
import os, subprocess, sys
import numpy as np
import imageio_ffmpeg
FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 48000
OUT = "C:/tmp_sfx/out"
os.makedirs(OUT, exist_ok=True)

# moment: (take, target rms dB, low-pass Hz, pitch factor, loop)
PICK = {
    "ChainThrow": ("ChainThrow_2", -19, 2600, 0.84, False),
    "ChainRun": ("ChainRun_1", -24, 2200, 0.80, True),
    "ChainBite": ("ChainBite_2", -16, 2600, 0.84, False),
    "ChainPull": ("ChainPull_2", -18, 2600, 0.87, False),
    "ChainOffer": ("ChainOffer_2", -24, 1300, 0.84, False),
    "ChainGrab": ("ChainGrab_1", -18, 2600, 0.84, False),
    "ChainLand": ("ChainLand_2", -18, 1700, 0.89, False),
    "ChainRewind": ("ChainRewind_1", -21, 1900, 0.84, False),
    "ChainBreak": ("ChainBreak_2", -18, 1600, 0.87, False),
    "SkillFail": ("SkillFail_2", -20, 3000, 1.0, False),
}
only = sys.argv[1:]


def run_filter(src, lp, pitch):
    af = f"lowpass=f={lp},lowpass=f={lp},highpass=f=40,asetrate={int(SR * pitch)},aresample={SR}"
    raw = subprocess.run([FF, "-v", "quiet", "-i", src, "-ac", "1", "-ar", str(SR), "-af", af, "-f", "f32le", "-"], capture_output=True).stdout
    return np.frombuffer(raw, np.float32).copy()


def trim_front(x, floor_db=-22):
    env = np.abs(x)
    thr = env.max() * 10 ** (floor_db / 20)
    idx = np.argmax(env > thr)
    start = max(0, idx - int(0.012 * SR))
    x = x[start:]
    # fade the tail to silence
    n = min(len(x), int(0.06 * SR))
    x[-n:] *= np.linspace(1, 0, n)
    return x


def seamless(x):
    n = int(0.25 * SR)
    head, tail = x[:n].copy(), x[-n:].copy()
    ramp = np.linspace(0, 1, n)
    body = x[n:-n] if len(x) > 2 * n else x
    mixed = tail * (1 - ramp) + head * ramp
    return np.concatenate([body, mixed])


for name, (take, rms_db, lp, pitch, loop) in PICK.items():
    if only and name not in only:
        continue
    src = f"C:/tmp_sfx/{take}.mp3"
    if not os.path.exists(src):
        print("missing", src)
        continue
    x = run_filter(src, lp, pitch)
    x = seamless(x) if loop else trim_front(x)
    rms = np.sqrt((x ** 2).mean()) + 1e-9
    x = x * (10 ** (rms_db / 20) / rms)
    peak = np.abs(x).max()
    if peak > 0.89:
        x *= 0.89 / peak
    wav = subprocess.run([FF, "-v", "quiet", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-ar", "44100", "-ac", "1",
                          "-b:a", "128k", f"{OUT}/{name}.mp3"], input=x.astype(np.float32).tobytes())
    print("wrote", name, f"{len(x) / SR:.2f}s")

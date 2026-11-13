# The garden's splash sound (R442; three takes R444): a piece going into the water, synthesized from what makes that
# sound.
#
#   the slap    first contact, water hitting water: a broadband burst (noise kept to 0.5 .. 6 kHz), 2 ms rise,
#               35 ms decay, and the body's low thud (90 Hz, 50 ms)
#   the plunk   the air cavity behind the piece pinches off a big bubble, which rings at its Minnaert frequency
#               f0 = (1 / 2 pi R) sqrt(3 gamma p0 / rho) ~= 3.26 / R Hz (R in metres): R 12 .. 17 mm -> 270 .. 190 Hz,
#               80 ms after the slap; a second, about 1.4 R, when the cavity's last of the air goes
#   bubbles     the air it drags under breaks into bubbles of 0.7 .. 6 mm (log-uniform), each a damped sine at its
#               Minnaert frequency rising a little as it rises (van den Doel: f(t) = f0 (1 + sigma d t), sigma 0.1,
#               damping d = 0.043 f0 + 0.0014 f0^1.5 per second), louder the bigger, most within a quarter second
#   the patter  the crown's and the jet's drops falling back over 0.25 .. 1.0 s: little clicks and tiny bubbles
# R444 ("물 튀기는 걸 좀 더 무작위로 자연스럽게"): three takes (GardenSplash, 2, 3), each its own draw of bubbles and drops,
# its own cavity bubble and bubble count, and a little more or less slap; GardenSplashFx picks one at random.
# 44.1 kHz, mono, 16 bit, 1.3 s, seeded (the same files every run).
#
#   "<Blender 5.2>/5.2/python/bin/python.exe" splash_sound.py
# Out: Assets/Resources/PawnRushGardenSfx/GardenSplash.wav, GardenSplash2.wav, GardenSplash3.wav (+ .meta, the folder's)
import hashlib
import os
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
FOLDER = "Assets/Resources/PawnRushGardenSfx"
SR, DUR = 44100, 1.3
# name, seed, the cavity bubble's radius (m), how many entrained bubbles, the slap's level
TAKES = (("GardenSplash", 442, .014, 55, .75), ("GardenSplash2", 443, .012, 64, .82), ("GardenSplash3", 444, .017, 46, .68))


def take(seed, plunk, count, slap):
    rng = np.random.default_rng(seed)
    out = np.zeros(int(SR * DUR))

    def add(start, signal):
        i0 = int(start * SR)
        n = min(len(signal), len(out) - i0)
        if n > 0:
            out[i0:i0 + n] += signal[:n]

    def band_noise(n, lo, hi):
        x = rng.standard_normal(n)
        spec = np.fft.rfft(x)
        f = np.fft.rfftfreq(n, 1 / SR)
        spec[(f < lo) | (f > hi)] = 0
        y = np.fft.irfft(spec, n)
        return y / (np.abs(y).max() + 1e-9)

    def bubble(start, radius, amp, sigma=.1):
        f0 = 3.26 / radius
        d = .043 * f0 + .0014 * f0 ** 1.5
        n = int(min(DUR - start, 7 / d) * SR)
        if n <= 0:
            return
        tt = np.arange(n) / SR
        phase = 2 * np.pi * f0 * (tt + .5 * sigma * d * tt * tt)      # the integral of f0 (1 + sigma d t)
        s = amp * np.sin(phase) * np.exp(-d * tt)
        k = min(44, n)
        s[:k] *= np.linspace(0, 1, k)                                 # no click at its start
        add(start, s)

    # The slap and the thud.
    n = int(.15 * SR)
    tt = np.arange(n) / SR
    add(.005, slap * band_noise(n, 500, 6000) * (1 - np.exp(-tt / .002)) * np.exp(-tt / .035))
    add(.005, .45 * np.sin(2 * np.pi * float(rng.uniform(80, 100)) * tt) * (1 - np.exp(-tt / .003)) * np.exp(-tt / .05))
    # The plunk: the cavity's big bubbles.
    bubble(.07 + float(rng.uniform(0, .025)), plunk, .9, .12)
    bubble(.14 + float(rng.uniform(0, .03)), plunk * float(rng.uniform(1.3, 1.5)), .45, .1)
    # The entrained bubbles.
    for _ in range(count):
        radius = float(np.exp(rng.uniform(np.log(.0007), np.log(.006))))
        start = .02 + min(float(rng.exponential(.09)), .6)
        bubble(start, radius, .26 * (radius / .003) ** .8 * float(rng.uniform(.5, 1.0)))
    # The patter of drops falling back.
    for _ in range(30):
        start = .25 + float(rng.gamma(2.2, .14))
        if start > DUR - .05:
            continue
        m = int(.004 * SR)
        add(start, .12 * float(rng.uniform(.5, 1)) * band_noise(m, 2000, 8000) * np.exp(-np.arange(m) / SR / .0012))
        if rng.random() < .55:
            bubble(start + .002, float(rng.uniform(.0008, .002)), .1 * float(rng.uniform(.5, 1)))
    # Soft-clipped, to -1 dB, a 60 ms fade at the end.
    out = np.tanh(out * 1.2)
    out *= .89 / (np.abs(out).max() + 1e-9)
    fade = int(.06 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    return (out * 32767).astype(np.int16)


def guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def meta(rel, body):
    p = os.path.join(ROOT, rel + ".meta")
    if not os.path.exists(p):
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write("fileFormatVersion: 2\nguid: %s\n%s" % (guid(rel), body))


AUDIO = """AudioImporter:
  externalObjects: {}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""

os.makedirs(os.path.join(ROOT, FOLDER), exist_ok=True)
meta(FOLDER, "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
for name, seed, plunk, count, slap in TAKES:
    rel = "%s/%s.wav" % (FOLDER, name)
    pcm = take(seed, plunk, count, slap)
    with wave.open(os.path.join(ROOT, rel), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    meta(rel, AUDIO)
    print("SPLASH WAV", rel, "plunk %.0f Hz" % (3.26 / plunk), "bubbles", count)

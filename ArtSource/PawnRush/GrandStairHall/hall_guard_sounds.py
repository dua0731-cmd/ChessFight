"""Pawn Rush grand stair hall (대계단 홀): the royal balcony's guard knights' sounds (R564).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I hall_guard_sounds.py

승규 님 (11-10): "B1으로 해줘" - the R563 mockup set B "전투마", take 1 (https://claude.ai/artifact/4GPPKdmv42yg2M9qJtBXJD;
ElevenLabs flow Lkkgpgt3VEdGTwt37YZY, eleven_text_to_sound_v2, influence .55, sfx_takes/guardknight/).
The knight (KnightCavalryCharge): warning 1.3 s, leap 1.0 s, landing 0.12 s, windup 0.6 s, charge 1.1 s.
  leap    BL1 ("A heavy war horse leaps down from a high balcony: a strong horse snort and hoof push-off, a whoosh through
          the air, then four heavy hooves slamming down onto a stone floor with a deep thud ... no metal armor clank, no
          neigh"), 2.2 s: its landing (the biggest rise in loudness after 0.4 s) put at LEAP = 1.0 s (silence in front if
          it came sooner), 0.8 s after it kept, 7 kHz lowpass, faded over 0.3 s, peak -1 dB. Starts as the leap does.
  charge  BC1 ("A heavy war horse paws the stone floor once and snorts, then bursts into a fast short gallop across a
          marble hall, heavy hooves thundering past and skidding to a stop ..."), 1.76 s, 7 kHz lowpass, faded over
          0.25 s, peak -1 dB. Starts as the windup does.
Dry and mono 44.1 kHz: the hall's ring is added in Unity (CavalryWarnFx.Sound.cs, RollingSpawner.Ring).
-> Assets/Resources/PawnRushHallSfx/HallGuardLeap.wav, HallGuardCharge.wav
"""
import os
import wave

import miniaudio
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TAKES = os.path.join(HERE, "sfx_takes", "guardknight")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushHallSfx"))
SR = 44100
LEAP = 1.0


def load(name):
    a = miniaudio.decode_file(os.path.join(TAKES, name + ".mp3"), output_format=miniaudio.SampleFormat.FLOAT32,
                              nchannels=1, sample_rate=SR)
    x = np.frombuffer(a.samples, dtype=np.float32).astype(np.float64)
    return x - x.mean()


def lowpass(x, hz):
    k = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    p = 0.0
    for i, v in enumerate(x):
        p = (1 - k) * v + k * p
        y[i] = p
    return y


def hit(x):
    # the landing: the biggest jump in loudness after 0.4 s, in 20 ms frames (as the mockup page placed it)
    n = int(.02 * SR)
    e = np.array([np.sqrt((x[i:i + n] ** 2).mean()) for i in range(0, len(x) - n, n)])
    db = 20 * np.log10(e + 1e-9)
    rise = db[1:] - db[:-1]
    start = int(.4 / .02)
    k = start + int(np.argmax(rise[start:] + .25 * db[start + 1:]))
    return (k + 1) * n


def finish(x, fade, db):
    x = lowpass(x, 7000)
    n = min(len(x), int(.004 * SR))
    x[:n] *= np.linspace(0, 1, n)
    n = min(len(x), int(fade * SR))
    x[-n:] *= np.cos(np.linspace(0, np.pi / 2, n)) ** 2
    return x * (10 ** (db / 20) / (np.abs(x).max() + 1e-12))


def write(name, x):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".wav")
    w = wave.open(path, "wb")
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    w.close()
    print("WROTE", path, round(len(x) / SR, 3), "s")


def main():
    leap = load("BL1")
    h = hit(leap)
    lead = int(LEAP * SR)
    front = leap[max(0, h - lead):h]
    body = np.concatenate([np.zeros(lead - len(front)), front, leap[h:h + int(.8 * SR)]])
    print("BL1 landing at", round(h / SR, 3), "s in the take")
    write("HallGuardLeap", finish(body, .3, -1))
    write("HallGuardCharge", finish(load("BC1"), .25, -1))


if __name__ == "__main__":
    main()

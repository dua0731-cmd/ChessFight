"""Pawn Rush grand stair hall (대계단 홀) beat 2: the falling chandeliers' sounds (R533).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I hall_chandelier_sounds.py

승규 님 (11-09): picked "A 째깍 원" from the chandelier warning mockups ("A로 해주고 샹들리에 깨지는 소리도 넣어").
  warning  synthesized on the floor circle's clock (VfxGroundWarn: lead = warning 1.2 + fall 0.4 = 1.6 s, 24 rim ticks
           lit clockwise, the last 0.35 s blinking 7 a second, cos(age * 7 * 2pi) peaks at 1.286 / 1.429 / 1.571 s):
           a soft tick every 0.2 s (every 3 rim ticks) rising a quarter tone at a time, then a clearer 523 Hz beep on
           each blink. Lowpassed at 3.5 kHz, peak -3 dB, 1.6 s (the mockup page's A, louder).
  break    ElevenLabs (flow Lkkgpgt3VEdGTwt37YZY, eleven_text_to_sound_v2, 1.6 s, influence .5, three takes):
           "Large chandelier falling and smashing on a stone floor, deep heavy thud with a burst of breaking glass, short
           debris tail, punchy video game impact, no voice" - take 3 (the heaviest start, 28 % under 200 Hz; the
           "Heavy brass crystal chandelier crashing..." takes were all glass, 1-3 % low). Cut at 1.35 s (the pooled
           chandelier lives 1.5 s after it lands), faded over 0.3 s, peak -1 dB.
Dry and mono 44.1 kHz: the hall's ring is added in Unity (ChandelierRain.Sound.cs).
-> Assets/Resources/PawnRushHallSfx/HallChandelierWarn.wav, HallChandelierBreak.wav
R560: the warning is now take A "나무 시계 째깍" (hall_warn_wood.py); the text above tells how it was before.
"""
import os
import sys
import wave

import miniaudio
import numpy as np

HERE_ = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE_)   # -I leaves this folder off the path
import hall_warn_wood  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
TAKES = os.path.join(HERE, "sfx_takes")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushHallSfx"))
SR = 44100
LEAD, URGENT, BLINK = 1.6, .35, 7.0


def t(sec):
    return np.arange(int(sec * SR)) / SR


def lowpass(x, hz):
    k = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    p = 0.0
    for i, v in enumerate(x):
        p = (1 - k) * v + k * p
        y[i] = p
    return y


def env(n, a=.003, r=.04):
    e = np.ones(n)
    na, nr = int(a * SR), min(n, int(r * SR))
    e[:na] = np.linspace(0, 1, na)
    e[-nr:] *= np.linspace(1, 0, nr) ** 2
    return e


def tone(f, sec, harm=(1, .35, .1)):
    tt = t(sec)
    return sum(g * np.sin(2 * np.pi * f * (i + 1) * tt) for i, g in enumerate(harm)) * env(len(tt))


def place(buf, x, at):
    i = int(at * SR)
    buf[i:i + len(x)] += x[:max(0, len(buf) - i)]


def blink_times():
    out, k = [], 0
    while k / BLINK < LEAD - .02:
        if k / BLINK >= LEAD - URGENT:
            out.append(k / BLINK)
        k += 1
    return out


def rms_to(x, db):
    act = x[np.abs(x) > np.abs(x).max() * .05]
    x = x * (10 ** (db / 20) / (np.sqrt((act ** 2).mean()) + 1e-12))
    pk, c = np.abs(x).max(), 10 ** (-1 / 20)
    return x * (c / pk) if pk > c else x


def peak_to(x, db):
    return x * (10 ** (db / 20) / (np.abs(x).max() + 1e-12))


def fade_out(x, seconds):
    n = min(len(x), int(seconds * SR))
    x = x.copy()
    x[-n:] *= np.cos(np.linspace(0, np.pi / 2, n)) ** 2
    return x


def load(name):
    a = miniaudio.decode_file(os.path.join(TAKES, name + ".mp3"), output_format=miniaudio.SampleFormat.FLOAT32,
                              nchannels=1, sample_rate=SR)
    return np.frombuffer(a.samples, dtype=np.float32).astype(np.float64)


def write(name, x):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".wav")
    w = wave.open(path, "wb")
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    w.close()
    print("WROTE", path, round(len(x) / SR, 3), "s", "peak", round(20 * np.log10(np.abs(x).max()), 1), "dB")


def main():
    # R560 (승규 "샹들리에 기사 A로"): take A "나무 시계 째깍" on the same clock, hall_warn_wood.py.
    write("HallChandelierWarn", hall_warn_wood.warnings()[0])

    crash = load("ChandelierSmash_3")[:int(1.35 * SR)]
    write("HallChandelierBreak", peak_to(fade_out(crash, .3), -1))


if __name__ == "__main__":
    main()

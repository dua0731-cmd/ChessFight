"""Pawn Rush grand stair hall (대계단 홀) wind way: the royal fans' sounds (R537, R552).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I hall_fan_sounds.py

승규 님 (11-09): "바람 소리 시작". The fans run on their vent (AirVent: idle 3 s, warning 0.8 s, blowing 3 s).
ElevenLabs (flow Lkkgpgt3VEdGTwt37YZY, eleven_text_to_sound_v2, influence .6, two takes each):
  hum   3 s, loop: "A huge old industrial ventilation fan with big wooden-and-iron blades turning, steady deep low
        whirring hum and rhythmic blade whoosh, constant, seamless loop. No voice, no music, no squeal, no beeping, no
        bright ringing." - take 2 (a blade's pulse about every 0.55 s; RoyalFanSpin.Sound.cs raises its pitch with the
        rotor's speed, so the pulse quickens as it spins up). Lowpassed at 3 kHz.
  wind  3 s, loop: "Strong powerful gale of wind blasting out of a giant fan, heavy steady rushing air, deep and full,
        constant, seamless loop. No voice, no music, no whistle, no howling tones, no ringing." - take 1. Lowpassed at
        2.5 kHz so it is a deep rush, not a hiss.
  gust  1.2 s: "A sudden heavy blast of air bursting out of a giant fan, one deep powerful whomp of wind pressure then
        rushing air, punchy video game wind burst. No voice, no music, no whistle, no ringing, no impact." - take 1
        (it dies away on its own; take 2 stopped dead at 0.33 s). Lowpassed at 4 kHz, cut at 0.9 s, faded 0.3 s.
The loops are made seamless by folding their last 0.25 s over their start (equal-power crossfade). Loops peak -3 dB,
the gust -1 dB. Dry and mono 44.1 kHz: the hall's ring is added in Unity (RollingSpawner.Ring).
-> Assets/Resources/PawnRushHallSfx/HallFanHum.wav, HallFanWind.wav, HallFanGust.wav
"""
import os
import wave

import miniaudio
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TAKES = os.path.join(HERE, "sfx_takes")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushHallSfx"))
SR = 44100


def load(name):
    a = miniaudio.decode_file(os.path.join(TAKES, name + ".mp3"), output_format=miniaudio.SampleFormat.FLOAT32,
                              nchannels=1, sample_rate=SR)
    return np.frombuffer(a.samples, dtype=np.float32).astype(np.float64)


def lowpass(x, hz):
    # two one-pole passes: a gentle 12 dB/octave roll-off
    k = np.exp(-2 * np.pi * hz / SR)
    for _ in range(2):
        y = np.empty_like(x)
        p = 0.0
        for i, v in enumerate(x):
            p = (1 - k) * v + k * p
            y[i] = p
        x = y
    return x


def seamless(x, seconds=.25):
    n = int(seconds * SR)
    head, body, tail = x[:n], x[n:-n], x[-n:]
    a = np.linspace(0, np.pi / 2, n)
    # the tail fades out over the head fading in; the loop then runs body -> (tail x head) -> body
    return np.concatenate([tail * np.cos(a) + head * np.sin(a), body])


def fade_out(x, seconds):
    n = min(len(x), int(seconds * SR))
    x = x.copy()
    x[-n:] *= np.cos(np.linspace(0, np.pi / 2, n)) ** 2
    return x


def peak_to(x, db):
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
    print("WROTE", path, round(len(x) / SR, 3), "s", "peak", round(20 * np.log10(np.abs(x).max()), 1), "dB")


def main():
    # R552 (승규 11-10 "이 소리 너무 바람 소리 같아" -> mockup C "묵직한 로터"): the hum is now a machine - a heavy rotor
    # chopping inside its box over a low motor (FanRotor_1: "A huge heavy rotor spinning fast inside a metal box, a deep
    # rhythmic thumping chop of big blades \"wub wub wub\" with a low motor rumble underneath, mechanical and powerful,
    # constant, seamless loop. No wind hiss, no voice, no ringing." 3 s, influence .65), at -18 dBFS RMS as on the
    # mockup page. The wind and the gust files stay; RoyalFanSpin.Sound.cs keeps the wind low and the gust silent.
    hum = load("FanRotor_1")
    hum = seamless(hum - hum.mean())
    write("HallFanHum", hum * (10 ** (-18 / 20) / np.sqrt((hum ** 2).mean())))
    write("HallFanWind", peak_to(seamless(lowpass(load("FanWind_1"), 2500)), -3))
    gust = lowpass(load("FanGust_1"), 4000)[:int(.9 * SR)]
    write("HallFanGust", peak_to(fade_out(gust, .3), -1))


if __name__ == "__main__":
    main()

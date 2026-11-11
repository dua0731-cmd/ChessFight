"""Pawn Rush grand stair hall (대계단 홀): the floating pads' landing sound (R558).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I hall_pad_sounds.py

승규 님 (11-10, R557 mockup https://claude.ai/artifact/WQtyGmDtoFdzfwGR8YaR3c): "착지는 C로 해주고 너무 탁이고 저기 구간은
기물이 뛰고 착지를 하면 살짝 내려가는 연출을 넣어줘, 움직이는 발판이 내는 소리는 지금 별로야".
  land  candidate C "체스판 딱" - ElevenLabs (flow Lkkgpgt3VEdGTwt37YZY, eleven_text_to_sound_v2, 0.6 s, influence .6):
        "A heavy wooden chess piece set down hard on a chessboard, a deep solid wooden knock, close and dry, one hit. No
        clink, no ringing, no voice." take 1 (sfx_takes/pads/LandC1 = sfx_takes/PadLandChess_1), made less sharp ("너무
        탁"): its click softened (lowpass 2.2 kHz, an 8 ms rise instead of the take's instant one) and a little low
        wooden body under it (a 120 Hz knock, 0.09 s) so it reads as a weighty piece settling on the pad, not a clack.
        From 3 ms before its hit, 0.55 s, faded 0.2 s, peak -2 dB.
No sound for the bobbing itself (승규: "움직이는 발판이 내는 소리는 지금 별로야").
Dry and mono 44.1 kHz: the hall's ring is added in Unity (RollingSpawner.Ring).
-> Assets/Resources/PawnRushHallSfx/HallPadLand.wav (FloatingPadFx)
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
    x = np.frombuffer(a.samples, dtype=np.float32).astype(np.float64)
    return x - x.mean()


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


def knock(hz, decay, seconds):
    t = np.arange(int(seconds * SR)) / SR
    f = hz * (1 + .4 * np.exp(-t / .02))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / decay) * np.minimum(1, t / .004)


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
    x = peak_to(lowpass(load("PadLandChess_1"), 2200), 0)
    hit = int(np.argmax(np.abs(x)))
    x = x[max(0, hit - int(.003 * SR)):][:int(.55 * SR)]
    body = knock(120, .09, .4)
    x[:len(body)] += .3 * body[:len(x)]
    rise = int(.008 * SR)
    x[:rise] *= np.linspace(0, 1, rise) ** 2
    fade = int(.2 * SR)
    x[-fade:] *= np.cos(np.linspace(0, np.pi / 2, fade)) ** 2
    write("HallPadLand", peak_to(x, -2))


if __name__ == "__main__":
    main()

"""Pawn Rush grand stair hall (대계단 홀): the jump pad's sounds (R562).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I hall_jump_sounds.py

승규 님 (11-10): "발판을 밟고 날아갈 때 C1을 넣어주고 착지 성공 할 때 B2를 넣어줘" - from the R561 mockups
(https://claude.ai/artifact/TbQJHw7S3cCc9cqCg8D8FG, ElevenLabs flow Lkkgpgt3VEdGTwt37YZY, eleven_text_to_sound_v2,
1.2 s, influence .55, sfx_takes/jumppad/).
  launch  C1 "낮은 부웅 솟음" ("A glowing magic launch pad throwing a player into the air: a deep warm bass surge
          'bwoom' that swells and rises quickly in pitch with a soft upward air whoosh ... no bells, no chimes, no
          sparkle"): from its start, 1.0 s, 6 kHz lowpass, faded over 0.3 s, peak -2 dB. Played at the pad.
  land    B2 "공기 분사" ("A powerful pneumatic air blast ... a sudden deep low thump and a big burst of compressed air,
          'pfoosh' ..."): from 4 ms before its hit, 0.85 s, 7 kHz lowpass, faded over 0.3 s, peak -1 dB. Played where
          the piece lands.
Dry and mono 44.1 kHz: the hall's ring is added in Unity (JumpPadFx.Sound.cs, RollingSpawner.Ring).
-> Assets/Resources/PawnRushHallSfx/HallJumpLaunch.wav, HallJumpLand.wav
"""
import os
import wave

import miniaudio
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TAKES = os.path.join(HERE, "sfx_takes", "jumppad")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushHallSfx"))
SR = 44100


def load(name):
    a = miniaudio.decode_file(os.path.join(TAKES, name + ".mp3"), output_format=miniaudio.SampleFormat.FLOAT32,
                              nchannels=1, sample_rate=SR)
    return np.frombuffer(a.samples, dtype=np.float32).astype(np.float64)


def lowpass(x, hz):
    k = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    p = 0.0
    for i, v in enumerate(x):
        p = (1 - k) * v + k * p
        y[i] = p
    return y


def onset(x):
    # the first sample above a tenth of the peak
    return int(np.argmax(np.abs(x) > np.abs(x).max() * .1))


def shape(x, start, seconds, hz, fade, db):
    x = x[max(0, start):start + int(seconds * SR)].copy()
    x -= x.mean()
    x = lowpass(x, hz)
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
    c1 = load("C1")
    write("HallJumpLaunch", shape(c1, onset(c1) - int(.002 * SR), 1.0, 6000, .3, -2))
    b2 = load("B2")
    write("HallJumpLand", shape(b2, onset(b2) - int(.004 * SR), .85, 7000, .3, -1))


if __name__ == "__main__":
    main()

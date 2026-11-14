"""Pawn Rush grand stair hall (대계단 홀): the falling knights' sounds (R543).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I hall_knight_sounds.py

승규 님 (11-09): "기사 소리 시작". A falling knight (FallingChessPiece) warns 1.4 s, falls 0.65 s and slams down with a
shockwave; its floor circle (W1, VfxGroundWarn) fills over warning + fall = LEAD 2.05 s, 24 rim ticks lighting
clockwise, the last 0.35 s blinking 7 a second (cos(age * 7 * 2pi) peaks).
  warn  synthesized, the chandelier's "째깍 원" (hall_chandelier_sounds.py, R533) on this circle's clock and a fourth
        lower (196 Hz ticks rising a quarter tone at a time, 392 Hz beeps on the blinks): a tick every LEAD / 8 s
        (every 3 rim ticks), a beep on each blink. Lowpassed at 3.2 kHz, peak -3 dB, 2.05 s - it ends at the impact.
  fall  ElevenLabs (flow Lkkgpgt3VEdGTwt37YZY, eleven_text_to_sound_v2, 1.0 s, influence .6, three takes): "A huge heavy
        stone chess knight statue dropping from high above, a fast descending deep whoosh of air rushing past as it
        falls, getting louder toward the end, cut off right before landing. No impact, ..." - every take came out loud
        first and dying away, so take 1 (the fullest, 59 % under 200 Hz) is played backwards: it swells into the
        impact. Its last 0.75 s, faded in 0.25 s, lowpassed at 5 kHz, peak -2 dB - it ends at the impact.
  land  ElevenLabs (same flow, 1.5 s): "A giant heavy stone chess knight statue slamming down onto a marble floor, one
        massive deep boom and thud with a rumbling shockwave burst of air and a little stone crumble, powerful video
        game ground slam. No voice, no music, no metallic ringing, no glass." - take 2 (the slam itself; it sat on a
        DC offset, removed with a 35 Hz highpass) over take 1 (the long low boom, -6 dB), with a synthesized 45 Hz
        thump under the hit. Lowpassed at 6 kHz, 1.4 s, faded 0.4 s, peak -1 dB - it starts at the impact.
Dry and mono 44.1 kHz: the hall's ring is added in Unity (FallingPieceFx.Sound.cs, RollingSpawner.Ring).
-> Assets/Resources/PawnRushHallSfx/HallKnightWarn.wav, HallKnightFall.wav, HallKnightLand.wav
R560: the warning is now take A "나무 시계 째깍" (hall_warn_wood.py); the text above tells how it was before.
R570 (승규 11-10 "A1로 해주고 범위는 그대로 하돼 울림을 크게 하고 바닥 떨어지는 바람 크게 해"):
  land  now mockup take A1 (sfx_takes/stonedrop/A1.mp3, R569: "huge marble chess knight statue ... massive deep boom",
        its impact 0.035 s in), from 0.03 s, with a stone hall's tail baked in (a 3.6 s low / 1.8 s high decay, early
        reflections, -7 dB under the hit) so the ring is big and still there after Unity stops the clip. 2.45 s, it ends
        before the next warning starts. Unity also rings it more (its own GameObject, RollingSpawner.Ring 3.8 s, +6 dB).
  fall  the same reversed whoosh, brought up all the way down (its quiet start lifted, so it rushes from the top, not
        only in the last 0.2 s), peak -0.5 dB; played at volume 1.0 instead of 0.7. Distances are unchanged.
R571 (승규 11-10 "내려오는 기사가 바닥을 칠 때 내는 소리 훨씬 크게"): the landing's A1 is driven into a soft clip
  (punch: tanh x7) with its overtones at 120-700 Hz brought up, and the whole clip soft-clipped again (tanh x2.4),
  peak -0.3 dB: loudness over the first 0.5 s -11.1 -> -5.5 dB, over 120 Hz -21.2 -> -14.3 dB. In Unity it plays on
  two voices in step at volume 1 (FallingPieceFx.Sound.cs), another +6.9 dB.
R640: one dry A1 landing, 0.8 s with a soft ending, peak -3 dBFS. Unity supplies the short shared room reflection.
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
LEAD, URGENT, BLINK = 2.05, .35, 7.0


def load_file(path):
    a = miniaudio.decode_file(path, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    return np.frombuffer(a.samples, dtype=np.float32).astype(np.float64)


def lift(x, amount, most_db=20):
    # Evens out how loud it gets: a part `d` dB under the loudest is raised by d * amount (at most most_db), along a
    # smooth envelope, so its shape stays and nothing distorts.
    e = np.sqrt(np.maximum(lowpass(x * x, 20), 1e-12))
    g = np.minimum((e / e.max()) ** -amount, 10 ** (most_db / 20))
    return x * g


def load(name):
    a = miniaudio.decode_file(os.path.join(TAKES, name + ".mp3"), output_format=miniaudio.SampleFormat.FLOAT32,
                              nchannels=1, sample_rate=SR)
    return np.frombuffer(a.samples, dtype=np.float32).astype(np.float64)


def lowpass(x, hz):
    k = np.exp(-2 * np.pi * hz / SR)
    for _ in range(2):
        y = np.empty_like(x)
        p = 0.0
        for i, v in enumerate(x):
            p = (1 - k) * v + k * p
            y[i] = p
        x = y
    return x


def highpass(x, hz):
    return x - lowpass(x, hz)


def env(n, a=.003, r=.04):
    e = np.ones(n)
    na, nr = int(a * SR), min(n, int(r * SR))
    e[:na] = np.linspace(0, 1, na)
    e[-nr:] *= np.linspace(1, 0, nr) ** 2
    return e


def tone(f, sec, harm=(1, .35, .1)):
    tt = np.arange(int(sec * SR)) / SR
    return sum(g * np.sin(2 * np.pi * f * (i + 1) * tt) for i, g in enumerate(harm)) * env(len(tt))


def place(buf, x, at):
    i = int(at * SR)
    buf[i:i + len(x)] += x[:max(0, len(buf) - i)]


def thump(hz, decay, seconds):
    t = np.arange(int(seconds * SR)) / SR
    f = hz * (1 + .6 * np.exp(-t / .03))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / decay) * np.minimum(1, t / .002)


def fade_in(x, seconds):
    n = min(len(x), int(seconds * SR))
    x = x.copy()
    x[:n] *= np.sin(np.linspace(0, np.pi / 2, n)) ** 2
    return x


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
    # R560 (승규 "샹들리에 기사 A로"): take A "나무 시계 째깍" on the same clock, hall_warn_wood.py.
    write("HallKnightWarn", hall_warn_wood.warnings()[1])

    fall = load("KnightFall_1")[::-1][-int(.75 * SR):]
    fall = fade_out(lowpass(fade_in(fall, .25), 5000), .008)
    write("HallKnightFall", peak_to(lift(fall, .5), -.5))   # R570: louder all the way down

    dry = highpass(load_file(os.path.join(TAKES, "stonedrop", "A1.mp3")), 25)[int(.03 * SR):]
    write("HallKnightLand", peak_to(fade_out(fade_in(dry[:int(.8 * SR)], .003), .2), -3))


if __name__ == "__main__":
    main()

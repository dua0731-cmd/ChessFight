"""Pawn Rush grand stair hall (대계단 홀): the swinging hammers' sounds (R535, R536, R539, R540, R541, R542, R545, R547, R556).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I hall_hammer_sounds.py

승규 님 (11-09): "아재 해머 소리", then "사슬 소리가 개구리 우는 소리 같고 바람 소리도 묵직하게" (R536). The hall's
hammers swing end to end in 2 s (PendulumSwing.SwingDuration) and hang 0.2 s at each end (PauseAtEnds).
  whoosh  R542 (승규 11-09 "스윙할 때 소리가 안 맞아 한 번 스윙하고 멈춤 지점까지 소리가 이어져야 돼"): one sound for the whole
          swing, end to end (SWING_SECONDS = SwingDuration 2 s). The swing follows angle = -A cos(pi t / 2), so the
          ball's speed goes as sin(pi t / 2): still at the start, fastest at 1.0 s, still again as it reaches the far
          end. The sound's loudness follows speed^1.5 and its brightness follows speed (a 500 Hz lowpassed copy at
          rest, a 3.5 kHz one at full speed), so it rises with the swing, is loudest as the ball passes the middle and
          dies away exactly as it arrives where it stops (the chain clunk, HallHammerCreak, plays there).
          The body is R541's take - ElevenLabs (flow Lkkgpgt3VEdGTwt37YZY, eleven_text_to_sound_v2, 1.2 s, influence
          .7): "One single smooth continuous heavy swoosh of a giant sledgehammer swinging past, a single "whoooosh"
          that rises smoothly and fades out smoothly, one pass only, deep and weighty. ..." take 2, played 20 % slower
          - its loud 0.4 s stretched to the swing's 2 s without changing its pitch (overlapping 90 ms grains read
          slowly through it, a little jitter so it doesn't buzz). +4 dB into a soft tanh limiter, peak -1 dB, 2.1 s.
          (R541 put that take's short swell at the middle only, so the swing was silent at its start and end; R539/R540
          used a take heard as two swishes; R535/R536 were long rumbling air whooshes, "wind, not a swing".)
          R545 (승규 11-09, picked A on the mockup page https://claude.ai/artifact/CM5pTusYpQNoZrRc7Mw6ks): the body is now
          candidate A - "a heavy iron wrecking ball on a thick chain swinging past, deep low whoosh with the chain
          grinding" (sfx_takes/hammer_mockup/A2 = sfx_takes/HammerChainBall_2), at its own speed: its own swell with its
          loudest 0.1 s at 1.0 s, over a bed of its loud 0.4 s stretched to the swing and shaped by speed as above
          (lowpass 400 Hz at rest .. 2.5 kHz at speed, 45 %). +3 dB soft limiter, peak -1 dB, 2.1 s.
          R547 (승규 11-09 "H로 해주고 사운드에 맞게 해머도 속도에 맞춰", second mockup round R546): the body is now
          candidate H - "A huge heavy sandbag hanging on a thick rope swinging past once, a dull muffled thick whoosh of
          air with the rope creaking softly under the weight, slow and heavy. No ringing, no squeak, no impact, no
          voice." (2 s, influence .6) take 1 (sfx_takes/hammer_mockup2/H1 = sfx_takes/HammerSandbag_1), unstretched:
          its loudest 40 ms at the swing's middle, and the hall's hammers now swing in 1.4 s (was 2) so the sound and
          the swing match (the take's body runs about 0.3 .. 1.4 s around its peak at 0.66 s). +2 dB soft limiter,
          peak -1 dB, 1.5 s.
          R556 (승규 11-10 "지금 해머 휙 소리인데 후웅~ 같은 소리로" -> R555 mockup https://claude.ai/artifact/CFp1b6qz97JJkJCLfVpeb7,
          "O로 해주고 지금 사슬 소리는 바꾸지 마"): a round low "후웅~" - H's air played 12 % slower (about 2 semitones
          lower) and lowpassed at 1.8 kHz ("후-"), over a deep body ("웅"): ElevenLabs "Deep slow air displacement as a
          massive wrecking ball passes, a soft booming "hwoooom" whoosh with deep low-end body, smooth rise and smooth
          fall, muffled. No swish, no whistle, no impact, no voice." (1.6 s, influence .6) take 1 (sfx_takes/
          hammer_mockup3/K1 = sfx_takes/HammerHoongK_1), its loudest 0.1 s at the middle plus half as much of its loud
          0.4 s stretched to the swing (lowpass 200 Hz at rest .. 900 Hz at speed, loudness speed^1.5), the body
          swelling with the ball's speed (35 % at the ends). Air .7 : body 1, +2.5 dB soft limiter, peak -1 dB, 1.5 s.
          The clunk is unchanged.
  clunk   ElevenLabs (same flow, 0.8 s, influence .6): "Thick heavy iron chain links snapping tight under a huge weight,
          one dull deep muffled clunk and short low thud, heavy and solid. No creak, no squeak, no groan, no ringing, no
          jingle, no voice." - take 1 (one sharp hit, a short tail). Played 30 % slower and lowpassed at 2.5 kHz, with
          a synthesized 55 Hz thump (40 ms attack-free, 0.25 s decay) under its hit so it has weight. Cut at 0.6 s,
          faded 0.2 s, peak -2 dB. R542: starts 10 ms before its hit, 0.5 s.
          (R535's creak, "A massive heavy iron chain and pivot straining..." take 2, sounded like a frog croaking.)
Dry and mono 44.1 kHz: the hall's ring is added in Unity (RollingSpawner.Ring).
-> Assets/Resources/PawnRushHallSfx/HallHammerWhoosh.wav, HallHammerCreak.wav (the clunk keeps the creak's file name)
"""
import os
import wave

import miniaudio
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TAKES = os.path.join(HERE, "sfx_takes")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushHallSfx"))
SR = 44100
SWING_SECONDS = 1.4  # one swing, end to end (the hall's PendulumSwing.SwingDuration; R547, was 2.0)


def load(name):
    a = miniaudio.decode_file(os.path.join(TAKES, name + ".mp3"), output_format=miniaudio.SampleFormat.FLOAT32,
                              nchannels=1, sample_rate=SR)
    return np.frombuffer(a.samples, dtype=np.float32).astype(np.float64)


def slower(x, rate):
    # plays `x` at `rate` (< 1: slower and lower, like a tape)
    n = int(len(x) / rate)
    return np.interp(np.arange(n) * rate, np.arange(len(x)), x)


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


def loudest(x, window=.1):
    n = int(window * SR)
    e = np.convolve(x ** 2, np.ones(n) / n, "same")
    return int(np.argmax(e))


def thump(hz, decay, seconds):
    t = np.arange(int(seconds * SR)) / SR
    f = hz * (1 + .6 * np.exp(-t / .03))   # a short drop in pitch, like a heavy knock
    phase = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(phase) * np.exp(-t / decay) * np.minimum(1, t / .002)


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


def stretch(x, seconds, grain=.09, hop=.015, seed=7):
    # x stretched to `seconds` at its own pitch: Hann grains of `grain` s laid every `hop` s, each read from where
    # a slow pointer through x has got to (plus a little jitter)
    rng = np.random.default_rng(seed)
    g, h = int(grain * SR), int(hop * SR)
    out = np.zeros(int(seconds * SR) + g)
    norm = np.zeros_like(out)
    w = np.hanning(g)
    steps = (len(out) - g) // h
    for k in range(steps):
        src = int((len(x) - g) * k / max(1, steps - 1) + rng.integers(-h, h + 1))
        src = min(max(src, 0), len(x) - g)
        out[k * h:k * h + g] += x[src:src + g] * w
        norm[k * h:k * h + g] += w
    return (out / np.maximum(norm, 1e-3))[:int(seconds * SR)]


def main():
    # R556: mockup O, "후웅" - H's air a little lower ("후-") over HammerHoongK_1's deep body ("웅"), both with their
    # loudest at the middle of the 1.4 s swing (the ball fastest there), the body swelling with the ball's speed.
    n = int((SWING_SECONDS + .1) * SR)
    tt = np.arange(n) / SR
    speed = np.sin(np.pi * np.clip(tt / SWING_SECONDS, 0, 1))

    def place(x, window):
        y = np.zeros(n)
        at = int(SWING_SECONDS / 2 * SR) - loudest(x, window)
        s = x[max(0, -at):][:n - max(0, at)]
        y[max(0, at):max(0, at) + len(s)] = s
        return y

    bag = load("HammerSandbag_1")
    bag = bag - bag.mean()
    air = peak_to(place(lowpass(slower(bag, .88), 1800), .04), 0)
    deep = load("HammerHoongK_1")
    deep = deep - deep.mean()
    core = deep[max(0, loudest(deep) - int(.2 * SR)):loudest(deep) + int(.2 * SR)]
    bed = stretch(core, SWING_SECONDS + .1)
    bed = peak_to((lowpass(bed, 200) * (1 - speed) + lowpass(bed, 900) * speed) * speed ** 1.5, 0)
    body = peak_to(place(deep, .1), 0) + .5 * bed
    whoosh = .7 * air + peak_to(body, 0) * (.35 + .65 * speed)
    whoosh = fade_in(whoosh * np.clip(1 - (tt - SWING_SECONDS) / .1, 0, 1), .01)
    whoosh = np.tanh(peak_to(whoosh, 2.5))
    write("HallHammerWhoosh", peak_to(fade_out(whoosh, .05), -1))

    clunk = lowpass(slower(load("HammerChainClunk_1"), .7), 2500)
    clunk = peak_to(clunk, 0)
    hit = int(np.argmax(np.abs(clunk)))
    low = np.zeros_like(clunk)
    th = thump(55, .25, .6) * .9
    low[hit:hit + len(th)] += th[:len(low) - hit]
    # R542: start 10 ms before the hit (the take had 0.18 s of near-silence first), so it lands as the ball stops
    clunk = (clunk + low)[max(0, hit - int(.01 * SR)):][:int(.5 * SR)]
    write("HallHammerCreak", peak_to(fade_out(fade_in(clunk, .005), .2), -2))


if __name__ == "__main__":
    main()

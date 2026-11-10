"""Pawn Rush grand stair hall (대계단 홀) beat 1: the rolls' sounds, from ElevenLabs takes (R531, R532, R548, R550).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I hall_roll_sounds.py

승규 님 (11-09): "이제 대관홀 차근차근 사운드 넣어 계단에 장애물 구르는 소리". The takes are in sfx_takes/, made in the
ElevenLabs flow "PawnRush Hall Rolling SFX R531" (Lkkgpgt3VEdGTwt37YZY), eleven_text_to_sound_v2, three takes each:
  StatueRollLoop  4 s loop, influence .45  "Huge heavy marble column rolling steadily across a polished marble floor,
                  deep continuous stone rumble and grinding, low and weighty, no voice"
  StatueStep      1.2 s, .5  "Single loud heavy marble block dropping onto a stone stair step, one deep booming thud
                  with a short stone crack, close-mic, no voice"
  CarpetRollLoop  4 s loop, .45  "A thick rolled-up heavy rug tumbling and rolling across a stone floor, soft muffled
                  continuous fabric rumble and swish, no voice"
  CarpetStep      1 s, .5  "Single heavy rolled-up rug dropping onto a stone step, one soft deep muffled thump of thick
                  fabric, close-mic, no voice"
Picked on a waveform / spectrogram sheet: StatueRollLoop 1 (even; 3 rings at 3.5 kHz, 2 bursts at its end), StatueStep 1
(the deep thud, 99 % under 200 Hz) with the first crack of 3 under it (2 is a bright click), CarpetRollLoop 2 (a tumbling
thump every few tenths), CarpetStep 1 (its first thump; the bounce 0.2 s later is cut - every landing plays its own).
All dry and mono 44.1 kHz: the hall's ring is added in Unity (RollingSpawner.Sound.cs).
  loops  DC and rumble under 30 Hz off, end crossfaded into the start over 0.3 s (seamless), RMS to its level
         (statue -20, carpet -24 dBFS), rare spikes soft-limited under -1 dB
  steps  from 3 ms before the hit, cut with a short fade, peak -1 dB
  runner steps (the middle lanes, on the crimson runner): 1.2 s / 1 s, .5, "Single very heavy stone statue dropping
         onto a thick carpeted stair step, one deep dull muffled thud, padded and soft, no crack, close-mic, no voice" and
         "Single rolled-up rug flopping onto a thick carpeted stair step, one very soft dull padded thump, fabric on
         fabric, close-mic, no voice"; StatueOnCarpet 1 (its first thud, no highs) and CarpetOnCarpet 1 (a short
         fabric hit), lowpassed at 2.5 / 3 kHz and peak -3 dB: duller and softer than the marble knocks
  hole warning (R532): 1.2 s, .5, "Deep rising underground rumble from a hole in a stone floor, something very heavy
         rolling up from below, short low swell, no voice" (Rumble 1, its swell peaks at .7 s and the roll comes up at
         .8 s) and "Heavy stone trapdoor grinding open in a marble stair, low deep rumble building up quickly, scraping
         stone, short swell, close-mic, no voice" (Grind 3, a quarter, lowpassed at 3 kHz), faded in from nothing
R548 (11-10, "사탕 소리가 나"): new takes in sfx_takes/roll2 (same flow, eleven_text_to_sound_v2, two takes each):
  StatueStepMarble 0.8 s, .6 "A huge solid marble statue dropping one step down onto a marble stair, a single deep heavy
                   stone boom with real weight, dull and massive, a short low stone scrape after. No click, no rattle,
                   no ringing, no voice." (1)
  StatueStepCarpet 0.8 s, .6 "... thudding down one step onto a thick carpeted stair, one very deep heavy padded thud,
                   muffled and massive, the floor shaking. ..." (1)
  CarpetStepMarble 0.6 s, .6 "A big heavy rolled-up carpet flopping down one step onto a bare marble stair, one deep
                   thick slap-thud of heavy fabric on hard stone. ..." (2)
  CarpetStepCarpet 0.6 s, .6 "... onto a thick carpeted stair, one soft deep muffled thump, fabric on fabric ..." (2)
  StatueRollFlat   4 s loop, .5 "A huge heavy marble statue rolling slowly across a flat polished marble floor, deep
                   continuous low stone rumble, smooth and heavy, steady. No clicks, no rattle, ..." (1)
  CarpetRollFlat   4 s loop, .5 "A big heavy rolled-up rug rolling across a flat marble floor, soft deep continuous
                   muffled rumble with a low fabric brush, steady. ..." (1)
-> Assets/Resources/PawnRushHallSfx/HallRoll{Statue,Carpet}{Loop,Step,StepRunner}.wav, HallRollHoleWarn.wav
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


def highpass(x, hz=30.0):
    k = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    prev_x = prev_y = 0.0
    for i, v in enumerate(x):
        prev_y = k * (prev_y + v - prev_x)
        prev_x = v
        y[i] = prev_y
    return y


def lowpass(x, hz):
    """One-pole lowpass: the carpet swallows the highs."""
    k = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    prev = 0.0
    for i, v in enumerate(x):
        prev = (1 - k) * v + k * prev
        y[i] = prev
    return y


def onset(x, frac=.3):
    env = np.convolve(np.abs(x), np.ones(64) / 64, "same")
    return max(0, int(np.argmax(env > env.max() * frac)) - int(.003 * SR))


def peak_to(x, db):
    return x * (10 ** (db / 20) / (np.abs(x).max() + 1e-12))


def rms_to(x, db):
    return x * (10 ** (db / 20) / (np.sqrt((x ** 2).mean()) + 1e-12))


def limit(x, ceiling_db=-1.0):
    """A soft limiter: the rare spikes over the ceiling are rounded off (tanh), the rest is untouched."""
    c = 10 ** (ceiling_db / 20)
    knee = .7 * c
    y = x.copy()
    over = np.abs(x) > knee
    y[over] = np.sign(x[over]) * (knee + (c - knee) * np.tanh((np.abs(x[over]) - knee) / (c - knee)))
    return y


def fade_out(x, seconds):
    n = min(len(x), int(seconds * SR))
    x = x.copy()
    x[-n:] *= np.cos(np.linspace(0, np.pi / 2, n)) ** 2
    return x


def seamless(x, seconds=.3):
    n = int(seconds * SR)
    head, body, tail = x[:n], x[n:-n], x[-n:]
    w = np.sin(np.linspace(0, np.pi / 2, n)) ** 2
    return np.concatenate([tail * (1 - w) + head * w, body])   # the end flows into the start


def step(x, length):
    s = onset(x)
    return fade_out(x[s:s + int(length * SR)], .04)


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


def knock(seconds, decay, seed):
    """A stone knock: noise between 250 and 900 Hz, struck and dying away in `decay` s (no click: 2 ms in)."""
    rng = np.random.default_rng(seed)
    n = int(seconds * SR)
    x = rng.standard_normal(n)
    x = lowpass(lowpass(x, 900), 900) - lowpass(lowpass(x, 250), 250)
    tt = np.arange(n) / SR
    return x * np.exp(-tt / decay) * np.minimum(1, tt / .002)


def on_stone(x, amount, decay, seed):
    x = peak_to(x, 0)
    k = peak_to(knock(len(x) / SR, decay, seed), 0)
    return fade_out(x + amount * k, .1)


def padded(x):
    x = lowpass(lowpass(x, 450), 450)
    n = int(.012 * SR)
    x = x.copy()
    x[:n] *= np.sin(np.linspace(0, np.pi / 2, n)) ** 2
    return fade_out(x, .1)


def main():
    # R548 (승규 11-10 "사탕 소리가 나 - 카펫 구간과 대리석 바닥 떨어지는 소리 구별해 주고, 계단 > 평지 > 계단 구간이라
    # 그 소리도 맞게"): R531's steps were cut to 0.17 s (a short "tock" four times a second, with a crack on the statue's)
    # and its stone loop had a grinding rattle in it - together they sounded like candy. Now (takes in sfx_takes/roll2):
    #   loops  a smooth heavy roll on the flat (heard at full only on the landing and the foot; on a flight it is a low
    #          undertone under the steps; on the runner Unity muffles it with a low-pass)
    #   steps  one whole heavy hit per step, 0.5 / 0.4 s with its own decay; marble = hard stone under it (a little of
    #          its 150 .. 900 Hz knock kept), runner = padded (lowpassed at 450 Hz, the thud only)
    # R550 (승규 11-10 "석상 대리석은 A로"): the statue on marble is candidate A - a marble block on a marble step
    # (roll3/MarbleBlock_1, its heavy body and hard stone knock, 0.6 s) - and on the flat a stone rolling on marble
    # (roll3/StoneRollMarble_1, harder and grittier than R548's)
    write("HallRollStatueLoop", limit(rms_to(seamless(highpass(load("roll3/StoneRollMarble_1"))), -20)))
    write("HallRollCarpetLoop", limit(rms_to(seamless(lowpass(highpass(load("roll2/Roll2CarpetRollFlat_1")), 1500)), -24)))
    # marble: the take's thud with a stone knock on its hit (stone answers in the 250 .. 900 Hz; the takes are almost
    # all under 200 Hz, so without it marble and runner sounded alike); runner: the thud only, its first 12 ms eased in
    write("HallRollStatueStep", peak_to(fade_out(step(highpass(load("roll3/MarbleBlock_1")), .6), .08), -1))
    write("HallRollCarpetStep", peak_to(on_stone(step(highpass(load("roll2/Roll2CarpetStepMarble_2")), .4), .4, .03, 2), -1))
    # R550 (승규 11-10 "카펫 구간에 있을 때 소리를 더 키워"): the runner hits made denser (+5 dB into a soft tanh) and to
    # -1 dB; Unity also plays everything on the runner louder (RollingSpawner.Sound.cs RunnerBoost)
    write("HallRollStatueStepRunner", peak_to(np.tanh(peak_to(padded(step(highpass(load("roll2/Roll2StatueStepCarpet_1")), .5)), 5)), -1))
    write("HallRollCarpetStepRunner", peak_to(np.tanh(peak_to(padded(step(highpass(load("roll2/Roll2CarpetStepCarpet_2")), .4)), 5)), -1))

    # R532 the hole warning: Rumble 1's swell with Grind 3's stone scrape under it, faded in so it grows out of nothing
    body = highpass(load("HoleWarnRumble_1"))[:int(1.05 * SR)]
    grind = lowpass(highpass(load("HoleWarnGrind_3")), 3000)[:len(body)]
    mix = peak_to(body, -1) + .25 * peak_to(grind, -1) * np.linspace(.3, 1, len(body))
    mix *= np.sin(np.linspace(0, np.pi / 2, len(mix))) ** .5
    write("HallRollHoleWarn", peak_to(fade_out(mix, .2), -1))


if __name__ == "__main__":
    main()

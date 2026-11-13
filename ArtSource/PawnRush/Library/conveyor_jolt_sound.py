"""Pawn Rush library (작전 회의실): the conveyor's jolting-stop clunk, near and far layers, from an ElevenLabs take (R443).

    blender --background --factory-startup --python conveyor_jolt_sound.py -- <take.mp3 or .wav>
    blender --background --factory-startup --python conveyor_jolt_sound.py -- <take> --out D:/check   (a copy elsewhere)

승규 님 (11-05): "컨베이어 벨트 멈출 때 덜컹 하면서 멈출 수 있게 하고 그 사운드 찾아주고". The takes were made in the
ElevenLabs flow "PawnRush Library Conveyor SFX R289" (SsseNEjgrzO8OYi98PaU, where the conveyor's and the cannons' other
sounds come from), three sfx nodes x 2 takes (eleven_text_to_sound_v2, 1.5 - 1.6 s, prompt influence 0.5):
  JoltStop_A_Iron   "Heavy iron conveyor carriage slamming to a sudden stop against a wooden buffer: one deep loud thunk
                     with a hard metal clank, then a short rattle of loose iron fittings and wooden slats settling ..."
  JoltStop_B_Wood   a wooden machine's thud and creak (take 1 rang at 1.9 kHz for 0.12 s - coin.py's ring - so not used)
  JoltStop_C_Game   a punchy game-style ka-chunk
Used: JoltStop_A_Iron take 2 (session WlsGxU5zvSpDhr11D8SW) - the deepest (82 % of its loudest moment under 200 Hz), one
thunk and a rattle that dies in about 0.8 s. Blender decodes the mp3 (aud); numpy then makes:
  near  mono 44.1 kHz, rumble and DC off (30 Hz), started 3 ms before the hit, cut where it falls 50 dB under its peak
        (1.5 s at most) with a 0.12 s fade, a light saturation so it sits as loud as the cannon's shot, peak -1 dB
  far   the near one dulled (three 700 Hz one-pole low-passes) in a 1.0 s stone-hall tail, peak -2 dB
-> Assets/Resources/PawnRushLibrarySfx/ConveyorJolt.wav + ConveyorJoltFar.wav (LibraryConveyorSound plays them on each
   jolting stop of LibraryShuttleDriver). To use another take, give its file instead.
"""
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SFX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrarySfx"))
SR = 44100
rng = np.random.default_rng(443)


def decode(path):
    if path.lower().endswith(".wav"):
        w = wave.open(path)
        a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768
        if w.getnchannels() > 1:
            a = a.reshape(-1, w.getnchannels()).mean(1)
        if w.getframerate() != SR:
            raise SystemExit(f"{path}: {w.getframerate()} Hz, wanted {SR}")
        return a
    import aud
    tmp = path + ".decoded.wav"
    aud.Sound(path).rechannel(1).resample(SR, False).write(
        tmp, rate=SR, channels=aud.CHANNELS_MONO, format=aud.FORMAT_S16, container=aud.CONTAINER_WAV, codec=aud.CODEC_PCM)
    a = decode(tmp)
    os.remove(tmp)
    return a


def write(path, x):
    w = wave.open(path, "wb")
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    w.close()
    print("WROTE", path, round(len(x) / SR, 3), "s")


def onepole(x, hz):
    a = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    s = 0.0
    for i, v in enumerate(x):
        s = (1 - a) * v + a * s
        y[i] = s
    return y


def near(raw):
    x = raw - onepole(raw, 30)
    peak = np.abs(x).max()
    start = max(0, int(np.argmax(np.abs(x) > .05 * peak)) - int(.003 * SR))
    x = x[start:]
    env = np.maximum.accumulate(np.abs(x)[::-1])[::-1]               # the loudest still to come
    end = min(len(x), int(1.5 * SR), int(np.argmax(env < peak * 10 ** (-50 / 20))) or len(x))
    x = x[:end].copy()
    fade = int(.12 * SR)
    x[-fade:] *= np.linspace(1, 0, fade) ** 2
    x /= np.abs(x).max()
    x = np.tanh(1.6 * x) / np.tanh(1.6)
    return x / np.abs(x).max() * .89


def far(x):
    y = onepole(onepole(onepole(x, 700), 700), 700)
    n = int(SR * 1.0)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 6.9, n))
    ir = onepole(ir, 2200)
    ir /= np.sqrt((ir ** 2).sum())
    pad = np.concatenate([y, np.zeros(n)])
    size = 2 ** int(np.ceil(np.log2(len(pad) + n)))
    wet = np.real(np.fft.ifft(np.fft.fft(pad, size) * np.fft.fft(ir, size)))[:len(pad)]
    z = .55 * pad + .6 * wet
    return z / np.abs(z).max() * .79


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    if not argv:
        raise SystemExit(__doc__)
    out = argv[argv.index("--out") + 1] if "--out" in argv else SFX
    os.makedirs(out, exist_ok=True)
    x = near(decode(argv[0]))
    write(os.path.join(out, "ConveyorJolt.wav"), x)
    write(os.path.join(out, "ConveyorJoltFar.wav"), far(x))


main()

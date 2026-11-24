"""R772: G's and H's door-opening sounds, put together from single ElevenLabs takes at the times their gate moments
move (user 11-22, "G랑 H 시작"). Writes raw/MiniGame/sfx_procession_gate.wav and sfx_chesshall_gate.wav, which
bake_minigame.py then cuts and levels like any other take.

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I ArtSource/PawnRush/Sfx/compose_gates.py

Takes: ElevenLabs flow "G · H 미니게임 빈 소리" (4D8axxzwyD2J4mVnWgVN), eleven_text_to_sound_v2, influence 0.6, four
each, in raw/MiniGame/takes_R772/<kind>_<take>.mp3 (prompts in Docs/PawnRush/PAWN_RUSH_AUDIO.md section 8-2):
  gate_wood_rise  a carved wooden door hauled up (take 1: the lowest and smoothest, no knock inside it)
  g_banner_drop   a brass rod's clack and a banner dropping open (take 3: its flap is loudest at 0.12 s, as G's cloth)
  h_square_flip   one board square flipped and slapped down (take 3: one wooden clack, the most wood of the four)
  h_brass_edge    a brass strip slammed down (take 3: the only bright one; the others are a dull thud 20 dB down)

Times (s from the moment's start, MiniGameProcessionGateFx / MiniGameChessHallGateFx, with MiniGameGateMoment.Slam):
  G  the rod lands by 0.09 and the cloth drops 0.04 .. 0.2: the banner take's loudest at 0.12, over the door
  H  rows land at FirstRow 0.08 + (3 - row) x RowGap 0.1 + Flip 0.17 = 0.25, 0.35, 0.45, 0.55 (outer squares 25 ms
     later, a second softer clack); the brass edge from edgeAt 0.59, flat by 0.68: its hit at 0.62; the door under it
Each layer is first set by its loudest 100 ms (relative dB below), so the mix holds whatever the takes' own levels.
"""
import os, wave
import numpy as np
import miniaudio

HERE = os.path.dirname(os.path.abspath(__file__))
TAKES = os.path.join(HERE, 'raw', 'MiniGame', 'takes_R772')
OUT = os.path.join(HERE, 'raw', 'MiniGame')
SR = 44100


def take(name):
    d = miniaudio.decode_file(os.path.join(TAKES, name + '.mp3'), output_format=miniaudio.SampleFormat.FLOAT32,
                              nchannels=2, sample_rate=SR)
    x = np.frombuffer(d.samples, np.float32).reshape(-1, 2).astype(np.float64)
    corr = np.corrcoef(x[:, 0], x[:, 1])[0, 1]
    if corr >= .2: return x.mean(1)
    return x[:, int((x[:, 1] ** 2).sum() > (x[:, 0] ** 2).sum())]


def loud100(y):
    k = int(.1 * SR)
    return 10 * np.log10(np.convolve(y ** 2, np.ones(k) / k, 'valid').max() + 1e-12)


def peak_at(y):
    """Where the loudest 5 ms is (s)."""
    k = int(.005 * SR)
    e = np.convolve(y ** 2, np.ones(k) / k, 'valid')
    return (int(np.argmax(e)) + k // 2) / SR


def layer(y, db):
    return y * 10 ** ((db - loud100(y)) / 20)


def place(mix, y, at):
    """Adds y so that sample 0 lands at `at` s (a negative `at` drops the front)."""
    i = int(round(at * SR))
    if i < 0: y, i = y[-i:], 0
    end = i + len(y)
    if end > len(mix): mix = np.pad(mix, (0, end - len(mix)))
    mix[i:end] += y
    return mix


def hit_at(mix, y, when, db):
    """Places y so its loudest 5 ms falls on `when`."""
    return place(mix, layer(y, db), when - peak_at(y))


def write(name, y):
    y = y / max(1e-9, np.abs(y).max()) * 10 ** (-3 / 20)
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(np.rint(np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())
    print(f'{name}: {len(y) / SR:.2f} s')


door = take('gate_wood_rise_1')
banner = take('g_banner_drop_3')
flip = take('h_square_flip_3')
edge = take('h_brass_edge_3')

# G: the door from 0, the banners over it (the loudest thing in the moment)
g = place(np.zeros(int(2.0 * SR)), layer(door, -3), 0.)
g = hit_at(g, banner, .12, 0)
write('sfx_procession_gate', g)

# H: the four rows' clacks (middle squares, then the outer ones 25 ms later, softer), the brass edge, the door under
h = place(np.zeros(int(2.0 * SR)), layer(door, -5), 0.)
for n, when in enumerate((.25, .35, .45, .55)):
    h = hit_at(h, flip, when, -3 + n * .5)
    h = hit_at(h, flip, when + .025, -9)
h = hit_at(h, edge, .62, 0)
write('sfx_chesshall_gate', h)

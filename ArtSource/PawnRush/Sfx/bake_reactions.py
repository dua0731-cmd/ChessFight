"""2026-11-25: three pawn reactions, first used by the tutorial (TutorialCourse.Sound) and kept beside the R652 movement
sounds so the game can use them later. No human voice in any of them (user: "사람 음성 소리나는 소리는 안돼").
User, replacing the first set (65 66 69, pure-tone bloops): "다이빙 할 때 넘어 뜨릴 때 상대 폰이 으악 소리 나게 음성 x
좌클릭 연타 사운드도 탁탁탁 소리. 우클릭으로 잡을 때 내는 소리는 뿍" -> picks 74 77 82.
Takes from the ElevenLabs flow "튜토리얼 독수리 오프닝 소리" (92W4uG7w7gbSbvADmfh7, eleven_text_to_sound_v2, influence 0.3),
saved in takes_reaction/ (the first set's takes stay there too):
  Knocked_74.mp3  74  "Cartoon game sound for a little character knocked over: a quick slide whistle that shoots up then
                       droops down "fweee-ooop!" like a surprised cry, followed by a soft bonk, playful and comic.
                       Instrument and foley only, absolutely no human voice, no words, no music, about 0.7 seconds."
      -> (not baked: the knocked-over sound was taken off again, user "넘어짐 소리는 빼주고"; the take stays)
  Mash_77.mp3     77  "Rapid struggling taps "tak tak tak tak": quick separate short crisp slaps, like small hands
                       patting fast on a padded toy, light and comic, about eight distinct taps. Foley only, no voice,
                       no music, about 1.5 seconds."
      -> CourseMash0..n.wav  one left-click of a held pawn struggling: the take's taps cut one by one (bake_glass.py's
                             step cutter), every one matched to one short loudness, so the clicks do not repeat one sound
  Grabbed_82.mp3  82  "Cartoon game sound: a single short hollow "puk" pop, like a cork pulled from a small bottle or a
                       suction cup popping off, round and plump, slightly low and soft, toy-like. One hit only, no voice,
                       no music, about 0.5 seconds."
      -> CourseGrabbed.wav   a pawn taken hold of (the loudest of the take's four pops)
All into Assets/Resources/PawnRushSkillSfx/, the one-shots cut by bake_moves.py's own rules (started on the hit, the
tail cut 50 dB down and at most the length below, 10 ms fade, peak -1 dBFS); the gain PawnRushSkillSfx.Prepare would
give each is printed for whoever plays them outside it (TutorialCourse.Sound).

  uv run --no-project --with miniaudio --with numpy python -I ArtSource/PawnRush/Sfx/bake_reactions.py
"""
import os

import miniaudio
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TAKES = os.path.join(HERE, 'takes_reaction')
OUT = os.path.normpath(os.path.join(HERE, '../../../Assets/Resources/PawnRushSkillSfx'))
SR = 48000
# one-shot clip -> (take, longest seconds, which part: the 'loudest' hit or the 'start')
ONE_SHOTS = {
             'CourseGrabbed': ('Grabbed_82.mp3', .15, 'loudest')}
MASH = ('Mash_77.mp3', .07, 6)   # take, longest tap, most kept


def helpers(script):
    space = {'__file__': os.path.join(HERE, script), '__name__': 'helpers'}
    with open(os.path.join(HERE, script), encoding='utf-8') as f:
        exec(compile(f.read().split('\nreport = {}')[0], script, 'exec'), space)
    return space


moves = helpers('bake_moves.py')    # opening, trim, write, short_db, prepared_db (at the SR given)
glass = helpers('bake_glass.py')    # load, steps_in, short_db, write (at its own 44.1 kHz)


def decode(take):
    d = miniaudio.decode_file(os.path.join(TAKES, take), output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    return np.asarray(d.samples, dtype=np.float64)


def prepare_gain(y):
    kk = int(.3 * SR)
    pad = np.concatenate([y, np.zeros(max(0, kk - len(y)))])
    loud = np.sqrt(np.convolve(pad ** 2, np.ones(kk) / kk, 'valid').max())
    return min(8.0, 10 ** (-21 / 20) / max(loud, 1e-9), .98 / np.abs(y).max())


for name, (take, longest, part) in ONE_SHOTS.items():
    x = decode(take)
    a = 0
    if part == 'loudest':
        k = int(.01 * SR)
        e = np.array([np.mean(x[i:i + k] ** 2) for i in range(0, len(x) - k, k)])
        a = max(0, int(np.argmax(e)) * k - int(.06 * SR))
        x = x[a:a + int(.4 * SR)]
    y, start = moves['trim'](x, SR, longest)
    y *= 10 ** (-1 / 20) / np.abs(y).max()
    moves['write'](os.path.join(OUT, name + '.wav'), y, SR)
    print('%s: from %.2f s of %s, %.3f s long, after Prepare %.1f dB (x%.2f)'
          % (name, a / SR + start, take, len(y) / SR, moves['prepared_db'](y, SR), prepare_gain(y)))

# The mash: the taps of one take, each its own clip (at bake_glass.py's 44.1 kHz).
take, longest, most = MASH
taps = glass['steps_in'](glass['load'](os.path.join(TAKES, take)), longest)
taps.sort(key=lambda c: -glass['short_db'](c[1]))
taps = sorted(taps[:most], key=lambda c: c[0])
target = np.median([glass['short_db'](y) for _, y in taps])
taps = [(t, y * 10 ** ((target - glass['short_db'](y)) / 20)) for t, y in taps]
g = 10 ** (-1 / 20) / max(np.abs(y).max() for _, y in taps)
for old in os.listdir(OUT):
    if old.startswith('CourseMash') and old.endswith('.wav'):
        os.remove(os.path.join(OUT, old))
for i, (t, y) in enumerate(taps):
    glass['write'](os.path.join(OUT, 'CourseMash%d.wav' % i), y * g)
print('CourseMash0..%d: taps at %s s of %s, %s s long, short loudness %.1f dB'
      % (len(taps) - 1, [round(t, 2) for t, _ in taps], take, [round(len(y) / 44100, 3) for _, y in taps],
         glass['short_db'](taps[0][1] * g)))

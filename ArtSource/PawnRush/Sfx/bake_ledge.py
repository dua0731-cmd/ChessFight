"""2026-11-25: the ledge catch '톡탁' - a hand taking hold of a wall's top edge (tutorial lessons 5 and 6; made to sit with
the R652 movement sounds so the game can use it later, user "실제 인게임에서도 적용 할만큼 중요하니까(아직 미적용)").
Take: the ElevenLabs flow "튜토리얼 독수리 오프닝 소리" (92W4uG7w7gbSbvADmfh7, eleven_text_to_sound_v2, influence 0.3),
the user's pick 21, saved as takes_ledge/LedgeGrab_21.mp3. Prompt:
  "Cartoon game foley: a tiny wooden toy figure's hand slapping onto a stone ledge and catching hold. One very short dry
   woody "tok-tak" clack with a soft hollow tonal pop around 700 Hz, crisp and cute, like a wooden chess piece tapped on
   stone. Single hit, no tail, no voice, no music, about 0.5 seconds."
The take is 6 s of about ten separate clacks; the loudest is kept and cut by bake_moves.py's own rules (started on its
hit, the tail cut 50 dB down and at most LONGEST, 10 ms fade, peak -1 dBFS):
  -> Assets/Resources/PawnRushSkillSfx/CourseLedgeGrab.wav
It also prints the gain PawnRushSkillSfx.Prepare would give it, for whoever plays it outside PawnRushSkillSfx
(the tutorial: TutorialCourse.Sound LedgeGain).

  uv run --no-project --with miniaudio --with numpy python -I ArtSource/PawnRush/Sfx/bake_ledge.py
"""
import os

import miniaudio
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TAKE = os.path.join(HERE, 'takes_ledge', 'LedgeGrab_21.mp3')
OUT = os.path.normpath(os.path.join(HERE, '../../../Assets/Resources/PawnRushSkillSfx/CourseLedgeGrab.wav'))
SR = 48000
LONGEST = .15   # the grab '햡' is .12 s; the catch has a second small tap

# bake_moves.py's helpers (opening, trim, write, short_db, prepared_db) without running its bake.
with open(os.path.join(HERE, 'bake_moves.py'), encoding='utf-8') as f:
    source = f.read().split('\nreport = {}')[0]
exec(compile(source, 'bake_moves.py', 'exec'))

d = miniaudio.decode_file(TAKE, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
x = np.asarray(d.samples, dtype=np.float64)
k = int(.01 * SR)
e = np.array([np.mean(x[i:i + k] ** 2) for i in range(0, len(x) - k, k)])
top = int(np.argmax(e)) * k
seg = x[max(0, top - int(.06 * SR)):top + int(.3 * SR)]
y, start = trim(seg, SR, LONGEST)
y *= 10 ** (-1 / 20) / np.abs(y).max()
write(OUT, y, SR)

kk = int(.3 * SR)
pad = np.concatenate([y, np.zeros(max(0, kk - len(y)))])
loud = np.sqrt(np.convolve(pad ** 2, np.ones(kk) / kk, 'valid').max())
prepare = min(8.0, 10 ** (-21 / 20) / max(loud, 1e-9), .98 / np.abs(y).max())
print('CourseLedgeGrab: hit at %.2f s of the take, %.3f s long, after Prepare %.1f dB (x%.2f)'
      % ((max(0, top - int(.06 * SR)) / SR) + start, len(y) / SR, prepared_db(y, SR), prepare))

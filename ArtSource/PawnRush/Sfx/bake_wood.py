"""2026-11-25: footsteps on a wooden beam - first the tutorial's plank bridge over the moat (user, on a screenshot of the
bridge: "외나무다리 이 소리가 아니야" -> the rope creak taken off, these steps instead). Take from the ElevenLabs flow
"튜토리얼 독수리 오프닝 소리" (92W4uG7w7gbSbvADmfh7, eleven_text_to_sound_v2, influence 0.3), the user's pick 42 (0.5 s), saved
as takes_wood/WoodWalk_42.mp3. Prompt:
  "Footsteps of a small light wooden toy figure walking at a steady quick pace along a thick solid wooden beam bridge:
   about nine separate steps, each one a short hollow woody knock "tok" on a thick plank, a little resonant like a log
   over water. Clean and close-mic, dry, no creaking, no squeaks, no water, no music, no voice."
The user picked no running take, so the run's steps are cut from the same take, shorter. Cut by bake_glass.py's own code
(its helpers, run here on these takes), every step matched to one short loudness, peak -1 dB:
  -> Assets/Resources/PawnRushStepSfx/WoodWalk0..n.wav, WoodRun0..n.wav (PawnRushStepSfx plays them on a WoodFloor).

  uv run --no-project --with miniaudio --with numpy python -I ArtSource/PawnRush/Sfx/bake_wood.py
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(HERE, 'bake_glass.py'), encoding='utf-8') as f:
    helpers = f.read().split('\nreport = {}')[0]
exec(compile(helpers, 'bake_glass.py', 'exec'))

TAKES = os.path.join(HERE, 'takes_wood')
# kind -> (take prefix, longest step in seconds, most kept)
KINDS = {'WoodWalk': ('WoodWalk', .14, 6), 'WoodRun': ('WoodWalk', .09, 6)}

report = {}
for kind, (prefix, longest, most) in KINDS.items():
    clips = []
    for fname in sorted(os.listdir(TAKES)):
        if fname.startswith(prefix + '_') and fname.endswith('.mp3'):
            clips += [(fname, t, y) for t, y in steps_in(load(os.path.join(TAKES, fname)), longest)]
    clips.sort(key=lambda c: -short_db(c[2]))
    clips = clips[:most]
    if not clips:
        raise SystemExit('no steps found for ' + kind)
    target = np.median([short_db(y) for _, _, y in clips])
    clips = [(f, t, y * 10 ** ((target - short_db(y)) / 20)) for f, t, y in clips]
    g = 10 ** (-1 / 20) / max(np.abs(y).max() for _, _, y in clips)
    for old in os.listdir(STEPS):
        if old.startswith(kind) and old[len(kind):len(kind) + 1].isdigit() and old.endswith('.wav'):
            os.remove(os.path.join(STEPS, old))
    for i, (f, t, y) in enumerate(clips):
        write(os.path.join(STEPS, '%s%d.wav' % (kind, i)), y * g)
    report[kind] = {'kept': [(f, round(t, 3), round(len(y) / SR, 3)) for f, t, y in clips],
                    'step_loudness_db': round(short_db(clips[0][2] * g), 1)}
print(json.dumps(report, indent=1, ensure_ascii=False))

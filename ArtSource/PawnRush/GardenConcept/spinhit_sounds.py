"""Royal water garden beat 1: the spin bars' hit sounds, one per bar size (R538).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I spinhit_sounds.py

승규 11-10 picked from the R537 listening page (ElevenLabs flow PaB2r5tvsfy80ziAKBO9, sfx_takes/R537): "맞을 때 소리는 큰 건 A로
해주고 작은 건 D로 해줘".
  HitA  "A heavy wooden log smacking into a wooden chess piece: one loud hard punchy wooden bonk ..."  -> the tall bar
  HitD  "Two hard polished wooden pieces clacking together hard: one bright dry loud wooden clack ..."  -> the low bars
Processed as plaza_sounds.py does a one-shot (rumble off, trimmed to the onset, peak -1 dB) and its far layer
-> Assets/Resources/PawnRushGardenSfx/SpinHitTall(.wav, Far.wav) and SpinHitSmall(.wav, Far.wav) (GardenSpinBarSound).
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
_src = open(os.path.join(HERE, "plaza_sounds.py"), encoding="utf-8").read()
_src = _src[:_src.rstrip().rfind("main()")]
_ns = {"__file__": os.path.join(HERE, "plaza_sounds.py"), "__name__": "plaza_sounds"}
exec(compile(_src, "plaza_sounds.py", "exec"), _ns)
takes = os.path.join(HERE, "sfx_takes", "R537")
for take, kind in (("HitA", "SpinHitTall"), ("HitD", "SpinHitSmall")):
    x = _ns["shot"](_ns["decode"](os.path.join(takes, take + ".mp3")))
    _ns["write"](os.path.join(_ns["OUT"], kind + ".wav"), x)
    _ns["write"](os.path.join(_ns["OUT"], kind + "Far.wav"), _ns["far"](x, False))

# R539 (승규 11-10, "큰 막대는 F로 해주고 작은 막대는 지금 소리로 해줘"): the tall bar's turning loop is the second round's F
# ("A big old merry-go-round platform turning with no music: soft steady low mechanical rumble of the turntable bearing
# with gentle rhythmic wooden clunks each revolution ..."), processed as plaza_sounds.py does a loop (seam crossfaded,
# levelled to the hub's -19 dBFS RMS) -> SpinHubTall(.wav, Far.wav). The low bars keep SpinHub.
loop = _ns["level"](_ns["seamless"](_ns["highpass"](_ns["decode"](os.path.join(takes, "SpinF.mp3")), 30)), -19.0)
_ns["write"](os.path.join(_ns["OUT"], "SpinHubTall.wav"), loop)
_ns["write"](os.path.join(_ns["OUT"], "SpinHubTallFar.wav"), _ns["far"](loop, True))

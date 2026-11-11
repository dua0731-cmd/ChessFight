"""Royal water garden beat 3 (the bobbing drums and the shortcut's lotus pads): their sounds (R536).

    uv run --no-project --python 3.12 --with miniaudio --with numpy python -I float_sounds.py              (writes)
    uv run ... python -I float_sounds.py --report / --pick PadSplash=2

승규 11-10: "다음 사운드" (after the plaza R530 and the geysers R534). The takes come from the ElevenLabs flow
"ChessFight geyser burst R534" (PaB2r5tvsfy80ziAKBO9), two more sfx nodes x 2 takes, saved as
sfx_takes/R536/<Kind>_<take>.mp3:
  FloatSlosh  6 s loop  "A large floating wooden drum slowly rising and sinking in calm canal water: deep soft sloshing
                         and gulping water against hollow wood, gentle wet slaps and low gurgles, continuous ..."
  PadSplash   1.2 s     "A small round wooden raft tipping sharply into a pond: one loud quick slap and splash of water,
                         short wet sploosh ..."
Chosen and processed exactly as plaza_sounds.py does (its checks, its loop seam and level, its one-shot trim, its far
layer) -> Assets/Resources/PawnRushGardenSfx/<Kind>.wav + <Kind>Far.wav (GardenFloatSound).
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
_src = open(os.path.join(HERE, "plaza_sounds.py"), encoding="utf-8").read()
_src = _src[:_src.rstrip().rfind("main()")]
_ns = {"__file__": os.path.join(HERE, "plaza_sounds.py"), "__name__": "plaza_sounds"}
exec(compile(_src, "plaza_sounds.py", "exec"), _ns)
_ns["TAKES"] = os.path.join(HERE, "sfx_takes", "R536")
_ns["KINDS"] = {"FloatSlosh": (True, -21.0), "PadSplash": (False, None)}
_ns["main"]()

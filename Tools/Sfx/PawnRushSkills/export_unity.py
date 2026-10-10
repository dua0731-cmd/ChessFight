# export_unity.py: writes the sounds 승규 님 picked on the R107 page as the clips the game plays (R114).
#   SPEC=spec_v2 python export_unity.py <Resources/PawnRushSkillSfx folder>
# Picks (10-10): pawn = the Queen of the Hill pawn's own files (played from Resources/QueenHillSkillSfx, nothing
# written here), queen A, rook A, bishop B, knight E. Each clip is the cue as build.py prepares it (trimmed, the
# first hit only for one-hit moments, cut or low-passed where spec_v2 says), re-levelled to -16, as a 44.1 kHz mono
# mp3 named after the moment; PawnRushSkillSfx.cs plays them by these names.
import os, subprocess, sys
import numpy as np
from build import SPEC, SR, FF, SINGLE, load, trim, dc_block, first_event, level, limit, pitch, lowpass, gen_path

PICKS = {"queen": "A", "rook": "A", "bishop": "B", "knight": "E"}
NAMES = {
    ("queen", "windup"): "QueenWindup", ("queen", "blast"): "QueenBlast",
    ("rook", "tick"): "RookTick", ("rook", "lock"): "RookLock", ("rook", "dash"): "RookDash",
    ("rook", "hit"): "RookHit", ("rook", "wall"): "RookWall",
    ("bishop", "throw"): "BishopThrow", ("bishop", "land"): "BishopLand", ("bishop", "arm"): "BishopArm",
    ("bishop", "hum"): "BishopHum", ("bishop", "trip"): "BishopTrip", ("bishop", "end"): "BishopEnd",
    ("knight", "leap"): "KnightLeap", ("knight", "turn"): "KnightTurn", ("knight", "land"): "KnightLand",
    ("knight", "stomp"): "KnightStomp", ("knight", "daze"): "KnightDaze", ("knight", "air"): "KnightAir",
}
# The queen's windup is 0.35 s in the game (F to the blast): only the take's last 0.4 s, rising into the blast.
TAIL = {("queen", "windup"): 0.40}


def cue(skill, d, name):
    cut = getattr(SPEC, "DERIVE", {}).get((skill, d, name))
    x = trim(dc_block(load(gen_path(d, skill, cut[0] if cut else name))))
    fc = getattr(SPEC, "LOWPASS", {}).get((skill, d, name))
    if fc:
        x = lowpass(x, fc)
    if cut:
        x = x[:int(cut[2] * SR)].copy()
        x[-int(0.03 * SR):] *= np.linspace(1, 0, int(0.03 * SR))
        x = pitch(x, cut[3])
    if name in SINGLE:
        x = trim(first_event(x))
    keep = TAIL.get((skill, name))
    if keep and len(x) > keep * SR:
        x = x[-int(keep * SR):].copy()
        x[:int(0.03 * SR)] *= np.linspace(0, 1, int(0.03 * SR))
    return limit(level(x, -16.0))


def main(folder):
    os.makedirs(folder, exist_ok=True)
    for skill, d in PICKS.items():
        for name in SPEC.SKILLS[skill]["cues"]:
            x = np.clip(cue(skill, d, name), -1, 1).astype(np.float32)
            out = os.path.join(folder, NAMES[(skill, name)] + ".mp3")
            subprocess.run([FF, "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
                            "-ar", "44100", "-c:a", "libmp3lame", "-b:a", "160k", out], input=x.tobytes(), check=True)
            print(f"{skill} {d} {name} -> {os.path.basename(out)} {len(x) / SR:.2f}s")


if __name__ == "__main__":
    main(sys.argv[1])

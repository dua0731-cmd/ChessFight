"""
Draws the pawn's TARGET pose (what RagdollPawn.Pose/Locomotion ask the joints for, not the physics
result) from behind and from the side, using the values in Settings/RagdollTuning.asset, and writes
run_preview.gif and sprint_preview.gif. Needs numpy, scipy and pillow:

    python3 Tools/RagdollPreview/preview.py [out_dir]

gait.py is a hand port of the gait maths in RagdollPawn.cs; keep it in step when that changes.
"""
import os, re, sys
from PIL import Image, ImageDraw
import model
from gait import CURRENT, frames

ASSET = os.path.join(model.ROOT, 'Assets', 'ChessFight', 'RagdollLab', 'Settings', 'RagdollTuning.asset')

def load_params():
    vals = {}
    for line in open(ASSET, encoding='utf-8'):
        m = re.match(r'    (\w+): ([-0-9.eE]+)$', line)
        if m: vals[m.group(1)] = float(m.group(2))
    P = dict(CURRENT)
    P.update({k: vals[k] for k in list(P) if k in vals})
    P.update(runLegDrop=vals.get('runLegDrop', 0.0), bobStyle='legs', armStyle='pendulum')
    return P

def gif(P, blend, cadence, title, path, cycles=2):
    n = int(round(30 / cadence)) * cycles
    back, _ = frames(P, blend, seconds=n / 30, view='back')
    side, _ = frames(P, blend, seconds=n / 30, view='side')
    out = []
    for b, s in zip(back, side):
        im = Image.new('RGB', (b.width * 2, b.height + 26), 'white')
        im.paste(b, (0, 26)); im.paste(s, (b.width, 26))
        ImageDraw.Draw(im).text((8, 7), title, fill=(20, 20, 20))
        out.append(im)
    out[0].save(path, save_all=True, append_images=out[1:], duration=33, loop=0)

if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(out, exist_ok=True)
    P = load_params()
    gif(P, 0.0, P['hopCadence'], f"RUN {P['moveSpeed']} m/s - back / side (target pose)", os.path.join(out, 'run_preview.gif'))
    gif(P, 1.0, P['sprintCadence'], f"SPRINT {P['sprintSpeed']} m/s - back / side (target pose)", os.path.join(out, 'sprint_preview.gif'))

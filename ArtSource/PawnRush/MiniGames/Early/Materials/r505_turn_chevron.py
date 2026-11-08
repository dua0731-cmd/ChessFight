# D capstan turn arrow (R505, user 11-08: "바닥에 그려지는 t 자형 빼주고 돌리라는 화살표 표시를 vfx 로 해"): a soft white
# chevron pointing up (+V) on black, for the additive particle material (colour and alpha are both the shape, so the
# black adds nothing). MiniGameTurnArrows lays 12 of them flat round the capstan and runs them the way it turns.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r505_turn_chevron.py
import bpy, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "..", "Assets", "Resources", "PawnRushMiniGames", "Fx", "T_Turn_Chevron.png"))
N = 128
y, x = (np.mgrid[0:N, 0:N].astype(np.float32) + .5) / N        # 0..1, y down in the array
u, v = x - .5, 1 - y                                            # v up
# chevron: two arms meeting at the top (v .82), opening downward to v .30 at u +-.36
def arm(sign):
    ax, ay, bx, by = 0., .82, sign * .36, .30
    dx, dy = bx - ax, by - ay
    t = np.clip(((u - ax) * dx + (v - ay) * dy) / (dx * dx + dy * dy), 0, 1)
    return np.hypot(u - (ax + t * dx), v - (ay + t * dy))
d = np.minimum(arm(-1), arm(1))
core = np.clip((.085 - d) / .03, 0, 1)                           # the stroke, soft edge
glow = np.clip(1 - d / .22, 0, 1) ** 2 * .35                     # a faint halo
a = np.clip(core + glow, 0, 1)
img = bpy.data.images.new("T_Turn_Chevron", N, N, alpha=True)
px = np.dstack([a, a, a, a]).astype(np.float32)[::-1]
img.pixels.foreach_set(px.ravel())
img.filepath_raw = OUT; img.file_format = 'PNG'; img.save()
print("saved", OUT)

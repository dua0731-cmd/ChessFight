"""Pawn Rush library (작전 회의실): the great door's blue lunette in the cannons' teal paint (R414).

    blender --background --factory-startup --python door_teal.py                        (rewrites the door map)
    blender --background --factory-startup --python door_teal.py -- --out D:/check/x.png (writes a copy instead)

승규 님 (11-05, the goal door's lunette): "깃발 엔딩 구간 문 색을 지금 파란색보다 대포에서 쓰이는 초록색 컬러로 교체".
The door concept (Assets/Resources/PawnRushLibrary/Door/LibraryDoor.png, PR_LibraryDoor) has a navy half-moon behind
the gold sun. Every blue pixel (blue over red by more than BLUE_MIN, fully from BLUE_FULL) takes the cannon's teal
(Course01v4Builder.LibraryLooks "Library teal", sRGB .30 .44 .47), its brightness relative to the navy's average kept,
so the painted shading, the rivets and the thin ray lines stay. Gold, wood, stone and the grey frame are untouched.
Running it again changes nothing (no blue is left).
"""
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
DOOR = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary", "Door",
                                     "LibraryDoor.png"))
TEAL = np.array((.30, .44, .47))          # sRGB, as Unity's "Library teal"
BLUE_MIN, BLUE_FULL = .04, .12
LUM = np.array((.2126, .7152, .0722))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[argv.index("--out") + 1] if "--out" in argv else DOOR
    img = bpy.data.images.load(DOOR)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rgb = px[..., :3]
    blue = np.clip((rgb[..., 2] - rgb[..., 0] - BLUE_MIN) / (BLUE_FULL - BLUE_MIN), 0, 1)
    core = blue > .99
    if not core.any():
        print("door: no blue left")
        return
    lum = rgb @ LUM
    ref = lum[core].mean()
    teal = np.clip(TEAL[None, None, :] * (lum / ref)[..., None], 0, 1)
    rgb[:] = rgb * (1 - blue[..., None]) + teal * blue[..., None]
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
    print(f"door: {int((blue > 0).sum())} pixels toward teal ({int(core.sum())} fully), navy mean lum {ref:.3f} -> {out}")


main()

"""Pawn Rush map kit v3: the underside of the floating course (concept art 2: every platform is a
chunk of castle floating in the sky, with a stepped block-stone base under it).

blender --background --factory-startup --python build_parts_v3.py -- <out_dir>
PR_Foundation: 4 x 4 m footprint, top face at 0, stepped down to -5 m; Unity stretches it per platform.
"""
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_parts_v1 as k  # noqa: E402


def foundation(name="PR_Foundation"):
    k.reset()
    rng = random.Random(23)
    objs = []
    tiers = [(4.0, 1.1), (3.5, 1.1), (2.8, 1.1), (2.0, 1.0), (1.1, 0.7)]
    top = 0.0
    for ti, (size, h) in enumerate(tiers):
        n = 2 if size > 1.5 else 1
        cell = size / n
        for i in range(n):
            for j in range(n):
                dh = rng.uniform(-0.15, 0.1) if ti else 0.0
                inset = rng.uniform(0.0, 0.12) * (1 if ti else 0)
                sx = cell - 0.04 - inset
                sz = h + dh
                cx = -size / 2 + cell * (i + 0.5) + rng.uniform(-0.05, 0.05) * (1 if ti else 0)
                cy = -size / 2 + cell * (j + 0.5) + rng.uniform(-0.05, 0.05) * (1 if ti else 0)
                tone = "Stone_CreamDark" if (i + j + ti) % 2 == 0 else "Stone_Shadow"
                objs.append(k.box(f"t{ti}_{i}{j}", (sx, sx, sz), (cx, cy, top - sz / 2), tone, bevel=0.06))
        top -= h
    part = k.join(objs, name)
    k.export(part, name)


if __name__ == "__main__":
    foundation()
    print("DONE", k.OUT)

"""Pawn Rush map kit v4: obstacle skins (item 4 of the 10-17 list).

blender --background --factory-startup --python build_parts_v4.py -- <out_dir>
Unity's Skin() stretches each part to the grey-box piece it covers, so sizes here only set proportions.
PR_Column: round stone post (spinning disc posts).  PR_HammerHead: stone mallet head with gold bands.
PR_DoorPanel: plank door with iron straps and gold studs (clock gates).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_parts_v1 as k  # noqa: E402
import bpy  # noqa: E402

k.PALETTE.setdefault("Iron", (0.22, 0.22, 0.24, 1.0, 0.8, 0.45))
k.PALETTE.setdefault("Wood", (0.42, 0.28, 0.16, 1.0, 0.0, 0.7))
k.PALETTE.setdefault("Stone_Charcoal", (0.16, 0.16, 0.18, 1.0, 0.0, 0.5))


def cyl(name, radius, depth, z, material, verts=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=(0, 0, z))
    o = bpy.context.active_object
    o.name = name
    mod = o.modifiers.new("bevel", "BEVEL")
    mod.width = min(0.04, depth * 0.2)
    mod.segments = 1
    mod.limit_method = "ANGLE"
    bpy.ops.object.modifier_apply(modifier="bevel")
    o.data.materials.append(k.mat(material))
    return o


def column(name="PR_Column"):
    """1.2 m wide, 4 m tall round post: wide base, banded drum shaft, flared cap."""
    k.reset()
    objs = [cyl("base", 0.75, 0.35, 0.175, "Stone_CreamDark"), cyl("plinth", 0.68, 0.2, 0.45, "Stone_Cream")]
    z = 0.55
    i = 0
    while z < 3.45:
        h = min(0.5, 3.45 - z)
        objs.append(cyl(f"d{i}", 0.6 - (0.02 if i % 2 else 0), h - 0.03, z + h / 2,
                        "Stone_Cream" if i % 2 == 0 else "Stone_CreamDark"))
        z += h
        i += 1
    objs.append(cyl("neck", 0.66, 0.15, 3.525, "Gold"))
    objs.append(cyl("cap", 0.75, 0.4, 3.8, "Stone_CreamDark"))
    part = k.join(objs, name)
    k.export(part, name)


def hammer_head(name="PR_HammerHead"):
    """Mallet head 2 x 1 x 1 (length along x): charcoal stone block, gold bands near both ends, gold faces."""
    k.reset()
    objs = [k.box("core", (1.6, 0.9, 0.9), (0, 0, 0.45), "Stone_Charcoal", bevel=0.08)]
    for s in (-1, 1):
        objs.append(k.box(f"band{s}", (0.14, 0.98, 0.98), (s * 0.55, 0, 0.45), "Gold", bevel=0.03))
        objs.append(k.box(f"face{s}", (0.2, 1.0, 1.0), (s * 0.9, 0, 0.45), "Gold", bevel=0.06))
    part = k.join(objs, name)
    k.export(part, name)


def door_panel(name="PR_DoorPanel"):
    """Door 1 x 1 x 0.2 (x wide, z up, y thin): five planks, two iron straps, gold studs."""
    k.reset()
    objs = []
    n = 5
    for i in range(n):
        w = 1.0 / n
        objs.append(k.box(f"p{i}", (w - 0.01, 0.16, 1.0), (-0.5 + w * (i + 0.5), 0, 0.5), "Wood", bevel=0.01))
    for j, z in enumerate((0.22, 0.78)):
        objs.append(k.box(f"strap{j}", (1.0, 0.2, 0.07), (0, 0, z), "Iron", bevel=0.01))
        for i in range(n):
            objs.append(k.box(f"stud{j}{i}", (0.04, 0.22, 0.04), (-0.5 + (i + 0.5) / n, 0, z), "Gold", bevel=0.0))
    part = k.join(objs, name)
    k.export(part, name)


if __name__ == "__main__":
    column()
    hammer_head()
    door_panel()
    print("DONE", k.OUT)

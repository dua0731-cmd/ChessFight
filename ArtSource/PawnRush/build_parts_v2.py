"""Pawn Rush map kit v2: skins for the moving devices (start bar, spin bar + hub).

blender --background --factory-startup --python build_parts_v2.py -- <out_dir>
Same conventions as build_parts_v1 (1 unit = 1 m, pivot bottom centre, FBX -Z forward / Y up).
Bars are built at the exact size of the grey-box collider they skin, so Unity scales them by 1.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_parts_v1 as k  # noqa: E402  (reads <out_dir> from argv too)
import bpy  # noqa: E402

k.PALETTE.setdefault("Iron", (0.22, 0.22, 0.24, 1.0, 0.8, 0.45))


def stripe_bar(length, height, depth, step, name):
    """Red/white striped hazard bar with gold end caps (concept art 2 edge colours)."""
    k.reset()
    objs = []
    cap = 0.15
    inner = length - 2 * cap
    n = max(2, int(round(inner / step)))
    seg = inner / n
    for i in range(n):
        m = "Trim_Red" if i % 2 == 0 else "Trim_White"
        objs.append(k.box(f"s{i}", (seg - 0.01, depth, height), (-inner / 2 + seg * (i + 0.5), 0, height / 2), m,
                          bevel=min(0.04, height * 0.12)))
    for s in (-1, 1):
        objs.append(k.box(f"cap{s}", (cap, depth + 0.04, height + 0.04), (s * (length / 2 - cap / 2), 0, height / 2),
                          "Gold", bevel=0.02))
    part = k.join(objs, name)
    k.export(part, name)


def spin_hub(radius=0.425, height=0.45, name="PR_SpinHub"):
    """Gold rook-head drum for the spin bar's pivot: 8-sided body, 4 merlons on top."""
    k.reset()
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=radius, depth=height * 0.75, location=(0, 0, height * 0.375))
    body = bpy.context.active_object
    body.data.materials.append(k.mat("Gold"))
    objs = [body]
    for i in range(4):
        a = math.radians(45 + 90 * i)
        objs.append(k.box(f"m{i}", (radius * 0.5, radius * 0.5, height * 0.25),
                          (math.cos(a) * radius * 0.62, math.sin(a) * radius * 0.62, height * 0.875), "Gold", bevel=0.015))
    part = k.join(objs, name)
    k.export(part, name)


def portcullis(width=4.0, height=4.6, depth=0.4, name="PR_Portcullis_4"):
    """Team gate skin: square bars with pointed feet and a gold crown band on top.
    Bars use the 'Iron' slot; Unity swaps it for the team colour (white / black)."""
    k.reset()
    objs = []
    bar = 0.14
    n_v = 7
    for i in range(n_v):
        x = -width / 2 + bar / 2 + i * (width - bar) / (n_v - 1)
        objs.append(k.box(f"v{i}", (bar, depth * 0.6, height - 0.35), (x, 0, (height - 0.35) / 2 + 0.2), "Iron", bevel=0.02))
        bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=bar * 0.75, depth=0.25, location=(x, 0, 0.1),
                                        rotation=(math.pi, 0, math.radians(45)))
        spike = bpy.context.active_object
        spike.data.materials.append(k.mat("Iron"))
        objs.append(spike)
    for j, z in enumerate((1.2, 2.4, 3.5)):
        objs.append(k.box(f"h{j}", (width, depth * 0.5, bar), (0, 0, z), "Iron", bevel=0.02))
    objs.append(k.box("band", (width, depth, 0.35), (0, 0, height - 0.175), "Gold", bevel=0.03))
    part = k.join(objs, name)
    k.export(part, name)


if __name__ == "__main__":
    stripe_bar(12.0, 0.32, 0.45, 0.75, "PR_StripeBar_12")
    stripe_bar(16.0, 1.2, 0.5, 1.0, "PR_StartBar_16")
    spin_hub()
    portcullis()
    print("DONE", k.OUT)

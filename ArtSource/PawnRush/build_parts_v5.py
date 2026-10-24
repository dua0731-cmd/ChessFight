"""Pawn Rush map kit v5: the open-world look (승규 10-22: "오픈 월드처럼, 하늘 다리").

blender --background --factory-startup --python build_parts_v5.py -- <out_dir>
PR_PlankBridge: 12 m rope-and-plank bridge (Blender +Y = Unity +Z), deck sags 0.9 m at mid-span;
                pivot at the span's centre line at deck-end height, so Unity can stretch it between two rims.
PR_TowerRoof:   pointed navy roof with a gold rim and finial for the 4 m kit tower (pivot at its base).
PR_StoneBridge: 12 m stone arch bridge with a parapet, for heavier links between big islands.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_parts_v1 as k  # noqa: E402
import bpy  # noqa: E402

k.PALETTE.setdefault("Wood", (0.42, 0.28, 0.16, 1.0, 0.0, 0.7))
k.PALETTE.setdefault("Rope", (0.62, 0.50, 0.32, 1.0, 0.0, 0.9))
k.PALETTE.setdefault("Roof_Navy", (0.13, 0.22, 0.48, 1.0, 0.0, 0.45))


def sag(y, length, depth):
    """Deck height at y (y in -L/2..L/2): a parabola 0 at the ends, -depth mid-span."""
    t = y / (length / 2)
    return -depth * (1 - t * t)


def rod(name, a, b, radius, material, verts=10):
    """Cylinder from point a to point b."""
    from mathutils import Vector
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=d.length, location=(a + b) / 2)
    o = bpy.context.active_object
    o.name = name
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    bpy.ops.object.transform_apply(rotation=True)
    o.data.materials.append(k.mat(material))
    return o


def plank_bridge(length=12.0, width=2.4, depth=0.9, name="PR_PlankBridge"):
    k.reset()
    rng = random.Random(5)
    objs = []
    step = 0.32
    n = int(length / step)
    for i in range(n + 1):
        y = -length / 2 + i * length / n
        z = sag(y, length, depth)
        w = width + rng.uniform(-0.12, 0.05)
        p = k.box(f"plank{i}", (w, step * 0.82, 0.08), (rng.uniform(-0.05, 0.05), y, z - 0.04),
                  "Wood" if i % 3 else "Stone_CreamDark" if False else "Wood", bevel=0.015)
        p.rotation_euler = (math.atan2(2 * depth * y / (length / 2) ** 2, 1), rng.uniform(-0.03, 0.03), rng.uniform(-0.04, 0.04))
        objs.append(p)
    # two stringers under the planks and two hand ropes, in short segments along the sag
    seg = 16
    for side in (-1, 1):
        for j in range(seg):
            y0 = -length / 2 + j * length / seg
            y1 = y0 + length / seg
            objs.append(rod(f"str{side}{j}", (side * (width / 2 - 0.15), y0, sag(y0, length, depth) - 0.1),
                            (side * (width / 2 - 0.15), y1, sag(y1, length, depth) - 0.1), 0.05, "Wood"))
            r0 = sag(y0, length, depth * 0.55) + 1.0
            r1 = sag(y1, length, depth * 0.55) + 1.0
            objs.append(rod(f"rope{side}{j}", (side * width / 2, y0, r0), (side * width / 2, y1, r1), 0.035, "Rope"))
            # hangers from rope to deck every segment
            objs.append(rod(f"hang{side}{j}", (side * width / 2, y0, r0), (side * width / 2, y0, sag(y0, length, depth)), 0.015, "Rope", 6))
        for end in (-1, 1):
            objs.append(rod(f"post{side}{end}", (side * width / 2, end * length / 2, -0.2), (side * width / 2, end * length / 2, 1.25), 0.09, "Wood", 10))
    part = k.join(objs, name)
    k.export(part, name)


def tower_roof(name="PR_TowerRoof"):
    """For the 4.3 m wide kit tower: overhanging cone, gold rim, gold ball and spike."""
    k.reset()
    objs = []
    bpy.ops.mesh.primitive_cone_add(vertices=48, radius1=2.75, radius2=0.05, depth=4.2, location=(0, 0, 2.1 + 0.25))
    cone = bpy.context.active_object
    cone.data.materials.append(k.mat("Roof_Navy"))
    objs.append(cone)
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=2.8, depth=0.25, location=(0, 0, 0.125))
    rim = bpy.context.active_object
    rim.data.materials.append(k.mat("Gold"))
    objs.append(rim)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.22, location=(0, 0, 4.55))
    ball = bpy.context.active_object
    ball.data.materials.append(k.mat("Gold"))
    objs.append(ball)
    bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=0.08, radius2=0.0, depth=0.9, location=(0, 0, 5.15))
    spike = bpy.context.active_object
    spike.data.materials.append(k.mat("Gold"))
    objs.append(spike)
    part = k.join(objs, name)
    k.export(part, name)


def stone_bridge(length=12.0, width=3.0, name="PR_StoneBridge"):
    """Deck at z 0, an arch underneath (segments of blocks), parapets 0.9 m."""
    k.reset()
    objs = []
    n = 24
    for i in range(n):
        y0 = -length / 2 + i * length / n
        yc = y0 + length / n / 2
        t = yc / (length / 2)
        under = 0.6 + 1.6 * (t * t)  # thicker toward the ends: an arch
        tone = "Stone_Cream" if i % 2 == 0 else "Stone_CreamDark"
        objs.append(k.box(f"deck{i}", (width, length / n - 0.02, under), (0, yc, -under / 2), tone, bevel=0.04))
        for side in (-1, 1):
            objs.append(k.box(f"par{side}{i}", (0.35, length / n - 0.02, 0.9), (side * (width / 2 - 0.175), yc, 0.45),
                              "Stone_Cream" if (i + (side > 0)) % 2 else "Stone_CreamDark", bevel=0.04))
    part = k.join(objs, name)
    k.export(part, name)


if __name__ == "__main__":
    plank_bridge()
    tower_roof()
    stone_bridge()
    print("DONE", k.OUT)

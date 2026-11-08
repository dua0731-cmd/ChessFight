# B East Wing room shell (R503): the code-built R312 shell is removed for B (user, 2026-11-07: "기존에 있던 구조물
# 치우고 다시 재배치 해", walls must run on without gaps). The models (ground and upper side modules, gates, arches,
# corridor modules) make the walls; this script makes the plain fill that closes every remaining gap up to the
# 13 m ceiling, in one continuous cream ashlar (PR_Mid_AshlarFill = the phase-1 cream stone brick tile, 2 m), and the
# walnut-board ceiling (PR_Mid_WalnutFloor, 2 m tile). Colliders stay the game's (MiniGameSectionBuilder).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python b_shell_v1.py -- [--render] [--export]
#
# Output Export/B_ShellEnv.fbx in the section frame (Blender x = section x, y = section z, z = up).
# Openings it keeps clear: the entry (x +-6.2 under the entry arch), the exit (same at z 36.3), the two team-gate
# tunnels (x +-(9.78..14.22), 0..6.46 m, through the north block z 24..30) and the windows of the models.
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import b_core_scene_v1 as cs  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TOP = 13.
TUNNEL = (9.78, 14.22, 6.46)          # team portal tunnel |x| span and ceiling (the arch's timber cross beam)


def measure():
    """Inner apex of the grand arch and the team arch, from the exported models."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(cs.OUT_FBX, "B_UpperEnv.fbx"))
    up = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    p = np.array([up.matrix_world @ v.co for v in up.data.vertices])
    near = p[(np.abs(p[:, 0]) < .4) & (np.abs(p[:, 1] - (-.4)) < .9) & (p[:, 2] > 3.)]
    grand = float(near[:, 2].min())
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(cs.OUT_FBX, "B_TeamArch.fbx"))
    ar = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    q = np.array([ar.matrix_world @ v.co for v in ar.data.vertices])
    team = float(q[(np.abs(q[:, 0]) < .4) & (q[:, 2] > 7.)][:, 2].min())
    jamb = float(np.abs(q[(q[:, 2] > 7.5) & (q[:, 2] < 8.5)][:, 0]).min())
    print("MEASURE grand arch inner apex %.2f, team arch inner apex %.2f, jamb inner |x| %.2f" % (grand, team, jamb))
    return grand, team, jamb


def fill_material():
    m = bpy.data.materials.new("PR_Mid_AshlarFill"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(os.path.join(HERE, "..", "Early", "Materials", "Textures", "PR_Early_StoneBrick_BaseColor.png"))
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = .85
    return m


def boxes(specs, mat, name):
    """Axis boxes (x0, x1, y0, y1, z0, z1) in the section frame (y = section z, z = up), box UVs on a 2 m tile."""
    bm = bmesh.new(); uv = bm.loops.layers.uv.new("map1")
    for x0, x1, y0, y1, z0, z1 in specs:
        r = bmesh.ops.create_cube(bm, size=1.)
        for v in r["verts"]:
            v.co = Vector((x0 if v.co.x < 0 else x1, y0 if v.co.y < 0 else y1, z0 if v.co.z < 0 else z1))
        for f in {f for v in r["verts"] for f in v.link_faces}:
            n = f.normal
            for l in f.loops:
                p = l.vert.co
                l[uv].uv = (p.x / 2, p.y / 2) if abs(n.z) > .5 else ((p.y / 2, p.z / 2) if abs(n.x) > .5 else (p.x / 2, p.z / 2))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def main():
    grand, team, jamb = measure()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    stone = fill_material()
    t0, t1, tc = TUNNEL
    specs = []
    # North facade: the plane sits at z 25.0, behind the team arches' stone rings (their front parts reach z 23.8..
    # ~24.5) and inside the centre gate's depth, so every model stands in front of it. Pieces only meet edge to edge
    # (no two front faces overlap in one plane: no flicker). Split height B = the arches' impost line.
    nf = (25.0, 25.5)
    B = team - .3
    specs += [(-22.3, 22.3, *nf, B, TOP)]                                      # band over everything above B
    for s in (-1, 1):
        a, b = sorted((s * 0., s * t0)); specs += [(a, b, *nf, 0., B)]          # centre to tunnel (gate in front)
        a, b = sorted((s * t1, s * 22.3)); specs += [(a, b, *nf, 0., B)]       # outer span (modules in front)
        a, b = sorted((s * t0, s * t1)); specs += [(a, b, *nf, tc, B)]         # inside the arch, over the beam
        # the tunnel through the block: two side walls and a ceiling
        for x in (t0, t1):
            a, b = sorted((s * (x - .25 if x == t0 else x), s * (x if x == t0 else x + .25)))
            specs += [(a, b, 24.0, 30.0, 0., tc)]
        a, b = sorted((s * t0, s * t1)); specs += [(a, b, 24.0, 30.0, tc, tc + .35)]
    # Behind the north corridor modules outside the team arches (x +-16.3..22, front z 24.0, only ~3 cm deep) the
    # facade stood 1 m back at z 25.0, leaving a slot open from the module top up (user, 2026-11-08: "벽뒤에 공간이
    # 있잖아"). A block from the module back to the facade, floor to ceiling, closes it; above the 4.4 m modules its
    # face (z 24.04) carries the wall straight up.
    for s in (-1, 1):
        a, b = sorted((s * 16.3, s * 22.6)); specs += [(a, b, 24.04, 25.0, 0., TOP)]
    # North block back (room face z 29.95 toward the rear corridor), tunnel rears kept open
    nb = (29.4, 29.95)
    specs += [(-22.3, 22.3, *nb, tc, TOP)]
    specs += [(-t0, t0, *nb, 0., tc)]
    for s in (-1, 1):
        a, b = sorted((s * t1, s * 22.3)); specs += [(a, b, *nb, 0., tc)]
    # Entry (room face z -0.05) and exit (room face z 35.95): wings full height, above the arch opening
    for zf, zb in ((-.6, -.05), (35.95, 36.6)):
        for s in (-1, 1):
            a, b = sorted((s * 6.2, s * 22.3)); specs += [(a, b, zf, zb, 0., TOP)]
        specs += [(-6.2, 6.2, zf, zb, grand - .2, TOP)]
    # Side walls behind every module, full height, z -0.6..36.6 (modules and the gallery stand in front), with a hole
    # behind each window so the windows look outside (user, 2026-11-07: "창문 밖이 보여야지"; no glass in the models).
    # Ground windows: module centres, +-1.1 m, 1.2..4.5 m; upper windows: +-1.0 m, 6.8..10.6 m. Pieces meet edge to edge.
    centres = (2., 8., 14., 20., 33.)
    bands = [(0., 1.2, None), (1.2, 4.5, 1.1), (4.5, 6.8, None), (6.8, 10.6, 1.0), (10.6, TOP, None)]
    for s in (-1, 1):
        a, b = sorted((s * 22.6, s * 23.0))
        for y0, y1, hw in bands:
            cuts = [-.6] + [z for c in centres for z in (c - hw, c + hw)] + [36.6] if hw else [-.6, 36.6]
            for z0, z1 in zip(cuts[0::2], cuts[1::2]):
                specs += [(a, b, z0, z1, y0, y1)]
    walls = boxes(specs, stone, "fill")
    # Ceiling: a 0.4 m walnut-board slab (13..13.4 m) over the whole room and the wall tops, so it reads as boards
    # from below and as a closed roof from above (user, 2026-11-07: "위에서도 천장 보이게 해"; a single down-facing
    # plane vanished when seen from above)
    wood = bpy.data.materials.new("PR_Mid_WalnutFloor")
    ceil = boxes([(-23., 23., -.6, 36.6, TOP, TOP + .4)], wood, "ceiling")
    # Aisle runner border: two straight matte-brass strips along the central aisle's edges (x +-1.86..2.0, z 4..24),
    # 1.5 cm proud of the floor (the concept's runner border; replaces the askew Tripo walkway tiles)
    brass = bpy.data.materials.new("PR_Mid_MatteBrass")
    strips = boxes([(-2.0, -1.86, 4.0, 24.0, -.05, .015), (1.86, 2.0, 4.0, 24.0, -.05, .015)], brass, "aisle strips")
    # Floor gaps between the game's floors and the new walls (user, 2026-11-07: "바닥 빈틈 없이 해"): the team-gate
    # tunnels are 4.44 m between their stone walls but the game's gate passage floor is 4 m (x +-10..14), and the
    # station floors end at x +-22 while the side fill wall stands at +-22.6. Slate strips fill only the gaps, edge to
    # edge with the game floors (top 0, no shared plane), 1 m deep like the game's slabs.
    slate = bpy.data.materials.new("PR_Mid_SlateFloor")
    gaps = []
    for s in (-1, 1):
        for a, b in ((t0 - .3, 10.), (14., t1 + .3), (22., 22.7)):
            x0, x1 = sorted((s * a, s * b))
            z0, z1 = (24., 30.) if a < 20. else (-.6, 36.6)
            gaps.append((x0, x1, z0, z1, -1., 0.))
    floor_fill = boxes(gaps, slate, "floor gaps")
    shell = cs.join([walls, ceil, strips, floor_fill], "B_ShellEnv")
    print("SHELL boxes", len(specs), "tris", cs.tris(shell))
    if "--export" in ARGS: cs.export(shell, "B_ShellEnv")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "B_Shell_v1.blend"))


main()

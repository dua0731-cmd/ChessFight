# R508 F ballroom: inspect the two Tripo FBX (frames/spotlight sheet, props sheet).
# Imports each, reports the mesh, bounds and triangles, splits the scattered assets into parts by
# connected pieces grouped along x (the sheet's row), prints each part's triangles and size, and renders
# a front view with the parts coloured so mixed-up parts are visible.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python f_inspect_v1.py
import bpy, bmesh, os, math
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source")
SETS = [("Frames", 3), ("Props", 5)]
COLS = [(.9, .3, .25), (.25, .6, .9), (.3, .8, .35), (.9, .75, .2), (.7, .35, .85), (.2, .8, .8)]


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def tris(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def islands(ob):
    """Connected vertex groups -> list of (min, max, vert indices)."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.verts.ensure_lookup_table()
    seen, out = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, idx = [v], []
        seen.add(v.index)
        while stack:
            a = stack.pop()
            idx.append(a.index)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen:
                    seen.add(b.index)
                    stack.append(b)
        cos = [ob.matrix_world @ bm.verts[i].co for i in idx]
        lo = Vector((min(c.x for c in cos), min(c.y for c in cos), min(c.z for c in cos)))
        hi = Vector((max(c.x for c in cos), max(c.y for c in cos), max(c.z for c in cos)))
        out.append((lo, hi, idx))
    bm.free()
    return out


def group_x(isl, n):
    """Merge islands whose x ranges overlap, then report groups left to right."""
    spans = sorted([[lo.x, hi.x, [k]] for k, (lo, hi, _) in enumerate(isl)], key=lambda s: s[0])
    merged = []
    for s in spans:
        if merged and s[0] <= merged[-1][1] + 1e-3:
            merged[-1][1] = max(merged[-1][1], s[1])
            merged[-1][2] += s[2]
        else:
            merged.append(s)
    print(f"  x groups: {len(merged)} (expected {n})")
    return merged


for name, n in SETS:
    reset()
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, f"R508_F_{name}.fbx"))
    obs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    print(f"== {name}: {len(obs)} mesh objects")
    for o in obs:
        print(f"  {o.name}: tris {tris(o.data)}, dims {tuple(round(d, 3) for d in o.dimensions)}, rot {tuple(round(math.degrees(r)) for r in o.rotation_euler)}, mats {[m.name for m in o.data.materials]}")
    ob = obs[0]
    if len(obs) > 1:
        for o in obs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.join()
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    me = ob.data
    lo = Vector([min(v.co[i] for v in me.vertices) for i in range(3)])
    hi = Vector([max(v.co[i] for v in me.vertices) for i in range(3)])
    print(f"  world bounds {tuple(round(x, 3) for x in lo)} .. {tuple(round(x, 3) for x in hi)}")
    isl = islands(ob)
    print(f"  islands {len(isl)}")
    groups = group_x(isl, n)
    # per group: triangles and size
    vert_group = {}
    for g, (_, _, ks) in enumerate(groups):
        for k in ks:
            for i in isl[k][2]:
                vert_group[i] = g
    count = [0] * len(groups)
    for p in me.polygons:
        count[vert_group[p.vertices[0]]] += len(p.vertices) - 2
    for g, (x0, x1, ks) in enumerate(groups):
        zs = [isl[k][0].z for k in ks] + [isl[k][1].z for k in ks]
        ys = [isl[k][0].y for k in ks] + [isl[k][1].y for k in ks]
        print(f"  part {g}: x {x0:.3f}..{x1:.3f} (w {x1 - x0:.3f}), y {min(ys):.3f}..{max(ys):.3f}, z {min(zs):.3f}..{max(zs):.3f}, tris {count[g]}, islands {len(ks)}")
    # colour the parts and render front + top
    col = me.color_attributes.new("part", "FLOAT_COLOR", "POINT")
    for v in me.vertices:
        c = COLS[vert_group[v.index] % len(COLS)]
        col.data[v.index].color = (*c, 1.)
    m = bpy.data.materials.new("show")
    m.use_nodes = True
    nt = m.node_tree
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "part"
    nt.links.new(attr.outputs[0], nt.nodes["Principled BSDF"].inputs["Base Color"])
    me.materials.clear()
    me.materials.append(m)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 1600, 600
    w = bpy.data.worlds.new("w")
    sc.world = w
    w.color = (.9, .9, .9)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    sc.collection.objects.link(sun)
    c = (lo + hi) / 2
    size = hi - lo
    for view, rot, loc in (("front", (math.radians(90), 0, 0), (c.x, lo.y - 20, c.z)),
                           ("top", (0, 0, 0), (c.x, c.y, hi.z + 20))):
        cam = bpy.data.cameras.new(view)
        cam.type = "ORTHO"
        cam.ortho_scale = max(size.x, size.y if view == "top" else size.z * 1600 / 600) * 1.08
        co = bpy.data.objects.new(view, cam)
        co.rotation_euler = rot
        co.location = loc
        sc.collection.objects.link(co)
        sc.camera = co
        sc.render.filepath = os.path.join(HERE, f"render_f_inspect_{name.lower()}_{view}.png")
        bpy.ops.render.render(write_still=True)

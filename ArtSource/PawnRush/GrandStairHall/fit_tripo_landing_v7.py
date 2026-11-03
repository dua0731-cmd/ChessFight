# v7 (from v5): the central landing rebuilt clean instead of pressed out of the Tripo dais (v6 tore in Unity).
#   1. The Tripo mesh's stored normals are dropped (they were the undeformed model's, so every bent part shaded
#      wrong in Unity) and recomputed from the shape, hard above 35°.
#   2. The Tripo faces in the landing's box (|x| < 14.8, y 51.2 .. 66.3, above the hall floor) and its crumpled drop
#      north of it (x -14.8 .. 12, y 66.3 .. 70.5, the spiral foot covers x 4 .. 12) are cut away; under the spiral foot (x 4 .. 12) the platform is
#      only flattened onto the landing height.
#   3. A clean landing box takes their place at the blockout's size (x ±14.4, y 51.15 .. 66.3, top 8): its top in the
#      asset's own landing stone, its sides, the rails (where the blockout had them) and the two newel posts that
#      close the stair's balustrade ends in the asset's wall panel, both captured flat from the Tripo model
#      (textures/landing_stone.png 14 x 5 m, textures/wall_panel.png 7 x 8 m, mapped 1:1 and mirrored on repeat).
#   4. The blockout's central landing and its rails are deleted (the king statue stays).
# Run: blender -b grand_stair_hall_v5.blend -P fit_tripo_landing_v7.py
import bpy, bmesh, math, os, re
import numpy as np

HERE = os.path.dirname(bpy.data.filepath)
TOP = 8.0
X_OUT, Y_S, Y_N = 14.4, 51.15, 66.3
STONE = (14.0, 5.0)      # metres the stone capture covers
PANEL = (7.0, 8.0)

ob = bpy.data.objects["Tripo royal circular hall"]
me = ob.data
M = np.array(ob.matrix_world); Mi = np.linalg.inv(M)

def smooth(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

# 1. Normals from the shape.
if "custom_normal" in me.attributes: me.attributes.remove(me.attributes["custom_normal"])
print("CUSTOM NORMALS LEFT", me.has_custom_normals)

# 2a. Flatten the platform under the spiral foot.
n = len(me.vertices)
co = np.zeros(n * 3); me.vertices.foreach_get("co", co)
w = (np.c_[co.reshape(-1, 3), np.ones(n)] @ M.T)[:, :3]
x, y, z = w.T.copy()
foot = smooth(3.5, 4.5, x) * (1 - smooth(12.0, 13.0, x)) * smooth(63.0, 63.8, y) * (1 - smooth(71.5, 72.5, y)) \
     * smooth(4.0, 4.4, z) * (1 - smooth(9.4, 10.0, z))
w[:, 2] = z + ((TOP + (z - TOP) * 0.06) - z) * foot
me.vertices.foreach_set("co", ((np.c_[w, np.ones(n)] @ Mi.T)[:, :3]).ravel())
me.update()

# 2b. Cut the landing's box and the drop north of it.
nf = len(me.polygons)
fc = np.zeros(nf * 3); me.polygons.foreach_get("center", fc)
fc = (np.c_[fc.reshape(-1, 3), np.ones(nf)] @ M.T)[:, :3]
cx, cy, cz = fc.T
cut = ((np.abs(cx) < 14.8) & (cy > 51.2) & (cy < Y_N) & (cz > 0.3)) | \
      ((cx > -14.8) & (cx < 12.0) & (cy >= Y_N) & (cy < 70.5) & (cz > 4.3) & (cz < 8.8))
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[bm.faces[i] for i in np.where(cut)[0]], context="FACES_ONLY")
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context="VERTS")
bm.to_mesh(me); bm.free()
me.set_sharp_from_angle(angle=math.radians(35))
me.update()
print("CUT", int(cut.sum()), "faces")

# 3. The landing box, in world metres (Blender X/Y/Z = Unity x/z/y).
col = bpy.data.collections.get("Landing (clean)") or bpy.data.collections.new("Landing (clean)")
if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)

def material(name, image_path):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(image_path, check_existing=True)
    tex.extension = "MIRROR"
    bsdf.inputs["Roughness"].default_value = .75
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"]); nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m

def quad(bm, uv, a, b, c, d, axes, size):
    """One face a-b-c-d (counter-clockwise seen from outside), UV = the two world axes / the texture's metres."""
    vs = [bm.verts.new(p) for p in (a, b, c, d)]
    f = bm.faces.new(vs)
    for loop in f.loops:
        p = loop.vert.co
        loop[uv].uv = (p[axes[0]] / size[0], p[axes[1]] / size[1])
    return f

def box(bm, uv, x0, x1, y0, y1, z0, z1, faces="NSEWT"):
    if "T" in faces: quad(bm, uv, (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1), (0, 1), PANEL)
    if "S" in faces: quad(bm, uv, (x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1), (0, 2), PANEL)
    if "N" in faces: quad(bm, uv, (x1, y1, z0), (x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (0, 2), PANEL)
    if "E" in faces: quad(bm, uv, (x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1), (1, 2), PANEL)
    if "W" in faces: quad(bm, uv, (x0, y1, z0), (x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (1, 2), PANEL)

def finish(bm, name, mat):
    m = bpy.data.meshes.new(name); bm.to_mesh(m); bm.free()
    m.materials.append(mat)
    o = bpy.data.objects.new(name, m); col.objects.link(o)
    print("BUILT", name, len(m.polygons), "faces")

tex_dir = os.path.join(HERE, "textures")
stone = material("Landing stone (Tripo capture)", os.path.join(tex_dir, "landing_stone.png"))
panel = material("Wall panel (Tripo capture)", os.path.join(tex_dir, "wall_panel.png"))

# Top: a grid (2 m) so it lights and fogs evenly.
bm = bmesh.new(); uv = bm.loops.layers.uv.new("UVMap")
xs = np.linspace(-X_OUT, X_OUT, 15); ys = np.linspace(Y_S, Y_N, 9); zt = TOP + .02
for i in range(len(xs) - 1):
    for j in range(len(ys) - 1):
        quad(bm, uv, (xs[i], ys[j], zt), (xs[i + 1], ys[j], zt), (xs[i + 1], ys[j + 1], zt), (xs[i], ys[j + 1], zt), (0, 1), STONE)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
finish(bm, "LandingTop", stone)

bm = bmesh.new(); uv = bm.loops.layers.uv.new("UVMap")
# Sides of the box (its south face only beside the stair, whose own riser and stringers meet it).
box(bm, uv, -X_OUT, X_OUT, Y_S, Y_N, 0.0, zt, faces="NEW")
box(bm, uv, 9.85, X_OUT, Y_S, Y_N, 0.0, zt, faces="S")
box(bm, uv, -X_OUT, -9.85, Y_S, Y_N, 0.0, zt, faces="S")
# Rails where the blockout had them: W full, E up to the spiral foot, S beside the stair, N up to the spiral foot.
RT = zt + 1.1
box(bm, uv, -X_OUT, -X_OUT + .4, Y_S, Y_N, zt, RT)
box(bm, uv, X_OUT - .4, X_OUT, Y_S, 63.5, zt, RT)
box(bm, uv, 9.85, X_OUT - .4, Y_S, Y_S + .4, zt, RT)
box(bm, uv, -X_OUT + .4, -9.85, Y_S, Y_S + .4, zt, RT)
box(bm, uv, -X_OUT + .4, 4.0, Y_N - .4, Y_N, zt, RT)
# Newel posts closing the stair balustrade's cut ends.
for s in (-1, 1):
    a, b = sorted((s * 7.85, s * 9.85))
    box(bm, uv, a, b, 49.6, 51.65, 0.0, 9.6)
finish(bm, "LandingWalls", panel)

# 4. Delete the blockout box and its rails.
BOX = re.compile(r"^B2_CentralLanding/Central_landing(_rail_.*)?$")
gone = [o for o in bpy.data.objects if o.type == "MESH" and BOX.match(re.sub(r"#\d+$", "", o.name))]
for o in gone: bpy.data.objects.remove(o, do_unlink=True)
print("DELETED", len(gone))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v7.blend"))
print("SAVED")

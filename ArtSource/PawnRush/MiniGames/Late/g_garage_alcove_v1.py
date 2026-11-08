# G rook garage alcove, modelled in Blender (R504; user 11-08 "이거 에셋 쓰지 말고 블렌더로 자세 하게 해줘" about the
# Tripo alcove). Built to the game's numbers, no scaling in Unity:
#   the garage square is 2.25 m; the blocking rook (MgKingEscort) is 1.6 m high, ~1.1 m across, and is pushed into
#   the garage from the road until the game's gold stop. The alcove stands on the garage square, mouth to the road.
#   block 2.6 wide x 2.2 deep x 3.1 high, centred on the garage; niche 1.6 wide, straight sides to 1.6 m and a round
#   arch (top 2.4 m), 1.8 m deep to a back wall, open at the floor (the game's garage floor shows).
#   detail: plinth band, two pilasters with capitals either side of the arch, an archivolt ring with a keystone,
#   recessed side panels, a cornice with a matte brass fillet, a stepped cap.
# Origin = bottom centre of the block, Blender -y = the mouth (toward the road). Materials by Unity name:
# PR_Late_MarbleCream (box UVs in metres / 2, the 2 m swatch) and PR_Late_Brass.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python g_garage_alcove_v1.py -- [--export] [--render]
import bpy, bmesh, math, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Export", "G")
TEX = os.path.join(HERE, "Materials", "Textures")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
NAME = "G_GarageAlcove"

W, D, H = 2.6, 2.2, 3.1                 # block
NW, SPRING, BACK = 1.6, 1.6, .3         # niche width, arch springing height, back wall thickness
R = NW / 2                              # arch radius (top at SPRING + R = 2.4)
Y0 = -D / 2                             # the mouth's face


def mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    return m


def box(bm, x0, x1, y0, y1, z0, z1, slot=0):
    res = bmesh.ops.create_cube(bm, size=1.)
    for v in res["verts"]:
        v.co = Vector((x0 if v.co.x < 0 else x1, y0 if v.co.y < 0 else y1, z0 if v.co.z < 0 else z1))
    for f in {f for v in res["verts"] for f in v.link_faces}:
        f.material_index = slot
    return res


def arch_prism(name, half, spring, y0, y1, seg=24, z0=-.01):
    """A prism with an arch-topped outline (straight sides to `spring`, semicircle of radius `half`), from y0 to y1."""
    pts = [(-half, z0), (half, z0), (half, spring)]
    for i in range(1, seg):
        a = math.pi * i / seg
        pts.append((half * math.cos(a), spring + half * math.sin(a)))
    pts.append((-half, spring))
    bm = bmesh.new()
    front = [bm.verts.new((x, y0, z)) for x, z in pts]
    back = [bm.verts.new((x, y1, z)) for x, z in pts]
    bm.faces.new(front[::-1]); bm.faces.new(back)
    n = len(pts)
    for i in range(n):
        bm.faces.new((front[i], front[(i + 1) % n], back[(i + 1) % n], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])          # outward, or the EXACT boolean cuts the wrong side
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def boolean(target, cutter, op):
    mod = target.modifiers.new("b", 'BOOLEAN'); mod.operation = op; mod.object = cutter; mod.solver = 'EXACT'
    with bpy.context.temp_override(object=target, active_object=target):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter)


def mesh_object(name, build):
    bm = bmesh.new(); build(bm)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(mat("PR_Late_MarbleCream")); me.materials.append(mat("PR_Late_Brass"))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # the block with the niche cut into it
    block = mesh_object("block", lambda bm: box(bm, -W / 2, W / 2, Y0, -Y0, 0., H - .45))
    # the niche in two cuts (one arch-topped prism lost its straight part in the boolean): the box, then the arch
    boolean(block, mesh_object("niche box", lambda bm: box(bm, -R, R, Y0 - .1, -Y0 - BACK, -.05, SPRING + .02)), 'DIFFERENCE')
    boolean(block, arch_prism("niche arch", R, SPRING, Y0 - .1, -Y0 - BACK, z0=SPRING - .02), 'DIFFERENCE')
    # recessed side panels (each side face): shallow boxes cut in
    for sx in (-1, 1):
        cut = mesh_object("panel", lambda bm, sx=sx: box(bm, sx * W / 2 - .04, sx * W / 2 + .04, Y0 + .35, -Y0 - .35, .55, H - .95))
        boolean(block, cut, 'DIFFERENCE')
    parts = [block]

    def add(build_fn):
        parts.append(mesh_object("part", build_fn))

    # plinth band, cornice (with a brass fillet under it) and a stepped cap
    add(lambda bm: box(bm, -W / 2 - .08, W / 2 + .08, Y0 - .08, -Y0 + .08, 0., .3))
    add(lambda bm: box(bm, -W / 2 - .06, W / 2 + .06, Y0 - .06, -Y0 + .06, .3, .38))
    add(lambda bm: box(bm, -W / 2 - .05, W / 2 + .05, Y0 - .05, -Y0 + .05, H - .5, H - .45, slot=1))     # brass fillet
    add(lambda bm: box(bm, -W / 2 - .14, W / 2 + .14, Y0 - .14, -Y0 + .14, H - .45, H - .2))
    add(lambda bm: box(bm, -W / 2 - .04, W / 2 + .04, Y0 - .04, -Y0 + .04, H - .2, H))
    # pilasters either side of the arch on the mouth face, with bases and capitals
    for sx in (-1, 1):
        cx = sx * (R + .28)
        add(lambda bm, cx=cx: box(bm, cx - .16, cx + .16, Y0 - .1, Y0 + .01, .3, SPRING + .55))
        add(lambda bm, cx=cx: box(bm, cx - .21, cx + .21, Y0 - .14, Y0 + .01, .3, .5))
        add(lambda bm, cx=cx: box(bm, cx - .22, cx + .22, Y0 - .16, Y0 + .01, SPRING + .55, SPRING + .72))
        add(lambda bm, cx=cx: box(bm, cx - .18, cx + .18, Y0 - .13, Y0 + .01, SPRING + .5, SPRING + .55, slot=1))   # brass collar
    # archivolt: a ring around the arch standing proud of the face, and its imposts
    ring = arch_prism("ring", R + .16, SPRING, Y0 - .07, Y0 + .01, z0=SPRING - .001)
    boolean(ring, arch_prism("ringcut", R, SPRING, Y0 - .2, Y0 + .2, z0=SPRING - .5), 'DIFFERENCE')
    ring.data.materials.append(mat("PR_Late_MarbleCream")); parts.append(ring)
    for x0, x1 in ((-R - .2, -R + .02), (R - .02, R + .2)):          # imposts under the ring's feet
        add(lambda bm, x0=x0, x1=x1: box(bm, x0, x1, Y0 - .09, Y0 + .01, SPRING - .12, SPRING))
    # keystone
    add(lambda bm: box(bm, -.16, .16, Y0 - .12, Y0 + .01, SPRING + R - .05, SPRING + R + .32))
    # the niche's back wall gets a recessed arched panel, so it reads as a niche, not a hole
    panel = arch_prism("backpanel", R - .25, SPRING - .1, -Y0 - BACK - .01, -Y0 - BACK + .06, z0=.45)
    panel.data.materials.append(mat("PR_Late_MarbleCream"))
    boolean(block, panel, 'DIFFERENCE')
    # join; then the mouth is cut through the plinth bands too, so the rook slides in over no sill
    for o in bpy.context.scene.objects: o.select_set(o in parts)
    bpy.context.view_layer.objects.active = block
    bpy.ops.object.join()
    block = bpy.context.view_layer.objects.active
    boolean(block, mesh_object("mouth", lambda bm: box(bm, -R, R, Y0 - .3, -Y0 - BACK, -.05, .6)), "DIFFERENCE")
    parts = [block]
    # UVs, shading
    for o in bpy.context.scene.objects: o.select_set(o in parts)
    bpy.context.view_layer.objects.active = block
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = NAME
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        nx, ny, nz = (abs(c) for c in f.normal)
        for l in f.loops:
            p = l.vert.co
            l[uv].uv = (p.x / 2, p.y / 2) if nz >= max(nx, ny) else ((p.y / 2, p.z / 2) if nx >= ny else (p.x / 2, p.z / 2))
    bm.to_mesh(me); bm.free()
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.set_sharp_from_angle(angle=math.radians(30))
    print("ALCOVE tris", sum(len(p.vertices) - 2 for p in me.polygons))
    return ob


def main():
    ob = build()
    if "--export" in ARGS:
        os.makedirs(OUT, exist_ok=True)
        for o in bpy.context.scene.objects: o.select_set(o == ob)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y',
                                 apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE',
                                 path_mode='STRIP', bake_space_transform=True)
    if "--render" in ARGS:
        for m, f in (("PR_Late_MarbleCream", "PR_Late_MarbleCream_BaseColor.png"), ("PR_Late_Brass", "PR_Late_Brass_BaseColor.png")):
            mm = bpy.data.materials[m]; mm.use_nodes = True; nt = mm.node_tree
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(TEX, f))
            nt.links.new(t.outputs[0], nt.nodes["Principled BSDF"].inputs[0])
        sc = bpy.context.scene
        # a stand-in rook (1.6 m, ~1.1 m across) parked in it
        bpy.ops.mesh.primitive_cylinder_add(radius=.55, depth=1.6, location=(0, 0, .8))
        bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0))
        w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = .8; sc.world = w
        bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30)))
        cd = bpy.data.cameras.new("c"); cd.lens = 35; cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
        cam.location = (-3.2, -5.5, 3.2); cam.rotation_euler = (Vector((0, 0, 1.4)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1100, 1100
        sc.render.filepath = os.path.join(HERE, "render_g_garage_alcove.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()

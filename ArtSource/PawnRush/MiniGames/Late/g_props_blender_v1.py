# G props modelled in Blender (R504; user 11-08 "이것도 블렌더로 해서 디테일 하게 해줘" about the Tripo palanquin,
# then "이런 소품 들도" for the crown pedestals and the velvet rope stanchions). Built at the game's sizes, replacing the
# Tripo cuts of g_props_v1.py (same FBX names, so MiniGameModels keeps loading them):
#   G_Palanquin      the king's carrier on the sled (MgKingEscort: 1.6 m square base, the king sits at 0.55 m). Open
#                    box as in the concept sheet (no canopy): walnut base frame and side panels with two arched
#                    reliefs each, corner posts with brass finials, a brass top rail, the functional gold grip band all
#                    round, a purple velvet cushion, a brass crown plate on the front and back. The king is the game's
#                    own piece (team coloured), so the model has none.
#   G_CrownPedestal  1.6 m: marble plinth, a die with a recessed panel on each face, a moulded cap, a brass crown
#                    (band, eight points with pearls, two crossing arches, orb and cross).
#   G_StanchionPair  1.0 m brass posts (turned: base, stem, collars, ball top), 0.8 m apart, a sagging purple velvet
#                    rope on brass hooks.
# Origin = bottom centre, Blender -y = front. Materials by Unity name: PR_Late_Walnut, PR_Late_Velvet, PR_Late_Brass,
# PR_Late_MarbleCream (box UVs in metres / 2) and PR_Late_GoldFunctional (plain, the game's functional gold #E2B23C).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python g_props_blender_v1.py -- [--export] [--render]
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Export", "G")
TEX = os.path.join(HERE, "Materials", "Textures")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MATS = ["PR_Late_Walnut", "PR_Late_Velvet", "PR_Late_Brass", "PR_Late_MarbleCream", "PR_Late_GoldFunctional"]
WALNUT, VELVET, BRASS, MARBLE, GOLD = range(5)


class Part:
    """One mesh being built: boxes, lathed solids and tubes go into one bmesh with a material index each."""

    def __init__(self):
        self.bm = bmesh.new()

    def _tag(self, geom, slot):
        for f in {f for v in geom for f in v.link_faces}:
            f.material_index = slot

    def box(self, x0, x1, y0, y1, z0, z1, slot, bevel=0.):
        self.bm.faces.ensure_lookup_table()
        n0 = len(self.bm.faces)
        res = bmesh.ops.create_cube(self.bm, size=1.)
        for v in res["verts"]:
            v.co = Vector((x0 if v.co.x < 0 else x1, y0 if v.co.y < 0 else y1, z0 if v.co.z < 0 else z1))
        if bevel:
            edges = list({e for v in res["verts"] for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=2, affect='EDGES', profile=.5)
        self.bm.faces.ensure_lookup_table()
        faces = set(self.bm.faces[n0:]) | {f for v in res["verts"] if v.is_valid for f in v.link_faces}
        if bevel:
            faces |= {f for e in self.bm.edges if e.select for f in e.link_faces}
        for f in faces:                       # every face made by this box (and its bevel)
            f.material_index = slot

    def lathe(self, profile, at, slot, seg=24):
        """profile: [(radius, z)] from bottom to top; a closed solid of revolution about the vertical axis at `at`."""
        rings = []
        for r, z in profile:
            ring = []
            for i in range(seg):
                a = 2 * math.pi * i / seg
                ring.append(self.bm.verts.new((at[0] + r * math.cos(a), at[1] + r * math.sin(a), at[2] + z)))
            rings.append(ring)
        new = []
        for k in range(len(rings) - 1):
            for i in range(seg):
                j = (i + 1) % seg
                new.append(self.bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i])))
        new.append(self.bm.faces.new(rings[0][::-1]))
        new.append(self.bm.faces.new(rings[-1]))
        for f in new: f.material_index = slot

    def tube(self, points, radius, slot, seg=10):
        """A closed tube along a polyline."""
        rings = []
        for k, p in enumerate(points):
            t = (points[min(k + 1, len(points) - 1)] - points[max(k - 1, 0)]).normalized()
            n = t.orthogonal().normalized(); b = t.cross(n)
            rings.append([self.bm.verts.new(p + (n * math.cos(2 * math.pi * i / seg) + b * math.sin(2 * math.pi * i / seg)) * radius) for i in range(seg)])
        new = []
        for k in range(len(rings) - 1):
            for i in range(seg):
                j = (i + 1) % seg
                new.append(self.bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i])))
        new.append(self.bm.faces.new(rings[0][::-1])); new.append(self.bm.faces.new(rings[-1]))
        for f in new: f.material_index = slot

    def sphere(self, at, r, slot, seg=16):
        prof = [(max(r * math.sin(math.pi * i / 8), 1e-4), r - r * math.cos(math.pi * i / 8)) for i in range(9)]
        self.lathe(prof, (at[0], at[1], at[2] - r), slot, seg)

    def pyramid(self, x, y, z, w, h, slot):
        b = [self.bm.verts.new((x + sx * w / 2, y + sy * w / 2, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        top = self.bm.verts.new((x, y, z + h))
        faces = [self.bm.faces.new(b[::-1])] + [self.bm.faces.new((b[i], b[(i + 1) % 4], top)) for i in range(4)]
        for f in faces: f.material_index = slot

    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        uv = self.bm.loops.layers.uv.verify()
        for f in self.bm.faces:
            nx, ny, nz = (abs(c) for c in f.normal)
            for l in f.loops:
                p = l.vert.co
                l[uv].uv = (p.x / 2, p.y / 2) if nz >= max(nx, ny) else ((p.y / 2, p.z / 2) if nx >= ny else (p.x / 2, p.z / 2))
        me = bpy.data.meshes.new(name); self.bm.to_mesh(me); self.bm.free()
        for m in MATS: me.materials.append(bpy.data.materials.get(m) or bpy.data.materials.new(m))
        me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
        me.set_sharp_from_angle(angle=math.radians(35))
        ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
        used = sorted({p.material_index for p in me.polygons})
        print("OBJ %-16s tris %5d  materials %s" % (name, sum(len(p.vertices) - 2 for p in me.polygons), [MATS[i] for i in used]))
        return ob

    def turned(self, angle, build):
        """Build some geometry with `build(self)` and turn just that geometry about the vertical axis (keeps slots)."""
        self.bm.verts.ensure_lookup_table()
        n0 = len(self.bm.verts)
        build(self)
        self.bm.verts.ensure_lookup_table()
        bmesh.ops.rotate(self.bm, verts=self.bm.verts[n0:], cent=(0, 0, 0), matrix=Matrix.Rotation(angle, 3, 'Z'))


def palanquin():
    p = Part()
    S, h = 1.6, .8                                       # base square, panel top
    p.box(-S / 2, S / 2, -S / 2, S / 2, 0., .14, WALNUT, bevel=.02)               # base frame
    for sx in (-1, 1):                                   # runners (as the game's sled)
        p.box(sx * .6 - .04, sx * .6 + .04, -S / 2 - .05, S / 2 + .05, -.0, .05, BRASS)
    p.box(-S / 2 - .025, S / 2 + .025, -S / 2 - .025, S / 2 + .025, .3, .44, GOLD)   # the functional grip band
    t = .09                                              # panel thickness
    def panel(q, side):
        q.box(-S / 2 + .1, S / 2 - .1, -S / 2, -S / 2 + t, .14, h, WALNUT)
        for cx in (-.33, .33):
            q.box(cx - .22, cx + .22, -S / 2 - .025, -S / 2 + .01, .5, .52, WALNUT)      # arch sill
            q.box(cx - .22, cx - .17, -S / 2 - .025, -S / 2 + .01, .5, .66, WALNUT)      # arch jambs
            q.box(cx + .17, cx + .22, -S / 2 - .025, -S / 2 + .01, .5, .66, WALNUT)
            arc = [Vector((cx + .195 * math.cos(math.pi * i / 10), -S / 2 - .008, .66 + .195 * math.sin(math.pi * i / 10))) for i in range(11)]
            q.tube(arc, .025, WALNUT, seg=6)
        q.box(-S / 2 + .1, S / 2 - .1, -S / 2 - .03, -S / 2 + .02, h - .04, h + .02, BRASS)   # brass top rail
        if side in (0, 2):                                 # crown plate front and back
            q.box(-.17, .17, -S / 2 - .045, -S / 2 - .01, .2, .29, BRASS)
            for k in range(5):
                q.pyramid(-.14 + .07 * k, -S / 2 - .03, .29, .05, .06 + .03 * (k % 2 == 0), BRASS)
    for side in range(4):                                # four walnut side panels, each with two arched reliefs
        p.turned(math.pi / 2 * side, lambda q, side=side: panel(q, side))
    for sx in (-1, 1):                                   # corner posts with finials
        for sy in (-1, 1):
            x, y = sx * (S / 2 - .08), sy * (S / 2 - .08)
            p.box(x - .1, x + .1, y - .1, y + .1, .14, h + .1, WALNUT, bevel=.015)
            p.box(x - .12, x + .12, y - .12, y + .12, h + .1, h + .14, BRASS)
            p.pyramid(x, y, h + .14, .17, .2, BRASS)
            p.sphere((x, y, h + .38), .04, BRASS, seg=10)
    p.box(-S / 2 + .1, S / 2 - .1, -S / 2 + .1, S / 2 - .1, .14, .42, WALNUT)       # inner floor
    p.box(-.62, .62, -.62, .62, .42, .55, VELVET, bevel=.05)                        # cushion: the king sits at 0.55
    return p.finish("G_Palanquin")


def crown(p, cx, cy, z, r):
    """A brass crown standing at z, radius r."""
    p.lathe([(r * .9, 0.), (r, .02), (r, .16 * r / .2), (r * .92, .18 * r / .2)], (cx, cy, z), BRASS, seg=20)
    for i in range(8):
        a = 2 * math.pi * i / 8
        x, y = cx + r * .95 * math.cos(a), cy + r * .95 * math.sin(a)
        p.pyramid(x, y, z + .17 * r / .2, .07 * r / .2, .12 * r / .2, BRASS)
        p.sphere((x, y, z + .31 * r / .2), .025 * r / .2, BRASS, seg=8)
    for a0 in (0., math.pi / 2):                         # two crossing arches
        arc = [Vector((cx + r * .92 * math.cos(math.pi * i / 12) * math.cos(a0), cy + r * .92 * math.cos(math.pi * i / 12) * math.sin(a0),
                       z + .17 * r / .2 + r * .55 * math.sin(math.pi * i / 12))) for i in range(13)]
        p.tube(arc, .018 * r / .2, BRASS, seg=6)
    top = z + .17 * r / .2 + r * .55
    p.sphere((cx, cy, top + .05 * r / .2), .05 * r / .2, BRASS)
    p.box(cx - .01, cx + .01, cy - .01, cy + .01, top + .09 * r / .2, top + .2 * r / .2, BRASS)
    p.box(cx - .045 * r / .2, cx + .045 * r / .2, cy - .01, cy + .01, top + .14 * r / .2, top + .16 * r / .2, BRASS)


def pedestal():
    p = Part()
    p.box(-.32, .32, -.32, .32, 0., .12, MARBLE, bevel=.015)
    p.box(-.29, .29, -.29, .29, .12, .2, MARBLE, bevel=.01)
    p.box(-.23, .23, -.23, .23, .2, 1.12, MARBLE)
    def frame(q):
        for x0, x1, z0, z1 in ((-.17, .17, .3, .33), (-.17, .17, .99, 1.02), (-.17, -.14, .3, 1.02), (.14, .17, .3, 1.02)):
            q.box(x0, x1, -.25, -.22, z0, z1, MARBLE)
    for side in range(4):                                # raised panel frame on each face (a fillet ring)
        p.turned(math.pi / 2 * side, frame)
    p.box(-.27, .27, -.27, .27, 1.12, 1.18, MARBLE)
    p.box(-.3, .3, -.3, .3, 1.18, 1.24, BRASS)
    p.box(-.31, .31, -.31, .31, 1.24, 1.3, MARBLE, bevel=.012)
    crown(p, 0., 0., 1.3, .2)
    return p.finish("G_CrownPedestal")


def stanchions():
    p = Part()
    post = [(.17, 0.), (.17, .03), (.13, .05), (.11, .08), (.05, .1), (.035, .14), (.03, .78), (.045, .8), (.045, .84), (.03, .86),
            (.032, .9), (.055, .92), (.06, .95), (.0001, .95)]
    for x in (-.4, .4):
        p.lathe(post, (x, 0., 0.), BRASS, seg=20)
        p.sphere((x, 0., .99), .065, BRASS)
        p.lathe([(.035, 0.), (.035, .04), (.0001, .04)], (x - .045 * (1 if x > 0 else -1), 0., .86), BRASS, seg=10)   # hook boss
    # the rope: a catenary from hook to hook, 0.22 m sag, with brass end caps
    a, b, sag, n = -.35, .35, .22, 24
    pts = []
    for i in range(n + 1):
        u = a + (b - a) * i / n
        pts.append(Vector((u, 0., .88 - sag * (1 - (u / b) ** 2))))
    p.tube(pts, .032, VELVET, seg=12)
    for x in (a, b):
        p.lathe([(.042, -.04), (.042, .04), (.0001, .04)], (x, 0., .88), BRASS, seg=12)
    return p.finish("G_StanchionPair")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    made = [palanquin(), pedestal(), stanchions()]
    if "--export" in ARGS:
        os.makedirs(OUT, exist_ok=True)
        for ob in made:
            for o in bpy.context.scene.objects: o.select_set(o == ob)
            bpy.context.view_layer.objects.active = ob
            bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, ob.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y',
                                     apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE',
                                     path_mode='STRIP', bake_space_transform=True)
    if "--render" in ARGS:
        for name, f in (("PR_Late_Walnut", "PR_Late_Walnut_BaseColor.png"), ("PR_Late_Velvet", "PR_Late_Velvet_BaseColor.png"),
                        ("PR_Late_Brass", "PR_Late_Brass_BaseColor.png"), ("PR_Late_MarbleCream", "PR_Late_MarbleCream_BaseColor.png")):
            m = bpy.data.materials[name]; m.use_nodes = True; nt = m.node_tree
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(TEX, f))
            nt.links.new(t.outputs[0], nt.nodes["Principled BSDF"].inputs[0])
            if name == "PR_Late_Brass": nt.nodes["Principled BSDF"].inputs["Metallic"].default_value = .8
        g = bpy.data.materials["PR_Late_GoldFunctional"]; g.use_nodes = True
        bsdf = g.node_tree.nodes["Principled BSDF"]; bsdf.inputs["Base Color"].default_value = (.76, .45, .05, 1)
        bsdf.inputs["Metallic"].default_value = 1.; bsdf.inputs["Roughness"].default_value = .25
        made[1].location.x = 1.6; made[2].location.x = 2.9
        # a stand-in king on the cushion (as the game's piece)
        bpy.ops.mesh.primitive_cylinder_add(radius=.3, depth=1.2, location=(0, 0, 1.15))
        bpy.ops.mesh.primitive_plane_add(size=12, location=(1.4, 0, 0))
        sc = bpy.context.scene
        w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = .8; sc.world = w
        bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30)))
        cd = bpy.data.cameras.new("c"); cd.lens = 40; cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
        cam.location = (-1.5, -5.2, 2.6); cam.rotation_euler = (Vector((1.45, 0, .7)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
        sc.render.filepath = os.path.join(HERE, "render_g_props_blender.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()

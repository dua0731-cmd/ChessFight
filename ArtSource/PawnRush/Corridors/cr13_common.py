# Section corridor kit (2026-11-22): shared tools for the corridor modules built in background Blender.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python cr01_garden.py -- [--export] [--render]
#
# Frame (Blender): X = the corridor's x (|x| < 6 is walked, the walls' bodies stand at |x| 6..6.6), Y = the corridor's z
# (the way, 0 at the entry), Z = up (floor top 0). Unity hangs every FBX under a pivot turned 180 degrees about y, as the
# mini-game models (MiniGameModels.Hang), so Blender x, y, z = that pivot parent's x, z, y.
# Wall pieces are authored for the WEST wall (x < 0, facing +x); the loader turns them 180 degrees for the east wall.
#
# Materials are named exactly as the Unity .mat they use. An existing .mat (corridor, mini-game, garden concept, carriage
# yard or Resources root) is never rewritten; a new one is written to Assets/Resources/PawnRushCorridors/Materials.
# Preview colours and textures are read from the Unity .mat itself, so the renders show what Unity will draw.
import bpy, bmesh, math, os, re, sys, json, shutil, hashlib
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OUT_REL = "Assets/Resources/PawnRushCorridors"
LIT = "933532a4fcc9baf4fa0491de14d08ed7"  # URP Lit
FBX_META = os.path.join(ROOT, "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta")
TEX_META = os.path.join(ROOT, "Assets/Art/PawnRushMap/Endgame/Textures/T_EG_BannerNavy_BaseColor.png.meta")
MAT_FOLDERS = ["Assets/Resources/PawnRushCorridors/Materials", "Assets/Resources/PawnRushMiniGames/Materials",
               "Assets/Resources/PawnRushGardenConcept/Materials", "Assets/Resources/PawnRushCarriageYard/Materials",
               "Assets/Art/PawnRushMap/Resources"]

sys.path.insert(0, os.path.join(ROOT, "ArtSource/PawnRush/MiniGames/Early"))
import mg_export  # noqa: E402  (MAT template, guid, folder, write)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def srgb_to_lin(c):
    return [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in c]


# ---------------------------------------------------------------------------------------------------------------- materials
_guid_paths = None


def _texture_by_guid(guid):
    global _guid_paths
    if _guid_paths is None:
        _guid_paths = {}
        for folder in ["Assets/Resources", "Assets/Art"]:
            for dp, _, files in os.walk(os.path.join(ROOT, folder)):
                for f in files:
                    if f.endswith(".png.meta") or f.endswith(".jpg.meta"):
                        try:
                            with open(os.path.join(dp, f), encoding="utf-8") as fh:
                                g = fh.read(200).split("\n")[1].split(":")[1].strip()
                            _guid_paths[g] = os.path.join(dp, f[:-5])
                        except Exception:
                            pass
    return _guid_paths.get(guid)


def unity_mat_file(name):
    for folder in MAT_FOLDERS:
        p = os.path.join(ROOT, folder, name + ".mat")
        if os.path.exists(p):
            return p
    return None


def _read_unity_mat(path):
    text = open(path, encoding="utf-8").read()
    col = re.search(r"_BaseColor: \{r: ([\d.e-]+), g: ([\d.e-]+), b: ([\d.e-]+), a: ([\d.e-]+)\}", text)
    tex = re.search(r"_BaseMap:\s*\n\s*m_Texture: \{fileID: \d+(?:, guid: ([0-9a-f]+))?", text)
    scale = re.search(r"_BaseMap:\s*\n\s*m_Texture:[^\n]*\n\s*m_Scale: \{x: ([\d.e-]+)", text)
    smooth = re.search(r"- _Smoothness: ([\d.e-]+)", text)
    metal = re.search(r"- _Metallic: ([\d.e-]+)", text)
    color = [float(col.group(i)) for i in range(1, 5)] if col else [1, 1, 1, 1]
    return dict(color=color, texture=_texture_by_guid(tex.group(1)) if tex and tex.group(1) else None,
                tiling=float(scale.group(1)) if scale else 1.0,
                rough=1 - float(smooth.group(1)) if smooth else .6, metal=float(metal.group(1)) if metal else 0.)


# New materials this kit adds (only written when no .mat of that name exists anywhere in MAT_FOLDERS).
# name: (sRGB colour, roughness, metallic, texture file in ArtSource or None, tiling, alpha, emission)
NEW = {}


def new_material(name, color, rough=.6, metal=0., texture=None, tiling=1.0, alpha=1.0):
    NEW[name] = dict(color=list(color), rough=rough, metal=metal, texture=texture, tiling=tiling, alpha=alpha)


_mats = {}


def material(name):
    """The Blender material for the Unity material `name` (existing .mat or one declared with new_material)."""
    if name in _mats:
        return _mats[name]
    path = unity_mat_file(name)
    if path:
        spec = _read_unity_mat(path)
    elif name in NEW:
        n = NEW[name]
        spec = dict(color=n["color"] + [n["alpha"]], texture=n["texture"], tiling=n["tiling"], rough=n["rough"],
                    metal=n["metal"])
    else:
        raise KeyError("no Unity material and no new_material for " + name)
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = next(nd for nd in nt.nodes if nd.type == "BSDF_PRINCIPLED")
    lin = srgb_to_lin(spec["color"][:3])
    p.inputs["Roughness"].default_value = spec["rough"]
    p.inputs["Metallic"].default_value = spec["metal"]
    if spec["color"][3] < .99:
        p.inputs["Alpha"].default_value = spec["color"][3]
        try:
            m.surface_render_method = "BLENDED"
        except Exception:
            pass
    if spec["texture"] and os.path.exists(spec["texture"]):
        img = bpy.data.images.load(spec["texture"], check_existing=True)
        coord = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (spec["tiling"], spec["tiling"], 1)
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        mul = nt.nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1.0
        nt.links.new(coord.outputs["UV"], mp.inputs["Vector"])
        nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
        nt.links.new(tex.outputs["Color"], mul.inputs["A"])
        mul.inputs["B"].default_value = lin + [1]
        nt.links.new(mul.outputs["Result"], p.inputs["Base Color"])
    else:
        p.inputs["Base Color"].default_value = lin + [1]
    _mats[name] = m
    return m


def write_new_materials(names):
    """Writes the .mat (and texture) of every NEW material used by the exported objects that Unity does not have yet."""
    mg_export.folder(OUT_REL + "/Materials")
    for name in sorted(names):
        if unity_mat_file(name) or name not in NEW:
            continue
        n = NEW[name]
        rel = OUT_REL + "/Materials/" + name + ".mat"
        tex_ref, tile = "{fileID: 0}", 1.0
        if n["texture"]:
            mg_export.folder(OUT_REL + "/Textures")
            trel = OUT_REL + "/Textures/" + os.path.basename(n["texture"])
            if not os.path.exists(os.path.join(ROOT, trel)):
                shutil.copyfile(n["texture"], os.path.join(ROOT, trel))
            if not os.path.exists(os.path.join(ROOT, trel + ".meta")):
                lines = open(TEX_META, encoding="utf-8").read().split("\n")
                lines[1] = "guid: " + mg_export.guid(trel)
                mg_export.write(trel + ".meta", "\n".join(lines))
            g = open(os.path.join(ROOT, trel + ".meta"), encoding="utf-8").read().split("\n")[1].split(":")[1].strip()
            tex_ref, tile = "{fileID: 2800000, guid: %s, type: 3}" % g, n["tiling"]
        r, g_, b = n["color"]
        a = n["alpha"]
        see = a < .99
        mg_export.write(rel, mg_export.MAT.format(
            name=name, lit=LIT, keywords="\n  - _SURFACE_TYPE_TRANSPARENT" if see else " []",
            queue=3000 if see else -1, rtype="Transparent" if see else "Opaque",
            extra_pass="\n  - DepthOnly\n  - SHADOWCASTER" if see else "", tex=tex_ref, tile="%.3f" % tile,
            dst=10 if see else 0, src=5 if see else 1, surface=1 if see else 0, zwrite=0 if see else 1,
            metal="%.3f" % n["metal"], smooth="%.3f" % (1 - n["rough"]),
            r="%.4f" % r, g="%.4f" % g_, b="%.4f" % b, a="%.3f" % a, er="0", eg="0", eb="0"))
        if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
            mg_export.write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (mg_export.guid(rel), mg_export.NATIVE))
        print("MAT new", name)


# ------------------------------------------------------------------------------------------------------------- mesh builder
class Mesh:
    """Accumulates bevelled parts into one bmesh; every part takes a Unity material name."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.uv_mode = {}   # face index range -> 'box' (metres) default; lathe parts keep their own UVs

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _bevel(self, geom_faces, width, segments=1):
        if width <= 0:
            return
        edges = list({e for f in geom_faces for e in f.edges})
        verts = list({v for f in geom_faces for v in f.verts})
        try:
            bmesh.ops.bevel(self.bm, geom=edges + verts, offset=width, offset_type="OFFSET", segments=segments,
                            profile=.5, affect="EDGES", clamp_overlap=True)
        except TypeError:
            bmesh.ops.bevel(self.bm, geom=edges + verts, offset=width, segments=segments, profile=.5,
                            vertex_only=False, clamp_overlap=True)

    def box(self, x0, x1, y0, y1, z0, z1, mat, bevel=.015, segments=1):
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        z0, z1 = min(z0, z1), max(z0, z1)
        b = min(bevel, (x1 - x0) * .45, (y1 - y0) * .45, (z1 - z0) * .45)
        before = set(self.bm.faces)
        m = Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)) @ Matrix.Diagonal(
            ((x1 - x0) / 2, (y1 - y0) / 2, (z1 - z0) / 2, 1))
        bmesh.ops.create_cube(self.bm, size=2, matrix=m)
        new = [f for f in self.bm.faces if f not in before]
        self._bevel(new, b, segments)
        idx = self.mi(mat)
        for f in self.bm.faces:
            if f not in before:
                f.material_index = idx

    def prism(self, pts, axis, d0, d1, mat, bevel=.01, segments=1, offset=(0, 0, 0)):
        """A polygon `pts` (2D, counter-clockwise) extruded along `axis` ('x', 'y' or 'z') from d0 to d1.
        x-axis: pts are (y, z); y-axis: (x, z); z-axis: (x, y)."""
        def to3(u, v, d):
            if axis == "x":
                return Vector((d, u, v)) + Vector(offset)
            if axis == "y":
                return Vector((u, d, v)) + Vector(offset)
            return Vector((u, v, d)) + Vector(offset)
        before = set(self.bm.faces)
        a = [self.bm.verts.new(to3(u, v, d0)) for u, v in pts]
        b = [self.bm.verts.new(to3(u, v, d1)) for u, v in pts]
        n = len(pts)
        flip = (axis == "y")  # keep outward normals for a counter-clockwise outline
        fa = self.bm.faces.new(list(reversed(a)) if not flip else a)
        fb = self.bm.faces.new(b if not flip else list(reversed(b)))
        for i in range(n):
            j = (i + 1) % n
            q = [a[i], a[j], b[j], b[i]]
            self.bm.faces.new(q if not flip else list(reversed(q)))
        new = [f for f in self.bm.faces if f not in before]
        bmesh.ops.recalc_face_normals(self.bm, faces=new)
        self._bevel(new, bevel, segments)
        idx = self.mi(mat)
        for f in self.bm.faces:
            if f not in before:
                f.material_index = idx

    def lathe(self, profile, centre, mat, segs=16, axis="z", radial=None, cap_top=True, cap_bottom=True, phase=0.):
        """A surface of revolution: profile [(r, h)] from bottom to top, about `axis` through `centre`.
        radial(angle, h) -> radius factor (flutes, petals)."""
        cx, cy, cz = centre
        before = set(self.bm.faces)
        rings = []
        for r, h in profile:
            ring = []
            for k in range(segs):
                a = phase + 2 * math.pi * k / segs
                rr = r * (radial(a, h) if radial else 1.0)
                c, s = math.cos(a) * rr, math.sin(a) * rr
                if axis == "z":
                    p = (cx + c, cy + s, cz + h)
                elif axis == "y":
                    p = (cx + c, cy + h, cz + s)
                else:
                    p = (cx + h, cy + c, cz + s)
                ring.append(self.bm.verts.new(p))
            rings.append(ring)
        for i in range(len(rings) - 1):
            for k in range(segs):
                j = (k + 1) % segs
                self.bm.faces.new([rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]])
        if cap_bottom and profile[0][0] > 1e-4:
            self.bm.faces.new(list(reversed(rings[0])))
        if cap_top and profile[-1][0] > 1e-4:
            self.bm.faces.new(rings[-1])
        new = [f for f in self.bm.faces if f not in before]
        bmesh.ops.recalc_face_normals(self.bm, faces=new)
        idx = self.mi(mat)
        for f in new:
            f.material_index = idx
            f.smooth = True

    def tube(self, path, radius, mat, segs=8, radii=None, cap=True):
        """A tube along a polyline `path` [(x, y, z)], radius tapering by `radii` if given."""
        before = set(self.bm.faces)
        pts = [Vector(p) for p in path]
        rings = []
        for i, p in enumerate(pts):
            t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            ref = Vector((0, 0, 1)) if abs(t.z) < .9 else Vector((1, 0, 0))
            u = t.cross(ref).normalized()
            v = t.cross(u).normalized()
            r = radii[i] if radii else radius
            rings.append([self.bm.verts.new(p + (u * math.cos(2 * math.pi * k / segs) + v * math.sin(2 * math.pi * k / segs)) * r)
                          for k in range(segs)])
        for i in range(len(rings) - 1):
            for k in range(segs):
                j = (k + 1) % segs
                self.bm.faces.new([rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]])
        if cap:
            self.bm.faces.new(list(reversed(rings[0])))
            self.bm.faces.new(rings[-1])
        new = [f for f in self.bm.faces if f not in before]
        bmesh.ops.recalc_face_normals(self.bm, faces=new)
        idx = self.mi(mat)
        for f in new:
            f.material_index = idx
            f.smooth = True

    def blob(self, centre, radius, mat, subdiv=2, squash=(1, 1, 1), noise=.0, seed=0):
        """A lumpy ball (boxwood, rose heads): an icosphere pushed in and out."""
        import random
        rnd = random.Random(seed)
        before = set(self.bm.faces)
        bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=1.0,
                                   matrix=Matrix.Translation(centre) @ Matrix.Diagonal(
                                       (radius * squash[0], radius * squash[1], radius * squash[2], 1)))
        new = [f for f in self.bm.faces if f not in before]
        verts = {v for f in new for v in f.verts}
        if noise:
            c = Vector(centre)
            for v in verts:
                d = v.co - c
                v.co = c + d * (1 + rnd.uniform(-noise, noise))
        idx = self.mi(mat)
        for f in new:
            f.material_index = idx
            f.smooth = True

    def transform_new(self, before, matrix):
        """Applies `matrix` to the verts of faces added since `before` (a set of faces)."""
        verts = {v for f in self.bm.faces if f not in before for v in f.verts}
        for v in verts:
            v.co = matrix @ v.co

    def finish(self, collection, pivot=(0, 0, 0), uv_scale=1.0):
        """Makes the object, its origin at `pivot`, metre box UVs on every face (texture repeats in the material)."""
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        uv = self.bm.loops.layers.uv.new("UVMap")
        for f in self.bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for loop in f.loops:
                co = loop.vert.co
                if ax == 0:
                    u, v = co.y, co.z
                elif ax == 1:
                    u, v = co.x, co.z
                else:
                    u, v = co.x, co.y
                loop[uv].uv = (u * uv_scale, v * uv_scale)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(material(m))
        ob = bpy.data.objects.new(self.name, me)
        collection.objects.link(ob)
        p = Vector(pivot)
        me.transform(Matrix.Translation(-p))
        ob.location = p
        for poly in me.polygons:
            if poly.use_smooth:
                continue
        return ob


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def empty(collection, name, at, rot=(0, 0, 0), parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = .25
    e.location = at
    e.rotation_euler = rot
    collection.objects.link(e)
    if parent is not None:
        e.parent = parent
        e.matrix_parent_inverse = parent.matrix_world.inverted()
    return e


def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


# ------------------------------------------------------------------------------------------------------------------- export
def export(objects, fbx_rel):
    """One FBX of `objects` (a mesh at its pivot plus its marker empties), written with the garden's import settings.
    The mesh's origin is moved to the world origin for the file, so the FBX's pivot is the module's pivot."""
    mg_export.folder(os.path.dirname(fbx_rel))
    root = next(o for o in objects if o.type == "MESH")
    saved = root.location.copy()
    shift = -saved
    for o in objects:
        if o.parent is None:
            o.location = o.location + shift
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, fbx_rel), use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z",
                             axis_up="Y", bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="FACE",
                             add_leaf_bones=False, path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
    for o in objects:
        if o.parent is None:
            o.location = o.location - shift
    if not os.path.exists(os.path.join(ROOT, fbx_rel + ".meta")):
        lines = open(FBX_META, encoding="utf-8").read().split("\n")
        lines[1] = "guid: " + mg_export.guid(fbx_rel)
        mg_export.write(fbx_rel + ".meta", "\n".join(lines))
    names = {m.name for o in objects if o.type == "MESH" for m in o.data.materials if m}
    write_new_materials(names)
    print("EXPORT %s  %d tris  materials %s" % (fbx_rel, sum(tris(o) for o in objects if o.type == "MESH"), sorted(names)))


# ------------------------------------------------------------------------------------------------------------------ preview
def render_setup(sun=True, world=(.62, .74, .92), strength=.45):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = 1536, 1024
    sc.render.film_transparent = False
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        sc.view_settings.view_transform = "Filmic"
    w = bpy.data.worlds.new("W")
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = list(world) + [1]
    bg.inputs["Strength"].default_value = strength
    sc.world = w
    if sun:
        s = bpy.data.lights.new("Sun", "SUN")
        s.energy = 5.5
        s.angle = math.radians(4)
        so = bpy.data.objects.new("Sun", s)
        so.rotation_euler = (math.radians(50), math.radians(-25), math.radians(-35))
        sc.collection.objects.link(so)


def shoot(path, at, look, lens=24):
    sc = bpy.context.scene
    cam = bpy.data.objects.get("PreviewCam")
    if cam is None:
        cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        sc.collection.objects.link(cam)
    cam.data.lens = lens
    cam.data.clip_end = 400
    cam.location = at
    d = Vector(look) - Vector(at)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDER", path)


def pawn(coll, x, y, scarf="GC_royal_blue"):
    m = Mesh("pawn")
    m.lathe([(0.001, 0), (.34, .02), (.36, .12), (.3, .35), (.2, .55), (.16, .62), (0.001, .64)], (x, y, 0),
            "GC_cream_stone_pale", segs=20)
    m.blob((x, y, .86), .25, "GC_cream_stone_pale", subdiv=2)
    m.lathe([(.17, .58), (.22, .62), (.2, .7), (.15, .72)], (x, y, 0), scarf, segs=16)
    return m.finish(coll, (x, y, 0))


# ------------------------------------------------------------------------------------------------------------ the corridor
LENGTH, HALF, WALL = 10.0, 6.0, .6
PIER_W = .9
BAYS = 3
AUTHORED_SPAN = 1.9   # a bay's clear span between pier faces as authored; the loader scales z by real span / this


def piers(frm, to):
    return [frm + PIER_W / 2 + (to - frm - PIER_W) * i / BAYS for i in range(BAYS + 1)]


def bay_span(frm, to):
    p = piers(frm, to)
    return p[1] - p[0] - PIER_W


def place(ob, x, y, side=-1, scale_y=1.0):
    """A copy of module `ob` placed as the loader will: west as authored, east turned 180 degrees about up."""
    c = ob.copy()
    c.data = ob.data
    c.location = (x, y, 0)
    c.rotation_euler = (0, 0, 0 if side < 0 else math.pi)
    c.scale = (1, scale_y, 1)
    c.hide_render = False
    bpy.context.scene.collection.objects.link(c)
    return c


def write_layout(kit_id, height, frm, to, modules, ornaments=None, open_sky=False, **extra):
    """CR0x_Layout.json beside the FBX files, in the shape the CR04..07 kit writes (the loader reads it)."""
    rel = OUT_REL + "/" + kit_id + "/" + kit_id.split("_")[0] + "_Layout.json"
    data = dict(id=kit_id, height=height, z_from=frm, z_to=to, bay_span_authored=AUTHORED_SPAN, pier_depth=PIER_W,
                open_sky=open_sky, modules=sorted(modules), ornaments=ornaments or [], **extra)
    mg_export.write(rel, json.dumps(data, indent=2) + "\n")
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        mg_export.write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n"
                        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % mg_export.guid(rel))
    print("LAYOUT", rel)

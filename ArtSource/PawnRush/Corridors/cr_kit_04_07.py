# Shared kit for the section-corridor models CR04..CR07 (lower-palace rooms, king's antechamber, war-room approach,
# library approach). Each corridor's script (CR0x_<Name>/cr0x_<name>.py) builds its modules with this kit, renders a
# preview of the assembled corridor and exports one FBX per module plus CR0x_Layout.json.
#
# The module contract (agreed with the CR01..03 session, notes "corridor-module-contract.md"):
#   corridor frame = +along the way, up, floor 0; walk |x| < 6 stays empty; wall bodies at |x| 6..6.6.
#   Blender authoring: x across, +y along the way, z up. Wall pieces are authored for the WEST wall (x < 0, facing +x)
#   with their pivot on the bodies' face line (corridor x -6 = local x 0); the east wall is the same model turned 180
#   degrees about the vertical. Bay / CeilingBay are authored at a 1.9 m clear span (pier faces at y +-0.95) and the
#   loader scales them along the way by span / 1.9. Piers are 0.9 m along the way.
#   Exported like MiniGames/Early/mg_export.py (-Z forward, Y up, metres), so Unity hangs them under a pivot turned
#   180 degrees about y, as MiniGameModels does.
# Materials: one URP Lit .mat per kit material in Assets/Resources/PawnRushCorridors/Materials (PR_Cor_<Name>), using
# the mini-games' installed tileable textures (Assets/Art/PawnRushMiniGames/...) by guid. An existing .mat is never
# rewritten (it may be tuned in Unity). UVs are box-projected in metres / the material's tile size.
#
# Run (background Blender, never the shared live one):
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python <script> -- [--export] [--render]
import bpy, bmesh, hashlib, json, math, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
ART = "Assets/Art/PawnRushMiniGames/"
OUT_REL = "Assets/Resources/PawnRushCorridors"
MAT_REL = OUT_REL + "/Materials"
LIT = "933532a4fcc9baf4fa0491de14d08ed7"  # URP Lit
FBX_META = "Assets/Resources/PawnRushMiniGames/Early/D/D_CenterGate.fbx.meta"
FOLDER = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
NATIVE = "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# Pier centres and clear bay span (SectionCorridor / MiniGamePalaceArt.CorridorPier).
PIER = .9
SPAN = 1.9


def piers(z_from, z_to):
    return [z_from + PIER * .5 + (z_to - PIER * .5 - (z_from + PIER * .5)) * i / 3 for i in range(4)]


# ---------------------------------------------------------------- materials

# name: texture (under ART, without _BaseColor.png) or None, tint (sRGB hex), tile size in metres, roughness, metal,
# emission strength (only for window glass: daylight seen through it, not a lamp).
MATS = {
    "CreamStone":   ("Mid/Textures/PR_Mid_AshlarFill", "#F2E6CF", 2.4, .85, 0, 0),
    "SandStone":    ("Mid/Textures/PR_Mid_AshlarFill", "#E2C39A", 2.4, .9, 0, 0),
    "MarbleCream":  ("Late/Textures/PR_Late_MarbleCream", "#FFFFFF", 2.0, .45, 0, 0),
    "MarbleShade":  ("Late/Textures/PR_Late_MarbleCream", "#C9C3B6", 2.0, .5, 0, 0),
    "Walnut":       ("Late/Textures/PR_Late_Walnut", "#FFFFFF", 1.6, .6, 0, 0),
    "WalnutDark":   ("Late/Textures/PR_Late_Walnut", "#9A7C68", 1.6, .55, 0, 0),
    "Brass":        ("Mid/Textures/PR_Mid_MatteBrass", "#FFFFFF", .8, .45, .8, 0),
    "VelvetPurple": ("Late/Textures/PR_Late_Velvet", "#FFFFFF", 1.0, .9, 0, 0),
    "VelvetNavy":   ("Late/Textures/PR_Late_Velvet", "#5E6E9C", 1.0, .9, 0, 0),
    "Plaster":      (None, "#EFE6D4", 1.0, .9, 0, 0),
    "IronTeal":     (None, "#2F6E78", 1.0, .55, .5, 0),
    "IronDark":     (None, "#2E3135", 1.0, .5, .6, 0),
    "Glass":        (None, "#BFD8EA", 1.0, .1, 0, .15),
    "GlassClear":   (None, "#8E9EA6", 1.0, .05, .2, 0),
    "ShadeBrown":   (None, "#6E3B28", 1.0, .45, .6, 0),
    "Wax":          (None, "#EFE6D2", 1.0, .7, 0, 0),
    "GlassWarm":    (None, "#F3D3A0", 1.0, .1, 0, .45),
    "Canvas":       (None, "#4E3F35", 1.0, .95, 0, 0),
    "NavyPaint":    (None, "#2C3B5C", 1.0, .7, 0, 0),
    "BookRed":      (None, "#8C3A32", 1.0, .8, 0, 0),
    "BookGreen":    (None, "#3E5A46", 1.0, .8, 0, 0),
    "BookBlue":     (None, "#34466E", 1.0, .8, 0, 0),
    "BookTan":      (None, "#B08A5A", 1.0, .8, 0, 0),
    "ObsMarble":    ("RO:Marble", "#FFFFFF", 2.0, .4, 0, 0),
    "ObsNavy":      ("RO:NavyEnamel", "#FFFFFF", 1.5, .4, .3, 0),
    "ObsGold":      ("RO:Gold", "#FFFFFF", .8, .3, .9, 0),
    "ObsPearl":     ("RO:Pearl", "#FFFFFF", .5, .2, 0, .25),
    "ObsSkyGlass":  (None, "#3E5C9A", 1.0, .1, 0, .4),
    "Rug":          ("Late/Textures/PR_Late_I_CarpetDark", "#A8453C", 1.2, .95, 0, 0),
    "RugBorder":    (None, "#2F3E6E", 1.0, .95, 0, 0),
}


def tex_files(tex):
    """(base colour, normal) asset paths of a kit texture: "RO:<Name>" = the royal observatory's own textures
    (Assets/Art/RoyalObservatory/Textures/RO_<Name>_Albedo / _Normal), else a mini-game texture under ART."""
    if tex.startswith("RO:"):
        d = "Assets/Art/RoyalObservatory/Textures/RO_" + tex[3:]
        return d + "_Albedo.png", d + "_Normal.png"
    return ART + tex + "_BaseColor.png", ART + tex + "_Normal.png"


def srgb_to_lin(c):
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4


def hex_rgb(h):
    return [int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)]


_mat_cache = {}


def mat(key):
    """The Blender material for a kit key (preview shading: tinted texture)."""
    if key in _mat_cache:
        return _mat_cache[key]
    tex, tint, tile, rough, metal, emit = MATS[key]
    m = bpy.data.materials.new("PR_Cor_" + key)
    m.use_nodes = True
    nt = m.node_tree
    p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    lin = [srgb_to_lin(c) for c in hex_rgb(tint)] + [1]
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if tex:
        img = nt.nodes.new("ShaderNodeTexImage")
        img.image = bpy.data.images.load(os.path.join(ROOT, tex_files(tex)[0]), check_existing=True)
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1
        nt.links.new(img.outputs["Color"], mix.inputs[6])
        mix.inputs[7].default_value = lin
        nt.links.new(mix.outputs[2], p.inputs["Base Color"])
    else:
        p.inputs["Base Color"].default_value = lin
    if emit > 0:
        p.inputs["Emission Color"].default_value = lin
        p.inputs["Emission Strength"].default_value = emit
    m["cor_key"] = key
    _mat_cache[key] = m
    return m


# ---------------------------------------------------------------- geometry builder

class Mesh:
    """Collects boxes, extrusions and arches into one bmesh with a material slot per kit key."""

    # Anti-flicker (user 11-22 "깜빡깜빡 거리는 부분 다 고쳐"): no two flat faces of different parts may lie in one
    # plane, face the same way and overlap. Every box / prism / ring cap is checked against the planes this module
    # already holds and, if it would share one, grown by the next step (2.5 mm) until it does not. Modules that meet
    # other modules in the corridor (piers, ceiling beams, portals) start half a step (1.25 mm) off, so they can never
    # land in a plane of a wall, ceiling bay or header either.
    STEP, TOL = .0039, .0013
    GROUP_BASE = (("_Pier", .0013), ("_Portal", .0013), ("_CeilingBeam", .0026))

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.keys = []
        self.planes = []
        self.base = next((b for suffix, b in self.GROUP_BASE if name.endswith(suffix)), 0.0)

    @staticmethod
    def _box_planes(lo, hi):
        out = []
        for a in range(3):
            o1, o2 = [k for k in range(3) if k != a]
            rect = (lo[o1], hi[o1], lo[o2], hi[o2])
            out.append((a, -1, lo[a], rect))
            out.append((a, 1, hi[a], rect))
        return out

    def _clash(self, planes):
        for a, sgn, v, r in planes:
            for a2, s2, v2, r2 in self.planes:
                if a2 == a and s2 == sgn and abs(v2 - v) < self.TOL:
                    du = min(r[1], r2[1]) - max(r[0], r2[0])
                    dv = min(r[3], r2[3]) - max(r[2], r2[2])
                    if du > .01 and dv > .01 and du * dv > 1e-4:
                        return True
        return False

    def _grow(self, lo, hi, axes=(0, 1, 2)):
        """The first growth (base, base + step, ...) at which the box lo..hi shares no plane; registers its planes."""
        for k in range(12):
            d = self.base + self.STEP * k
            l2 = [lo[i] - (d if i in axes else 0) for i in range(3)]
            h2 = [hi[i] + (d if i in axes else 0) for i in range(3)]
            planes = [p for p in self._box_planes(l2, h2) if p[0] in axes]
            if not self._clash(planes):
                break
        self.planes.extend(planes)
        return l2, h2

    def _slot(self, key):
        if key not in self.keys:
            self.keys.append(key)
        return self.keys.index(key)

    def _tag(self, faces, key, smooth=False):
        i = self._slot(key)
        for f in faces:
            f.material_index = i
            f.smooth = smooth

    def box(self, x0, y0, z0, x1, y1, z1, key, bevel=.025):
        """An axis-aligned box between two corners (any order), its edges chamfered."""
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        z0, z1 = min(z0, z1), max(z0, z1)
        if x1 - x0 < 1e-4 or y1 - y0 < 1e-4 or z1 - z0 < 1e-4:
            return
        (x0, y0, z0), (x1, y1, z1) = self._grow([x0, y0, z0], [x1, y1, z1])
        r = bmesh.ops.create_cube(self.bm, size=1)
        vs = r["verts"]
        for v in vs:
            v.co = Vector((x0 if v.co.x < 0 else x1, y0 if v.co.y < 0 else y1, z0 if v.co.z < 0 else z1))
        faces = list({f for v in vs for f in v.link_faces})
        self._tag(faces, key)          # before the bevel: its faces copy the material of the faces they cut
        b = min(bevel, (x1 - x0) * .3, (y1 - y0) * .3, (z1 - z0) * .3)
        if b > .004:
            edges = list({e for f in faces for e in f.edges})
            vset = set(vs)
            res = bmesh.ops.bevel(self.bm, geom=edges + vs, offset=b, segments=1, affect="EDGES", profile=.5)
            self._tag([f for f in res["faces"] if f.is_valid], key)
            # Faces the bevel rebuilt in place: every face now touching the cube's (moved) verts.
            near = {f for e in res.get("edges", []) if e.is_valid for f in e.link_faces}
            self._tag([f for f in near if f.is_valid], key)

    def prism(self, outline, axis, a0, a1, key):
        """Extrudes a closed 2D outline (list of (u, v)) along `axis` ('x', 'y' or 'z') from a0 to a1.
        x: (u, v) = (y, z); y: (u, v) = (x, z); z: (u, v) = (x, y)."""
        def p3(u, v, a):
            return Vector((a, u, v)) if axis == "x" else Vector((u, a, v)) if axis == "y" else Vector((u, v, a))
        a0, a1 = self._caps(outline, axis, a0, a1)
        lo = [self.bm.verts.new(p3(u, v, a0)) for u, v in outline]
        hi = [self.bm.verts.new(p3(u, v, a1)) for u, v in outline]
        faces = []
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
        faces.append(self.bm.faces.new(list(reversed(lo))))
        faces.append(self.bm.faces.new(hi))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._tag(faces, key)

    def _caps(self, outline, axis, a0, a1):
        """A prism's two caps go through the same plane check as a box's faces (along its axis only)."""
        ax = "xyz".index(axis)
        us, vs = [p[0] for p in outline], [p[1] for p in outline]
        lo, hi = [0.0] * 3, [0.0] * 3
        o1, o2 = [k for k in range(3) if k != ax]
        lo[ax], hi[ax] = min(a0, a1), max(a0, a1)
        lo[o1], hi[o1], lo[o2], hi[o2] = min(us), max(us), min(vs), max(vs)
        l2, h2 = self._grow(lo, hi, axes=(ax,))
        return (l2[ax], h2[ax]) if a0 <= a1 else (h2[ax], l2[ax])

    def ring(self, outer, inner, axis, a0, a1, key):
        """A frame between two closed 2D outlines with the same point count (an arch ring, a window frame)."""
        def p3(u, v, a):
            return Vector((a, u, v)) if axis == "x" else Vector((u, a, v)) if axis == "y" else Vector((u, v, a))
        a0, a1 = self._caps(outer, axis, a0, a1)
        # The frame's own side faces stand 1.9 mm off the nominal outlines (inner in, outer out), so they never lie in
        # the plane of a box built to the same numbers (a sill, a panel, the wall around).
        j = self.base + .0019
        def off(pts, sign):
            cu = sum(p[0] for p in pts) / len(pts)
            cv = sum(p[1] for p in pts) / len(pts)
            out = []
            for u, v in pts:
                # Each axis on its own, so a rectangle's straight sides move the full j.
                su = 0 if abs(u - cu) < 1e-6 else (1 if u > cu else -1)
                sv = 0 if abs(v - cv) < 1e-6 else (1 if v > cv else -1)
                out.append((u + sign * j * su, v + sign * j * sv))
            return out
        outer, inner = off(outer, 1), off(inner, -1)
        n = len(outer)
        L = {}
        for name, pts in (("o", outer), ("i", inner)):
            for a_name, a in (("0", a0), ("1", a1)):
                L[name + a_name] = [self.bm.verts.new(p3(u, v, a)) for u, v in pts]
        faces = []
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new((L["o0"][i], L["o0"][j], L["o1"][j], L["o1"][i])))
            faces.append(self.bm.faces.new((L["i0"][j], L["i0"][i], L["i1"][i], L["i1"][j])))
            faces.append(self.bm.faces.new((L["o0"][j], L["o0"][i], L["i0"][i], L["i0"][j])))
            faces.append(self.bm.faces.new((L["o1"][i], L["o1"][j], L["i1"][j], L["i1"][i])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._tag(faces, key)

    def cylinder(self, c, r, z0, z1, key, seg=12, axis="z"):
        """A cylinder of radius r around centre c (2D in the plane across `axis`) from z0 to z1 along `axis`."""
        pts = [(c[0] + r * math.cos(2 * math.pi * i / seg), c[1] + r * math.sin(2 * math.pi * i / seg)) for i in range(seg)]
        self.prism(pts, axis, z0, z1, key)

    def lathe(self, profile, centre, key, seg=16, axis="z"):
        """A solid of revolution: profile = [(radius, height), ...] from bottom to top (radius 0 closes an end),
        turned about a line through `centre` along `axis` ('z' up, 'x' across: then height runs along +x)."""
        cx, cy, cz = centre
        rings = []
        for r, h in profile:
            ring = []
            for i in range(seg):
                a = 2 * math.pi * i / seg
                if axis == "z":
                    co = Vector((cx + r * math.cos(a), cy + r * math.sin(a), cz + h))
                else:
                    co = Vector((cx + h, cy + r * math.cos(a), cz + r * math.sin(a)))
                ring.append(self.bm.verts.new(co))
            rings.append(ring)
        faces = []
        for k in range(len(rings) - 1):
            a, b = rings[k], rings[k + 1]
            for i in range(seg):
                j = (i + 1) % seg
                quad = [a[i], a[j], b[j], b[i]]
                uniq = []
                for v in quad:
                    if all((v.co - w.co).length > 1e-6 for w in uniq):
                        uniq.append(v)
                if len(uniq) >= 3:
                    try:
                        faces.append(self.bm.faces.new(uniq))
                    except ValueError:
                        pass
        if profile[0][0] > 1e-6:
            faces.append(self.bm.faces.new(list(reversed(rings[0]))))
        if profile[-1][0] > 1e-6:
            faces.append(self.bm.faces.new(rings[-1]))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._tag(faces, key, True)

    def sphere(self, c, r, key, seg=12, rings=8):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=r)
        for v in res["verts"]:
            v.co += Vector(c)
        self._tag({f for v in res["verts"] for f in v.link_faces}, key, True)

    def tube(self, pts, r, key, seg=8, closed=False, caps=True):
        """A round tube swept along a polyline of Vectors (parallel-transport frames)."""
        pts = [Vector(p) for p in pts]
        n = len(pts)
        tans = []
        for i in range(n):
            if closed:
                t = pts[(i + 1) % n] - pts[i - 1]
            else:
                t = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
            tans.append(t.normalized())
        ref = Vector((0, 0, 1)) if abs(tans[0].z) < .9 else Vector((1, 0, 0))
        nrm = (ref - tans[0] * ref.dot(tans[0])).normalized()
        rings = []
        for i in range(n):
            t = tans[i]
            nrm = (nrm - t * nrm.dot(t)).normalized()
            bi = t.cross(nrm)
            rings.append([self.bm.verts.new(pts[i] + (nrm * math.cos(2 * math.pi * k / seg) + bi * math.sin(2 * math.pi * k / seg)) * r)
                          for k in range(seg)])
        faces = []
        last = n if closed else n - 1
        for i in range(last):
            a, b = rings[i], rings[(i + 1) % n]
            for k in range(seg):
                j = (k + 1) % seg
                faces.append(self.bm.faces.new((a[k], a[j], b[j], b[k])))
        if not closed and caps:
            faces.append(self.bm.faces.new(list(reversed(rings[0]))))
            faces.append(self.bm.faces.new(rings[-1]))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._tag(faces, key, True)

    def torus(self, c, R, r, key, seg=24, tseg=8, axis="z"):
        """A ring of major radius R around centre c, in the plane across `axis`."""
        pts = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            if axis == "z":
                pts.append((c[0] + R * math.cos(a), c[1] + R * math.sin(a), c[2]))
            elif axis == "x":
                pts.append((c[0], c[1] + R * math.cos(a), c[2] + R * math.sin(a)))
            else:
                pts.append((c[0] + R * math.cos(a), c[1], c[2] + R * math.sin(a)))
        self.tube(pts, r, key, tseg, closed=True)

    def finish(self, collection, location=(0, 0, 0)):
        """Box-projected UVs (metres / tile), merge doubles, an object in `collection`."""
        bm = self.bm
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            key = self.keys[f.material_index]
            tile = MATS[key][2]
            n = f.normal
            ax = max(range(3), key=lambda k: abs(n[k]))
            for loop in f.loops:
                co = loop.vert.co
                if ax == 0:
                    u, v = co.y, co.z
                elif ax == 1:
                    u, v = co.x, co.z
                else:
                    u, v = co.x, co.y
                loop[uv].uv = (u / tile, v / tile)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for k in self.keys:
            me.materials.append(mat(k))
        ob = bpy.data.objects.new(self.name, me)
        ob.location = location
        collection.objects.link(ob)
        return ob


def wall_with_arch(m, y0, y1, z0, z1, cx, w, sill, spring, x_face, x_back, key, seg=10):
    """A wall slab across y0..y1, z0..z1 (face at x_face, back at x_back, a west-wall piece: x_face > x_back) with a
    round-headed opening (centre y cx, width w, from `sill` up to `spring`, then a half-round head). Built from
    convex boxes and vertical strips over the head so every face triangulates cleanly; the opening's reveals come
    with them."""
    r = w * .5
    m.box(x_back, y0, z0, x_face, y1, sill, key, 0)                          # under the sill
    m.box(x_back, y0, sill, x_face, cx - r, z1, key, 0)                      # left of the opening
    m.box(x_back, cx + r, sill, x_face, y1, z1, key, 0)                      # right of it
    top = spring + r
    for i in range(seg):
        a0, a1 = math.pi * i / seg, math.pi * (i + 1) / seg
        u0, u1 = cx + r * math.cos(a0), cx + r * math.cos(a1)
        v0, v1 = spring + r * math.sin(a0), spring + r * math.sin(a1)
        m.prism([(u1, v1), (u0, v0), (u0, z1), (u1, z1)], "x", x_back, x_face, key)
    return top


def arc_fill(m, u0, u1, v_spring, rise, v_top, axis, a0, a1, key, seg=12):
    """The solid between a segmental arc (from (u0, v_spring) through the crown v_spring + rise to (u1, v_spring))
    and the line v = v_top above it: a beam with an arched underside. Vertical strips, each convex."""
    half = (u1 - u0) * .5
    R = (half * half + rise * rise) / (2 * rise)
    cu, cv = u0 + half, v_spring + rise - R
    def v_at(u):
        return cv + math.sqrt(max(0.0, R * R - (u - cu) ** 2))
    for i in range(seg):
        ua, ub = u0 + (u1 - u0) * i / seg, u0 + (u1 - u0) * (i + 1) / seg
        m.prism([(ua, v_at(ua)), (ub, v_at(ub)), (ub, v_top), (ua, v_top)], axis, a0, a1, key)


def bez(p0, p1, p2, p3, n=10):
    """Points on a cubic Bezier."""
    p0, p1, p2, p3 = (Vector(p) for p in (p0, p1, p2, p3))
    out = []
    for i in range(n + 1):
        t = i / n
        s = 1 - t
        out.append(p0 * s ** 3 + p1 * 3 * s * s * t + p2 * 3 * s * t * t + p3 * t ** 3)
    return out


def spiral(c, r0, r1, turns, plane, a0=0.0, n=24, flip=1):
    """A flat scroll curl (Archimedean spiral) around centre c. plane 'yz' (on a wall facing +x) or 'xz' / 'xy'."""
    out = []
    for i in range(n + 1):
        t = i / n
        a = a0 + flip * 2 * math.pi * turns * t
        r = r0 + (r1 - r0) * t
        u, v = r * math.cos(a), r * math.sin(a)
        if plane == "yz":
            out.append(Vector((c[0], c[1] + u, c[2] + v)))
        elif plane == "xz":
            out.append(Vector((c[0] + u, c[1], c[2] + v)))
        else:
            out.append(Vector((c[0] + u, c[1] + v, c[2])))
    return out


def arch(cx, w, spring, seg=10, v_round=None):
    """A semicircular-headed opening outline (u across, v up), bottom at v 0, sides to `spring`, then a round head."""
    r = w * .5
    pts = [(cx - r, 0.0), (cx + r, 0.0), (cx + r, spring)]
    for i in range(1, seg):
        a = math.pi * i / seg
        pts.append((cx + r * math.cos(a), spring + r * math.sin(a)))
    pts.append((cx - r, spring))
    return pts


def lift(pts, dv):
    return [(u, v + dv) for u, v in pts]


def grow(pts, cx, spring, d):
    """The same arch outline grown by d (a frame's outside): wider by d each side, round head bigger by d."""
    out = []
    for u, v in pts:
        if v <= spring + 1e-6:
            out.append((u + (d if u > cx else -d), v - (d if v < 1e-6 else 0)))
        else:
            du, dv = u - cx, v - spring
            L = math.hypot(du, dv)
            out.append((cx + du * (L + d) / L, spring + dv * (L + d) / L))
    return out


def empty(collection, name, loc):
    e = bpy.data.objects.new(name, None)
    e.location = loc
    e.empty_display_size = .2
    collection.objects.link(e)
    return e


# ---------------------------------------------------------------- scene, preview, export

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mat_cache.clear()


def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons) if ob.type == "MESH" else 0


def guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def folder(rel):
    parts = rel.split("/")
    for i in range(2, len(parts) + 1):
        sub = "/".join(parts[:i])
        os.makedirs(os.path.join(ROOT, sub), exist_ok=True)
        if not os.path.exists(os.path.join(ROOT, sub + ".meta")):
            write(sub + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (guid(sub), FOLDER))


def meta_guid(rel):
    with open(os.path.join(ROOT, rel + ".meta"), encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":")[1].strip()
    raise RuntimeError("no guid in " + rel)


MAT = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {lit}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:{keywords}
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap:
    RenderType: Opaque
  disabledShaderPasses:
  - MOTIONVECTORS
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {tex}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _BumpMap:
        m_Texture: {bump}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {tex}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _EmissionMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: 0
    - _BumpScale: 1
    - _Cull: 2
    - _DstBlend: 0
    - _DstBlendAlpha: 0
    - _EnvironmentReflections: 1
    - _Metallic: {metal}
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: {smooth}
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _SrcBlendAlpha: 1
    - _Surface: 0
    - _WorkflowMode: 1
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: {{r: {r}, g: {g}, b: {b}, a: 1}}
    - _Color: {{r: {r}, g: {g}, b: {b}, a: 1}}
    - _EmissionColor: {{r: {er}, g: {eg}, b: {eb}, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
--- !u!114 &7772292899409006985
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion
  version: 10
"""


def write_materials(keys):
    folder(MAT_REL)
    for key in keys:
        name = "PR_Cor_" + key
        rel = MAT_REL + "/" + name + ".mat"
        if os.path.exists(os.path.join(ROOT, rel)):
            print("MAT kept", name)
            continue
        tex, tint, tile, rough, metal, emit = MATS[key]
        r, g, b = hex_rgb(tint)
        kw = []
        texref = bump = "{fileID: 0}"
        if tex:
            base, nrm = tex_files(tex)
            texref = "{fileID: 2800000, guid: %s, type: 3}" % meta_guid(base)
            if os.path.exists(os.path.join(ROOT, nrm + ".meta")):
                bump = "{fileID: 2800000, guid: %s, type: 3}" % meta_guid(nrm)
                kw.append("_NORMALMAP")
        if emit > 0:
            kw.append("_EMISSION")
        write(rel, MAT.format(name=name, lit=LIT, keywords="".join("\n  - " + k for k in kw) if kw else " []",
                              tex=texref, bump=bump, metal="%.3f" % metal, smooth="%.3f" % (1 - rough),
                              r="%.4f" % r, g="%.4f" % g, b="%.4f" % b,
                              er="%.4f" % (r * emit), eg="%.4f" % (g * emit), eb="%.4f" % (b * emit)))
        if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
            write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (guid(rel), NATIVE))
        print("MAT", name)


def export_module(ob, fbx_rel, extra=()):
    """One module (and its LIGHT_ empties) to one FBX, pivot = the object's origin at the world origin."""
    folder(os.path.dirname(fbx_rel))
    bpy.ops.object.select_all(action="DESELECT")
    loc = ob.location.copy()
    ob.location = (0, 0, 0)
    for e in extra:
        e.location -= loc
    ob.select_set(True)
    for e in extra:
        e.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, fbx_rel), use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
    ob.location = loc
    for e in extra:
        e.location += loc
    if not os.path.exists(os.path.join(ROOT, fbx_rel + ".meta")):
        with open(os.path.join(ROOT, FBX_META), encoding="utf-8") as f:
            lines = f.read().split("\n")
        lines[1] = "guid: " + guid(fbx_rel)
        write(fbx_rel + ".meta", "\n".join(lines))
    print("EXPORT %-60s %6d tris" % (fbx_rel, tris(ob)))


def place(src, col, loc, rot_z=0.0, sy=1.0):
    """A linked copy of a module in the preview assembly."""
    o = bpy.data.objects.new(src.name + "_at", src.data)
    o.location = loc
    o.rotation_euler = (0, 0, rot_z)
    o.scale = (1, sy, 1)
    col.objects.link(o)
    return o


def assemble(mods, height, z_from, z_to, portal_start, portal_end, header_start, header_end, end_wall=None,
             centre=()):
    """The whole corridor from its modules, as the loader will place them: west wall at corridor x -6 (pivot),
    the east wall turned 180 degrees, bays and ceiling bays stretched to the real span."""
    col = collection("Preview")
    ps = piers(z_from, z_to)
    span = (ps[1] - ps[0]) - PIER
    for side in (-1, 1):
        rot = 0 if side < 0 else math.pi
        for i, z in enumerate(ps):
            # As the loader: a portal at an end swallows that end's pier.
            if not ((i == 0 and portal_start and "Portal" in mods) or (i == 3 and portal_end and "Portal" in mods)):
                place(mods["Pier"], col, (side * 6, z, 0), rot)
            if "CeilingBeam" in mods and side < 0:
                place(mods["CeilingBeam"], col, (0, z, height))
        for i in range(3):
            zc = (ps[i] + ps[i + 1]) * .5
            m = mods.get("BayMid") if i == 1 and "BayMid" in mods else mods["Bay"]
            place(m, col, (side * 6, zc, 0), rot, span / SPAN)
            if "CeilingBay" in mods and side < 0:
                place(mods["CeilingBay"], col, (0, zc, height), 0, span / SPAN)
    if portal_start and "Portal" in mods:
        place(mods["Portal"], col, (0, 0, 0))
    if portal_end and "Portal" in mods:
        place(mods["Portal"], col, (0, 10, 0), math.pi)
    if header_start and "Header" in mods:
        place(mods["Header"], col, (0, z_from, height))
    if header_end and "Header" in mods:
        place(mods["Header"], col, (0, z_to, height), math.pi)
    if end_wall is not None:
        place(end_wall, col, (0, 10, 0))
    if "FloorInlay" in mods:
        place(mods["FloorInlay"], col, (0, 0, 0))
    for name, (x, y, z) in centre:
        place(mods[name], col, (x, y, z))
    # The code's floor paving (stand-in), the ceiling slab above, pawns for scale.
    g = Mesh("Preview floor")
    for xi in range(6):
        for zi in range(5):
            g.box(xi * 2 - 6 + .015, zi * 2 + .015, -.2, xi * 2 - 4 - .015, zi * 2 + 2 - .015, .024,
                  "MarbleShade" if (xi + zi) & 1 else "MarbleCream", .01)
    for i, x in enumerate((-1.6, 0, 1.6)):
        g.cylinder((x, 1.6), .32, .02, .75, "Plaster", 14)
        g.box(x - .2, 1.42, .75, x + .2, 1.78, 1.0, "Plaster", .1)
    g.finish(col)
    return col


def render_views(col, folder_rel, prefix, height, views):
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = 1280, 860
    ee = scene.eevee
    for attr, val in (("use_gtao", True), ("gtao_distance", 1.5), ("use_raytracing", True), ("use_shadows", True),
                      ("fast_gi_method", "GLOBAL_ILLUMINATION")):
        try:
            setattr(ee, attr, val)
        except (AttributeError, TypeError):
            pass
    scene.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in
        bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items] else "Standard"
    world = bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.62, .72, .86, 1)
    bg.inputs["Strength"].default_value = .55
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), math.radians(20), math.radians(35))
    scene.collection.objects.link(sun)
    for i, y in enumerate((1.7, 5.0, 8.3)):
        L = bpy.data.objects.new("Lamp%d" % i, bpy.data.lights.new("Lamp%d" % i, "POINT"))
        L.data.energy = 1100
        L.data.shadow_soft_size = .15
        L.data.color = (1, .82, .6)
        L.location = (0, y, height - 2.2)
        scene.collection.objects.link(L)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    out = []
    for name, eye, look, lens in views:
        cam.location = eye
        d = Vector(look) - Vector(eye)
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = lens
        path = os.path.join(ROOT, folder_rel, "%s_%s.png" % (prefix, name))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        out.append(path)
        print("RENDER", path)
    return out


STD_VIEWS = [
    ("pawn", (0, -2.0, 3.0), (0, 9, 2.8), 18),
    ("wall", (3.5, 2.0, 2.0), (-6, 6, 3.0), 20),
    ("outside", (-14, -4, 6), (-6, 5, 3), 24),
]

# Low-poly building blocks for the turned pieces (king, queen): rings in design-image pixels about the axis,
# flat faces, a face part number per face. The piece faces -y; rows count down from the image's top.
import bpy, bmesh, math
from mathutils import Vector

class Builder:
    def __init__(self, row_top, row_bot, z_top, z_bot):
        self.bm = bmesh.new()
        self.part = self.bm.faces.layers.int.new("part")
        self.S = (z_top - z_bot) / (row_bot - row_top)
        self.row_bot, self.z_bot = row_bot, z_bot

    def Z(self, row):
        return self.z_bot + (self.row_bot - row) * self.S

    def P(self, x, y, row):
        return Vector((x * self.S, y * self.S, self.Z(row)))

    def ring(self, pts):
        return [self.bm.verts.new(p) for p in pts]

    def circle(self, hw, row, n=16, vertex_front=False, cx=0.0, cy=0.0):
        """A ring of n corners. Face-centred on the front by default (its flat faces reach hw at the front and
        sides); with vertex_front a corner points to the front and hw is the corners' radius."""
        if vertex_front:
            rv, a0 = hw, -math.pi / 2
        else:
            rv, a0 = hw / math.cos(math.pi / n), -math.pi / 2 + math.pi / n
        return self.ring([self.P(cx + rv * math.cos(a0 + k * 2 * math.pi / n),
                                 cy + rv * math.sin(a0 + k * 2 * math.pi / n), row) for k in range(n)])

    def bridge(self, lo, hi, part):
        n = len(lo)
        for k in range(n):
            k1 = (k + 1) % n
            f = self.bm.faces.new((lo[k], lo[k1], hi[k1], hi[k]))
            f[self.part] = part

    def fan(self, r, centre, top, part):
        c = self.bm.verts.new(centre)
        n = len(r)
        for k in range(n):
            k1 = (k + 1) % n
            f = self.bm.faces.new((r[k], r[k1], c) if top else (r[k1], r[k], c))
            f[self.part] = part

    def lathe(self, prof, part, n=16, cap_bottom=True, cap_top=True, vertex_front=False):
        """prof: (half width, row) from the bottom up."""
        rings = [self.circle(hw, row, n, vertex_front) for hw, row in prof]
        for lo, hi in zip(rings, rings[1:]):
            self.bridge(lo, hi, part)
        if cap_bottom:
            self.fan(rings[0], self.P(0, 0, prof[0][1]), False, part)
        if cap_top:
            self.fan(rings[-1], self.P(0, 0, prof[-1][1]), True, part)
        return rings

    def sphere(self, cx, cy, row, r, part, n=16, bands=8, lat0=-90.0, lat1=90.0):
        """A sphere (or the slice between two latitudes, closed flat at a cut) of n sides; r in pixels."""
        lats = [lat0 + i * (lat1 - lat0) / bands for i in range(bands + 1)]
        rings, poles = [], {}
        for la in lats:
            ph = math.radians(la)
            if abs(abs(la) - 90.0) < 1e-6:
                poles[la] = self.P(cx, cy, row - r * math.sin(ph))
                continue
            rings.append((la, self.circle(r * math.cos(ph), row - r * math.sin(ph), n, False, cx, cy)))
        for (la, lo), (lb, hi) in zip(rings, rings[1:]):
            self.bridge(lo, hi, part)
        la, lo = rings[0]
        self.fan(lo, poles.get(lats[0], self.P(cx, cy, row - r * math.sin(math.radians(la)))), False, part)
        lb, hi = rings[-1]
        self.fan(hi, poles.get(lats[-1], self.P(cx, cy, row - r * math.sin(math.radians(lb)))), True, part)

    def finish(self, name, obj_name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        bpy.ops.wm.read_factory_settings(use_empty=True)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(obj_name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob

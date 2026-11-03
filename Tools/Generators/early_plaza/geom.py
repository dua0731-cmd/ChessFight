# -*- coding: utf-8 -*-
# Small SVG helpers for the early mini-game section sheet: plan (x, z), section (z, y) and a pinhole view.
import math
from html import escape as esc


def n(v):
    return f"{v:.1f}"


class Fig:
    def __init__(self, fid, w, h, label, cls="dg"):
        self.fid, self.w, self.h, self.label, self.cls = fid, w, h, label, cls
        self.parts, self.defs = [], []

    def add(self, s):
        self.parts.append(s)

    def marker(self, suffix="", cls="mk-ink"):
        # Markers take their colour from their own path's class (not from the line that uses them).
        mid = self.fid + "-arw" + suffix
        self.defs.append(
            f'<marker id="{mid}" viewBox="0 0 10 10" refX="8.5" refY="5" markerWidth="7" markerHeight="7" '
            f'orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" class="{cls}"/></marker>')
        return mid

    def svg(self):
        d = "<defs>" + "".join(self.defs) + "</defs>" if self.defs else ""
        return (f'<svg viewBox="0 0 {self.w} {self.h}" class="{self.cls}" role="img" aria-label="{esc(self.label)}" '
                f'xmlns="http://www.w3.org/2000/svg">{d}{"".join(self.parts)}</svg>')


def text(fig, x, y, s, cls="", anchor="middle", size=None, weight=None):
    a = f' font-size="{size}"' if size else ""
    if weight:
        a += f' font-weight="{weight}"'
    fig.add(f'<text x="{n(x)}" y="{n(y)}" text-anchor="{anchor}" class="{cls}"{a}>{esc(s)}</text>')


class Plan:
    """Plan view: x east -> right, z north -> up."""

    def __init__(self, fig, xmin, zmax, s, ox, oy):
        self.f, self.xmin, self.zmax, self.s, self.ox, self.oy = fig, xmin, zmax, s, ox, oy

    def P(self, x, z):
        return self.ox + (x - self.xmin) * self.s, self.oy + (self.zmax - z) * self.s

    def rect(self, x0, x1, z0, z1, cls, extra=""):
        ax, ay = self.P(min(x0, x1), max(z0, z1))
        self.f.add(f'<rect x="{n(ax)}" y="{n(ay)}" width="{n(abs(x1 - x0) * self.s)}" '
                   f'height="{n(abs(z1 - z0) * self.s)}" class="{cls}" {extra}/>')

    def line(self, x0, z0, x1, z1, cls, extra=""):
        a, b = self.P(x0, z0), self.P(x1, z1)
        self.f.add(f'<line x1="{n(a[0])}" y1="{n(a[1])}" x2="{n(b[0])}" y2="{n(b[1])}" class="{cls}" {extra}/>')

    def poly(self, pts, cls, extra=""):
        p = " ".join(f"{n(a)},{n(b)}" for a, b in (self.P(x, z) for x, z in pts))
        self.f.add(f'<polygon points="{p}" class="{cls}" {extra}/>')

    def path(self, d_m, cls, extra=""):
        # d_m: list of (cmd, x, z) with M/L only
        out = []
        for c, x, z in d_m:
            a, b = self.P(x, z)
            out.append(f"{c}{n(a)} {n(b)}")
        self.f.add(f'<path d="{" ".join(out)}" class="{cls}" {extra}/>')

    def circle(self, x, z, r, cls, extra=""):
        a, b = self.P(x, z)
        self.f.add(f'<circle cx="{n(a)}" cy="{n(b)}" r="{n(r * self.s)}" class="{cls}" {extra}/>')

    def arc(self, x, z, r, a0, a1, cls, extra=""):
        # angles in degrees, measured from +x towards +z (counter-clockwise on the plan)
        p0 = self.P(x + r * math.cos(math.radians(a0)), z + r * math.sin(math.radians(a0)))
        p1 = self.P(x + r * math.cos(math.radians(a1)), z + r * math.sin(math.radians(a1)))
        large = 1 if abs(a1 - a0) > 180 else 0
        sweep = 0 if a1 > a0 else 1  # screen y is flipped
        self.f.add(f'<path d="M{n(p0[0])} {n(p0[1])} A{n(r * self.s)} {n(r * self.s)} 0 {large} {sweep} '
                   f'{n(p1[0])} {n(p1[1])}" class="{cls}" {extra}/>')

    def text(self, x, z, s, cls="", anchor="middle", dy=0, size=None, weight=None):
        a, b = self.P(x, z)
        text(self.f, a, b + dy, s, cls, anchor, size, weight)

    def callout(self, x, z, num):
        a, b = self.P(x, z)
        self.f.add(f'<g class="co"><circle cx="{n(a)}" cy="{n(b)}" r="8.5"/>'
                   f'<text x="{n(a)}" y="{n(b + 3.6)}" text-anchor="middle">{num}</text></g>')


class Section:
    """Section view: z north -> right, y up -> up."""

    def __init__(self, fig, zmin, ymax, s, ox, oy):
        self.f, self.zmin, self.ymax, self.s, self.ox, self.oy = fig, zmin, ymax, s, ox, oy

    def P(self, z, y):
        return self.ox + (z - self.zmin) * self.s, self.oy + (self.ymax - y) * self.s

    def rect(self, z0, z1, y0, y1, cls, extra=""):
        ax, ay = self.P(min(z0, z1), max(y0, y1))
        self.f.add(f'<rect x="{n(ax)}" y="{n(ay)}" width="{n(abs(z1 - z0) * self.s)}" '
                   f'height="{n(abs(y1 - y0) * self.s)}" class="{cls}" {extra}/>')

    def line(self, z0, y0, z1, y1, cls, extra=""):
        a, b = self.P(z0, y0), self.P(z1, y1)
        self.f.add(f'<line x1="{n(a[0])}" y1="{n(a[1])}" x2="{n(b[0])}" y2="{n(b[1])}" class="{cls}" {extra}/>')

    def poly(self, pts, cls, extra=""):
        p = " ".join(f"{n(a)},{n(b)}" for a, b in (self.P(z, y) for z, y in pts))
        self.f.add(f'<polygon points="{p}" class="{cls}" {extra}/>')

    def text(self, z, y, s, cls="", anchor="middle", dy=0, size=None, weight=None):
        a, b = self.P(z, y)
        text(self.f, a, b + dy, s, cls, anchor, size, weight)


class View:
    """Pinhole camera on the exit terrace looking north (+z), pitched down."""

    def __init__(self, fig, w, h, eye=(0.0, 5.0, -9.0), pitch=12.0, hfov=84.0):
        self.f, self.w, self.h = fig, w, h
        self.e = eye
        t = math.radians(pitch)
        self.c, self.sn = math.cos(t), math.sin(t)
        self.F = (w / 2) / math.tan(math.radians(hfov / 2))
        self.boxes = []  # (depth, svg)

    def cam(self, x, y, z):
        dx, dy, dz = x - self.e[0], y - self.e[1], z - self.e[2]
        return dx, dy * self.c + dz * self.sn, -dy * self.sn + dz * self.c

    def P(self, x, y, z):
        xc, yc, zc = self.cam(x, y, z)
        zc = max(zc, 0.2)
        return self.w / 2 + self.F * xc / zc, self.h / 2 - self.F * yc / zc

    def depth(self, x, y, z):
        return self.cam(x, y, z)[2]

    def face(self, pts, cls, shade=None):
        p = " ".join(f"{n(a)},{n(b)}" for a, b in (self.P(*q) for q in pts))
        s = f'<polygon points="{p}" class="{cls}"/>'
        if shade == "top":
            s += f'<polygon points="{p}" class="sh-top"/>'
        elif shade == "side":
            s += f'<polygon points="{p}" class="sh-side"/>'
        return s

    def ground(self, x0, x1, z0, z1, y, cls):
        self.f.add(self.face([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], cls))

    def disc(self, x, z, r, y, cls, k=28):
        pts = [(x + r * math.cos(2 * math.pi * i / k), y, z + r * math.sin(2 * math.pi * i / k)) for i in range(k)]
        return self.face(pts, cls)

    def box_svg(self, x0, x1, y0, y1, z0, z1, cls, top_cls=None):
        ex, ey, ez = self.e
        out = []
        if x0 > ex:
            out.append(self.face([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], cls, "side"))
        if x1 < ex:
            out.append(self.face([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], cls, "side"))
        if y1 < ey:
            out.append(self.face([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], top_cls or cls, "top"))
        out.append(self.face([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], cls))
        return "".join(out)

    def box(self, x0, x1, y0, y1, z0, z1, cls, top_cls=None):
        d = self.depth((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
        self.boxes.append((d, self.box_svg(x0, x1, y0, y1, z0, z1, cls, top_cls)))

    def item(self, depth_at, svg):
        self.boxes.append((self.depth(*depth_at), svg))

    def seg(self, a, b, cls, extra=""):
        p, q = self.P(*a), self.P(*b)
        return f'<line x1="{n(p[0])}" y1="{n(p[1])}" x2="{n(q[0])}" y2="{n(q[1])}" class="{cls}" {extra}/>'

    def flush(self):
        for _, s in sorted(self.boxes, key=lambda t: -t[0]):
            self.f.add(s)
        self.boxes = []

# Top-down layout map of the I king's bedchamber from the game coordinates (MgCheckBlock / CheckBlockGenerator),
# used as a reference image for the GPT concept sheet. One hand-made position that follows the generator's rules
# (the real position changes every round). numpy only.
# Run: "<Blender>/5.2/python/bin/python.exe" -I i_layout_map.py <out.png>
import sys, zlib, struct
import numpy as np

S = 22                      # pixels per metre
M = 30                      # margin (px)
W, H = int(44 * S + 2 * M), int(36 * S + 2 * M)
img = np.full((H, W, 3), (244, 236, 220), np.uint8)
yy, xx = np.ogrid[:H, :W]

def px(x, z):               # room metres -> pixel (back wall at the top, entrance at the bottom)
    return M + (x + 22) * S, M + (36 - z) * S

def rect(x0, z0, x1, z1, c):
    a, b = px(min(x0, x1), max(z0, z1)); d, e = px(max(x0, x1), min(z0, z1))
    img[int(b):int(e), int(a):int(d)] = c

def disc(x, z, r, c):
    cx, cy = px(x, z); rr = r * S
    img[(xx - cx) ** 2 + (yy - cy) ** 2 <= rr * rr] = c

def ring(x, z, r, w, c):
    cx, cy = px(x, z); r0, r1 = (r - w) * S, r * S
    d = (xx - cx) ** 2 + (yy - cy) ** 2
    img[(d <= r1 * r1) & (d >= r0 * r0)] = c

def line(x0, z0, x1, z1, w, c):
    ax, ay = px(x0, z0); bx, by = px(x1, z1)
    dx, dy = bx - ax, by - ay; L2 = dx * dx + dy * dy
    t = np.clip(((xx - ax) * dx + (yy - ay) * dy) / L2, 0, 1)
    d2 = (xx - (ax + t * dx)) ** 2 + (yy - (ay + t * dy)) ** 2
    img[d2 <= (w * S / 2) ** 2] = c

def diamond(x, z, r, w, c):
    cx, cy = px(x, z); d = np.abs(xx - cx) + np.abs(yy - cy)
    img[(d <= r * S) & (d >= (r - w) * S)] = c

WALL, FLOOR, AISLE, RUNNER = (120, 104, 90), (60, 66, 92), (52, 58, 84), (110, 70, 140)
LIGHT, DARK, RAIL, GROOVE, CART = (203, 191, 159), (167, 154, 124), (106, 97, 82), (29, 32, 40), (120, 78, 45)
GOLD, RED = (225, 180, 60), (220, 40, 40)
BLUE, ORANGE, EBONY, IVORY = (63, 193, 217), (240, 138, 44), (47, 41, 38), (243, 234, 219)

rect(-23, -0.5, 23, 36.5, WALL)                     # outer walls
rect(-22, 0, 22, 36, FLOOR)                         # navy marble room floor
rect(-3, 0, 3, 24, AISLE)                           # central aisle
rect(-.8, 0, .8, 23, RUNNER)                        # narrow velvet runner
rect(-10, 24, 10, 30, (226, 210, 180))              # closed royal facade block (does not open)
rect(-22, 24, -14, 30, WALL); rect(14, 24, 22, 30, WALL)
rect(-3, 24, 3, 24.6, (40, 50, 90))                 # closed royal door
rect(-22, 30, 22, 36, (150, 150, 155))              # back corridor behind the team doors to the exit
rect(-6, -0.5, 6, 0, FLOOR); rect(-6, 36, 6, 36.5, FLOOR)   # entrance / exit openings

FILES = 'abcdefgh'
def sq(name):               # square -> station (v, u)
    f, r = FILES.index(name[0]), int(name[1])
    return -9 + (f + .5) * 2.25, 2 + (r - .5) * 2.25

# One position (CheckBlockGenerator rules): king d7; rook a7 blocked on b7 by a cart from b5; bishop g4 blocked
# on f5 by a cart from h7 (diagonal rail); knight e5 to drag off; hidden double check rook d1 + bishop a4,
# escape square e8.
POS = dict(king='d7', rails=[('a7', 'b5', 'b6', 'b7'), ('g4', 'h7', 'g6', 'f5')], knight='e5',
           double=('d1', 'a4'), escape='e8')

for side, team, us, them, beam in ((-1, BLUE, IVORY, EBONY, ORANGE), (1, ORANGE, EBONY, IVORY, BLUE)):
    ox = 12 * side
    P = lambda name: (ox - sq(name)[0] * side, 4 + sq(name)[1])   # blue: x = -12 + v; orange mirrored
    rect(ox - 10, 4, ox + 10, 6, (90, 95, 120))     # start dais band (0.4 m)
    for f in range(8):
        for r in range(1, 9):
            x, z = P(FILES[f] + str(r))
            rect(x - 1.125, z - 1.125, x + 1.125, z + 1.125, LIGHT if (f + r) % 2 == 1 else DARK)
    rect(ox - 2, 23.6, ox + 2, 24.4, team)          # team side door (4 m)
    kx, kz = P(POS['king'])
    for a, start, mid, block in POS['rails']:
        for s in (start, mid, block):
            x, z = P(s); rect(x - 1.025, z - 1.025, x + 1.025, z + 1.025, RAIL)
        line(*P(start), *P(block), .36, GROOVE)
        diamond(*P(block), 1.0, .13, GOLD)
    if side < 0:            # blue board: every check still on, carts at the start of their rails
        for a, start, mid, block in POS['rails']:
            line(*P(a), kx, kz, .2, beam)
            x, z = P(start); rect(x - .62, z - .62, x + .62, z + .62, CART); disc(x, z, .35, us)
        nx, nz = P(POS['knight']); line(nx, nz, kx, kz, .2, beam)
        disc(nx, nz, .45, beam); disc(nx, nz, .37, them)
    else:                   # orange board: carts in place (grey stub beams), knight dragged off, double check up
        for a, start, mid, block in POS['rails']:
            bx, bz = P(block); ax, az = P(a)
            line(ax, az, bx + (ax - bx) * .3, bz + (az - bz) * .3, .2, (122, 112, 103))
            rect(bx - .62, bz - .62, bx + .62, bz + .62, CART); disc(bx, bz, .35, us)
        for d in POS['double']:
            line(*P(d), kx, kz, .2, RED)
        ex, ez = P(POS['escape']); rect(ex - 1.05, ez - 1.05, ex + 1.05, ez + 1.05, (250, 200, 150))
        for d in POS['double']:
            x, z = P(d); disc(x, z, .45, beam); disc(x, z, .37, them)
    for a, *_ in POS['rails']:
        x, z = P(a); disc(x, z, .45, beam); disc(x, z, .37, them)
    ring(kx, kz, 1.3, .3, RED)                      # check ring round the bed
    rect(kx - .95, kz - 1.15, kx + .95, kz + 1.15, (90, 60, 40))   # canopy bed on the 0.9 m base
    disc(kx, kz, .45, us)

out = sys.argv[-1]
raw = b''.join(b'\x00' + img[y].tobytes() for y in range(H))
def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
open(out, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0))
                      + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))
print(out, W, H)

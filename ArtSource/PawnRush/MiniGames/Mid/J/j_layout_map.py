# Top-down layout map of the J laundry room from the game coordinates (MgKnightPads / KnightRoute),
# used as a reference image for the GPT concept sheet. numpy only (no PIL in Blender's python).
# Run: "<Blender>/5.2/python/bin/python.exe" -I j_layout_map.py <out.png>
import sys, zlib, struct
import numpy as np

S = 22                      # pixels per metre
M = 30                      # margin (px)
W, H = int(44 * S + 2 * M), int(36 * S + 2 * M)
img = np.full((H, W, 3), (244, 236, 220), np.uint8)

def px(x, z):               # room metres -> pixel (back wall at the top, entrance at the bottom)
    return M + (x + 22) * S, M + (36 - z) * S

def rect(x0, z0, x1, z1, c):
    a, b = px(min(x0, x1), max(z0, z1)); d, e = px(max(x0, x1), min(z0, z1))
    img[int(b):int(e), int(a):int(d)] = c

def disc(x, z, r, c):
    cx, cy = px(x, z); rr = r * S
    yy, xx = np.ogrid[:H, :W]
    img[(xx - cx) ** 2 + (yy - cy) ** 2 <= rr * rr] = c

def ring(x, z, r, w, c):
    cx, cy = px(x, z); r0, r1 = (r - w) * S, r * S
    yy, xx = np.ogrid[:H, :W]
    d = (xx - cx) ** 2 + (yy - cy) ** 2
    img[(d <= r1 * r1) & (d >= r0 * r0)] = c

FONT = {'1': ["010", "110", "010", "010", "111"], '2': ["111", "001", "111", "100", "111"],
        '3': ["111", "001", "111", "001", "111"], '4': ["101", "101", "111", "001", "001"],
        '5': ["111", "100", "111", "001", "111"], '6': ["111", "100", "111", "101", "111"],
        '7': ["111", "001", "010", "010", "010"], '8': ["111", "101", "111", "101", "111"]}

def digit(x, z, ch, c, k=5):
    cx, cy = px(x, z); x0, y0 = int(cx - 1.5 * k), int(cy - 2.5 * k)
    for j, row in enumerate(FONT[ch]):
        for i, on in enumerate(row):
            if on == '1': img[y0 + j * k:y0 + (j + 1) * k, x0 + i * k:x0 + (i + 1) * k] = c

WALL, AISLE, WATER, SLATE, FACADE = (120, 104, 90), (150, 150, 155), (170, 222, 222), (70, 80, 95), (226, 210, 180)
TUB, GOLD = (150, 95, 50), (225, 180, 60)
BLUE, ORANGE, CHAR, CREAM = (63, 193, 217), (240, 138, 44), (47, 41, 38), (243, 234, 219)

rect(-23, -0.5, 23, 36.5, WALL)                     # outer walls
rect(-22, 0, 22, 36, (205, 200, 190))               # room floor
rect(-2, 0, 2, 24, AISLE)                           # central aisle
rect(-10, 24, 10, 30, FACADE)                       # closed grand facade block
rect(-22, 24, -14, 30, WALL); rect(14, 24, 22, 30, WALL)
rect(-6, -0.5, 6, 0, (205, 200, 190)); rect(-6, 36, 6, 36.5, (205, 200, 190))   # entrance / exit openings

SQ = {'d1': (-1.125, 3.125), 'b2': (-5.625, 5.375), 'a4': (-7.875, 9.875), 'c5': (-3.375, 12.125),
      'e4': (1.125, 9.875), 'g5': (5.625, 12.125), 'h7': (7.875, 16.625), 'f8': (3.375, 18.875)}
ROUTE = ['d1', 'b2', 'a4', 'c5', 'e4', 'g5', 'h7', 'f8']
for side, team, doll, trim in ((-1, BLUE, CHAR, ORANGE), (1, ORANGE, CREAM, BLUE)):
    ox = 12 * side
    X = lambda v: ox - v * side                     # white (left) is x = -12 + v; black is mirrored
    rect(X(-10), 6.1, X(10), 23.3, WATER)           # soapy basin
    rect(X(-10), 4, X(10), 6.1, SLATE)              # start platform
    rect(X(-3.2), 23.3, X(4.4), 24, SLATE)          # strip to the team door
    rect(ox - 2, 23.6, ox + 2, 24.4, team)          # team door (4 m)
    for i, s in enumerate(ROUTE):
        v, u = SQ[s]; x, z = X(v), 4 + u
        disc(x, z, 1.1, TUB); ring(x, z, .8, .1, GOLD)
        digit(x, z - .45, str(i + 1), GOLD)
        if 0 < i < 7:
            dx = -.55 * side                        # +0.55 in station v, mirrored for black
            disc(x + dx, z + .55, .38, trim)
            disc(x + dx, z + .55, .3, doll)

out = sys.argv[-1]
raw = b''.join(b'\x00' + img[y].tobytes() for y in range(H))
def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
open(out, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0))
                      + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))
print(out, W, H)

"""Fit the giant chess room's stored decor to the 10 m tile (seoungtest R687, 2026-11-17, 승규 "10 m (지금 장면)").

The scene plays the board at tileSize 10 (80 x 80 m, z 0..80, giants at x = +-62, exit platform to z 100), but
WarRoomComplete's stored decor was laid out for 16 m (128 m board). This moves/resizes only those pieces:
  - the giant oak table under the board and its four legs/collars: 82 x 112 m over z -10..102
  - the two commander chairs: centred on the giants (x +-62, z 40)
  - the editor-only board preview: scaled 10/16 on x/z
  - the final exit walkway: from the exit platform's end (z 100) to the exit door (z 170)
Walls, room size, doors, plants, shelves and every gameplay object stay as they are.

Each edit applies only if the object still has its 16 m value, so running twice changes nothing.
Run with any Python 3 (Blender's: "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I).
Never while Unity is in Play.
"""
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
PREFAB = os.environ.get("RL_PREFAB") or os.path.join(HERE, "..", "..", "..", "Assets", "Prefabs", "Obstacles", "WarRoomComplete.prefab")

# (GameObject name, old local position) -> (new position or None, new scale or None)
EDITS = [
    ("Giant oak table", (0, -2.8, 64), (0, -2.8, 46), (82, 4, 112)),
    ("Board preview - replaced by live chess on Play", (0, 0, 0), None, (0.625, 1, 0.625)),
    ("Final exit walkway", (0, -0.6, 158), (0, -0.6, 135), (12, 1.2, 70)),
    ("Final exit walkway brass edge", (0, -1.25, 158), (0, -1.25, 135), (12.15, 0.15, 70.15)),
    ("Final exit walkway apron -1", (-5.6, -2, 158), (-5.6, -2, 135), (0.5, 2, 69)),
    ("Final exit walkway apron 1", (5.6, -2, 158), (5.6, -2, 135), (0.5, 2, 69)),
]
for x in (-1, 1):
    for z_old, z_new in ((-10, -4), (138, 96)):
        EDITS.append(("Table leg", (56 * x, -12, z_old), (35 * x, -12, z_new), None))
        EDITS.append(("Brass leg collar", (56 * x, -19, z_old), (35 * x, -19, z_new), None))
    EDITS += [
        ("Commander chair seat", (86 * x, 1, 64), (62 * x, 1, 40), None),
        ("Commander chair back", (97 * x, 18, 64), (73 * x, 18, 40), None),
        ("Chair crown", (94.8 * x, 28, 64), (70.8 * x, 28, 40), None),
        ("Chair arm rest", (86 * x, 8, 51), (62 * x, 8, 27), None),
        ("Chair arm rest", (86 * x, 8, 77), (62 * x, 8, 53), None),
    ]
    side = "-1" if x < 0 else "1"
    EDITS.append((f"Final exit walkway carved leg {side} 147.2", (4.8 * x, -11, 147.2), (4.8 * x, -11, 101.2), None))
    EDITS.append((f"Final exit walkway leg ferrule {side} 147.2", (4.8 * x, -20.5, 147.2), (4.8 * x, -20.5, 101.2), None))


def fmt(v):
    s = repr(round(float(v), 4))
    return s[:-2] if s.endswith(".0") else s


def main():
    text = open(PREFAB, encoding="utf-8", newline="").read()
    nl = "\r\n" if "\r\n" in text[:2000] else "\n"
    blocks = re.split(r"(?=^--- !u!)", text, flags=re.M)
    by_id = {}
    for i, b in enumerate(blocks):
        m = re.match(r"--- !u!(\d+) &(-?\d+)", b)
        if m: by_id[m.group(2)] = (int(m.group(1)), i)
    names = {}
    for fid, (cls, i) in by_id.items():
        if cls == 1:
            names[fid] = re.search(r"m_Name: (.*)", blocks[i]).group(1).strip()
    transforms = []   # (name, block index)
    for fid, (cls, i) in by_id.items():
        if cls == 4:
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)", blocks[i]).group(1)
            transforms.append((names.get(go, ""), i))

    def pos_of(b):
        m = re.search(r"m_LocalPosition: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}", b)
        return tuple(float(v) for v in m.groups())

    done = skipped = 0
    for name, old, new_pos, new_scl in EDITS:
        hits = [i for n, i in transforms if n == name and all(abs(a - b) < 1e-3 for a, b in zip(pos_of(blocks[i]), old))]
        if not hits:
            already = [i for n, i in transforms if n == name and new_pos and
                       all(abs(a - b) < 1e-3 for a, b in zip(pos_of(blocks[i]), new_pos))]
            print(("already  " if already else "MISSING  ") + name, old)
            skipped += 1
            continue
        for i in hits:
            b = blocks[i]
            if new_pos:
                b = re.sub(r"m_LocalPosition: \{[^}]*\}",
                           "m_LocalPosition: {x: %s, y: %s, z: %s}" % tuple(map(fmt, new_pos)), b, count=1)
            if new_scl:
                b = re.sub(r"m_LocalScale: \{[^}]*\}",
                           "m_LocalScale: {x: %s, y: %s, z: %s}" % tuple(map(fmt, new_scl)), b, count=1)
            blocks[i] = b
            done += 1
            print("moved    " + name, old, "->", new_pos, new_scl or "")
    if "--dry-run" in sys.argv:
        print(f"[dry run] {done} would change, {skipped} skipped"); return
    open(PREFAB, "w", encoding="utf-8", newline="").write("".join(blocks))
    print(f"{done} transforms changed, {skipped} skipped -> {os.path.normpath(PREFAB)}")


main()

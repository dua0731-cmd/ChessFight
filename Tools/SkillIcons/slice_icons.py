# Cuts the sheet skill-icons.html draws (shot by make-icons.ps1 at twice its size, on a clear background) into one PNG a
# skill: Assets/Resources/PawnRushSkillIcons/<Name>.png, Name being the skill's PawnRushSkillIds constant (the skill tree
# loads them by that name). A picture new to the folder gets a texture .meta like the loading screen's chess icons'
# (128 px at most in the game, alpha is transparency), with a GUID made from its path so drawing again keeps it.
# Usage: python -I Tools/SkillIcons/slice_icons.py <sheet.png>
import hashlib
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pngio  # noqa: E402

ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
OUT_REL = 'Assets/Resources/PawnRushSkillIcons'
TEMPLATE = os.path.join(ROOT, 'Assets', 'Resources', 'Loading', 'ChessIcons', 'Knight_White.png.meta')
CELL, COLS = 256, 10


def names():
    src = open(os.path.join(HERE, 'skill-icons.html'), encoding='utf-8').read()
    return re.findall(r"^\s*(?:def|alias)\('(\w+)'", src, re.M)


def guid(rel):
    return hashlib.md5(('chessfight:' + rel).encode()).hexdigest()


def main(sheet):
    w, h, rows = pngio.read(sheet)
    todo = names()
    need = ((len(todo) + COLS - 1) // COLS) * CELL
    if w < COLS * CELL or h < need:
        raise SystemExit(f'the sheet is {w}x{h}, {len(todo)} pictures need {COLS * CELL}x{need}')
    out = os.path.join(ROOT, *OUT_REL.split('/'))
    os.makedirs(out, exist_ok=True)
    folder_meta = out + '.meta'
    if not os.path.exists(folder_meta):
        open(folder_meta, 'w', newline='\n').write(
            f'fileFormatVersion: 2\nguid: {guid(OUT_REL)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n'
            '  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    template = open(TEMPLATE, encoding='utf-8', newline='').read()
    for i, name in enumerate(todo):
        x0, y0 = (i % COLS) * CELL, (i // COLS) * CELL
        cut = [rows[y0 + y][x0 * 4:(x0 + CELL) * 4] for y in range(CELL)]
        path = os.path.join(out, name + '.png')
        pngio.write(path, CELL, CELL, cut)
        meta = path + '.meta'
        if not os.path.exists(meta):
            body = re.sub(r'^guid: \w+', 'guid: ' + guid(OUT_REL + '/' + name + '.png'), template, count=1, flags=re.M)
            open(meta, 'w', encoding='utf-8', newline='').write(body.replace('maxTextureSize: 1024', 'maxTextureSize: 128'))
    drawn = set(todo)
    left = sorted(f[:-4] for f in os.listdir(out) if f.endswith('.png') and f[:-4] not in drawn)
    print(f'{len(todo)} pictures in {OUT_REL}' + (f'; no longer drawn (left as they are): {", ".join(left)}' if left else ''))


if __name__ == '__main__':
    main(sys.argv[1])

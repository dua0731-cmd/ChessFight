# R624 (11-13): the chess piece icons of Sword Fight's entrance screen (LoadingScreenView.SwordPick.cs) - the pick cards,
# our team's pieces and the clash. 사용자: "뭔가 사진으로 그냥 가져다 붙여 놓은 거 같은 느낌이야 퀄리티가 부족해 실제
# 체스에서 쓰는 아이콘으로 해야 해": the rendered figures (MenuArt.Portrait) went, these flat diagram icons came in, in the
# way chess boards and apps draw the pieces - a white piece is an ivory body with an ebony outline and ebony details, a
# black piece an ebony body with a gold outline and gold details (the black team's ebony and gold set, R527).
#
# Drawn here (own shapes, Staunton proportions) on a 100 x 100 box at 4x and brought down to 512 x 512 with alpha, so the
# edges are smooth at every size the screen shows them. Run from the project root:
#     python ArtSource/Loading/ChessIcons/chess_icons.py
# It writes Assets/Resources/Loading/ChessIcons/<Piece>_<White|Black>.png (+ .meta with the import settings; the GUID
# comes from the path, so rewriting keeps it), ArtSource/Loading/ChessIcons/svg/<Piece>_<White|Black>.svg (the same
# shapes as vectors, for the design canvas: sharp at any zoom) and ArtSource/Loading/ChessIcons/preview.png.
# R624 11-13 (사용자 "기물 테두리가 해상도가 낮아 보여"): 512 -> 1024 px (a 4K screen shows the clash's big piece at about
# 900 px), BC7-quality compression instead of none, and the SVGs for the canvas (a browser shrinking a 512 px picture
# three times over left the thin lines jagged).
import hashlib
import math
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Loading", "ChessIcons")
HERE = os.path.dirname(os.path.abspath(__file__))

SIZE = 1024         # the icon's pixels
SS = 4              # drawn this many times bigger, then brought down
BOX = 100.0         # the design box
OUTLINE = 3.0       # outline width (design units)
DETAIL = 2.6        # inner lines

STYLES = {
    # body, outline, details
    "White": ((244, 238, 223, 255), (21, 17, 13, 255), (21, 17, 13, 255)),
    # the body a shade above the dark cards (21, 17, 13) so a black piece reads as solid, not only its gold line
    "Black": ((40, 32, 25, 255), (214, 172, 86, 255), (214, 172, 86, 255)),
}

# ---------------------------------------------------------------- shapes (design units, y down)

def flatten_path(d, steps=28):
    """Absolute M L H V C Q Z only. Returns a list of point lists (one per sub path)."""
    tokens = d.replace(",", " ").split()
    paths, cur, at, cmd, i = [], [], (0.0, 0.0), "M", 0

    def num():
        nonlocal i
        v = float(tokens[i]); i += 1
        return v

    while i < len(tokens):
        t = tokens[i]
        if t[0].isalpha():
            cmd = t[0]
            i += 1
            if cmd == "Z":
                if cur: paths.append(cur)
                cur = []
                continue
            if len(t) > 1:
                tokens.insert(i, t[1:])
        if cmd == "M":
            if cur: paths.append(cur)
            at = (num(), num()); cur = [at]; cmd = "L"
        elif cmd == "L":
            at = (num(), num()); cur.append(at)
        elif cmd == "H":
            at = (num(), at[1]); cur.append(at)
        elif cmd == "V":
            at = (at[0], num()); cur.append(at)
        elif cmd == "C":
            c1 = (num(), num()); c2 = (num(), num()); b = (num(), num())
            for s in range(1, steps + 1):
                u = s / steps; v = 1 - u
                cur.append((v * v * v * at[0] + 3 * v * v * u * c1[0] + 3 * v * u * u * c2[0] + u * u * u * b[0],
                            v * v * v * at[1] + 3 * v * v * u * c1[1] + 3 * v * u * u * c2[1] + u * u * u * b[1]))
            at = b
        elif cmd == "Q":
            c = (num(), num()); b = (num(), num())
            for s in range(1, steps + 1):
                u = s / steps; v = 1 - u
                cur.append((v * v * at[0] + 2 * v * u * c[0] + u * u * b[0], v * v * at[1] + 2 * v * u * c[1] + u * u * b[1]))
            at = b
        else:
            raise ValueError("unknown command " + cmd)
    if cur: paths.append(cur)
    return paths


def sym(left):
    """A symmetric outline from its left half: a path from the top middle (x 50) down the left side to the bottom
    middle. The right half is the mirror, walked back up."""
    pts = flatten_path(left)[0]
    right = [(BOX - x, y) for (x, y) in reversed(pts)]
    return [pts + right[1:-1]]


def circle(cx, cy, r, n=120):
    return [[(cx + r * math.cos(2 * math.pi * k / n), cy + r * math.sin(2 * math.pi * k / n)) for k in range(n)]]


def ellipse(cx, cy, rx, ry, n=120):
    return [[(cx + rx * math.cos(2 * math.pi * k / n), cy + ry * math.sin(2 * math.pi * k / n)) for k in range(n)]]


def rrect(x0, y0, x1, y1, r, n=12):
    pts = []
    for (cx, cy, a0) in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return [pts]


def line(d):
    return flatten_path(d)

# ---------------------------------------------------------------- the pieces
# Parts are drawn in order: ("body", outline) is filled with the body colour and outlined, ("group", [outlines]) is
# several shapes outlined as one (no line where they meet: the queen's crown and its balls), ("line", path) is an inner
# line, ("dot", shape) a filled inner mark. A part drawn later covers the ones before, so a band drawn over the top of a
# body hides the body's end under it, as on a turned piece.

def base():
    # the turned foot: a ring on a wide slab
    return [("body", rrect(23.5, 76.5, 76.5, 84.0, 3.0)), ("body", rrect(17.5, 83.0, 82.5, 92.5, 3.6))]


def pawn():
    return [
        ("body", sym("M 50 50 L 41.5 50 C 41.5 61 34 68 28.5 78 L 50 78")),
        ("body", rrect(34.0, 45.0, 66.0, 52.5, 3.6)),
        ("body", circle(50, 31.5, 13.5)),
    ] + base()


def rook():
    top = [[(23.5, 37), (23.5, 14), (34.5, 14), (34.5, 21.5), (44, 21.5), (44, 14), (56, 14), (56, 21.5),
            (65.5, 21.5), (65.5, 14), (76.5, 14), (76.5, 37)]]
    return [
        ("body", sym("M 50 40 L 33.5 40 L 32 69 C 31.2 73 29.5 75.5 27 78 L 50 78")),
        ("line", line("M 32.6 66 L 67.4 66")),
        ("body", top),
        ("line", line("M 26 30.5 L 74 30.5")),
        ("body", rrect(28.5, 36.0, 71.5, 42.5, 2.6)),
    ] + base()


def bishop():
    mitre = [flatten_path("M 50 17 C 61.5 26 66.5 36 64.5 46 C 63 53 57 57 50 57 C 43 57 37 53 35.5 46 "
                          "C 33.5 36 38.5 26 50 17 Z")[0]]
    return [
        ("body", sym("M 50 60 L 42.5 60 C 42.5 68 34.5 72 29 78 L 50 78")),
        ("body", mitre),
        ("line", line("M 56.5 30.5 L 45.5 44.5")),
        ("body", circle(50, 13.5, 4.8)),
        ("body", rrect(36.0, 55.5, 64.0, 62.0, 3.2)),
    ] + base()


def queen():
    crown = sym("M 50 16 L 43.5 39 L 33.5 20 L 31.5 41 L 18 26.5 C 24 40 28.5 49 30.5 58 L 50 58")
    # each ball sits down over its point (the outline would otherwise leave a gap above a thin point)
    balls = [(50, 15.6, 4.5), (33.6, 19.6, 4.2), (18.8, 26.0, 4.2), (66.4, 19.6, 4.2), (81.2, 26.0, 4.2)]
    parts = [("body", sym("M 50 61 L 40.5 61 C 40.5 69 33 73 28.5 78 L 50 78")),
             ("group", [crown] + [circle(x, y, r) for (x, y, r) in balls])]
    parts += [("line", line("M 29.5 51 C 40 48.5 60 48.5 70.5 51")), ("body", rrect(32.0, 56.0, 68.0, 62.5, 3.2))]
    return parts + base()


def king():
    cross = [[(47, 5), (53, 5), (53, 10.5), (58.5, 10.5), (58.5, 16.5), (53, 16.5), (53, 25), (47, 25), (47, 16.5),
              (41.5, 16.5), (41.5, 10.5), (47, 10.5)]]
    cup = sym("M 50 29 L 33 29 C 28.5 29 27.5 33.5 29.5 37.5 C 32.5 44 35 50.5 36.5 57 L 50 57")
    dome = [flatten_path("M 33.5 30.5 C 36 22 64 22 66.5 30.5 Z")[0]]
    return [
        ("body", cross),
        ("body", sym("M 50 61 L 40.5 61 C 40.5 69 33 73 28.5 78 L 50 78")),
        ("body", cup),
        ("line", line("M 31.2 41 C 41 39.2 59 39.2 68.8 41")),
        ("body", dome),
        ("body", rrect(32.0, 55.5, 68.0, 62.0, 3.2)),
    ] + base()


def knight():
    head = flatten_path(
        "M 29 79 C 29.5 70 33.5 63 40.5 57.5 "
        "C 35 58.5 29.5 60.5 24.5 60.5 C 20.5 60.5 17.3 58.6 16.2 55.6 "
        "L 15.2 52.2 C 14.4 49.2 15.6 46.4 18.5 44.4 "
        "C 22.6 40.2 26.2 33.4 30 27.6 C 32.2 23.4 35 20.6 38.2 18.4 "
        "L 41 8.8 L 46.4 15.8 "
        "C 50.6 14 55.4 13.8 60 15.6 "
        "C 72.5 20.8 80.6 34 80.6 50.5 C 80.6 62 77.6 71 75.2 79 Z")
    return [
        ("body", head),
        ("line", line("M 40.5 57.5 C 43.6 51.6 44.6 45.6 42.4 40")),
        ("line", line("M 60.5 20.5 C 68.6 26 73.6 35.6 74.4 47.6")),
        ("line", line("M 16.8 55.6 C 19.4 56.6 22 56.6 24.6 55.6")),
        ("dot", ellipse(33.2, 30.2, 2.7, 2.1)),
        ("dot", circle(20.6, 48.4, 1.7)),
    ] + base()


PIECES = {"King": king, "Queen": queen, "Rook": rook, "Bishop": bishop, "Knight": knight, "Pawn": pawn}

# ---------------------------------------------------------------- drawing

def px(paths, s):
    return [[(x * s, y * s) for (x, y) in p] for p in paths]


def thick_line(draw, pts, width, colour, closed):
    if len(pts) < 2: return
    seq = pts + [pts[0], pts[1]] if closed else pts
    draw.line(seq, fill=colour, width=int(round(width)), joint="curve")
    r = width / 2.0
    ends = [] if closed else [pts[0], pts[-1]]
    for (x, y) in ends:
        draw.ellipse((x - r, y - r, x + r, y + r), fill=colour)


def render(piece, style, size=SIZE):
    body, outline, detail = STYLES[style]
    big = size * SS
    s = big / BOX
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    for kind, shape in PIECES[piece]():
        paths = px(shape, s) if kind != "group" else []
        if kind == "body":
            for p in paths:
                draw.polygon(p, fill=body)
            for p in paths:
                thick_line(draw, p, OUTLINE * s, outline, True)
        elif kind == "group":
            # all the outlines first at twice the width, then all the bodies over them: only the outside of the
            # union keeps its line (as wide as any other part's, all of it outside the shapes)
            for one in shape:
                for p in px(one, s):
                    thick_line(draw, p, 2 * OUTLINE * s, outline, True)
            for one in shape:
                for p in px(one, s):
                    draw.polygon(p, fill=body)
        elif kind == "line":
            for p in paths:
                thick_line(draw, p, DETAIL * s, detail, False)
        elif kind == "dot":
            for p in paths:
                draw.polygon(p, fill=detail)
    # premultiplied while shrinking, so the transparent rim does not darken the edge
    return img.convert("RGBa").resize((size, size), Image.LANCZOS).convert("RGBA")


META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 1
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 2
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 2
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid_for(rel):
    # the same rule as Tools/Generators/mkmeta.py: one GUID per path, so a rerun keeps it
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def write_meta(path, template):
    meta = path + ".meta"
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    with open(meta, "w", newline="\n") as f:
        f.write(template.format(guid=guid_for(rel)))


def svg_points(p):
    return "M " + " L ".join("%.2f %.2f" % (x, y) for (x, y) in p)


def svg(piece, style):
    body, outline, detail = STYLES[style]
    hexc = lambda c: "#%02x%02x%02x" % c[:3]
    out = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="512" height="512">']
    join = 'stroke-linejoin="round" stroke-linecap="round"'
    for kind, shape in PIECES[piece]():
        if kind == "body":
            for p in shape:
                out.append('<path d="%s Z" fill="%s" stroke="%s" stroke-width="%g" %s/>' % (svg_points(p), hexc(body), hexc(outline), OUTLINE, join))
        elif kind == "group":
            for one in shape:
                for p in one:
                    out.append('<path d="%s Z" fill="none" stroke="%s" stroke-width="%g" %s/>' % (svg_points(p), hexc(outline), 2 * OUTLINE, join))
            for one in shape:
                for p in one:
                    out.append('<path d="%s Z" fill="%s"/>' % (svg_points(p), hexc(body)))
        elif kind == "line":
            for p in shape:
                out.append('<path d="%s" fill="none" stroke="%s" stroke-width="%g" %s/>' % (svg_points(p), hexc(detail), DETAIL, join))
        elif kind == "dot":
            for p in shape:
                out.append('<path d="%s Z" fill="%s"/>' % (svg_points(p), hexc(detail)))
    out.append("</svg>")
    return "\n".join(out) + "\n"


def preview(icons):
    # every icon at 168 px on the pick card's colour, then at 64 px on the card, the marked card and ivory
    cell, small = 196, 76
    bgs = [(21, 17, 13), (42, 32, 22), (239, 230, 210)]
    w = cell * 6
    h = cell * 2 + small * 2 * len(bgs)
    sheet = Image.new("RGBA", (w, h), (10, 8, 6, 255))
    names = list(PIECES)
    for row, style in enumerate(("White", "Black")):
        for col, name in enumerate(names):
            tile = Image.new("RGBA", (cell, cell), (21, 17, 13, 255))
            icon = icons[(name, style)].resize((168, 168), Image.LANCZOS)
            tile.alpha_composite(icon, (14, 14))
            sheet.alpha_composite(tile, (col * cell, row * cell))
    y = cell * 2
    for bg in bgs:
        for style in ("White", "Black"):
            for col, name in enumerate(names):
                tile = Image.new("RGBA", (small, small), bg + (255,))
                tile.alpha_composite(icons[(name, style)].resize((64, 64), Image.LANCZOS), (6, 6))
                sheet.alpha_composite(tile, (col * cell, y))
            y += small
    sheet.save(os.path.join(HERE, "preview.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    write_meta(OUT, FOLDER_META)
    icons = {}
    for name in PIECES:
        for style in STYLES:
            img = render(name, style)
            icons[(name, style)] = img
            path = os.path.join(OUT, "%s_%s.png" % (name, style))
            img.save(path, optimize=True)
            write_meta(path, META)
            os.makedirs(os.path.join(HERE, "svg"), exist_ok=True)
            with open(os.path.join(HERE, "svg", "%s_%s.svg" % (name, style)), "w", newline="\n") as f:
                f.write(svg(name, style))
    preview(icons)
    print("wrote", len(icons), "icons to", os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    sys.exit(main())

"""The hall's people - chess-piece figurines (seoungtest R837, 2026-11-25): the user's two Tripo models of the R836 sheets
(user 11-25: "인형 장식품은 이 에셋을 써"), copied from Downloads to user_assets/:

  royal_characters.glb  (BH_A10) king · queen · partner · the waltz pair (queen and partner joined by the arms)
  chess_pieces.glb      (BH_A11) waiter (black bishop) · seated guest (white pawn, back view) · magician (white rook) ·
                        violinist (white pawn) · bassist (black rook)

Each is one mesh of all five with one PBR atlas (4096: base colour, normal, glTF roughness-metallic). figure(name)
imports the model once, cuts the named figure out between planes across x at the gaps between figures, and returns
it as a new object: its feet at z 0, centred on the origin, facing -y (as in the model), in the model's units (about
0.4 tall) - the caller scales and places it. Its material is the set's, renamed to the bh_kit slot
"BH | figures_royal" / "BH | figures_staff" (GrandBanquetHallDressing draws it with BH_FiguresRoyal / BH_FiguresStaff,
which bh_figures.unity_materials() writes from the atlas).

    import bh_figures as F; ob = F.figure("waiter", collection)
"""
import os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(HERE, "user_assets")
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))

SETS = {"royal": "royal_characters.glb", "staff": "chess_pieces.glb"}
# name -> (set, x from, x to) in the model's units (the gaps found from the vertices' spread along x)
FIGURES = {
    "king": ("royal", -.60, -.278), "queen": ("royal", -.278, -.068), "partner": ("royal", -.068, .14), "pair": ("royal", .14, .60),
    "waiter": ("staff", -.60, -.32), "guest": ("staff", -.32, -.12), "magician": ("staff", -.12, .11),
    "violinist": ("staff", .11, .30), "bassist": ("staff", .30, .60),
}
_source = {}


def _load(set_name):
    ob = _source.get(set_name)
    if ob is not None and ob.name in bpy.data.objects:
        return ob
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(ASSETS, SETS[set_name]))
    new = [o for o in bpy.data.objects if o not in before]
    ob = next(o for o in new if o.type == "MESH")
    ob.data.transform(ob.matrix_world)
    ob.matrix_world = Matrix.Identity(4)
    for o in new:
        if o is not ob:
            bpy.data.objects.remove(o)
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    m = ob.data.materials[0]
    m.name = "BH | figures_" + set_name
    _source[set_name] = ob
    return ob


def figure(name, collection=None, obj_name=None, islands=False):
    """islands=True: keep the model's loose parts whose middle (x) lies in the figure's range instead of cutting at
    the planes - nothing is sliced (B4's queen and partner: a gown's side and the partner's arm reach over the
    planes). B1/B2 were made with the planes and stay so."""
    set_name, x0, x1 = FIGURES[name]
    src = _load(set_name)
    bm = bmesh.new()
    bm.from_mesh(src.data)
    if islands:
        bm.verts.ensure_lookup_table()
        seen, drop = set(), []
        for v in bm.verts:
            if v.index in seen:
                continue
            stack, comp = [v], []
            seen.add(v.index)
            while stack:
                a = stack.pop()
                comp.append(a)
                for e in a.link_edges:
                    b = e.other_vert(a)
                    if b.index not in seen:
                        seen.add(b.index)
                        stack.append(b)
            xs = [c.co.x for c in comp]
            if not (x0 <= (min(xs) + max(xs)) / 2 < x1):
                drop += comp
        bmesh.ops.delete(bm, geom=drop, context="VERTS")
    for at, normal, clear_inner in (() if islands else ((x0, (1, 0, 0), True), (x1, (1, 0, 0), False))):
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=(at, 0, 0), plane_no=normal,
                               clear_inner=clear_inner, clear_outer=not clear_inner)
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; zs = [v.co.z for v in bm.verts]
    shift = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
    bmesh.ops.translate(bm, verts=bm.verts, vec=-shift)
    me = bpy.data.meshes.new("fig_" + name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(src.data.materials[0])
    ob = bpy.data.objects.new(obj_name or ("fig_" + name), me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    return ob


def size(ob):
    """(width x, depth y, height z) of a figure object (its mesh, unscaled)."""
    vs = [v.co for v in ob.data.vertices]
    return tuple(max(v[i] for v in vs) - min(v[i] for v in vs) for i in range(3))


def unity_materials():
    """BH_FiguresRoyal / BH_FiguresStaff: the sets' atlases into Assets/Art/BanquetHall/Figures (base colour, normal,
    and URP's metallic map made from glTF's roughness-metallic: R metallic = its blue, A smoothness = 1 - its green),
    URP Lit materials in Resources/BanquetHall/Materials, tiling 1. Never while Unity is in Play."""
    import numpy as np
    sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels", "textures"))
    sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels"))
    import rcy_unity_materials as ru
    import rcy_textures as rt
    ru.ROOT = ROOT
    tex_rel, mat_rel = "Assets/Art/BanquetHall/Figures", "Assets/Resources/BanquetHall/Materials"
    for rel in ("Assets/Art/BanquetHall", tex_rel):
        os.makedirs(os.path.join(ROOT, rel), exist_ok=True)
        ru.folder_meta(rel)
    for set_name in SETS:
        src = _load(set_name)
        imgs = {}
        for n in src.data.materials[0].node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image:
                nm = n.image.name.lower()
                kind = "basecolor" if "basecolor" in nm else "normal" if "normal" in nm else "rm" if "_rm" in nm else None
                if kind:
                    imgs[kind] = n.image
        unity = "BH_Figures" + set_name.capitalize()
        out = {}
        for kind, img in imgs.items():
            w, h = img.size
            px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]   # top row first
            if kind == "rm":
                metal, rough = px[..., 2], px[..., 1]
                px = np.stack([metal, metal, metal, 1.0 - rough], -1)
            elif kind == "basecolor":
                px[..., 3] = 1.0
            fname = "%s_%s.png" % (unity, {"basecolor": "Albedo", "normal": "Normal", "rm": "MetallicSmoothness"}[kind])
            rel = tex_rel + "/" + fname
            rt.save_png(px.astype(np.float32), os.path.join(ROOT, rel))
            meta = os.path.join(ROOT, rel + ".meta")
            if os.path.exists(meta):
                g = open(meta, encoding="utf-8").read().split("\n")[1].split()[1]
            else:
                g = ru.guid(rel)
                text = ru.texture_meta(g, kind == "normal", 2048)
                if kind == "rm":
                    text = text.replace("    sRGBTexture: 1\n", "    sRGBTexture: 0\n", 1)
                ru.write(rel + ".meta", text)
            out[kind] = g
        mat = ru.material(unity, out["basecolor"], out["normal"], 1.0, 1.0)
        old = "    - _MetallicGlossMap:\n        m_Texture: {fileID: 0}\n"
        assert mat.count(old) == 1
        mat = mat.replace(old, "    - _MetallicGlossMap:\n        m_Texture: {fileID: 2800000, guid: %s, type: 3}\n" % out["rm"])
        for a, b in (("  - _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A\n", "  - _METALLICSPECGLOSSMAP\n"),
                     ("    - _SmoothnessTextureChannel: 1\n", "    - _SmoothnessTextureChannel: 0\n")):
            assert mat.count(a) == 1, a
            mat = mat.replace(a, b)
        rel = "%s/%s.mat" % (mat_rel, unity)
        ru.write(rel, mat)
        if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
            ru.write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                                    "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % ru.guid(rel))
        print("BH_FIG material", unity, sorted(out))


if __name__ == "__main__":
    # every figure cut out, side by side, for a look (stills/figures/figures.png); "unity": the materials too
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sys.path.insert(0, HERE)
    import bh_stills as st
    obs = []
    x = 0.0
    for name in FIGURES:
        ob = figure(name)
        w, d, h = size(ob)
        ob.location = (x + w / 2, 0, 0)
        x += w + .05
        obs.append(ob)
        print("BH_FIG", name, "w %.3f d %.3f h %.3f" % (w, d, h), "tris", sum(len(p.vertices) - 2 for p in ob.data.polygons))
    if "unity" in args:
        unity_materials()
    sc = bpy.context.scene
    cam = st.setup_render(sc, 1800)
    sc.render.resolution_y = 700
    sc.cycles.samples = 48
    bpy.context.view_layer.update()
    st.frame(cam, obs, 0.0, .12, 85, 1.02)
    os.makedirs(os.path.join(HERE, "stills", "figures"), exist_ok=True)
    st.render_only(sc, obs, os.path.join(HERE, "stills", "figures", "figures.png"))

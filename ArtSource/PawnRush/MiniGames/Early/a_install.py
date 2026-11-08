# A ceremonial court (R506): copies the exported FBX + textures into Assets and writes their .meta and URP Lit materials.
# Same as d_install.py (whose helpers it borrows by import, so the two write identical metas and materials).
# Plain Python (no Blender needed). On this PC `python` is a Store stub, so run it with Blender's interpreter:
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I a_install.py
#
# Models  -> Assets/Resources/PawnRushMiniGames/Early/A/<name>.fbx   (MiniGameModels.ParadeCourt loads them by name)
# Mats    -> Assets/Resources/PawnRushMiniGames/Materials/<name>.mat (the FBX's imported material is swapped by name)
# Texture -> Assets/Art/PawnRushMiniGames/Early/Textures/<name>.png
import os, sys, types

HERE = os.path.dirname(os.path.abspath(__file__))

# d_install.py runs its main() on import; load its helpers without that last line.
src = open(os.path.join(HERE, "d_install.py"), encoding="utf-8").read().replace("\nmain()\n", "\n")
d = types.ModuleType("d_install"); d.__file__ = os.path.join(HERE, "d_install.py")
exec(compile(src, d.__file__, "exec"), d.__dict__)

MODELS = "Assets/Resources/PawnRushMiniGames/Early/A"

# material name -> (texture stem, smoothness = 1 - Blender roughness[, sRGB tint])
MATERIALS = {
    # a_props_v1.py: the three Tripo atlases (resolution kept)
    "PR_A_TripoFacade": ("PR_A_TripoFacade", .15), "PR_A_TripoStructure": ("PR_A_TripoStructure", .15),
    "PR_A_TripoProps": ("PR_A_TripoProps", .2),
    # the rebuilt tower body and gate post base: R502 tileable stone brick tinted to the Tripo stone
    "PR_A_StoneBrick": ("PR_Early_StoneBrick", .1, (.91, .90, .87)),
    # R506 swatches (Materials/r506_material_swatches.py, 2 m a tile): the new bench, the board's squares, the floor
    "PR_A_IvoryLimestone": ("PR_A_IvoryLimestone", .35), "PR_A_Slate": ("PR_A_Slate", .3),
    "PR_A_Basalt": ("PR_A_Basalt", .45), "PR_A_CourtPaving": ("PR_A_CourtPaving", .2),
}
# flat-colour materials a_props_v1.py made (sRGB colour, smoothness, metallic)
FLAT = {"PR_A_TowerDark": ((.12, .10, .09), .05, 0), "PR_A_PaintWhite": ((.95, .92, .86), .75, 0),
        "PR_A_PaintBlack": ((.14, .12, .11), .75, 0)}
# a_banner_tex_v1.py: crown and team banners for the code-built swaying cloth, alpha-clipped (the V hem's corners)
BANNERS = ["PR_A_CrownBanner", "PR_A_CrownBannerLong", "PR_A_TeamBannerWhite", "PR_A_TeamBannerBlack"]
FBX = ["A_Facade", "A_TeamArch", "A_TeamDoor", "A_Wall", "A_Tower", "A_GardenWall", "A_GatePost", "A_Cypress",
       "A_Flagpole", "A_Planter", "A_Bench", "A_Carpet", "A_PotWhite", "A_PotBlack", "A_PaintRack"]


def main():
    for name in FBX:
        d.copy(os.path.join(HERE, "Export", name + ".fbx"), MODELS + "/" + name + ".fbx", d.FBX_META)
    for name, (stem, smooth, *tint) in MATERIALS.items():
        b = d.copy(os.path.join(HERE, "Materials", "Textures", stem + "_BaseColor.png"), d.TEXS + "/" + stem + "_BaseColor.png", d.BASE_META)
        n = d.copy(os.path.join(HERE, "Materials", "Textures", stem + "_Normal.png"), d.TEXS + "/" + stem + "_Normal.png", d.NORMAL_META)
        d.material(name, b, n, smooth, tint[0] if tint else None)
    for name, (rgb, smooth, metal) in FLAT.items():
        d.material(name, None, None, smooth, rgb, metal)
    for name in BANNERS:   # as G's royal banner (g_install_unity.py): alpha test on, cutout queue
        b = d.copy(os.path.join(HERE, "Materials", "Textures", name + "_BaseColor.png"), d.TEXS + "/" + name + "_BaseColor.png", d.BASE_META)
        d.material(name, b, None, .1)
        rel = d.MATS + "/" + name + ".mat"
        t = d.read(rel)
        if "_ALPHATEST_ON" not in t:
            t = t.replace("  m_ValidKeywords: []\n", "  m_ValidKeywords:\n  - _ALPHATEST_ON\n")
            t = t.replace("- _AlphaClip: 0", "- _AlphaClip: 1").replace("m_CustomRenderQueue: -1", "m_CustomRenderQueue: 2450")
            t = t.replace("RenderType: Opaque", "RenderType: TransparentCutout")
            assert "_ALPHATEST_ON" in t and "- _AlphaClip: 1" in t, name
            d.write(rel, t)


main()

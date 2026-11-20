"""Look at exported FBX models together, the way Unity places them (seoungtest R725, 2026-11-19): for checking seams between
models from different scripts (a star platform meeting the entry walk, a turning orbit over its still pillar).

    blender -b --factory-startup --python ro_fbx_stills.py -- <out folder> <shot> <model.fbx>[,<frame>] ...

Each model is Models/<zone>/<name>.fbx under Assets/Resources/RoyalObservatory, imported back (the FBX axes undone by
Blender's importer) and, when a graybox frame is given (a model hung on a Device_* or a track), placed in that frame
at rest. Shots: "threshold" (star 1 meeting the entry walk and its threshold), "gate_column" (the entry gate's west
column), "orbit_hub" (orbit 1's middle: the sun, the arms, the stem, the pillar's top). The library preview of
ro_r0_star_model.stills draws them.
"""
import os, sys
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k   # noqa: E402

MODELS = os.path.join(k.ROOT, "Assets", "Resources", "RoyalObservatory", "Models")

SHOTS = {
    "threshold": ((-2, 14.2, 0), {"seam_low": (Vector((3.5, -4.0, 1.4)), Vector((0, .2, 0)), 28),
                                  "seam_top": (Vector((0, -1.0, 4.5)), Vector((0, .3, 0)), 30)}),
    "gate_column": ((-6.75, 14.5, 3), {"column": (Vector((2.6, 3.4, .4)), Vector((0, .3, 0)), 32),
                                       "column_side": (Vector((-1.5, 3.0, -.5)), Vector((0, .4, 0)), 32)}),
    "orbit_hub": (None, {"hub": (Vector((2.2, -1.6, -.6)), Vector((0, 0, -1.0)), 34),
                         "hub_low": (Vector((1.8, -1.4, -2.4)), Vector((0, 0, -1.1)), 34)}),
}


def main(argv):
    out, shot, specs = argv[0], argv[1], argv[2:]
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for spec in specs:
        rel, _, fr = spec.partition(",")
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, rel))
        new = [o for o in bpy.data.objects if o not in before]
        if fr:
            m = k.frame(fr)
            for o in new:
                if o.parent is None:
                    o.matrix_world = m @ o.matrix_world
    # imported materials come in as "RO | gold" etc. (the slots' names): the library preview finds them by name
    import ro_r0_star_model as r0
    centre, shots = SHOTS[shot]
    centre = Vector(centre) if centre else k.frame("Device_OrbitRing 1").translation
    r0.stills([], out, shots={"%s_%s" % (shot, n): v for n, v in shots.items()}, centre=centre,
              floor_at=(centre.x, centre.y, -18), fill_offset=(4, -5, 6))


if __name__ == "__main__":
    main(sys.argv[sys.argv.index("--") + 1:])

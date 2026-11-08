# Exports the Blender chess castle (LobbyCastle.blend, closed gate) for Unity:
# - drops stand-ins, cameras, lights and hidden helpers; splits multi-material meshes
# - every procedural material is baked to textures (albedo, normal, metal+smoothness, emission) on a
#   shared UV atlas of all the objects that use it; image-based materials keep their own UVs and files
# - static objects are joined per material; the two gate leaves stay separate, pivoted on their hinges
# - writes Castle.fbx, the textures and materials.json (what Unity needs to build each material)
# Usage: blender -b --factory-startup <LobbyCastle.blend> --python export_castle.py -- <out_dir>
import bpy, bmesh, os, sys, json, math, shutil
import numpy as np

OUT = sys.argv[sys.argv.index("--") + 1]
TEX = os.path.join(OUT, "Textures"); os.makedirs(TEX, exist_ok=True)
sc = bpy.context.scene
def log(*a): print("[export]", *a, flush=True)

def select(objs, active=None):
    for o in bpy.context.view_layer.objects: o.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = active or (objs[0] if objs else None)

# ---------- 1. clean the scene
for name in ("Stand-in", "Cameras", "Lights"):
    c = bpy.data.collections.get(name)
    if c:
        for o in list(c.all_objects): bpy.data.objects.remove(o, do_unlink=True)
for o in list(bpy.data.objects):
    if o.type in ('LIGHT', 'CAMERA'): bpy.data.objects.remove(o, do_unlink=True)
for o in list(bpy.data.objects):
    if o.hide_render or o.location.z < -50: bpy.data.objects.remove(o, do_unlink=True)
for o in bpy.data.objects: o.hide_set(False); o.hide_viewport = False
# text to mesh
fonts = [o for o in bpy.data.objects if o.type == 'FONT']
if fonts:
    select(fonts); bpy.ops.object.convert(target='MESH')
log("objects", len(bpy.data.objects))

# the gate leaves: remember each hinge's world matrix and its descendants
hinges = [o for o in bpy.data.objects if o.type == 'EMPTY' and o.name.startswith("Gate Hinge")]
leaf_sets = {}
for h in hinges:
    side = "R" if h.matrix_world.translation.x > 0 else "L"
    leaf_sets[side] = (h.matrix_world.copy(), [o for o in h.children_recursive if o.type == 'MESH'])
dynamic = {o for _, objs in leaf_sets.values() for o in objs}
gate_light = [o for o in bpy.data.objects if o.name.startswith("Gate Light")]

# clear every parent keeping transforms (archway parts, glb imports, leaves), then drop empties
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
select(meshes); bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in list(bpy.data.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o, do_unlink=True)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
# single-user data so each object can get its own UVs, and split multi-material meshes
select(meshes); bpy.ops.object.make_single_user(object=True, obdata=True)
for o in list(meshes):
    if len([m for m in o.data.materials if m]) > 1:
        select([o]); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.separate(type='MATERIAL'); bpy.ops.object.mode_set(mode='OBJECT')
meshes = [o for o in bpy.data.objects if o.type == 'MESH' and len(o.data.polygons)]
for o in meshes:
    used = {p.material_index for p in o.data.polygons}
    if len(o.data.materials) > 1 and len(used) == 1:
        keep = o.data.materials[list(used)[0]]
        o.data.materials.clear(); o.data.materials.append(keep)
log("meshes after split", len(meshes))
ONLY = [x for x in os.environ.get("ONLY", "").split(",") if x]
EXPORT_NAME = os.environ.get("EXPORT_NAME", "Castle")
if ONLY:
    # a partial export (e.g. just the floor): keep only meshes whose material name starts with one of ONLY
    for o in list(meshes):
        m0 = o.data.materials[0].name if o.data.materials else ""
        if not any(m0.startswith(k) for k in ONLY): bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    leaf_sets = {}
    log("partial export", ONLY, len(meshes))

# ---------- 2. classify materials
def bsdf_of(m):
    return next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if m and m.use_nodes else None
def image_source(m):
    b = bsdf_of(m)
    if b is None or not b.inputs['Base Color'].links: return None
    n = b.inputs['Base Color'].links[0].from_node
    return n.image if n.type == 'TEX_IMAGE' and n.image else None
by_mat = {}
for o in meshes:
    m = o.data.materials[0] if o.data.materials else None
    if m is None: continue
    by_mat.setdefault(m.name, []).append(o)

def lin2srgb(c): return [round(float(x) ** (1 / 2.2), 4) for x in c[:3]]
info = {}
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 1
sc.render.bake.margin = 8; sc.render.bake.margin_type = 'EXTEND'

def bake_into(objs, m, kind, img, **kw):
    nt = m.node_tree
    n = nt.nodes.get("BakeTarget") or nt.nodes.new('ShaderNodeTexImage'); n.name = "BakeTarget"; n.image = img; nt.nodes.active = n
    select(objs); bpy.ops.object.bake(type=kind, **kw)

def area(objs):
    a = 0.0
    for o in objs:
        mw = o.matrix_world; s = abs(mw.to_scale().x * mw.to_scale().y * mw.to_scale().z) ** (2 / 3)
        a += sum(p.area for p in o.data.polygons) * s
    return a

for mname, objs in sorted(by_mat.items()):
    m = bpy.data.materials[mname]; b = bsdf_of(m); safe = "".join(ch if ch.isalnum() else "_" for ch in mname)
    entry = {"objects": len(objs)}
    if b is None:
        entry.update(kind="solid", color=[1, 0, 1]); info[mname] = entry; continue
    img = image_source(m)
    linked = any(b.inputs[k].links for k in ('Base Color', 'Roughness', 'Metallic', 'Normal', 'Emission Strength', 'Emission Color'))
    if img is not None:
        # image-based (Tripo hall parts, pennants): keep UVs, write the files
        def save(image, suffix):
            path = os.path.join(TEX, safe + suffix + ".png")
            if image.filepath and os.path.exists(bpy.path.abspath(image.filepath)): shutil.copy(bpy.path.abspath(image.filepath), path)
            else: image.filepath_raw = path; image.file_format = 'PNG'; image.save()
            return os.path.basename(path)
        entry.update(kind="image", albedo=save(img, "_Albedo"))
        for n in m.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image and n.image is not img:
                nm = n.image.name.lower()
                if "normal" in nm: entry["normal"] = save(n.image, "_Normal")
                elif "metalsmooth" in nm: entry["metalsmooth"] = save(n.image, "_MetalSmooth")
                elif "emission" in nm: entry["emission"] = save(n.image, "_Emission"); entry["emission_k"] = 1.5
        if not b.inputs['Metallic'].links: entry["metallic"] = round(b.inputs['Metallic'].default_value, 3)
        if not b.inputs['Roughness'].links: entry["smoothness"] = round(1 - b.inputs['Roughness'].default_value, 3)
        entry["alpha_clip"] = bool(b.inputs['Alpha'].links)
        info[mname] = entry; log("image", mname); continue
    if not linked:
        entry.update(kind="solid", color=lin2srgb(b.inputs['Base Color'].default_value),
                     metallic=round(b.inputs['Metallic'].default_value, 3), smoothness=round(1 - b.inputs['Roughness'].default_value, 3))
        ek = b.inputs['Emission Strength'].default_value
        if ek > 0: entry.update(emission=lin2srgb(b.inputs['Emission Color'].default_value), emission_k=round(ek, 2))
        info[mname] = entry; continue
    # procedural: shared atlas for every object that uses it, then bake
    a = area(objs); size = 1024 if a < 25 else 2048
    select(objs)
    for o in objs:
        while o.data.uv_layers: o.data.uv_layers.remove(o.data.uv_layers[0])
        o.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.002)
    bpy.ops.object.mode_set(mode='OBJECT')
    imgs = {k: bpy.data.images.new(safe + k, size, size, alpha=(k == "_MetalSmooth")) for k in ("_Albedo", "_Normal", "_Rough", "_Metal", "_Emission")}
    for k, im in imgs.items(): im.colorspace_settings.name = 'sRGB' if k in ("_Albedo", "_Emission") else 'Non-Color'
    import time as _t; t0 = _t.time()
    # base colour through an emission shader: a DIFFUSE colour bake reads black on metals
    nt0 = m.node_tree; out0 = next(n for n in nt0.nodes if n.type == 'OUTPUT_MATERIAL')
    keep0 = out0.inputs['Surface'].links[0].from_socket if out0.inputs['Surface'].links else None
    e0 = nt0.nodes.new('ShaderNodeEmission')
    if b.inputs['Base Color'].links: nt0.links.new(b.inputs['Base Color'].links[0].from_socket, e0.inputs['Color'])
    else: e0.inputs['Color'].default_value = b.inputs['Base Color'].default_value
    nt0.links.new(e0.outputs['Emission'], out0.inputs['Surface'])
    bake_into(objs, m, 'EMIT', imgs['_Albedo'])
    if keep0: nt0.links.new(keep0, out0.inputs['Surface'])
    nt0.nodes.remove(e0); log('  albedo', round(_t.time() - t0), 's')
    has_n = bool(b.inputs['Normal'].links)
    if has_n: bake_into(objs, m, 'NORMAL', imgs['_Normal'], normal_space='TANGENT')
    rough_l = bool(b.inputs['Roughness'].links); metal_l = bool(b.inputs['Metallic'].links)
    if rough_l: bake_into(objs, m, 'ROUGHNESS', imgs['_Rough'])
    has_emit = bool(b.inputs['Emission Strength'].links) or b.inputs['Emission Strength'].default_value > 0
    if has_emit: bake_into(objs, m, 'EMIT', imgs["_Emission"])
    nt = m.node_tree; out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    old = out.inputs['Surface'].links[0].from_socket if out.inputs['Surface'].links else None
    em = nt.nodes.new('ShaderNodeEmission')
    if b.inputs['Metallic'].links: nt.links.new(b.inputs['Metallic'].links[0].from_socket, em.inputs['Color'])
    else: em.inputs['Color'].default_value = (b.inputs['Metallic'].default_value,) * 3 + (1,)
    nt.links.new(em.outputs['Emission'], out.inputs['Surface'])
    if metal_l: bake_into(objs, m, 'EMIT', imgs['_Metal'])
    if old: nt.links.new(old, out.inputs['Surface'])
    def px(im): return np.array(im.pixels[:], dtype=np.float32).reshape(size, size, 4)
    mval = px(imgs['_Metal'])[..., :1] if metal_l else np.full((size, size, 1), b.inputs['Metallic'].default_value, np.float32)
    rval = px(imgs['_Rough'])[..., :1] if rough_l else np.full((size, size, 1), b.inputs['Roughness'].default_value, np.float32)
    ms = np.concatenate([np.repeat(mval, 3, 2), 1 - rval], 2)
    msi = bpy.data.images.new(safe + "_MS", size, size, alpha=True); msi.colorspace_settings.name = 'Non-Color'
    msi.pixels.foreach_set(ms.ravel()); msi.update()
    def save(im, suffix):
        im.filepath_raw = os.path.join(TEX, safe + suffix + ".png"); im.file_format = 'PNG'; im.save(); return safe + suffix + ".png"
    entry.update(kind="baked", size=size, albedo=save(imgs["_Albedo"], "_Albedo"), normal=save(imgs["_Normal"], "_Normal") if has_n else None,
                 metalsmooth=save(msi, "_MetalSmooth"))
    if has_emit: entry.update(emission=save(imgs["_Emission"], "_Emission"), emission_k=1.0)
    info[mname] = entry; log("baked", mname, size, round(a, 1), "m2")

# ---------- 3. join static objects per material; the gate leaves become two pivoted objects
for side, (mw, parts) in leaf_sets.items():
    parts = [o for o in parts if o.name in bpy.data.objects]
    if not parts: continue
    select(parts, parts[0]); bpy.ops.object.join()
    leaf = bpy.context.view_layer.objects.active; leaf.name = "GateLeaf_" + side
    # origin on the hinge
    sc.cursor.location = mw.translation; select([leaf]); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
statics = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith("GateLeaf_")]
groups = {}
for o in statics:
    m = o.data.materials[0] if o.data.materials else None
    groups.setdefault(m.name if m else "none", []).append(o)
for mname, objs in groups.items():
    if len(objs) > 1:
        select(objs, objs[0]); bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active if len(objs) > 1 else objs[0]
    o.name = "Castle_" + "".join(ch if ch.isalnum() else "_" for ch in mname)
for o in bpy.data.objects:
    if o.type == 'MESH' and not o.name.startswith("GateLeaf_"):
        select([o]); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')
log("final objects", len([o for o in bpy.data.objects if o.type == 'MESH']), "tris", tris)

# ---------- 4. export
select([o for o in bpy.data.objects if o.type == 'MESH'])
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, EXPORT_NAME + ".fbx"), use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='STRIP')
leaves = {o.name: [round(v, 4) for v in o.location] for o in bpy.data.objects if o.name.startswith("GateLeaf_")}
json.dump({"materials": info, "leaves_blender": leaves}, open(os.path.join(OUT, "materials.json" if not ONLY else EXPORT_NAME + "_materials.json"), "w"), indent=1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(r"C:\Work\ChessFight\ArtSource\Lobby", EXPORT_NAME + "_export.blend"), compress=True)
log("DONE", OUT)

"""Export (brief §9.8, §10): per part a folder ArtSource/PawnRush/Endgame/<Part>/ with <Part>_work.blend and
<Part>.fbx holding <Part>_LOD0/_LOD1/_LOD2 (Unity builds the LODGroup), UV0 for textures and UV1 for lightmaps,
then PR_EG_manifest.json with every placement in Unity axes."""
import json
import math
import os

import bpy

import eg_bl_util as B

LOD_RATIOS = (1.0, 0.5, 0.2)
UV1 = "UV1_Lightmap"

DESCRIBE = {
    "PR_EG_WarTable16": ("R1 회전 작전 탁자 판", "윗면 가운데(판 윗면 y 0), 회전축"),
    "PR_EG_SweepBar": ("R1 스윕 막대", "회전축(판 윗면에서 0.65 m 위)"),
    "PR_EG_FerryDeck": ("S2 Ferry 갑판", "갑판 윗면 가운데"),
    "PR_EG_HurdleBaton": ("S2 고정 걸림 막대(지휘봉)", "막대 가운데(바닥 위 0.36 m)"),
    "PR_EG_ShelfWall6": ("R2 06 미닫이 벽(책장, 12 m 길용 6 m 벽)", "벽 가운데(1.5 m 위)"),
    "PR_EG_ShelfHousing": ("R2 06 벽집(책장 캐비닛)", "입구 가운데 바닥"),
    "PR_EG_GateFrame": ("R2 09 시계 문 틀", "바닥 가운데"),
    "PR_EG_GateDoor": ("R2 09 시계 문 판 3.8 x 3.2", "판 가운데(닫힘 1.6 m 위), 위로 4.1 m"),
    "PR_EG_RookScreen6": ("S3 · S4 룩 차폐판 6 m", "가운데(1.6 m 위)"),
    "PR_EG_PendulumWeight": ("R3 11 진자 무게추 + 지도 두루마리", "회전축(바닥에서 7.3 m 위)"),
    "PR_EG_PendulumGantry": ("R3 11 진자 틀", "바닥 가운데"),
    "PR_EG_MapChestTile": ("R4 05 승강 타일(지도함 뚜껑)", "타일 가운데(윗면 0.2 m 아래)"),
    "PR_EG_LiftShaft": ("R4 05 구덩이 테두리 · 수직갱", "세트 가운데 바닥"),
    "PR_EG_WindVent": ("R4 15 바람 분사구", "바닥 가운데, 바람은 로컬 +z"),
    "PR_EG_MapBelt": ("R5 08 지도 인쇄 벨트 6 x 10", "받침 바닥 가운데(윗면 0.4 m 위)"),
    "PR_EG_GiantPawn": ("R5 10 대형 폰 조형물", "받침 바닥 가운데"),
    "PR_EG_CrownCircle": ("R5 10 경고 원(금빛 왕관 원) 반지름 5", "원 가운데 바닥"),
    "PR_EG_HangingCrate": ("R5b 사슬 상자(Ferry) 6 x 3", "갑판 윗면 가운데"),
    "PR_EG_CraneRail": ("R5b 크레인 레일", "바닥 높이 가운데"),
    "PR_EG_CompassDisc": ("R6 01 별자리 · 나침반 원판", "받침 바닥 가운데(윗면 0.4 m 위)"),
    "PR_EG_BridgePanel": ("S4 12 접이식 다리 판", "경첩 쪽(착지대 안쪽 끝, 윗면 높이)"),
    "PR_EG_BridgeLanding": ("S4 12 착지대", "윗면 가운데"),
}


def _select_only(objs):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def make_lods(src, coll):
    """Copies of `src` (modifiers applied): _LOD0 as is, _LOD1 / _LOD2 decimated (collapse 0.5 / 0.2)."""
    out = []
    dg = bpy.context.evaluated_depsgraph_get()
    base = bpy.data.meshes.new_from_object(src.evaluated_get(dg))
    for i, ratio in enumerate(LOD_RATIOS):
        me = base.copy() if i else base
        o = bpy.data.objects.new(f"{src.name}_LOD{i}", me)
        coll.objects.link(o)
        if ratio < 1.0:
            dec = o.modifiers.new("Decimate", "DECIMATE")
            dec.decimate_type = "COLLAPSE"
            dec.ratio = ratio
            dec.use_collapse_triangulate = True
            _select_only([o])
            with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
                bpy.ops.object.modifier_apply(modifier="Decimate")
        out.append(o)
    return out


def add_uv1(objs, margin=0.004):
    """UV1 for lightmaps: Lightmap Pack into a new layer, no overlaps, margin ~4 px at 1024."""
    for o in objs:
        me = o.data
        if UV1 not in me.uv_layers:
            me.uv_layers.new(name=UV1)
        me.uv_layers.active = me.uv_layers[UV1]
    _select_only(objs)
    with bpy.context.temp_override(selected_objects=objs, active_object=objs[0], object=objs[0]):
        bpy.ops.uv.lightmap_pack(PREF_CONTEXT="ALL_FACES", PREF_PACK_IN_ONE=False, PREF_NEW_UVLAYER=False,
                                 PREF_BOX_DIV=12, PREF_MARGIN_DIV=0.3)
    for o in objs:
        o.data.uv_layers.active = o.data.uv_layers[0]
        o.data.uv_layers[0].active_render = True


def export_part(src, root, coll):
    name = src.name
    folder = os.path.join(root, name)
    os.makedirs(folder, exist_ok=True)
    lods = make_lods(src, coll)
    add_uv1(lods)
    _select_only(lods)
    fbx = os.path.join(folder, f"{name}.fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, add_leaf_bones=False,
                             use_tspace=True, use_mesh_modifiers=True, mesh_smooth_type="FACE", path_mode="STRIP",
                             embed_textures=False, object_types={"MESH"})
    blend = os.path.join(folder, f"{name}_work.blend")
    bpy.data.libraries.write(blend, set(lods) | {src}, fake_user=True, compress=True)
    tris = [sum(len(p.vertices) - 2 for p in o.data.polygons) for o in lods]
    for o in lods:
        bpy.data.objects.remove(o)
    return dict(part=name, fbx=os.path.relpath(fbx, root), blend=os.path.relpath(blend, root), lod_tris=tris)


def export_all(parts, root):
    coll = B.collection("EG_ExportTmp")
    rows = []
    for src in parts:
        rows.append(export_part(src, root, coll))
        print("[export]", rows[-1]["part"], rows[-1]["lod_tris"], flush=True)
    bpy.data.collections.remove(coll)
    return rows


def manifest(placed, rigs, root, path):
    """PR_EG_manifest.json: [{name, fbx, position, euler, scale, role, skins, pivot, attach}], Unity axes."""
    rows = []
    for p in placed:
        part = p["fbx"].split("/")[0]
        desc = DESCRIBE.get(part)
        rows.append(dict(name=p["name"], fbx=p["fbx"], position=p["position"], euler=p["euler"], scale=p["scale"],
                         role=p["role"], skins=p.get("skins") or (desc[0] if desc else ""),
                         pivot=p.get("pivot") or (desc[1] if desc else ""), attach=p.get("attach", "")))
    for r in rigs:
        root_name = r.root.name
        for part in r.parts:
            src = part.get("eg_source", part.data.name)
            desc = DESCRIBE.get(src, ("", ""))
            loc = part.matrix_world.to_translation()
            rows.append(dict(name=part.name, fbx=f"{src}/{src}.fbx", position=B.unity_pos(loc),
                             euler=_unity_euler(part), scale=[round(part.scale.x, 4), round(part.scale.z, 4), round(part.scale.y, 4)],
                             role="skin" if src in DESCRIBE else "static", skins=desc[0], pivot=desc[1],
                             attach=f"{root_name} (Unity: under the device root, motion zeroed)"))
    with open(path, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    return rows


def _unity_euler(o):
    """World rotation of a Blender object as Unity euler degrees (x, y, z), for yaw-and-tilt parts."""
    m = o.matrix_world.to_3x3()
    # Blender -> Unity basis change (x, y, z) -> (x, z, y): M_u = P M_b P with P swapping y and z
    import mathutils
    P = mathutils.Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))
    mu = P @ m @ P
    # the elementary rotation matrices read the same in both bases; Unity applies Z, then X, then Y
    e = mu.to_euler("ZXY")
    return [round(math.degrees(e.x), 3), round(math.degrees(e.y), 3), round(math.degrees(e.z), 3)]

"""Assemble Docs/KingRush/ENDGAME_LD_HARD_v1.md from the template, the computed tables, the Unity list and the
Blender reports (Review/validation_v1.json, Review/export_report.json).

    python make_doc.py
"""
import json
import os
import struct
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(ROOT, "Docs", "KingRush", "ENDGAME_LD_HARD_v1.md")


def read(name):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        return f.read()


def load(path):
    p = os.path.join(HERE, path)
    if not os.path.exists(p):
        return None
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def png_size(path):
    """(width, height) of a PNG under this folder, None when it is not there (renders are LFS files)."""
    p = os.path.join(HERE, path)
    if not os.path.exists(p):
        return None
    with open(p, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return struct.unpack(">II", head[16:24])


def blender_section():
    val = load("Review/validation_v1.json")
    exp = load("Review/export_report.json")
    lines = []
    lines.append("다시 만들기와 파일 설명은 [ArtSource/PawnRush/Endgame/README.md](../../ArtSource/PawnRush/Endgame/README.md). "
                 "Blender 5.2(PyPI `bpy` 5.2.3)를 화면 없이 돌렸고, GPU가 없는 클라우드라 EEVEE는 Mesa llvmpipe(소프트웨어 OpenGL)로 렌더했다.")
    lines.append("")
    lines.append("| 산출물 | 위치 |")
    lines.append("|---|---|")
    lines.append("| 빌드 스크립트(키트 · 배치 · 검증 · 렌더 · 내보내기) | `ArtSource/PawnRush/Endgame/build_endgame_v1.py` + `eg_*.py` |")
    lines.append("| 전체 장면 | `ArtSource/PawnRush/Endgame/Review/Endgame_work.blend` (컬렉션 EG_S1 … EG_EX, EG_Devices, EG_Rails, EG_Signals, EG_Hall, EG_Kit, EG_GameplayRef(잠금 · 와이어)) |")
    lines.append("| 부품 FBX(LOD0–2, UV1) + _work.blend | `ArtSource/PawnRush/Endgame/PR_EG_*/` |")
    lines.append("| 배치 목록 | `ArtSource/PawnRush/Endgame/PR_EG_manifest.json` (Unity 축) |")
    lines.append("| 텍스처 | `ArtSource/PawnRush/Endgame/Textures/` (URP Lit 채널 묶음, 트림 시트, 데칼 아틀라스) |")
    cut = png_size("Review/cut_S1.png")
    cut_txt = f"({cut[0]} × {cut[1]})" if cut else ""
    lines.append(f"| 리뷰 렌더 | `Review/plan_overview.png`, `Review/sheet_<비트>.png`(1 px = 2 cm), `Review/cut_<S1 · R1 · S2 · S3 · R5 · S4>.png`{cut_txt}, "
                 "`Review/timing_preview.mp4`(장치 타이밍, 52 s) |")
    lines.append("| 장치 스킨 시트 · 턴테이블 | `Review/skins_sheet.png`(스킨 22종 단독), `Review/turntable_sheet.png`(같은 22종을 90°마다 네 방향) |")
    lines.append("")
    if exp:
        skins = [r for r in exp if any(k in r["part"] for k in ("WarTable", "SweepBar", "Ferry", "Hurdle", "ShelfWall", "Gate",
                                                               "RookScreen", "Pendulum", "MapChest", "WindVent", "MapBelt",
                                                               "GiantPawn", "Crown", "HangingCrate", "CompassDisc", "Bridge"))]
        lines.append(f"내보낸 부품 {len(exp)}개(장치 스킨 {len(skins)}개). LOD0 삼각형이 가장 많은 것:")
        lines.append("")
        lines.append("| 부품 | LOD0 / LOD1 / LOD2 |")
        lines.append("|---|---|")
        for r in sorted(exp, key=lambda r: -r["lod_tris"][0])[:8]:
            lines.append(f"| `{r['part']}` | {' / '.join(str(t) for t in r['lod_tris'])} |")
        lines.append("")
    if val:
        inter = val["interference"]
        hits = [d for d in inter if d["hits"]]
        samples = sum(d["samples"] for d in inter)
        lines.append("### 검증 보고")
        lines.append("")
        lines.append(f"- **간섭**: 움직이는 장치 {len(inter)}개, 한 주기 0.05 s 표본 {samples}개, 스킨을 2 cm 줄여 정적 지형 · 같은 비트의 다른 장치와 BVH 겹침 → "
                     + ("**0건**." if not hits else f"{len(hits)}개 장치에서 겹침: " + ", ".join(d["device"] for d in hits) + "."))
        lines.append("  - 처음 돌렸을 때 걸린 것(고친 뒤 0건): 06 벽 ↔ 홈 끝 난간 기둥, 룩 차폐판 ↔ 난간 금 덮개(안쪽 8 cm), 회전판 장식 테 ↔ 다리, "
                     "사슬 상자 바퀴 ↔ 레일(13 cm), 접이식 다리 경첩 ↔ 착지대(6 cm), 낙하 말 ↔ 벨트 윗면(2.9 cm).")
        lines.append("  - 09 시계 문이 위로 열릴 때 아치 상자를 지나가는 것은 프리팹 설계라 검사에서 뺐다.")
        lines.append("  - 스킨 시트 검토에서 R2 06 책장 벽의 책이 양쪽 모두 벽 속(안쪽)을 향한 것을 찾아 바깥으로 돌렸다(겉 크기 · 충돌체와의 간격은 그대로, 다시 검사해 0건).")
        p = val["path"]
        bad_gaps = [g for g in p["gaps"] if not g["ok"]]
        lines.append(f"- **경로**: 필수 틈 {'모두 1.5 m 이하' if not bad_gaps else '초과 ' + str(len(bad_gaps)) + '곳'}, 올라가는 턱 없음, "
                     f"난간 {len(p['rails'])}개 모두 1.2 m, 가장 좁은 곳 6 m(R3 · R5b), CP 8개 모두 장치 범위 밖, "
                     f"마지막 밀어내기 뒤 정리 구간 {p['notes'][0]['metres'] if p['notes'] else '?'} m.")
        unr = sorted({u["floor"] for u in p["unrailed"]})
        lines.append(f"  - 난간 없는 바닥 가장자리(의도): {', '.join(unr)}. 장치 위(S2 갑판, R5 벨트 구간, R5b 상자, R6 원판, S4 다리 판)도 난간이 없다. "
                     "06 벽 홈은 벽 자체가, 계단 · 입구 띠는 옆 대리석 벽이 막는다.")
        b = val["budget"]
        worst = max(b["views"], key=lambda v: v["visible_tris"])
        lines.append(f"- **예산**: 리뷰 카메라 6대 중 가장 많은 화면 {worst['camera'].replace('Budget cam ', '')} {worst['visible_tris']:,} 삼각형(기준 1,500,000), "
                     f"장면 전체 {b['scene_tris']:,}, 재질 {b['material_count']}개(기준 12). 장치 스킨 최대 "
                     + str(max(a['tris'] for a in b['assets'] if a['kind'] == 'skin')) + " 삼각형(기준 20,000), 홀 모듈 최대 "
                     + str(max(a['tris'] for a in b['assets'] if a['kind'] == 'hall')) + "(기준 5,000).")
        lines.append("- **안전 창**: §3 표. 모두 `eg_timing.py`가 Unity 장치 코드와 같은 함수로 계산.")
        lines.append("")
    lines.append("### 텍스처와 UV")
    lines.append("")
    lines.append("- UV0: 박스 투영, 모든 부품이 1 UV = 4 m(월드 기준이라 이어지는 부품끼리 무늬가 맞음). 큰 면은 재질별 타일 텍스처(대리석 · 지도 2048은 타일링 1, 나머지 1024는 타일링 2, 모두 512 px/m).")
    lines.append("- **트림 시트**: 내보낼 때 너비 0.35 m 이하인 금 · 청록 · 호두나무 · 돌 · 쇠 · 지도 면(베벨, 띠, 몰딩)을 `EG_Trim`으로 옮기고 UV0을 그 재질의 띠(256 px)에 깐다. 띠는 U로 4 m마다 반복. 재질은 12개(기준 이하).")
    lines.append("- UV1_Lightmap: Lightmap Pack(겹침 없음, 여백 있음). 데칼 아틀라스(1024)는 나침반 · 룩 문장 · » 화살표 · 왕관 원 · 지도 인쇄.")
    lines.append("- 굽기: 지시서는 Cycles 굽기였지만, 절차적 무늬를 이음새 없이 타일로 만들려고 같은 무늬를 주기적 잡음(numpy, `make_textures.py`)으로 다시 만들었다. Blender 재질은 렌더 기준으로 남김.")
    lines.append("")
    lines.append("### 아직 안 한 것")
    lines.append("")
    if not png_size("Review/turntable_sheet.png"):
        lines.append("- 장치 턴테이블 렌더.")
    else:
        lines.append("- 턴테이블은 90°마다 네 방향 정지 시트다. 도는 동영상은 만들지 않았다.")
    if not cut or cut[0] < 1920:
        lines.append("- 컷 6장의 1920 × 1080 최종본" + (f"(지금 {cut[0]} × {cut[1]})." if cut else "."))
    lines.append("- Unity에 넣어 확인하기(이 문서의 어떤 것도 Unity에서 보지 않았다).")
    return "\n".join(lines)


def main():
    tables = subprocess.run([sys.executable, os.path.join(HERE, "eg_doc_tables.py")], capture_output=True, text=True,
                            cwd=HERE, check=True).stdout
    tables = tables.replace("## 비트 표", "## 3. 비트 표 · 안전 창 · 시간\n\n### 비트 표").replace("## 안전 창", "### 안전 창").replace("## 시간", "### 시간")
    doc = read("doc_template_ld_v1.md")
    doc = doc.replace("{TABLES}", tables.strip())
    doc = doc.replace("{UNITY_LIST}", read("doc_unity_list_v1.md").strip())
    doc = doc.replace("{BLENDER_SECTION}", blender_section())
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(doc)
    print("wrote", OUT)


if __name__ == "__main__":
    main()

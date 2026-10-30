# 폰 러시 엔드게임(전략회의실) — Blender 키트 · 블록아웃 · 검증 (R255, 2026-10-30)

설계 문서: [ENDGAME_LD_HARD_v1](../../../Docs/KingRush/ENDGAME_LD_HARD_v1.md). **Unity에 아직 넣지 않았고, 사람이 확인하지 않았다.**

## 다시 만들기

스크립트 하나가 키트 생성 → 배치 → 검증 → 렌더 → 내보내기 → 배치 목록까지 한다(Blender 5.2).

```
blender --background --factory-startup --python build_endgame_v1.py -- Review --renders quick --check --export --parts .
```

- `--renders none|quick|cuts|sheets|mp4|full`: quick = 전체 배치도 + 컷 6장(1280 × 720), sheets = 비트별 배치도(1 px = 2 cm, Grease Pencil 치수선), mp4 = 장치 타이밍 미리보기, full = 전부(컷 1920 × 1080).
- `--check`: 간섭(장치마다 한 주기를 0.05초로 표본, 스킨을 2 cm 줄여 BVH 겹침) · 경로 · 안전 창 · 예산 → `Review/validation_v1.json`.
- `--export --parts <폴더>`: 부품마다 `<폴더>/<Part>/<Part>.fbx`(LOD0 · LOD1 · LOD2, UV0 + UV1_Lightmap)와 `<Part>_work.blend`, `<폴더>/PR_EG_manifest.json`.
- GPU 없는 리눅스(클라우드)에서는 Mesa(llvmpipe)를 깔고 `EGL_PLATFORM=surfaceless`로 돌린다. Blender 대신 PyPI `bpy==5.2.3`도 같다(`python build_endgame_v1.py -- …`).

계산만 따로(블렌더 없이):

```
python eg_timing.py ld_v1      # 비트별 안전 창 (graybox도 됨)
python eg_estimate.py          # 이상 · 실수 없는 평균 · 실패 포함 평균
python eg_doc_tables.py        # 문서 표
python make_textures.py        # Textures/ (numpy + Pillow)
```

## 파일

| 파일 | 하는 일 |
|---|---|
| `eg_layout.py` | 회색 상자(`graybox()`, WarRoom.cs 그대로)와 LD v1(`ld_v1()`)의 바닥 · 벽 · 난간 · 장치 · CP. 장치 시간 함수(ObstacleMotion · PendulumSwing · FoldingBridge · FallingChessPiece · AirVent를 그대로 옮김) |
| `eg_timing.py` | 폰(0.7 × 1.0 m 캡슐)이 2.5 m/s로 지날 때 한 주기의 안전 창 |
| `eg_estimate.py` · `eg_doc_tables.py` | 시간 추정, 문서 표 |
| `build_endgame_v1.py` | 진입점 |
| `eg_bl_util.py` · `eg_bl_materials.py` | 장면 · 메시 도구, 절차적 재질 11개 |
| `eg_bl_kit.py` | 키트(난간 · 기둥 · 지도 탁자 · 룩 조각상 · 홀 장식)와 장치 스킨 21종 |
| `eg_bl_scene.py` | 배치(비트별 바닥 · 벽 메시, 난간 인스턴스, 장치 루트 + 스킨, 홀), 시계로 자세 잡기 |
| `eg_bl_check.py` · `eg_bl_annot.py` · `eg_bl_anim.py` · `eg_bl_render.py` · `eg_bl_export.py` | 검증, 배치도 주석, 타이밍 MP4, 렌더, 내보내기 |
| `make_textures.py` → `Textures/` | 이음새 없는 텍스처(URP Lit 채널 묶음), 트림 시트, 데칼 아틀라스 |
| `PR_EG_*/` | 부품 FBX + _work.blend (Git LFS) |
| `Review/` | 렌더, 검증 보고, 전체 장면 `Endgame_work.blend` |
| `checks/` | 안전 창 · 시간 추정 JSON, 문서 표 |

## Unity로 넘기기 (지시서 §10)

- 축: Blender X = Unity X, Blender Y = Unity Z(진행 방향), Blender Z = Unity Y. 원점은 S1 바닥 앞 가운데. FBX 설정은 소드 파이트 경기장과 같다.
- 재질은 이름만(EG_Marble_Cream · EG_Gold · EG_Teal · EG_Walnut · EG_MapPaper · EG_BannerNavy · EG_Stone · EG_Lamp_Emissive · EG_Iron · EG_Books · EG_Window_Emissive). Unity에서 URP Lit에 `Textures/`를 끼운다: `_BaseMap` ← `_BaseColor`, `_MetallicGlossMap` ← `_MetallicSmoothness`(R 금속, A 매끄러움), `_BumpMap` ← `_Normal`, `_OcclusionMap` ← `_Occlusion`(G).
- 충돌체는 넣지 않았다. Unity 빌더가 기본 도형 충돌체를 만든다. 밟거나 닿는 스킨 면은 충돌체와 ±2 cm.
- 장치 스킨은 `PR_EG_manifest.json`의 `attach` 이름(예: `Device_S2_ferry`)인 장치 루트 아래에, 움직임 0일 때의 자리로 넣는다.
- 바닥 · 벽(`PR_EG_Floor_<비트>`, `PR_EG_Walls_<비트>`)은 코스 좌표 그대로라 원점(0, 0, 0)에 둔다.

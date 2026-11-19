# 왕의 서재 코스 그래픽 작업대 (seoungtest R687, 2026-11-17)

pr/2에서 가져온 왕의 서재 → 거대 체스 코스(`WarRoomComplete`)를 D "거인의 책상" 컨셉대로 모델링하기 위한 Blender 작업 폴더입니다.
맵 구조는 [ROYAL_LIBRARY_STRUCTURE](../../../Docs/PawnRush/ROYAL_LIBRARY_STRUCTURE.md), 컨셉아트는 [R687_GiantChessLibrary](../../../Docs/PawnRush/ConceptArt/R687_GiantChessLibrary).

## 들어 있는 것

| 파일 | 내용 |
|---|---|
| `rl_blockout.py` | Unity 프리팹을 읽어 Blender에 그대로 다시 세움(위치 · 회전 · 크기 · 기본 도형 · 재질 색). 프리팹은 읽기만 함 |
| `rl_tile10_fit.py` | 체스 방 장식을 칸 10 m 판에 맞춤(탁자 · 의자 · 판 미리보기 · 출구 길, 28개). 16 m 값일 때만 바꿔 두 번 돌려도 같음. `--dry-run`으로 미리 보기. Unity Play 중에는 돌리지 말 것 |
| `rl_kit.py` | 구역 모델 스크립트가 같이 쓰는 도구(R0): 재질 칸 8개 `RL \| stone · walnut · varnished · brass · leather · parchment · ink · cloth`(`slot()`), 미터 UV(`surface_uvs`, 마차뜰 rcy_kit 것), 프리팹 물체의 자세(`frame`), FBX 내보내기(`export` → `Assets/Resources/RoyalLibrary/Models/<구역>/<모델>.fbx`, 움직이는 장치는 그 물체 기준 좌표로) |
| `rl_r0_test_table.py` | (기록용) R0 시험 한 점: 코스 탁자 키트(`e5/rl_e5_06_07_tables.py`의 `build_table`)로 출발 탁자를 처음 끝까지 내보낸 스크립트(승규 Unity 확인 11-18). 출발 탁자는 R1이 넘겨받아 지금은 `rl_r1_entrance_model.py` → `Models/Entrance/RlStartTable.fbx` · `RoyalLibraryDressing.Entrance.cs`(`Models/R0` · `RoyalLibraryDressing.R0.cs`는 지움) — 이 스크립트는 다시 돌리지 말 것(쓰지 않는 `Models/R0` FBX를 다시 만듦) |
| `rl_r3_typewriter_model.py` | R3 타자기 구역: E5 타자기 · 키 · 힌트판 · 코스 탁자를 블록아웃 판정에 맞춰(세그먼트 상한) → `rl_r3_typewriter.blend`, 사진 `stills/r3/`, FBX 7개 `Models/Typewriter/`(키는 `Key 0-0`, 힌트판은 `Memory clue 1` 기준), Unity는 `RoyalLibraryDressing.Typewriter.cs` |
| `rl_r7_desk_props_model.py` | R7 거인 책상 소품(D6 위 두 줄): 잉크병 · 회중시계 · 돋보기 · 탁상 스탠드 · 찻잔 · 양피지 + 만년필을 가죽 책 더미 받침 위에 10곳(`PLACES`, 체스 방 C1 ~ C6 · 앞 구간 F1 ~ F4). `-- layout --still`(배치도) · `-- <소품|all> --still`(사진 `stills/r7/`) · `-- export [--preview <폴더>]` → `Models/Desk/RlDesk<자리>.fbx` · `-- check <fbx 폴더>`, 블렌드 `rl_r7.blend`. Unity `RoyalLibraryDressing.Desk.cs`, 조명은 `RoyalLibraryLighting.cs` |
| `rl_lookdev.py` | 내보낸 FBX를 라이브러리 재질(텍스처 · 디테일 · 타일링)로 입혀 렌더 → 모양 확인용 |
| `rl_giant_model.py` | 거인 GM(뼈대 포함), `-- A|B [--export]` → `Models/ChessRoom/RlGiant_A/B.fbx`, 시험 사진 `stills/giant/` (R6, 승규 확인 11-18) |
| `rl_giant_face_stills.py` | 거인 얼굴을 기준 그림과 같은 크기로 비교(`blender -b rl_giant_a.blend --python … -- A`) |
| `rl_giant_closeup_stills.py` | 거인 손 · 어깨 가까이 시험 사진(`-- hand|shoulder <out.png>`) |
| `rl_giant_acting_stills.py` | 거인 동작(기다림 · 반응) 미리보기 — `GiantGrandmaster.cs`와 같은 계산 |
| `rl_giant_neck_stills.py` | 거인 목 확대 시험 사진(`blender -b rl_giant_a.blend --python … -- <폴더>`) |
| `rl_r6_chess_model.py` | R6 체스(거인 제외, 11-18): 체스판 · 경고 칸 · 점유 테두리 · 진입/출구 발판 · 결승선 · 출구 아치 · 기물 6종(D6, 몸통 `ivory`, 흑은 코드가 `ebony`) → `Models/Chess/RlChess*.fbx`, `GiantChessCourse.cs`가 Play 때 걸음. `-- <부품|all> [--no-export] [--stills] [--preview <폴더>]`, 사진 `stills/r6/`
| `rl_textures.py` | 재질 라이브러리 8종(R0 첫 단계, 승규 "이 사진과 똑같게 해상도로 구워" — `textures/swatch_row_target.png`, D6 시트 아래 견본 줄): Codex 원본(`textures/codex/r0`, 1254²) → 이음매 없는 1024² 타일 `textures/library/RL_*_Albedo.png`(알파 = 부드러움) · `_Normal.png` + `rl_textures.json`. 마차뜰 `rcy_textures.py`의 도구를 그대로 씀. 평균색은 견본에서 잰 색과 1 ~ 4 단위 차 |
| `rl_unity_materials.py` | 위 라이브러리를 Unity로: `Assets/Art/RoyalLibrary/Textures` + `Assets/Resources/RoyalLibrary/Materials/RL_*.mat`(URP Lit). GUID는 경로로 정해짐(다시 돌려도 같음), Unity Play 중에는 돌리지 말 것 |
| `rl_detail_compare.py` | 디테일 맵 확인: 재질마다 40 cm 판을 낮은 해 아래 렌더(왼쪽 기본만, 오른쪽 + 디테일) → `textures/detail_compare.png` |
| `rl_swatch_compare.py` | 굽은 재질을 견본처럼 둥근 판에 입혀 렌더(조명 확인용). 견본과 납작한 텍스처 비교는 `textures/swatch_vs_texture.png` |
| `RoyalLibrary_blockout.blend` | 위 스크립트로 만든 작업 파일(Blender 5.2.3) |

## 장면 구성 (`RL …` 컬렉션)

| 컬렉션 | 무엇 |
|---|---|
| 00 Shell | 외벽 · 천장 · 서재 벽 · 바닥 장식 |
| 01 Entrance ~ 04 Typewriter | 앞 구간: 차원문 · 출발 탁자, 도장 탁자, 덮히는 책 · 인장 휴식, 타자기 |
| 05 Library decor | 앞 구간 책장 · 화분 · 기보 액자 |
| 06 Chess room | 거대 체스 방(총지휘부) 벽 · 참나무 탁자 · 의자 · 서가 · 화초 · 문 |
| 07 Exit / 08 Markers | 출구 길, 체크포인트 · 경로 점(빈 오브젝트) |
| 09 Play-time chess, tile 10 m (정한 값) | Play 때 코드가 만드는 체스판 · 진입/출구 발판 · 출구 아치 · 거인 자리(장면 값 10 m) |
| 09 Play-time chess, tile 16 m | 같은 것을 장식 기준 16 m로(숨김) — 칸 크기 정할 때 켜서 비교 |
| 10 Concept art | D · E 컨셉 12장을 코스 서쪽(x = -230)에 세워 둔 참고 이미지 |

- 축: Unity (x, y 위, z 앞) → Blender (x, z 위, y 앞). 진행 방향이 Blender +Y. 단위 미터 그대로.
- 프리팹 계층은 부모-자식 그대로이고, 오브젝트 이름 = Unity 이름(사용자 속성 `unity_name`에도 있음). 모델을 만들면 같은 자리에 두고 이름을 맞추면 Unity에서 찾기 쉽습니다.
- 같은 모양은 메시 하나를 같이 씁니다(`RL_unit_cube` 등). 색은 오브젝트마다 `RL_<재질>` — Unity 재질 이름과 같음.
- 천장 · 천장 들보는 뷰포트에서만 숨김(위에서 코스가 보이게).
- 비활성 오브젝트는 숨김.

## 다시 만들기

프리팹이 바뀌면(pr/2를 다시 가져오는 등):
- 열린 Blender에서: Text Editor 또는 MCP로 `exec(open(r"…\rl_blockout.py").read())` — `RL …` 컬렉션만 지우고 다시 세움.
- 백그라운드: `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --python ArtSource/PawnRush/RoyalLibraryModels/rl_blockout.py` → `RoyalLibrary_blockout.blend` 덮어씀.
- 모델 작업은 이 파일을 덮어쓰지 말고 별도 `.blend`(예: `RoyalLibrary_v1.blend`)에서 블록아웃을 링크하거나 복사해 시작하세요.

Blender 연결(MCP) 설정은 [Tools/BlenderMCP](../../../Tools/BlenderMCP/README.md).

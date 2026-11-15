# 왕실 마차뜰 컨셉 아트 프롬프트 (R676, 2026-11-15)

'체스 왕국의 마차뜰' 컨셉 아트 8장을 만드는 프롬프트입니다.
- 배치 · 크기 · 개수는 지금 맵(R662) 코드 값입니다.
- 그림은 경기에서 쓰는 구간 모드 기준입니다.
- 이미지 생성기가 읽도록 프롬프트는 영어로 썼습니다.

## 쓰는 법 (ChatGPT 같은 이미지 생성기)

1. 아래 둘째 표의 프롬프트 파일 내용을 통째로 붙여 넣습니다.
2. 참고 그림을 **아래 순서대로** 같이 첨부합니다. 프롬프트가 그림을 "reference 1-9"로 부릅니다.

| 순서 | 파일 | 무엇을 가져오나 |
|---|---|---|
| 1 | `Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets/01_mechanical_assets.webp` | 렌더 스타일 |
| 2 | `Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets/02_architecture_modules.webp` | 렌더 스타일 · 7번 시트 형식 |
| 3 | `Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets/03_props_materials.webp` | 렌더 스타일 · 8번 시트 형식 |
| 4 | `Docs/PawnRush/ConceptArt/R506_CeremonialCourt/A_01_overview.png` | 같은 1단계 광장: 제목 형식 · 하늘 · 먼 궁전 정면 |
| 5 | `Docs/PawnRush/ConceptArt/R505_PortcullisCourt/D_01_overview.png` | 같은 1단계: 성벽 · 룩 모양 탑 · 슬레이트 지붕 |
| 6 | `ArtSource/Loading/CourseReveal/MiniGames/Early_MG_C_MoatDrawbridge.png` | 게임 화면: 돌 모양 · 물색 |
| 7 | `RCY_ref_pawns_in_game.png` (이 폴더) | 플레이어 폰의 게임 속 모습(흑 · 백) |
| 8 | `RCY_ref_pawn_front.png` (이 폴더) | 폰 비율 · 얼굴 · 팔 |
| 9 | 이미지마다 다른 배치도(아래 표) | 위에서 본 실제 배치. 1~6번 이미지만 |

| 이미지 | 붙여 넣을 프롬프트 | 9번 배치도 |
|---|---|---|
| 1 전체 조감(세로) | `RCY_01_overview_prompt.txt` | `RCY_map_course.png` |
| 2 짐마차 마당 | `RCY_02_wagon_yard_prompt.txt` | `RCY_map_wagon_yard.png` |
| 3 거대 물레방아 | `RCY_03_waterwheel_crossing_prompt.txt` | `RCY_map_canal.png` |
| 4 하역 창고 앞쪽: 컨베이어 · 짐더미 줄 1 · 목재 크레인 | `RCY_04_sorting_yard_cranes_prompt.txt` | `RCY_map_warehouse.png` |
| 5 하역 창고 뒤쪽: 다락 승강대 · 짐더미 줄 2 · 하역장 | `RCY_05_hoist_loft_and_berth_prompt.txt` | `RCY_map_warehouse.png` |
| 6 연못 물레방아 · 대리석 절벽 · 최종 성문 | `RCY_06_pond_cliff_and_gate_prompt.txt` | `RCY_map_pond_gate.png` |
| 7 건축 모듈 시트 | `RCY_07_architecture_modules_prompt.txt` | 없음 |
| 8 소품 · 재질 시트 | `RCY_08_props_materials_prompt.txt` | 없음 |

권하는 순서:
1. 1번(전체 조감)만 먼저 뽑아 분위기를 봅니다.
2. 마음에 들면 2~6번을 뽑습니다.
3. 마지막에 7 · 8번 시트를 뽑습니다.

다른 방법:
- **첨부를 못 하는 도구(Midjourney 등):** `RCY_short_prompt.txt`를 씁니다. 짧은 프롬프트와 제외어 한 줄입니다.
- **Codex로 8장을 한 번에:** `PROMPT.md` 전체를 씁니다. 첫 문단이 진행 방법을 지시합니다.
- **직접 그린 마차뜰 그림이 있을 때(R532 · R659):** 10~12번으로 더 붙여도 됩니다. 분위기 · 짐 · 크레인만 참고하게 되어 있습니다.

## 컨셉에서 정한 것

바꾸고 싶은 것이 있으면 알려 주세요.

- **파란색을 쓰지 않음.**
  - 지금 블록아웃의 파란 원뿔 지붕 · 깃발 · 차양 · 마차는 이렇게 바뀝니다: 회색 슬레이트 지붕, 크림색 깃발, 모래색 천, 진녹색 · 호두색 마차.
  - 이유 ①: 마차뜰 다음 방인 1단계 광장(C · D · A)의 컨셉 아트와 맞춥니다.
  - 이유 ②: 파랑이 백팀 색으로 읽히지 않게 [3단계 아트 기준](../../PAWN_RUSH_ART_PHASES.md)을 따릅니다.
- **체스 성으로 보이게 한 것:**
  - 탑은 모두 룩 머리(평평한 톱니 왕관)이고 성벽 위에도 톱니를 둡니다.
  - 마차뜰 문장은 왕관 쓴 나이트 머리이고 무광 황동입니다.
  - 짐마다 기물 그림 도장, 분류 칸마다 기물 명판을 붙입니다.
  - 크레인 · 승강대 팔 끝에 나이트 머리 조각을 답니다(바닥에서 6 m 넘는 곳에만).
  - 먼 궁전의 지붕선은 룩 · 비숍 · 퀸 · 킹 모양입니다.
- **말은 그냥 말:** 나이트 기물과 헷갈리지 않게 이마 끈의 작은 나이트 배지만 답니다.
- **마차는 지금 코드처럼 닫힌 왕실 마차:** 유리 대신 덧창이고 마부석은 비어 있습니다. 천 덮인 짐마차로 바꿀지는 정해 주세요.
- **바닥은 무늬 없는 판석:**
  - 체크 무늬는 문과 체크포인트 아래 한 줄짜리 "랭크 띠"뿐입니다.
  - 이 띠는 새로 제안한 것이고 지금 맵에는 없습니다.
- **색의 뜻은 게임 그대로:** 밝은 금색은 밟고 · 잡고 · 뛰는 곳, 빨강은 위험 예고, 초록은 가도 됨입니다. 장식 금속은 무광 황동입니다.
- **최종 성문 뒤:** 경기용 구간 모드(테라스 · 아치 · 계단)를 그립니다. 맵 시험 장면의 결승 뜰 · 왕관은 그리지 않습니다.

## 다시 만들기

- **배치도 · 폰 그림:** `ArtSource/PawnRush/CarriageYardConcept/rcy_concept_refs.ps1`을 Windows PowerShell에서 실행합니다. 맵 좌표가 바뀌면 이 스크립트의 숫자를 고칩니다.
- **이미지별 프롬프트 파일:** `PROMPT.md`를 고친 뒤 `ArtSource/PawnRush/CarriageYardConcept/rcy_assemble_prompts.py`를 실행합니다.

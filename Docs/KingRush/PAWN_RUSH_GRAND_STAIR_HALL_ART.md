# 대계단 현관홀(2-1) 아트 — 이어서 작업하는 사람을 위한 안내

> main R309(2026-11-05). 승규 님이 `seoungtest` 브랜치의 시험 씬(구역 H)에서 만든 대계단 현관홀 아트를 "여기서 작업은 끝났어 … 다른 사람이 이어서 작업 할 수 있게 main에다가 넣어"로 main에 옮겼다. 블록아웃(길·충돌·장애물 판정)은 [PAWN_RUSH_SECTIONS](PAWN_RUSH_SECTIONS.md)·[기획 v0.3](PAWN_RUSH_GRAND_STAIR_HALL_DESIGN_v0.3.md)이 그대로 맞고, 이 문서는 그 위에 입힌 **그림(모델 · 재질 · 효과)**만 다룬다.
>
> **번호 주의:** 코드 주석의 R308 ~ R413은 `seoungtest`의 번호다(main의 R308 · R309와 다름). 요청 하나하나의 원문 · 결과는 `seoungtest`의 `HANDOFF.md` 머리글과 `Docs/Project/REQUIREMENTS.md`에 있다.

## 1. 어떻게 도는가

| 단계 | 하는 일 | 코드 |
|---|---|---|
| 1. 블록아웃 | 프리팹 `Prefabs/PawnRushSections/Mid_2-1_GrandStairHall.prefab`의 `Content`. 메뉴 **ChessFight → Pawn Rush → Build Mid 2-1 Grand Stair Hall**로 굽거나 Play 때 코드로 지음 | `GrandStairHallBuilder*.cs` |
| 2. 옛 블록아웃 다시 짓기 | `Content` 밑에 `Layout v2 (seoungtest R413)` 표시가 없으면(이번 전에 구운 프리팹) Play 때 새로 지음. 아트가 지금 블록아웃(나선 발치 · 환풍기 턱 · 퀸 석상 블록 등)을 전제로 하기 때문 | `GrandStairHall.Awake`, `GrandStairHallBuilder.LayoutMark` |
| 3. 아트 입히기 | **Play 때만.** 아트는 재질 · 메시를 코드로 새로 만드는데 프리팹 에셋은 그것을 담을 수 없어서 굽지 않는다. 블록아웃 조각은 그림만 끄고 충돌은 그대로 | `Course01v4Builder.DressGrandStairHall` → `GrandStairHallModel` |

끄기: 프리팹의 `GrandStairHall` → `Dress In Play` 체크 해제(블록아웃만 보임).

**Scene 창(Play 전)에는 블록아웃만 보인다.** 아트를 보려면 `PawnRush_Sections` 시험 씬에서 Play.

## 2. 무엇이 있는가(구획별)

| 구획 | 그림 | 코드(`Assets/Scripts/PawnRush/`) | 자산 |
|---|---|---|---|
| 홀 전체 | Blender 홀 v15(나선 띠 · 아치 · 원형 벽 · 돔 · 바닥) | `Course01v4Builder.GrandStairHallModel.cs`(차례와 홀 재질), `HideListed`가 블록아웃 그림 끔 | `Resources/PawnRushGrandStairHall/GrandStairHallV15.fbx` + `_Hidden.txt` |
| 벽 · 장식 | 크림 석회암 + 금 몰딩, 원형 벽은 금 없음, 창살 무광 은색 | `GrandStairHallModel.cs`(`PalaceMaterial` · `PlainPalaceWall` · `MatteSilver`) | 셰이더 `PR_PalaceStone` · `PR_PalaceWall` · `PR_PalaceTrim`, `PalaceTrim.png` |
| 바닥 · 계단 | 칼라카타 오로 대리석(월드 좌표), 이음새 없음 | `.GrandStairHallFloors.cs` · `.GrandStairHallStair.cs` | `PR_VeinedMarble` · `PR_CalacattaOro.mat` |
| 대계단(비트 1) | 대리석 계단 · 빨간 카펫 · 금 난간 · 왕관 등불 | `.GrandStairHallStair.cs`(`RoyalStair`) | `RoyalStair.fbx` · `RoyalStair_Marble*.png` · `HallCarpet.png` |
| 굴림대 | 카펫 말이(뛰어넘기) · 흰 대리석 폰(피하기), 튀고 흔들림 | `RollingSpawner.cs` | `RollingCarpet.fbx` · `RollingStatue.fbx` |
| 계단 옆 석상 4 | 흰 대리석 + 트래버틴 받침 | `.GrandStairHallStatues.cs`(`StairStatues`) | `StairStatuesV36.fbx`, `PR_StatueMarble` · `PR_Travertine` |
| 중앙 계단참 석상 | 퀸(받침 없음, 대리석 + 금 왕관 · 홀) | `.GrandStairHallStatues.cs`(`LandingQueen`), 블록 `GrandStairHallBuilder.Beats` | `QueenStatue.fbx` |
| 샹들리에 낙하 | 예고 뒤 나타나 떨어져 튐 + 바닥 예고 VFX | `ChandelierRain.cs`, `FallingPieceFx.cs` | `FallingChandelier.fbx`, `VFX/Shaders/VFX_*` |
| 나선 길 | 체스판(시작 1 m는 대리석 + 금 문턱), 금 테 | `.GrandStairHallStair.cs`(`SpiralPaving` · `SpiralFootJoin`), `HallAnchoredMaterials.cs` | `PR_SpiralChecker` |
| 해머 2 | 은색 받침 · 금 사슬 13고리(흔들리며 휨) · 공 궤적 | `.GrandStairHallHammer.cs`, `HammerChainSway.cs` | `PendulumHammer.fbx` |
| 떠 있는 발판 · 점프 발판 | 발판 모델, 점프 발판 사각 빛 VFX | `.GrandStairHallModel.cs`(`HangPads`), `JumpPadFx.cs` | `FloatingPad.fbx` · `JumpPadModel.fbx` + png |
| 바람 길 | 왕실 환풍기(회전) + 바람 VFX, 돌 턱 | `.GrandStairHallWind.cs`, `RoyalFanSpin.cs`, `VentWindFx.cs` | `RoyalFan.fbx` + png, `VFX_WindFlow` · `VFX_WindSwirl` |
| 떨어지는 기사 | 기사 모델 + 바닥 예고 데칼 | `.GrandStairHallKnights.cs`, `FallingPieceFx.cs` | `FallingKnight.fbx` |
| 왕실 발코니 | 벽감 · 시계 대문 · 근위 기사 예고(유령 리허설) | `.GrandStairHallBalcony.cs`, `CavalryWarnFx.cs` | `RoyalBalconyV34.fbx` · `RoyalBalconyMovers.fbx` |
| 입구 · 현관 옆벽 | 손으로 지은 입구(아치는 호두나무), 막힌 창 | `.GrandStairHallEntrance.cs` | `EntranceV46.fbx` · `FoyerSidesV47.fbx` + `_Lights.txt`, `PR_WorldWood` |
| 돔 | 크림 돌 뼈대 + 깎인 모서리 금선, 금 천창 | `.GrandStairHallDome.cs`, `HallAnchoredMaterials.cs` | `PR_PalaceTrim` |
| 난간 | 호두나무(월드 좌표 결) | `.GrandStairHallEntrance.cs`(`WoodenGateAndRails`) | `PR_WorldWood` |

## 3. Blender 원본

`ArtSource/PawnRush/GrandStairHall/`: 스크립트(`*.py`, 파일 머리 주석에 실행 방법), 최신 `.blend` 7개(`grand_stair_hall_v47C` = 홀 전체의 마지막 상태, 나머지는 부품), `source/`(받은 Tripo 원본 등), `textures/`, 최신 렌더 폴더. 홀 `.blend`의 옛 버전(v2 ~ v46C, 약 2 GB)과 옛 렌더는 `seoungtest` 브랜치에만 둔다.

자주 쓰는 것: 해머 `hammer_v51.py`(사슬 고리 따로), 퀸 `queen_statue_v52.py`, 환풍기 `royal_fan_v35.py`, 입구 `entrance_v46.py`, 대계단 `royal_stair_v45.py`. 내보내면 `Assets/Resources/PawnRushGrandStairHall/`의 FBX를 덮어쓴다.

## 4. 함정(겪은 것)

- **월드 좌표를 재질에 바로 넣지 않는다.** 구역 · 구간은 지은 뒤 옮겨진다. 돔 중심 · 나선 문턱처럼 위치가 필요한 셰이더 값은 `HallAnchoredMaterials`로 넣는다(옮겨지면 다시 씀).
- **모델에 UV가 없는 것이 많다**(홀 · 입구 · 난간). 그래서 대리석 · 돌 · 나무는 월드 좌표 셰이더다.
- **`MeshCutOut`은 Play 때만 자른다**(계단참 난간, 단상 탑 기둥). 플레이어 빌드에서는 FBX의 Read/Write가 켜져 있어야 한다.
- **메시를 따로 만든 `.meta`는 설정을 확인한다**(PNG가 큐브맵으로 들어온 적 있음).

## 5. 확인 상태

전부 **Unity 미확인**(코드 컴파일만). 확인 목록은 [VALIDATION](../Network/VALIDATION.md)의 R309 표.

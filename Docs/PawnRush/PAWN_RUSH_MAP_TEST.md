# 폰 러쉬 맵 시험 씬 (R283, 2026-11-02)

> **main 병합(R507, 2026-11-08):** 이 문서는 `claude/nifty-tesla-8212va`(팀원 songjiseong)의 `Docs/KingRush/`에서 main의 `Docs/PawnRush/`로 옮겼다. 병합하며 바뀐 것: 메뉴(**ChessFight → Pawn Rush → Open Map Test**, 맵 만들기 도구는 **Development → Map Authoring**), 코드 `Assets/Scripts/PawnRush/CarriageYard/` → `Assets/Scripts/PawnRush/`, 장애물 프리팹 `Assets/Prefabs/Obstacles/RoyalCarriageYard/` → `Assets/Prefabs/Obstacles/`(폴더 규칙 T7, 메타 · GUID 그대로). **이 문서의 R283~R288은 그 브랜치의 번호**로, main의 같은 번호(seoungtest 정원 작업 · 네트워크 R306 등)와 다른 요청이다. 왕실 마차뜰은 아직 7구간 판(`Open Section Test`)의 슬롯에 넣지 않았다([ROYAL_CARRIAGE_YARD](ROYAL_CARRIAGE_YARD.md) §12 결정 1).

앞으로 만들 **폰 러쉬 맵 프리팹**을 하나씩 열어, 자기 캐릭터(래그돌 폰) 하나로 직접 뛰어다니고 스킬을 써 보는 오프라인 작업 씬이다. `seoungtest`의 `SeoungTest_PawnRush`(코드로 지은 구역을 나란히 세움)와 따로, **프리팹 단위**로 맵을 시험한다. 코드 작성 · 리눅스 컴파일만 했고 **Unity · 사람 확인 전.**

## 1. 파일

| 무엇 | 경로 |
|---|---|
| 씬 | `Assets/Scenes/PawnRush_MapTest.unity` (코스 01 씬의 카메라 · 조명 · 물리 설정(120 Hz) · 플레이테스트 폰만 가져옴, 코스는 없음) |
| **맵 프리팹 저장 폴더** | `Assets/Prefabs/PawnRushMaps/` (하위 폴더 가능, 규칙은 폴더의 `README.md`) |
| 씬 컴포넌트 | `Assets/Scripts/PawnRush/PawnRushMapTest.cs` (맵 목록 · 맵 짓기 · 시작점 · 키 · 도움말) |
| 기본 시험장 | `Assets/Scripts/PawnRush/PawnRushCalibrationYard.cs` (코드로 짓는 치수 측정장) |
| 에디터 메뉴 | `Assets/Scripts/PawnRushEditor/PawnRushMapTestMenu.cs`(R507부터 Pawn Rush · Development → Map Authoring 묶음) |

## 2. 캐릭터 시스템 — 이 씬에서 무엇이 어떻게 움직이나

| 부분 | 무엇이 하는가 |
|---|---|
| 폰 생성 · 입력 | `PlaytestSpawner`가 Play 때 `RagdollPawn.prefab`(래그돌 폰, `RagdollDriver` = `ICharacterDriver`)을 시작점에 하나 만들고, 키보드 · 마우스를 `CharacterCommand`로 넘긴다. 카메라는 `OrbitCamera`(마우스 시점, 이동은 카메라 기준) |
| 폰 러쉬 움직임 | 씬이 `CourseSkills.PawnRush = true`를 켜면 `PawnRushCourseSkills`가 저절로 붙어 모든 폰에 **폰 러쉬 전용 물리**(`PawnRushPhysicsParams`: 점프 4.9 m/s, 공중 중력, 턱 올라서기 등)와 **기물 스킬**(`PawnRushSkillParams`, 기획 쿨타임)을 넣는다. 코스 01과 같다 |
| 스킬 | F = 스킬(룩 · 비숍은 F 조준 → 좌클릭), 오른쪽 아래 스킬 카드. 1~6으로 기물을 바꿔 각 기물 스킬을 시험 |
| 낙사 · 복귀 | 맵의 `KillVolume` · `WaterZone` · `Checkpoint`를 스포너가 그대로 처리(1.5초 뒤 마지막 체크포인트). y −40 아래로 떨어져도 복귀. 골반이 50 m/s를 넘어도(물리 엔진이 쳐 낸 폰) 곧바로 복귀(R531, 경기 방장도 같음) |
| 장애물 | 맵 안의 `Obstacle`(Spinner · Oscillator · Pendulum …)은 `ObstacleClock` 시간으로 움직이고, 위에 선 폰은 `IMovingSurface`로 같이 움직인다 |

## 3. 쓰는 법

1. 메뉴 **ChessFight → Pawn Rush → Open Map Test** → Play. 맨 처음엔 **기본 시험장**이 열린다.
2. 새 맵: **ChessFight → Development → Map Authoring → New Pawn Rush Map Prefab** → `PawnRushMaps/NewMap.prefab`(시작점 · 바닥 · 낙사 상자)이 생긴다. 프리팹을 열어 꾸민다.
3. Project 창에서 맵 프리팹 우클릭 → **ChessFight → Test In Pawn Rush Map Test**: 씬이 열리고 그 맵으로 Play. (메뉴 **Development → Map Authoring → Test Selected Pawn Rush Map**도 같음.) 폴더 밖 프리팹은 씬의 `maps` 목록에 넣고 시험한다.
4. 마지막에 연 맵을 기억해 다음 Play도 그 맵으로 시작한다(PlayerPrefs).
5. 씬이 깨지면 **Development → Map Authoring → Rebuild Pawn Rush Map Test Scene**: 코스 01 씬(`Assets/Prototype/PawnRush/Scenes/PawnRush_Course01.unity`, R497에 옮김)에서 다시 만든다.

### 키

| 키 | 하는 일 |
|---|---|
| WASD · Space · Shift · 좌클릭 · 우클릭 | 이동 · 점프 · 질주 · 다이빙 · 잡기(꾹) |
| F / 1~6 | 스킬 / 폰 · 나이트 · 비숍 · 룩 · 퀸 · 킹 |
| [ ] | 이전 / 다음 맵 |
| F5 | 지금 맵 다시 짓기 (Play 중 프리팹을 고친 뒤 바로 반영) |
| T | 카메라가 보는 바닥으로 폰 옮기기(자유 카메라에서도) |
| F1 / F2 | 자유 카메라 / 맵 전체 보기 |
| F3 | 느리게 1 → 0.5 → 0.25배 (장애물 타이밍 보기) |
| R / Backspace | 마지막 체크포인트 / 맵 처음부터 |
| H | 도움말 (왼쪽 위: 맵 목록 · 시작점 · 폰 위치 x y z · 속도) |

## 4. 맵 프리팹 규칙

폴더의 [README](../../Assets/Prefabs/PawnRushMaps/README.md)와 같다. 진행 +z, 원점 그대로 배치, 시작점은 `SpawnPoint`(가장 작은 index, 백팀 먼저) → `Start` 자식 → 남쪽 가장자리 바닥 순. 움직이는 장애물은 `Obstacle` 상속(절대 규칙 7). 맵마다 프리팹 하나라 여러 사람이 같은 씬을 고치지 않는다(절대 규칙 12).

## 5. 기본 시험장 (코드, 맵 목록 첫 번째)

폰이 실제로 어디까지 되는지 숫자로 보는 곳. 시작 (0, 0, 0), 북쪽으로 진행, 숫자 표지판이 서 있다.

| 곳 (x) | 무엇 |
|---|---|
| −22 ~ −14 | 턱 0.30 / 0.45 / 0.60 / 0.75 / 0.90 / 1.20 m |
| −11 ~ −5 | 1.5 m 높이 발판 사이 틈 1.0 / 1.5 / 2.0 / 2.5 / 3.0 / 4.0 m |
| −2 ~ 2 | 빈 직선(질주 · 다이빙) |
| 3 ~ 15.5 | 경사 10 / 20 / 30°, 길이 8 m |
| 18 ~ 22 | 오를 수 있는 벽 1 / 2 / 3 / 5 m, 오를 수 없는 대리석 벽 3 m |
| z 66 ~ 96 | 회전 막대(윗면 0.6 m, 60°/초), 좌우로 움직이는 발판(±5 m, 6초) |
| z 96 ~ 106 | 구덩이 위 폭 2 / 1 / 0.5 m 다리, 구덩이 `KillVolume`, 체크포인트 z 92 |

## 5-1. 폴더에 있는 맵

| 맵 | 문서 |
|---|---|
| `RoyalCarriageYard`(왕실 마차뜰, R284) · `RoyalCarriageYard_Obstacles`(그 장애물 시험장) | [ROYAL_CARRIAGE_YARD](ROYAL_CARRIAGE_YARD.md) |

## 6. 아직 안 된 것

- Unity에서 열어 본 적 없음: 씬 YAML은 손으로 썼다(코스 01 씬 블록 복사). 열리지 않거나 참조가 빠지면 **Rebuild Pawn Rush Map Test Scene**.
- 오프라인 한 명만. 온라인 · 봇 · 여러 폰 시험은 없다.
- 표지판은 영어 · 숫자만(내장 글꼴에 한글 없음).

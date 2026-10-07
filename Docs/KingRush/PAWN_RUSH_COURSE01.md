# 폰 러시 코스 01 「여덟 번째 랭크」 — 구현 (v0.2, R69)

2026-10-07. 기획: [레벨 디자인 v0.2](PAWN_RUSH_COURSE01_DESIGN_v0.2.md)(성한 님, 사용자 첨부 원문). 그 전 v0.1 구현(R68, 일자 코스)은 같은 씬에서 **v0.2로 바뀌었다**(사용자: "같은 씬에 업데이트"). v0.1 원문은 [PAWN_RUSH_COURSE01_DESIGN_v0.1.md](PAWN_RUSH_COURSE01_DESIGN_v0.1.md), v0.1 코드는 커밋 `c4015ff`.
**상태: Linux 컴파일·자동 검사와 파이썬 좌표 점검만. Unity에서 사람이 본 적 없음.** 확인 목록은 [VALIDATION](../Network/VALIDATION.md) 최상단.

## 1. 여는 법

1. Unity 메뉴 **ChessFight → Pawn Rush → Open Course01** (또는 `Assets/Scenes/PawnRush/PawnRush_Course01.unity`).
2. 그대로 **Play** → 씬에 코스가 없으면 코드가 기획서 월드 좌표대로 코스 전체를 만들고, 래그돌 폰(백팀)이 출발 광장(y 0)에 선다. 3초 카운트다운 뒤 출발 막대가 내려간다.
3. 코스를 씬에 실제 오브젝트로 두려면 Play를 멈추고 **ChessFight → Pawn Rush → Build Course01 v2**. `Course01v2_Root`를 지우고 다시 만든 뒤 씬을 저장하고 검증기를 돌린다(두 번 눌러도 같다). 이후 Play는 씬의 것을 그대로 쓴다. **루트 안을 손으로 고치면 다음 Build 때 사라진다.**

| 키 | 동작 |
|---|---|
| WASD·Space·Shift·좌클릭·우클릭·마우스 | 래그돌 기본 조작(오비트 카메라) |
| **F5** | 백팀 ↔ 흑팀(그 팀 출발 자리에서 다시 시작) |
| R / Backspace | 마지막 체크포인트로 / 처음부터(카운트다운 다시) |
| V | 자유 카메라 |

오른쪽 위에 구간(S+A, W1, B, W2, C, T)·랭크·진행 m. 섬에 들어가면 그림 카드 3초와 버튼 진척 막대. 구간 입장·퇴장은 `Logs/PawnRush/course01_날짜.csv`(열: 구간, enter/exit, 초, 팀).

## 2. 메뉴 (ChessFight → Pawn Rush)

| 메뉴 | 하는 일 |
|---|---|
| Open Course01 | 코스 씬을 열고 코스 루트 선택 |
| Build Course01 v2 | 코스 루트를 지우고 기획서 좌표로 다시 만듦(가져온 장애물은 프리팹 연결 유지) → 씬 저장 → 검증 |
| Validate Course01 | 검증기만 돌리고 결과 창 |
| Mirror White → Black | 선택한 `W1_White`/`W2_White`(또는 그 안의 것)로 흑팀 절반을 다시 만듦. 손으로 고친 것을 시험할 때용 |
| Mirror Selected (Left-Right Pair) | 선택한 것을 x = 0 기준으로 거울 복제, 왕복·흔들림 장애물은 위상 + 주기/2 |

## 3. 씬 구조

```text
Pawn Rush Course01 (PawnRushCourse, 월드 원점)
  Course01v2_Root
    S_Start         출발 광장, 출발 막대, Team_White/Team_Black 출발 자리
    A_Ground        A1 회전 막대, A2 둑·원판 구덩이·밀대, A3 기병 앞마당·성벽·테라스
    W1_White        팀 구간 1(서쪽 바깥 날개): 알코브, 연결 다리, 바람 다리, 5랭크 단, 섬 ①, 출구 다리, 팀 장벽 2, 팀 구역, CP3·CP4
    W1_Black        W1_White의 거울(x 반전)
    B_Middle        B0 합류(출발 광장 지붕), B1 원형 회전판, B2 진자 통로·성벽 들보, B3 갈림
    W2_White        팀 구간 2(안쪽 날개): 알코브, 모퉁이, 레인, 6랭크 단, 섬 ②, 출구 다리, 팀 장벽 2, 팀 구역, CP8·CP9
    W2_Black        거울
    C_Upper         C0 합류, 시계 문, C1 룩 벽 광장, C2 탑 앞마당
    T_Tower         Body, NorthFace(직등면·진자), Ramp_West, Ramp_East(거울), Top(결승 원·왕관)
    Checkpoints     공용 CP0·1·2·5·6·7·10·11 (팀 체크포인트는 각 날개 안)
    KillVolumes     y −10 낙사 영역
    ProgressPath    P0~P32(백팀), 흑팀은 x 반전
```

## 4. 코드

| 파일 | 내용 |
|---|---|
| `Course01v2Builder.cs` | 루트 만들기, 장치(`Device_SpinBar`·`RookWall`·`RotatingBoardRound`·`PawnStatue`), 기병·점핑 발판·원판 도우미, 진자 틀, 팀 장벽·아치, **미니게임 섬**(진행 방향으로 돌려 놓음, 착지대가 옆으로 열리는 출구 지원) |
| `Course01v2Builder.Sections.cs` | 기획서 6절 표를 줄마다 옮긴 S·A·W1·B·W2·C·T, 체크포인트, 진행 경로 점 |
| `PawnRushCourse.cs` | 코스 루트(키트, Play 때 비어 있으면 만들기), 진행 m·구간, 높이로 랭크 |
| `ProgressPath.cs` | 경로 점(백팀)과 구간, 흑팀 거울, 진행 m |
| `CourseFloor.cs` | 걸어 다니는 윗면 표시(검증기의 층 간격·팀 구역 검사용) |
| `Course01Validator.cs` | v0.2 검사(아래 5절) |
| `TeamMirror.cs` | 거울 기준 = 월드 x = 0. `MirrorTeam`(W*_White → W*_Black), `MirrorGroup`(탑 동쪽 경사로) |
| `Gameplay/Course/FallDistanceRespawn.cs` | **낙차 8 m 복귀(신규)**. 마지막으로 선 바닥(골반 아래 0.9 m 안의 윗면)보다 8 m 넘게 내려가면 보고 → `PlaytestSpawner`가 마지막 체크포인트로. 벽을 오르는 동안은 바닥이 갱신되지 않음. 플레이테스트가 폰에 붙인다 |
| `Gameplay/Course/TeamZone.cs` | **팀 구역 볼륨(신규)**. 다른 팀 폰이 들어오면 즉시 복귀 |
| `Gameplay/Course/FinishZone.cs` | 아무 모양의 트리거 + "골반만" 옵션(탑 꼭대기 지름 6 m 원기둥) |
| v0.1에서 그대로 | `NoClimbSurface`(등반·잡기 불가), `KillVolume`, `TeamBarrier`, `CourseCheckpoint`(기존 `Checkpoint` 위), `MiniGameSlot`·`SlotDoor`·`MiniGameBase`·`MiniGamePlaceholder`, `PromotionZone`, `NoGrabZone`, `RankMarker`, `StartBar`, `ObstaclePhase`(초 단위 위상), 재질·셰이더, `Course01Kit` |
| 지운 것 | v0.1의 모듈 이어 붙이기: `Course01Modules*`, `Course01Layout`(+에셋), `CourseModule`, `Course01ModuleBuilder`(모듈 프리팹 굽기) |

## 5. 검증기 (Validate Course01)

1. 위아래로 겹친 두 바닥(`CourseFloor`)의 윗면 차 ≥ 9 m. 경사로가 낀 쌍은 오류가 아니라 메모로만 보고(아래 B2 경사로).
2. 팀 구역이 공용 바닥·경사로와 겹치지 않음.
3. 백·흑 진행 경로 길이 차 ≤ 0.1 m.
4. 필수 경로 틈 ≤ 2.5 m(아래 4 m 안에 바닥이 없는 구간).
5. 체크포인트 부활 자리 6곳 아래 바닥.
6. 슬롯 문 = 출구 높이, 슬롯마다 미니게임, 낙사 영역 있음.

## 6. 기획서와 다르게 한 곳

| 곳 | 기획서 | 구현 | 이유 |
|---|---|---|---|
| 씬 | 새 씬 `PawnRush_Course01_v2.unity`, v0.1 씬은 둠 | **같은 씬을 v0.2로** | 사용자 요청 |
| 탑 북벽 쉬는 턱 | 면 z 88에 z 88~88.8로 튀어나온 턱 2개 | 면이 0.8 m씩 물러나는 계단: z 88(y 24~28) → 87.2(28~32) → 86.4(32~36) | 튀어나온 턱은 오르는 몸이 턱 밑에 걸린다(v0.1 M14와 R56에서 쓴 방식). 턱 깊이 0.8 m·높이 28/32는 같음 |
| 탑 북벽 진자 | 축 z 89.6, 다리 x ±7 | 축 z 88.6, 틀은 탑 꼭대기(기둥 x ±9.5) | 계단식 면에서 공(지름 2.2)이 y 30~32를 오르는 폰에 닿으려면 1 m 가까워야 한다. 공이 축에서 6 m까지 와서 x ±7 다리는 공에 맞는다 |
| B2 진자 기둥 | x ±6, 아래에서 올라옴 | x ±7, y 11부터 | 아래는 공용 A의 원판 구덩이라 기둥이 원판을 뚫는다. ±6이면 공에 맞는다 |
| B1 회전판 | (받침 언급 없음) | 받침 없음 | 바로 아래가 05 승강 칸 구덩이 |
| 기병 K1 | 착지 (−3, 3, 42), 출발대는 프리팹 위치 | 출발점을 테라스 (−7, 6, 48)로 옮김(K2의 거울). 도약 높이 4 → 2 m (두 마리) | 프리팹 출발점은 원판 구덩이 위(z 39)라 받침이 원판과 겹친다. 4 m 도약은 위층 판(y 11) 밑까지 올라간다 |
| 탑 경사로 해머 | 받침은 바깥 난간 쪽, 머리가 경사로 폭을 가로질러 | 받침은 난간 쪽(Y −90°), 머리는 경사로를 따라 돈다 | 프리팹은 받침 앞에서 한 평면으로 돈다. 받침을 난간 쪽에 두면 도는 면이 경사로 방향이 된다 |
| 낙차 복귀 시간 | 1.5초 뒤 부활 | **8 m를 넘는 순간 바로**(설정값 `respawnDelay`, 기본 0) | 9 m 아래층까지 0.07초밖에 안 남아 1.5초를 기다리면 아래층에 내려앉는다 |
| 팀 체크포인트·장벽·구역 | `Checkpoints`, `TeamBarriers`, `TeamZones` 묶음 | 각 날개(`W*_White`) 안에 두고 거울로 흑팀 것을 만듦 | 거울 한 번으로 흑팀 것이 같이 생긴다 |
| 왕관 장식 | 왕관 자리 위 | 결승 원 3 m 위에 떠 있음 | 결승 원 안에 서는 폰과 겹치지 않게 |
| 경로 길이 | 약 460 m | 경로 점 P0~P32를 이으면 486.7 m | 점은 기획서 그대로. 표의 거리 합과 점의 길이가 다르다 |

## 7. 사람이 결정·확인할 것

- **B2 경사로(z 41~50)가 테라스(y 6) 위를 7.7~9 m 높이로 지난다**(z 47~50). 9 m 규칙보다 낮다. 경사로 아래 판 두께 1 m라 테라스 폰 머리 위로 약 6.7 m 여유. 검증기가 메모로 알린다.
- 기획서 "결정할 것" 5개: 판 목표 시간, 낙차 기준값(8 m), 결승 왕관 원, 슬롯 문 위치, 프로모션 ③ 위치.
- 하지 않은 것: 미니게임 A~E, 프로모션·결승·과반 판정 규칙, 부활 2초 면역, 온라인, 장애물 힘의 방장 판정.

## 8. 자동으로 확인한 것과 못 한 것

- `Tools/run-tests-linux.sh --compile`: 런타임·에디터 코드 Roslyn 컴파일(에디터는 `UnityEditor.dll` 2021.1), Core 83·세션 39, 경계·장애물 시계 검사 통과.
- 파이썬으로 빌더의 바닥 좌표를 읽어 점검: 체크포인트 16개(공용 8 + 팀 4 × 2)의 부활 자리 96곳 아래 바닥, 팀 구역과 공용 바닥 안 겹침, 경사로 모두 19.5° 이하, 경로 틈은 05 승강 칸 자리(장애물이라 파이썬 모델에 없음) 말고는 없음.
- **못 한 것: Unity 실행.** 래그돌 완주, 낙차 복귀, 팀 구역, 탑 두 길, 셰이더는 사람이 Unity에서 봐야 한다.

# 킹러시

## 최신: R56 왕의 계단과 결승

사용자는 직접 테스트를 미루고 다음 단계 진행을 요청했다. 같은 씬에 **왕의 계단→4자리 승격/10초 집결→체크메이트 결승**을 추가했다. 외나무/긴 계단, 돌아오는 칸, 거대한 손, 왕좌 누적8초, 시간별 판 붕괴와 재진입/승패까지 연결했다. **F8** 계단, **Shift+F8** 결승. [규칙·범위·자동 검사](FINAL_COURSE.md). R55/R56 사용자 확인 대기. 아래 과거의 세 번째 코스/결승 미구현 기록은 이 범위로 대체하며, 온라인·남은 능력·최종 아트는 미구현이다.

## 최신: R55 집결 진행과 두 번째 코스

**승격 자리 결정→10초→미도착 양 팀 자동 집결→벽 개방**으로 수정하고, 성벽 갈림길/시소 체스판 미션을 이어 붙인다. [R55 규칙·단축키·범위](CASTLE_COURSE.md). F7 정상 집결 시험, F5 성벽, Shift+F5 시소. R54는 사용자 일반 피드백 수신, R55는 실기 확인 대기. 아래 R54 당시의 “다음 코스 없음”은 R55로 대체된다.

## 최신: R54 맵·미션 우선

사용자가 능력 추가를 잠시 멈추고 **맵(미션·코스)부터** 만들도록 변경했다. **장난감 상자 → 승격 발판 → 잡은 말 상자 미션 → 다리·팀 출구**의 첫 연결 코스를 추가했다. Unity **ChessFight → King Rush → Open Opening Course**. **F4**로 미션과 정지 더미를 바로 시험한다. [실행·규칙·구간 편집·검사](OPENING_COURSE.md).

새 씬 `KingRushOpening.unity`, 구간 프리팹11개. R54 사용자 확인 대기. 아래 R52/R53은 보존된 **능력 시험장**의 기록이며 “능력 전부 다음 미션”이라는 예전 순서는 R54로 대체한다. 전체3막/결승·온라인 연결·나머지 능력은 아직 없다.

## R52 첫 래그돌 시험장 (`JY-kingrush`)

2026-09-27. **R52 첫 시험장은 사용자가 “잘 작동해”라고 확인**했다(승격·열린 문 화면 첨부). 개별 검사 전부나 Steam 성공으로 확대하지 않는다. 새 기획은 [DESIGN](DESIGN.md), 개발 목록은 [MECHANICS_TODO](MECHANICS_TODO.md). 확정 구조/능력과 미션·맵의 1차 안을 구분한다.

사용자 정정: **`JY-gpt_gamemode@852eb2b` → 별도 `JY-kingrush`**. Claude 최신 미커밋 변경과 소드파이트 원본 채팅은 수정하지 않았다. 원문 문서가 전제한 QotH M6/M11/M13은 이 기준에 없다. 공통 기물표 대신 `KingRushPiece`/모드 전용 컴포넌트를 사용한다.

### 실행

1. Unity Hub에서 **`C:/Users/trews/.codex/worktrees/jy-kingrush/ChessFight`**를 연다(Unity 6000.3.11f1).
2. **ChessFight → King Rush → Open Mechanics Test** → Play. 또는 `Assets/Scenes/KingRushPrototype.unity`를 연다. Steam 필요 없음.
3. 나머지 11명은 입력 없는 물리 더미. **Tab** 조종 대상 변경, **F2** 백팀 킹 선택. 팀당 마지막 슬롯을 킹으로 정한 것은 시험용이며 최종 선정 규칙이 아니다.
4. 정면 노란 발판(나이트/비숍)에 **폰 하나만 1.5초** 서면 승격. 두 폰이면 멈춤, 진행하던 폰이 내려가면 초기화. 킹·승격 기물은 세지 않는다. 발판은 한 번만 쓴다.
5. **F4**: 초기화 후 파란 구역에 **킹+아군 더미2+적군 더미3** 배치. **E** 체크!: 0.5초 원형 예고 → 반경4m, 수평6+위1m/s, 아군 포함 피격, 쿨타임12초. 룩은 아래 R53 시험, 나머지 네 능력은 미구현으로 안내.
6. **F7 백팀 / F8 흑팀 완료**는 실제 미션 대신 쓰는 디버그 버튼. 자기 팀 문은 즉시, 상대 문은 첫 완료+20초(자기 완료가 더 빠르면 그때). 열린 자기 팀 문을 지나면 승격자는 폰 복귀, 고정 킹은 유지.
7. **R** 현재 구간 입구 부활(승격 유지), 물은 기존 5초 둥둥 후 부활. **F3** 전체 초기화. **Esc** 안내/커서, **Esc 후 Backspace** 로비. Esc 중에도 물리/시간은 계속 흐른다.

WASD·Shift·Space·좌클릭 태클·우클릭 잡기는 기존 그대로. E/Q는 모드가 가로채 갈고리로 바뀌지 않는다. 검 없음. 모든 기물은 동일한 폰 질량/이동/등반/질주/피격 설정을 쓴다.

Windows 빌드: `Builds/KingRushPrototype/ChessFight.exe`. **새 시험장은 아직 로비 선택에 연결하지 않았다.** 로비 KingRush는 기존 캡슐 코스다. 기존 소드파이트·로비 연결은 기반 브랜치 그대로 보존했다.

### R53 룩 인간 대포 — 사용자 확인 대기

Play를 멈춰 컴파일한 뒤 다시 Play한다. **F5**는 룩+적군 더미, **Shift+F5**는 룩+아군 더미를 긴 파란 시험 레인에 배치한다. 필요하면 조금 다가가 **우클릭을 계속 눌러 실제로 잡고 E**를 누른다.

- 0.6초 조준 뒤 발사. 마우스로 방향을 바꾸며, 아래를 볼수록 가까이(4~20m) 날린다. 청록색 예상 궤적/착지 원을 표시한다. 땅을 찾지 못하면 빨강으로 바뀌고 발사하지 않는다.
- 조준 중 우클릭을 놓거나 잡기가 풀리거나 피격/구역 이탈하면 취소. 상대가 쌓아 둔 버둥 게이지는 초기화하지 않는다.
- 아군은 발부터 착지, 적은 실제 착지 후 1초 누움. 발사 성공 뒤 8초 쿨타임. 날아가는 동안 미션 몸 수에서 제외한다.
- 발사자 팔에 걸리지 않도록 발사자/대상 사이 충돌만 0.2초 유예하고 기존 충돌 설정을 복구한다. 벽/다른 몸은 계속 충돌한다. 비행 중 이동·점프·태클 입력은 잠시 차단하며 착지/중단 뒤 정상 복구한다.
- 궤적은 출발/착지점 중 높은 쪽 위로 3m 솟는 포물선이다. 벽을 피하는 자동 경로가 아니며 장애물 충돌은 그대로 남는다.
- 룩 자세는 가벼운 상체 숙임까지만. 성곽 머리/포구 아트, 달라붙은 폰 떼기(폰 능력), 온라인 예고 동기화는 후속이다. 아직 전체 K3 완료가 아니다.

기본 조작·공유 튜닝·소드파이트는 보존한다. `RagdollPawn.KingRush.cs`는 이 모드 대포에서만 호출하는 발사 보조 진입점이며 일반 도약대는 사용하지 않는다.

개발 검사에서 걷기 앵커가 대포의 선형 속도를 빼앗는 문제와, 이전 물리 상태에 따라 발사자의 팔에 걸리는 문제를 발견했다. 비행 중 선형 드라이브 해제(자세 균형은 유지)·위 0.2초 발사자 충돌 유예로 처리한다. 기존 공유 `RagdollPawn.cs`를 고치지 않고 모드 전용 partial 진입점/후순위 FixedUpdate에서만 적용하며, 다음 정상 스텝이 기존 드라이브를 복구한다. 검사 순서를 바꾼 단독/전체 실행으로 회귀를 확인한다.

### 구현 범위와 남은 일

| 항목 | 이번 범위 | 다음 단계 |
|---|---|---|
| K1 동일 몸 | 기물 정체성만 관리, 공유 튜닝/질량 무변경 | 기물 외형·달리기/밀림 계측 |
| K0 규칙 | 파랑 완료 시각/20초 문, 9개 발판 일정·독점, 문/승격만 `KR1` Encode/Apply(원자적 검증·과거 상태 무시) | 미션 득점/연장, T0·왕좌/게이지·승패, Steam 연결 |
| K5 승격/출구 | 첫 발판2개, 중복 신체 제거, 쟁탈 정지/이탈 초기화, 기물 유지 부활·고정 킹 예외 | 후속 구간3/4개 배치, 홀로그램 아트 |
| K2 능력 구역 | 몸 중심 출입/취소, 모드 로컬 E 누름/유지, 쿨타임 링/HUD | 공통 CharacterCommand/패킷 AbilityHeld는 **아직 안 바꿈**. 상태 효과는 능력별 추가 |
| K3a 체크! 1차 | 예고원·아군 포함 피격·기존 잡기 해제·12초 대기 | 홀 드는 자세·3m 이동/착지 뒤1초 계측·새 구속/왕좌 연동. **전체 완료 아님** |
| K4 몸 세기 기반 | 경기 명단 몸당1회, 물/잡힘/발사 제외, 파랑 몸 수 표시 | 승천/여왕의 손/매달림/탑승·운반자 위치·지면 정보 |
| K3b 인간 대포 1차 | 실제 잡기 기반 조준·포물선·착지 구분·취소/쿨타임 | 외형·매달림 연동·사용자 손맛/온라인 |
| K6~K12 | R54 K6 첫 상자 미션·첫 빨강 코스 부분 구현 | 첫 연결 코스 사용자 확인 → 후속 맵/미션. 능력4종·결승·온라인/봇은 후속 |

구간은 사람별이며 앞선 사람이 뒤쪽 사람의 능력을 켜지 않는다. 이 **기능 시험장**은 빨강1→파랑1→빨강2만 있다. 별도 R54 연결 코스에는 개인 체크포인트/상자 미션이 있다. 전체 경기·결승은 아직 없다. 이 시험장의 F4/킹 배정/문 완료 키는 본 게임 규칙이 아니다.

### 코드·검사

- `Core/KingRushRules.cs`, `KingRushPieces.cs`: Unity 없는 규칙. KR1은 문/승격만 다루는 기반이며 전체 경기 전송 규약이 아니다.
- `Gameplay/KingRush/`: 명단/시간·능력/몸 계약·승격 발판/구역/문. 래그돌·Steam 참조 없음.
- `RagdollLab/Scripts/KingRushPawn.cs` / `.Cannon.cs`: 공유 폰 옆의 모드 상태/입력/체크!/대포. `RagdollPawn.KingRush.cs`에 모드 전용 발사 보조. 공유 카메라/튜닝 무변경.
- `KingRushPrototype.cs`: 시험장/물 부활/더미. 물 콜백은 예약만, Update에서 부활.
- `KingRushBuilder`: 새 씬이 없을 때만 생성. 기존 씬 재생성 안 함. 별도 시험 빌드의 첫 씬은 시험장이며 일반 배포 목록은 안 바꿈.
- `KingRushAutoTest`: 실제 플레이어 자동 검사. 사용자 손맛/두 PC 시험을 대신하지 않음.

```powershell
./Tools/Test-NetworkCore.ps1
./Tools/Test-NetworkCompile.ps1 -SteamRuntimeSources <Steamworks Runtime 폴더>
Unity.exe -batchmode -nographics -projectPath <이 폴더> -executeMethod ChessFight.RagdollLab.Editor.KingRushBuilder.BuildBatch -logFile <로그>
Builds/KingRushPrototype/ChessFight.exe -kingRushTest -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile <로그>
```

**R53 자동 기록:** Core46·모의 세션20·실제 Unity DLL 전 어셈블리 컴파일·참조 경계 통과. 최종 Windows 빌드(`Logs/kingrush-r53-build.log`) 전체 검사 **80/0**(1600×900, `Logs/kingrush-r53-full.log`). 5/10/20m 예상점 대비 착지 오차 약 **0.07/0.16/0.36m**. 실제 잡기·0.6초 예고·버둥 게이지/탈출·방향 전환·재누름·실제 잡은 상태 쿨타임 차단·피격/부활/구역 취소·지면 없음·벽 충돌·쓰러진 아군 착지·이동 복구·발사자 충돌 유예/복구를 포함한다. 앞선 빌드의 전체79/0, 대포 단독28/0은 중간 기록이다. R53 사용자 손맛/Steam 확인은 아니다.

**R52 자동 기록:** 첫 Windows 플레이어29/0. 당시 최종 빌드(`kingrush-build-2.log`)는 **33/0을 두 번** 통과했다(`kingrush-test-2.log`:1600×900, `kingrush-test-3.log`:1280×720). 실제 문을 걸어 통과·F4 더미 배치·킹 발판 제외·파랑 E/Q 갈고리 격리를 추가 검사했다. 기존 `RagdollPawn.cs`/카메라/튜닝/소드파이트·로비 씬은 여전히 시작 커밋과 차이 없음(대포 보조는 새 partial 파일). `Logs/kingrush-*.log`는 Git 제외. 렌더: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/ChessFighter/KingRushTest`의 overview/hud/promoted/check-warning/cannon-aim.png. AI가 렌더를 확인했다. [사람 확인 R52/R53](../Network/VALIDATION.md).

**순서 변경 재실행:** 같은 R53 최종 빌드에 `-kingRushTest -kingRushCannonOnly`를 주면 앞선 R52 시험 없이 대포부터 검사한다. **47/0**(1280×720, `Logs/kingrush-r53-cannon.log`). 전체 실행과 단독 실행 모두 통과했다.

빌드와 DLL 검사는 **같은 프로젝트에서 동시에 돌리지 않는다**. Unity 종료가 Temp를 지워 검사 중 DLL이 사라질 수 있다. R53은 별도 임시 사본에서 빌드해 사용자가 연 에디터와 분리했다. 기존 공유 URP missing-script/convex-mesh 경고는 별도 자산 문제. 새 Steam 프로토콜 변경 없음(v10/CFS3/CFR4).

---

아래는 **기존 캡슐 코스** 설명이다. 새 시험장과 구분한다.

첫 미니게임. 폴가이즈식 장애물 레이스. 담당: 진호·지성(맵·장애물). 사용자 요구(R11): 킹러시 맵을 구현할 씬. 팀원용 설명은 [팀 가이드 6장](../TEAM_GUIDE_KO.md)(같은 내용).

| 문서 | 내용 |
|---|---|
| [MAP_IMPORT.md](MAP_IMPORT.md) | R47 별도 프로젝트 맵·19 장애물 이식: 열기, 기존 래그돌 연결, 검사와 미연결 범위 |
| 이 문서 | 씬 구성, 코스 컴포넌트, 오프라인 플레이테스트, 네트워크 한계 |
| [OBSTACLES.md](OBSTACLES.md) | 장애물 규칙, 기존 장애물, 새 장애물 만드는 법 |
| [OBSTACLE_TEMPLATE.md](OBSTACLE_TEMPLATE.md) | 장애물 기획서 양식. 완성본은 `Docs/KingRush/Obstacles/이름.md` |

## 1. `Assets/Scenes/KingRush.unity` 현재 배치 (자리만 잡은 뼈대)

```text
z = -20 ~ 20   Start Platform   40 × 40   스폰 12곳 (청 6 / 주황 6, y = 0)
z =  20 ~ 200  Runway           폭 16
                 z=50   Spinner A      회전봉 (점프로 넘기)
                 z=80   Checkpoint 1
                 z=100  Sliding Wall   좌우로 미끄러지는 벽
                 z=130  Pendulum       진자
                 z=150  Checkpoint 2
                 z=180  Spinner B      반대로 도는 회전봉
z = 200 ~ 230  Finish Platform  Finish Zone
```

크기·배치·장애물은 기획대로 바꿔도 된다. **남겨 둘 것:** 스폰 지점, 체크포인트, 골인 컴포넌트, `Playtest`, `Physics`(`PhysicsProfile` 120Hz), `ChessFight Game Root`(네트워크 경기 화면용).

씬 파일은 `Tools/Generators/gen_scenes.py`로 처음 만들었다. **Unity에서 저장한 뒤에는 생성기를 다시 돌리지 않는다.**

## 2. 코스 컴포넌트 (`Scripts/Gameplay/Course/`)

| 컴포넌트 | 역할 |
|---|---|
| `SpawnPoint` | 입장 위치. **바닥(발 닿는 곳)**에 둔다. index 0은 오프라인 스폰, 나머지는 경기 때 roster slot으로 배정할 예정 |
| `Checkpoint` | 트리거. 지나가면 리스폰 지점이 된다. 더 높은 `Order`만 갱신. 정적 이벤트 `Checkpoint.Reached` |
| `FinishZone` | 골인. 도착만 알린다(`FinishZone.Reached`). 점수·라운드 종료는 모드 규칙 몫 |
| `PhysicsProfile` | 씬 동안만 물리 120Hz / 솔버 24회, 나갈 때 복구 |

## 3. 오프라인 플레이테스트

KingRush를 직접 열고 Play → Steam 없이 캐릭터 하나. WASD, Space, 왼쪽 클릭(밀치기), 오른쪽 누르기(잡기), R(체크포인트로), Backspace(처음부터). 떨어지면(`fallLimit`) 자동 리스폰, 골인하면 기록 표시.

지금 캐릭터는 **임시 CharacterController 캡슐**(`Prefabs/Characters/PlaytestCharacter`)이다. 래그돌이 병합되었으므로(2026-09-25) `Playtest`의 `Character Prefab`을 `Assets/ChessFight/RagdollLab/Prefabs/RagdollPawn.prefab`으로 바꾸면 래그돌로 달릴 수 있다(**Unity 미확인**, [Player/RAGDOLL §4](../Player/RAGDOLL.md)).

## 4. 네트워크 경기에서의 한계

- 경기가 시작되면 모두 KingRush로 넘어가고 장애물은 **모든 PC에서 같은 위치**로 움직인다(공유 시계).
- 하지만 캐릭터는 아직 로비용 평면 모터(38×38, 충돌 없음)라 **스타트 플랫폼 위에만 서 있고 코스를 달릴 수 없다.**
- 체크포인트·골인은 로컬 이벤트다. 호스트 판정·순서 검증은 없다.
- 해결 순서: ~~래그돌 병합~~(09-25 완료) → 래그돌 권한 결정 → 호스트 시뮬레이션 → 체크포인트·골인 호스트 판정 → 라운드 규칙.

## 5. 협업 규칙

- `KingRush.unity`는 **한 번에 한 명만** 편집한다(누가 잡고 있는지 공유).
- 맵은 **구간 프리팹**(`Prefabs/KingRush/Section_01.prefab` 등)으로 만들고 씬에는 배치만 한다.
- 장애물마다 기획서를 쓴다([양식](OBSTACLE_TEMPLATE.md)).

## 6. 진호·지성 님 AI 시작 프롬프트

```text
ChessFight 저장소 main에서 feature 브랜치를 만들어 킹러시 맵과 장애물을 작업합니다.
저장소 루트 HANDOFF.md를 먼저 읽고, Docs/KingRush/README.md와 OBSTACLES.md를 읽으세요.
Assets/Scenes/KingRush.unity가 작업 씬입니다. 직접 Play하면 오프라인 플레이테스트가 됩니다.
장애물은 Assets/Scripts/Gameplay/Obstacles/Obstacle을 상속하고 Evaluate만 구현합니다.
반드시 지킬 것: 위치·회전은 ObstacleClock 시간의 순수 함수로만 계산하고 프레임마다 누적하지 않습니다.
각 장애물은 Docs/KingRush/OBSTACLE_TEMPLATE.md 양식으로 기획서를 함께 만듭니다.
맵 구간은 프리팹으로 나눠 두 사람이 같은 씬 파일을 동시에 고치지 않게 합니다.
```

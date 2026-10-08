# 씬 구성과 흐름

**R54 별도 첫 연결 코스:** `KingRushOpening.unity`, 메뉴 **ChessFight → King Rush → Open Opening Course**. 오프라인12명, 장난감 코스·상자 미션·출구. 구간 프리팹은 `Assets/Prefabs/KingRushOpening`. `KingRushOpeningBuilder` 전용 빌드에서만 첫 씬이고 일반 로비/배포 라우팅은 바꾸지 않는다. 기존 `KingRushPrototype` 능력 시험장도 보존. [실행](../KingRush/OPENING_COURSE.md).

사용자 요구(R11): 씬을 **인트로 - 로비 - 게임 씬(킹러시, 승격쟁탈전 등)**으로 나누고, 킹러시 맵 작업 씬과 래그돌 테스트 씬을 만든다. 구현 `72ddf9e`. R19(2026-09-25): 경기 씬은 **경기 방의 게임 모드**가 정한다([GameModes](../GameModes/README.md)). **Unity에서 아직 열지 않았다**(확인 목록: [VALIDATION](../Network/VALIDATION.md) 최상단).

## 1. 흐름

```text
 (R84) 게임 실행 → [시작 로고 영상, 인트로 위] → Intro ──시작 판 클릭──▶ (처음 한 번: 이름 설정 화면, Intro 씬 안) ──[장면 전환]──▶ Lobby
 Intro ──시작 판 클릭──▶ Lobby ──모두가 phase=playing──▶ [로딩 화면] ──모두 준비, 공유 시각 go──▶ 모드 씬 ──경기 끝(Match==0) / Esc──▶ Lobby
 (타이틀)          (파티·모드·매칭)                  킹 러시 = KingRush
                                                   소드 파이트 = SwordFight / 퀸 오브 더 힐 = 준비 중
 RagdollTest ← 개발 전용. 흐름 밖.
```

| 씬 | 빌드 순서 | 붙는 컨트롤러 | Play를 누르면 |
|---|---|---|---|
| `Intro.unity` | 0 | `IntroController` | 온라인. (R84) 시작 로고 영상 → 타이틀, Steam 시작, 시작 판 클릭 → 처음이면 이름 설정 화면(같은 씬의 `NameScreen`), (R85) "이름 바꾸기" → 이름 바꾸기 화면 → 다시 타이틀 → 장면 전환(`SceneTransition`) 뒤 Lobby(비동기 로드), Esc 설정 창 |
| `Lobby.unity` (구 ChessFightLab, GUID 동일) | 1 | `LobbyBootstrap` (구 GameBootstrap) | 온라인. 파티 라인업(3D), 모드·파티·매칭 HUD. 이동 없음 |
| `KingRush.unity` | 2 | 경기로 들어왔을 때만 `MatchSceneView` | 직접 열면 **오프라인 플레이테스트** |
| `SwordFight.unity` | 3 | `SwordFightGame`, 온라인은 `SteamSwordFightLink` | 직접 열면 폰 2v2(나+봇 3), 온라인은 로비 명단 |
| `RagdollTest.unity` | 비활성 | 없음 (씬 안의 `LabGame`이 동작) | 래그돌 랩. Steam 없이 2인 로컬 |
| `QueenOfTheHill.unity` (R48, 맵 7차 R49) | 빌드 제외(아직) | 없음 | 퀸 오브 더 힐 그레이박스. `QueenHillLevel`이 맵 데이터(JSON)로 맵을 만들고 래그돌 폰 1인 오프라인 |
| `PawnRushVictory.unity`, `PawnRushLose.unity` (R76, 폰 러쉬 결과 화면) | 빌드 제외(아직 흐름에 없음) | 씬에 저장된 `PawnRushResultDirector`(Steam 모름, `victory`만 다름) | 결과 화면 미리보기. 탁자 체스판·말·결승 중계 결과판을 코드로 만들고 예시 경기를 보여 줌(R 다시 재생, H 결과판). R62의 `LastScene.unity`는 지움 |
| `SampleScene.unity` | 빌드 제외 | 없음 | 아무것도 안 함(템플릿) |

씬 이름 상수는 `Game/SceneNames.cs`. **런타임에 로드하는 씬은 빌드 목록(`EditorBuildSettings.asset`)에도 있어야 한다.** 빌드 메뉴(`NetworkSetup.ShippedScenes`)가 Intro·Lobby·KingRush·SwordFight를 넣는다.

R47: `GameSceneConfig.customMatchSimulation=true`인 SwordFight에서는 캡슐 `MatchSceneView`와 `Motion.Update`를 실행하지 않는다. 별도 래그돌-네트워크 다리가 **기존 NetworkRuntime.Session을 빌려** 실행하며 독립적인 Steam 초기화/종료는 하지 않는다. 직접 열어 Play하면 Steam 없이 연습한다. 공유 캐릭터 물리와 다른 씬은 그대로다.

## 2. NetworkRuntime — 씬을 넘어 사는 유일한 Steam 소유자

**R52 별도 시험 씬:** `KingRushPrototype.unity`는 `KingRushBuilder` 전용 빌드에서만 첫 씬이며 일반 `EditorBuildSettings`/GameModes 라우팅은 바꾸지 않는다. `PhysicsProfile`, `KingRushPrototype`, `LabCamera`로 오프라인 12명을 만든다. `GameSceneConfig`/Steam 컨트롤러가 없고, 기존 `KingRush.unity`·`SwordFight.unity`는 수정하지 않는다. [실행](../KingRush/README.md).

`Scripts/Bootstrap/NetworkRuntime.cs`

- `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`에서 **첫 씬이 Intro나 Lobby일 때만** 만들어진다. `DontDestroyOnLoad`.
- 가진 것: `SteamSession`, `SteamMotion`, 입력 소스(`Controls`), `MovementGate`(씬이 등록하는 이동 허용 조건).
- 매 프레임: `Session.Tick()` → 입력 읽기(게이트 적용) → `Motion.Update()` → F8 지연 시뮬레이터 키(개발 빌드) → `FollowMatch()`.
- **(R58, 09-30) 로비 → 경기 씬은 `MatchLoader`가 로딩 화면 뒤에서 `LoadSceneAsync`로 불러온다.** 예전 동기 `LoadScene`은 모든 PC를 5~10초 멈췄다. 모두 준비되면 방장이 정한 공유 시각에 동시에 화면이 걷히고, 그때까지 이동 입력을 막는다. 경기 → 로비는 그대로 동기 로드. 상세: [UI §8](UI.md#8-로딩-화면-r58-2026-09-30)
- `FollowMatch()`: Lobby에서 `Session.Started`가 되면 경기 방의 모드(`Session.MatchMode`)의 씬을 고른다. 씬이 없는 모드면 `Session.Abort("… 준비 중")`으로 경기를 끝낸다. 있으면 `PlaytestSpawner.NetworkDriven = true`, `ObstacleClock.Use(공유 시계)` 후 그 씬 로드. 경기 방이 사라지면(`Match == 0`) 되돌리고 Lobby 로드.
- `sceneLoaded` 때마다 씬에 맞는 컨트롤러를 **`GameSceneConfig`가 있는 오브젝트에** `AddComponent`로 붙인다. 경기로 연 모드 씬에는 `MatchSceneView`(구 `KingRushMatchView`, 모드 공용)를 붙인다. 컨트롤러를 씬에 저장하지 않는 이유: 씬 파일이 Steam 어셈블리를 참조하면 Steam 패키지 없는 PC에서 깨진다. 또 `RuntimeInitializeOnLoadMethod`는 한 번만 불린다.
- 종료: `Motion.Dispose()` → `Session.Dispose()` 순서(Motion이 SessionChanged 구독을 푼다).
- `BuildTag` = `Application.version` + 개발 빌드면 `-dev`. `SteamSession`에 전달된다.
- 내부 `SharedClock`: `SteamUtils.GetServerRealTime()`(초 단위, 모든 PC 동일) + 로컬 시계로 소수부. PC 간 실제 오차는 미측정.

## 3. 씬별 내용

**Intro** — `IntroHud.uxml`. 키 입력 한 번이면 Lobby. 클릭 UI가 필요 없게 만들었다(클릭 문제 회피). Steam 시작 실패여도 로비로 간다(로비에 재시도 버튼). (R64부터 시작 판 클릭.) **(R84)** 게임을 켤 때 시작 로고 영상(`LogoIntro`)이 그 위에 한 번 나오고, 저장된 이름이 없으면 시작 판이 같은 씬 안의 이름 설정 화면(`NameScreen`)으로 간다. (R85) 시작 판 옆 "이름 바꾸기"는 같은 화면을 바꾸기 모드로 열고 다시 타이틀로 돌아온다(`NameChange`, 로비 오른쪽 위 내 이름도 같음). Lobby로는 장면 전환(`SceneTransition`, 씬이 바뀌어도 살아 있는 패널) 뒤에서 `LoadSceneAsync`로 넘어가고, 로비 무대가 생기면 내 나이트 자리에서 열린다 → [UI §15](UI.md#15-시작-로고이름-설정-화면장면-전환-r84-2026-10-08).

**Lobby** — 편집 모드에는 카메라와 `ChessFight Game Root`만 있다. Play하면 `LobbyBootstrap`이 `LobbyStage`(하늘·흰 바닥·금색 원, 파티 라인업. 프리미티브와 팀 재질 복사본으로 만들어 새 에셋이 없다)와 `NetworkHudView`를 만든다. **로비는 메뉴라 캐릭터가 움직이지 않는다**(`MovementGate` = 항상 false). 예전 `Arena` 체스판 프리팹은 로비에서 더 이상 쓰지 않는다. HUD는 [UI](UI.md).

**KingRush** — 코스 뼈대, 장애물, 스폰 12곳, 체크포인트 2개, 골인, `PhysicsProfile`(120Hz), `Playtest`(`PlaytestSpawner`), `ChessFight Game Root`. 상세: [KingRush](../KingRush/README.md).
- 직접 Play: `PlaytestSpawner`가 임시 캐릭터를 만든다.
- 경기로 진입: `PlaytestSpawner.NetworkDriven`이라 캐릭터를 안 만들고, `MatchSceneView`가 네트워크 캡슐, 읽기 전용 `MatchHud`(명단, 핑, 끊김 배너)를 띄운다. **캡슐은 아직 로비용 평면 모터라 코스를 달릴 수 없다.** Esc = `Session.Cancel()`.

**QueenOfTheHill** (R48, `JY-lobby`) — 편집 모드에는 카메라(`OrbitCamera`), 조명, `ChessFight Game Root`(`GameSceneConfig`·`PhysicsProfile`), `Queen of the Hill Level`(`QueenHillLevel`), `Playtest`(`PlaytestSpawner` + `QueenHillPlaytestPanel`, 래그돌 프리팹·백팀)만 있다. **맵은 Play 때 `Resources/QueenHill/QueenHillLayout.json`(원본: `Tools/QueenHill/build_layout.py`)으로 만든다.** 메뉴 `ChessFight > Scenes > Queen of the Hill (offline graybox)`. 생성기 `Tools/Generators/gen_queenhill_scene.py`. 네트워크 래그돌(M14)이 생기기 전까지 로비 흐름에 넣지 않는다(`GameModes.QueenOfTheHill.Scene`은 비어 있다). 상세: [GRAYBOX](../GameModes/QueenOfTheHill/GRAYBOX.md).

**PawnRushVictory / PawnRushLose** (R76, 10-06 `claude/menu-c` → `JY-kingrush`(거기서는 R65), 10-07 `claude/bold-johnson-8ez95n`에 병합, R62 `LastScene`을 대신함) — 폰 러쉬 결과 화면. 이긴 팀은 Victory, 진 팀은 Lose. 편집 모드에는 `Main Camera`와 `Pawn Rush Result`(`PawnRushResultDirector`)만 있다. **Play하면 디렉터가 램프 아래 탁자 체스판, 두 팀의 말, 조명, 배경, 결승 중계 결과판을 전부 코드로 만든다**(그래서 모양을 바꾸려면 코드를 고친다). 메뉴 `ChessFight > Scenes > Pawn Rush Victory / Pawn Rush Lose (result preview)`. 생성기 `Tools/Generators/gen_pawnrush_result.py`(두 씬을 덮어쓰니 `--overwrite`를 줘야 돈다). 자세한 것은 [UI §13](UI.md#13-폰-러쉬-결과-화면-r76-2026-10-06).

**RagdollTest** — 2026-09-25부터 **준영 님 래그돌 랩 씬 그대로**다. 회전 봉, 벽 2·3·4m, 경사 15·30·45°, 외줄, 림보·터널이 있고, `LabGame`이 P1·P2·더미를 만든다. 튜닝 패널은 Tab. 물리 120Hz는 `LabGame`이 직접 걸고 씬을 떠날 때 되돌린다(`PhysicsProfile` 없음). 빌더 메뉴 `ChessFight > Ragdoll Lab > Rebuild Pawn + Scene`이 이 파일을 다시 만든다. 상세와 네트워크 안전성: [Player/RAGDOLL](../Player/RAGDOLL.md).

## 4. 렌더링

`Game/RenderPipelineOverride.cs`가 `BeforeSceneLoad`에 URP 에셋 참조를 비워 Built-in으로 그리고 종료 때 되돌린다. 어느 씬을 직접 열어도 보인다. `NetworkColor` 셰이더는 Unlit이라 조명이 화면에 영향을 주지 않는다.

## 5. 새 게임 씬(모드)을 추가하려면

1. `Assets/Scenes/<이름>.unity`를 만들고 `ChessFight Game Root`(`GameSceneConfig`)를 둔다. 래그돌을 쓰면 `PhysicsProfile`도 둔다.
2. `SceneNames`에 상수, 빌드 목록과 `NetworkSetup.ShippedScenes`에 추가.
3. `Core/GameModes.cs`의 그 모드 `Scene`에 씬 이름을 넣는다. `NetworkRuntime`은 고칠 필요가 없다(`FollowMatch`가 모드에서 씬을 읽고, `Attach`가 그 씬에 `MatchSceneView`를 붙인다). 모드 전용 화면·규칙이 생기면 그때 `Attach`에 추가한다.
4. 오프라인 시험이 필요하면 `Playtest` 오브젝트를 둔다.
5. 전체 체크리스트: [GameModes §4](../GameModes/README.md).

# 씬 구성과 흐름

사용자 요구(R11): 씬을 **인트로 - 로비 - 게임 씬(킹러시, 승격쟁탈전 등)**으로 나누고, 킹러시 맵 작업 씬과 래그돌 테스트 씬을 만든다. 구현 `72ddf9e`. R19(2026-09-25): 경기 씬은 **경기 방의 게임 모드**가 정한다([GameModes](../GameModes/README.md)). **Unity에서 아직 열지 않았다**(확인 목록: [VALIDATION](../Network/VALIDATION.md) 최상단).

## 1. 흐름

```text
 Intro ──아무 키──▶ Lobby ──모두가 phase=playing──▶ 모드 씬 ──경기 끝(Match==0) / Esc──▶ Lobby
 (타이틀)          (파티·모드·매칭)                  킹 러시 = KingRush
                                                   퀸 오브 더 힐·소드 파이트 = 씬 준비 중
 RagdollTest ← 개발 전용. 흐름 밖.
```

| 씬 | 빌드 순서 | 붙는 컨트롤러 | Play를 누르면 |
|---|---|---|---|
| `Intro.unity` | 0 | `IntroController` | 온라인. 타이틀, Steam 시작, 아무 키 → Lobby |
| `Lobby.unity` (구 ChessFightLab, GUID 동일) | 1 | `LobbyBootstrap` (구 GameBootstrap) | 온라인. 파티 라인업(3D), 모드·파티·매칭 HUD. 이동 없음 |
| `KingRush.unity` | 2 | 경기로 들어왔을 때만 `MatchSceneView` | 직접 열면 **오프라인 플레이테스트** |
| `RagdollTest.unity` | 비활성 | 없음 (씬 안의 `LabGame`이 동작) | 래그돌 랩. Steam 없이 2인 로컬 |
| `SampleScene.unity` | 빌드 제외 | 없음 | 아무것도 안 함(템플릿) |

씬 이름 상수는 `Game/SceneNames.cs`. **런타임에 로드하는 씬은 빌드 목록(`EditorBuildSettings.asset`)에도 있어야 한다.** 빌드 메뉴(`NetworkSetup.ShippedScenes`)가 Intro·Lobby·KingRush를 넣는다.

## 2. NetworkRuntime — 씬을 넘어 사는 유일한 Steam 소유자

`Scripts/Bootstrap/NetworkRuntime.cs`

- `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`에서 **첫 씬이 Intro나 Lobby일 때만** 만들어진다. `DontDestroyOnLoad`.
- 가진 것: `SteamSession`, `SteamMotion`, 입력 소스(`Controls`), `MovementGate`(씬이 등록하는 이동 허용 조건).
- 매 프레임: `Session.Tick()` → 입력 읽기(게이트 적용) → `Motion.Update()` → F8 지연 시뮬레이터 키(개발 빌드) → `FollowMatch()`.
- `FollowMatch()`: Lobby에서 `Session.Started`가 되면 경기 방의 모드(`Session.MatchMode`)의 씬을 고른다. 씬이 없는 모드면 `Session.Abort("… 준비 중")`으로 경기를 끝낸다. 있으면 `PlaytestSpawner.NetworkDriven = true`, `ObstacleClock.Use(공유 시계)` 후 그 씬 로드. 경기 방이 사라지면(`Match == 0`) 되돌리고 Lobby 로드.
- `sceneLoaded` 때마다 씬에 맞는 컨트롤러를 **`GameSceneConfig`가 있는 오브젝트에** `AddComponent`로 붙인다. 경기로 연 모드 씬에는 `MatchSceneView`(구 `KingRushMatchView`, 모드 공용)를 붙인다. 컨트롤러를 씬에 저장하지 않는 이유: 씬 파일이 Steam 어셈블리를 참조하면 Steam 패키지 없는 PC에서 깨진다. 또 `RuntimeInitializeOnLoadMethod`는 한 번만 불린다.
- 종료: `Motion.Dispose()` → `Session.Dispose()` 순서(Motion이 SessionChanged 구독을 푼다).
- `BuildTag` = `Application.version` + 개발 빌드면 `-dev`. `SteamSession`에 전달된다.
- 내부 `SharedClock`: `SteamUtils.GetServerRealTime()`(초 단위, 모든 PC 동일) + 로컬 시계로 소수부. PC 간 실제 오차는 미측정.

## 3. 씬별 내용

**Intro** — `IntroHud.uxml`. 키 입력 한 번이면 Lobby. 클릭 UI가 필요 없게 만들었다(클릭 문제 회피). Steam 시작 실패여도 로비로 간다(로비에 재시도 버튼).

**Lobby** — 편집 모드에는 카메라와 `ChessFight Game Root`만 있다. Play하면 `LobbyBootstrap`이 `LobbyStage`(하늘·흰 바닥·금색 원, 파티 라인업. 프리미티브와 팀 재질 복사본으로 만들어 새 에셋이 없다)와 `NetworkHudView`를 만든다. **로비는 메뉴라 캐릭터가 움직이지 않는다**(`MovementGate` = 항상 false). 예전 `Arena` 체스판 프리팹은 로비에서 더 이상 쓰지 않는다. HUD는 [UI](UI.md).

**KingRush** — 코스 뼈대, 장애물, 스폰 12곳, 체크포인트 2개, 골인, `PhysicsProfile`(120Hz), `Playtest`(`PlaytestSpawner`), `ChessFight Game Root`. 상세: [KingRush](../KingRush/README.md).
- 직접 Play: `PlaytestSpawner`가 임시 캐릭터를 만든다.
- 경기로 진입: `PlaytestSpawner.NetworkDriven`이라 캐릭터를 안 만들고, `MatchSceneView`가 네트워크 캡슐, 읽기 전용 `MatchHud`(명단, 핑, 끊김 배너)를 띄운다. **캡슐은 아직 로비용 평면 모터라 코스를 달릴 수 없다.** Esc = `Session.Cancel()`.

**RagdollTest** — 2026-09-25부터 **준영 님 래그돌 랩 씬 그대로**다. 회전 봉, 벽 2·3·4m, 경사 15·30·45°, 외줄, 림보·터널이 있고, `LabGame`이 P1·P2·더미를 만든다. 튜닝 패널은 Tab. 물리 120Hz는 `LabGame`이 직접 걸고 씬을 떠날 때 되돌린다(`PhysicsProfile` 없음). 빌더 메뉴 `ChessFight > Ragdoll Lab > Rebuild Pawn + Scene`이 이 파일을 다시 만든다. 상세와 네트워크 안전성: [Player/RAGDOLL](../Player/RAGDOLL.md).

## 4. 렌더링

`Game/RenderPipelineOverride.cs`가 `BeforeSceneLoad`에 URP 에셋 참조를 비워 Built-in으로 그리고 종료 때 되돌린다. 어느 씬을 직접 열어도 보인다. `NetworkColor` 셰이더는 Unlit이라 조명이 화면에 영향을 주지 않는다.

## 5. 새 게임 씬(모드)을 추가하려면

1. `Assets/Scenes/<이름>.unity`를 만들고 `ChessFight Game Root`(`GameSceneConfig`)를 둔다. 래그돌을 쓰면 `PhysicsProfile`도 둔다.
2. `SceneNames`에 상수, 빌드 목록과 `NetworkSetup.ShippedScenes`에 추가.
3. `Core/GameModes.cs`의 그 모드 `Scene`에 씬 이름을 넣는다. `NetworkRuntime`은 고칠 필요가 없다(`FollowMatch`가 모드에서 씬을 읽고, `Attach`가 그 씬에 `MatchSceneView`를 붙인다). 모드 전용 화면·규칙이 생기면 그때 `Attach`에 추가한다.
4. 오프라인 시험이 필요하면 `Playtest` 오브젝트를 둔다.
5. 전체 체크리스트: [GameModes §4](../GameModes/README.md).

# 폴더·어셈블리 구조

사용자 요구(R3): **꼭 필요한 최소 폴더로, 한눈에 구조가 보이게.** 그래서 `Assets/` 바로 아래는 종류별 폴더만 두고, 스크립트는 **폴더 하나 = 어셈블리 하나**다.

## 1. 폴더

```text
Assets/
  Art/                원본 모델·텍스처·애니메이션 (README만 있음)
  Materials/          Team{Blue,Orange} Board{Dark,Light} Course CourseEdge Obstacle Finish Prop .mat + NetworkColor.shader(Unlit)
  Prefabs/            PawnAvatar(네트워크 캡슐) · Arena(로비 체스판)
    Characters/       PlaytestCharacter (임시 캐릭터)
    Obstacles/        Spinner · SlidingWall · Pendulum
  Resources/          코드가 이름으로 읽는 것만: NetworkHud.uxml/.uss, IntroHud.uxml, MatchHud.uxml, NetworkTheme.tss, ChessFightControls.inputactions
  Scenes/             Intro · Lobby · KingRush · RagdollTest (+ SampleScene 템플릿, 빌드 제외·비활성)
  Scripts/            ↓ 폴더 하나 = 어셈블리 하나
  Settings/           템플릿 URP 에셋 (현재 미사용)
  TutorialInfo/       Unity 템플릿 잔재
Docs/                 문서 트리 (HANDOFF.md에서 시작)
Tests/Network/        Unity 밖 테스트 (Core, 모의 Steam 세션)
Tools/                테스트 스크립트, Generators/
```

`Assets/ChessFight/…` 같은 중첩 폴더는 `8649011`에서 없앴다. **예외: 준영 님 래그돌 랩은 `Assets/ChessFight/RagdollLab/`에 그대로 둔다**(2026-09-25 병합). 빌더 메뉴가 이 경로를 알고 있고, 랩은 Art·Generated·Materials·Prefabs·Scripts·Settings를 한 덩어리로 관리한다. 랩 씬만 `Assets/Scenes/RagdollTest.unity`로 옮겼다. `Assets/ChessFight.meta`는 PC마다 GUID가 달라지지 않게 커밋되어 있다.

## 2. 어셈블리

| 폴더 | 어셈블리 | 참조 | 게이트 | 책임 |
|---|---|---|---|---|
| `Scripts/Core/` | `ChessFight.Network.Core` | 없음 (`noEngineReferences`) | — | 예약 규칙, 패킷, 이동 모터, 봇 ID·두뇌, 연결 품질, FriendInfo. **Unity 없이 테스트** |
| `Scripts/Network/` | `ChessFight.Network.Steam` | Core, Steamworks.NET | `CHESSFIGHT_STEAM` | `SteamSession`(파티·매칭·로비), `SteamMotion`(이동 전송) |
| `Scripts/Game/` | `ChessFight.Game` | Core | — | HUD 뷰, 카메라, 입력 추상화, 씬 이름, 패널·폰트, 파이프라인 우회, 프리팹 스크립트. **Steam 무참조** |
| `Scripts/Gameplay/` | `ChessFight.Gameplay` | Game | — | 캐릭터 계약, 장애물, 코스, 물리 프로필, 오프라인 플레이테스트. **Steam 무참조** |
| `Scripts/Bootstrap/` | `ChessFight.Game.Steam` | Core, Network.Steam, Game, Gameplay, Steamworks.NET | `CHESSFIGHT_STEAM` | `NetworkRuntime`, 씬 컨트롤러(Intro/Lobby/KingRush) |
| `Scripts/Input/` | `ChessFight.Game.Input` | Game, Unity.InputSystem | `CHESSFIGHT_INPUTSYSTEM` | Input System 이동 소스(자기 등록) |
| `Scripts/Editor/` | Assembly-CSharp-Editor | — | — | 설치·씬 메뉴·빌드, `InputSettingsGuard` |
| `ChessFight/RagdollLab/Scripts/` | `ChessFight.RagdollLab` | Gameplay | — | 래그돌(`RagdollPawn`, 손, 튜닝), 랩(`LabGame`, 카메라, 패널, 자동 점검, XInput), `RagdollDriver`(`ICharacterDriver` 구현). **Steam 무참조** |
| `ChessFight/RagdollLab/Editor/` | `ChessFight.RagdollLab.Editor` | RagdollLab | Editor 전용 | 래그돌 리그·프리팹·씬 빌더 |

Steam 게이트 어셈블리의 플랫폼은 Editor, WindowsStandalone64/32다.

## 3. 의존 방향

```text
Core  ←  Network.Steam  ←┐
  ↑                       Bootstrap (Game.Steam)
Game  ←  Gameplay      ←┘
  ↑         ↑
  │       RagdollLab (래그돌. Gameplay만 참조, 아무도 참조하지 않음)
Input (자기 등록, 아무도 참조하지 않음)
```

- **프리팹과 씬이 참조하는 스크립트는 `Game`·`Gameplay`에만 있다.** 그래서 Steamworks가 설치되기 전에 열어도 missing script가 없다.
- `Bootstrap`만 모든 것을 안다. 씬 컨트롤러는 씬에 저장되지 않고 `NetworkRuntime`이 붙인다([SCENES](SCENES.md)).
- 래그돌(`ChessFight.RagdollLab`)은 `Gameplay`를 참조하지만 `Gameplay`·`Bootstrap`·네트워크는 래그돌을 참조하지 않는다. 그래서 캐릭터 호출은 항상 `ICharacterDriver`로 한다. **이 경계는 `Tools/run-tests-linux.sh`의 경계 검사가 매번 확인한다.**

## 4. define 규칙

| define | 뜻 | 어디서 |
|---|---|---|
| `CHESSFIGHT_STEAM` | Steamworks.NET 2025.164.1 이상이 설치됨 | asmdef `versionDefines` → `defineConstraints` |
| `CHESSFIGHT_INPUTSYSTEM` | Input System 패키지가 설치됨 | 〃 |
| `ENABLE_INPUT_SYSTEM` | Active Input Handling **설정**이 New/Both | Unity. **설치 여부로 쓰지 않는다.** 런타임 분기에만 |
| `STEAMWORKS_NET` | Steamworks 패키지 자체 define | 우리 코드는 쓰지 않는다 |

## 5. 주요 파일 한눈에

| 바꾸고 싶은 것 | 파일 |
|---|---|
| 캐릭터 겉모습(로비·경기) | `Prefabs/PawnAvatar.prefab` |
| 로비 맵 | `Prefabs/Arena.prefab` |
| 색 | `Materials/*.mat` |
| HUD 배치·스타일 | `Resources/NetworkHud.uxml` / `.uss` (이름은 `NetworkHudView`와 계약) |
| 키 배치 | `Resources/ChessFightControls.inputactions` |
| 씬 배선 | 각 씬의 `ChessFight Game Root` (`GameSceneConfig`) |
| 매칭·예약 규칙 | `Scripts/Core/TeamReservations.cs` |
| 로비·파티 | `Scripts/Network/SteamSession.cs` |
| 이동 동기화 | `Scripts/Network/SteamMotion.cs`, `Scripts/Core/MotionProtocol.cs` |
| 씬 흐름 | `Scripts/Bootstrap/NetworkRuntime.cs` |

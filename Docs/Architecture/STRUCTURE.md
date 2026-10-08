# 폴더·어셈블리 구조

사용자 요구(R3): **꼭 필요한 최소 폴더로, 한눈에 구조가 보이게.** 그래서 `Assets/` 바로 아래는 종류별 폴더만 두고, 스크립트는 **폴더 하나 = 어셈블리 하나**다.

## 1. 폴더

2026-10-08(R90) 다시 정리: **`Assets/` 바로 아래는 종류별 폴더만, 같은 종류가 두 군데에 있지 않게.** 기능 이름의 폴더(`ChessFight/`, `Maps/`, `Scenes/PawnRush/` 같은 것)는 만들지 않는다. 스크립트는 **`Scripts/` 아래 폴더 하나 = 어셈블리 하나**(래그돌 랩·폰 러시 코스도 여기). 파일은 `.meta`와 함께 옮겨서 GUID 참조는 그대로다.

```text
Assets/
  Art/                원본 모델·텍스처
    Pawn/             폰 모델(Pawn.fbx)·텍스처 (래그돌 랩)
    PawnGenerated/    래그돌 빌더가 굽는 메시·텍스처·충돌 상자(Boxes/)
    YogurtCup/        요구르트 컵 소품(가져온 맵 장애물)
  Materials/          모든 재질과 셰이더: 네트워크 캡슐·로비, 래그돌 랩(Lab_*·Pawn*), 폰 러시 코스(Board_*·Wall_*·Course*.shader 등),
                      가져온 장애물(Blue·Red·Stone·MapBoardDark·MapBoardLight 등), 옛 킹 러쉬 코스(Cream·Wood 등)
  Prefabs/            PawnAvatar(네트워크 캡슐·로비 라인업) · RagdollPawn(래그돌 폰) · Arena(옛 로비, 미사용)
    Characters/       PlaytestCharacter (임시 캡슐)
    Obstacles/        Spinner·SlidingWall·Pendulum + 가져온 장애물 19개(01_SpinningDisc … PR_* 변형, 폰 러시 코스가 씀)
    KingRushOpening/  옛 킹 러쉬 코스 구간 프리팹 24개(씬은 10-08 삭제, 코드와 함께 보관)
  Resources/          코드가 이름으로 읽는 것만: HUD uxml/uss, 글꼴, 메뉴 그림, PawnRushSkillFx/(스킬 효과), QueenHill/(맵 데이터, 생성물) …
  Scenes/             Intro · Lobby · PawnRush_Course01(폰 러쉬 경기) · PawnRush_SkillTest · PawnRushVictory · PawnRushLose ·
                      SwordFight · QueenOfTheHill(그레이박스) · RagdollTest(래그돌 랩)
  Scripts/            ↓ 폴더 하나 = 어셈블리 하나 (2절)
  Settings/           RagdollTuning.asset·preset_*.json(래그돌 튜닝), Course01Kit.asset(폰 러시 코스 재료 목록), 템플릿 URP 에셋(미사용,
                      프로젝트 설정이 참조해 남김)
  StreamingAssets/    시작 로고 영상
  InputSystem_Actions.inputactions   템플릿 파일(빌드 설정이 참조해 남김)
Docs/                 문서 트리 (HANDOFF.md에서 시작)
Tests/Network/        Unity 밖 테스트 (Core, 모의 Steam 세션)
Tools/                테스트 스크립트, Generators/(씬·프리팹 생성기, templates/ = 지운 KingRush·SampleScene 씬을 생성기 틀로 보관), QueenHill/
```

없앤 것(10-08): `Assets/ChessFight/`(래그돌 랩 예외였던 폴더 → `Scripts/RagdollLab`·`RagdollLabEditor`·`RagdollLabSteam`, `Art/Pawn`·`PawnGenerated`, `Materials`, `Prefabs`, `Settings`, `Resources`), `Assets/Maps/`(폰 러시 코스 → `Scripts/PawnRush`·`PawnRushEditor`·`Materials`·`Settings`, 가져온 맵 → `Prefabs/Obstacles`·`Materials`·`Art/YogurtCup`·`Scripts/ImportedMapEditor`), `Scenes/PawnRush/`, `Prefabs/KingRushOpening/Materials/`, `TutorialInfo/`·`Readme.asset`(Unity 템플릿). 예전 `8649011`(09-22)의 정리와 같은 원칙이고, 그때 예외로 둔 래그돌 랩도 이번에 같은 규칙으로 옮겼다.

## 2. 어셈블리

| 폴더 | 어셈블리 | 참조 | 게이트 | 책임 |
|---|---|---|---|---|
| `Scripts/Core/` | `ChessFight.Network.Core` | 없음 (`noEngineReferences`) | — | 예약 규칙, 패킷, 이동 모터, 봇 ID·두뇌, 연결 품질, FriendInfo, **게임 모드 목록(`GameModes`)**, 채팅 규칙(`Chat`: 글 다듬기·채널·1초 제한·로그), 퀸 오브 더 힐 규칙·기물·코스 숫자(`QueenHillRules`·`ChessPieces`·`QueenHillCourse`). **Unity 없이 테스트** |
| `Scripts/Network/` | `ChessFight.Network.Steam` | Core, Steamworks.NET | `CHESSFIGHT_STEAM` | `SteamSession`(파티·매칭·로비), `SteamMotion`(이동 전송) |
| `Scripts/Game/` | `ChessFight.Game` | Core | — | HUD 뷰, 로비 3D 라인업(`LobbyStage`), 카메라, 입력 추상화, 씬 이름, 패널·폰트, 파이프라인 우회, 프리팹 스크립트. **Steam 무참조** |
| `Scripts/Gameplay/` | `ChessFight.Gameplay` | Game | — | 캐릭터 계약, 장애물, 코스, 물리 프로필, 오프라인 플레이테스트, 퀸 오브 더 힐(`QueenHill/`: 종·열린 길·체크포인트·승격·그레이박스 맵 `QueenHillLevel`). **Steam 무참조** |
| `Scripts/Bootstrap/` | `ChessFight.Game.Steam` | Core, Network.Steam, Game, Gameplay, Steamworks.NET | `CHESSFIGHT_STEAM` | `NetworkRuntime`, 씬 컨트롤러(Intro/Lobby, 모드 씬 공용 `MatchSceneView`) |
| `Scripts/Input/` | `ChessFight.Game.Input` | Game, Unity.InputSystem | `CHESSFIGHT_INPUTSYSTEM` | Input System 이동 소스(자기 등록) |
| `Scripts/Editor/` | Assembly-CSharp-Editor | — | — | 설치·씬 메뉴·빌드, `InputSettingsGuard` |
| `Scripts/RagdollLab/` | `ChessFight.RagdollLab` | Gameplay | — | 래그돌(`RagdollPawn`, 손, 튜닝), 랩(`LabGame`, 카메라, 패널, 자동 점검, XInput), `RagdollDriver`(`ICharacterDriver` 구현). **Steam 무참조** |
| `Scripts/RagdollLabEditor/` | `ChessFight.RagdollLab.Editor` | RagdollLab | Editor 전용 | 래그돌 리그·프리팹·씬 빌더 |
| `Scripts/RagdollLabSteam/` | `ChessFight.RagdollLab.Net` | RagdollLab, Game, Bootstrap, Gameplay, Core, Network.Steam, Steamworks.NET | `CHESSFIGHT_STEAM` | 랩·소드 파이트의 Steam 다리(`SteamRagdollLink`, `SteamSwordFightLink`) |
| `Scripts/PawnRush/` | `ChessFight.PawnRush` | Game, Gameplay, Core | — | 폰 러시 코스 01: 코드로 만드는 코스(`Course01v4Builder`), 미션 광장·미니게임, 검증기, 오프라인 플레이(`Course01Playtest`) |
| `Scripts/PawnRushEditor/` | `ChessFight.PawnRush.Editor` | PawnRush 등 | Editor 전용 | 메뉴 Pawn Rush(Open/Build/Validate Course01, 거울 복사) |
| `Scripts/ImportedMapEditor/` | `ChessFight.ImportedMap.Editor` | Game, Gameplay | Editor 전용 | 가져온 장애물 프리팹 선택 메뉴(맵 씬은 10-08 삭제) |

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
| 로비 배경·라인업 | `Scripts/Game/LobbyStage.cs` (색·카메라·자리 간격) |
| 게임 모드 목록 | `Scripts/Core/GameModes.cs` ([GameModes](../GameModes/README.md)) |
| 색 | `Materials/*.mat` |
| HUD 배치·스타일 | `Resources/NetworkHud.uxml` / `.uss` (이름은 `NetworkHudView`와 계약) |
| 키 배치 | `Resources/ChessFightControls.inputactions` |
| 씬 배선 | 각 씬의 `ChessFight Game Root` (`GameSceneConfig`) |
| 매칭·예약 규칙 | `Scripts/Core/TeamReservations.cs` |
| 경기 중 빈자리 규칙(R59) | `Scripts/Core/Backfill.cs` |
| 로비·파티 | `Scripts/Network/SteamSession.cs` |
| 채팅(R61) | `Scripts/Core/Chat.cs`(규칙), `Scripts/Network/SteamSession.cs`(`Say`·`Hear`), `Scripts/Game/ChatBox.cs`(화면), `Resources/ChatHud.uxml` |
| 메뉴 디자인 C(R63) | `Scripts/Game/MenuArt.cs`(텍스처·조명·3D 글씨·말 그림), `ChunkyButtons.cs`(판 버튼), `MenuMarks.cs`(왕관·화살표), `IntroStage.cs`, `LobbyStage.cs`, `LoadingStudio.cs`·`LoadingScreenView.cs`(로딩 입장), `Resources/Fonts/` ([UI §11](UI.md#11-메뉴-디자인-c-그랜드-아레나-수정안-r63-2026-10-03)) |
| 폰 러쉬 결과 화면(R76, R62를 대신함) | `Scenes/PawnRushVictory.unity`·`PawnRushLose.unity`, `Scripts/Game/PawnRushResult*.cs`(진행·무대·결과판, Steam 모름), 말 `LastSceneFigure.cs`·메시 `LastSceneArt.cs`, `Scripts/Core/MatchResult.cs`(도착 순서 규칙), `Resources/PawnRushResultHud.uxml` / `.uss`, `PawnRushResultTheme.tss` ([UI §13](UI.md#13-폰-러쉬-결과-화면-r76-2026-10-06)) |
| 이동 동기화 | `Scripts/Network/SteamMotion.cs`, `Scripts/Core/MotionProtocol.cs` |
| 씬 흐름 | `Scripts/Bootstrap/NetworkRuntime.cs` |

# Network 구현 검증 기록

최종 갱신: 2026-09-22 (KST). 코드 기준: `Network` 브랜치 (구조 개편 + AI 봇 커밋).
대상: Unity `6000.3.11f1`, Windows x64, Steamworks.NET `2025.164.1`.

상태: **핵심 로직·모의 세션 검사 통과 / 두 PC·두 계정의 파티 입장과 공개 매칭 성사 확인 / 두 계정 이동 동기화는 사용자 보고 없음 / 구조 개편과 AI 봇은 Unity 실기 미검증**.

> 이 문서의 표는 **사용자가 실제로 확인했다고 보고한 것만** '성공'으로 적는다.
> 코드 작성·코드 검토는 검증이 아니다.

## ★ 2026-09-24 씬 분리 — 팀 배포 전 확인 목록

씬 분리와 팀 개발 환경(`Docs/TEAM_GUIDE_KO.md`)을 추가했다. **Unity에서 아직 열지 않았다.** 팀에 배포하기 전에 아래를 확인하고 결과를 이 표에 적는다.

| # | 확인 | 결과 |
|---|---|---|
| 1 | 프로젝트가 Safe Mode 없이 열린다 | 미확인 |
| 2 | `KingRush` 직접 Play → 캡슐 캐릭터 등장, WASD·점프, 회전봉에 밀림, 체크포인트·골인 기록 | 미확인 |
| 3 | `RagdollTest` 직접 Play → 턱·벽·상자 밀기, 진자에 밀림 | 미확인 |
| 4 | `Intro` Play → 타이틀, 아무 키 → 로비 | 미확인 |
| 5 | 로비에서 비공개 방 → 봇 채우기 → 경기 시작 → **KingRush로 전환**, 캡슐 표시 | 미확인 |
| 6 | KingRush에서 Esc → **로비로 복귀, 파티 유지** | 미확인 |
| 7 | 로비 복귀 후 물리 주기가 50Hz로 돌아옴 (`PhysicsProfile` 복구) | 미확인 |
| 8 | 두 PC: 경기 시작 시 둘 다 KingRush로, 장애물 위치가 두 화면에서 같음 | 미확인 |

## ★ 2026-09-24 네트워크 기획안 반영 — 확인 목록

끊김 경고·점프 횟수·버전 검사·공개 매치 봇 규칙·핑/지연 시뮬레이터·Rich Presence를 추가했다(`Docs/AI/NETWORK_HANDOFF_KO.md` §2.7). **프로토콜 v2라 두 PC 모두 이 커밋 이상이어야 한다.**

| # | 확인 | 결과 |
|---|---|---|
| 1 | 로비 상세 정보에 `버전 0.1.0-dev`, 경기 중 `핑 / 응답` 수치 표시 | 미확인 |
| 2 | 두 PC 경기 중 방장 창을 일시정지(또는 방장 PC Steam 오프라인) → 손님 화면에 0.5초 안에 노란 배너, 2초 뒤 빨간 "멈춤", 12초 뒤 파티 복귀 | 미확인 |
| 3 | 손님 PC에서 F8 → `+200ms / 손실 5%`에서도 점프가 빠지지 않음(10회 중 10회) | 미확인 |
| 4 | Player Settings Version을 한쪽만 `0.1.1`로 바꿔 빌드 → 파티 초대 시 "게임 버전이 다릅니다" 표시 | 미확인 |
| 5 | Steam 친구 목록에서 상대의 "게임 참가" → 같은 파티로 입장 | 미확인 |

## 확인 결과

| 항목 | 결과 | 근거와 한계 |
|---|---|---|
| Core C# 테스트 | 통과, 28개 (2026-09-24) | 정원·중복·입장 실패·만료, 1,000회 무작위 예약, 패킷 검증, 이동·점프 수식, 봇 ID·소유권·충원·로밍, **손실된 점프 1회 보존, v1 패킷 거절, 끊김 단계, 응답 시간, 지연 시뮬레이터**. 실제 물리/통신 검증 아님 |
| 세션 상태 테스트 | 통과, 12개 (2026-09-24) | 생산 SteamSession.cs를 모의 Steam 서비스와 실행. 파티 동행, 팀 입장, 취소, 늦은 콜백, 부분 실패, 호스트 이탈, 12개 동시 검색 수렴, **다른 build 파티 거절·검색 분리, 릴리스 봇 규칙, 개발 빌드 봇 허용, Rich Presence 참가** |
| 어셈블리별 컴파일 | 통과 | 실제 Unity 라이브러리와 공식 Steamworks 소스 사용. Steamworks/Core/Steam 런타임/Editor 도구 별도 컴파일 |
| 실제 Editor import·컴파일 | 2026-09-21 성공 | 원본 ChessFighter를 Unity Hub에서 실행, 메뉴 및 Play 화면 확인 |
| UPM 의존성 설치 | 2026-09-21 성공 | 실제 manifest/lock 생성 및 패키지 컴파일. c9c1e6a에 커밋·Push |
| Editor Steam 1인 파티·경기 | 성공 | Party ready, 0 아닌 파티/경기 ID, Waiting room: 1/12 확인 |
| Editor UI·카메라·캡슐 | 표시 확인 | 체스판, BLUE 캡슐, 명단과 추적 화면 확인. 키 입력은 보냈으나 점프 궤적/조작감을 캡처만으로 입증하지 않음 |
| Windows x64 개발 빌드 | 2026-09-21 성공 | Editor.log의 Build Finished, Result: Success. 약 54초. exe, Data, Mono, steam_appid.txt, steam_api64.dll 확인 |
| 독립 실행 빌드 표시 | 2026-09-22 확인 | 체스판·UI 표시. Steam 미실행 상태에서 초기화 실패 안내 표시 |
| 독립 실행 빌드 Steam 입장 | 2026-09-22 1인 성공 | 로그인 후 빌드 재시작. Party ready, 비공개 방 Waiting room: 1/12, BLUE 캡슐·명단 확인 |
| 두 PC·두 계정 파티 입장 | 2026-09-22 성공 | 사용자 보고. 서로 다른 Steam 계정 2대에서 파티 입장 확인 |
| 두 PC·두 계정 공개 자동 매칭 성사 | 2026-09-22 성공 | 사용자 보고. 각자 1인 파티로 Quick match를 눌러 서로 같은 경기 방에 수렴하는 것까지 확인. 12인 정원 충족과 자동 시작은 별개 |
| 두 PC·두 계정 이동 동기화 | 보고 없음 | 위 매칭 확인 시 양방향 이동·점프가 보였는지는 보고되지 않았다. 다음 세션에서 명시적으로 확인하고 이 행을 갱신한다 |
| 실제 12인 매칭·인터넷 부하 | 미실행 | 팀 실기 테스트 필요 |
| 지연·손실·강제 종료·장시간 실행 | 미실행 | 출시 준비 판정 불가 |
| **AI 봇** (파티 봇·호스트 충원 봇) | Core 검사만 통과 | 아래 '구조 개편과 AI 봇' 참고. Unity 실행·표시·12인 부하 미검증 |
| **프리팹/씬/Input System 개편** | Unity 미실행 | 에디터에서 한 번도 열지 않았다. 최우선 확인 대상 |

빌드 위치: `C:\Users\dua07\GitHub\ChessFighter\Builds\NetworkTest\ChessFight.exe`.
프로젝트 내부 Builds는 Git 제외 대상이다. 팀원에게 실행 파일을 공유할 때는 NetworkTest 폴더 전체를 전달한다.

## 구조 개편과 AI 봇 (2026-09-22)

런타임 코드 생성에서 **자산 기반 구조**로 바꾸고 AI 봇을 넣었다. 게임 플레이 화면은 이전과 같게 유지하는 것이 목표다.

| 변경 | 내용 |
|---|---|
| 플레이어 표시 | `PawnAvatar.prefab` (캡슐 + Accent 구체 + `PawnAvatar` 스크립트) |
| 맵 | `Arena.prefab` (8×8 타일 64개, 위치·크기·색 동일) |
| 재질 | `TeamBlue/TeamOrange/BoardDark/BoardLight.mat` 자산. 런타임 `new Material` 제거 |
| UI | `NetworkHud.uxml` + `NetworkHudView`. UXML의 `\n` 문자 표시 문제를 `&#10;`으로 수정 |
| 입력 | `ChessFightControls.inputactions` + Input System. 패키지 미설치 시 Legacy로 자동 대체 |
| 씬 | `Assets/Scenes/ChessFightLab.unity` 신규 (2026-09-24 `Lobby.unity`로 이름 변경). URP 잔여 참조 없음 |
| 폴더 | `Assets/{Scripts,Prefabs,Materials,Resources,Scenes}`. 스크립트는 폴더 하나 = 어셈블리 하나 |
| 어셈블리 | `ChessFight.Game` (Steam 무관) / `ChessFight.Game.Steam` (부트스트랩) 분리 |

### 같은 날 후속 수정

| 증상 | 원인 | 수정 |
|---|---|---|
| `The referenced script (Unknown) ... is missing!` | `SampleScene`의 Main Camera / Directional Light / Global Volume에 URP 컴포넌트 3개가 남아 있었다. URP 패키지가 없어 GUID가 해결되지 않는다 | 세 컴포넌트와 Global Volume 오브젝트를 제거. 이 씬은 부트 대상에서 제외 |
| **UI가 눌리지 않음** | 확정하지 못했다. 재현할 Unity가 없다. 가능성 높은 순으로 아래 세 가지를 모두 고쳤다 | |
| ↳ 패널이 화면보다 길어짐 | 봇 UI 4줄을 추가해 패널이 720px를 넘었다. flex 축소/잘림으로 요소를 못 누를 수 있다 | 패널을 `ScrollView`로 변경 |
| ↳ ~~Input System 전환~~ | ~~`EventSystem` 추가~~ **철회.** 이 프로젝트에는 `com.unity.ugui`가 없어 `UnityEngine.EventSystems` 자체가 존재하지 않는다(CS0234로 Safe Mode 진입). UI Toolkit은 EventSystem 없이 자체 `DefaultEventSystem`으로 입력을 받으므로 애초에 불필요했다 | 해당 파일 삭제 |
| ↳ Steam 미실행 | `Online`이 false면 모든 버튼이 비활성이라 "아무것도 안 눌리는" 것처럼 보인다 | `Retry Steam connection` 버튼 추가, HUD에 `Steam: online/OFFLINE` 표시 |

Input System 가설이 무효가 되면서 남은 후보는 **패널 높이**와 **Steam 미실행** 둘이다. 실행 후 HUD의 `Steam:` 줄과 `Input:` 줄을 먼저 본다.

### UI 클릭 — 아직 미해결 (2026-09-24)

`activeInputHandler`를 `0`으로 되돌린 뒤에도 **클릭이 안 되고, 패널의 포인터 수신 로그조차 찍히지 않는다.**

원래 동작하던 `87805f0`과 현재를 비교하면 EventSystem 없음·UXML 구조·PanelSettings 생성 방식이 모두 같다. 남은 차이는 이 설정뿐이므로, **파일 수정이 실제로 적용되지 않았을 가능성이 크다.** Unity가 켜진 채 Pull하면 메모리에 있는 값으로 `ProjectSettings.asset`을 덮어쓴다. 사용자가 앞서 보고한 "변경점이 계속 되살아난다"가 정확히 이 현상이다. 이 설정은 **에디터 재시작 전까지 적용되지 않는다.**

이번에 넣은 것:

| | 내용 |
|---|---|
| `Scripts/Editor/InputSettingsGuard.cs` | `SerializedObject`로 Unity를 **통해서** 값을 되돌린다. 파일을 몰래 고치는 것과 달리 Unity가 덮어쓰지 않는다. uGUI가 설치되면 간섭하지 않는다 |
| 대체 클릭 처리 | 패널이 실제 포인터 이벤트를 못 받으면 마우스 좌표로 직접 버튼을 찾아 실행한다. **진짜 이벤트가 한 번이라도 오면 스스로 꺼진다** |
| `붙여넣기` 버튼 | 포인터 이벤트가 없으면 키 입력도 없다. 클립보드에서 방 번호를 넣어 타이핑 없이 2PC 테스트가 가능하다 |
| 시작 1초 후 진단 로그 | 입력 백엔드 define, 패널 유무, root/screen 크기, 버튼 수, 실제 포인터 수신 여부 |

다음 실행의 진단 로그가 원인을 가른다.

- `입력 백엔드: LEGACY` 만 → 설정이 적용된 것. 그런데도 포인터가 안 오면 설정이 원인이 아니다
- `LEGACY INPUTSYSTEM` → 설정이 **아직 적용되지 않았다**. 에디터 완전 재시작 필요
- `screen: (0, 0)` → 레이아웃이 0 크기라 picking이 안 되는 것

### UI 클릭 문제 — 1차 조치 (2026-09-24)

**원인은 `activeInputHandler`였다.** 레이아웃이 아니었다.

| | 값 | 클릭 |
|---|---|---|
| `87805f0` ~ 구조 개편 전 | `0` (Input Manager Old) | **됨** |
| `3abe230` 이후 | `2` (Both) | **안 됨** |

`3abe230`에서 Input System을 쓰려고 `0 → 2`로 바꿨고, 정확히 그 시점부터 클릭이 멈췄다. 그 뒤 HUD 레이아웃을 세 번 바꿨지만(원래 패널 → ScrollView → 전면 레이아웃) **한 번도 눌리지 않았다.** 마크업이 아니라 프로젝트 설정이라는 증거다.

이유: **이 프로젝트에는 uGUI(`com.unity.ugui`)가 없다.** 그래서 UI Toolkit 런타임 패널은 `EventSystem` 경로를 쓸 수 없고 자체 `DefaultEventSystem`에 의존하는데, Input System 백엔드가 켜지면 이 경로가 포인터·키보드 이벤트를 전달하지 못한다. 클릭뿐 아니라 **방 번호 입력도 불가능**해진다.

**조치: `activeInputHandler`를 `0`으로 되돌렸다.** 검증된 구성으로 복귀한 것이다.

| 항목 | 현재 |
|---|---|
| UI 클릭·타이핑 | 레거시 Input → 동작 |
| 캐릭터 이동 | `LegacyMoveInputSource` |
| Input System 패키지 | 설치·고정 유지 (1.20.0) |
| `ChessFightControls.inputactions` | 유지 |
| `InputSystemMoveSource` | 컴파일됨. `ENABLE_INPUT_SYSTEM`이 꺼져 있어 스스로 레거시에 넘긴다 |

**Input System을 다시 켜려면 순서가 있다.** 설정만 바꾸면 클릭이 또 죽는다.

1. `com.unity.ugui` 설치
2. 씬에 `EventSystem` + `InputSystemUIInputModule` 추가
3. 그 다음에 Active Input Handling을 `Both`로 변경

**진단 장치:** HUD 패널이 포인터 이벤트를 받으면 Console에 `[ChessFight] HUD 포인터 입력 확인됨.`이 한 번 찍힌다. 버튼이 안 눌리는데 이 로그도 없으면 입력 백엔드 문제이고, 로그는 찍히는데 버튼만 안 되면 picking이나 활성 상태 문제다.

### UI 전면 개편 (2026-09-24)

HUD가 한 덩어리 패널에서 **화면 전체를 쓰는 폴가이즈식 레이아웃**으로 바뀌었다. 문구는 모두 한국어다. 좌측에 상태/도구/명단, 우측 상단에 방 번호, **우측 하단에 큰 액션 버튼**이 있다.

액션 버튼은 `HudStep` 4단계(`Home` → `Mode` → `Party` → `Busy`)를 오간다. **`게임 시작`은 화면 이동일 뿐 아무것도 시작하지 않는다.** 실제로 Steam을 부르는 것은 `빠른 매칭`과 파티 화면의 `게임 시작`뿐이다. 세션이 실제로 바빠지면 `Busy`로 강제 전환된다.

**미검증:** 이 레이아웃은 Unity에서 실행해보지 않았다. 특히 **한글 폰트**가 확인 대상이다. 기본 `LegacyRuntime.ttf`에는 한글이 없어서 `Font.CreateDynamicFontFromOSFont`로 맑은 고딕 등 OS 폰트를 잡도록 했다. 실패하면 기본 폰트로 떨어지며 Console에 어느 폰트를 썼는지 남는다. **한글이 네모로 깨지면 그 로그를 먼저 본다.**

### 실행 후 확인된 것 (2026-09-24)

| 증상 | 원인 | 조치 |
|---|---|---|
| **HUD가 아예 안 보임** | 패널을 `ScrollView`로 바꾼 것이 원인. `NetworkTheme.tss`는 이 프로젝트의 USS만 가져오고 **Unity 기본 런타임 테마(`unity-theme://default`)를 가져오지 않는다.** Button·Label·Toggle은 USS가 직접 스타일을 주므로 살아남지만, `ScrollView`처럼 기본 테마의 내부 스타일에 의존하는 복합 컨트롤은 크기가 0이 되어 패널 전체가 사라진다 | 패널을 평범한 `VisualElement`로 되돌리고 `flex-shrink: 0`으로 눌리지 않게 했다. 패널 내용을 한 줄 줄이고 여백을 줄여 720p에 들어가게 했다 |
| **manifest/lock/ProjectSettings가 계속 변경됨** | 정상 동작이었다. 부트스트랩이 Input System을 설치하고 Unity가 그 결과를 파일에 쓴 것이다. **Discard → 재시작 → 재설치**가 반복된 것 | `com.unity.inputsystem 1.20.0`을 manifest와 lock에 **고정해서 커밋**했다. 이제 새 PC는 설치 단계 없이 같은 버전을 받고, 파일이 저절로 바뀌지 않는다 |

**Input System 실제 버전은 `1.20.0`이다.** 레지스트리 접근이 막혀 추측할 수 없었는데, 사용자 PC의 lock 파일 diff에서 확인했다.

`NetworkTheme.tss`에 기본 테마를 추가하는 것(`@import url("unity-theme://default");`)은 **아직 하지 않았다.** 구문이 틀리면 TSS import가 실패해 테마가 null이 되고 패널이 통째로 안 보이게 되므로, 화면이 나오는 것을 먼저 확인한 뒤 별도로 시도한다. 테마가 null이면 이제 Console에 오류를 남긴다.

### Safe Mode 두 번 — 원인과 최종 구조

| 회차 | 에러 | 원인 |
|---|---|---|
| 1 | `CS0234 'EventSystems' does not exist` | manifest에 없는 uGUI를 참조했다. 코드를 삭제해 해결 |
| 2 | `CS0246 'InputAction' could not be found` | **`ENABLE_INPUT_SYSTEM`을 "패키지가 있음"으로 잘못 알았다.** 이 define은 **Active Input Handling 설정**을 따라간다. 설정을 Both로 바꿔놨으니 패키지가 없는 PC에서도 `#if` 블록이 컴파일되어 터졌다 |

**치명적인 점:** Unity는 Safe Mode에서 `[InitializeOnLoad]`를 실행하지 않는다. 즉 **컴파일이 깨지면 패키지를 설치해줄 부트스트랩도 못 돈다.** 그래서 "패키지가 하나도 없는 상태에서도 반드시 컴파일될 것"이 절대 조건이다.

최종 구조는 선택적 패키지를 **전부 같은 방식**으로 막는다. Steamworks가 원래 쓰던 검증된 패턴이다.

| 어셈블리 | 게이트 | 패키지 없을 때 |
|---|---|---|
| `ChessFight.Network.Core` | 없음 (순수 C#) | 컴파일 |
| `ChessFight.Game` | 없음 (UnityEngine만) | 컴파일 |
| `ChessFight.Network.Steam` | `CHESSFIGHT_STEAM` | **통째로 제외** |
| `ChessFight.Game.Steam` | `CHESSFIGHT_STEAM` | **통째로 제외** |
| `ChessFight.Game.Input` | `CHESSFIGHT_INPUTSYSTEM` | **통째로 제외** |
| Assembly-CSharp(-Editor) | 없음 | 컴파일 (설치 도구가 여기 있다) |

`defineConstraints`가 충족되지 않으면 어셈블리 자체가 컴파일 대상에서 빠지므로, 그 안의 패키지 참조도 문제가 되지 않는다.

**규칙 두 가지.**

1. **선택적 패키지를 쓰는 코드는 반드시 자기 asmdef에 `versionDefines` + `defineConstraints`로 가둔다.** asmdef 없이 Assembly-CSharp에 두면 프로젝트 전체가 Safe Mode로 떨어진다.
2. **`ENABLE_INPUT_SYSTEM`을 패키지 존재 여부로 쓰지 않는다.** 그건 설정값이다. 패키지 존재는 `CHESSFIGHT_INPUTSYSTEM`(asmdef가 만든 것), 백엔드 활성화는 `ENABLE_INPUT_SYSTEM`으로 구분한다. 후자는 "패키지는 있는데 Active Input Handling이 Old"인 경우 입력이 조용히 0이 되는 것을 막는 데 쓴다.

현재 manifest에 uGUI(`com.unity.ugui`)와 Input System은 **없다.** Input System은 에디터 첫 실행 시 `NetworkSetup`이 버전 고정 없이 설치한다(레지스트리 접근이 막혀 있어 정확한 버전을 찍을 수 없었다). **설치 후 생성되는 `manifest.json`과 `packages-lock.json`을 커밋해야 다른 PC가 같은 버전으로 고정된다.** 커밋 전까지는 PC마다 입력 백엔드가 다를 수 있고, 그 상태는 HUD의 `Input:` 줄과 `ChessFight > Setup > Report input backend`로 확인한다.

**검증되지 않은 사항 — 에디터에서 반드시 먼저 확인한다.**

1. `.prefab` / `.mat` / `.unity` 파일은 **손으로 작성한 Unity YAML**이다. Unity가 정상 import 하는지, 캡슐·체스판이 이전과 같게 보이는지 확인해야 한다.
2. `com.unity.inputsystem`은 manifest에 **버전을 고정하지 않았다.** `ChessFight > Setup > Install dependencies`가 에디터에 맞는 버전을 설치한다. 설치 후 manifest/lock을 커밋한다.
3. `ProjectSettings.asset`의 `activeInputHandler`를 `0`(Legacy) → `2`(Both)로 바꿨다. 에디터가 켜진 상태였다면 재시작을 요구할 수 있다.
4. 봇은 Steam 계정이 없다. 로비에 입장하지 않고 예약 정원만 차지하며 호스트가 이동을 시뮬레이션한다. 실제 화면에서 봇 캡슐이 보이고 움직이는지 확인해야 한다.
5. 12인 봇 충원 시 호스트 CPU·대역폭은 측정하지 않았다.

## 관찰된 경고와 한계

- `The referenced script (Unknown) on this Behaviour is missing!` 경고는 `SampleScene`의 URP 컴포넌트 3개를 제거해 해결했다(미검증). 이 브랜치에는 URP 패키지가 없으며 테스트 공간은 실행 중 Built-in 렌더링을 사용한다.
- 현재 Start 버튼은 실제 게임 규칙 시작이나 씬 전환이 아니라 경기 로비 입장 마감이다.
- UI 힌트의 `\n` 문자 표시는 UXML에서 `&#10;`으로 수정했다(미검증). 긴 명단, 다양한 해상도, 한글 이름 전체 지원은 별도 확인이 필요하다.
- Start 버튼은 이제 실제 시작 조건(비공개 2명 이상 / 공개 12명)과 같은 조건에서만 활성화된다(미검증).
- Player.log는 현재 도구에서 파일 읽기가 거부되었다. 독립 실행 빌드 확인은 실제 화면 관찰에 근거하며, Player 로그에 오류가 전혀 없다고 주장하지 않는다.
- 원본 프로젝트의 UnityConnectSettings.asset 자동 변경은 Steam 관련 변경이 아니어서 미커밋으로 남겼다.

## 실행한 자동 검증과 해석

`Tools/Test-NetworkCore.ps1`: 28개 Core + 12개 세션 검사(2026-09-24 연결 품질·버전·봇 규칙·Rich Presence 추가). Core는 Unity에 의존하지 않는다. 모의 서비스는 로비·이벤트·소유권·정원을 재현하지만 Steam 백엔드 전파와 실제 P2P를 재현하지 않는다. SteamMotion의 예측·보정 통합 검사가 아니다.

`Tools/Test-NetworkCompile.ps1`: Unity .NET Standard 2.1 및 UnityEngine/UnityEditor 어셈블리와 실제 Steamworks 소스를 사용한다. 더미 API를 사용한 컴파일을 실제 패키지 호환성 검증으로 대체하지 않는다.

## 과거 장애 이력

초기 구현은 별도 복사본에서 수행했다. 그 환경에서 headless Unity의 Licensing Client IPC 연결이 실패했고 Git HTTPS도 제한되었다. 그래서 첫 커밋에는 설치 부트스트랩만 있었다. 이후 사용자 원본 프로젝트를 Pull하고 Unity Hub의 실제 Editor로 설치·Play·빌드를 완료했다. **과거의 전체 런타임 검증 차단 기록은 현재 상태가 아니다.**

GitHub 앱 쓰기 403은 사용자 GitHub Desktop으로 Push하여 해결한 작업 도구 문제다. 초기 구현 87805f0과 실제 설치 결과 c9c1e6a가 Network에 반영되었다.

## 다음 검증 우선순위

1. ~~에디터가 새 구조를 여는지~~ — **2026-09-24 확인됨.** 로비 씬(현 `Lobby`)의 프리팹·재질·HUD 정상, 한글(맑은 고딕) 정상.
2. ~~UI 버튼이 눌리는지~~ — **2026-09-24 확인됨** (Active Input Handling을 Old로 되돌린 뒤). 게임 내 친구 목록·초대도 동작 확인.
3. **Input System 설치와 이동.** 패키지 설치 후 WASD/Space가 동작하는지, HUD의 `Input:` 줄이 Input System을 가리키는지.
4. **두 계정 양방향 이동·점프.** 매칭까지는 확인했으므로 이제 실제로 서로의 캡슐이 움직이는지 확인한다.
5. **봇 1인 테스트.** `비공개 방 만들기` + 파티 봇 5 → 6인, 이어서 `12명 채우기` → 12인. 봇이 보이고 움직이는지, 프레임과 대역폭이 견디는지.
6. **봇 2계정 테스트.** 각 PC가 1인 + 봇 5로 `게임 시작 → 빠른 매칭` → 6v6 자동 시작. 상대 PC에서도 봇이 보이는지.
7. 파티장/파티원 취소, 구성 변경, 호스트 정상 종료 및 강제 종료 후 명단/캡슐 정리.
8. 실제 6+6, 3+3+2+2+1+1, 4+4+4 거절, 부분 입장 실패.
9. 입력 손실 시 점프, 지연 시 예측 보정, 호스트 프레임 저하, 장시간 세션 측정.

설계와 재현 절차: [AI 인수인계](../AI/NETWORK_HANDOFF_KO.md), [사용자 실행 안내](README.md).

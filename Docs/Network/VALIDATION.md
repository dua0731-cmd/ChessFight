# Network 구현 검증 기록

최종 갱신: 2026-09-22 (KST). 코드 기준: `Network` 브랜치 (구조 개편 + AI 봇 커밋).
대상: Unity `6000.3.11f1`, Windows x64, Steamworks.NET `2025.164.1`.

상태: **핵심 로직·모의 세션 검사 통과 / 두 PC·두 계정의 파티 입장과 공개 매칭 성사 확인 / 두 계정 이동 동기화는 사용자 보고 없음 / 구조 개편과 AI 봇은 Unity 실기 미검증**.

> 이 문서의 표는 **사용자가 실제로 확인했다고 보고한 것만** '성공'으로 적는다.
> 코드 작성·코드 검토는 검증이 아니다.

## 확인 결과

| 항목 | 결과 | 근거와 한계 |
|---|---|---|
| Core C# 테스트 | 통과, 23개 | 정원·중복·입장 실패·만료, 1,000회 무작위 예약, 패킷 검증, 이동·점프 수식, 봇 ID·소유권·충원·로밍. 실제 물리/통신 검증 아님 |
| 세션 상태 테스트 | 통과, 7개 | 생산 SteamSession.cs를 모의 Steam 서비스와 실행. 파티 동행, 팀 입장, 취소, 늦은 콜백, 부분 실패, 호스트 이탈, 12개 동시 검색 수렴 |
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
| 씬 | `Assets/Scenes/ChessFightLab.unity` 신규(유일한 시작 씬). URP 잔여 참조 없음 |
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

`Tools/Test-NetworkCore.ps1`: 23개 Core + 7개 세션 검사(봇 5개 추가). Core는 Unity에 의존하지 않는다. 모의 서비스는 로비·이벤트·소유권·정원을 재현하지만 Steam 백엔드 전파와 실제 P2P를 재현하지 않는다. SteamMotion의 예측·보정 통합 검사가 아니다.

`Tools/Test-NetworkCompile.ps1`: Unity .NET Standard 2.1 및 UnityEngine/UnityEditor 어셈블리와 실제 Steamworks 소스를 사용한다. 더미 API를 사용한 컴파일을 실제 패키지 호환성 검증으로 대체하지 않는다.

## 과거 장애 이력

초기 구현은 별도 복사본에서 수행했다. 그 환경에서 headless Unity의 Licensing Client IPC 연결이 실패했고 Git HTTPS도 제한되었다. 그래서 첫 커밋에는 설치 부트스트랩만 있었다. 이후 사용자 원본 프로젝트를 Pull하고 Unity Hub의 실제 Editor로 설치·Play·빌드를 완료했다. **과거의 전체 런타임 검증 차단 기록은 현재 상태가 아니다.**

GitHub 앱 쓰기 403은 사용자 GitHub Desktop으로 Push하여 해결한 작업 도구 문제다. 초기 구현 87805f0과 실제 설치 결과 c9c1e6a가 Network에 반영되었다.

## 다음 검증 우선순위

1. **에디터가 새 구조를 여는지.** `ChessFightLab` 씬을 열고 프리팹·재질·UI가 깨지지 않는지, Play 화면이 이전과 같은지.
2. **UI 버튼이 눌리는지.** 눌리지 않으면 HUD의 `Steam:` / `Input:` 줄과 Console 로그를 확인한다.
3. **Input System 설치와 이동.** 패키지 설치 후 WASD/Space가 동작하는지, HUD의 `Input:` 줄이 Input System을 가리키는지.
4. **두 계정 양방향 이동·점프.** 매칭까지는 확인했으므로 이제 실제로 서로의 캡슐이 움직이는지 확인한다.
5. **봇 1인 테스트.** 비공개 방 + 파티 봇 5 → 6인, 이어서 Fill room to 12 → 12인. 봇이 보이고 움직이는지, 프레임과 대역폭이 견디는지.
6. **봇 2계정 테스트.** 각 PC가 1인 + 봇 5로 Quick match → 6v6 자동 시작. 상대 PC에서도 봇이 보이는지.
7. 파티장/파티원 취소, 구성 변경, 호스트 정상 종료 및 강제 종료 후 명단/캡슐 정리.
8. 실제 6+6, 3+3+2+2+1+1, 4+4+4 거절, 부분 입장 실패.
9. 입력 손실 시 점프, 지연 시 예측 보정, 호스트 프레임 저하, 장시간 세션 측정.

설계와 재현 절차: [AI 인수인계](../AI/NETWORK_HANDOFF_KO.md), [사용자 실행 안내](README.md).

# 실제로 겪은 사고와 재발 방지

모두 이 프로젝트에서 **실제로 일어난 일**이다. 비슷한 증상이 보이면 여기부터 본다. 새 사고를 겪으면 맨 아래에 추가한다.

## 증상 → 원인 → 해결

| # | 증상 | 원인 | 해결·재발 방지 | 관련 |
|---|---|---|---|---|
| 1 | **HUD가 보이는데 아무것도 안 눌림.** 로그도 없음 | Active Input Handling을 Both/New로 바꿈. uGUI가 없어 UI Toolkit 런타임 패널이 EventSystem 경로를 못 쓰고, 자체 이벤트 시스템은 Input System 백엔드에서 포인터·키 이벤트를 못 받는다 | **Old(0)로 둔다.** `InputSettingsGuard`가 강제하고, 켜진 Editor가 설정 파일을 덮어쓰는 문제 때문에 SerializedObject로 쓴다. 적용 후 Unity 재시작. 그래도 안 되면 `NetworkHudView`의 대체 클릭 경로(panel.Pick)가 동작 | R8, R9 |
| 2 | **Safe Mode** + CS0234 `UnityEngine.EventSystems` | uGUI가 없는데 EventSystem 코드를 넣음 | 없는 패키지를 Assembly-CSharp에서 참조하지 않는다 | R4 |
| 3 | **Safe Mode** + CS0246 `InputAction` (다른 PC, 또는 로컬 변경을 지운 뒤) | `ENABLE_INPUT_SYSTEM`을 "패키지 설치됨"으로 착각. 이 define은 **설정**을 따르므로 패키지가 없어도 정의된다 | 선택 패키지 코드는 전용 asmdef(`ChessFight.Game.Input`)에 두고 `versionDefines`로 `CHESSFIGHT_INPUTSYSTEM` 정의 + `defineConstraints`로 게이트. **Safe Mode에서는 `[InitializeOnLoad]`가 돌지 않으므로 패키지 없이도 컴파일되어야 한다** | R5 |
| 4 | **Play해도 HUD가 전혀 안 보임** | `ScrollView`가 기본 테마 없이 크기 0으로 무너짐 | 프로젝트 TSS는 기본 테마를 가져오지 않는다. 복합 컨트롤(ScrollView, Dropdown 등) 금지, 평범한 VisualElement + `flex-shrink: 0` | R6 |
| 5 | **변경점이 저절로 생기고** Discard하면 재시작 요구 후 또 생김 (manifest, lock, ProjectSettings) | 설치 스크립트가 버전 없이 패키지를 추가해 PC마다 결과가 다름. Discard하면 다시 설치 | 버전을 manifest/lock에 **고정하고 커밋**. 설치 결과는 절대 Discard하지 않는다 | R6 |
| 6 | `The referenced script (Unknown) on this Behaviour is missing!` (MainCamera 등) | URP 패키지가 없는데 템플릿 씬에 URP 컴포넌트(UniversalAdditionalCameraData, Volume 등)가 남음 | SampleScene에서 제거. 새 씬에는 URP 컴포넌트를 넣지 않는다 | R3 |
| 7 | 한글이 네모로 나옴 | 기본 LegacyRuntime 폰트에 한글 없음 | `RuntimePanels.KoreanFont`가 OS 폰트(맑은 고딕 → 나눔고딕 → Noto Sans KR)를 잡는다. Console의 폰트 로그 확인 | R7 |
| 8 | 씬을 바꾸면 파티가 끊길 구조 | 로비 씬 오브젝트가 Steam 세션을 소유 | `NetworkRuntime`(DontDestroyOnLoad)이 단일 소유 | R11 |
| 9 | 래그돌 골반이 주저앉음 | 물리 50Hz | 해당 씬에 `PhysicsProfile`(120Hz/24회) | R11 |
| 10 | 장애물이 PC마다 다른 위치 (래그돌 랩 `SpinningBar`) | `angle += 속도 × dt` 누적 | 시간의 순수 함수(`Obstacle.Evaluate`) | R11 |
| 11 | 시간 기반 회전이 흔들림 | Steam 서버 시계(유닉스 초)가 커서 float 곱셈 정밀도 손실 | `Obstacle.WrapDegrees`로 감싸고 double로 계산 | R11 |
| 12 | 코드 내용이 Unity에 안 보임 | 다른 폴더(작업 복사본)에 Push하고 Unity가 여는 원본에는 Pull하지 않음 | 실제 경로 확인 후 Pull | 이전 도구 |
| 13 | Steam 초기화 실패 후 복구 불가 | Steam을 켜기 전에 Play | HUD의 `Steam 다시 연결` 버튼(`SteamSession.Retry`) | R3 |
| 15 | 브랜치 병합 때 래그돌 파일 118개가 `Assets/Scripts/RagdollLab/`로 옮겨지는 충돌 | `Network`가 예전에 `Assets/ChessFight/*`를 옮긴 이력을 git이 "폴더 이름 변경"으로 보고 새 파일까지 따라 옮기려 함 | `git merge --abort` 후 `git -c merge.directoryRenames=false merge …`. 옛 구조에서 갈라진 브랜치를 병합할 때 같은 문제가 난다 | R16 |
| 16 | Pull 후 `Assets/ChessFight.meta` 같은 폴더 메타가 저절로 생김 | 폴더는 병합으로 되살아났는데 폴더 `.meta`는 예전에 지워져 있었다. Unity가 PC마다 다른 GUID로 만든다 | 원래 GUID로 `.meta`를 커밋한다(`git show <옛 커밋>:경로.meta`) | R16 |
| 17 | Unity를 켤 때마다 **"Input System native platform backend not enabled"** 창(Enable Restart / Don't Enable) | Input System 패키지는 설치돼 있는데 Active Input Handling은 Old. `Enable Restart`를 누르면 HUD 클릭이 죽고 `InputSettingsGuard`가 되돌리며 또 재시작 요구 | 패키지 제거(R20), `NetworkSetup`의 자동 설치에서도 뺌. 이미 눌렀다면 Pull 후 Unity 재시작, Player 설정이 Old인지 확인 | R20 |
| 18 | **위쪽 카드는 보이는데 아래쪽에 붙인 카드가 전부 안 보임**(새 로비의 파티 바·모드 카드·게임 시작) | UIDocument 루트를 화면 전체로 늘리는 규칙은 Unity **기본 테마**에 있다. 우리 테마는 기본 테마를 안 가져오므로(U3) 루트 높이 = 내용 높이. 카드를 전부 `position: absolute`로 두면 내용 높이가 0이 되어 `bottom:`으로 붙인 것이 화면 위쪽 밖으로 나간다 | `RuntimePanels.Create`가 `root.StretchToParentSize()`로 루트를 늘린다. 새 HUD도 이 함수로 만든다 | R22 |
| 14 | 문서가 코드와 어긋남 (예: Both로 바꿨다는 옛 문장) | 여러 도구가 문서를 부분만 갱신 | [HANDOFF §8](../../HANDOFF.md) 체크리스트. 옛 문서는 삭제하거나 새 트리로 안내 | R15 |

## AI 도구 작업 시 주의

- 쉘 heredoc 안에 복잡한 따옴표가 든 커밋 메시지를 넣으면 쉘 문법 오류가 난다. 메시지는 파일로 써서 `git commit -F`로 넣는다.
- `mcs`(Mono 컴파일러)는 최신 문법(튜플 분해, 괄호 식에 메서드 호출 등)을 못 읽는다. 래그돌 코드는 mcs로 컴파일되지 않는다. 그래서 `Tools/run-tests-linux.sh --compile`은 **Roslyn**(.NET 8 SDK의 csc, Unity와 같은 컴파일러)을 쓴다. 테스트 실행에만 mcs를 쓰므로 **Core·SteamSession·테스트 코드는 mcs가 읽을 수 있게**(이름 있는 튜플 대신 작은 struct) 유지한다.
- 참조 DLL이 Unity 2021.3이라 Unity 6에서 바뀐 이름(`linearVelocity`, `linearDamping`, `PhysicsMaterial` 등)은 스크립트가 임시 복사본에서만 옛 이름으로 바꾼다. 새 Unity 6 API를 쓰다 컴파일 검사가 실패하면 `unity6_to_2021`에 규칙을 추가한다.
- 이름 충돌: `CameraRig`의 속성 이름을 `Camera`로 두면 `UnityEngine.Camera` 타입을 가린다(`Target`으로 바꿈).
- Unity가 한 번 저장한 씬·프리팹에 생성기(`Tools/Generators/gen_scenes.py`)를 다시 돌리면 편집 내용이 사라진다.
- 이전 도구 시절: GitHub 연동 앱이 저장소 쓰기에 403을 냈다. 사용자가 GitHub Desktop으로 커밋·푸시해 해결했다. 권한 문제는 코드 오류가 아니다. 도구마다 쓰기 권한을 먼저 확인한다.
- 이전 도구 시절: 샌드박스의 headless Unity가 라이선스 IPC 문제로 실패했다. 이후 사용자 Unity Hub에서 빌드에 성공했다. 과거 장애를 지금의 검증 불가 사유로 재사용하지 않는다.
- Steam 로그인은 사용자가 직접 한다. 비밀번호·인증 코드·토큰을 문서나 저장소에 적지 않는다.

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
| 19 | 래그돌이 **물에서 부활한 직후 다시 물 쪽으로 초속 20 m로 끌려감**(상자를 쥔 채 빠졌을 때). 로그: `Destroying components immediately is not permitted during physics trigger/contact…` | 충돌로 넘어질 때(`OnCollisionEnter` 안) 손을 놓으며 `DestroyImmediate`로 잡기 관절을 지웠는데 Unity가 물리 콜백 안에서는 거부했다. 관절은 남고 추적만 끊겨, 일어나 다시 잡으면 관절이 2개가 됐고, 순간이동 뒤 남은 관절이 폰을 끌고 갔다 | `PawnHand`가 손을 놓을 때 그 손의 잡기 관절을 **전부** 지우고, 물리 콜백 안이면 `Destroy`(프레임 끝)로 미룬다(`PhysicsCallbackDepth`). **물리 콜백 안에서는 순간이동·관절 삭제를 하지 않고 다음 Update로 미룬다**(HANDOFF 규칙 14). 자동 점검 `M4 물: 무언가를 잡은 채…`가 손 관절 수와 부활 직후 속도를 본다 | R34 |
| 20 | 래그돌이 **벽에 몸을 대고 W를 누르고 있으면 뒤로 튕겨 나감** — 등반으로 턱에 올라선 뒤 W를 조금 더 누르면 다음 블록 벽에서 튕겨 턱 아래로 떨어짐(플레이 영상의 "되튐") | 이동 앵커(kinematic)가 벽 속으로 최대 0.6 m 앞서 몸을 누르고 있다가, 벽에서 0.28 m/s로 살짝 되튀는 것을 "반대 방향 전환"(`anchorBrakeLeash`)으로 착각해 **앵커를 한 스텝에 0.3 m 뒤로 옮겼다.** kinematic 몸의 순간이동은 PhysX에서 초속 33 m 속도가 되고, 관절 감쇠기가 그 속도로 골반을 끌어 **3.8 m/s로 뒤로 걷어찼다** | 제동은 몸이 실제로 1 m/s 넘게 반대로 달릴 때만(`RagdollPawn.BrakeMinSpeed`). **kinematic 관절 목표(앵커)는 한 스텝에 크게 옮기지 않는다** — 옮긴 거리가 곧 속도다. 자동 점검 `등반: 턱에 올라선 뒤 뒤로 떨어지지 않음` | R36 |
| 21 | 자동 점검 9개가 **처음부터 계속 실패** — 그중 4개는 동작이 아니라 **점검이 틀렸다**(끝난 뒤의 속도를 잼, 두 폰의 출발선이 다름, 올라선 뒤에도 앞으로 계속 누름) | 점검을 Linux 세션에서 **Unity 없이 쓰고 한 번도 돌리지 않은 채** 커밋했다. 파이썬 모델로 잰 값은 실제 PhysX와 달랐다(벽의 튀어나온 밑면, 머리 박치기, 앵커 되튐) | 새 점검은 **빌드한 랩 플레이어에서 한 번 이상 돌려 보고** 커밋한다([RagdollLab README "자동 점검"](../RagdollLab/README.md#자동-점검)의 사본 빌드 절차, `-ragdollOnly`로 일부만). 실패하면 먼저 추적값을 찍어 점검과 동작 중 무엇이 틀렸는지 가른다 | R36 |
| 17 | 온라인에서 방장이 아닌 PC의 **움직이는 장애물이 전부** 판정과 어긋남("안 맞았는데 맞음") | 참가자가 장애물을 자기 Steam 시계로 돌림. Steam 서버 시계는 초 단위이고 PC 간 정밀도가 없으며, 인형은 방장 스냅샷 시각(전송 지연 + 110 ms 과거)으로 그려져 두 시계가 다름. 랩 회전 봉은 각도를 PC마다 누적해 더 심함 | 방장이 스냅샷에 장애물 시각을 싣고 참가자는 그 시각으로 장애물을 돌린다(`HostObstacleClock`). 스스로 움직이는 물체는 `Obstacle` 상속(테스트 스크립트가 검사). **공유 시계는 "같은 시계"가 아니라 "같은 장면"이어야 한다: 인형과 장애물은 같은 시점으로 그린다** | R51(`JY-lobby`) |
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

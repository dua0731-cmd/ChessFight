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
| 25 | 온라인에서 방장이 아닌 PC의 **움직이는 장애물이 전부** 판정과 어긋남("안 맞았는데 맞음") | 참가자가 장애물을 자기 Steam 시계로 돌림. Steam 서버 시계는 초 단위이고 PC 간 정밀도가 없으며, 인형은 방장 스냅샷 시각(전송 지연 + 110 ms 과거)으로 그려져 두 시계가 다름. 랩 회전 봉은 각도를 PC마다 누적해 더 심함 | 방장이 스냅샷에 장애물 시각을 싣고 참가자는 그 시각으로 장애물을 돌린다(`HostObstacleClock`). 스스로 움직이는 물체는 `Obstacle` 상속(테스트 스크립트가 검사). **공유 시계는 "같은 시계"가 아니라 "같은 장면"이어야 한다: 인형과 장애물은 같은 시점으로 그린다** | R51(`JY-lobby`) |
| 14 | 문서가 코드와 어긋남 (예: Both로 바꿨다는 옛 문장) | 여러 도구가 문서를 부분만 갱신 | [HANDOFF §8](../../HANDOFF.md) 체크리스트. 옛 문서는 삭제하거나 새 트리로 안내 | R15 |
| 24 | **로딩 화면이 안 뜨고** 경기 씬이 그냥 열림. Console: `AssertionException: Assertion failure. Values are not equal.` (`UIDocument.set_panelSettings`, `MatchLoader.cs` 70) | 채팅(`ChatBox`)의 UIDocument를 `NetworkRuntime` 오브젝트에 직접 붙였다. 그 **자식 오브젝트**인 로딩 화면의 UIDocument가 채팅 문서의 자식 문서가 되고, 부모와 다른 PanelSettings를 받자 Unity가 Assert. `MatchLoader`는 이 오류를 잡고 로딩 화면 없이 씬을 불러서 경기는 되지만 화면은 없다 | UIDocument끼리는 부모·자식 오브젝트에 두지 않는다. 자기 PanelSettings(정렬 순서)를 가진 화면은 **형제 오브젝트**로(`ChessFight Network Runtime` 아래 `Chat`, `Loading Screen`). `3bf8981`에서 고침(AI가 Unity에서 확인) | R61 |
| 26 | 코드로 만든 어두운 홀인데 **반짝이는 바닥·체스판·말이 파랗게** 보임(R76 첫 결과 화면) | 씬의 기본 반사가 템플릿 씬의 파란 절차 하늘에서 구워진 것. Play 중에 `RenderSettings.skybox`를 바꿔도 반사는 그대로 | 반사를 코드로 만든 큐브맵으로: `RenderSettings.defaultReflectionMode = Custom`, `customReflectionTexture`(`PawnRushResultStage.HallReflection`). 앰비언트도 직접 정한다 | R76 |
| 27 | UI Toolkit 글자가 **안 보이거나 다른 글자가 엉뚱한 곳에** 찍힘. Console: `Unable to load font face for [Cascadia Mono]`, `Can't Generate Mesh, No Font Asset has been assigned.` | `Font.CreateDynamicFontFromOSFont(이름 목록)`의 첫 이름이 이 PC에 없음. 그 글꼴을 쓴 라벨이 망가지고 이웃 라벨까지 깨져 보임 | `Font.GetOSInstalledFontNames()`로 있는 이름 하나를 골라 만든다(`PawnRushResultHud.Build`) | R76 |
| 28 | Painter2D로 그린 반투명 줄·판이 **불투명**하게 나옴 | `FillGradient.MakeLinearGradient(Color, Color, …)`(두 색 도우미)가 알파를 버림 | 알파 키를 넣은 `Gradient`로 만든다(`PawnRushResultHud.Paint.Fill`) | R76 |
| 29 | Windows `Tools/Test-NetworkCompile.ps1`이 **Ragdoll lab compilation failed**(`PawnRushSkills`·`SkillSpeedScale`·`PreSkills` 등이 없다는 CS0103) | 스크립트가 `RagdollLab/Scripts`의 맨 위 `.cs`만 읽고 R71에 생긴 하위 폴더 `PawnRushSkills/`의 partial 파일을 빠뜨림(코드는 멀쩡함, Unity는 컴파일됨) | `-Recurse`로 하위 폴더까지 읽게 고침(R76 병합 때). 스크립트 폴더를 새로 나누면 컴파일 도구가 그 폴더를 읽는지 확인한다 | R76 |
| 30 | Unity UI의 반투명 색이 웹 시안과 다르게 보임: 밝은 테두리·옅은 바탕은 **훨씬 진하게**, 어두운 그늘·판은 **훨씬 옅게**(뒤가 비침) | 프로젝트가 선형(Linear) 색 공간이라 UI Toolkit도 선형으로 섞는다. 시안(CSS)의 알파는 sRGB에서 섞은 값이다 | 시안 값을 그대로 쓰지 않는다: 어두운 것은 `StageKit.Darkening(a)`(.78 → .96), 어두운 바탕 위 밝은 것은 sRGB 곡선(`Mathf.GammaToLinearSpace`, .38 → .12). 그라데이션은 `MenuArt.Ramp(…, HudBlend.Shade/Tint)`, 모드 카드처럼 겹이 많은 판은 sRGB로 합친 불투명 텍스처(`MenuArt.LineCard`) | R83 |
| 31 | 라벨 글자가 **한 글자씩 세로로 줄바꿈**(로비 "플레이" 탭) | UI Toolkit의 Label(TextElement)에 자식 요소를 붙이면 글자 크기를 재지 않아 폭이 0이 된다 | 밑줄 같은 장식은 라벨을 감싼 상자에 붙인다(`a-tab-box`). 버튼에 아이콘을 넣을 때는 글자를 자식 Label로 옮긴다(`NetworkHudView.IconText`) | R83 |
| 32 | 게임을 켜면(에디터 Play) **시작 로고가 영상이 나오기 전에 1초 만에 사라짐**. 코드로 다시 부르면 끝까지 잘 나옴 | Play 뒤 첫 프레임까지 3.7초가 걸리는데(씬·Steam 준비), `Time.unscaledTime`으로 잰 "영상 준비 기다림 4초"에 그 시간이 들어가 영상 준비(약 1.6초)가 끝나기 전에 포기함 | 기다림·연출 시간은 **보인 프레임으로 센다**: 한 프레임은 최대 0.05초(`LogoIntro.shown`, `SceneTransition.clock`). 로딩으로 멈춘 프레임이 기다림을 다 써 버리거나 움직임을 건너뛰게 하지 않는다 | R84 |
| 33 | **빌드에서만** 스킬 이펙트가 맞는 순간부터 안 나오고 신호가 약 1초 막힘. 플레이어 기록: `ArgumentNullException: Value cannot be null. Parameter name: shader` | `Shader.Find("Unlit/Color")`(URP Unlit도)가 플레이어 빌드에 없다. 편집기에서는 찾아서 편집기 시험으로는 안 보인다. `new Material(null)`이 오류를 내고 그 이펙트 처리가 거기서 멈췄다 | 빌드에 늘 있는 `Sprites/Default`로 대신(`SkillInkKit.Flat`, `QueenHillSkillFx.Kit.Flat`, `PawnRushSkillFx.Unlit`). `Shader.Find`로 찾는 셰이더는 빌드에도 들어가는지(Resources 아래, Always Included) 확인하고 null에 대비한다. 온라인 신호 처리는 하나가 오류를 내도 다음 것을 그린다(`LabNetLink.DispatchEvents`) | R111 |
| 34 | F3 창 왕복 지연이 재생 지연만큼 들쑥날쑥(+300 ms에서 396 ms), 참가자가 들어오자마자 몇 초짜리 값 | 스냅샷을 **그릴 때**(재생 지연 뒤) 쟀고, 입력을 아직 못 들은 사람에게 방장이 자기 시계를 되돌려 줘서 참가자가 서로 다른 두 시계를 뺐다 | 도착할 때 재고, 못 들은 사람에게는 0(`LabNetLink.ReceiveSnapshot`). 두 PC의 시계를 빼지 않는다 | R111 |
| 35 | 손실 10%에서 재생 지연의 "한 장 더"가 1초에 한 번 넘게 켜졌다 꺼짐(로컬 시험에서는 꺼진 채 남음) | 켜고 끄는 기준이 한 줄(8%)이고 1초 평균이라 10% 근처에서 흔들렸다 | 켜짐 8%·꺼짐 5%, 4초 평균(`PlaybackDelay`). 기준은 작은 시뮬레이션 프로그램으로 바뀌는 횟수를 세어 정했고, Core 시험이 "손실 10%에서 100초 동안 95% 넘게 켜져 있고 2번 이하로 바뀜"을 본다 | R111 |
| 36 | (막은 사고) 따로 쓰려던 브랜치의 커밋이 `C:/Work/ChessFight`(bold-johnson)에 섞일 뻔함 | 커밋 훅(`~/.claude/scripts/chessfight-sync-unity.sh`)은 워크트리 브랜치가 `claude/*`이면 그 커밋을 `C:/Work/ChessFight`의 지금 브랜치로 빨리 감는다. 새 브랜치가 bold-johnson에서 갈라졌으면 빨리 감기가 그대로 된다 | bold-johnson과 섞이면 안 되는 브랜치는 `claude/` 없이 짓는다(`skill-network-test`). 그 브랜치의 Unity 폴더(`C:\Work\ChessFight-SkillNet`)는 손으로 맞춘다: `git -C C:/Work/ChessFight-SkillNet fetch <워크트리 저장소> skill-network-test` → `merge --ff-only FETCH_HEAD` | R112 |
| 37 | 시험 브랜치와 bold-johnson을 서로 합칠 때 스킬 시험 씬이 지워지거나 되살아남 | bold-johnson은 씬을 지웠고(`9a94131`) 시험 브랜치는 둔다 | 시험 브랜치에는 `9a94131`을 `merge -s ours`로 기록해 두어 bold-johnson → 시험 브랜치 합치기는 씬을 안 지운다. 시험 브랜치 → bold-johnson으로 합치면 씬이 되살아나니 그 뒤 `Assets/Scenes/SkillTest`를 다시 지운다(DECISIONS SK9) | R112 |
| 38 | 온라인 폰 러쉬(코스 01)에서 방장 화면만 맞고, 다른 PC에서는 다른 플레이어가 실제와 다른 곳에 보임(10-11, 5인 시험) | 래그돌 스냅샷 위치를 16비트로 ±80 m(`RagdollNetProtocol.PositionRange`)까지만 담음. 랩(작은 경기장)에 맞춘 값을 길이 246 m 코스에 그대로 써서 z 80 m 너머의 폰이 참가자 화면에서 80 m에 붙음. 방장은 자기 물리를 그대로 그려서 맞게 보임 | 범위 ±320 m(약 1 cm 단위), `CFRA`·v19. **새 맵을 온라인에 올릴 때는 맵 전체가 `PositionRange` 안에 드는지 먼저 본다.** 스킬 순간 신호의 위치(`Core/SkillSync.cs` `SkillWire.PositionRange`)는 아직 ±80 m라, 스킬을 코스 경기에 넣을 때 같이 넓혀야 한다 | R124 |
| 39 | 온라인 폰 러쉬에서 망치·떨어지는 기물·바람·도약대·컨베이어가 **모든 화면에서 맞는 것처럼 보이는데 아무도 밀리지 않음**(방장 포함). 오프라인에서는 됨 | 가져온 장애물이 "내가 판정하는 PC인가"를 `ObstacleContext.Authority` = "장애물 시계가 공유되지 않음"으로 읽었다. 경기에서는 방장도 공유 시계를 쓰므로 방장에서도 거짓 | `ObstacleClock.Authority`(오프라인 또는 방장, 경기 연결이 설정)로 읽는다. **판정 여부를 시계 종류로 추론하지 않는다** | R126 |
| 40 | 참가자 화면에서 장애물과 폰이 어긋나 "맞은 것 같은데 안 맞고, 안 맞은 것 같은데 맞음" | 참가자는 장애물을 공유 시계의 "지금"으로, 폰은 재생 지연(70~160 ms) 전 모습으로 그렸다. 방장 시계도 물리 걸음과 따로 흘렀다 | 방장은 물리 걸음으로 센 장애물 시각을 스냅샷에 찍고(공유 시계 쪽으로 천천히 맞춤), 참가자는 그리는 순간의 그 시각으로 장애물을 돌린다(`HostObstacleClock`). 상태(미니게임)도 그 순간에 적용(`CFP3` 대기열) | R126 |
| 41 | (막은 사고) 미니게임 효과음을 낼 때마다 다른 물결·빛기둥이 그 자리로 끌려감 | 효과음 목소리 16개를 효과 루트 하나에 붙여 두고 소리마다 루트를 옮겼다. 물결·빛기둥은 그 루트의 자식 | 목소리마다 자식 오브젝트(`ArenaFx.NewVoice`). 위치를 옮길 것은 따로 떼어 둔다 | R126 |
| 42 | 미니게임 손잡이를 잡고 밀면 **벽타기로 바뀌고**, 밀어야 할 조각상은 **아예 안 잡힘** | 우클릭 = 잡기 + 벽타기. 잡은 채 가파른 면 쪽으로 이동 입력이면 벽타기가 시작됨(키네마틱 물체도 오를 수 있는 면). 오르지 못하게 `NoClimbSurface`를 붙이면 손도 못 잡음(손잡이 블록을 따로 둔 이유) | `GrabOnlySurface`(잡히지만 안 오름, 볼트도 안 됨). **손으로 미는 물체에는 `NoClimbSurface`를 쓰지 않는다** | R128 |
| 43 | 새 HUD의 USS가 **하나도 안 먹음**: 모든 글자가 왼쪽 위에 줄줄이 쌓이고 판은 화면 가득 늘어남. 오류 메시지 없음 | `Resources.Load<StyleSheet>("VoiceHud")`가 같은 이름의 `VoiceHud.uxml` 안에 Unity가 만든 하위 에셋 **`inlineStyle`**(StyleSheet)을 돌려줌. `.uss`는 읽히지 않았다 | 코드에서 직접 읽는 `.uss`는 **UXML과 다른 이름**으로(`VoiceChat.uss`). 이름이 같은 짝(KingRushHud 등)은 `.tss` 테마가 `@import`로 읽어서 괜찮았다. 읽은 뒤 `AssetDatabase.GetAssetPath`로 어느 파일인지 확인하면 바로 보인다 | R131 |
| 44 | MCP 화면 캡처가 **3D 장면만 찍히고 UI가 하나도 없음**(새로 연 에디터) | Game 뷰가 다른 탭 뒤에 있어 그려지지 않았다. 캡처는 Game 뷰의 그림을 쓰므로 UI Toolkit 패널이 빠진다 | `execute_code`로 `EditorApplication.ExecuteMenuItem("Window/General/Game")` 뒤 Game 뷰 `Focus()`를 하고 찍는다 | R131 |
| 45 | URP를 깔았는데 **Play만 하면 거의 모든 것이 분홍**(편집 모드에서는 정상, 하늘·스프라이트·글자만 보임) | `Game/RenderPipelineOverride.cs`(URP 패키지가 없던 때의 우회)가 Play 시작(`BeforeSceneLoad`)에 URP 에셋 참조를 비워 Built-in으로 URP 셰이더를 그렸다 | 지움(R151). 렌더 파이프라인을 런타임에 바꾸는 코드를 두지 않는다. `Camera.Render()`는 URP 17에서도 그대로 된다 | R151 |
| 46 | URP에서 **스킬 블룸이 붙는 씬만 색이 바램**(하늘·바닥이 회색빛, 후처리를 끄면 정상) | 템플릿 URP 에셋(`PC_RPAsset`·`Mobile_RPAsset`)의 Volume Profile 칸에 `SampleSceneProfile`(Neutral 톤매핑·비네트 0.2)이 들어 있었다 | 에셋의 Volume Profile을 비웠다. 남은 것은 전역 기본 프로필(모두 중립)뿐 | R151 |
| 47 | URP로 바꾸니 **로비·결과 화면이 어두움**(스포트라이트 무대) | Built-in 점·스포트 빛은 1 / (1 + 25 (거리/범위)²)로, URP는 1 / 거리²로 줄어든다(먼 빛은 훨씬 어둡고, 가까운 빛은 훨씬 밝다) | `MenuArt.SpotIntensity`(비추는 곳에서 같게)·`PipelineSettings.PointIntensity`(범위 1/4에서 같게)로 세기를 준다 | R151 |
| 48 | 코드와 같은 설정으로 만든 **URP 재질 에셋을 저장하면 키워드가 바뀜**(검은 발광이면 `_EMISSION` 꺼짐, 반투명이면 `_ALPHAPREMULTIPLY_ON` 켜짐, 큐가 3000으로) | URP는 재질 에셋을 가져올 때 인스펙터와 같은 규칙으로 다시 맞춘다(반투명은 "반사 유지" = 셰이더에서 알파 곱하기) | `LitMaterials.MakeTransparent`를 URP 규칙과 같게 하고, 빌드용 조합 재질(`Resources/URPVariants`)의 발광은 검정이 아닌 값으로 둔다 | R151 |
| 49 | **기물 모양이 바뀐 직후 그 기물이 안 그려짐**(콘솔: `SkinnedMeshRenderer: Rendering stopped because the data for mesh ... does not match the expected mesh data size and vertex stride`) | 켜진 채로 `skin.sharedMesh`를 정점 수가 다른 메시로 바꾸면 GPU 스키닝 버퍼가 옛 크기로 남는다. 같은 프레임에 또 그리는 일(정지 화면 캡처·렌더 텍스처)이 있으면 Unity가 그 스킨 그리기를 멈춘다 | `RagdollPawn.SwapSkinMesh`: 바꾸는 동안 스킨을 껐다 켠다(버퍼를 새로 만든다) | R157 |
| 50 | 소드 파이트 경기 시작 때 `UnityException: CreateImpl is not allowed to be called from a MonoBehaviour constructor` (`SwordFightMatch`) | `static readonly MaterialPropertyBlock block = new …` 필드 초기화: 클래스의 정적 초기화가 컴포넌트를 만드는 순간(생성자) 돌아서 Unity가 거부. Linux 컴파일 검사로는 안 잡힘 | Unity 객체(`MaterialPropertyBlock`·`Material`·`Texture2D`·`Mesh`…)는 필드 초기화에서 만들지 않는다. 처음 쓸 때(`Awake`·`OnGUI`·`??=`) 만든다 | R149 |
| 51 | 에디터 스크립트로 카메라를 RenderTexture에 그려 캡처하면 **블룸이 없고 흰색이 0.89 근처에서 납작, 진한 빨강이 분홍**(게임 화면과 다름) | URP는 카메라 색 버퍼 형식을 대상 RenderTexture 형식에서 정한다. 8비트(`ARGB32`) 대상이면 1을 넘는 HDR 값이 블룸·톤매핑 전에 잘린다 | 대상을 `ARGBHalf`(`sRGB = false`)로 만들고, 읽을 때 `Mathf.LinearToGammaSpace`로 바꿔 PNG로 쓴다(`VfxCapture.Read`) | R172 |
| 52 | `Tools/Test-NetworkCompile.ps1`(또는 `run-tests-linux.sh`)이 `The type or namespace name 'Universal' does not exist in the namespace 'UnityEngine.Rendering'` · `VolumeProfile`을 못 찾음으로 실패(Unity 안에서는 잘 컴파일됨) | 두 스크립트는 URP 패키지 DLL 없이 컴파일한다. URP 코드는 asmdef `versionDefines`가 켜는 `CF_URP`로 감싸는 약속인데, R172의 `VfxCapture.cs` · `VfxWorkbenchSetup.cs`가 감싸지 않았다 | URP 타입(`UnityEngine.Rendering.Universal`, `Volume` · `VolumeProfile` 같은 SRP Core)을 쓰는 파일은 `#if CF_URP` … `#endif`로 감싸고, 그 어셈블리 asmdef에 `versionDefines`(`com.unity.render-pipelines.universal`, `17.0`, `CF_URP`)를 둔다(Game · RagdollLab · VFX.Editor). 이 PC의 Unity는 Program Files가 아니라 `%USERPROFILE%\OneDrive\문서\6000.3.11f1\Editor` → `-UnityEditor`로 넘긴다 | R174 |
| 53 | Play 중에 스크립트가 다시 컴파일된 직후 `ArgumentNullException: Value cannot be null. Parameter name: dest`(`VfxSigil.Draw` ← `OnEnable`) | 편집기 설정이 "Recompile And Continue Playing"이라 Play 중에 도메인을 다시 불러온다. 직렬화되지 않는 `MaterialPropertyBlock` 필드는 null이 되고 `Awake`는 다시 돌지 않는다(`OnEnable`은 다시 돎). `Awake`에서만 블록을 만드는 부품(예: `VfxSigil`)이 그때 떠 있으면 오류 | Play를 다시 시작하면 사라진다. 새 부품은 쓰기 직전에 `if (block == null) block = new MaterialPropertyBlock();`로 만든다(`VfxPhantom` · `VfxCue`처럼) | R174 |
| 54 | LineRenderer 폭 곡선을 `Mathf.Pow(Mathf.Sin(Mathf.PI * u), k)` 키로 만들었더니 **선 뒤쪽 4분의 1이 안 그려짐**(비숍 F6 X의 아래 두 팔이 반에서 뚝 끊김, 깊이 흐림을 꺼도 같음) | `Mathf.Sin(Mathf.PI)`가 float에서 0이 아니라 -8.7e-8 → 음수의 소수 거듭제곱 = NaN → 마지막 키가 NaN이고 `SmoothTangents`가 이웃 키 접선까지 NaN으로 만들어 u 0.75 뒤 폭이 전부 NaN | 거듭제곱 전에 `Mathf.Max(0f, …)`로 자른다(`VfxSlash.Shape`). 선이 이상하게 끊기면 `widthCurve.Evaluate`부터 본다 | R175 |
| 55 | Stretched Billboard 파티클(비숍 F7 공기 줄)을 길게 하려고 `lengthScale`을 2 → 4로 올렸는데 **길이가 거의 그대로**(밝기를 같이 올려서 진해지기만 함) | `lengthScale`은 파티클 **굵기(크기)에 곱하는 값**이다. 굵기가 0.8~1.4 cm라 ×2를 더해도 2~3 cm. 빠른 파티클의 길이는 거의 `velocityScale` × 속도(7~11 m/s × 0.06 ≈ 0.4~0.7 m)가 정한다 | 가늘고 빠른 줄은 `velocityScale`로 길이를 바꾼다(0.06 → 0.12면 두 배). `lengthScale`은 느리거나 굵은 파티클에만 쓴다 | R178 |
| 56 | 새 HLSL 셰이더(`VFX_SpinBlurAdd`)에서 변수 이름을 `line`으로 했더니 셰이더 컴파일 오류 | `line`은 HLSL 예약어(지오메트리 셰이더 입력 종류)라 변수 이름으로 못 쓴다. `point` · `triangle` · `lineadj` · `triangleadj`도 같다 | 다른 이름(`stripe` 등)을 쓴다. 셰이더를 고친 뒤에는 콘솔과 셰이더 인스펙터의 오류를 본다 | R181 |
| 57 | 셰이더 프로퍼티 기본값을 바꿨는데 **이미 있는 재질(`M_*.mat`)의 그림이 그대로** | 재질은 만들 때의 값을 자기 파일에 저장하고, 셰이더 기본값은 그 프로퍼티가 재질에 없을 때만 쓴다. 빌더의 `Material()`도 재질이 없을 때만 새로 만든다 | 기본값을 바꿀 때 이미 있는 재질 값도 코드로 같이 넣는다(`material.SetFloat` → `EditorUtility.SetDirty` → `AssetDatabase.SaveAssets`). 부품 값도 같다: 빌더의 `Keep`이 옛 프리팹 값을 되살리므로 프리팹 값을 직접 바꾼다(`PrefabUtility.LoadPrefabContents` → `SaveAsPrefabAsset`, R178) | R181 |
| 58 | Windows PowerShell 5.1로 돌린 `.ps1`이 한국어 글자가 든 줄에서 `The string is missing the terminator` · `Missing ')' in method call`로 파싱 실패 | 5.1은 BOM 없는 스크립트를 시스템 코드 페이지(한국어 Windows = CP949)로 읽어 UTF-8 한글이 깨지고 따옴표가 어긋난다. AI 편집 도구는 BOM 없는 UTF-8로 저장한다 | 한글이 든 `.ps1`은 UTF-8 **BOM**으로 저장한다(`[IO.File]::WriteAllText(path, text, (New-Object Text.UTF8Encoding($true)))`) | R181 |
| 59 | 새 셰이더를 `Assets/VFX/Shaders`에 복사했더니 다른 채팅이 쓰던 같은 이름 파일(`VFX_StarburstAdd.shader` = 퀸 R162 불 광선)이 덮어써짐. `git status`에 그 파일이 `M`으로 뜸 | 새 파일 이름이 이미 있는지 보지 않고 복사했다. 여러 채팅이 같은 `Assets/VFX` 폴더에 파일을 만든다 | 새 파일을 만들기 전에 `ls` · `git ls-files '*이름*'`으로 이름이 비었는지 확인한다(셰이더는 `Shader "…"` 이름도). 덮어썼으면 Unity가 가져오기 전에 `git checkout -- <파일>`로 되돌리고 내 파일은 다른 이름으로, 그 파일을 쓰는 채팅에 알린다 | R185 |
| 60 | (모의 시험에서 재현) 대기실에서 더 좋은 PC에게 방장을 넘기는 중에 Steam의 방 주인 변경이 몇 초 늦으면 "방장이 나가 경기가 끝났습니다"로 방이 끝남 | `PollMatch`는 "방 주인 ≠ 기록의 방장"이 2초 넘으면 방장이 사라졌다고 봤다. `SetLobbyOwner`의 결과는 Steam에서 늦게 올 수 있다 | 기록의 방장(`host`)이 아직 방에 있으면 넘기는 중으로 보고 `WaitingHandoffGrace`(10초)까지 기다린다(정말 사라졌으면 예전처럼 2초). 모의 Steam은 `FakeSteam.OwnerDelay`로 늦춰 시험한다 | R294 |
| 61 | Unity 편집기가 열어 둔 프로젝트 폴더로는 batchmode 빌드(`NetworkSetup.Build`)를 돌릴 수 없음. `robocopy`가 끝 코드 1을 내면 도구가 "실패"로 알림 | 한 프로젝트 폴더는 Unity 하나만 열 수 있다. `robocopy`의 끝 코드 0~7은 성공이다(1 = 파일을 복사함) | 임시 git worktree를 만들고 `Library`를 `robocopy /E /XD Bee`로 복사(약 3 GB)한 뒤 그 폴더에서 `-batchmode -quit -executeMethod ChessFight.Editor.NetworkSetup.Build`. 나온 `Builds/NetworkTest`를 본 폴더로 옮기고 `git worktree remove --force`로 지운다. robocopy 결과는 출력 표의 `FAILED` 칸으로 본다 | R294 |
| 62 | (코드 검토) 방장이 바뀌는 순간 옛 방장과의 음성 채팅이 잠깐 끊김 | `SteamNetworkingMessages.CloseSessionWithUser`는 그 사람과의 **모든 채널**(음성 40 포함)을 닫는다. 옛 방장은 보통 경기에 남아 있다 | 방장 교체 때는 자기 스트림 채널만 `CloseChannelWithUser(ref id, 채널)`로 닫는다(모든 채널이 닫히면 Steam이 세션도 닫는다). 세션 통째로는 상대가 명단에서 빠졌거나 경기를 나갈 때만 | R295 |
| 63 | (코드 검토) 경기 씬을 늦게 불러오는 느린 PC가 방장을 "응답 없음"으로 보고 방장을 넘기려 나서거나 12초 뒤 파티로 나감 | 폰 러쉬 · 소드 파이트(`customMatchSimulation`) 씬은 로비 캡슐 스트림(`SteamMotion`)을 쓰지 않아, 방장이 먼저 씬에 들어가면 그 스트림이 멈춘다. 로딩 중인 PC에게는 침묵으로 보인다 | 로딩 중(`loading` 또는 `MatchLoader.Blocking`)에는 캡슐 스트림이 침묵을 재지 않고(`SteamMotion.Loading`), 경기 씬의 링크가 방장의 첫 패킷이나 공동 시작 시각(`go`) 중 먼저 온 때부터 잰다(Core `HostSilence`) | R295 |
| 64 | (코드 검토) 폰 러쉬에서 방장이 나가면 방장을 받지 않은 셋째 PC의 시계 · 발판 · 스킬이 멈춤 | 역할이 바뀐 두 PC(`IsHost`가 바뀐 PC)만 받은 것을 지웠다. 셋째 PC는 옛 방장의 상태 번호를 들고 있어 새 방장의 작은 번호를 "옛것"으로 버렸다 | 모든 PC가 `session.Host` 바뀜을 보고(Core `HostWatch`) 받은 것을 지운다. 새 방장은 받은 번호에서 이어 센다(`stateTick = lastState`) | R295 |
| 65 | (R297부터 지킬 것) 패킷 배열을 리스트 · 큐에 넣어 두었다가 나중에 보내거나 읽으면 **다른 패킷의 내용**이 들어 있음 | 패킷을 만드는 함수(`MotionProtocol` · `RagdollNetProtocol` · `PawnRushNetState` 등)와 받기 루프는 길이별 배열 하나를 다시 쓴다(`PacketPool`). 같은 길이의 다음 패킷이 그 배열을 덮는다 | 만든 · 받은 그 프레임에 쓰고 버린다. 쥐어야 하면 `(byte[])bytes.Clone()`(지연 시뮬레이터가 이렇게 함). 새 프로토콜도 `PacketStreams`를 쓰고, 바꾸기 전에 Core의 바이트 해시 시험(`GoldenBytes`)에 넣는다 | R297 [3] |
| 66 | (코드 검토) 받기 루프에서 메시지 하나가 예외를 던지면 같은 묶음의 나머지 메시지를 잃고 Steam 메모리도 남음 | 메시지마다 `try`/`finally`(`Release`)만 있고 `catch`가 없어 예외가 루프 밖으로 나갔다 | 메시지마다 `catch` → `ReceiveErrors.Log(채널, 예외)`. 새 받기 루프도 같은 모양으로(31 · 32 · 33 · 34 · 35 · 40 참고) | R297 [5] |
| 67 | 폰 러쉬: **폰 발 · 무릎 높이 턱에 붙어서 점프하면 뒤로 튕겨 나감**(모든 턱, 미니게임도). 가슴 높이 벽에서는 안 그럼 | 20번과 같은 원리가 다른 길로 남아 있었다. 벽에 막힘 판정이 가슴 높이 광선 하나라 낮은 턱은 못 봐서 앵커가 서 있을 때 한도 0.6 m만큼 턱 안에 있었고, 점프 스텝에 공중 한도 0.12 m로 **한 번에 0.48 m(= 그 스텝 24 m/s)** 당겨져 관절 감쇠기가 몸을 뒤로 끌었다 | 폰 러쉬(`lipNoRecoil`): 발 · 골반 높이 광선도 막힘으로 보고(`PawnRushLowLip`), 땅을 떠날 때 앵커는 초당 0.8 / 감쇠비보다 빠르게 당기지 않는다(`PawnRushAirLeash`). **앵커 한도를 바꾸는 코드는 한도가 줄어드는 순간(땅 → 공중, 막힘 → 풀림)에 앵커가 한 스텝에 얼마나 옮겨지는지 본다** | R398 |
| 68 | 폰 러시 구간 시험: **대계단 현관홀 다음 미니게임이 홀 안(왕실 단)에 겹쳐 붙음** — 코드의 출구는 맞는데 | `PawnRushRound`는 구간 프리팹을 꺼진 묶음 안에 만들고 **깨어나기 전에** 프리팹에 저장된 입구 · 출구 값으로 이어 붙인다. 홀 프리팹은 R303 전 출구 (0, 19, 132)를 들고 있었고, 코드(`GrandStairHallBuilder.Exit` (0, 15, 158.3))는 Awake의 `Describe`에서야 들어갔다 | 프리팹 값을 코드와 같게(R493). **입구 · 출구를 바꾸면 그 구간 프리팹 YAML의 `entry` · `exit`도 같은 값으로**(구워지지 않는 뿌리만 프리팹 — 정원 · 홀 · 작전 회의실 — 은 특히) | R493 |
| 69 | 작전 회의실 **대포 회전 소리가 구간 시험의 모든 구간에서 들림**(SeoungTest 장면에서는 괜찮음) | 소리가 나는 "방 상자"를 홀의 `Renderer` 전부의 `bounds`로 쟀는데, 골 룩의 레이저 예고 선(`LineRenderer`, `useWorldSpace`, 꺼짐)이 처음 겨누기 전까지 **월드 원점 (0, 0, 0)**에 있어 상자가 원점까지 늘었다. 원점에 서 있던 구역 E에서는 상자 모서리라 티가 안 났다 | 방 상자는 `MeshRenderer`(모델 · 효과의 판)만으로(R494). **월드 공간 선 · 궤적 · 파티클 렌더러의 `bounds`는 그 물체 자리와 상관없을 수 있으니 공간을 잴 때 쓰지 않는다**; 원점이 아닌 곳에 놓고도 확인한다 | R494 |

## 22. 빠른 발도 시 물리 칼이 수납 방향으로 돌아감 (R50)

R50 실행 로그에서 수동 설정했던 무게중심이 검날 중앙 `z=0.5`로 되돌아가고, 발도 첫 프레임의 방향도 수납 방향을 유지했다. 0.1초 뒤 조준 오차 약117도였지만 충분히 기다리면 정렬돼 이전 느린 검사에서는 놓쳤다. 초기화 순서가 중요하다.

- 충돌체를 먼저 켠 뒤 **표시 Transform와 Rigidbody 양쪽에 시작 자세**를 설정한다. 이후 동적인 베기는 순간 회전이 아니라 torque로 계산한다.
- `automaticCenterOfMass`와 `automaticInertiaTensor`를 명시적으로 끄고, 발도 때도 손잡이 지지 무게중심/회전 관성을 재설정한다. API 의미는 [Unity 6.3 automaticCenterOfMass](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody-automaticCenterOfMass.html), [automaticInertiaTensor](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody-automaticInertiaTensor.html) 참고. 여기의 재초기화 현상은 이 프로젝트 실행에서 측정한 것이다.
- **10회 수납/발도 각각 0.1초 시점**의 실제 칼 방향과 무게중심/자동 계산 플래그를 검사한다. 단순히 Drawn=true 또는 1초 뒤 안정화만 검사하지 않는다.
- 접촉 콜백이 아닌 다음 FixedUpdate에서 처리한다. 공유 Pawn·카메라·튜닝을 이 문제 때문에 바꾸지 않는다.

## 23. 느린 칼 접촉의 반동을 강한 베기로 오인 (R51)

실제 검의 회전 에너지만 제한한 첫 실행은 **49/1**이었다. 느린52°/초 드래그에서 충돌 반동이 빠르게 계산돼 명시적 칼 타격/넘어짐이1회 발생했다. **평활한 조준 입력 속도도 요구**한 다음 실행은50/0, 느린 접촉681회 중 타격0/넘어짐0이었다. 물리 반동과 플레이어의 공격 의도를 구분한다.

- 약한 접촉 시험은 실제 겹침/접촉이 있었는지와 `Hits`·`Knockdowns`·최종 Active 상태를 함께 검사한다. 칼이 빗나간 것만으로 통과시키지 않는다.
- 강한 베기는 카메라 보정까지 거친 입력으로도 명중을 검사한다. 아군/부활 보호 시험에도 강한 동작을 사용한다.
- 공통 래그돌의 충돌/넘어짐 튜닝은 그대로 둔다. 판정 임계값과 손맛은 사용자/2PC 재확인이 필요하다.

## 24. Blender → Unity FBX가 반 바퀴 돌아 들어옴, Blender 배치 목록이 (0, 0, 0) (R256)

- **증상:** 엔드게임 모델을 배치 목록 그대로 놓으니 바닥이 z 반대쪽(S1 바닥이 z −7)에 있고, 난간 · 홀 장식은 모두 원점에 몰림.
- **원인 1:** Blender FBX 내보내기(앞 −Z, 위 Y)를 Unity가 가져오면 메시가 (x, y, z) → (−x, y, −z), 즉 Y축 180° 돈다. 대칭인 장치 스킨에서는 안 보여서 늦게 찾음.
- **해결 1:** Unity에서 놓을 때 배치 목록의 회전 뒤에 180°를 더 돌린다(`Course01v4Builder.WarRoomModels.FbxTurn`).
- **원인 2:** Blender에서 `obj.location`을 정한 직후 `obj.matrix_world`는 depsgraph 갱신 전이라 옛 값(0)이다. 또 타이밍 MP4가 장치에 단 드라이버 · 키는 그 뒤 `rest()`로 놓은 자리를 덮어쓴다.
- **해결 2:** 부모 없는 물체는 `location`을 읽는다(`eg_bl_scene.record`), MP4 뒤에 드라이버 · 키 · 프레임 핸들러를 지운다(`eg_bl_anim.clear`). 배치 목록을 다시 내보낸 뒤 Unity 메뉴로 다시 가져온다.

## 25. 합쳐진 메시에서 떼어낸 움직이는 부품이 고정 부분 조각을 데려감 (R277)

- **증상:** 정원 광장에서 회전봉이 돌 때 화단 안쪽 경계석이 뜯겨 나가는 것처럼 틈이 생기고, 조각이 회전봉과 함께 돈다(사용자 영상, 11-01).
- **원인:** R275에서 통합 광장 메시의 회전봉을 따로 떼어낼 때 반지름 기준 선택에 경계석 고리(13.2~13.8m)와 허브 바닥 고리(2.5~3.1m) 조각이 함께 들어갔다. 회전봉 메시 충돌체에도 그 조각이 들어가 바닥 근처에서 캐릭터를 밀었다.
- **해결:** 떼어낸 메시를 연결 조각(island)별로 반지름·높이로 나눠 본다. 고정 부분 조각은 쉬는 자세(프레임1) 그대로 고정 오브젝트로 되돌린다(`ArtSource/PawnRush/Garden/build_v21.py` 1a2). 떼어낸 뒤 **프레임1과 몇 초 뒤를 위에서 렌더해 비교**하면 바로 보인다.

## 26. `Physics.RaycastAll`은 거리순이 아님 (R278)

- **증상:** 시험 씬 구역 G(정원)의 시작점이 출발 발판이 아니라 다른 높이에 잡힘(사용자: 시작점이 바닥에 안 맞음).
- **원인:** `SeoungTestCourse.TryFloor`가 `RaycastAll` 결과의 첫 바닥을 썼는데, 결과 순서는 정해져 있지 않다. 발판 아래 해자 기단(-2m) 등 겹친 바닥이 먼저 나올 수 있다.
- **해결:** `distance`로 정렬한 뒤 첫 바닥을 쓴다. 여러 겹 바닥이 있는 맵에서 `RaycastAll`을 쓸 때는 항상 정렬한다.

## 27. GLB 메시를 깎아 메우면 사용자 지정 법선이 남아 얼룩이 생김 (R279)

- **증상:** 원형 광장 구멍을 지우고 평평하게 메웠는데 렌더에 작은 방사형 어두운 얼룩이 남음(형상은 완전히 평평).
- **원인:** 받은 GLB 메시에 `custom_normal`이 있어, 남은 모서리(corner)의 법선이 옛 구멍 테두리 쪽으로 기운 채 유지됨. 평면 셰이딩으로 바꿔도 사용자 지정 법선이 우선한다.
- **해결:** 수정 부위의 위쪽을 향한 면 모서리 법선을 (0, 0, 1)로 다시 정해 `normals_split_custom_set`(`build_v23.py` 1번). Unity는 FBX 법선을 가져오므로(normalImportMode 0) Blender에서 고쳐야 한다.

## 28. 버전마다 Resources 모델 이름을 바꾸면 저장된 씬이 지워진 파일을 가리킴 (R282)

- **증상:** Pull 뒤 시험 씬의 정원이 통째로 안 보임(사용자: "맵이 사라졌어").
- **원인 후보:** 정원 FBX를 `RoyalWaterGarden_v2x.fbx`로 버전마다 새로 넣고 이전 파일을 지웠다. 메뉴로 지어 저장한 씬의 정원은 이전 FBX의 메시를 GUID로 가리키므로, 다시 짓기 전까지 모두 Missing이 된다. 또 `SeoungTestCourse.Build`는 루트를 꺼 둔 채 구역을 짓기 때문에 구역 하나가 예외를 내면 맵 전체가 꺼진 채 남았다.
- **해결:** 이름(과 GUID)을 `RoyalWaterGarden.fbx` · `RoyalWaterGarden_Motion.json`으로 고정하고 내보내기가 덮어쓴다. 구역마다 try/catch, 루트는 finally에서 켠다. 모델을 못 읽으면 LFS 포인터(수백 바이트)인지 검사해 알린다.

## 29. 셰이더 전역 배열은 처음 정한 크기가 편집기 세션 내내 고정됨 (R283)

- **증상(예상):** `Shader.SetGlobalVectorArray`로 넘기는 배열 길이를 늘렸는데(정원 착수선 12 → 24), 이미 열려 있던 Unity에서는 뒤쪽 항목이 셰이더에 안 들어가거나 크기 경고가 뜬다.
- **원인:** Unity는 전역 배열의 크기를 처음 설정할 때 정하고, 스크립트를 다시 컴파일해도 그 세션 동안 바꾸지 않는다.
- **해결:** 크기를 바꿀 때는 전역 이름도 바꾼다(`_GardenSplash*` → `_GardenLanding*`). 이름을 못 바꾸면 Unity를 껐다 켠다.
- **같이 겪은 것(Blender 조사):** `scene.ray_cast`는 렌더에서 숨긴 물체와 커브(`V07 … lip foam`)도 맞힌다. 맞은 물체 이름 · 종류를 함께 출력해 확인할 것.

## 30. Blender의 면 하나짜리 메시는 Unity에서 반쯤 사라질 수 있음 (R284)

- **증상:** 정원 분출구의 어두운 입구(`V26 | spout shadow …`, 면 하나) 9개 중 4개가 Unity에서 안 보여 입구가 벽으로 막혀 보였다. Blender 렌더에서는 모두 보인다(Blender는 뒷면도 그림).
- **원인:** `bmesh.ops.recalc_face_normals`는 닫히지 않은 면 하나의 앞뒤를 정할 기준이 없어 방향이 제각각이다. Unity 셰이더는 뒷면을 잘라 낸다.
- **해결:** 면 하나짜리 판은 보는 쪽 방향을 직접 정해 `reverse_faces`로 맞춘다(`build_v27.py`). 확인은 Blender 렌더가 아니라 월드 법선과 바깥 방향의 내적으로.
- **같이 겪은 것:** 아주 얕은 물(정원 수조 3cm)에 깊은 물과 같은 물결 높이를 주면 골마다 바닥이 물 위로 튀어나온다. 물 높이 아래 바닥까지의 깊이를 재고, 얕은 물은 물결이 작은 재질을 따로 쓴다.

## 31. 정원의 V19 돌 블록은 메시 하나를 같이 씀 (R287)

- **증상:** 정원 테라스 판 2장의 메시 꼭짓점을 고쳐(안쪽 끝 줄이기) 내보냈더니, 테라스 판 사이가 벌어지고 저수지 · 광장 · 길 등 상관없는 `V19 |` 구조물 수십 개의 크기가 바뀌었다(승규 님: "구조물이 빠지거나 그래").
- **원인:** `V19 |` 블록은 모두 같은 깎은 모서리 상자 메시(꼭짓점 10706개)를 공유하고, 크기는 물체 변환(배율)으로 정한다. 메시를 고치면 그 메시를 쓰는 모든 블록이 바뀐다.
- **해결:** 구조물은 물체 변환만 바꾼다(옮기기). 모양을 꼭 바꿔야 하면 `o.data = o.data.copy()`로 먼저 떼어 낸다. 고친 뒤에는 이전 판과 모든 물체의 경계 상자를 비교해 "그대로 / 통째로 옮김 / 지움" 말고 다른 변화가 없는지 확인한다.

## 32. 정원을 다시 내보내면 이미 지은 씬이 엉뚱한 부품을 꾸밈 (R287)

- **증상:** 승규 님 Unity에서 정원이 엉망(갈색 판이 물 위로, 숨은 충돌 판이 보임, 성벽 · 탑이 흩어짐). 씬은 첫 v30 내보내기로 지어 저장됐고, 그 뒤 같은 `Version`으로 두 번 더 내보냄.
- **원인:** FBX 부품 이름이 `GW_0000`처럼 정렬 순서 번호였다. 물체 하나를 더하거나 빼면 뒤 번호가 모두 밀리고, 저장된 씬은 재질 · 충돌체 · 렌더러 끔 · 움직임 대상을 이름으로 붙들고 있어 다른 메시에 붙는다. `Version`이 같으면 씬을 다시 짓지 않는다.
- **해결:** 부품 이름을 Blender 이름의 해시로(`export_unity_v30.py`: `GW_` + sha1 12자리) — 물체가 더해지거나 빠져도 다른 이름은 그대로. 정원 FBX/JSON을 다시 내보낼 때마다 `SeoungTestCourse.Version`을 올린다(같은 R 안에서도).

## AI 도구 작업 시 주의

- **R496 사용자가 보는 기존 프로젝트를 유지:** 이 사용자는 `C:\Users\dua07\GitHub\ChessFighter`에서 `main`을 직접 확인하도록 명시했다. 수정 중인 파일 보존을 이유로 새 워크트리를 만들어 결과 위치를 바꾸지 않는다. 먼저 외부 사본·해시·patch·명명 stash·원래 브랜치로 보존한 뒤 같은 프로젝트를 전환한다. 당시 C 프리팹은 main에 없는 `EarlyPlaza`를 참조하므로 stash 전체 자동 적용은 Missing Script를 만들 수 있다. 기존 보관 worktree가 main을 점유하면 파일을 삭제하지 않고 같은 HEAD에서 detach하여 점유만 해제한다. 앞으로 별도 worktree를 열라는 R313 안내 대신 고정 경로/main을 따른다(T13). LFS 필터를 끈 상태에서 `.blend`·WAV가 modified로 보이면 실제 SHA256과 HEAD LFS oid를 대조하고 일치 파일을 되돌리거나 다시 저장하지 않는다.

- **R313 GitHub Desktop 작업 폴더와 Unity 프로젝트 루트 불일치:** Desktop의 `api-bridge-main`은 `C:\Users\dua07\GitHub\ChessFighter\.worktrees\api-bridge-main`의 `main`이고, Unity는 원래 `C:\Users\dua07\GitHub\ChessFighter`의 `claude/bold-johnson-8ez95n`을 열고 있었다. 전자는 R312 미니게임10종·메뉴를 갖고 후자는 R155 프리팹2개만 가져 제보 화면과 일치했다. Pull은 선택한 폴더에만 반영된다. **Repository → Show in Explorer**가 여는 폴더와 Unity 프로젝트 루트를 대조하고 Hub **Add project from disk**로 같은 폴더를 연다. 이름이 같아 창 제목만으로 구분하지 않는다. 원래 사용자 수정2개를 보존하고 경로를 바로잡으며, 기존 브랜치를 강제로 바꾸거나 생성기로 자산을 덮어쓰지 않는다. 올바른 폴더의 별도 Unity에서 실제 저장 시험 씬 Start·C 궁전·캐릭터·HUD를 AI가 관찰했지만 사용자 입력을 감지한 뒤 숫자키·팀 전환은 더 조작하지 않았다. 화면 관찰과 사용자 직접 퍼즐 성공은 구분한다. [실제 경로·순서·결과](../PawnRush/PAWN_RUSH_MINIGAME_SECTIONS.md).

- **R312 구운 프리팹과 실제 실행의 차이:** 게임의 비직렬화 배열·참조는 저장만으로 복구되지 않아 Play에 Game socket만 재설치한다. A판 정적 캐시는 구간 인스턴스로 묶고 비활성 계층의 다른 팀도 연결한다. `ArenaDressing`처럼 별도 MonoBehaviour를 다른 이름의 파일 안에 두면 저장된 컴포넌트의 스크립트를 잃을 수 있어 동일 이름 파일로 분리했다. OS 동적 폰트/atlas는 저장용 기본 폰트와 live 바인딩을 분리하고, 자산 참조 검사는 ExecuteAlways 실행 전 영속 프리팹에서 한다. 재질/메시 캐시 원본을 자산화하지 말고 복제한다.

- **R312 편집기 작업/검사의 범위:** 미리보기 굽기는 `NewPreviewScene`을 쓰며 새 Untitled 씬을 additive로 만들어 닫는 방식은 피한다. 시험 씬을 고칠 때 대상 active scene을 명시하고 복원하지 않으면 다른 씬의 RenderSettings를 바꾼다. batch 시작 직후 Play는 Unity SearchDatabase 초기화 예외로2회 실패했고, import/compile 완료 뒤2초 idle 후 시작하자 통과했다(실패 로그 보존·예외 필터 완화 없음). Unity 종료가 Temp를 지워 보고서는 `Logs/MiniGameValidation`에 남긴다. 오래된 Mono csc는 기존 C#9 문법을 못 읽으므로 Windows 전체 컴파일은 해당 Unity의 `NetCoreRuntime/dotnet` + `DotNetSdkRoslyn/csc.dll`을 먼저 쓴다. 강제 완료·참조/문 검사와 실제 플레이 입력으로 퍼즐을 푸는 검사는 구분한다.

- **R311 3D 설계의 일반화와 빈 컨트롤:** A는 공유판 하나, C는 전체 폭 해자라서 모든 게임을 같은 좌우 스테이션 반복으로 만들면 틀린다. 권양기처럼 낮은 소품도 완성된 길을 가릴 수 있으므로 도개교4m 접근폭과 장치 경계를 확인한다. 표시 버튼은 클릭 가능 여부만이 아니라 실제 표시 메시 수가 줄어드는지도 검사한다. 중후반에 비어 있던 주변장식 그룹의 버튼은 초반 전용으로 정리했다. 생성 이미지는 정확한 개수·경로·치수를 보장하지 않으므로 [모델링 지침](../PawnRush/PAWN_RUSH_MINIGAME_MODELING.md)·3D를 우선한다.

- **R310 로컬 3D 페이지 확인 범위:** 앱 브라우저가 로컬 페이지 접근을 정책으로 거부하면 다른 브라우저·전달 경로로 우회하지 않는다. 일반 브라우저에서 사용자가 열 HTML을 제공하고, 가능하면 장면 생성·좌표·카메라·팀 문을 별도로 검사한다. CPU로 만든 배치 그림은 브라우저 캡처가 아니며 실제 WebGL·마우스·모바일 확인을 대신하지 않는다. [미리보기 확인 범위](../PawnRush/PAWN_RUSH_MINIGAME_PREFABS.md#5-파일과-검사)에 구분해 기록한다.

- **R56 높은 계단/결승 시험:** 시각 단차만 촘촘히 배치하면 래그돌 치마·발이 모서리에 걸린다. 코스에 연속 경사 충돌면을 두고 시각 단차의 충돌을 끈다. 경사는 수평 회전 발판의 중심이 아니라 **가까운 모서리 높이**에 맞춰야 숨은 큰 턱이 사라진다. 위층 길이 아래층을 덮어 머리를 막지 않게 분리한다. 공통 조작을 바꿔 지형 오류를 가리지 않는다. 왕좌 게이지 검사는 실제 착지/점유를 기다리고, 탑 재진입 검사는 종료 시점만 보는 대신 비행 중 한 번이라도 실제 공중 발사가 있었는지와 이후 착지를 함께 검사한다.

- **R56 순서 차이의 실제 원인:** 계단 단독 통과만 보면 놓친다. 결승 후 다시 시작한 경우 두 번째 회전 발판 직전에 넘어지고, 일어나는 동안 남은 전진 속도로 바깥 가장자리를 넘어갔다. 위치·Ragdoll/GettingUp 기록으로 분리한 뒤, 넓은 길의 회전부 바깥에1m 난간을 추가했다. 같은 넘어진 경로에서 난간에 멈추고 실제로 회복해 통과하는지 재검사했다. 시험의 넉다운을 무시하거나 공동 물리 임계값을 바꾸지 않는다.

- **R55 이동 코스 시험:** 고정된 목표점을 좇으며 움직이는 발판 위에서 계속 점프하면, 실제 탑승이 아니라 물에 뛰어드는 시험이 된다. 도착 시점까지 기다려 걷기로 타고, 실제 `Riding`과 반대편까지 운반된 위치를 별도로 측정한다. 맵의 발판 끝은 육지와 여유 있게 겹치고 상단을15cm 낮춰 발과 정면으로 부딪히는 접합부를 줄였다. 시소 진입은 중앙 축으로 경로를 나눈다. 낙하한 뒤 **모드 예약을 남긴 채** Pawn만 순간 배치하면5초 뒤 옛 물 복귀가 다음 시험을 오염시킨다. 독립 지형 시험을 시작할 때 모드도 초기화하고, 앞선 실패는 그대로 기록한다. 최종 전체 시험에서도 시소 출구의 수평 좌표에 도착했지만 아직 공중이라 통과 인원이0인 실패가 있었다. 수평 거리만으로 종료하는 이동 도우미 뒤에는 **실제 착지와 출구 계수**를 함께 기다린다. 사용자가 보존하도록 한 움직이는 발판의 추가 점프 높이를 시험 편의로 제거하지 않는다.

- **R54 맵의 TextMesh 안내판:** 편집기에서 OS 동적 폰트로 생성한 프리팹은 폰트/atlas를 영속 자산으로 저장하지 못한다. 실행 시 폰트를 다시 연결하고 atlas 재생성 이벤트에 텍스처를 갱신한다. 기본 GUI 글자 재질은 벽을 뚫고 보여 여러 간판이 겹치므로, 맵 안내판에만 깊이 검사를 하는 글자 셰이더를 사용한다. 가까운 간판/전체 코스 렌더와 별도 HUD 렌더를 함께 확인한다. 공통 이름표 재질은 이 문제 때문에 바꾸지 않는다.

- **R54 상자 판정만 통과해도 미션이 플레이 가능한 것은 아니다.** 적을 상자에 순간 배치하는 검사는31개 통과했지만 실제 잡기/던지기 경로는0.8m 턱에 막혔다. 공유 물리를 바꾸지 않고 앞쪽 접근 경사를 추가해 실제 운반→득점까지 재검사했다. 규칙 검사와 물리적 달성 경로를 분리해 둘 다 검사한다.

- **R49 물리 칼:** 첫 실행에서 손에 연결한 긴 칼이 아래로 처지고 바닥을 파고들었다. 손목 제어의 무게 지지, 매끄러운 칼 접촉 재질, 바닥 아래로 향하는 불가능한 목표 제한을 모드 안에서 적용했다. 공유 중력/래그돌 튜닝을 바꿔 덮지 않는다. 칼 접촉은 콜백에서 기록만 하고 다음 FixedUpdate에서 처리한다. 실제 플레이어의 저공 베기·벽·12칼 검사를 실행하고 궤적/렌더도 본다.

- 쉘 heredoc 안에 복잡한 따옴표가 든 커밋 메시지를 넣으면 쉘 문법 오류가 난다. 메시지는 파일로 써서 `git commit -F`로 넣는다.
- `mcs`(Mono 컴파일러)는 최신 문법(튜플 분해, 괄호 식에 메서드 호출 등)을 못 읽는다. 래그돌 코드는 mcs로 컴파일되지 않는다. 그래서 `Tools/run-tests-linux.sh --compile`은 **Roslyn**(.NET 8 SDK의 csc, Unity와 같은 컴파일러)을 쓴다. 테스트 실행에만 mcs를 쓰므로 **Core·SteamSession·테스트 코드는 mcs가 읽을 수 있게**(이름 있는 튜플 대신 작은 struct) 유지한다.
- 참조 DLL이 Unity 2021.3이라 Unity 6에서 바뀐 이름(`linearVelocity`, `linearDamping`, `PhysicsMaterial` 등)은 스크립트가 임시 복사본에서만 옛 이름으로 바꾼다. 새 Unity 6 API를 쓰다 컴파일 검사가 실패하면 `unity6_to_2021`에 규칙을 추가한다.
- 이름 충돌: `CameraRig`의 속성 이름을 `Camera`로 두면 `UnityEngine.Camera` 타입을 가린다(`Target`으로 바꿈).
- Unity가 한 번 저장한 씬·프리팹에 생성기(`Tools/Generators/gen_scenes.py`)를 다시 돌리면 편집 내용이 사라진다.
- 이전 도구 시절: GitHub 연동 앱이 저장소 쓰기에 403을 냈다. 사용자가 GitHub Desktop으로 커밋·푸시해 해결했다. 권한 문제는 코드 오류가 아니다. 도구마다 쓰기 권한을 먼저 확인한다.
- 이전 도구 시절: 샌드박스의 headless Unity가 라이선스 IPC 문제로 실패했다. 이후 사용자 Unity Hub에서 빌드에 성공했다. 과거 장애를 지금의 검증 불가 사유로 재사용하지 않는다.
- Steam 로그인은 사용자가 직접 한다. 비밀번호·인증 코드·토큰을 문서나 저장소에 적지 않는다.
- **R111 Unity MCP `execute_code`:** Play에 들어가면 도메인을 다시 불러와 연결이 잠깐 끊기고, 명령이 실패하거나 다시 보내질 수 있다. 부작용 있는 명령(로컬 시험 방장 시작 등)은 "이미 있으면 안 함"으로 막는다. 막지 않았을 때 방장이 여러 개 떠서 UDP 47611이 "이미 사용 중"이었다. 빌드도 한 번에 하나씩 부른다.
- **R111 로컬 시험으로 지연을 잴 때:** 편집기 방장이 자기 화면을 연속 저장(`burst`)하면 약 3프레임/초로 느려져 스냅샷이 몰려 오고 신호가 최대 250 ms 늦게 그려진다. 시간은 저장 없이 `report`·기록 줄로 재고, 화면은 참가자 창만 저장한다. `ScreenCapture`는 프레임 끝에 저장된다.
- **R112 Unity 편집기 두 개(MCP):** `C:/Work/ChessFight`와 `C:/Work/ChessFight-SkillNet`이 함께 켜져 있으면 MCP 명령은 "지금 고른 쪽"으로 간다. 먼저 `mcpforunity://instances`를 읽고 `set_active_instance`로 고른 뒤, 명령 안에서도 `Directory.GetCurrentDirectory()`로 폴더를 확인한다(사용자가 다른 편집기에서 Play 중일 수 있다). Play에 들어간 직후 몇 초는 그 편집기가 목록에서 빠져 "not found"가 나는데, 다른 편집기로 넘어가지 않으니 잠깐 기다렸다 다시 부른다. 새 폴더의 첫 열기는 약 3분, 첫 빌드는 셰이더 때문에 약 2분 반 걸렸다.
- **R111 개발 명령과 지연 시뮬레이터:** 시험 명령 패킷까지 시뮬레이터를 지나면 손실 10%에 빠져 "10번 눌러 8번" 같은 틀린 결과가 나온다. 시험 도구의 패킷은 시뮬레이터를 건너뛰게 한다(`TypeDev`).

## 33. 낱개 조각으로 된 메시는 Decimate로 줄이면 구멍이 남 (R296)

- **증상:** 작전 회의실 벽 패널(`PR_EG_HallWallPanel`, 벽돌 수백 개)을 Decimate(collapse)로 줄였더니 LOD0은 벽돌이 뭉개지고, 먼 거리 LOD2는 면이 뒤집혀 렌더에서 검은 삼각형(Unity는 뒷면을 안 그리니 구멍)이 생겼다. 정원 V19 블록은 2,000 삼각형까지 줄이니 부드러운 음영이 넓은 평면에 사선 줄무늬를 남겼다.
- **원인:** collapse는 따로 떨어진 조각 사이도 합치고 경계를 지키지 않는다. 남은 큰 삼각형 위에서 부드러운 음영이 번진다.
- **해결:** 조각 모음 메시는 줄이지 않고 원래 LOD를 쓴다(`relod_r296.py`의 `KEEP`). 줄인 뒤에는 각진 모서리를 다시 정한다(`set_sharp_from_angle`, 정원 35° · 회의실 30°). 면 뒤집힘은 메시 자기 자신에게 면 앞 2 mm에서 법선 방향으로 광선을 쏴 뒷면에 먼저 닿는 넓이로 잰다. 예전 LOD0에 대고 재면 몇 mm 가라앉은 면도 뒤집힌 것으로 잡히고, 가장 가까운 면으로 재면 얇은 판(바닥 · 책)에서 틀린다. 줄이기 전과 후를 꼭 렌더로 비교한다.


## 34. Unity를 연 채로 LFS 바이너리를 받으면 `.meta`가 새 GUID로 바뀜 (R325)

- **증상:** 11-03 이 PC(seoungtest)에서 홀 · 도서관 · 정원 · 엔드게임 FBX와 도서관 WAV 83개의 `.meta`가 지워졌다가, Unity가 새 GUID · 기본 가져오기 설정으로 다시 만들었다. Console에 "Air cannon sound missing" 등.
- **까닭:** Unity가 켜질 때 LFS 파일이 아직 없어서 짝 없는 `.meta`를 지웠고, 파일이 들어온 뒤 새로 만들었다.
- **고침:** 새로 생긴 `.meta`를 커밋하지 말고 저장소 것으로 되돌린다: `git checkout -- <경로>.meta`(원격에만 있으면 그 브랜치를 먼저 맞춘다). 받기 · 체크아웃은 Unity를 닫고 하거나, 끝나자마자 `git status`에서 바이너리 `.meta`가 지워지거나 바뀌었는지 본다.

## 35. mkmeta로 만든 PNG `.meta`를 Unity가 큐브맵으로 가져옴 (R330)

- **증상:** R329의 카펫(`Assets/Resources/PawnRushGrandStairHall/HallCarpet.png`)이 안 깔리고 Console에 "카펫을 깔지 못했습니다". `Resources.Load<Texture2D>`가 null, `Resources.Load`는 Cubemap.
- **까닭:** `Tools/Generators/mkmeta.py`는 guid만 쓴 `.meta`를 만든다. 텍스처 설정이 없는 그 `.meta`로 Unity가 1024² PNG를 TextureCube로 가져왔다.
- **고침:** 텍스처는 가져온 뒤 설정을 확인한다. 열린 에디터가 있으면 `unity command get_import_settings --asset <경로>` / `set_import_settings --settings '{"textureShape":"Texture2D"}'`로 바꾸면 `.meta`에 `textureShape: 1`이 저장된다.

## 36. 시험 씬 구역은 지은 뒤 옮겨짐 — 셰이더에 월드 좌표를 지을 때 넣으면 어긋남 (R406)

- **증상:** R405 대계단 홀 돔의 금 모서리가 뼈대마다 들쭉날쭉(어떤 뼈대는 면 전체가 금, 어떤 것은 금 없음). R403 나선 금 문턱도 같은 까닭으로 엉뚱한 곳을 가리킴.
- **까닭:** `SeoungTestCourse`는 구역을 원점에서 지은 뒤 `Place`로 줄 자리(수십 ~ 수백 m 옆)로 옮긴다. 빌더가 그때 잰 월드 좌표(`_DomeCentre`, `_FootPoint` 등)를 재질에 넣으면 옮긴 뒤에는 틀린 점이 된다.
- **고침:** 위치 · 방향은 움직이는 물체 기준으로 들고 있다가 월드 값으로 다시 쓰는 `PawnRush/HallAnchoredMaterials`로 넣는다(`HallAnchors(hallModel).Add(재질, 속성, 월드 값, 방향이면 true, w)`). 높이 띠처럼 셰이더가 물체 원점 기준으로 재는 값은 괜찮다.

## 37. Unity가 FBX 재질 이름의 `|`를 `_`로 바꿔 가져옴 (R420)

- **증상:** 정원 광장 컨셉 장식이 Unity에서 바닥 마젠타 · 통나무 흰색 · 돌 주황. Editor.log에 "[정원] 컨셉 재질이 없습니다: …/GC___cream_stone".
- **까닭:** Blender 재질 "GC | cream stone"이 Unity에서 "GC _ cream stone"이 됨. 이름에서 " | "만 바꾸는 규칙으로는 `GC___cream_stone`이 되어 `Resources`의 `GC_cream_stone.mat`을 못 찾고, FBX가 만든 기본 재질(텍스처 없음)이 남음.
- **막는 법:** 재질 이름은 글자 · 숫자가 아닌 것 묶음을 밑줄 하나로 바꿔 맞춘다(`RoyalWaterGardenBuilder.ConceptMaterial`). 새 Blender 재질 이름에 `|`를 써도 되지만, Unity 쪽 찾기는 이 규칙으로만 한다.

## 38. 손으로 만든 Blender 면이 안쪽을 보면 Unity에서 사라짐 (R434)

- **증상:** 정원 로봇 허브가 Unity에서 깨져 보이고 속이 비침. Blender 렌더에서는 멀쩡함.
- **까닭:** Blender 렌더는 뒷면도 그리지만 Unity(URP Lit, Cull Back)는 그리지 않는다. bmesh에서 링을 잇는 `faces.new`로 만든 금 띠가 안쪽을 향해 있었다.
- **막는 법:** 손으로 만든 면은 `bmesh.ops.recalc_face_normals`로 법선을 바깥으로 맞춘다(gc_kit `Mesh.done`은 이미 함). 렌더로 확인할 때 모든 재질에 `use_backface_culling = True`를 켜고 가까이 찍어 본다.

## 39. 장식 면이 다른 면과 같은 평면이면 Unity에서 깜빡임 (R435)

- **증상:** 정원 수문 가로보 아래 · 문 기둥 · 수로 바닥이 카메라를 움직이면 반짝반짝 깜빡임.
- **까닭:** 두 면이 같은 평면(또는 몇 mm 차이)에 겹치면 깊이 버퍼가 어느 쪽을 그릴지 매 프레임 바뀐다(z-fighting). 금 띠 바닥 = 돌 가로보 바닥, 벽 홈 면 = 기둥 면, 수로 바닥 윗면 = 블록아웃 물 그림 − 5 mm였다.
- **막는 법:** 겹치는 장식은 최소 1–3 cm 떼어 놓는다(덧대는 쪽이 밖으로). 덮지 않고 남겨 두는 블록아웃 그림(물 · 화살표 · 유리 폭포) 바로 옆 · 밑에는 장식 면을 두지 않는다. 블록 뒤 줄눈 심은 블록보다 안쪽 · 짧게. 내보내기 전 `blender -b --python check_coplanar.py -- <Sample.blend> "*"`로 검사(R438). R474부터 검사기는 모든 방향의 면(비스듬한 벽 · 지붕)을 보고, 상자 겹침이 아니라 삼각형의 실제 공통 면적(1 cm² 넘으면)으로 판정하며 종류별 예시를 3개씩 보여 준다. 다른 부품이 같은 높이에서 만나는 수평 면(성벽 길 띠 ↔ 탑 띠, 두 벽이 함께 덮는 모서리의 갓돌)도 잘 걸린다 — 한쪽을 1~2 cm 키우거나 한쪽을 다른 쪽 뒤에서 시작한다.

## 40. FBX 정점 색은 Blender로 다시 불러오면 값이 달라 보임 (R440)

- **증상:** 정원 폭포 메시(정점 색에 셰이더용 데이터)를 내보낸 뒤 Blender로 다시 불러오면 b(씨앗) 값이 0.412 → 0.141처럼 달라져 있음.
- **까닭:** `export_concept.py`는 정점 색을 선형 그대로(`colors_type="LINEAR"`) 쓰고, Blender의 FBX 가져오기는 그것을 sRGB로 보고 선형으로 바꾼다. 파일 속 값은 원래 값이고 Unity는 그 값을 그대로 읽는다(정점 색은 색 공간 변환 없음).
- **막는 법:** 데이터용 정점 색은 0과 1(그대로 남음)과 중간 값의 sRGB→선형 관계로 검사하거나, 원래 스크립트의 값과 직접 비교한다. 다시 불러온 값을 고치려 하지 말 것.

## 41. Tripo 에셋을 줄이면 큰 평면 삼각형이 옆 재질 색을 먹음 · 잎은 모양만으로 못 찾음 (R485)

- **증상:** Tripo 성문(UV · 텍스처 없는 메시 하나)을 자리별로 칠한 뒤 Decimate(Collapse)로 54만 → 9만 면으로 줄이자, 벽의 넓은 평면에 녹색 · 파란 큰 삼각형이 생김. 또 잎을 법선 회전 · 법선 퍼짐(침식 포함) · 뒤로 쏜 광선으로 찾으려 하면 잎은 못 찾고 줄눈 · 모서리 · 얇은 벽 뒤 면이 잎이 됨.
- **까닭:** Collapse는 재질 경계를 지키지 않아, 평평한 돌 면이 합쳐진 큰 삼각형이 이웃 잎 · 깃발 면의 재질을 가져간다. Tripo 잎은 돌처럼 매끈한 덩어리이고 벽에 붙어 있어 틈도 없다.
- **막는 법:** 칠한 뒤 재질이 다른 두 면 사이의 꼭짓점을 꼭짓점 그룹 가중치 0으로 두고 Decimate의 `vertex_group` · `vertex_group_factor`로 고정한다(`gate_asset.py`). 잎 · 깃발은 정사영 렌더에서 잰 자리 상자 안에서 "뒤 벽 평면(주변 광선으로 맞춤, 앞으로 나온 것 빼고 다시)보다 앞" 또는 "자라는 윗면 위"로 고르고, 이웃 다수결로 다듬는다.

## 42. Tripo 메시의 빈 속을 판으로 막을 때 판 뒤 끝이 옆면과 "맞닿기만" 하면 틈으로 하늘이 보임 (R490)

- **증상:** 출구 성문 탑 앞 돌 판 옆에 세로 틈이 있어 Unity에서 하늘 · 뒤쪽 처마가 비침(둥근 판 모서리가 어두운 기둥처럼 보임).
- **까닭:** Tripo 탑 몸통은 앞면이 없는 빈 껍질이고 Unity는 안쪽 면을 그리지 않는다. 판 뒤 끝(−0.0485)이 몸통 옆면 앞 끝(−0.048)과 붙어만 있었고, 판 모서리를 둥글게 깎아 그 사이가 벌어졌다.
- **막는 법:** 막는 판은 몸통 속으로 충분히 들어가게 하거나(뒤로 겹치기), 판 뒤에 옆면 바로 안쪽까지 오는 마개를 넣는다(`gate_arch_asset.py` `PLUG_*`). 확인은 하늘을 자홍색으로 두고 뒷면을 안 그리게 한 렌더에서 돌 사이 자홍 픽셀을 센다.

## R497 파일·폐기 모드 정리 시 참조 보존

파일과 `.meta`를 같이 옮겨도 `Resources.Load`·`AssetDatabase`·씬 문자열·생성기·검사 도구의 경로는 자동으로 바뀌지 않는다. 공유 Queen 효과/음원을 `Resources/SharedSkills/Fx`·`Sfx`로 옮긴 뒤 실제 로드 문자열도 수정하고, 진짜 삭제된 Queen 메타63GUID가 현재 Assets YAML에 남지 않았는지 대조했다(참조0). 기물 Queen과 폐기 게임 모드를 같은 것으로 삭제하지 않는다.

Prototype 코드41개는 새 asmdef로 분리하면 기존 어셈블리 내부 참조가 끊기므로7개 asmref로 원래 소속을 유지했다. 외부 컴파일/테스트의 소스 수집도 Prototype을 포함하고 중복을 제외한다. 브랜치 bundle은 Git 참조 복구용이며 LFS 바이너리 전체 백업은 아니다. 원문 HANDOFF를 archive에 bytecopy 보존한 뒤 현재 인계를 줄여 과거 결정의 근거를 잃지 않게 했다.

## R498 파티클 Orbital XYZ 모드와 검사 도구의 재생 상태

작전 회의실의 `LibraryAirCannonFx`는 Orbital X·Y가 Constant 0인데 Z만 `MinMaxCurve(2.5f, 5f)`의 TwoConstants라 `Particle Orbital Velocity curves must all be in the same mode`를 냈다. `MinMaxCurve(0f)`와 `MinMaxCurve(0f, 0f)`는 값이 모두0이어도 모드가 다르다. X·Y를 두 인자 생성자로 맞추고 설정 완료 후 모듈을 켠다. Z 범위와 로컬 공간·외형은 유지한다. 같은 Orbital XYZ 묶음 안의 모드를 맞추며, 선형 XYZ와 Orbital XYZ 사이의 모드는 달라도 된다(실제 Unity 대조군 통과).

실제 충전/발사·입자 방출·시뮬레이션의 세 방향 검사에서 수정 전 엔진 오류30건+모드 실패3건, 수정 후 오류0·세 방향 통과를 AI가 기록했다. 자동 검사 결과와 사용자 회의실 외형 확인은 구분한다. 검사기 자체의 첫 실행은 재생 중 `randomSeed`를 설정한 assertion도 냈으므로, 먼저 `Stop(...StopEmittingAndClear)`한 뒤 씨앗을 넣도록 고쳤다. 운영 효과가 collider를 `Object.Destroy`로 정리하므로 이 경로의 회귀 검사는 Play Mode에서 실행한다. 초기 검사기 실패 기록도 `Logs/AirCannonFxValidation/R498-initial-harness-failure.txt`에 보존한다. [VFX 상세](../VFX/README.md#r498-작전-회의실-공기-대포의-orbital-velocity-오류-2026-11-06).

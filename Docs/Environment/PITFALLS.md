# 실제로 겪은 사고와 재발 방지

모두 이 프로젝트에서 **실제로 일어난 일**이다. 비슷한 증상이 보이면 여기부터 본다. 새 사고를 겪으면 맨 아래에 추가한다.

번호는 아래 표의 줄과 그 뒤의 절이 따로 매겨 와 22 ~ 56에서 겹친다(인용할 때는 '표 N' · '절 N'이나 제목으로). R725부터 새 절은 표의 마지막 줄(89) 다음 번호(90 ~)를 쓴다.

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
| 38 | 온라인 폰 러쉬(코스 01)에서 방장 화면만 맞고, 다른 PC에서는 다른 플레이어가 실제와 다른 곳에 보임(10-11, 5인 시험) | 래그돌 스냅샷 위치를 16비트로 ±80 m(`RagdollNetProtocol.PositionRange`)까지만 담음. 랩(작은 경기장)에 맞춘 값을 길이 246 m 코스에 그대로 써서 z 80 m 너머의 폰이 참가자 화면에서 80 m에 붙음. 방장은 자기 물리를 그대로 그려서 맞게 보임 | 범위 ±320 m(약 1 cm 단위), `CFRA`·v19. **새 맵을 온라인에 올릴 때는 맵 전체가 `PositionRange` 안에 드는지 먼저 본다.** 스킬 순간 신호의 위치(`Core/SkillSync.cs` `SkillWire.PositionRange`)는 아직 ±80 m라, 스킬을 코스 경기에 넣을 때 같이 넓혀야 한다. **11-11에 7구간 판(약 580 m)에서 ±320 m로 다시 일어남 → 81번(R541, Core `WirePoint`)** | R124 · R541 |
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
| 58 | Windows PowerShell 5.1로 돌린 `.ps1`이 한국어 글자가 든 줄에서 `The string is missing the terminator` · `Missing ')' in method call`로 파싱 실패 | 5.1은 BOM 없는 스크립트를 시스템 코드 페이지(한국어 Windows = CP949)로 읽어 UTF-8 한글이 깨지고 따옴표가 어긋난다. AI 편집 도구는 BOM 없는 UTF-8로 저장한다 | 한글이 든 `.ps1`은 UTF-8 **BOM**으로 저장한다(`[IO.File]::WriteAllText(path, text, (New-Object Text.UTF8Encoding($true)))`). 파싱이 되더라도 한글 기본값(경로)은 깨진다: R660 `Run-CarriageYardBots.ps1`의 사본 경로 `C:\유니티\…`가 깨져 robocopy 16으로 실패(R661에 BOM) | R181, R661 |
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
| 70 | IMGUI(`OnGUI`) **글자 색이 지정한 색보다 어둡고 진하게** 나옴: 주황 `#FFA23A` → `#FF5C0A`, 초록 `#3DDC84` → `#0BB63A`, 회색 설명 `#939EAF` → 거의 검정. 같은 색으로 칠한 `GUI.DrawTexture` 점 · 판은 정확 | 선형 색 공간에서 IMGUI 글자만 색이 sRGB → 선형으로 한 번 더 바뀐 것처럼 그려진다(개발 빌드 캡처 값이 정확히 c^2.2). 흰색에 가까운 글자는 거의 차이가 없어 잘 드러나지 않는다 | 글자 색은 선형 색 공간일 때 `colour.gamma`로 넘긴다(`NetMetricsOverlay.Label`). 개발 빌드 캡처로 점과 같은 색 확인, 에디터 Game 뷰는 사람 확인 전 | R521 |
| 71 | IMGUI 상자를 **처음 켤 때 게임이 1초 가까이 멈춤**(F3 측정 카드 0.9초. 방장이면 모두의 경기가 멈춘다) | Unity 6 IMGUI는 글자를 처음 재거나 그릴 때 그 글자를 만든다: 한글 한 글자 1.5~3 ms(개발 빌드, 그 뒤로는 어떤 크기 · 굵기든 0에 가까움), 글꼴의 첫 글자는 약 120 ms. `Font.RequestCharactersInTexture`로 미리 불러도 줄지 않는다(그 글꼴 텍스처와 따로). OS 글꼴 만들기(`CreateDynamicFontFromOSFont`) 자체는 2 ms | 상자가 붙은 첫 프레임(장면 시작)에 쓸 글자를 `GUIStyle.CalcSize`로 한 번씩 재 둔다(`NetMetricsOverlay.Warm`, 상자 자신의 문장 만들기를 샘플 숫자로). 다른 IMGUI 상자도 글자가 많으면 같은 방법 | R521 |
| 72 | **빌드(exe)만 Steam에 연결하지 못함**: Steam이 켜져 있고 로그인돼 있는데도 "Steam 초기화 실패. Steam을 실행하고 로그인한 뒤 다시 연결하세요"만 뜨고, 다시 연결해도 같음. 에디터 Play는 됨. Player.log에는 아무 말도 없음 | Steam은 자기가 띄우지 않은 프로그램에게서 App ID를 환경 변수 `SteamAppId`나 게임 옆 `steam_appid.txt`로만 받는다. 에디터는 프로젝트 루트(`steam_appid.txt` = 480)에서 돌아 되지만, **Unity의 File › Build Profiles 빌드는 그 파일을 복사하지 않는다**(Steamworks.NET의 빌드 단계도 안 함). 복사하던 것은 `ChessFight` 빌드 메뉴뿐이었다. Steam의 진짜 이유("No appID found")는 `SteamAPI.Init()`이 참/거짓만 돌려줘 화면 · 로그 어디에도 남지 않았다 | `SteamAppIdBuildStep`(빌드 후처리)이 모든 Windows · Linux 빌드의 exe 옆에 파일을 복사하고, `SteamSession.EnsureAppId`가 둘 다 없으면 초기화 전에 `SteamAppId`를 넣는다(exe 옆 파일 → 없으면 `DevAppId` 480). 실패하면 `InitEx`가 준 Steam의 이유를 `[ChessFight] Steam 초기화 실패` 경고로 Player.log에 남긴다. 빌드가 Steam에 못 붙으면 먼저 exe 옆 `steam_appid.txt`와 Player.log(`%USERPROFILE%\AppData\LocalLow\DefaultCompany\ChessFighter`)를 본다 | R523 |
| 73 | 미니게임에서 **썰매를 출구 벽 속까지 밀어야 끝남**(G 행차 회랑: 길 끝에서 가마가 판 끝을 1.1 m 넘어 팀 문 통로 안, 0.4 m 낮은 바닥 위에 떠 있음. R524, 사용자 "마지막 구간은 룩이 있는 그 라인 후에 출구 벽 속까지 밀어야 되는 현상을 말하는거야") | 게임 규칙(Core `KingEscortPath`)의 끝점 u 20.3이 스테이션 바닥(u 0~20) 밖이었다. 코드로 지은 예전 방에서는 아무것도 없던 자리지만, R504 모델 작업이 북쪽 벽 앞면을 판 끝(u 20)에 맞추고 팀 문 통로는 0.4 m 낮은 바닥으로 이어져 끝점이 벽 두께 속이 됐다. 게임 숫자와 방 모델이 따로 바뀌어 어느 검사도 못 잡았다(썰매는 키네마틱이라 벽을 그냥 지나감) | 게임이 물체를 보내는 자리(길 끝 · 멈춤 자리)는 스테이션 바닥 안에 둔다(물체 반 크기만큼 안쪽). G는 Core 테스트로 고정(썰매 앞면 u < 20). 방 모델 · 벽을 옮길 때는 게임의 끝점 · 문 자리와 겹치지 않는지 캡처로 본다 | R524 |
| 74 | 여러 PC 공개 매칭에서 **참가자가 대기실 · 시작 순간에 말없이 튕김**, 방장 Player.log에는 아무 이유도 없음(11-09 빌드 시험) | 세 가지가 겹칠 수 있었다. (1) 시작이 방을 닫는데, 대기실이 성능 1등 PC로 넘어가는 동안(Steam 방 주인 이전 동안은 아무도 자리를 못 줌) 들어온 사람은 아직 자리가 없어 "자리가 없습니다" · "입장 승인 시간 초과"로 튕김. (2) 시작 순간 방장이 바뀌면 새 방장이 한 폴 전 명단을 게시해 막 앉은 사람이 빠짐. (3) 대기실 움직임 패킷이 12초 안 오면 `SteamMotion`이 조용히 `Cancel` — 파티원이면 파티 매칭 전체 취소. 어느 것도 Player.log에 안 남았다(`Fail`은 화면 글만) | R525: 시작이 자리 받는 중인 사람을 최대 8초 기다림, 새 방장은 방 명단부터 읽음, 대기실에서는 움직임 끊김으로 안 나감, 모든 퇴장 · 방장 교체를 `[ChessFight] 세션:` 줄로. 다음에 튕기면 그 PC의 Player.log(다시 켜면 Player-prev.log)부터 본다. 모의 Steam(`SessionFlowTests`)의 방 주인 이전 지연(`FakeSteam.OwnerDelay`)을 1~3초로 두면 재현된다 | R525 |
| 75 | **에디터에서는 보이는 효과가 빌드에서만 안 보임**, Player.log에 `셰이더가 없습니다(ChessFight/VFX/…)`(11-09 빌드, 대계단 홀의 떨어지는 말 · 샹들리에 · 환풍기) | 코드가 `Shader.Find(이름)`으로 셰이더를 찾아 재질을 만들었다. 에디터는 프로젝트의 모든 셰이더를 찾지만, 플레이어 빌드는 장면 · Resources · 그 의존물이 참조하는 셰이더만 넣고 `Shader.Find`는 빠진 것을 못 본다. 원래는 메뉴(ChessFight/VFX/Hazard/Build)가 만든 재질이 참조할 예정이었는데 그 메뉴 결과가 저장소에 없었다 | 이름으로 찾는 셰이더는 빌드가 참조하게 둔다: `Resources` 폴더에 두거나(스킬 효과 셰이더 방식) `Assets/Resources/VfxShaderKeep.asset` 목록에 넣고 `VfxShaderKeep.Find`로 찾는다. `Tools/run-tests-linux.sh --checks-only`가 `ChessFight/VFX/…` 이름을 검사한다. 빌드의 Player.log에서 "셰이더가 없습니다" · 분홍 재질을 먼저 본다 | R526 |
| 76 | 미니게임에서 **바닥에 놓인 물건을 잡기로 못 집음**(R528 B 판자를 처음 실제 잡기로 바꿀 때 Unity 사본 측정) / 재질 `.meta`를 복사해 새 재질을 만들 뻔함 | 서 있는 폰의 손은 잡기 키를 누르면 0.40 ~ 0.48 m 높이로 앞에 나오고, 잡기 반경은 0.25 m(+ 접촉 0.05 m)라 높이 0.12 m 판자 윗면에 닿지 않는다(0.34 · 0.68 m 더미는 닿음). 또 `Assets/Resources/PawnRushMiniGames/Materials`의 `.mat.meta` 73개는 meta 형식이 아니다(첫 줄이 `fileFormatVersion`이 아니라 재질 내용 조각, R502~R507 아트 커밋 그대로 — Unity는 지금 그대로 불러옴) | 바닥 물건을 집는 게임은 손의 물리 쥐기 대신 "잡기 키 + 손에서 N m 안"으로 판정한다(`PlankBridge.PickReach` 0.5 m, `IMiniGamePawn.GrabHeld`). 새 재질 meta는 그 폴더 것을 복사하지 말고 `NativeFormatImporter` 형식으로 새로 쓴다(예: `PR_Mid_WoodKit_Maple.mat.meta`) |
| 77 | **어떤 PC에서만 질주(Shift) 중 점프(Space)가 안 됨**, 서서 · 걸으며는 됨(11-09 빌드 시험, 참가자 PC들; 사용자는 "키 3개 이상"으로 봄) | Unity는 입력기(IME)를 Windows에 맡겨 둔다(`Input.imeCompositionMode` Auto, 게임 코드는 정하지 않았음). 한국어 Microsoft 입력기는 **Shift+Space를 한/영 전환 키**로 쓰는 설정이 흔하고, 그 창에 입력기가 켜져 있으면 Shift+Space를 입력기가 가져가 Space가 게임에 안 온다(PC마다 설정이 달라 어떤 PC만) | 게임 중엔 입력기를 끈다: `ImeGuard`(Game)가 시작부터 Off, `ImeGuard.Watch(텍스트필드)`로 등록한 입력칸에 키보드가 있을 때만 On(R530). 새 입력칸을 만들면 Watch에 등록할 것(한글 입력이 필요하면 꼭) |
| 78 | **가만히 서 있던 폰이 갑자기 수백 ~ 수천 m/s로 하늘로 튕김**(11-09 빌드 시험 "진자에 튐", 정원 선착장 · 둑; 진자 · 로봇 팔 탓으로 보였음) | 움직이는 장애물(`Obstacle`)은 키네마틱 몸을 `MovePosition`으로 옮기고 미리 충돌(`ContinuousSpeculative`)을 쓴다. 자세 함수가 **한 프레임에 크게 건너뛰면**(정원 보트가 고리 끝 |x| 24에서 반대쪽 끝으로: 48 m / (1/120초) ≈ 5,760 m/s) 그 건너뜀을 움직임으로 쓸고, 그렇게 빠른 몸의 미리 충돌 범위가 수십 m로 커져 멀리 서 있는 폰까지 쳐 낸다(보트는 물속 9.8 m 아래인데도). 방장은 코스 아래로 떨어진 폰만 되돌려 하늘의 폰이 남았다 | 자세가 끊기는 순간은 `Obstacle.JumpsBetween`으로 '끊김'이라고 알린다(그러면 쓸지 않고 그 자리에 놓음 — 마차뜰 화물 · 문 · 순회 마차가 이미 씀, R531에 정원 보트). 새 장애물의 자세 함수에 `%`(고리) · 순간이동이 있으면 꼭. 안전망: 골반 50 m/s를 넘은 폰은 곧바로 체크포인트로(`PawnRushRunaway`, R531) |
| 79 | **방장이 아닌 PC의 화면이 방 상태를 틀리게 읽음**(R533에서 발견: 입장 화면의 인원 줄이 방장 아닌 PC에서 "0 대 0") | `SteamSession`의 자리 예약 장부(`reservations`)와 자리 못 받은 사람 수는 방 주인(물리 담당) PC에만 있다. 그것을 읽는 속성(`TeamCount` · `TestBots` · `SeatingJoiners`)은 다른 PC에서 0을 돌려줬다 | 화면에 쓰는 값은 방 데이터에서 읽게 한다: 인원 = 6 − `free0` · `free1`, 자리 못 받은 사람 수 = 방장이 쓰는 `seating`(R533). 새 화면 값을 만들 때 그 값이 방 주인 PC에만 있는지 먼저 본다(R533부터 방장(관리)은 방 주인이 아닐 수 있다) |
| 80 | **경기 장면을 나중에 띄운 PC는 그 전의 일을 모른다**(R538에서 발견: 돌아온 PC가 물리 담당이 되면 떨어진 폰을 출발 칸에 되살릴 뻔함) | 폰 러쉬의 체크포인트는 PC마다 자기 물리가 트리거를 지나는 것을 보고 기록했다(R502: 새 물리 담당도 알게 하려고 모든 PC에서). 그런데 경기 도중에 장면을 띄운 PC(돌아온 사람, R536 전의 빈자리 입장)는 이미 지나간 트리거를 볼 수 없어 모든 폰의 체크포인트가 '없음'이다. 그 PC가 물리 담당이 되면(R536은 좋은 PC에게 바로 넘긴다) 누가 떨어질 때 출발 칸으로 보낸다 | '모든 PC가 스스로 알아낸다'는 값은 늦게 온 PC에게 없다. 그런 값은 코스 상태에 실어 보낸다(R538: 자리별 체크포인트 · 기물, `PawnRushNetState` CFP6 — 매번은 아니고 바뀔 때와 가끔). 새 경기 상태를 만들 때 '경기 도중에 장면을 띄운 PC도 이 값을 갖게 되는가'를 본다 |
| 81 | **38번이 다시: 참가자만 대관홀 마지막 유리 발판에서 투명 벽에 막힘**(11-11 팀원 온라인 시험, 물리 담당은 지나감, 오프라인은 괜찮음) | 7구간 판(R508 · R517, 구간 사이 복도 10 m)은 약 540 ~ 580 m까지 간다. 래그돌 스냅샷 · 스킬 패킷의 위치는 R124 뒤에도 ±320 m(16비트)라, 320 m 너머의 말은 참가자 화면에서 320 m 선에 붙어 그려졌다. 정원으로 시작한 판에서는 대관홀 안 z 118.5(마지막 유리 줄), 마차뜰로 시작한 판에서는 z 72쯤이 그 선이다. 38번의 규칙("맵 전체가 범위 안에 드는지 본다")은 코스 하나를 올릴 때 지켰지만, 구간을 이어 붙인 판이 길어질 때는 아무도 다시 보지 않았다. 디자인 차이(main · seoungtest 대관홀)와는 상관없다 | 위치를 Core `WirePoint`로(R541): 가로 x · z 24비트 1 mm 단위 ±8388 m, 높이 y 16비트 ±320 m, magic `CFRE` · v36(말 65 → 67바이트, 6명 60번은 그대로). 판을 만들 때 구간 끝이 범위에서 가로 200 m · 높이 60 m 안쪽으로 들어오지 않으면 `PawnRushRound`가 빨간 오류를 남긴다. **판에 구간 · 복도를 더하거나 판을 길게 · 높게 만들 때도 `WirePoint` 범위를 본다.** 소드 파이트 칼 상태(`SwordWire.PositionRange`)는 경기장 안이라 ±320 m 그대로 — 큰 경기장을 만들면 같이 본다 |
| 82 | **개수 · 자리가 정해진 채 한 덩어리로 구운 모델은 그 게임의 규칙을 못 바꾸게 한다**(R543: 종탑 6개를 제자리에 구운 `E_StationEnv.fbx`, 막대 4개를 한 메시로 합친 `D_Capstan_Bars.fbx` — 탑 수 · 막대 수를 인원수에 맞추려니 충돌체만 옮겨지고 그림은 옛 자리에 남을 뻔함) | 게임 코드가 만드는 상자는 숨기고 모델 하나를 통째로 걸어 둔 방식이라, 코드의 배치를 바꿔도 화면에는 드러나지 않는다(컴파일 · 자동 검사 모두 통과) | 개수나 자리가 달라질 수 있는 것은 **낱개 모델**을 내보내 게임 오브젝트마다 건다(`E_Tower_9` · `D_Capstan_Bar`, `MiniGameModels.Library` · `.Capstan`). 미니게임의 배치를 바꾸기 전에 `MiniGameModels.<방>.cs`가 무엇을 통째로 거는지 먼저 본다. 방 모델이 차지한 자리(2층 회랑 · 샹들리에 · 벽 책장)는 Blender로 FBX를 열어 잰다(R543: 회랑이 방 안으로 1.3 m, 샹들리에 반지름 1.5 m · 바닥 10.3 m). 덤: Unity 배치 점검을 `-nographics`로 돌리면 F 무도회장의 거울(RenderTexture)이 오류를 내 런타임 점검이 FAIL로 끝난다 — 그래픽을 켜고 돌린다 |
| 83 | **작업하는 동안 다른 사람이 `main`에 같은 요청 번호를 먼저 올림**(R543, 11-11: 시작할 때 HANDOFF의 '다음 빈 번호' R542로 코드 주석 · 테스트 이름 · 문서 · 그림 폴더 이름까지 다 적었는데, 푸시 직전에 fetch해 보니 팀원의 R542 커밋(경기 이름표)이 먼저 올라와 있었다) | HANDOFF의 다음 번호는 작업을 **시작한 순간**의 값이다. 몇 시간 걸리는 작업 사이에 `main`은 움직인다(팀원 · 다른 AI 채팅). 둘 다 문서 맨 위에 끼워 넣으므로 HANDOFF · VALIDATION · HISTORY · REQUIREMENTS는 늘 충돌한다 | 문서를 마감하기 전과 커밋 전에 `git fetch origin main` → `git log HEAD..origin/main`으로 번호와 겹치는 파일을 본다. 번호가 겹치면 **내가 더한 줄에서만** 바꾼다(`git diff -U0 HEAD`의 추가 줄 범위만 — 통째로 바꾸면 seoungtest가 같은 번호로 남긴 기록까지 바뀐다; 새 파일 · 폴더 이름 · 링크 앵커 `#…-r543-…`도 함께). 충돌한 문서는 상대 것을 그대로 받고(`git checkout --ours`, rebase 중에는 ours가 원격) 내 문단 · 표 · 줄을 그 위에 다시 얹는다. 합친 뒤 테스트 · 전체 컴파일을 다시 돌리고 푸시한다 |
| 84 | **작전 회의실 컨베이어를 겨눈 탄이 갑판 위 0.31 m 허공에서 터졌다**(R616 사본 점검이 "400발 중 400발이 갑판 위가 아님"으로 잡음, R454부터 있던 것). 같은 조사에서: 큰 문 조형은 문짝이 따로 없는 한 덩어리 메시였다 | 컨베이어 갑판의 충돌 상자(`Conveyor collision`)는 지을 때 캐리지 위 0.06 m에 놓였다가 **첫 물리 스텝에** `LibraryConveyorSurface`가 벨트 판의 걷는 면으로 0.31 m 내려 맞춘다. `LibraryRookTurret.Start`는 그 전에 갑판 높이를 재서 계속 썼다. 문 조형(`V09_Arched_double_door`)은 UV 없는 점토 한 덩어리(삼각형 24,000개)에 그림을 물체 원점 기준으로 투영(`PR_LibraryFacade`)한 것이라 "문짝 노드"가 없다 | 포탑은 갑판이 맞춰진 뒤(`LibraryConveyorSurface.Fitted`)에 높이를 재고 그때부터 쏜다. **Play 때 한 번 자리를 고쳐 잡는 물체의 위치를 다른 스크립트의 `Start`에서 재서 기억하지 말 것.** 탄착 · 높이 같은 것은 눈으로 안 보여도 배치 점검에서 "어디에 떨어졌나"를 숫자로 확인한다(`WarRoomEndingValidation`). 한 덩어리 조형을 움직이는 부품으로 쓰려면 런타임에 상자로 잘라낸다(`Course01v4Builder.CutDoor`: 삼각형을 상자 면에서 나눠 가장자리가 곧음) — 튀어나온 부속(문고리)이 자르는 면 앞에 남지 않는지 사진으로 확인하고, 자른 조각은 원래 물체와 같은 자리 · 축의 자식으로 둬야 투영 그림이 그대로 따라온다. TextMesh 글씨(`YardKit.Label`)는 벽 · 문을 뚫고 보이므로 문 뒤에 표지를 두지 않는다 |
| 85 | **Steam 방 기록을 쓰고 같은 순간에 방 주인을 넘기면 그 기록이 사라질 수 있다**(R644, 11-14 팀 시험: 대기실 물리 담당이 3 ~ 4초마다 왔다 갔다, 방이 터짐). `SetLobbyData`는 "조금 뒤에 묶어서" 보내고 `SetLobbyOwner` · `LeaveLobby`는 바로 보낸다(Steamworks 문서의 표현, 실제 순서를 로그로 확인한 것은 아님 · 가짜 Steam에 이 순서를 넣으니 증상이 그대로 재현됨). 방 기록은 방 주인 것만 받으므로 늦게 도착한 기록은 버려진다 | 기록을 쓴 뒤 **받는 쪽이 그 기록을 읽고 답할 때까지**(자기 member data `wclaim` / `claim` = 기록의 번호) 방 주인을 넘기지 않는다. 나가는 PC의 마지막 기록에는 기대지 않는다(남은 PC들이 순서대로 이어받는다). 가짜 Steam: `FakeSteam.DataDelay` |
| 86 | **물리 담당이 같은 PC로 되돌아오면 스냅샷 번호가 0부터 다시 시작해 다른 PC가 전부 버렸다**(R644). `SteamMotion.OnHostChanged`가 `tick = lastSnapshot`인데, 잠깐 물리 담당이 아니었던 PC는 그사이 받은 것이 없어 0이었다. 물리 담당이 바뀐 줄 모르는 다른 PC는 옛 큰 번호를 들고 있어 새 스냅샷을 옛것으로 버렸고, 대기실에서 물리 담당이 12초 넘게 "안 들리는" 상태로 있다가 경기가 시작되는 프레임에 나갔다(`Started`는 이미 참, `Loading`은 아직 거짓) | `tick = Math.Max(tick, lastSnapshot)`, 경기가 시작되는 순간 침묵을 새로 센다. 경기 씬 링크(`HostWatch`)에도 같은 모양이 있는지는 보지 않았다 |
| 87 | **Git Bash의 `sed -i`는 파일의 CRLF를 LF로 바꾼다**(R644: 시험 파일 전체가 LF가 됨) | C# · 문서를 고칠 때는 Edit 도구나 `newline=''`로 여는 파이썬을 쓴다. 바뀌었으면 `unix2dos`. heredoc 두 개를 `||`로 잇는 것도 안 된다(뒤 명령이 입력을 못 받아 대화형으로 멈춤) |
| 88 | **코드로 빈 장면에 맵을 지은 시험의 폰은 폰 러쉬 움직임 없이 달렸다**(R661: R660 마차뜰 시험 봇) | `PawnRushCourseSkills`(폰 러쉬 점프 · 중력 · 0.25 m 자동 턱 오르기 · 모서리 도움 = `PawnRushPhysicsParams`, 기물 스킬)는 `CourseSkills.PawnRush`가 켜진 채 플레이테스트 스포너가 있는 코스 장면이 열릴 때만 스스로 붙는다. 빈 장면에서 Play한 뒤 `PawnRush`를 켜면 아무도 붙이지 않아 공용 래그돌 움직임이 된다(0.55 m 턱 앞에서 점프가 계속 미끄러져 원인을 맵에서 찾게 됨) | 장면을 코드로 짓는 시험은 `PawnRushCourseSkills`를 직접 붙인다(`CarriageYardBotTest.Build`: 어셈블리 참조가 없으면 형식 이름으로 찾아서). 결과 기록에 "Pawn Rush movement and skills: … added" 줄이 있는지 본다 |
| 89 | **넘어뜨린 폰을 "그 물체를 통과하게" 해 둔 사이 일어나 걸어가면 물체 위가 아니라 속으로 들어간다**(R662 시험 봇: 승강대 판 속에 1.5초 넘게 선 봇, 조급한 봇이 같은 판에 7.4초마다 다시 맞음. 같은 판에서 우물 사이 기둥을 대각선으로 뚫고 가려던 봇은 판 밑에 남아 7.4초마다 맞음) | `YardImpact`는 넘어뜨린 폰이 그 물체와 부딪히지 않게(ghost) 두고, 경계 상자 + 0.1 m가 겹치지 않거나 `ghostSeconds`가 지나야 되돌린다. 바닥에 앉는 판 가장자리 0.1 m 안에 누운 폰은 3초 동안 판을 통과하고, 그사이 일어나 판으로 걸어가면 판 속에 선다. 또 우물 깊이(2.4 m)는 내려오는 판에 맞고 판이 닿기까지(약 0.7초) 밀려서 다 빠져나갈 수 있는 거리가 아니다. 밀기의 옆 몫(15%)은 판 가운데에서 바깥 = 기둥 쪽이었다 | 바닥에 앉는 물체는 `liftOut`: 물체가 멈추면 실제로 박힌(`Physics.ComputePenetration` 2 cm 넘게) 폰만 위로 들어 올리고(0.5초마다 다시), 박히지 않은 폰은 바로 되돌려 그 위로 올라서게 한다. 기둥 옆에서는 밀기를 곧장 바깥으로(`alongShare` 1). 물체가 내려오는 동안에도 그 발자국 아래에 있는 동안은 통과를 유지하고(`Beneath` — R662 검토: 판보다 5 cm 넘게 아래에 누운 폰은 '겹침' 검사에서 빠져 판이 닿기 전에 통과가 끝났다), 멈췄다 다시 움직이는 물체(크레인 짐)는 통과 시간을 멈춤보다 길게(4초). 물체에 얹힌 단단한 장식(자루)도 통과 대상에 넣거나 그림으로만 둔다. 시험 봇의 "몸이 무언가 속 1.5초" 이상 검사는 오래 갇힌 것을 찾지만 짧게 눌리는 것은 못 잡는다 — 검토로 본다 |

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
- **R498 미니게임:** 코드로 만든 게임 부품도 같다. 바닥 판 가장자리에 묻은 벽(해자·물 벽), 판 밑에 깔린 금 테두리, 연석 바깥 면 = 도로 옆면이 깜빡였다. 충돌체는 그대로 두고 그림만 `MissionGames.Proud`(1 cm) 떼어 놓는다(`MissionGames.LinedWall`, `PaintBoard.ClipRims`).

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

## R520 편집 모드에서는 맞고 Play에서만 겹쳐 보이던 무도회장 인형 (`?? statue`)

F 대무도회장의 석상(상대 팀 폰, 팀마다 16개)은 게임이 설치될 때마다 `MiniGameModels.Apply` → `Dancer`가 옛 모양(게임의 폰 몸통, R312 코드 옷)을 끄고 태엽 인형을 씌운다. 끄는 반복문이 "인형 부품은 건너뛰기"를 `r.transform.IsChildOf(look.Find(RideName) ?? statue)`로 썼는데, 인형이 아직 없으면 `?? statue` 때문에 석상 전체가 기준이 되어 **모든 부품을 건너뛰었다**. 편집기 Build는 `Apply`를 두 번 돌려(두 번째에는 인형이 있어 제대로 끔) 프리팹 · 편집 화면에서는 멀쩡했고, Play는 게임을 새로 깔고 한 번만 돌아 주황 원통 · 쇠 상자 옷과 검은 폰이 인형을 덮었다(사용자 11-09 "표적들이 이상하게 생긴 애들이 있음"). 인형의 금빛(잡을 수 있는 때)도 몸통을 못 찾아 한 번도 켜지지 않았다.

- "건너뛸 것"을 고르는 조건에 `??`로 넓은 기본값을 주지 않는다. 없으면 아무것도 건너뛰지 않는 쪽이 맞다.
- 그림을 끄고 켜는 코드는 Play(한 번 설치)에서 확인한다. 편집 화면은 Build가 같은 단계를 여러 번 돌린 결과다. `MiniGamePrefabValidation.RunPlayBatch`가 R520부터 F 방 석상마다 인형 말고 켜진 그림이 없는지 본다.

## R516 배치 모드 Play 점검이 편집기 오류 하나로 일시정지돼 시간 초과된다

미니게임 런타임 점검(`MiniGamePrefabValidation.RunPlayBatch`, 프로젝트 사본에서 배치 모드)이 첫 방 캡처 직후, 또는 F 무도회장을 띄운 직후 멈춰 1200초 뒤 실패했다. 로그에는 화면 갱신("no audio listeners" 수십만 줄)이 계속되는데 점검은 `Time.fixedTime`이 늘기를 기다리고 있었다. Play 중 렌더 파이프라인이 자기 편집기 에셋(`TraceRenderingLayerMask.urtshader`)에 대해 `Host type is not matching any asset type` 오류를 찍으면, 이 PC의 콘솔 설정 **Error Pause**(에디터 설정이라 사본에도 적용)가 Play를 일시정지시키기 때문이다. 게임 오류가 아니니 점검은 그 메시지를 실패로 세지 않고, 매 프레임 `EditorApplication.isPaused`를 풀어 둔다(R516). 배치 모드 Play 자동화를 새로 짤 때도 같은 함정을 본다. (R533에서 다시 밟음: 입장 화면을 그려 보는 프로브가 첫 프레임에서 멈춘 화면을 두 번 찍었다 — 사진 두 장의 파일 크기가 똑같고 카드가 "입장!"에 머물러 있으면 이것이다. 프로브는 매 틱 `EditorApplication.isPaused = false`로 풀고, 기다림은 편집기 시계가 아니라 `Time.unscaledTime`과 **프레임 수**로 센다: 사진을 찍은 직후의 한 프레임은 길어서(ReadPixels · PNG) "0.5초 뒤"가 몇 프레임 뒤일 뿐이고, 그러면 USS 색 전환 도중이 찍혀 스타일이 안 먹은 것처럼 보인다.)

## R513 걸음이 정한 높이에 있던 이동 앵커가 점프 순간 한 스텝에 내려가 점프를 끌어내린다

서 있는 동안 래그돌의 이동 앵커(키네마틱)는 걸음이 원하는 골반 높이에 있다. 질주 출렁임(`sprintLift` + `sprintBob`)이 있으면 그 높이는 처지는 골반보다 최대 0.12 m 위다. 점프하면 공중 규칙대로 앵커를 "골반 + 한 스텝"으로 보내는데, 키네마틱은 한 스텝에 움직인 거리가 곧 속도라(PITFALLS 20) 앵커가 아래로 약 7 m/s로 움직인 셈이 되고, 관절 감쇠기(945 N·s/m)가 50 kg 몸을 같이 끌어내려 상승 2 m/s를 잃었다. 상승 보정(`GuardJumpRise`)은 한 스텝 0.5 m/s 넘는 손실을 천장 부딪힘으로 보고 포기해서, 폰 러쉬 질주 점프가 0.58~0.86 m(정상 1.09)로 낮고 들쭉날쭉했다. 코드만 읽어서는 안 보였고 단계별 기록(`LabAutoTest.PawnRushMove`의 가장 낮은 점프 추적)에서 첫 스텝에 2 m/s가 사라지는 것으로 찾았다. R513는 점프 순간 앵커를 골반 높이로 옮겨 놓고(`Rigidbody.position`, 속도 없이) 시작한다(폰 러쉬만, `Launch`도 같은 방법). 앵커를 공중 · 땅 규칙 사이로 바꾸는 다른 순간(착지 등)도 같은 함정을 본다.

## R508 경기 시작 전의 임시 시작 시각을 1시간 넘게 잡으면 마차뜰 마차가 바로 출발한다

구간 라운드는 장면이 열리자마자 지어지지만 로비의 go는 모든 PC가 로딩을 마친 뒤에 온다. 그 사이 구간의 시작 시각을 "아주 먼 뒤"로 잡고 싶어지는데, 왕실 마차뜰의 마차 무리(Core `RoamingFleet`)는 계획한 시작보다 `Leap`(3600초) 넘게 이른 시각을 **시계가 뛴 것**으로 읽어 그 시각에서 바로 다시 시작한다. 지금+3600초로 잡으면 go 전에 PC마다 제 시각에 마차가 출발한다. R508은 임시 시작을 지금+600초(`SteamPawnRushLink.ProvisionalStart`)로 두어 성문은 닫혀 있고 마차는 집에서 기다리게 했고, go가 오면 `SetStartClock`으로 다시 계획한다. 시간으로 움직이는 새 장치가 시계 뜀을 따로 다루면 같은 함정이 있는지 본다.

## R507 seoungtest 계열 브랜치를 main에 병합하면 Intro·Lobby가 지워진다

`seoungtest`는 시험 브랜치로 만들며 Intro·Lobby·RagdollTest 같은 main 장면과 빌드 장면 목록을 지웠다. 그 브랜치에서 갈라진 브랜치(예: `claude/nifty-tesla-8212va`)에도 이 장면들이 없다. 그런 브랜치가 main을 한 번 합친 적이 있으면(그 브랜치 쪽 병합 `afd5de6`가 장면 삭제를 그대로 둠) git은 "그 브랜치가 장면을 지웠다"로 읽어, 다시 main에 합칠 때 Intro·Lobby 삭제와 옛 빌드 목록·옛 네트워크 코드가 충돌 없이 딸려 올 수 있다. 사용자도 그 브랜치를 Unity에서 열면 Scenes 폴더에 Intro·Lobby가 안 보여 "지워졌나" 하고 묻게 된다(R492·R507 두 번).

R507은 `git merge -s ours --no-commit`으로 main 트리를 그대로 둔 채 병합 기록만 만들고, 그 브랜치가 **직접 만든 커밋의 변경**(`git diff <갈라진 점> <그 브랜치 커밋>`, 병합 커밋은 빼고)만 `git apply --3way`로 얹었다. 마지막에 `git diff --cached --name-status HEAD`에 `D`가 없고 `A`·`M`이 모두 그 브랜치 작업인지 확인한다. 같은 이유로 그 브랜치의 요청 번호(R283~R288)는 main 번호와 겹치니 "· nifty-tesla"처럼 브랜치를 붙여 옮긴다.

Linux 검사(`Tools/run-tests-linux.sh --compile`)에는 R507 때 main 자체에 이미 두 가지 Linux 전용 실패가 있었다(병합 전 `98f7e0e`에서도 같음). ① Core "Packets (R297 [3])" 고정 바이트의 6번째 값(`PawnRushNetState`)이 mono에서 다르게 나와 첫 실패에서 Core 시험 전체가 멈춘다(Windows `Test-NetworkCore.ps1` 기록은 통과). ② PawnRush 에디터 어셈블리 컴파일이 쓰는 UnityEditor.dll(2021.1)에 `Texture.imageContentsHash`·`EditorSceneManager.OpenPreviewScene`·`RenderPipeline.StandardRequest`가 없어 `MiniGamePrefabMenu`·`MiniGamePrefabValidation`에서 오류 6개가 난다. 검사를 끄거나 고정값을 바꾸지 말고, 로컬에서만 그 한 줄을 출력으로 바꿔 나머지 시험·컴파일을 돌린 뒤 되돌리고, 두 실패를 따로 보고한다.
## 43. MonoBehaviour 필드 초기화에서 `new MaterialPropertyBlock()` 금지 (R506)

`readonly MaterialPropertyBlock b = new MaterialPropertyBlock();`처럼 필드에서 만들면 Unity가 "CreateImpl is not allowed to be called from a MonoBehaviour constructor (or instance field initializer)"를 내고 b가 null로 남는다. 그 뒤 필드 초기화(배열 등)도 함께 실패해 Update가 매 프레임 예외로 멈추고, 화면에는 "아무 일도 안 일어남"으로만 보인다. Awake/Update에서 처음 쓸 때 만든다. 증상이 안 보이면 먼저 Editor.log를 본다.

## R652 세션이 갑자기 끝난 뒤 직전에 쓴 파일이 0으로 채워져 있다

11-14 22:50에 `git checkout`으로 되살린 `Tests/Network/FakeSteam.cs`는 시험을 통과했는데, 앱 세션이 다시 시작된 뒤 같은 크기(19,362바이트)에 내용이 전부 0x00이 되어 있었다(수정 시각은 그대로). 디스크에 다 쓰기 전에 꺼진 것으로 보인다. git에는 "수정됨"으로만 보이고 컴파일러는 `CS1056 Unexpected character`를 낸다. 세션이 끊긴 뒤에는 그 전후로 쓴 파일을 `tr -d '\000' < 파일 | wc -c`(0이 아닌 바이트 수)로 훑어 0으로 찬 파일을 찾고, 커밋이나 원본에서 다시 꺼낸다. `git fsck`는 저장소 쪽만 본다.

## R654 Unity 오디오 버퍼 1024에서는 소리가 누른 뒤 약 85 ms 늦게 난다

`ProjectSettings/AudioManager.asset`의 DSP 버퍼가 1024(Best performance)이면 Windows에서 AudioSource.Play부터 소리까지 버퍼 넷, 48 kHz에서 약 85 ms가 걸린다. 사용자에게는 "앞에 빈소리가 있는 것처럼" 들렸다. R654에서 512(Good latency, 약 43 ms)로 낮추고 소리 파일의 앞부분도 타격 2 ms 앞에서 자르게 했다. 256(Best latency)은 사양 낮은 PC에서 끊길 수 있어 쓰지 않았다.

## 44. 메쉬 에셋을 `EditorUtility.CopySerialized`로 덮으면 정점 수가 섞임 (JY-piece-rig 11-12)

- **증상:** 새 로우폴리 폰(10,630정점)으로 다시 구운 뒤, 폰이 모든 모드에서 늘어난 조각으로 그려졌다. 물리 자동 테스트는 통과했다. 충돌체는 메쉬 에셋이 아니라 계산한 위치로 만들기 때문이다.
- **원인:**
  - `RagdollLabBuilder.SaveAsset`이 새 메쉬를 기존 `PawnSkinned.asset`(옛 폰 6,960정점)에 `CopySerialized`로 복사했다.
  - 저장된 YAML에 `vertexCount: 10630`과 `m_VertexCount: 6960`이 함께 남았다.
  - Unity는 6,960개로 읽었고, 웨이트 번호가 엉뚱한 뼈(오른팔·손가락)를 가리켰다.
- **해결:** 메쉬는 `Clear()` 뒤 제자리에서 채운다(`RagdollLabBuilder.FillMesh`, `PieceModelBaker`가 이미 쓰던 방식).
- **확인 방법:**
  - 에셋 파일에서 `vertexCount`와 `m_VertexCount`가 같은지 본다.
  - 배치에서 프리팹을 장면에 꺼내 그려 본다. 자동 물리 테스트만으로는 겉모습이 깨진 것을 못 잡는다.

## 45. 팔 뼈를 다른 물체 밑으로 옮기면 뼈대의 100배를 잃어 팔뚝이 한 점으로 그려짐 (R669, 11-15)
- **증상:** 폰과 모든 기물이 손 공만 보이고 팔뚝이 없음(승규 님: "손은 있는데 팔이 없어"). 편집 모드 미리보기는 정상.
- **원인:** JY-piece-rig 뼈대는 Armature 크기가 100이고 메쉬는 1/100 단위다. `RagdollVisualSync.Setup`이 팔 뼈를 늘이기 보조 물체(크기 1) 밑으로 `SetParent(helper, false)`로 옮겨, 팔 뼈의 세계 크기가 1이 되었다. 스키닝 행렬이 1/100이 되어 팔뚝 정점이 뼈 자리 한 점에 모였다.
- **해결:** 옮기기 전 `lossyScale`을 기억했다가 새 부모 밑에서 같게 맞춘다. 뼈를 옮기는 코드는 늘 세계 크기를 지킨다(`SetParent(…, true)` 또는 크기 다시 맞춤).
- **확인 방법:** Play 중 `SkinnedMeshRenderer.BakeMesh`로 그린 정점을 뼈별로 모아 폭을 잰다(0이면 이 문제). `bone.localToWorldMatrix * bindposes[i]`의 열 길이가 뼈마다 같은지 본다.

## 46. 물리 스텝마다 줄어드는 짧은 표시값은 다른 PC가 놓친다 (R670, 11-15)
- **증상:** 소드 파이트 온라인에서 참가자 화면에 타격 소리 · 타격 이펙트가 안 나오는 타격이 생김(상대는 넘어짐). 승규 님 보고는 "온라인에서 피격 할 때 잘 안맞는 경우가 있는 것 같고"였고, 아래 원인은 코드로 따진 것이다(두 PC에서 직접 보지는 못함).
- **원인:** 맞은 표시(`SwordFightPawn.HitFlash`)를 0.14초로 켜고 물리 스텝(1/120초)마다 줄였는데, 다른 PC는 상태 패킷에 실린 값이 0.13 이상으로 뛰어야 타격으로 본다. 0.13 이상인 시간이 8 ~ 17 ms뿐이라 17 ~ 33 ms 간격 패킷이 자주 건너뛰었다. 맞은 기물의 스텝이 벤 기물보다 뒤에 도는 짝은 같은 스텝에서 바로 한 번 줄어 더 짧았다.
- **해결:** 0.3초(`CutFlash`)로 켠다: 0.13 이상 170 ms, 가장 느린 전송(30 Hz)에서도 다섯 장에 실린다.
- **규칙:** "값이 뛰면 일이 일어난 것"으로 읽는 표시값은 문턱 위에 **패킷 간격의 세 배 이상** 머물게 하거나, 눌린 횟수처럼 세는 값(`PressCount`) · 번호 붙은 순간(`SkillWire` 이벤트)으로 보낸다. 방장 화면에서는 직접 불려서 늘 잘 나오므로, 한 PC 시험으로는 드러나지 않는다.

## R679 경기 중 클릭하는 화면이 `PointerTaken`을 안 세우면 첫 클릭에 커서가 다시 잠긴다 (11-16)

승격 카드(`PromotionChooser`)는 커서를 풀고 `GUI.Button`으로 클릭을 받게 돼 있었는데, 6 대 6 시험에서 "마우스로 선택이 안 된다". `OrbitCamera.Update`는 커서가 풀려 있을 때 마우스 버튼이 눌리면 **다시 잠근다**(`GameSettings.PointerTaken`이 꺼져 있으면). 그래서 카드를 누른 그 클릭에 커서가 잠겨 포인터가 화면 가운데(2 · 3번 카드 사이 틈)로 가고, 같은 클릭이 다이빙으로도 나갔다. 카드는 커서를 한 번만 풀었으므로 그 뒤로는 계속 못 눌렀다.

- 경기 중에 마우스로 누르는 화면(메뉴 `GameMenu`, 경기 표 `VoiceHud`, 승격 카드)은 떠 있는 동안 `GameSettings.PointerTaken = true`.
- 커서는 한 번이 아니라 **떠 있는 동안 매 프레임** 풀어 둔다(다른 화면이 닫히며 잠그거나 창이 뒤로 갔다 오면 다시 잠긴다).
- 돌려줄 때 다른 화면(메뉴 · 경기 표)이 아직 열려 있으면 `PointerTaken`을 끄지 않는다.

## 47. 스텝 뒤쪽에서 바뀌는 단계는 '앞뒤 비교'로 못 본다 (R683, 11-15)
- **증상:** 소드 파이트가 끝난 방이 계속 '경기 중'으로 남아, 나간 사람 모두에게 '경기로 돌아가기'가 뜨고 새 매칭이 막힘(11-15 빌드 시험, 다섯 PC). 마지막 점수의 슬로 모션도 없었음.
- **원인:** 물리 담당(`SwordFightMatch.FixedUpdate`)이 `rules.Advance` 앞뒤로만 단계를 비교했는데, 경기를 끝내는 득점(10점 · 서든데스)은 같은 스텝 뒤쪽의 낙하 처리(`RingOut`)에서 난다. 다음 스텝에는 처음부터 결과 단계라 '바뀜'이 한 번도 참이 아니었고, 결과 진입에 달린 것(방에 끝 알리기 · 슬로 모션 · 싸움 멈춤)이 R149부터 득점 종료에서는 한 번도 안 돌았다. 시간 종료(`Advance`가 끝냄)만 정상이라 시험에서 안 드러났다.
- **규칙:** 상태가 한 스텝 안 여러 곳에서 바뀔 수 있으면, 읽는 쪽이 비교하지 말고 **바꾸는 쪽이 알리게** 한다(Core `SwordMatchRules.TakeEntered`: 들어간 단계를 순서대로 한 번씩 건넴, Core 시험 "R683 Sword Fight …").

## 48. 코드로 건 UI Toolkit 필터(blur · grayscale)는 빌드에 셰이더가 없다 (R683, 11-15)
- **증상:** 빌드에서만 `ArgumentNullException: shader`(`FilterFunctionDefinitionUtils.CreateBlurFilterFunctionDefinition`) — 입장 화면의 코스 공개 · 기물 선택이 열릴 때마다. 뒤 배경이 안 흐려지고 그 프레임의 `Update` 나머지가 건너뛰어짐. 편집기에서는 멀쩡함.
- **원인:** Unity는 `style.filter`의 재질을 `new Material(Shader.Find("Hidden/UIR/GaussianBlur"))`(회색 등은 `"Hidden/UIR/ColorEffect"`)로 만든다(6000.3.11f1의 `UnityEngine.UIElementsModule.dll`을 풀어 확인). 이 두 내장 셰이더는 PanelSettings **에셋**(`m_RuntimeGaussianBlurShader` · `m_RuntimeColorEffectShader`)이 참조해야 빌드에 들어가는데, 이 게임은 PanelSettings를 코드로 만든다(`RuntimePanels` · `ScreenOverlay`의 `CreateInstance`) → 어느 빌드에도 없어 `Shader.Find`가 null.
- **고침 · 규칙:** `ProjectSettings/GraphicsSettings.asset`의 Always Included Shaders에 내장 20300 · 20301을 넣었다(`Tools/run-tests-linux.sh --checks-only`가 본다). 필터를 거는 코드는 셰이더가 있는지 먼저 묻고 실패해도 화면이 이어지게 한다(`LoadingScreenView.SoftenStage`). 다른 필터 종류를 새로 쓰면 그 셰이더 번호를 편집기에서 확인한다(`AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Shader.Find(이름), …)`). 빌드로는 아직 확인 못 함.

## 49. 실패한 Steam P2P 세션은 저절로 다시 열리지 않는다 (R683, 11-15)
- **증상:** 경기 시작 약 12 ~ 20초 뒤 "물리 계산을 맡은 PC와 연결이 끊어졌습니다"로 나가짐 — 로딩 중 Player.log에 `Steam P2P 연결 실패 … 5008`(또는 시간 초과)이 한 줄 있고, '경기로 돌아가기'로 돌아가면 바로 연결됨(11-15, 두 PC).
- **원인:** `SteamNetworkingMessages.SendMessageToUser`는 세션이 한 번 실패하면 `CloseSessionWithUser`를 부르거나 `k_nSteamNetworkingSend_AutoRestartBrokenSession`을 줄 때까지 계속 `k_EResultNoConnection`을 돌려준다(SDK 설명). 실패 콜백은 로그만 남겼고 전송에는 그 깃발이 없어, 한 번 실패한 연결이 방을 나갈 때까지 죽어 있었다. (상대 PC가 아직 이 PC를 명단에 안 올려 세션 요청을 안 받는 순간에도 실패할 수 있다.)
- **규칙:** Steam P2P 전송은 `SteamSend.Unreliable` · `Reliable` · `Flags`로만 한다(새 전송 줄을 만들 때도). 실제 Steam에서는 확인 전.

## 50. kinematic 몸에는 속도를 넣을 수 없다 — 먼저 풀고 넣는다 (R683, 11-15)
- **증상:** 물리 담당을 이어받는 순간 Player.log에 "Setting linear velocity of a kinematic body is not supported" · "angular"가 기물마다 22줄(몸 11조각 × 2). 11-15 한 PC에서 770줄.
- **원인 · 고침:** `RagdollPawn.SetNetworkPuppet(false)`가 `isKinematic = false`보다 먼저 속도를 0으로 넣었다(Unity는 무시하고 경고). 순서를 바꿨다. 꼭두각시(kinematic) 몸을 물리로 되돌릴 때는 늘 푼 다음에 속도를 정한다.

## 51. Steam 방 사람 목록은 PC마다 잠깐 다를 수 있다 — 넘겨받자마자 자리를 빼지 않는다 (R689, 11-15)
- **증상:** 대기실 물리 담당이 성능 1등 PC로 넘어간 1초 뒤, 방을 만든 사람이 "예약이 해제되었거나 파티원 연결이 끊겼습니다"로 튕겼다가 4초 뒤 다시 들어옴. 넘겨받은 PC의 Player.log에 그 순간 "자리 정리: ○○ 님 파티가 모두 방을 나감"(그 사람 이름이 방 안 이름이 아니라 Steam 이름으로 — 그 PC의 Steam 목록에 없었다는 표시).
- **원인:** 넘겨받은 PC는 방이 자기 것이 되는 순간 자리 장부를 Steam 방 목록과 맞춰 보고(`TeamReservations.Reconcile`) 목록에 없는 사람의 자리를 바로 풀었다. 그 순간 그 PC의 Steam만 방에 있던 사람을 목록에 안 보였다(이유는 모름).
- **규칙:** 다른 PC에서 넘겨받은 장부를 처음 맞출 때는 Steam 목록 한 번만 보고 자리를 빼지 않는다 — 앞 물리 담당이 앉힌 사람은 3초 지킨다(`SteamSession.HoldAdoptedSeats`).

## 52. 경기에서 나간 물리 담당의 명단은 방에 남는다 — 돌아온 PC는 명단이 아니라 답을 믿는다 (R689, 11-15)
- **증상:** 물리 담당이던 PC가 경기에서 나갔다가 곧바로 '경기로 돌아가기'로 돌아온 지 2초 뒤 "예약이 해제되었거나 파티원 연결이 끊겼습니다"로 다시 나가짐(명단 1, 자기 없음).
- **원인:** 방 데이터는 방 주인만 쓴다. 나간 물리 담당이 마지막에 쓴 명단(자기가 든)은 다음 물리 담당이 방 주인이 될 때까지 그대로 남는다. 돌아온 PC는 그 명단에서 자기를 보고 입장된 것으로 여겨 자리 요청을 멈췄고, 다음 물리 담당이 방을 받은 뒤 쓴 첫 명단(나가 있던 동안 빠진)에서 빠져 나갔다.
- **규칙:** 돌아오는 PC는 물리 담당의 답(`grant` = `ok`)을 받은 뒤에만 앉은 것으로 본다(`SteamSession.PollMatch`). 방의 명단은 누가 언제 썼는지 모르는 값이다.

## 53. 사본에 복사한 Library 때문에 사본의 Unity가 열린 에디터의 IL 후처리 도우미를 끈다 (R719, 11-19)
- **증상:** 원본 Library를 사본(`C:\유니티\ChessFight_TestCopy`)으로 통째로 복사해 배치 Unity를 돌린 뒤, 원본 에디터의 `Library/ilpp.pid`에 적힌 `Unity.ILPP.Runner`가 사라졌다. 원본 에디터는 다음 스크립트 컴파일 때 다시 띄운다(R684 마차뜰 충돌 점검도 11-16에 "Found a lingering IL Post Processing runner process ... Killing it"을 봤다).
- **원인:** 복사한 Library의 `ilpp.pid` · `EditorInstance.json` · `ArtifactDB-lock` · `SourceAssetDB-lock`은 원본 에디터의 것이다. 사본의 Unity가 그 PID를 남은 프로세스로 보고 끈다.
- **규칙:** 사본에서 Unity를 켜기 전에 그 네 파일을 지운다(`Tools/GrdProbe/Run-GrdProbe.ps1` · `Tools/ColliderAudit/Run-CarriageYardColliderAudit.ps1`은 지움, `Tools/Run-CarriageYardBots.ps1`은 아직 안 지움). Library를 복사해 사본을 새로 만들 때 특히 조심한다.

## 54. BatchRendererGroup Variants "Keep All"의 저장 값은 2다 — 1은 Strip All (R719, 11-19)
- **증상(예상):** GRD 작업 프롬프트가 `ProjectSettings/GraphicsSettings.asset`의 `m_BrgStripping`을 1로 예상했다. 1로 쓰면 DOTS 인스턴싱 변형이 모두 빠지고 GRD는 경고와 함께 꺼진다.
- **확인:** 6000.3.11f1의 `UnityEditor.Rendering.BatchRendererGroupStrippingMode` = KeepIfEntitiesGraphics 0 · StripAll 1 · KeepAll 2(DLL에서 직접 읽음). 코드 속성 `EditorGraphicsSettings.batchRendererGroupShaderStrippingMode`는 이 버전에서 읽기 전용이다(쓰면 CS0200). 편집기 코드로 바꾸려면 `SerializedObject`로 GraphicsSettings의 `m_BrgStripping`을 쓴다. 켜진 에디터는 파일을 고쳐도 새로고침 때 다시 읽는다.
- **같이 겪은 것:** `GPUResidentDrawer` · `GPUResidentDrawerMode`는 `Unity.RenderPipelines.GPUDriven.Runtime` 어셈블리에 있다. URP 에셋의 `gpuResidentDrawerMode`를 쓰는 편집기 코드는 asmdef에 그 참조가 있어야 한다(없으면 CS0012). Unity 밖에서 컴파일을 미리 볼 때 ScriptAssemblies의 DLL을 다 넣으면 이걸 못 잡으니, asmdef에 적힌 참조만 넣고 본다.

## 55. GPU 가림 컬링을 켜면 큰 메시(바닥 · 벽 · 천장 · 성문)가 사라진다 (R719, 11-19, AI 측정)
- **증상:** 사본 배치 모드(Intel Iris Xe · D3D11)에서 GRD + GPU Occlusion Culling을 켜면 마차뜰 입구의 바닥 · 성문 · 탑, 왕의 서재의 벽 · 천장 · 문, 대무도회장 · 침실 · 거인 체스의 큰 껍데기가 사라지고 작은 물체만 남는다. 순간이동 직후만이 아니라 가만히 있어도, Unity가 카메라를 스스로 그려도 그렇다. GRD만 켜면 이상 없다.
- **원인:** 확정 못 했다. 카메라를 감싸는 큰 메시(경계 구 안에 카메라가 있음)가 가려진 것으로 판정되는 것으로 보인다. 서재의 벽은 Play 때 코드가 만드는 메시(`RoyalLibraryDressing.Reskin`)라 Unity 이슈 UUM-108782(런타임 메시 + GRD에서 사라짐)와도 비슷하다.
- **규칙:** 가림 컬링은 켜지 않는다(승규 11-19). 다시 볼 때는 다른 GPU · 새 Unity 버전에서 `Tools/GrdProbe/Run-GrdProbe.ps1 -Mode sweep`으로 장면 전체 사진을 비교하고, 사람이 Game 창에서 본다. [GPU_RESIDENT_DRAWER §6](GPU_RESIDENT_DRAWER.md)

## 56. 배치 모드에서 렌더링을 잴 때의 함정 (R719, 11-19)
- Unity의 Batches · SetPass · Draw Calls · Triangles 카운터(ProfilerRecorder · `UnityStats`)는 배치 모드에서 프로파일러를 켜도 0이다. 프로파일러를 켜 두니 "running out of memory" 경고까지 났다(16 GB 노트북, 원본 에디터와 동시). 그리기 수는 사람이 Game 창 Stats로 본다.
- 배치 모드에서는 아무것도 스스로 그려지지 않는다. Play 중에 URP 에셋 값(GRD 등)을 바꾸면 파이프라인은 다음 카메라 렌더 때 다시 만들어지므로, 바꾼 뒤 한 번 그려야 GRD가 켜지고 꺼진다(그리기 전에 상태를 보면 한 박자 늦다).
- 렌더 스레드가 없다(`threading SingleThreaded`). "CPU main · render thread"를 나눌 수 없고 `Gfx.WaitForPresentOnGfxThread`도 없다. 카메라를 렌더 텍스처에 그린 시간(CPU)과 GPU가 끝날 때까지(합계)로 잰다.
- 렌더링 디버거용 GRD 통계(`debugStats.enabled`)를 켠 채 재면 GRD 쪽이 3 ~ 4 ms 더 나온다. 시간을 재는 구간에서는 끈다.
- 꺼 둔 카메라를 `Camera.Render()`로 손으로 그리는 것과, 켜 두고 targetTexture를 줘서 플레이어 루프가 매 프레임 그리게 하는 것(게임과 같은 길)을 둘 다 써서 결과를 맞춰 봤다 — 가림 컬링 누락은 둘 다에서 났다.

## 90. 에디터 검사로 지은 구간에서는 Awake가 돌지 않는다 — 자식은 Configure에서 짓는다 (R725, 11-18)
- **증상:** 에디터 검사(배치, Play 아님)가 구간 프리팹을 빈 장면에 놓고 빌더를 불러 지으면, Awake에서 자식을 만드는 부품(왕실 금고실의 뒤집히는 판 갑판 등)이 비어 있어 바닥 · 이름 검사가 틀린 답을 낸다.
- **원인:** Edit 모드에서 `Instantiate`한 오브젝트의 MonoBehaviour는 Awake가 불리지 않는다(`ExecuteInEditMode`가 아니면).
- **규칙:** 부품의 자식(갑판 · 충돌체 · 신호)은 빌더가 부르는 `Configure`에서 바로 짓고, 두 번 짓지 않게 막는다(`VaultPlate.Configure`의 `built`). Awake는 이미 지은 것을 찾기만 한다. 검사는 Awake 없이도 모든 것이 있어야 통과하게 쓴다(금고실 E4).

## 91. for 문 안의 람다는 반복 변수를 함께 쓴다 — 번호를 복사해 넘긴다 (R725, 11-18)
- **증상:** 장애물 여러 개를 `for (int k …)`로 지으며 `t => Rules.DrawerOut(k, t)`를 넘기면 모든 장애물이 마지막 번호의 박자로 움직인다(마지막 실행 때 k 값).
- **원인:** C#의 `for` 반복 변수는 반복마다 새로 생기지 않는다(`foreach`만 C# 5부터 새로 생김). 람다는 변수 자체를 잡는다.
- **규칙:** 람다 앞에서 `int d = k;`로 복사하고 그것을 쓴다(금고실 빌더의 서랍 · 격벽 · 판, 주석 "copied: each lambda keeps its own").

## 92. Windows에서 mkmeta.py가 만든 .meta는 CRLF다 — LF로 바꿔 커밋한다 (R725, 11-18)
- **증상:** 새 .meta 20개가 CRLF였다. `.gitattributes`가 `*.meta eol=lf`라 커밋하면 줄 끝이 바뀌어 다음 체크아웃마다 '바뀐 파일'로 보이거나 Unity가 다시 쓸 수 있다.
- **규칙:** `Tools/Generators/mkmeta.py`로 만든 뒤 내용이 mkmeta 출력 그대로인지 보고 LF로 바꾼다. 커밋 전 `file <path>.meta`로 확인.

## 93. 소수점이 정확히 맞아떨어지는 경계는 시계 크기에 따라 다르게 반올림된다 (R725, 11-18)
- **증상:** Core 시험이 같은 계산을 시계 0과 실제 크기 시계(약 1.76e9초)에서 돌리자 벨트 타는 시간이 10,000개 출발 위상 중 717개에서 0.02초 달랐다(`RoyalVaultRules.BeltRideSeconds`).
- **원인:** 동쪽 벨트의 두 경사가 정확히 250걸음 떨어져 걸음마다 상쇄돼, 430걸음 뒤 거리가 정확히 14.0 m가 되는 경우가 많았다. 그때 한 걸음을 더 갈지(`z < length`)를 반올림 오차가 정했고, 큰 시계에서는 오차가 달랐다.
- **규칙:** 걸음을 세는 반복의 끝 조건은 작은 여유를 둔다(`z < length - 1e-9`). 박자 규칙의 Core 시험은 시계 0과 실제 크기 시계 둘 다에서 같은 답이 나오는지 본다(Core 시험 "Royal vault clock").

## 94. Obstacle은 Update에서 한 물리 스텝 늦은 자세를 보여 준다 — 검사는 시계를 멈추고 FixedUpdate를 기다린다 (R725, 11-18)
- **증상:** Play 검사가 장애물 자세를 규칙 함수와 비교하면 격벽 3.3 cm, 판 0.63°, 떨어지는 프레스 다이 약 25 cm가 어긋나 허용치(±0.02 m · ±0.5°)를 넘었다.
- **원인:** `Obstacle`은 transform을 보간한다. Update에서 읽으면 최대 한 스텝(120 Hz) 전 자세다.
- **규칙:** 검사는 `ObstacleClock.Use(() => T)`로 시계를 멈추고 FixedUpdate 두세 번 뒤에 읽거나, `VaultMover.AlongAt` · `DegreesAt`처럼 규칙 값과 비교한다.

## 95. 움직이는 부품 하나에 YardImpact 껍데기 둘이면 두 껍데기에 걸친 폰이 한 스텝에 두 번 맞는다 (R725, 11-18)
- **증상:** 왕관 프레스 다이의 두 껍데기(남쪽 −z · 북쪽 +z 밀기) 경계 z 110에 걸친 폰이 같은 물리 스텝에 6.3 m/s, 이어 9.6 m/s로 서로 반대로 밀렸다.
- **원인:** YardImpact의 대기 시간(`cooldown`) · `lastHit`은 껍데기마다 따로다. 장애물 사이의 연속 타격 유예(`YardKit.RecentlyDown`)는 두 번째 넘어짐만 막고 두 번째 밀기는 그대로 넣는다. `replaceSpeed`면 두 번째가 첫 번째를 지우고 자기 속도를 더한다.
- **규칙:** 움직이는 부품 하나 = 껍데기 하나. 방향은 껍데기 하나의 `lane` · `alongShare`로 낸다(프레스: 길 가로 방향 lane + alongShare 0 → 다이 가운데 선에서 몸이 있는 쪽으로). 같은 장애물이 따로 미는 것(충격파)은 그 껍데기에 맞은 폰을 건너뛴다(`YardKit.HitGiven`).

## 96. YardKit 표지 글자는 벽을 뚫고 보인다 (R725, 11-18)
- **증상:** 왕실 금고실 아치의 "7"이 12 m 앞 닫힌 문 너머로 보였다. 다른 맵의 YardKit 표지도 같다.
- **원인:** `YardKit.Label`의 글꼴 재질이 GUI/Text Shader(ZTest Always)를 쓴다.
- **규칙:** 회색 모형 단계에서는 표지를 벽 앞 열린 곳이나 머리 위에 둔다. 고치는 것은 아트 단계(가려지는 글자 재질)로 미뤘다 — 모든 YardKit 표지가 바뀌므로 따로 정한다.

## 97. 봇의 점프 판단이 '장애물이 지금 내 자리에 오는 때'만 보면 자기 움직임을 놓친다 (R725, 11-18)
- **증상:** 금고실 허들 회랑(안전한 길)에서 봇의 75%가 넘어졌다(목표 15 ~ 55%). 정확한 박자를 읽는 봇도 12번 중 8번 넘어졌다.
- **원인:** 명세의 봇 규칙은 막대가 골반의 '지금' 자리에 오는 시각이 점프 창에 들면 뛰었다. 봇은 그동안 1 ~ 2 m 움직인다. 둘째 허들은 거의 질주 속도로 뒤에서 오므로 그 규칙으로는 맞는 점프가 거의 없다.
- **규칙:** 봇의 점프는 '지금 누르면'을 흉내 낸다: 0.01초씩 날아가는 동안 골반도 자기 속도로, 막대도 규칙 함수로 움직여 발이 막대 위인지 본다(`RoyalVaultBotTest.HurdleFlight`). 길도 막대가 정면에서 오는 쪽을 고른다. 결과: 조심하는 봇의 허들 넘어짐 0, 회랑 넘어짐 비율 36%(눈 감은 봇의 몫). 사람 판단은 아니다.

## 98. YardImpact의 `ghostSeconds`만 늘려서는 통과가 길어지지 않는다 — 몸이 부품 상자와 겹치는 동안만 이어진다 (R725, 11-18)
- **증상:** 금고실 허들이 넘어뜨린 폰을 다른 팔이 그냥 지나가게 하려고 `ghostSeconds`를 2.5초로 했다. 그런데 모형(맞은 99번)에서 통과는 맞고 0.35 ~ 1.06초(중앙값 0.35)에 끝났고, 다른 팔이 올 때까지 이어진 적은 한 번도 없었다.
- **원인:** YardImpact는 0.35초가 지나면 `Overlaps`(`solid` + `alsoSolid` AABB, 0.1 m 여유)가 참인 동안만 통과를 유지하고, 한 번 거짓이 되면 끝낸다. 도는 가는 막대의 AABB는 반 바퀴에 두 번 막대 선을 따라 얇아진다.
- **규칙:** 오래 통과시켜야 하는 부품은 충돌하지 않는 트리거 상자를 `alsoSolid`에 넣어 AABB를 넓힌다(`RV hurdle k zone`: 스크립트 없음, 부딪히는 껍데기와 다른 자식이라 그 OnTriggerStay가 받지 않음). 그 전에 그 맵의 물리 질의가 모두 `QueryTriggerInteraction.Ignore`인지 확인한다. 검사기(E14)가 그 트리거를 받아들이게 한다.

## 99. YardImpact `cooldown`을 길게 하면 비틀거린 몸에도 걸린다 — 비틀린 몸은 통과되지 않아 그동안 단단한 부품이 그냥 민다 (R725, 11-20)
- **증상:** 허들 대기 시간을 1.94 / 2.1초로 늘리자 허브 가까이에서 비틀거린(넘어지지 않은) 폰이 그동안 타격 판정 없이 굴대에 밀렸다(모형: 한 번 지나갈 때 1.69번).
- **원인:** `lastHit`는 넘어짐과 비틀거림을 다 기록한다. 통과(LetThrough)는 넘어짐에만 있다.
- **규칙:** 넘어짐에만 긴 대기를 주려면 `staggerCooldown`(R725, 음수면 `cooldown`과 같음 — 다른 장애물은 그대로)으로 비틀거림의 대기를 따로 짧게 한다(허들 1초).

## 100. `knockdownSeconds`는 '잃는 시간'이 아니다 — 래그돌은 낮게 누워 있거나 멈춰 있을 때만 센다 (R725, 11-20)
- **증상:** 서랍의 밀기를 넉다운(기본 0.9초)으로 바꾸면 손해가 1.5초쯤 늘 줄 알았는데 모형으로는 약 0.6초만 늘었다.
- **원인:** RagdollPawn은 땅이 있고 골반이 서는 높이 + 0.05 m 아래이거나 멈춰 있을 때만 넉다운 시간을 센다. 서랍에서 4 m 떨어지는 0.71초는 세지 않는다. 폰 러쉬의 일어서기는 `PawnRushPhysicsParams`의 0.1 + 0.1초라 RagdollPawn 상수(0.4초)가 아니다.
- **규칙:** 원하는 손해(맞음 → 다시 달림)를 먼저 정하고 세는 시간을 거꾸로 맞춘다(금고실 서랍: `DrawerKnockdown` 1.8초 → 맞고 2.7초 뒤 달림).

## 101. 매 프레임 `Quaternion.Angle`을 더하면 프레임이 빠를수록 회전이 적게 잡힌다 (R725 측정 중 발견, 11-20 — R724 검사, 고치지 않음)
- **증상:** 왕실 천문대 Play 검사의 구르는 행성 줄("1 m에 26.0° ± 6 %")이 사본에서 23.6 / 23.5로 두 번 실패했다. R724에 저장된 보고서는 24.8로 통과했지만 이미 4.7 % 낮았다. R725는 그 파일들을 건드리지 않았다.
- **원인(추정):** Unity `Quaternion.Angle`은 두 회전의 내적이 1 − 1e-6보다 크면(약 0.16° 아래) 0을 돌려준다. 프레임 사이 회전이 작을수록 더 많이 버린다(`RoyalObservatoryValidation.cs:366`이 편집기 틱마다 더함).
- **규칙:** 쌓인 회전은 작은 걸음을 더하지 말고 시작 자세와 지금 자세 사이의 각이나 규칙 함수의 값으로 잰다.

## 102. 주변광을 '하늘 모드 ↔ 3색 모드'로 바꾸는 순간 빛이 튄다 — 섞을 땐 구면 조화(`ambientProbe`)로, 하늘 모드에서 써 넣는다 (R734, 11-21)
- **증상:** 사용자 "마차뜰에서 도개교로 넘어갈 때 후처리 때문에 조명이 확 바껴". 마차뜰 · 서재 · 천문대 조명은 무게가 0을 넘는 첫 프레임에 판 장면의 하늘 주변광(구면 조화)을 위 · 옆 · 아래 세 점만 재서 3색(Trilight) 모드로 바꾸고, 무게 0에서 하늘 모드로 되돌렸다. 세 점짜리 3색은 원래 하늘 빛과 같지 않아 들어오는 순간과 나가는 순간에 한 프레임에 빛이 바뀌었다. 게다가 섞는 거리가 8 ~ 14 m(대부분 10 m 복도 안)에 0.5초라 달리는 폰은 1초 남짓에 전부 지나갔다.
- **사실(Unity 6000.3, `ZoneLightCheck`가 빈 장면의 흰 구로 잼):** ① 3색 모드에서 색을 넣으면 Unity가 **바로** 그 구면 조화를 `RenderSettings.ambientProbe`에 만든다(읽어 오면 그려진 것과 같음) ② **하늘 모드에서 써 넣은 `ambientProbe`는 그대로 그려지고**, 두 값의 반반은 화면에서도 정확히 반반 ③ **3색 모드에서 써 넣은 `ambientProbe`는 무시된다**(색에서 다시 만듦) ④ 3색을 '하늘이 위에서 오는 빛'으로 직접 적분한 근사는 Unity 것과 0.2 넘게 다르다(Unity의 3색은 훨씬 진하다).
- **규칙:** 지역 조명을 섞을 때는 모드를 바꾸지 말고 구면 조화끼리 섞어 **하늘 모드**에서 `ambientProbe`로 써 넣는다. 목표 3색의 조화는 Unity에게 만들게 해서 읽어 온다(`ZoneLight.Gradient`: 잠깐 3색을 넣고 읽은 뒤 되돌림). 장면 빛을 '처음 들어올 때 적어 두고 0에서 되돌리는' 일은 곳마다 따로 하지 말고 하나(`ZoneLightMixer`)가 한다 — 두 곳이 동시에 걸리면 서로의 빛을 장면 빛으로 적어 잘못 되돌린다.

## 103. FBX 안 메시 오브젝트가 파일과 같은 이름이면, 이름으로 모델을 찾는 코드가 두 번 걸린다 (11-22, 동관 보이지 않는 벽)
- **증상:** 사용자 사진 + "동관 쪽에 여기 보이지 않는 벽에 막히는 문제". 보수 중인 동관(B) 입구 띠(체크무늬 바닥)의 가운데 통로 옆에서 폰이 막힘.
- **원인:** R701 소품 충돌 상자(`MiniGameModels.TableBoxes`)는 `B_StationEnv` · `B_HallEnv`라는 이름의 Transform을 찾아 그 '매단 틀'(180° 피벗의 부모)에 표의 상자를 깐다. B의 FBX는 메시 오브젝트도 파일과 같은 이름이라 모델 뿌리와 그 안 메시가 둘 다 걸렸고, 메시 쪽에서는 '틀'이 180° 돌아간 피벗이라 상자 표가 거울처럼 뒤집혀 한 번 더 깔렸다 — 소품이 없는 자리(입구 띠 x ±3.4 · z 1.8~4.4, 높이 1.3 m, 홀 쪽은 입구 밖 z −23)에 보이지 않는 상자. E · J는 메시 이름이 달라 괜찮았다.
- **규칙:** 이름으로 모델 뿌리를 찾을 때는 부모가 같은 이름이면 건너뛴다(`TableBoxes`). `MiniGamePrefabValidation.CheckPropBoxes`가 `PROP_COL <모델>` 그룹이 그 모델 바로 옆(모델의 돌아간 틀)에 있으면 실패로 잡는다. 확인 방법: 사본 Unity에서 방을 짓고 '그림 없는 단단한 충돌체'를 꼭짓점 8개로 재어 목록 · 빨간 상자로 보기(두 꼭짓점만 바꾸면 상자 크기가 틀리게 나온다).

## 104. Blender 5.2 Cycles에서 볼륨 상자가 겹치면 겹친 곳의 구름이 네모나게 잘린다 (R793, 11-23, 인트로 구름 층)
- **증상:** 구름 층을 정사영으로 렌더하니 구름 여러 개가 곧은 세로 · 가로 선으로 잘려 네모난 홈이 났다(상자 가장자리에서 밀도를 0으로 줄여도 그대로).
- **원인:** 구름마다 볼륨 상자(재질 따로)를 두었는데 상자가 서로 겹쳤다. 겹친 곳에서 두 볼륨이 더해지지 않고 한쪽이 지워졌다(Blender 5.2.3에서 본 것, 원인 코드는 확인 안 함).
- **규칙:** 볼륨 상자끼리 겹치지 않게 둔다. 정사영 렌더면 깊이(카메라 방향)로 떼어 놓으면 화면은 그대로다(`render_sky.py`: 구름마다 y를 40씩, 상자 깊이 32).

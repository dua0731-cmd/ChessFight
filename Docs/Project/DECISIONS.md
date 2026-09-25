# 확정된 결정

이미 정해진 것과 그 이유다. **바꾸기 전에 이유를 읽고, 바꾸면 여기와 [HANDOFF](../../HANDOFF.md)를 고친다.**
새 결정은 번호를 이어서 추가한다. 기획 결정 대기 목록(D1–D6 등)은 [ROADMAP](ROADMAP.md)에 있다.

| # | 결정 | 이유 | 바꾸면 생기는 일 |
|---|---|---|---|
| K1 | 네트워크는 **Steamworks.NET + Steam 로비 + Steam Networking Messages**. 전용 서버, Photon, Mirror, NGO, UGS는 쓰지 않는다 | 5명 팀, 서버 운영비 거의 없음, 출시도 Steam | 매칭·권한 모델 전체를 다시 만들어야 한다 |
| K2 | **호스트 권위**: 경기 방을 만든 PC가 이동을 판정한다. 클라이언트는 입력만 보낸다 | 서버 없이 부정 좌표를 막는 가장 싼 방법 | 클라이언트 좌표를 믿으면 순간이동 치트가 된다 |
| K3 | 파티(최대 6)와 경기 방(12)은 **다른 로비**다. 파티는 **같은 팀에 통째로** 예약한다 | 친구와 같은 팀 보장, 경기 후에도 파티 유지 | 파티가 양 팀으로 갈라질 수 있다 |
| K4 | Steam 로비 소유권 이전은 **게임 호스트 이전이 아니다.** 호스트가 나가면 경기가 끝난다 | 호스트 이전은 상태 복제가 필요한 큰 작업 | 새 호스트가 상태 없이 판정을 시작해 어긋난다 |
| K5 | 모든 로비에 `protocol`과 `build`를 기록하고 **같은 값끼리만** 만난다. 개발 빌드는 `-dev`가 붙어 릴리스와 분리된다 | 다른 버전끼리 섞이면 원인 모를 동기화 오류 | 옛 빌드 테스터가 조용히 어긋난다 |
| K6 | 봇은 **Steam 계정 없는 명단 항목**이고 ID는 `BotIdentity`만 만든다. 호스트만 시뮬레이션하고 패킷을 주고받지 않는다 | 12명 없이 12인 테스트 | 봇에게 전송하면 Steam 오류, ID 충돌 |
| K7 | **공개 매치에는 사람만**(M6). 단 Editor·개발 빌드는 봇 허용 | 출시 규칙은 사람만, 개발 중에는 인원이 없다 | 개발 빌드에서도 막으면 12인 테스트를 못 한다 |
| U1 | UI는 **UI Toolkit(UXML/USS) + 런타임 PanelSettings**. uGUI는 없다 | 사용자 요청(R2), 레이아웃을 코드 밖으로 | uGUI를 추가하면 규칙 U2를 다시 검토해야 한다 |
| U2 | **Active Input Handling = Input Manager (Old)**. Input System 1.20.0 패키지는 설치·고정하지만 백엔드는 끈다. `InputSettingsGuard`가 강제한다 | uGUI 없이 백엔드를 켜면 UI Toolkit 런타임 패널이 클릭·키 이벤트를 못 받는다(R8에서 실제 발생) | HUD 전체가 안 눌린다. 켜려면 uGUI 설치 → EventSystem + InputSystemUIInputModule → 설정 변경 순서 |
| U3 | HUD 테마(`NetworkTheme.tss`)는 **프로젝트 USS만** 가져온다. 기본 테마 없음 → 복합 컨트롤 금지 | 손으로 쓴 TSS가 `@import url("NetworkHud.uss")` 한 줄뿐이다. Unity 기본 테마(`unity-theme://default`)를 넣는 것은 아직 Unity에서 시험하지 않았다 | 기본 테마 없이 복합 컨트롤을 쓰면 크기 0으로 사라진다(R6). 기본 테마를 추가하면 기존 HUD 모양이 바뀔 수 있다 |
| U4 | 한글 폰트는 `Font.CreateDynamicFontFromOSFont`(맑은 고딕 우선) | 기본 LegacyRuntime 폰트에 한글이 없다. 폰트 파일 없이 해결 | 폰트 에셋을 넣는다면 라이선스와 용량 확인 |
| U5 | ~~시작 버튼은 **단계형**: 게임 시작 → 파티 생성/빠른 매칭 → 친구 초대/게임 시작~~ → **U7로 대체**(2026-09-25) | 사용자 요청(R7), 폴가이즈 참고 | — |
| U7 | **로비는 참고 영상(R19)대로**: 파티가 3D로 줄지어 서는 메뉴(이동 없음), 우측 하단 **모드 카드 + 게임 시작**. 게임 시작은 **파티 전체로 곧바로 매칭**한다(단계 없음). 매칭 중에는 상단 패널(6칸 VS 6칸)과 매칭 중 카드 | 사용자가 준 영상. 파티는 항상 있으므로 "파티 생성" 단계가 필요 없고, 모드 카드가 무엇을 시작할지 보여 준다 | 되돌리려면 `LobbyBootstrap.WireHud`의 `Play`와 UXML 우측 하단을 바꾼다 |
| U6 | 친구 초대는 **게임 내 패널**, 검색창 대신 페이지 이동, 번호 칸에 붙여넣기 버튼 | 오버레이 해상도 문제(R10). 대체 클릭 경로에서는 키 입력이 안 들어온다 | 타이핑이 필요한 UI는 대체 경로에서 죽는다 |
| S1 | **폴더 하나 = 어셈블리 하나**, 최소 폴더(`Scripts · Scenes · Prefabs · Resources · Materials · Art`) | 사용자 요청(R3), 의존 방향이 폴더로 보인다 | — |
| S2 | `Core`는 Unity·Steam 무의존(`noEngineReferences`). `Game`·`Gameplay`는 Steam 무의존 | Unity 없이 테스트, Steam 패키지 없이도 프리팹 스크립트가 컴파일 | 프리팹에 missing script, Safe Mode |
| S3 | 선택 패키지 코드는 **전용 asmdef + versionDefines + defineConstraints**로 가둔다. `ENABLE_INPUT_SYSTEM`을 설치 여부로 쓰지 않는다 | Safe Mode에서는 설치 스크립트가 못 돈다(R4, R5) | 패키지 없는 PC가 Safe Mode에 갇힌다 |
| S4 | UPM 의존성은 manifest/lock에 **고정**하고 결과를 커밋한다. Steamworks.NET = git 커밋 `c21a8f0e…`, Input System = 1.20.0 | 설치 결과가 PC마다 달라 변경점이 계속 생겼다(R6) | 변경점 무한 반복 |
| S5 | URP 에셋은 있지만 URP 패키지는 없다. `RenderPipelineOverride`가 Play 동안 Built-in으로 그린다 | 템플릿 잔재. 렌더 파이프라인 결정은 아직 안 함 | URP를 들이면 재질·셰이더·이 우회를 함께 정리 |
| C1 | 씬 흐름 **Intro → Lobby → KingRush → Lobby**, 세션 소유자는 `DontDestroyOnLoad`의 `NetworkRuntime` 하나 | 씬을 바꿔도 파티·경기가 끊기지 않아야 한다(R11) | 씬 전환 때 Steam 종료 |
| C2 | 씬 컨트롤러는 **씬에 저장하지 않고** `NetworkRuntime`이 `sceneLoaded` 때 붙인다. 씬 파일은 Steam 어셈블리를 참조하지 않는다 | Steam 패키지 없는 PC에서도 씬이 깨지지 않는다 | missing script |
| C3 | KingRush·RagdollTest를 **직접 Play하면 오프라인 플레이테스트**(Steam 없이) | 맵·래그돌 작업자가 매칭 없이 바로 시험 | — |
| G1 | 캐릭터 계약은 `ICharacterDriver` + `CharacterCommand`(월드 좌표 Move, Jump, Shove, Grab). 래그돌 랩의 `PawnInput`과 같은 모양 | 래그돌 어댑터가 필드 복사로 끝남, 호스트·봇·오프라인 공통 | — |
| G2 | 장애물 포즈는 **`ObstacleClock` 시간의 순수 함수**. 경기 중 시계는 Steam 서버 시간 + 로컬 소수부 | 장애물 패킷 없이 모든 PC가 같은 장면 | PC마다 장애물 위치가 달라진다 |
| G3 | 래그돌 씬 물리 **120Hz / 솔버 24회**를 `PhysicsProfile`이 씬 단위로 적용. 프로젝트 기본(50Hz)은 바꾸지 않는다 | 래그돌 랩 측정: 60Hz 이하에서 골반이 주저앉음 | 로비 등 다른 씬까지 비용 증가 |
| G4 | `Teleport(position)`의 position은 **발 닿는 바닥 지점**. 스폰 지점도 y=0 | 캐릭터마다 키가 다르다 | 래그돌이 공중에서 떨어지거나 파묻힌다 |
| G5 | **RagdollTest 씬 = 래그돌 랩 씬.** 래그돌 코드는 `Assets/ChessFight/RagdollLab/`의 `ChessFight.RagdollLab` 어셈블리(참조: Gameplay만)에 두고, 게임·네트워크 쪽은 `ICharacterDriver`(`RagdollDriver`)로만 부른다 | 사용자 요구(R16): 랩을 그대로 옮기되 네트워크·멀티에 문제가 없게. 빌더가 경로를 안다 | 네트워크 코드가 래그돌에 묶이거나, 래그돌이 Steam에 묶인다. 경계 검사가 실패한다 |
| G6 | **게임 모드 하나 = 미니게임 하나.** 모드 목록은 `Core/GameModes.cs` 한 곳. 파티장이 고르고 **모드마다 따로 매칭**(로비 데이터 `mode` + 검색 필터). 씬이 없는 모드는 로비에서 "준비 중"으로 잠근다 | 사용자 결정(R19). 모드가 섞이면 다른 규칙의 사람끼리 만난다 | 모드 키를 바꾸면 옛 빌드와 다른 모드로 읽힌다. 필터를 빼면 모드가 섞인다(테스트가 잡는다) |
| T1 | 팀원은 `main`, AI는 `Network` 브랜치에서 작업하고 병합은 사용자 결정. **로비·게임모드 작업은 `JY-lobby`에만 커밋·푸시**(2026-09-25 사용자 지시. `main`·`Network`·`JY-ragdoll`에는 푸시하지 않는다. `JY-ragdoll`은 래그돌 물리 튜닝용) | 팀원 작업을 AI 변경이 덮지 않게 | — |
| T2 | 같은 `.unity`를 동시에 편집하지 않는다. 맵은 구간 프리팹. Unity YAML 병합 도구와 Git LFS 사용 | 씬 병합은 사실상 불가능 | 작업 유실 |
| T3 | 손으로 쓰는 Unity YAML은 `Tools/Generators`로 만들고 GUID는 경로의 md5로 고정한다 | AI가 Unity 없이 자산을 만들 때 재현 가능 | GUID 충돌, 참조 끊김 |

# ChessFight — Steam 파티·자동 매칭 테스트

대상: `Network` 브랜치 / **Unity 6000.3.11f1 / Windows x64**.
이번 구현은 온라인 이동 테스트용 첫 단계다. 킹러시 규칙, 캐릭터 스킬, 래그돌 전투는 포함하지 않는다.

다른 AI나 개발자가 이어받을 때는 **[상세 인수인계 문서](../AI/NETWORK_HANDOFF_KO.md)**를 먼저 읽는다. 파티 예약, 매칭, 패킷 포맷, 예측·보정, 실제 확인 결과와 후속 검증을 설명한다.

## 처음 실행

1. 작업 중인 씬을 저장하고 `Network` 브랜치를 Pull한다. 프로젝트 버전과 같은 Unity를 사용한다.
2. Unity가 manifest/lock의 **Steamworks.NET 2025.164.1**을 Package Manager로 복원한다. Git 커밋 `c21a8f0e31c56ae8707130967faf491f7dd7c0d8`에 고정했다. `NetworkSetup`에는 패키지가 없을 때의 설치 도구도 남아 있다.
3. 패키지 설치/컴파일이 끝날 때까지 기다린다. 설치가 실패하면 Git for Windows 설치와 인터넷 연결을 확인하고 Unity Hub를 완전히 재시작한다. `ChessFight > Network > Install Steamworks dependency`로 재시도한다.
4. `c9c1e6a`에 실제 설치 결과인 **`Packages/manifest.json`, `Packages/packages-lock.json`이 함께 커밋되어 있다.** 이후 의존성을 변경할 때도 두 파일을 함께 커밋한다. 설치 도구는 이미 설치된 패키지를 반복 추가하지 않는다.
5. Steam 클라이언트를 실행하고 로그인한다. 프로젝트 루트의 `steam_appid.txt`는 개발용 App ID **480**이다.
6. `ChessFight > Network > Open test scene`을 실행하고 **Play**를 누른다. `Assets/ChessFight/Game/Scenes/ChessFightLab.unity`가 열린다.

### 2026-09-22 구조 변경 — Pull 후 반드시 읽는다

런타임에 코드로 만들던 것을 **자산**으로 바꿨다. 보이는 화면은 그대로다.

- **입력이 Input System으로 바뀌었다.** `ChessFight > Setup > Install dependencies`가 Steamworks와 `com.unity.inputsystem`을 함께 설치한다(에디터 시작 시 자동 1회 시도). **설치 후 생성된 `Packages/manifest.json`과 `packages-lock.json`을 커밋한다.** 패키지가 없으면 Legacy 입력으로 자동 대체되므로 컴파일은 깨지지 않는다.
- `ProjectSettings`의 Active Input Handling을 **Both**로 바꿨다. 에디터가 켜진 채 Pull 했다면 재시작을 요구할 수 있다.
- 씬이 `ChessFightLab`으로 바뀌었다. 기존 `SampleScene`에서도 계속 동작한다.
- 편집 모드의 `ChessFightLab`에는 카메라와 `ChessFight Game Root`만 있다. **체스판·캡슐·HUD는 프리팹/UXML 자산이며 Play 중에 생성(Instantiate)된다.** 누가 경기에 들어올지는 실행 전에 알 수 없으므로 캐릭터 생성 자체는 런타임이 맞다.
- 고칠 곳: 캐릭터 모양은 `PawnAvatar.prefab`, 맵은 `Arena.prefab`, 색은 `*.mat`, UI는 `NetworkHud.uxml`/`.uss`, 키 배치는 `ChessFightControls.inputactions`. 모두 `Assets/ChessFight/Game/` 아래에 있다.

## 가장 빠른 2인 테스트

**서로 다른 Steam 계정으로 로그인한 두 PC**를 사용한다. 동일 계정의 Editor/빌드 두 개를 실제 Steam 멀티플레이 검증으로 사용하지 않는다.

1. PC A: `Create private test` → `Copy match ID`로 방 번호 복사.
2. PC B: 그 번호를 `Lobby ID`에 붙여넣고 `Join match ID`.
3. 두 플레이어가 입장 승인되면 파랑/주황 캡슐이 생성된다. **WASD 이동, Space 점프**. 번호 입력칸 밖을 클릭해야 이동 입력이 작동한다.
4. A에서 움직일 때 B에도 같은 이동이 보이는지, 반대 방향도 확인한다.
5. `Start match`는 비공개 방 2명 이상, 공개 방 12명일 때만 활성화된다. 누르면 세션을 시작하고 추가 입장을 닫는다. 시작 전 대기 공간에서도 움직일 수 있다.
6. B에서 `Cancel / leave match` → A에서 B의 캡슐이 사라지는지 확인한다.

각 PC의 독립 파티가 같은 테스트 방에 들어오는 방식이다. 두 사람이 같은 팀이어야 한다면 먼저 아래의 **파티 ID**로 함께 파티를 만든 뒤 방을 생성한다.

## AI 봇으로 인원 채우기

12명을 모으기 어려울 때 쓴다. 봇은 **Steam 계정이 없는 명단 항목**이다. 로비에 접속하지 않고 예약 정원만 차지하며, 경기 호스트의 PC가 이동을 계산한다. 봇에게는 패킷을 보내지도 받지도 않는다.

**파티 봇 (`- / +` 버튼)**

- 파티장만, 매칭 시작 전에만 조절할 수 있다. 상한은 `6 - 현재 파티원 수`다.
- 봇은 **파티와 함께 같은 팀에 예약된다.** 사람이 늘면 자동으로 줄어든다.
- 봇 ID에는 파티장의 Steam ID가 들어 있어 다른 파티의 봇과 절대 겹치지 않는다. 남의 파티 봇을 사칭해 자리를 잡을 수도 없다.
- **PC 2대로 12인 테스트:** 양쪽이 각각 1인 파티 + 봇 5로 `Quick match` → 6 대 6이 채워져 자동 시작한다.

**방 채우기 (`Fill room to 12`)**

- 경기 호스트만, 시작 전에만 쓸 수 있다. 빈 자리를 봇으로 한 번에 채운다.
- **PC 1대로 12인 테스트:** `Create private test` → 파티 봇 5 → `Fill room to 12` → 12인. 호스트 CPU와 대역폭을 혼자 측정할 수 있다.
- `Remove room bots`로 되돌린다. 공개 방에서 쓰면 남은 자리가 사라져 실제 플레이어가 못 들어온다.

봇은 정해진 지점으로 걸어가고 가끔 점프하는 수준이다. **게임 AI가 아니라 인원·부하 채우기용이다.** 킹러시 규칙이나 기물 스킬은 들어 있지 않다.

## 친구 파티 + 자동 매칭

- 실행 시 각자 1인 비공개 파티가 만들어진다. 파티 최대 6명.
- `Invite Steam friends`: Steam 오버레이 초대. Editor에서 오버레이가 열리지 않으면 `Copy party ID` → 친구의 `Join party ID`를 사용한다.
- **Party ID**는 친구 그룹, **Match ID**는 12인 경기 방이다. 서로 다른 버튼으로 입장한다.
- 파티장만 `Quick match (6 vs 6)`를 누른다. 파티원은 파티장의 매칭 결과를 자동으로 따라간다.
- 파티 전체를 같은 팀에 예약한다. 예: 4+4명으로 채워진 방에 또 다른 4인 파티는 들어갈 수 없다. 팀을 나누지 않고 다른 방을 찾는다.
- 기존 공개 방 검색 → 팀 전체 자리 확보 → 없으면 방 생성. 자기 파티만 있는 방은 주기적으로 더 먼저 만들어진 방을 찾아 합류한다.
- 파티원이 모두 입장하기 전까지 예약을 유지한다. 25초 내 실패하면 해당 파티 전체 예약을 해제한다.
- 6명씩 두 팀, 총 12명이 입장 완료되면 자동 시작한다. 별도 유저가 없으면 대기하며, 봇이나 임의 인원 축소로 시작하지 않는다.
- 파티원 누구나 취소할 수 있다. 파티원 변경/부분 입장 실패도 파티 전체 매칭을 취소한다. 대기/경기 중에는 새 파티원 초대를 잠근다.
- 방장이 나가면 경기는 종료되고 남은 플레이어는 자신의 파티로 돌아간다. Steam의 로비 소유권 이전을 게임 방장 이전으로 취급하지 않는다.

## 구조와 확장 지점

네트워크(`Assets/ChessFight/Network/`)와 표현(`Assets/ChessFight/Game/`)을 분리했다. Game은 Steam을 모르고, Network는 Unity 화면을 모른다.

| 파일 | 책임 |
|---|---|
| `Network/Core/TeamReservations.cs` | 호스트 전용 6v6 정원, 파티 단위 예약·만료·입장 완료 |
| `Network/Core/MotionProtocol.cs` | 버전/세션이 포함된 바이너리 입력·스냅샷, 크기/수치/순서 검증, 평면 이동 모델 |
| `Network/Core/BotIdentity.cs` | Steam ID와 겹칠 수 없는 봇 ID, 파티장 소유권 |
| `Network/Core/BotBrain.cs` | 호스트 전용 봇 로밍. 순수 C#이라 Unity 없이 테스트된다 |
| `Network/Steam/SteamSession.cs` | 비공개 파티, 공개 경기 검색, 초대, 파티 추적, 봇 슬롯, 취소, 호스트 이탈 처리 |
| `Network/Steam/SteamMotion.cs` | Steam Networking Messages, 호스트 이동 처리(봇 포함), 클라이언트 예측·보정 |
| `Game/Steam/GameBootstrap.cs` | 조립 지점. 세션을 만들고 나머지에 데이터를 넘긴다 |
| `Game/Runtime/GameSceneConfig.cs` | Inspector에서 프리팹·재질·UI를 지정하는 유일한 곳 |
| `Game/Runtime/PawnAvatar.cs` | 캐릭터 프리팹의 표시 전용 보간. 위치를 결정하지 않는다 |
| `Game/Runtime/PawnSpawner.cs` | 명단에 맞춰 프리팹을 생성·파괴 |
| `Game/Runtime/CameraRig.cs` | 추적 카메라 |
| `Game/Runtime/NetworkHudView.cs` | UXML 바인딩. Steam을 참조하지 않는다 |
| `Game/Runtime/MoveInput.cs` | 입력 추상화 + Legacy 대체 구현 |
| `Input/InputSystemMoveSource.cs` | Input System 구현. `ENABLE_INPUT_SYSTEM`으로 감싼다 |
| `Game/Resources/ChessFight/` | `PawnAvatar.prefab`, `Arena.prefab`, `*.mat`, `NetworkHud.*`, `ChessFightControls.inputactions` |
| `Game/Scenes/ChessFightLab.unity` | 테스트 씬 |
| `Editor/NetworkSetup.cs` | 패키지 설치·씬 열기·Windows 테스트 빌드 메뉴 |

어셈블리는 넷이다. `ChessFight.Network.Core`(Unity 무관) → `ChessFight.Network.Steam`(Steam 게이트) / `ChessFight.Game`(Steam 무관, 프리팹이 참조) → `ChessFight.Game.Steam`(Steam 게이트). **프리팹이 참조하는 스크립트는 Steamworks 없이도 컴파일된다.** 그래서 패키지 설치 전에 프로젝트를 열어도 프리팹이 깨지지 않는다.

- 관리형 API 래퍼는 Steamworks.NET, 실제 통신은 Steam Networking Messages다. 별도 유료 매칭 서버나 Unity Gaming Services는 사용하지 않는다.
- 이동 시뮬레이션은 호스트에서 **30Hz**, 스냅샷은 최대 **20Hz**. 클라이언트는 위치를 명령하지 않고 입력을 보낸다. 대각선 속도 제한, 입력 타임아웃, 이전 패킷 무시, 점프/착지, 맵 경계를 처리한다.
- 실시간 입력/스냅샷은 비신뢰 메시지로 최신 상태를 전달한다. 파티 예약은 Steam 로비 메시지/메타데이터로 처리한다.
- 패킷 송신자의 Steam 신원과 현재 경기 명단을 확인한다. 패킷 내부의 임의 ID를 입력 소유자로 믿지 않는다.
- 검색에 전용 `protocol` 값을 사용해 App ID 480의 다른 게임 로비를 제외한다. 네트워크 호환성이 깨지는 변경 시 `SteamSession.Protocol`을 올린다.
- `SampleScene`/`NetworkSandbox`라는 씬에서만 부트스트랩이 켜진다. 다른 게임 씬에는 자동 생성하지 않는다. 나중에 정식 시작 씬으로 옮길 때 이 조건을 명시적으로 변경한다.
- 현재 브랜치에는 URP 패키지가 없지만 템플릿 URP 참조가 남아 있다. 테스트 공간은 실행 중에만 Built-in 렌더링을 선택하고 종료 시 되돌린다. 기존 씬/렌더 파이프라인 자산은 재저장하지 않았다.

## 빌드와 검증

`ChessFight > Network > Build Windows development test` → `Builds/NetworkTest/ChessFight.exe`. `ChessFightLab` 씬만 포함한다.
개발 빌드 옆에 `steam_appid.txt`도 복사한다. Steam 정식 배포 시에는 자체 App ID와 배포 설정을 사용하고 개발용 480 파일을 배포하지 않는다.

자동 테스트(추가 테스트 패키지 불필요):

```powershell
./Tools/Test-NetworkCore.ps1
```

실제 Unity 라이브러리와 설치된 Steamworks 소스로 어셈블리 컴파일:

```powershell
./Tools/Test-NetworkCompile.ps1 -SteamRuntimeSources 'Library/PackageCache/<Steamworks 패키지 폴더>/Runtime'
```

Unity 경로가 다르면 두 스크립트에 `-UnityEditor '<설치된 Unity의 Editor 폴더>'`를 지정한다. 결과와 한계는 [VALIDATION.md](VALIDATION.md)에 기록했다.

## 현재 범위의 한계

- **인터넷 지연·패킷 손실과 12인 부하는 아직 검증이 필요하다.** 2026-09-22 두 PC·두 계정의 파티 입장과 공개 매칭 성사는 확인했다. 양방향 이동 동기화는 아직 보고되지 않았다. 2026-09-21 Editor의 Steam 1인 파티·비공개 방·캡슐 표시 및 Windows 빌드는 성공했다. 2026-09-22 독립 실행 빌드에서도 Steam 로그인 후 1인 파티·비공개 방·캡슐 생성을 확인했다. 자세한 범위는 [검증 기록](VALIDATION.md)을 따른다. 현재 결과는 출시 완료 판정이 아니다.
- 이동 테스트용 캡슐이다. 기물 선택·6종 스킬·팀 전투·라운드 진행·래그돌·플레이어 간 충돌은 이후 작업이다. 현재 `Start`는 입장 마감 상태만 전환한다.
- **2026-09-22 구조 개편과 AI 봇은 아직 Unity에서 한 번도 실행하지 않았다.** 프리팹·씬·재질은 손으로 작성한 Unity YAML이다. 먼저 에디터에서 열어 화면이 이전과 같은지 확인하고 결과를 [VALIDATION.md](VALIDATION.md)에 남긴다.
- 봇은 호스트가 전부 시뮬레이션한다. 호스트가 약하면 12인 봇 방의 프레임이 떨어질 수 있고, 그 부하는 아직 측정하지 않았다.
- MMR/랭크, 매칭 품질 보장, 재접속, 호스트 이전, 악성 호스트 방어, 서버 기반 안티치트는 없다. 파티 구성 정보는 신뢰할 수 있는 팀 테스트 환경을 기준으로 하며 독립 서버가 친구 그룹 소속을 검증하지 않는다.
- 다른 파티까지 들어온 대기방끼리 전체 인원을 재배치하는 통합 매칭기는 아니다. 팀 정원이 맞지 않거나 인원이 여러 방에 분산되면 기다리거나 취소 후 다시 검색해야 할 수 있다.
- 검색 범위는 Steam 기본 근거리 필터다. 서로 멀리 떨어진 국가에서 테스트할 때는 Match ID로 직접 연결해 확인한다.
- 이 단계에서는 JSON 로비 상태와 테스트 UI를 단순하게 유지했다. 정식 서비스 전에는 로비 변경 빈도, 장시간 대기, 강제 종료, NAT 환경과 12인 부하를 실제 계정으로 검증해야 한다.

참고: [Steam 로비](https://partner.steamgames.com/doc/features/multiplayer/matchmaking), [Steam Networking Messages](https://partner.steamgames.com/doc/api/ISteamNetworkingMessages), [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET/tree/2025.164.1).

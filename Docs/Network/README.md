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
6. `ChessFight > Network > Open test scene`을 실행하고 **Play**를 누른다. 기존 `SampleScene`을 그대로 열어도 된다. 런타임에 체스판과 UI가 생성된다.

초기 구현 `87805f0`은 설치 부트스트랩만 포함했지만 후속 `c9c1e6a`에서 설치 결과까지 반영했다. 원본 프로젝트가 최신 Network를 Pull했는지 확인한다. 편집 모드의 SampleScene은 비어 있고 체스판·UI·캐릭터는 Play 중 생성된다.

## 가장 빠른 2인 테스트

**서로 다른 Steam 계정으로 로그인한 두 PC**를 사용한다. 동일 계정의 Editor/빌드 두 개를 실제 Steam 멀티플레이 검증으로 사용하지 않는다.

1. PC A: `Create private test` → `Copy match ID`로 방 번호 복사.
2. PC B: 그 번호를 `Lobby ID`에 붙여넣고 `Join match ID`.
3. 두 플레이어가 입장 승인되면 파랑/주황 캡슐이 생성된다. **WASD 이동, Space 점프**. 번호 입력칸 밖을 클릭해야 이동 입력이 작동한다.
4. A에서 움직일 때 B에도 같은 이동이 보이는지, 반대 방향도 확인한다.
5. `Start private test (2+)`는 2명 이상 입장이 완료되면 세션을 시작하고 추가 입장을 닫는다. 시작 전 대기 공간에서도 움직일 수 있다.
6. B에서 `Cancel / leave match` → A에서 B의 캡슐이 사라지는지 확인한다.

각 PC의 독립 파티가 같은 테스트 방에 들어오는 방식이다. 두 사람이 같은 팀이어야 한다면 먼저 아래의 **파티 ID**로 함께 파티를 만든 뒤 방을 생성한다.

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

| 파일 | 책임 |
|---|---|
| `Core/TeamReservations.cs` | 호스트 전용 6v6 정원, 파티 단위 예약·만료·입장 완료 |
| `Core/MotionProtocol.cs` | 버전/세션이 포함된 바이너리 입력·스냅샷, 크기/수치/순서 검증, 평면 이동 모델 |
| `Steam/SteamSession.cs` | 비공개 파티, 공개 경기 검색, 초대, 파티 추적, 취소, 호스트 이탈 처리 |
| `Steam/SteamMotion.cs` | Steam Networking Messages, 호스트 이동 처리, 클라이언트 예측·보정 |
| `Runtime/NetworkSandbox.cs` | 테스트 맵·캡슐 생성, 시각적 보간, 카메라, UI 연결 |
| `Resources/NetworkHud.*`, `NetworkTheme.tss` | UI Toolkit 테스트 UI |
| `Editor/NetworkSetup.cs` | 패키지 설치·씬 열기·Windows 테스트 빌드 메뉴 |

- 관리형 API 래퍼는 Steamworks.NET, 실제 통신은 Steam Networking Messages다. 별도 유료 매칭 서버나 Unity Gaming Services는 사용하지 않는다.
- 이동 시뮬레이션은 호스트에서 **30Hz**, 스냅샷은 최대 **20Hz**. 클라이언트는 위치를 명령하지 않고 입력을 보낸다. 대각선 속도 제한, 입력 타임아웃, 이전 패킷 무시, 점프/착지, 맵 경계를 처리한다.
- 실시간 입력/스냅샷은 비신뢰 메시지로 최신 상태를 전달한다. 파티 예약은 Steam 로비 메시지/메타데이터로 처리한다.
- 패킷 송신자의 Steam 신원과 현재 경기 명단을 확인한다. 패킷 내부의 임의 ID를 입력 소유자로 믿지 않는다.
- 검색에 전용 `protocol` 값을 사용해 App ID 480의 다른 게임 로비를 제외한다. 네트워크 호환성이 깨지는 변경 시 `SteamSession.Protocol`을 올린다.
- `SampleScene`/`NetworkSandbox`라는 씬에서만 부트스트랩이 켜진다. 다른 게임 씬에는 자동 생성하지 않는다. 나중에 정식 시작 씬으로 옮길 때 이 조건을 명시적으로 변경한다.
- 현재 브랜치에는 URP 패키지가 없지만 템플릿 URP 참조가 남아 있다. 테스트 공간은 실행 중에만 Built-in 렌더링을 선택하고 종료 시 되돌린다. 기존 씬/렌더 파이프라인 자산은 재저장하지 않았다.

## 빌드와 검증

`ChessFight > Network > Build Windows development test` → `Builds/NetworkTest/ChessFight.exe`.
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

- **실제 Steam 2계정 접속, 인터넷 지연·패킷 손실과 12인 부하는 아직 검증이 필요하다.** 2026-09-21 Editor의 Steam 1인 파티·비공개 방·캡슐 표시 및 Windows 빌드는 성공했다. 2026-09-22 독립 실행 빌드에서도 Steam 로그인 후 1인 파티·비공개 방·캡슐 생성을 확인했다. 자세한 범위는 [검증 기록](VALIDATION.md)을 따른다. 현재 결과는 출시 완료 판정이 아니다.
- 이동 테스트용 캡슐이다. 기물 선택·6종 스킬·팀 전투·라운드 진행·래그돌·플레이어 간 충돌은 이후 작업이다. 현재 `Start`는 입장 마감 상태만 전환한다.
- MMR/랭크, 매칭 품질 보장, 재접속, 호스트 이전, 악성 호스트 방어, 서버 기반 안티치트는 없다. 파티 구성 정보는 신뢰할 수 있는 팀 테스트 환경을 기준으로 하며 독립 서버가 친구 그룹 소속을 검증하지 않는다.
- 다른 파티까지 들어온 대기방끼리 전체 인원을 재배치하는 통합 매칭기는 아니다. 팀 정원이 맞지 않거나 인원이 여러 방에 분산되면 기다리거나 취소 후 다시 검색해야 할 수 있다.
- 검색 범위는 Steam 기본 근거리 필터다. 서로 멀리 떨어진 국가에서 테스트할 때는 Match ID로 직접 연결해 확인한다.
- 이 단계에서는 JSON 로비 상태와 테스트 UI를 단순하게 유지했다. 정식 서비스 전에는 로비 변경 빈도, 장시간 대기, 강제 종료, NAT 환경과 12인 부하를 실제 계정으로 검증해야 한다.

참고: [Steam 로비](https://partner.steamgames.com/doc/features/multiplayer/matchmaking), [Steam Networking Messages](https://partner.steamgames.com/doc/api/ISteamNetworkingMessages), [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET/tree/2025.164.1).

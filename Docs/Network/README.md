# 네트워크 — 개요와 테스트 방법

전체 상황은 [HANDOFF](../../HANDOFF.md). 이 폴더는 멀티플레이 문서만 모은다.

| 문서 | 내용 |
|---|---|
| **이 문서** | 한눈에 보는 구조, 사람이 하는 테스트 방법 |
| [SESSION.md](SESSION.md) | 파티, 자동 매칭, 파티 단위 예약, 로비 데이터, 취소·이탈, Rich Presence, 버전 검사 |
| [MOTION.md](MOTION.md) | 이동 동기화, 패킷 형식, 끊김 단계, 핑, F8 지연 시뮬레이터 |
| [BOTS.md](BOTS.md) | AI 봇, 공개 매치 규칙 |
| [PLAN_V0.1_STATUS.md](PLAN_V0.1_STATUS.md) | 승규 기획안 v0.1 항목별 상태, 승규 다음 작업 |
| [VALIDATION.md](VALIDATION.md) | 실제 확인 기록과 Unity 확인 목록 |

## 1. 한눈에

- **서버 없음.** Steam 로비로 파티(최대 6)와 경기 방(12)을 만들고, 경기 방을 만든 PC가 **호스트**로 모든 이동을 판정한다. 나머지는 입력만 보낸다.
- 파티는 **같은 팀에 통째로** 들어간다. 6 대 6이 모두 입장하면 공개 경기가 자동 시작한다.
- 호스트가 나가면 경기가 끝나고 모두 자기 파티로 돌아간다(호스트 이전 없음).
- 프로토콜 `chessfight.dua0731.network.v2` + 빌드 값이 같은 사람끼리만 만난다.
- Steamworks.NET 2025.164.1, Steam Networking Messages 채널 31. UGS, Photon, Mirror, NGO는 쓰지 않는다.

## 2. 가장 빠른 2인 테스트

**서로 다른 Steam 계정의 두 PC**, **같은 커밋**이어야 한다.

1. 두 PC 모두 Steam 로그인 → `ChessFight > Scenes > Intro` 또는 `Lobby` → Play.
2. PC A: 좌측 하단 `비공개 방` → 상단 매칭 패널의 방 번호 `복사`.
3. PC B: 좌측 하단 `# 코드로 참가` → `붙여넣기` → `비공개 방 참가`.
4. 두 화면 상단 패널의 칸이 파랑 1 VS 주황 1로 찬다. (2026-09-25부터 **로비에서는 캐릭터가 움직이지 않는다.**)
5. A: 우측 하단 `경기 시작` → 둘 다 경기 씬(킹 러시)으로 넘어간다.
6. 경기 씬에서 **WASD 이동, Space 점프.** A가 움직일 때 B 화면에서도 움직이는지, 반대도 확인한다. ← **아직 보고된 적 없는 가장 중요한 확인**
7. Esc → 로비 복귀, 파티 유지.
8. 연결 품질: 연결 정보 카드(상단 `정보`)·경기 화면의 `핑 / 응답`, B에서 F8을 눌러 지연을 켜고 점프가 빠지지 않는지, A 창을 일시정지해 B에 노랑 → 빨강 배너가 뜨는지.

같은 팀으로 시험하려면 먼저 파티 바의 파티 코드 `복사` → 상대 `# 코드로 참가` → `파티 참가`(또는 친구 초대)로 같은 파티를 만든 뒤 파티장이 방을 만든다.

## 3. 친구와 파티 + 자동 매칭

- 실행하면 각자 1인 파티가 생긴다.
- 캐릭터 옆 `+ 친구 초대`(또는 상단 `친구`, F 키): 게임 안 친구 목록에서 `초대`. 받는 쪽은 Steam 초대를 수락한다. 또는 Steam 친구 목록의 "게임 참가"(Rich Presence).
- 파티장이 모드 카드에서 모드를 고르고 `게임 시작`(Enter)을 누르면 **같은 모드를 고른 파티끼리** 매칭된다. 파티원은 자동으로 따라간다.
- 누구든 `취소`하면 파티 전체 매칭이 취소되고 파티는 유지된다.

## 4. 봇으로 인원 채우기

- PC 1대 12인: `+` 다섯 번 → `비공개 방 만들기` → `12명 채우기`.
- PC 2대 6v6: 양쪽 파티 봇 5 → 각자 `빠른 매칭`(개발 빌드) → 자동 시작.
- 상세: [BOTS](BOTS.md).

## 5. 자동 테스트

```text
Linux/AI:  Tools/run-tests-linux.sh [--compile]
Windows:   ./Tools/Test-NetworkCore.ps1
           ./Tools/Test-NetworkCompile.ps1 -SteamRuntimeSources 'Library/PackageCache/<Steamworks 폴더>/Runtime'
```

Unity 경로가 다르면 PowerShell 스크립트에 `-UnityEditor '<Editor 폴더>'`를 준다. 현재 기준 Core 28개, 세션 12개.

## 6. 범위의 한계

- 이동은 **평면 캡슐 모터**(38×38, 충돌 없음)다. 네트워크 경기에서 킹러시 코스를 달릴 수 없다. 래그돌 네트워크는 미정.
- 기물 선택, 스킬, 라운드, 승패, 호스트 이전, 재접속, 신뢰 이벤트 채널이 없다.
- 12인 부하, 강제 종료, 실제 패킷 손실, NAT 환경은 미검증.
- 파티 명단 위조와 악성 호스트는 막지 않는다(신뢰할 수 있는 팀 테스트 전제).
- 검색은 Steam 기본 거리 필터다. 먼 국가끼리는 경기 번호로 직접 연결해 시험한다.

참고: [Steam 로비](https://partner.steamgames.com/doc/features/multiplayer/matchmaking), [Steam Networking Messages](https://partner.steamgames.com/doc/api/ISteamNetworkingMessages), [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET/tree/2025.164.1).

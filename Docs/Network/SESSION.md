# 파티·매칭·예약 (`SteamSession`, `TeamReservations`)

코드: `Scripts/Network/SteamSession.cs`, `Scripts/Core/TeamReservations.cs`. 테스트: `Tests/Network/SessionFlowTests.cs`(모의 Steam 12개), `NetworkCoreTests.cs`(예약 규칙).

## 1. 두 종류의 로비

| | 파티 로비 | 경기 로비 |
|---|---|---|
| 목적 | 같이 놀 친구 그룹 유지 | 양 팀 12명 수용 |
| 최대 | 6 | 12 |
| 공개 | 비공개(ID를 알면 입장 가능) | 자동 매칭은 공개, 테스트 방은 비공개 |
| ID | `Party` | `Match` |
| 주인 | 파티장 `IsLeader` | 최초 생성자 `Host` (고정) |
| 경기 취소 시 | 유지 | 나감 |

한 사람은 자기 파티 로비에 남은 채로 경기 로비에도 들어간다. 파티장과 경기 호스트는 같을 수도 다를 수도 있다.

`Busy` = 비동기 요청 중 ∨ 검색 중 ∨ 경기 방 안 ∨ 파티 `route`가 `idle`이 아님.

## 2. 시작과 초대

- `Initialize()`: Packsize/DLL 검사 → `SteamAPI.Init()` → Self ID → `InitRelayNetworkAccess()` → 콜백 등록(로비 채팅, 초대 `GameLobbyJoinRequested_t`, Rich Presence 참가 `GameRichPresenceJoinRequested_t`) → 실행 인자에 `+connect_lobby <ID>`가 있으면 그 파티로, 없으면 1인 파티 생성.
- 초기화 실패 → HUD `Steam 다시 연결` → `Retry()`.
- 초대: 게임 내 친구 패널의 `InviteToParty(friend)`(= `InviteUserToLobby`). 예비로 `Invite()`(오버레이).
- **Rich Presence(M5):** 파티가 한가할 때 `connect = "+connect_lobby <파티ID>"`, `status = "파티 n/6"`. 검색·경기 중에는 `connect`를 지우고 `status`만 "매칭 찾는 중/경기 대기실/경기 중". 값이 바뀔 때만 설정한다. 종료 때 `ClearRichPresence()`. 파싱은 `SteamSession.ParseConnect`.
- **버전 검사:** 입장한 로비의 `kind`, `protocol`, `build`를 `Incompatibility()`가 확인한다. 파티가 다른 버전이면 나와서 "게임 버전이 다릅니다 (내 버전 X / 상대 Y)"를 보여 주고 **새 1인 파티**를 만든다(옛 코드는 파티 없이 남았다).

## 3. 자동 매칭 알고리즘

별도 매칭 서버가 아니라 Steam 로비 검색 위에 만든 정책이다.

1. 파티장만 `FindMatch()`. 파티원 배열을 정렬해 `queuedHumans`로 고정하고, 파티 봇을 붙여 `queuedMembers`를 만든다. 새 ticket, 각 파티원의 `cancel` 기준값 기록.
   - 릴리스 빌드에서 파티 봇이 있으면 공개 매칭을 거부한다(M6, `BotsBlockPublicMatch`).
2. 파티 추가 입장을 잠그고 `route=search`. 파티원은 이를 보고 기다린다.
3. 검색 필터: `protocol`, **`build`**, `kind=match`, `phase=waiting`, `private=0`, 사람 수만큼 빈자리, 기본 거리, 최대 50개.
4. 결과의 `free0`/`free1`을 보고 **한 팀에 파티 전체(봇 포함)가 들어가는 방**만 후보로 남긴다.
5. 로비 ID 오름차순으로 시도. 검색 결과는 오래됐을 수 있어 입장이 최종 승인은 아니다.
6. 파티장이 먼저 입장해 호스트에 예약을 요청하고, 승인된 뒤에만 `route=<경기ID>`로 바꿔 파티원이 따라오게 한다.
7. 빈 결과가 두 번이면 자기 경기 로비를 만든다. 재시도는 1~3초 무작위, 검색 실패는 4초 뒤.
8. 자기 파티만 있는 공개 대기 방의 호스트는 약 6초 뒤, 이후 약 8초마다 **더 작은 로비 ID**의 방을 찾아 옮긴다(동시 생성 수렴).
9. 예약이 정확히 12이고 모두 입장 완료면 공개 경기를 자동 시작한다.

여러 파티가 이미 들어간 대기 방끼리는 합치지 않는다. 모의 12인 동시 검색 수렴 테스트가 실제 Steam의 수렴 시간을 보장하지는 않는다.

## 4. 파티 단위 예약

예약 요청 = 경기 로비 채팅의 UTF-8 JSON:

```json
{"kind":"reserve","ticket":"GUID","party":123,"members":[11,12,13]}
```

호스트는 채팅 발신자가 현재 로비 멤버인지 확인하고, **Steam이 알려준 발신자**를 leader로 쓴다(수신 버퍼 2048바이트).

`TeamReservations.Reserve()` 검사:
- sender·party ≠ 0, ticket 1~64자
- members 1~6명이고 sender 포함, 0·중복·다른 예약과 겹침 금지
- 같은 leader의 재요청은 party·ticket·배열이 완전히 같을 때만 동일 예약. 재요청이 만료를 연장하지 않는다
- 봇은 발신자가 될 수 없고, 봇 ID의 소유자가 발신자여야 한다
- 사용량이 적은 팀부터, 안 들어가면 반대 팀, 둘 다 안 되면 거절
- **예약 즉시 아직 입장하지 않은 파티원까지 정원에 센다.** 기한 25초
- 릴리스 빌드의 공개 방이면 봇이 포함된 요청을 거절한다(`|bots`)

결과는 `grant_<leaderID>` = `ticket|ok` / `|full` / `|expired` / `|bots`. 파티장은 자기 ticket 응답만 본다. `ok`가 아니면 다른 방을 찾는다. roster가 grant보다 먼저 와도 자기가 roster에 있으면 route를 갱신한다.

호스트가 파티원 전원의 실제 입장을 보면 `Committed=true`. 25초 안에 다 못 들어오거나, 입장 완료된 파티에서 한 명이 빠지면 **그 파티 예약 전체를 해제**한다. 봇은 로비에 들어오지 않으므로 항상 입장 완료로 친다.

예: 4+4로 각 팀 4명이면 빈자리는 4지만 새 4인 파티는 못 받는다. 3+3+2+2+1+1은 6 대 6을 채운다.

`PublishRoster()`는 예약된 사람 중 **실제 로비에 있는 사람(과 봇)만** roster에 싣는다. `free0/free1`은 예약 수로 계산한다.

보안 범위: 발신자 신원과 명단 형식은 확인하지만, 선언한 파티원이 정말 그 파티 소속인지는 독립적으로 검증하지 않는다.

## 5. 로비 데이터

| 키 | 위치 | 값 |
|---|---|---|
| `protocol` | 두 로비 | `chessfight.dua0731.network.v2` |
| `build` | 두 로비 | `NetworkRuntime.BuildTag` 예: `0.1.0-dev` |
| `kind` | 두 로비 | `party` / `match` |
| `route` | 파티 | `idle` / `search` / 경기 로비 ID |
| `cancel` | 파티원 member data | 취소할 때 새 GUID |
| `host` | 경기 | 호스트 Steam ID |
| `phase` | 경기 | `waiting` / `playing` / `closed` |
| `private` | 경기 | `0` 공개 / `1` 테스트 |
| `free0`, `free1` | 경기 | 예약 반영 팀별 남은 자리 |
| `grant_<ID>` | 경기 | 예약 결과 |
| `roster` | 경기 | `{"members":[{"id":…,"team":0,"slot":0}]}` (4096자, 최대 12, 중복·0·범위 검사) |

상태는 단일 enum이 아니라 `pending`, `Searching`, `Match`, `Started`, `admitted`, `seenRoster`, `privateRoom`, 파티 `route`의 조합이다. `generation`은 비동기 취소 토큰이다. 요청마다 번호를 기록하고 취소·시간 초과 때 올린다. 늦게 온 콜백은 번호가 다르면 무시하고, 이미 들어간 로비면 나온다.

| 시간 | 값 |
|---|---|
| 로비 상태 확인 | 0.25초 |
| Create/Join/Search 대기 한도 | 20초 |
| 예약 기한 | 25초 |
| 경기 입장 후 roster 대기 | 28초 |
| reserve 재전송 | 2초 |
| 호스트 변경·closed 감지 유예 | 2초 |
| HUD 갱신 | 0.2초 |

## 6. 취소·퇴장·호스트 이탈

- 파티장 취소: generation 증가, 검색 중단, `route=idle`, 파티 입장 다시 허용, 경기 로비 나감.
- 파티원 취소: 자기 `cancel`에 새 값 → 파티장이 보고 전체 취소.
- 파티 구성 변경: 검색 시작 때와 명단이 다르면 취소.
- 파티장 교체: 매칭 취소 후 안내.
- 호스트가 나갈 때 `phase=closed`, joinable=false를 먼저 게시한다.
- Steam이 경기 로비 소유자를 바꿔도 고정 Host와 다르면 2초 뒤 경기 종료. **로비 소유권 이전 ≠ 호스트 이전.**
- 시작 전에는 예약 그룹 단위 정리, 시작 후에는 로비에서 사라진 사람을 roster에서 뺀다.
- 방장 신호 끊김(스냅샷 없음)은 [MOTION §4](MOTION.md)의 단계(0.5/2/12초)로 처리한다.
- `SessionChanged` 이벤트 → `SteamMotion`이 연결·입력·예측 버퍼를 초기화한다.

## 7. 공개 API 요약 (HUD가 쓰는 것)

`FindMatch(bool privateTest)`, `JoinParty(id)`, `JoinPrivateMatch(id)`, `StartGame()`, `Cancel()`, `LeaveParty()`, `Retry()`, `Friends()`, `InviteToParty(id)`, `Invite()`, `SetPartyBots(n)`, `FillRoomWithBots()`, `ClearRoomBots()`.
속성: `Online`, `Party`, `Match`, `Host`, `IsHost`, `IsLeader`, `Busy`, `Searching`, `Started`, `PrivateRoom`, `Roster`, `PartyMembers`, `PartyBots`, `MaxPartyBots`, `RoomBots`, `Build`, `AllowPublicBots`, `BotsBlockPublicMatch`, `CanUseRoomBots`, `Status`, `Error`.

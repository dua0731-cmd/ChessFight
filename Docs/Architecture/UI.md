# UI (HUD)

사용자 요구: UI는 UXML(UI Toolkit)로 만든다(R2). 한국어, 화면 전체, 폴가이즈 메인처럼 **우측 하단 큰 시작 버튼**, **단계형 시작**(R7). 클릭이 반드시 되어야 한다(R8, R9). Steam 오버레이 대신 게임 안 친구 초대 UI(R10).

## 1. 기술 기반

| 항목 | 내용 |
|---|---|
| 방식 | UI Toolkit 런타임 패널. `RuntimePanels.Create`가 `PanelSettings`를 만들어(또는 `GameSceneConfig`의 것을 써서) `UIDocument`에 붙인다 |
| 테마 | `Resources/NetworkTheme.tss` = `@import url("NetworkHud.uss")` 한 줄. **Unity 기본 테마 없음** → ScrollView·Dropdown 등 복합 컨트롤 금지 |
| 해상도 | 참조 1280×720, ScaleWithScreenSize |
| 폰트 | `RuntimePanels.KoreanFont`: OS 폰트 맑은 고딕 → 나눔고딕 → Noto Sans KR → 굴림 → 돋움 → Arial Unicode MS. 실패하면 LegacyRuntime(한글 없음). Console에 `[ChessFight] HUD 폰트:` 로그 |
| 입력 | Active Input Handling = Old. uGUI·EventSystem 없음. UI Toolkit이 자체 이벤트 시스템으로 받는다 |
| 파일 | `NetworkHud.uxml/.uss`(로비), `IntroHud.uxml`, `MatchHud.uxml`(킹러시 경기) |
| 뷰 코드 | `Game/NetworkHudView.cs`(Steam을 모름, `HudModel`만 받는다). 채우는 쪽은 `Bootstrap/LobbyBootstrap.BuildModel()` |

**UXML의 `name`과 `NetworkHudView`의 `Q<T>(name)`이 계약이다.** 한쪽만 바꾸면 해당 버튼이 조용히 죽는다.

## 2. 클릭이 안 될 때를 대비한 이중 장치

1. **본 경로:** UI Toolkit 포인터 이벤트. Active Input Handling이 Old여야 동작한다(`InputSettingsGuard`).
2. **대체 경로(`NetworkHudView.Update`):** 실제 포인터 이벤트가 한 번도 오지 않으면 `Input.GetMouseButtonDown(0)`과 `panel.Pick`으로 버튼을 직접 누른다. 진짜 이벤트가 한 번이라도 오면 꺼진다. **키 입력은 도와주지 못한다.** 그래서:
   - 번호 칸에는 **붙여넣기** 버튼이 있다.
   - 친구 목록은 검색창이 아니라 **페이지 이동**이다.
   - 타이핑이 필요한 새 UI를 만들지 않는다.
3. **진단 로그:** 1초 뒤 패널 상태를 한 번 출력하고, 첫 포인터 이벤트를 받으면 한 번 기록한다. "버튼이 안 눌리고 로그도 없다"면 입력 백엔드 문제다.

버튼은 `focusable = false`다(키보드 포커스를 뺏어 WASD를 막지 않게). 입력칸 밖을 클릭하면 포커스가 root로 돌아가 이동이 재개된다.

## 3. 로비 HUD 배치

| 위치 | 카드 |
|---|---|
| 좌측 상단 | `CHESSFIGHT`, 상태·오류, `Steam 다시 연결`(초기화 실패 때만) |
| 좌측 중단 | AI 봇(`-` `+`, `12명 채우기`, `봇 비우기`), 번호로 직접 입장(번호 칸, 붙여넣기, 파티 참가, 경기 참가), `비공개 방 만들기`, WASD 토글 |
| 좌측 하단 | 명단(내 파티 n/6 또는 경기 명단 n/12) |
| 우측 상단 | 방 번호(파티, 경기, 복사), 상세 정보(Steam 상태, 연결 상태, 핑·응답, 입력 백엔드, **버전**, 개발 빌드면 `F8 지연 시뮬레이터`) |
| **우측 하단** | 단계 안내 문구 + **액션 스택** |
| 가운데 오버레이 | 친구 초대 패널 |

## 4. 액션 스택 — 단계형 시작

```text
Home   [게임 시작]                     ← 누르면 아무것도 시작되지 않는다. 화면 이동만
          ↓
Mode   [파티 생성] [빠른 매칭] [뒤로]
          ↓ 파티 생성          ↳ 빠른 매칭 → FindMatch()
Party  [친구 초대] [게임 시작] [파티 나가기] [뒤로]
                      ↳ FindMatch() (파티 전체로 공개 매칭)
Busy   [경기 시작] [취소]              ← 세션이 실제로 바쁘면 강제 전환
```

- `Busy`는 검색 중이거나, 경기 방 안이거나, 파티원이 파티장을 따라가는 중일 때다. `session.Busy`를 그대로 쓰지 않는다. 시작 직후 1인 파티를 만드는 동안에도 true라서 취소 버튼이 떠 버린다.
- 봇이 있는 파티는 **릴리스 빌드**에서 빠른 매칭·파티 게임 시작이 꺼지고 안내 문구가 나온다(M6).

## 5. 버튼과 호출

| 버튼 | 호출 | 효과 |
|---|---|---|
| 게임 시작(Home) / 파티 생성 / 뒤로 | 없음 | 단계 이동만 |
| 빠른 매칭, 게임 시작(Party) | `FindMatch()` | 공개 검색·생성 |
| 친구 초대 | 친구 패널 열기 | 아래 6절 |
| 파티 나가기 | `LeaveParty()` | 새 1인 파티 |
| 경기 시작(Busy) | `StartGame()` | 비공개 2명 이상 / 공개 12명일 때만 활성 |
| 취소 | `Cancel()` | 파티 유지, 경기 취소 |
| `-` / `+` | `SetPartyBots(n)` | 파티장, 매칭 전 |
| 12명 채우기 / 봇 비우기 | `FillRoomWithBots()` / `ClearRoomBots()` | 호스트, 시작 전. 릴리스 빌드는 비공개 방에서만 |
| 파티 복사 / 경기 복사 / 붙여넣기 | 클립보드 | 번호 복사·붙여넣기 |
| 파티 참가 / 경기 참가 | `JoinParty()` / `JoinPrivateMatch()` | 번호로 입장 |
| 비공개 방 만들기 | `FindMatch(true)` | 검색 없이 비공개 경기 |
| Steam 다시 연결 | `Retry()` | 초기화 실패 때만 보임 |

## 6. 친구 초대 패널

- `SteamSession.Friends()`가 Steam 친구를 읽어 Core의 `FriendInfo`(Steam 타입 없음)로 준다. 그래서 `Game`이 Steam을 몰라도 그릴 수 있다. **이 분리를 깨지 않는다.**
- 정렬: 이 게임 중 → 온라인 → 자리 비움 → 오프라인, 이름순. 한 페이지 8명, `온라인만`/`전체 보기`, ◀ ▶, 새로고침. 열려 있으면 3초마다 다시 읽는다.
- 행의 `초대` = `SteamMatchmaking.InviteUserToLobby`(오버레이 없음). 받는 쪽은 기존 `GameLobbyJoinRequested_t` 경로.
- `Steam 오버레이로 초대`는 예비 수단.
- 매칭 중에는 파티가 잠기므로 패널이 자동으로 닫힌다.
- 파티가 대기 중이면 Rich Presence로 Steam 친구 목록에서 "게임 참가"도 가능하다([Network/SESSION](../Network/SESSION.md)).

## 7. 인트로·경기 HUD

- `IntroHud.uxml`: 타이틀, Steam 상태, "아무 키나 눌러 시작". 클릭이 필요 없다.
- `MatchHud.uxml`(`KingRushMatchView`): 읽기 전용. 상태, 명단, 핑/응답 줄, Esc 안내. 방장 신호가 끊기면 상단 가운데에 **노란 "연결 불안정" → 빨간 "멈춤"** 배너(`.warning`, `.warning-frozen`). 매 프레임 갱신한다.
- 오프라인 플레이테스트 도움말은 IMGUI(`PlaytestSpawner.OnGUI`)로 그린다.

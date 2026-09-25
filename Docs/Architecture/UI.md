# UI (HUD)

사용자 요구: UI는 UXML(UI Toolkit)로 만든다(R2). 한국어, 화면 전체, 폴가이즈 메인처럼 **우측 하단 큰 시작 버튼**(R7). 클릭이 반드시 되어야 한다(R8, R9). Steam 오버레이 대신 게임 안 친구 초대 UI(R10). **로비는 참고 영상처럼**: 파티 라인업, 모드 카드, 파티 바, 매칭 패널(R19).

## 1. 기술 기반

| 항목 | 내용 |
|---|---|
| 방식 | UI Toolkit 런타임 패널. `RuntimePanels.Create`가 `PanelSettings`를 만들어(또는 `GameSceneConfig`의 것을 써서) `UIDocument`에 붙인다 |
| 테마 | `Resources/NetworkTheme.tss` = `@import url("NetworkHud.uss")` 한 줄. **Unity 기본 테마 없음** → ScrollView·Dropdown 등 복합 컨트롤 금지 |
| 해상도 | 참조 1280×720, ScaleWithScreenSize |
| 폰트 | `RuntimePanels.KoreanFont`: OS 폰트 맑은 고딕 → 나눔고딕 → Noto Sans KR → 굴림 → 돋움 → Arial Unicode MS. 실패하면 LegacyRuntime(한글 없음). Console에 `[ChessFight] HUD 폰트:` 로그 |
| 입력 | Active Input Handling = Old. uGUI·EventSystem 없음. UI Toolkit이 자체 이벤트 시스템으로 받는다 |
| 파일 | `NetworkHud.uxml`(로비), `NetworkHud.uss`(모든 HUD 공용), `IntroHud.uxml`, `MatchHud.uxml`(모드 공용 경기 화면) |
| 뷰 코드 | `Game/NetworkHudView.cs`(Steam을 모름, `HudModel`만 받는다). 채우는 쪽은 `Bootstrap/LobbyBootstrap.BuildModel()` |

**UXML의 `name`과 `NetworkHudView`의 `Q<T>(name)`이 계약이다.** 한쪽만 바꾸면 해당 버튼이 조용히 죽는다.

## 2. 클릭이 안 될 때를 대비한 이중 장치

1. **본 경로:** UI Toolkit 포인터 이벤트. Active Input Handling이 Old여야 동작한다(`InputSettingsGuard`).
2. **대체 경로(`NetworkHudView.Update`):** 실제 포인터 이벤트가 한 번도 오지 않으면 `Input.GetMouseButtonDown(0)`과 `panel.Pick`으로 버튼을 직접 누른다. 진짜 이벤트가 한 번이라도 오면 꺼진다. **키 입력은 도와주지 못한다.** 그래서:
   - 번호 칸에는 **붙여넣기** 버튼이 있다. 주요 동작에는 단축키(Enter, F, M, Esc)도 있다.
   - 친구 목록은 검색창이 아니라 **페이지 이동**이다.
   - 타이핑이 필요한 새 UI를 만들지 않는다.
3. **진단 로그:** 1초 뒤 패널 상태를 한 번 출력하고, 첫 포인터 이벤트를 받으면 한 번 기록한다. "버튼이 안 눌리고 로그도 없다"면 입력 백엔드 문제다.

버튼은 `focusable = false`다(키보드 포커스를 뺏어 단축키를 막지 않게). 입력칸 밖을 클릭하면 포커스가 root로 돌아가 단축키가 다시 동작한다. 동적으로 만드는 버튼(친구 행, `+ 친구 초대`, 모드 항목)도 대체 경로 목록에 등록한다.

## 3. 로비 화면 (2026-09-25 재구성, R19)

사용자가 준 참고 영상(폴가이즈풍 메인 화면)을 따랐다. **로비는 메뉴다.** 캐릭터가 움직이지 않고(WASD 꺼짐), 파티가 가운데에 줄지어 선다.

```text
┌ CHESSFIGHT  플레이 기물도감 전적 ─────────────── [친구 n] [정보] [내 이름] ┐
│                  (알림 토스트 / Steam 끊김 배너 / 매칭 패널)                 │
│                                                                            │
│        ★ 나이트메어        퀸사이드        (+) 친구 초대                      │
│        파티장 · 2/6        대기 중                                          │
│          [ 나 ]            [ 파티원 ]       [ 빈 자리 ]   ← 3D (LobbyStage)   │
│                                                                            │
│ [# 코드로 참가] [비공개 방]                         ┌ MODE 킹 러시  변경 > ┐   │
│ ┌ PARTY 2/6 │ 파티 코드 복사 │ AI 봇 - 0 + │ 파티 나가기 ┐ │  게임 시작  Enter │   │
│ └────────────────────────────────────────────┘ └──────────────────┘   │
│                 Enter 게임 시작 · F 친구 · M 모드 · Esc 닫기    v0.1.0-dev · STEAM │
└────────────────────────────────────────────────────────────────────────────┘
```

| 위치 | 내용 | 이름(UXML) |
|---|---|---|
| 상단 바 | 로고, 탭(플레이만 동작, 기물 도감·전적은 **자리만**), `친구 n`(온라인 친구 수), `정보`(연결 정보 카드), 내 Steam 이름 | `friends-open`, `details-open`, `profile` |
| 상단 가운데 | 알림 토스트(파티 참가·나감, 초대 보냄, 복사, 오류는 빨강), Steam 끊김 배너 + `Steam 다시 연결`, **매칭 패널** | `toast`, `offline`, `retry`, `match-panel` |
| 가운데 (3D) | 나 가운데, 파티원·파티 봇이 좌우로. 빈 자리 최대 2곳에 회색 실루엣 + `+ 친구 초대`. 이름표는 코드가 3D 위치에 맞춰 옮긴다 | `lineup` |
| 좌측 하단 | `# 코드로 참가`(번호 입력 창), `비공개 방`, **파티 바**(인원 n/6, 파티 코드 + 복사, AI 봇 `-` `+`, 파티 나가기) | `code-open`, `create-test`, `party-*`, `bots-*`, `leave-party` |
| 우측 하단 | **모드 카드**(누르면 모드 선택 창) + 노란 **게임 시작**. 바쁠 때는 그 자리에 **매칭 중 카드**(시간·인원, 취소, 방장이면 경기 시작) | `mode-open`, `play`, `busy`, `cancel`, `start` |
| 가운데 창 | 코드로 참가(번호 칸, 붙여넣기, 파티 참가, 비공개 방 참가), 모드 선택 | `code-modal`, `mode-modal` |
| 우측 카드 | 친구 초대, 연결 정보(상태 문장, Steam·연결·핑·입력·버전, F8) | `friends`, `details-card` |

뷰 코드: `Game/NetworkHudView.cs`(Steam 모름, `HudModel`만 받음). 채우는 쪽: `Bootstrap/LobbyBootstrap.BuildModel()`. 3D: `Game/LobbyStage.cs`(프리미티브 + 팀 재질 복사본, 새 에셋 없음. 캐릭터 겉모습은 `PawnAvatar` 프리팹).

## 4. 시작 흐름 — 참고 영상대로 바꿈

```text
대기   [MODE 카드] [게임 시작]          ← 파티장: 파티 전체로 공개 매칭 시작(FindMatch)
                                          파티원: 비활성, "파티장이 시작하기를 기다리는 중"
  ↓ 게임 시작 / 비공개 방 / 코드로 참가
바쁨   [매칭 중 00:03 · 4/12] [취소]      ← 상단에 매칭 패널: QUICK MATCH · 모드, 6칸 VS 6칸
       (비공개 방 방장) [경기 시작]         방 안이면 방 번호·복사·12명 채우기·봇 비우기
```

- **R7의 단계형(게임 시작 → 파티 생성/빠른 매칭 → …)은 R19에서 참고 영상 흐름으로 바뀌었다**([DECISIONS U7](../Project/DECISIONS.md)). 파티는 항상 존재하므로(1인 파티 자동 생성) "파티 생성" 단계가 필요 없고, 게임 시작 위의 모드 카드가 무엇을 시작하는지 보여 준다.
- 바쁨 = 검색 중 ∨ 경기 방 안 ∨ 파티원이 파티장을 따라가는 중. `session.Busy`를 그대로 쓰지 않는다(1인 파티를 만드는 동안에도 true).
- 매칭 중에는 이름표를 숨긴다(매칭 패널과 겹치지 않게, 영상과 같다).
- 봇이 있는 파티는 **릴리스 빌드**에서 게임 시작이 꺼지고 "봇 포함 시 비공개 방 전용"이 뜬다(M6).
- 단축키: **Enter** 게임 시작, **F** 친구, **M** 모드, **Esc** 창 닫기. 대체 클릭 경로에서도 키는 동작한다.

## 5. 버튼과 호출

| 버튼 | 호출 | 조건 |
|---|---|---|
| 게임 시작 | `FindMatch()` | 한가한 파티장, 모드에 씬 있음, (릴리스) 봇 없음 |
| 비공개 방 | `FindMatch(true)` | 한가한 파티장 |
| 모드 선택 창의 모드 | `SetMode(key)` | 한가한 파티장, 씬 있는 모드. 준비 중 모드는 잠김 |
| 경기 시작 | `StartGame()` | 비공개 방 방장, 2명 이상(봇 포함) |
| 취소 | `Cancel()` | 바쁠 때. 파티는 유지 |
| 파티 나가기 | `LeaveParty()` | 한가할 때. 새 1인 파티 |
| `-` / `+` | `SetPartyBots(n)` | 한가한 파티장 |
| 12명 채우기 / 봇 비우기 | `FillRoomWithBots()` / `ClearRoomBots()` | 방장, 시작 전. 릴리스 빌드는 비공개 방만 |
| 파티 코드 복사 / 방 번호 복사 | 클립보드 | 숫자만 복사(화면에는 4자리씩 띄어 보임) |
| 코드로 참가 창: 붙여넣기 / 파티 참가 / 비공개 방 참가 | 클립보드 / `JoinParty()` / `JoinPrivateMatch()` | 띄어쓰기가 있어도 읽는다 |
| `+ 친구 초대`, 친구 | 친구 카드 열기 | 한가하고 파티가 6명 미만 |
| Steam 다시 연결 | `Retry()` | Steam 초기화 실패 때만 보임 |

## 6. 친구 초대 카드

- `SteamSession.Friends()`가 Steam 친구를 읽어 Core의 `FriendInfo`(Steam 타입 없음)로 준다. 그래서 `Game`이 Steam을 몰라도 그릴 수 있다. **이 분리를 깨지 않는다.**
- 그룹: **이 게임 중 → 온라인 → 자리 비움 → 오프라인**, 그룹 제목에 인원. 이 게임 중인 친구는 상대의 Rich Presence 상태("킹 러시 경기 중", "로비에서 대기 중 · 파티 1/6")가 이름 아래에 보인다(`FriendInfo.Detail`).
- 한 페이지 7명, `온라인`/`전체`, `<` `>`, 새로고침. 열려 있으면 3초마다, 닫혀 있으면 10초마다(상단 `친구 n` 숫자용) 다시 읽는다.
- 이미 내 파티인 친구는 `초대` 대신 "내 파티".
- 행의 `초대` = `SteamMatchmaking.InviteUserToLobby`(오버레이 없음). 받는 쪽은 기존 `GameLobbyJoinRequested_t` 경로. `Steam 오버레이로 초대`는 예비 수단.
- 매칭 중에는 파티가 잠기므로 카드가 자동으로 닫힌다.

## 7. 인트로·경기 HUD

- `IntroHud.uxml`: 하늘색 배경, `CHESS FIGHT` 로고, "6 vs 6 · 체스 말 파티 게임", Steam 상태, "아무 키나 눌러 시작". 클릭이 필요 없다.
- `MatchHud.uxml`(`MatchSceneView`): 읽기 전용. 제목은 경기 모드 이름, 상태, 명단, 핑/응답 줄, Esc 안내. 방장 신호가 끊기면 상단 가운데에 **노란 "연결 불안정" → 빨간 "멈춤"** 배너(`.warning`, `.warning-frozen`). 매 프레임 갱신한다.
- 오프라인 플레이테스트 도움말은 IMGUI(`PlaytestSpawner.OnGUI`)로 그린다.

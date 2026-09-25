# 퀸 오브 더 힐 — 기획·개발 인수인계 (Claude Code → GPT 아스트라)

작성 2026-09-25, 브랜치 `JY-lobby`. 이 모드를 **구체적인 기획부터 개발까지** 다른 AI(ChatGPT 아스트라)가 이어받기 위한 문서다. 사용자가 요청했다(R24).

**읽는 순서:** 루트 [`HANDOFF.md`](../../../HANDOFF.md) 전체 → **이 문서** → [게임 모드 개요](../README.md) → [N4 참고 원문](N4_REFERENCE.md) → 필요할 때 §6 파일 지도의 문서·코드.
**우선순위:** 사용자의 최신 발언 > 이 문서의 "사용자 확정" > N4 원문의 "사용자 확정" > N4 원문의 "임시값"(미승인).

---

## 0. 한눈에

- **퀸 오브 더 힐** = 게임 모드 키 `queenhill`. 가운데 성(구조물)을 올라 **정상에 먼저 닿은 폰이 퀸으로 승격**하는 팀 대결.
- 로비에는 이미 모드로 들어가 있다. **씬이 없어서 "준비 중"으로 잠겨 있다.** 씬을 만들고 `GameModes.cs`에 씬 이름을 넣으면 로비에서 고를 수 있게 된다.
- 개발 순서(사용자 결정): 로비(완료) → **퀸 오브 더 힐(지금)** → 소드 파이트 → 킹 러시(기획 정리 후).
- 가장 큰 기술 문제: **네트워크 경기의 캐릭터는 아직 평면 캡슐이라 등반이 불가능하다.** 등반하는 캐릭터는 준영 님의 **래그돌**이고, 네트워크 래그돌은 랩에 2인 1단계만 있다. 그래서 **오프라인(직접 Play)으로 먼저 만들고, 네트워크는 나중 단계**로 권한다(§5).

## 1. 작업 조건 — 사용자 지시, 반드시 지킨다

| 조건 | 출처 |
|---|---|
| 커밋·푸시는 **`JY-lobby`에만**. `main`, `Network`, `JY-ragdoll`에는 푸시하지 않는다. `JY-ragdoll`은 래그돌 물리 튜닝용으로 따로 유지한다 | R18 |
| **캐릭터 조작·물리(래그돌)는 이 브랜치에서 바꾸지 않는다.** `Assets/ChessFight/RagdollLab/`은 읽기만 한다. 래그돌 쪽 변경이 필요하면 사용자에게 알리고 준영 님과 `JY-ragdoll`에서 한다 | R18 |
| 어셈블리 경계: `Assets/Scripts`는 래그돌 타입을 모른다. 캐릭터는 **`ICharacterDriver`로만** 부른다. 래그돌은 Steam을 모른다. 둘을 잇는 코드는 `Assets/ChessFight/RagdollLabSteam/` 같은 별도 다리 어셈블리에만 둔다 | HANDOFF §4-13 |
| 사용자는 **초보**이고 코드를 직접 쓰지 않는다. 대답은 **쉬운 한국어**, 표와 단계로 쓴다. 코드 주석·커밋 메시지는 **영어**. Unity에서 확인할 것은 **순서대로** 적어 준다 | R18, HANDOFF §7 |
| **검증은 정직하게.** 사용자가 Unity·Steam에서 직접 본 것만 "성공"이다. 코드 작성·테스트 통과는 검증이 아니다 | HANDOFF §4-10 |
| 작업마다 테스트(`Tools/run-tests-linux.sh --compile`, Windows는 `Tools/Test-NetworkCore.ps1`)와 [HANDOFF §8](../../../HANDOFF.md#8-작업-종료-체크리스트) 문서 갱신 | R18 |
| 모드마다 따로 매칭한다(이미 구현됨) | R19 |

**아스트라가 저장소에 직접 푸시할 수 없으면:** 바뀐 파일 전체 내용과 커밋 메시지를 사용자에게 주고, 사용자가 GitHub Desktop으로 `JY-lobby`에 커밋·푸시한다. 사용자에게 **"GitHub Desktop 위쪽 Current branch가 `JY-lobby`인지"** 꼭 확인시킨다.

## 2. 지금 저장소 상태 (2026-09-25, `JY-lobby`)

| 커밋 | 내용 | Unity 확인 |
|---|---|---|
| `ec6bc1f` | 게임 모드 목록, 모드별 매칭(프로토콜 v3), 모드 씬 로드, 영상풍 새 로비, 타이틀 | 로비 라인업·이름표·`+ 친구 초대`·상단 바·한글 **성공**(사용자 스크린샷) |
| `d55f67d` | Input System 패키지 제거(켤 때마다 뜨던 백엔드 창 제거) | 오류 없음 확인 |
| `9d49b81` | HUD 루트를 화면 전체로 늘림(아래쪽 카드가 안 보이던 문제) | **미확인** — 모드 카드·게임 시작 버튼이 보이는지 아직 모름 |

- 자동 검사: 경계 검사, Core 29개, 모의 Steam 세션 15개, 래그돌 포함 7개 어셈블리 Roslyn 컴파일 통과.
- 남은 확인 목록: [VALIDATION](../../Network/VALIDATION.md) 최상단 "새 로비와 게임 모드" 표.
- **사고 기록(R23):** 사용자가 한 번 **다른 폴더의 빈 Unity 프로젝트**를 열어 "파일이 다 사라졌다"고 느꼈다. Unity 위쪽 메뉴에 **`ChessFight`**가 있는지로 올바른 프로젝트인지 먼저 확인시킨다([PITFALLS #12](../../Environment/PITFALLS.md)).
- 사용자 Unity: **6000.3.11f1**, Windows, Unity Hub. 첫 흐름: `ChessFight > Scenes > Intro (online flow)` → Play → 아무 키 → 로비.

## 3. 기획 재료

### 3.1 사용자 확정 (N4 세션 9/19~20, [원문 §1](N4_REFERENCE.md))

1. 폰 여러 명이 주인공이다. 가운데 구조물 꼭대기까지 올라가 **승격**하는 것이 목표다.
2. 구성은 **폰 4~8명 + 나머지 기물 각 1~2개**다. **팀당인지 두 팀 합계인지 미확인**이다.
3. 가운데 **구조물의 레벨 디자인과 등반 액션은 최소한 제대로** 되어 있어야 한다.
4. 혼자 테스트하므로 아군·적군을 **전부 AI로 채울 수 있어야** 한다.
5. **모든 기물을 직접 플레이**해 볼 수 있어야 한다.
6. 기물 능력(9/19 기획 3-3절):
   - 나이트: 2단 점프 + 밟기로 찌그러뜨림
   - 룩: 선딜 후 고정 방향 긴 돌진
   - 비숍: 6초 호버 + 돌조각 투사체
   - 폰: 능력 없음. 뒤에서 치면 "앙파상"
   - 퀸: 승격한 폰 딱 1명
7. (R19) 게임 모드 하나 = 미니게임 하나. 모드마다 따로 매칭.

### 3.2 N4 프로토타입의 임시값 (사용자 미승인, [원문 §2·§4](N4_REFERENCE.md))

- 기본 구성은 팀당 폰 4 + 룩·비숍·나이트 각 1(7 대 7)이었다.
- 퀸은 라운드당 1명이다. 정상 구역에 먼저 들어간 폰이 되고, 동시에 도착하면 중앙에 가까운 폰이 된다.
- 퀸이 생기면 이후 죽은 사람은 부활하지 않는다. 퀸 팀은 상대를 전멸시키면 이긴다. 상대 팀은 퀸을 맵 밖으로 떨어뜨리면 이긴다. 6분이 지나면 무승부다.
- 퀸 등장 전에 떨어지면 3초 뒤 부활한다. 아군끼리는 밀치기가 안 된다.
- 퀸 수치: 체력 150, 칼 60, 속도 7 m/s. 룩·비숍·나이트 수치는 원문 표에 있다.
- 구조물 "The Keep"은 40×60 m 바닥 위에 있다. 1층 2.6 m → 성탑 4.6 m → 2층 5.2 m → 발코니 7.8 m → 정상 기둥 10.4 m(금색 판)이고, 180도 회전 대칭이다. AI 경로는 3개다.
- **이 수치들은 키 1.8 m 캡슐 캐릭터 기준이다.** 이 저장소의 래그돌에는 그대로 맞지 않는다(§3.4).

### 3.3 원문이 없는 자료 — 사용자에게 받아야 함

N4 원문이 참조하는 **9/19 기획개발 핸드오프**(기물 능력 3-3절 원문), **기본 기획 v0.2 PDF**, **전체 기획안 v0.3 .md**는 이 저장소에 없다. 사용자가 가지고 있으면 대화 첫머리에 첨부해 달라고 한다.

### 3.4 기획에서 정해야 할 것 — 사용자와 먼저 확정한다

| # | 항목 | 선택지 | 이 저장소의 제약·권고 |
|---|---|---|---|
| D1 | **인원·구성** | (a) 지금 매칭 그대로 **6 대 6** 안에서 구성(예: 팀당 폰 3 + 룩·비숍·나이트 1) (b) 팀 인원을 늘림 | 매칭은 **팀당 6명 고정**이다(`TeamReservations.TeamSize = 6`). 명단 검사도 slot 0~5만 받는다(`SteamSession.ReadRoster`). (b)는 매칭·예약·명단·프로토콜을 모두 바꾸는 큰 네트워크 작업이다. **(a) 권고.** 봇이 빈자리를 채운다 |
| D2 | 킹이 있나 | 없음 / 있음(역할?) | N4에는 킹이 없었다 |
| D3 | 퀸 승격 판정 | 정상 구역 첫 진입, 동시 도착 규칙 | 판정은 **호스트 PC 한 곳**에서 한다(호스트 권위, 결정 K2) |
| D4 | 승리 조건·제한 시간·부활 | N4 임시값 채택 / 수정 | 결과를 로비로 돌려보내는 흐름은 아직 없다(§5 E단계) |
| D5 | 기물 능력의 조작 | 버튼 하나 추가(예: `Ability`) | 캐릭터 입력 약속 `CharacterCommand`에는 Move·Jump·Shove·Grab만 있다. 능력을 넣으려면 약속에 필드를 추가하고, 래그돌 어댑터(`RagdollDriver`, 래그돌 폴더)도 바꿔야 한다 → **준영 님과 협의 필요**. 전력질주(Shift)도 랩에는 있지만 이 약속에는 아직 없다 |
| D6 | 기물 선택 | 로비 영상의 "기물 선택" 화면(1·2지망, 제한 시간, 모두 확정 → 시작) | 모든 모드 공통 흐름이라 별도 작업(§5 E단계) |
| D7 | **구조물 크기** | N4 수치를 래그돌 기준으로 다시 잡음 | **래그돌 폰의 키는 1.0 m**다(N4 캡슐은 1.8 m). 래그돌은 55°보다 가파른 벽이면 **어디든 스테미나로 오른다.** 스테미나 8초, 오를 때 초당 약 1.25를 써서 한 번에 약 **6~8 m**, 속도 1.2 m/s, 땅에서 4초면 다시 가득 찬다. 그래서 N4의 "한 번에 잡는 벽 2.0~3.2 m" 규칙은 맞지 않는다. 대신 **"쉬는 턱 사이 오르막 길이"**로 난이도를 설계한다. 수치 출처: [RagdollLab README](../../RagdollLab/README.md) "등반". 점프 높이는 문서에 없으니 RagdollTest에서 재 본다 |
| D8 | AI 봇 | 경로 기반 AI | 지금 봇(`BotBrain`)은 평면 캡슐만 움직인다. 래그돌 봇은 `ICharacterDriver.SetCommand`로 움직이는 새 AI가 필요하다. N4의 `BotPilot`(경로·점프·등반·재계획)이 참고 아이디어다(코드는 없음) |

**권장 첫 산출물:** 사용자와 D1~D8을 정해 **`Docs/GameModes/QueenOfTheHill/DESIGN.md`(기획서)**로 남긴다. 각 항목에 "사용자 확정 / 임시값"을 표시한다.

## 4. 기술 현황 — 이 저장소가 N4와 다른 점

| | N4 프로토타입(참고만) | 이 저장소(ChessFight, `JY-lobby`) |
|---|---|---|
| 네트워크 | 별도 세션 컨트롤러, 최대 16명 | **Steam 로비 + 방장(호스트) 판정**, 파티 단위 6 대 6 매칭, 봇, 모드별 매칭 |
| 캐릭터 | 1.8 m 캡슐 `PlayerMotor`, 자동 매달리기 | **래그돌**(`RagdollPawn`, 키 1.0 m, 잡기·밀치기·슬라이딩 태클·스테미나 등반) — 준영 님 담당, 이 브랜치에서 수정 금지 |
| 네트워크 경기의 캐릭터 | — | **평면 캡슐(`PawnMotor`)**: 38×38 m 안, 충돌 없음, **등반 불가** |
| 네트워크 래그돌 | — | 랩에만 1단계가 있다(`SteamRagdollLink`, 채널 32). 2인 호스트 권위이고, 12명 30 Hz면 약 2 Mbit/s다. **랩 씬(LabGame이 있는 씬)에서만 켜진다** |
| 레벨 | JSON → 에디터 빌더가 씬 생성 | 씬은 Unity에서 만들거나 에디터 빌더 메뉴로 만든다(아래) |
| UI | 자체 | UI Toolkit. 기본 테마 없음 → **ScrollView·Dropdown 금지**. `RuntimePanels.Create`로 만든다(루트 늘리기 포함, PITFALLS #18) |
| 입력 | — | **레거시 `Input`만.** Input System 패키지는 R20에서 제거했으니 다시 넣지 않는다 |

지켜야 할 기술 규칙(자세히는 [HANDOFF §4](../../../HANDOFF.md)):
- 래그돌 씬은 **`PhysicsProfile`**(120 Hz, 솔버 24회)을 둔다.
- 움직이는 장애물은 **`ObstacleClock` 시간의 순수 함수**로만 만든다(누적 금지).
- URP 컴포넌트를 씬에 넣지 않는다. 재질은 `ChessFight/NetworkColor` 또는 Built-in `Standard` 셰이더를 쓴다.
- Unity 밖에서 파일을 만들면 `.meta`를 `Tools/Generators/mkmeta.py`로 함께 만든다.
- **씬 YAML을 손으로 쓰지 않는다.** 사용자가 Unity에서 한 번 저장한 씬을 생성기로 덮어쓰지 않는다.
- `Core`(`Assets/Scripts/Core`)는 Unity 없이 Mono `mcs`로 테스트한다. 그래서 **최신 C# 문법(이름 있는 튜플 등)을 쓰지 않는다.**
- 같은 `.unity` 씬을 두 사람이 동시에 고치지 않는다.

**아스트라는 Unity를 실행할 수 없다.** 그래서 씬은 두 가지 방법 중 하나로 만든다.
1. **(권장) 에디터 빌더 메뉴:** 예를 들어 `Assets/Scripts/Editor/QueenHillBuilder.cs`에 메뉴 `ChessFight > Queen of the Hill > Build Scene`을 만든다. 성의 층·높이를 코드 상수나 JSON에서 읽어 `Assets/Scenes/QueenOfTheHill.unity`를 저장하게 한다. 사용자는 메뉴 한 번만 누르면 된다. 래그돌 랩의 `RagdollLabBuilder`와 N4의 `PromotionHillBuilder`가 같은 방식이다. 레벨을 고칠 때는 수치를 바꾸고 메뉴를 다시 누른다.
2. Unity에서 사용자가 손으로 만든다. 이 경우 아스트라가 단계별 클릭 순서를 준다.

## 5. 권장 개발 순서

한 단계 = 커밋 하나 + 테스트 + 문서 갱신 + 사용자의 Unity 확인이다.

| 단계 | 할 일 | 네트워크 | 래그돌 수정 |
|---|---|---|---|
| **0 기획** | §3.4 D1~D8 확정 → `DESIGN.md` | — | — |
| **A 오프라인 맵** | 빌더 메뉴로 `QueenOfTheHill.unity` 생성: 바닥, 성 구조물, 스폰 12곳(`SpawnPoint`, 발 닿는 바닥에), 정상 구역, `PhysicsProfile`, `ChessFight Game Root`(`GameSceneConfig`), `Playtest`(`PlaytestSpawner`의 캐릭터를 `Assets/ChessFight/RagdollLab/Prefabs/RagdollPawn.prefab`으로). 직접 Play하면 래그돌로 성을 올라 볼 수 있어야 한다 | 없음 | 없음(프리팹을 참조만) |
| **B 규칙(순수 로직)** | `Core/QueenHillRules.cs`(Unity·Steam 없음): 퀸 1명, 동시 도착, 승리·무승부, 제한 시간, 부활 시각 → `Tests/Network/NetworkCoreTests.cs`에 테스트 추가. 정상 구역 컴포넌트는 `Gameplay/Course/`에 둔다(`FinishZone`·`Checkpoint`와 같은 방식, `ICharacterDriver`로 누가 들어왔는지 알림) | 없음 | 없음 |
| **C 오프라인 규칙 연결** | 직접 Play에서 정상에 닿으면 퀸 승격 표시, 떨어지면 부활, 결과 표시 | 없음 | 없음 |
| **D 로비 연결** | `GameModes.QueenOfTheHill`의 `Scene`에 `"QueenOfTheHill"`, `SceneNames`에 상수, 빌드 목록(`EditorBuildSettings`)과 `NetworkSetup.ShippedScenes`에 추가 → 로비에서 모드 선택 가능, 경기 시작 시 이 씬으로 이동. 이때 네트워크 캐릭터는 **아직 평면 캡슐**이다(한계를 문서에 적는다) | 흐름만 | 없음 |
| **E 네트워크 래그돌** | 누가 시뮬레이션할지 결정한다(랩 1단계처럼 호스트가 전부 계산 권장). 경기 씬용 다리 코드는 **Steam을 알아도 되는 다리 어셈블리**에 둔다(`Assets/Scripts`나 `RagdollLab` 안 금지). 퀸 승격 같은 **한 번만 일어나는 이벤트**는 호스트가 정하고 모두에게 알린다(예: 경기 로비 데이터 `queen=<id>`). 신뢰 이벤트 채널은 아직 없다([ROADMAP](../../Project/ROADMAP.md)) | 있음 | **준영 님과 협의** |
| **F 나머지** | 래그돌 봇 AI, 기물 능력(D5), 기물 선택 화면(D6), 라운드 소개·결과 → 로비 | 있음 | 능력은 협의 |

A~D는 이 브랜치 규칙 안에서 바로 할 수 있다. E·F의 래그돌 변경은 사용자 확인 없이 하지 않는다.

## 6. 파일 지도

| 무엇 | 파일 |
|---|---|
| 모드 목록(퀸 오브 더 힐 항목, `Scene` 비어 있음) | `Assets/Scripts/Core/GameModes.cs` |
| 모드별 매칭(로비 데이터 `mode`, 검색 필터) | `Assets/Scripts/Network/SteamSession.cs`(`SetMode`, `PartyMode`, `MatchMode`, `FindMatch`) |
| 경기 시작 → 모드 씬 로드, 씬 컨트롤러 붙이기 | `Assets/Scripts/Bootstrap/NetworkRuntime.cs`(`FollowMatch`, `Attach`) |
| 모드 공용 경기 화면(명단·핑·끊김·Esc) | `Assets/Scripts/Bootstrap/MatchSceneView.cs`, `Assets/Resources/MatchHud.uxml` |
| 씬 이름, 빌드 목록 | `Assets/Scripts/Game/SceneNames.cs`, `ProjectSettings/EditorBuildSettings.asset`, `Assets/Scripts/Editor/NetworkSetup.cs`(`ShippedScenes`, 씬 메뉴) |
| 캐릭터 약속 | `Assets/Scripts/Gameplay/Characters/ICharacterDriver.cs` |
| 래그돌 어댑터(읽기만) | `Assets/ChessFight/RagdollLab/Scripts/RagdollDriver.cs`, 프리팹 `Assets/ChessFight/RagdollLab/Prefabs/RagdollPawn.prefab` |
| 오프라인 플레이테스트 | `Assets/Scripts/Gameplay/Playtest/PlaytestSpawner.cs` |
| 코스 부품(스폰·체크포인트·골인) | `Assets/Scripts/Gameplay/Course/` |
| 물리 프로필, 장애물 | `Assets/Scripts/Gameplay/PhysicsProfile.cs`, `Assets/Scripts/Gameplay/Obstacles/` |
| 로비 모드 카드 색 | `Assets/Scripts/Game/NetworkHudView.cs`(`ModeColor`, `queenhill` 보라색) |
| 테스트 | `Tests/Network/`, `Tools/run-tests-linux.sh`, `Tools/Test-NetworkCore.ps1` |
| 문서 | [GameModes](../README.md), [SCENES §5](../../Architecture/SCENES.md), [KingRush](../../KingRush/README.md)(씬 구성 예시), [Player/RAGDOLL](../../Player/RAGDOLL.md), [RagdollLab README](../../RagdollLab/README.md), [Network/SESSION](../../Network/SESSION.md), [PITFALLS](../../Environment/PITFALLS.md) |

## 7. 아스트라 시작 프롬프트 (사용자가 붙여 넣음)

```text
ChessFight(Unity 6000.3.11f1, Steam 파티 게임) 저장소의 JY-lobby 브랜치에서
'퀸 오브 더 힐' 게임 모드를 기획부터 개발까지 이어서 진행해 줘.

먼저 읽을 것(순서대로):
1. 저장소 루트 HANDOFF.md 전체
2. Docs/GameModes/QueenOfTheHill/README.md (이 모드 인수인계 — 가장 중요)
3. Docs/GameModes/README.md, Docs/GameModes/QueenOfTheHill/N4_REFERENCE.md
저장소를 직접 읽을 수 없으면 나에게 위 파일들을 올려 달라고 말해 줘.

지킬 것:
- 커밋·푸시는 JY-lobby에만. main, Network, JY-ragdoll에는 올리지 않는다.
  직접 푸시할 수 없으면 바뀐 파일 전체와 커밋 메시지를 주면 내가 GitHub Desktop으로 올린다.
- 캐릭터 조작·물리(래그돌, Assets/ChessFight/RagdollLab)는 바꾸지 않는다. 필요하면 먼저 나에게 묻는다.
- Assets/Scripts는 래그돌 타입을 모르고, 캐릭터는 ICharacterDriver로만 부른다.
- 나는 초보라서 설명은 한국어로 쉽게, 코드 주석과 커밋 메시지는 영어로.
  Unity에서 확인할 것은 순서대로 알려 주고, 내가 Unity에서 직접 본 것만 '성공'으로 기록한다.
- 작업마다 테스트(Tools/run-tests-linux.sh --compile 또는 Tools/Test-NetworkCore.ps1)와
  HANDOFF.md §8 작업 종료 체크리스트대로 문서를 갱신한다.

첫 할 일: 코드부터 쓰지 말고, README §3.4의 기획 결정 D1~D8을 나와 하나씩 정해서
Docs/GameModes/QueenOfTheHill/DESIGN.md 기획서로 만들어 줘. 그다음 README §5 순서(A→B→C→D)로 개발한다.
```

함께 첨부하면 좋은 것: 9/19 기획개발 핸드오프, 기본 기획 v0.2 PDF, 전체 기획안 v0.3(§3.3). 로비 참고 영상은 로비 작업용이라 필요 없다.

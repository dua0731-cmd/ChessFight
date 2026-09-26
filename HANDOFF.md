# ChessFight HANDOFF — 모든 작업은 여기서 시작한다

> **AI·개발자 공통 규칙:** 새 채팅, 다른 AI 도구, 새 팀원 모두 **이 파일을 가장 먼저 끝까지 읽는다.**
> 그다음 아래 [6. 어디를 읽을까](#6-어디를-읽을까--작업-분야별-안내)에서 작업 분야 문서만 골라 읽고 코드로 간다.
> 작업을 마치면 [8. 작업 종료 체크리스트](#8-작업-종료-체크리스트)대로 **이 파일과 요구사항 기록을 갱신한다.** 그래야 다음 도구가 같은 지점에서 이어 간다.

최종 갱신: **2026-09-26** · 기준 브랜치 **`JY-lobby`**(로비·게임모드 작업), **`JY-ragdoll_v2`**(퀸 오브 더 힐 래그돌 기능, 캐릭터 조작·물리 변경), 그 밖의 AI 작업은 `Network` · 기준 커밋: 이 파일을 갱신한 커밋(`git log -1 -- HANDOFF.md`)

---

## 0. 30초 요약

- **ChessFight**는 체스 말 6종(킹·퀸·룩·비숍·나이트·폰)을 팀당 하나씩 맡아 **6 대 6**으로 여러 미니게임 라운드를 겨루는 파티 게임이다. 첫 미니게임은 폴가이즈식 레이스 **킹러시**다.
- 팀 5명, 약 12주, Unity **6000.3.11f1**. 전용 서버 없이 **Steam 로비 + 방장(호스트) PC가 판정**하는 구조다(Steamworks.NET, App ID 480).
- 지금까지 만든 것:
  - Steam 파티·6v6 자동 매칭
  - 네트워크 이동 테스트
  - AI 봇
  - 한국어 전체 화면 HUD
  - 게임 내 친구 초대
  - 씬 분리(Intro → Lobby → KingRush)
  - 킹러시·래그돌 팀 작업 환경
  - 연결 품질 기능(끊김 경고, 핑, 지연 시뮬레이터, 버전 검사)
  - 준영 님 래그돌 랩 병합(RagdollTest 씬 = 랩 씬, `RagdollDriver`로 게임 캐릭터 계약 연결)
  - **(`JY-lobby`) 참고 영상풍 새 로비와 게임 모드**: 킹 러시·퀸 오브 더 힐·소드 파이트 목록, 모드별 매칭, 모드 씬 로드 → [GameModes](Docs/GameModes/README.md)
  - **(`JY-ragdoll_v2`) 퀸 오브 더 힐 래그돌 기능 M1~M4**: 탈것(움직이는 발판·벽), 입력 확장(능력 E·Q, 상호작용 F, 조준), 피격 약속, 물·부활, RagdollTest의 [7] 시험대 → [Player/RAGDOLL §8](Docs/Player/RAGDOLL.md#8-퀸-오브-더-힐-기능-m1m4)
- **아직 캡슐 이동 기술 프로토타입이다.** 기물 선택, 스킬, 래그돌 네트워크, 라운드 규칙, 승패는 없다. 모드 중 씬이 있는 것은 킹 러시(코스 뼈대)뿐이다.
- 이 프로젝트의 AI 작업은 사용자(메인 기획자, GitHub `dua0731-cmd`)의 요청으로 진행되어 왔다. **요청 이력 전체는 [요구사항 기록](Docs/Project/REQUIREMENTS.md)에 있다.**

## 1. 현재 상태 스냅샷

| 항목 | 상태 |
|---|---|
| 개발 브랜치 | **로비·게임모드 작업은 `JY-lobby`에만 커밋·푸시**(2026-09-25 사용자 지시, R18. `main`·`Network`·`JY-ragdoll`에는 푸시 금지). **퀸 오브 더 힐의 래그돌 쪽 기능(캐릭터 조작·물리)은 `JY-ragdoll_v2`**(2026-09-26 사용자가 `JY-lobby`의 `d3d617d`에서 만듦, R34). 그 밖의 AI 작업은 기존대로 `Network` |
| `main` | `0df4403`(R17 병합). **`Network`의 커밋을 모두 포함하고 24커밋 앞선다**(09-25 확인). `JY-lobby`는 이 커밋에서 시작했다. 로비 작업을 `main`에 넣을지는 사용자 결정 |
| 다른 원격 브랜치 | `Network`(`0ecd3b6`, main에 포함됨), `JY-ragdoll`(준영, 래그돌 랩·**물리 튜닝용으로 유지**, R17), `킹을-지켜라`(**비호환**: Unity 6000.3.12f1·URP·uGUI·자체 Steam 전송), `SteamNetworkTest`(옛 실험) |
| 네트워크 프로토콜 | **`JY-lobby`: `chessfight.dua0731.network.v3`**(게임 모드 추가). **`JY-ragdoll_v2`: v4**(래그돌 랩 입력 패킷 27바이트, magic `CFR2`). `main`·`Network`는 v2. 캡슐 입력 패킷 magic `CFF2`(변경 없음). 다른 프로토콜 빌드와는 매칭 불가 |
| 자동 테스트 | 어셈블리 경계 검사 + Core 29개 + 모의 Steam 세션 15개 통과, 래그돌 포함 7개 어셈블리 Roslyn 컴파일 통과 (2026-09-25, `JY-lobby`). **`JY-ragdoll_v2`(09-26)**: 같은 테스트 + **실제 Unity 6000.3.11f1 DLL로 전 어셈블리 컴파일**(랩 Steam 다리·빌더 포함) 통과, **빌드한 랩 플레이어의 래그돌 자동 점검 46 통과 / 9 실패**(새 퀸 오브 더 힐 12개는 전부 통과, 실패 9개는 원래 코드에서도 같은 기존 실패 → [RagdollLab README](Docs/RagdollLab/README.md#자동-점검)) |
| Unity 실기 확인 | 2026-09-24까지: 로비 HUD 표시·한글·클릭·친구 초대 동작 (사용자 보고) |
| **Unity 미확인** | **퀸 오브 더 힐 래그돌 기능 M1~M4(09-26, JY-ragdoll_v2)**, **새 로비·게임 모드(09-25, JY-lobby)**, 씬 분리(72ddf9e), 연결 품질 기능(01dd655), 래그돌 병합(09-25)은 **아직 Unity에서 사람이 보지 않았다.** 확인 목록: [VALIDATION.md](Docs/Network/VALIDATION.md) 최상단 표들 |
| 두 PC 확인 | 파티 입장·공개 매칭 성사 확인(2026-09-22). **양방향 이동은 미보고** |

## 2. 무엇이 되고 무엇이 안 되는가

| 기능 | 상태 | 상세 문서 |
|---|---|---|
| Steam 파티(최대 6)·초대·번호 입장 | 동작 확인 | [Network/SESSION](Docs/Network/SESSION.md) |
| 공개 자동 매칭 6v6 (파티 단위 같은 팀 예약) | 두 PC 성사 확인, 12인 미확인 | [Network/SESSION](Docs/Network/SESSION.md) |
| 비공개 테스트 방 | 동작 확인 | 〃 |
| 게임 내 친구 초대 패널 | 동작 확인(옛 로비). 새 로비의 친구 카드는 Unity 미확인 | [Architecture/UI](Docs/Architecture/UI.md) |
| **새 로비**(참고 영상풍: 3D 파티 라인업, 모드 카드, 게임 시작 = 바로 매칭, 매칭 패널, 파티 바) | 코드·컴파일만, Unity 미확인 | [Architecture/UI](Docs/Architecture/UI.md) |
| **게임 모드**(킹 러시 선택 가능, 퀸 오브 더 힐·소드 파이트 준비 중), 모드별 매칭, 모드 씬 로드 | 코드·테스트만 | [GameModes](Docs/GameModes/README.md), [Network/SESSION](Docs/Network/SESSION.md) |
| 네트워크 이동(호스트 판정·클라 예측) | 1인 캡슐 표시 확인, 양방향 미보고. **`JY-lobby`에서는 로비에서 안 움직이고 경기 씬에서만 움직인다** | [Network/MOTION](Docs/Network/MOTION.md) |
| AI 봇(파티 봇, 방 채우기) | 코드·테스트만 | [Network/BOTS](Docs/Network/BOTS.md) |
| 끊김 경고, 점프 누른 횟수, 버전 검사, 핑, F8 시뮬레이터, Rich Presence | 코드·테스트만 | [Network/MOTION](Docs/Network/MOTION.md), [기획안 반영](Docs/Network/PLAN_V0.1_STATUS.md) |
| 씬 흐름 Intro → Lobby → 모드 씬(지금은 KingRush) → Lobby | 코드만, Unity 미확인 | [Architecture/SCENES](Docs/Architecture/SCENES.md) |
| 킹러시 오프라인 플레이테스트(임시 캡슐 캐릭터) | 코드만 | [KingRush](Docs/KingRush/README.md) |
| 장애물 3종(시간의 함수) | 코드만 | [KingRush/OBSTACLES](Docs/KingRush/OBSTACLES.md) |
| **퀸 오브 더 힐 래그돌 기능 M1~M4**(`JY-ragdoll_v2`): 탈것·움직이는 벽, 능력·상호작용·조준 입력, 피격(`IHitReceiver`), 물·부활(`WaterZone`), RagdollTest [7] 시험대 | 코드·자동 점검 통과(빌드한 랩 플레이어), Unity 미확인. M5~M14는 시작 안 함 | [Player/RAGDOLL §8](Docs/Player/RAGDOLL.md#8-퀸-오브-더-힐-기능-m1m4), [MECHANICS_TODO](Docs/GameModes/QueenOfTheHill/MECHANICS_TODO.md) |
| 래그돌 (RagdollTest = 래그돌 랩, 2인 로컬 + 랩 전용 Steam 2인 호스트 판정) | 병합·코드·컴파일만, Unity 미확인. 게임 씬(KingRush) 네트워크 래그돌은 없음. 조작·등반 등 상세는 [RagdollLab README](Docs/RagdollLab/README.md) | [Player/RAGDOLL](Docs/Player/RAGDOLL.md) |
| **없음** | 기물 선택·스킬·라운드·승패·점수, 네트워크 경기에서 코스 달리기, 래그돌 네트워크, 호스트 이전, 재접속, 신뢰 이벤트 채널, 로딩 동기화 | [ROADMAP](Docs/Project/ROADMAP.md) |

## 3. 진행 중인 일과 다음 할 일

**사용자가 할 일 — 순서대로**
0. (퀸 오브 더 힐 래그돌) `JY-ragdoll_v2`로 바꿔 Pull한 뒤 `ChessFight > Scenes > Ragdoll Test` → Play → [VALIDATION.md](Docs/Network/VALIDATION.md) 최상단 **퀸 오브 더 힐 표(13개)**. F9로 시험대에 가서 F5·F6·F7, 탈것, 물을 해 본다. 되는 것·안 되는 것을 알려 주면 그대로 기록한다
1. `JY-lobby`를 Pull(LFS 포함)한 뒤 Unity에서 열고 [VALIDATION.md](Docs/Network/VALIDATION.md) 최상단 **새 로비 표(11개)**를 확인한다. 이어서 씬 분리·래그돌 병합 표. 막히면 증상·스크린샷과 Console 첫 오류를 AI에게 준다.
2. 두 PC로 양방향 이동을 확인한다(가장 오래 미뤄진 검증). 이제 경기 씬에서 한다([Network/README §2](Docs/Network/README.md)).
3. 퀸 오브 더 힐을 시작하기 전에 결정: 인원 구성(6 대 6 안에서?), 래그돌 기준 구조물 크기 → [GameModes §3](Docs/GameModes/README.md).
4. 로비 작업을 `main`에 넣을지(병합 시점) 결정한다.

**팀원(이번 주 배정)**
- 준영: 래그돌은 병합됨. 이후 작업은 Network/main 기준, 멀티 규칙 준수 → [Player/RAGDOLL](Docs/Player/RAGDOLL.md)
- 진호·지성: 킹러시 맵과 장애물, 장애물별 기획서 작성 → [KingRush](Docs/KingRush/README.md)
- 승규: 네트워크 방어 기획. 추천 작업과 AI 프롬프트 → [기획안 반영 상태 §4](Docs/Network/PLAN_V0.1_STATUS.md#4-승규-님께-요청할-다음-작업)

**AI의 다음 작업 후보 (사용자 지시가 있을 때만 착수)**
1. Unity 확인 중 나오는 오류 수정 (최우선). 새 로비는 Unity에서 한 번도 안 열었다: 배치·겹침·색 조정이 나올 수 있다
2. **퀸 오브 더 힐**: **GPT 아스트라가 기획부터 이어받는다**(R24). 인수인계·기획 결정 D1~D8·개발 순서·시작 프롬프트: [GameModes/QueenOfTheHill/README](Docs/GameModes/QueenOfTheHill/README.md)
3. 경기 흐름: 기물 선택 화면(참고 영상), 라운드 소개, 결과 → 로비
   - **퀸 오브 더 힐 개발 목록(R33):** 래그돌·게임플레이에 새로 필요한 기능 M1~M14와 우선순위·시작 프롬프트 → [MECHANICS_TODO](Docs/GameModes/QueenOfTheHill/MECHANICS_TODO.md). **M1~M4는 `JY-ragdoll_v2`에서 구현됨(R34, Unity 미확인).** 다음은 §3 순서대로 **M5**(자유 조준 갈고리 + 앙파상) → M8·M9·M10 → M6·M7. 래그돌 작업 브랜치는 `JY-ragdoll_v2`
   - **기존 래그돌 자동 점검 실패 9개**(등반 7, 전력질주 스테미나 고갈, 경사 45°): 이번 작업 전 코드에서도 같은 숫자로 실패한다. 준영 님 확인 또는 사용자 지시가 있을 때 고친다 → [RagdollLab README "자동 점검"](Docs/RagdollLab/README.md#자동-점검)
   - **팀 색 정정(R32):** 이 게임의 두 팀은 **백팀·흑팀**이다(캐릭터 자체가 흰 말·검은 말). 코드는 아직 청팀·주황팀(재질 `TeamBlue`/`TeamOrange`, HUD 문구 "청팀/주황팀", 매칭 패널 칸 색). 바꾸려면 사용자 확인 후 작업
4. Network → main 병합 PR (현재는 main이 Network를 포함하므로 불필요할 수 있음)
5. 기획안의 "결정 필요" 항목: 신뢰 이벤트 채널 → 로딩 동기화 → 호스트 끊김 2단계(라운드 무효, 파티 유지)
6. 네트워크 경기에서 래그돌로 코스 달리기(래그돌 권한 결정 필요. 병합은 끝남)

## 4. 절대 규칙 — 어기면 과거에 실제로 사고가 났던 것들

1. **Active Input Handling은 `Input Manager (Old)`(0)로 둔다. Input System 패키지는 설치하지 않는다**(`JY-lobby`, R20: 설치돼 있으면 켤 때마다 백엔드를 켜라는 창이 뜬다). uGUI가 없어서 Both/New로 바꾸면 HUD 클릭과 입력이 전부 죽는다. `InputSettingsGuard`가 자동으로 되돌린다. → [PITFALLS §1](Docs/Environment/PITFALLS.md)
2. **프로젝트는 선택 패키지가 하나도 없어도 컴파일되어야 한다.** Safe Mode에서는 설치 스크립트가 돌지 않는다. 선택 패키지 코드는 전용 asmdef + `versionDefines` + `defineConstraints`로 가둔다. `ENABLE_INPUT_SYSTEM`을 패키지 설치 여부로 쓰지 않는다.
3. **`Packages/manifest.json`·`packages-lock.json`이 바뀌면 Discard하지 말고 커밋한다.**
4. **폴더 하나 = 어셈블리 하나.** `Game`·`Gameplay`는 Steam을 참조하지 않는다. `Core`는 Unity도 참조하지 않는다. → [Architecture/STRUCTURE](Docs/Architecture/STRUCTURE.md)
5. **Steam 초기화·종료 소유자는 `NetworkRuntime` 하나다.** SteamManager를 따로 추가하지 않는다.
6. **HUD에는 기본 테마가 없다.** ScrollView·Dropdown 같은 복합 컨트롤은 보이지 않는다. 평범한 VisualElement만 쓴다.
7. **장애물 위치·회전은 `ObstacleClock` 시간의 순수 함수로만 계산한다.** 프레임마다 누적하지 않는다.
8. **클라이언트는 입력만 보낸다.** 좌표를 권위로 받지 않는다. 파티는 같은 팀에 통째로 예약한다.
9. **네트워크 비호환 변경을 하면 `SteamSession.Protocol`을 올린다.** 패킷 구조를 바꾸면 `MotionProtocol` magic도 올린다.
10. **검증 표기는 정직하게 한다.** 사용자가 Unity나 Steam에서 직접 본 것만 VALIDATION에 '성공'으로 적는다. 코드 작성과 테스트 통과는 검증이 아니다.
11. **`.meta`는 항상 같이 커밋한다.** Unity 밖에서 파일을 만들면 `Tools/Generators/mkmeta.py`로 만든다.
12. **같은 `.unity` 씬을 두 사람이 동시에 고치지 않는다.** 맵은 구간 프리팹으로 나눈다.
13. **`Assets/Scripts`(네트워크·게임 코드)는 래그돌 타입을 참조하지 않고, 래그돌(`Assets/ChessFight/RagdollLab`)은 Steam을 참조하지 않는다.** 랩을 Steam에 잇는 코드는 `Assets/ChessFight/RagdollLabSteam/`(Bootstrap과 같은 다리)에만 둔다. 캐릭터는 `ICharacterDriver`로만 부른다. `Tools/run-tests-linux.sh`의 경계 검사가 확인한다.
14. **물리 콜백(`OnCollision*`·`OnTrigger*`) 안에서 순간이동하거나 관절을 즉시 지우지 않는다.** Unity가 `DestroyImmediate`를 거부해 잡기 관절이 남는다. 알리기만 하고 다음 Update에서 처리한다(`WaterZone` → 시험대·`PlaytestSpawner`). → [PITFALLS §19](Docs/Environment/PITFALLS.md)

## 5. 문서 트리

```text
HANDOFF.md                      ★ 진입점 (이 파일)
AGENTS.md / CLAUDE.md / GEMINI.md / .github/copilot-instructions.md / .cursor/rules/
                                → 각 AI 도구가 자동으로 읽는 파일. 전부 "HANDOFF.md 먼저"로 안내
Docs/
├─ README.md                    문서 지도(이 트리의 상세판)
├─ Project/                     무엇을·왜
│  ├─ OVERVIEW.md               제품, 팀과 역할, 일정, 브랜치
│  ├─ REQUIREMENTS.md           ★ 사용자 요구사항 전체 이력 (요청 → 결과 → 커밋)
│  ├─ DECISIONS.md              확정된 결정과 이유 (바꾸기 전에 읽는다)
│  ├─ ROADMAP.md                남은 일, 우선순위, 미구현 목록
│  └─ HISTORY.md                커밋별 변경 이력
├─ Environment/                 어떻게 작업하나
│  ├─ SETUP.md                  Unity, 패키지, Steam, Git LFS, 병합 도구, 빌드
│  ├─ PITFALLS.md               ★ 실제로 겪은 사고와 재발 방지
│  └─ AI_WORKFLOW.md            AI 작업 규칙, 테스트 실행, 생성기, 문서 갱신 의무
├─ Architecture/                전체 구조
│  ├─ STRUCTURE.md              폴더, 어셈블리, 의존 방향, define
│  ├─ SCENES.md                 씬 구성과 흐름, NetworkRuntime
│  └─ UI.md                     HUD(UI Toolkit), 폰트, 클릭 대체 경로, 새 로비 배치, 친구 카드, 버튼 표
├─ Network/                     멀티플레이
│  ├─ README.md                 네트워크 개요와 사람용 테스트 방법
│  ├─ SESSION.md                파티, 매칭, 예약, 로비 데이터, 취소
│  ├─ MOTION.md                 이동 동기화, 패킷, 연결 품질
│  ├─ BOTS.md                   AI 봇
│  ├─ PLAN_V0.1_STATUS.md       승규 기획안 v0.1 항목별 반영 상태, 승규 다음 작업
│  └─ VALIDATION.md             ★ 실제 확인 기록과 확인 목록
├─ Player/                      플레이어 캐릭터
│  ├─ MOVEMENT_INPUT.md         입력 경로, 네트워크 이동, 오프라인 캐릭터
│  └─ RAGDOLL.md                래그돌 병합 상태, RagdollTest, 네트워크 안전성, 어댑터, 멀티 규칙
├─ RagdollLab/README.md         준영 님 랩 문서 (조작, 구조, 측정 근거)
├─ GameModes/README.md          게임 모드 목록(킹 러시·퀸 오브 더 힐·소드 파이트), 모드별 매칭, 새 모드 추가법
│  └─ QueenOfTheHill/           ★ 퀸 오브 더 힐: 인수인계(README), 기획(DESIGN), 새 기능 개발 목록(MECHANICS_TODO), 아트 후보(ART_CONCEPTS), 이미지 프롬프트, N4 참고 원문
├─ KingRush/                    첫 미니게임
│  ├─ README.md                 맵 구성, 코스 컴포넌트, 네트워크 한계
│  ├─ OBSTACLES.md              장애물 규칙과 만드는 법
│  └─ OBSTACLE_TEMPLATE.md      장애물 기획서 양식
├─ TEAM_GUIDE_KO.md             팀원용 한 권 안내서 (역할별 장, AI 시작 프롬프트)
└─ AI/                          옛 경로 (새 문서로 안내만 함)
Tools/
├─ run-tests-linux.sh           Unity 없는 환경의 테스트 + 컴파일 검사
├─ Test-NetworkCore.ps1 / Test-NetworkCompile.ps1   Windows(Unity 설치 PC)용 같은 검사
└─ Generators/                  씬·프리팹 YAML 생성기, .meta 생성기
```

## 6. 어디를 읽을까 — 작업 분야별 안내

| 작업 | 먼저 읽을 것 | 그다음 코드 |
|---|---|---|
| 무엇이든 처음 | 이 파일 → [REQUIREMENTS](Docs/Project/REQUIREMENTS.md) → [DECISIONS](Docs/Project/DECISIONS.md) → [PITFALLS](Docs/Environment/PITFALLS.md) | — |
| Unity 오류, Safe Mode, 클릭 안 됨 | [PITFALLS](Docs/Environment/PITFALLS.md), [SETUP](Docs/Environment/SETUP.md) | `Scripts/Editor/`, asmdef |
| 파티, 매칭, 초대 | [Network/SESSION](Docs/Network/SESSION.md) | `Network/SteamSession.cs`, `Core/TeamReservations.cs` |
| 이동 동기화, 지연, 끊김 | [Network/MOTION](Docs/Network/MOTION.md) | `Network/SteamMotion.cs`, `Core/MotionProtocol.cs`, `Core/LinkQuality.cs` |
| 봇 | [Network/BOTS](Docs/Network/BOTS.md) | `Core/BotIdentity.cs`, `Core/BotBrain.cs` |
| 씬 추가, 씬 전환 | [Architecture/SCENES](Docs/Architecture/SCENES.md) | `Bootstrap/NetworkRuntime.cs`, `Game/SceneNames.cs` |
| 게임 모드 추가, 모드별 매칭 | [GameModes](Docs/GameModes/README.md), [Network/SESSION](Docs/Network/SESSION.md) | `Core/GameModes.cs`, `Network/SteamSession.cs`, `Bootstrap/MatchSceneView.cs` |
| **퀸 오브 더 힐** | [GameModes/QueenOfTheHill](Docs/GameModes/QueenOfTheHill/README.md) → [RagdollLab README](Docs/RagdollLab/README.md) "등반" | 위 문서 §6 파일 지도 |
| HUD, UI, 로비 | [Architecture/UI](Docs/Architecture/UI.md) | `Game/NetworkHudView.cs`, `Game/LobbyStage.cs`, `Bootstrap/LobbyBootstrap.cs`, `Resources/*.uxml/uss` |
| 플레이어 입력, 조작 | [Player/MOVEMENT_INPUT](Docs/Player/MOVEMENT_INPUT.md) | `Game/MoveInputSource.cs`, `Input/` |
| 래그돌 | [Player/RAGDOLL](Docs/Player/RAGDOLL.md), [RagdollLab/README](Docs/RagdollLab/README.md) | `Assets/ChessFight/RagdollLab/Scripts/`, `Gameplay/Characters/ICharacterDriver.cs` |
| 킹러시 맵, 장애물 | [KingRush](Docs/KingRush/README.md), [OBSTACLES](Docs/KingRush/OBSTACLES.md) | `Gameplay/Obstacles`, `Gameplay/Course` |
| 테스트, 검증 | [AI_WORKFLOW §3](Docs/Environment/AI_WORKFLOW.md), [VALIDATION](Docs/Network/VALIDATION.md) | `Tests/Network`, `Tools/` |

## 7. 사용자와 일하는 방식

- **대답은 한국어로 한다.** 코드 주석과 커밋 메시지는 영어다(기존 관례).
- 사용자는 메인 기획자다. 개념은 **쉽게, 표와 단계로** 설명한다. 코드 용어는 괄호로 보충한다.
- "구현해줘"는 **구현 → 테스트 → 작업 브랜치에 커밋·푸시**까지다(로비·게임모드는 `JY-lobby`, 그 밖은 `Network`. §1). **PR은 요청할 때만** 만든다.
- 확인·검토 요청("체크해줘", "어떻게 생각해")에는 먼저 평가만 하고, 구현 여부를 묻는다.
- 사용자는 Windows에서 Unity Hub로 `C:\Users\dua07\GitHub\ChessFighter`를 연다. 결과는 스크린샷이나 증상으로 알려준다. AI는 Unity를 직접 실행할 수 없는 경우가 많으므로 **Unity에서 확인할 항목을 명확히 적어 준다.**
- 사용자가 싫어한 것:
  - 클릭 안 되는 UI
  - Safe Mode
  - 저절로 생기는 변경점
  - 읽기 힘든 중첩 폴더
  - 추측으로 "된다"고 말하기
- 로비·게임모드 브랜치(`JY-lobby`)에서는 **캐릭터 조작·물리(래그돌)를 바꾸지 않는다**(R18). 요청자는 초보라서 설명은 쉽게, 코드 주석은 영어로, Unity 확인은 순서로 적는다.
- 팀원 이름: 준영(래그돌), 진호·지성(킹러시 맵·장애물), 승규(네트워크 기획). 팀원은 ChatGPT(아스트라) 등 다른 AI를 쓴다. 그래서 이 문서 체계가 도구에 상관없이 읽혀야 한다.

## 8. 작업 종료 체크리스트

AI든 사람이든 작업을 끝낼 때:

1. 테스트: `Tools/run-tests-linux.sh --compile`(Linux) 또는 `Tools/Test-NetworkCore.ps1`(Windows). 결과 수를 기록한다.
2. **[REQUIREMENTS.md](Docs/Project/REQUIREMENTS.md)에 요청 한 줄을 추가한다**(날짜, 요청 요약, 결과, 커밋, 상태).
3. 결정이 새로 생겼으면 [DECISIONS.md](Docs/Project/DECISIONS.md)에, 새 함정을 겪었으면 [PITFALLS.md](Docs/Environment/PITFALLS.md)에 추가한다.
4. 바꾼 분야의 상세 문서(Network/…, KingRush/… 등)를 코드와 맞춘다.
5. Unity에서 확인해야 할 것이 생겼으면 [VALIDATION.md](Docs/Network/VALIDATION.md) 최상단에 '미확인' 표로 추가한다.
6. **이 파일의 §1 스냅샷, §2 표, §3 다음 할 일을 갱신하고 "최종 갱신" 날짜를 바꾼다.**
7. [HISTORY.md](Docs/Project/HISTORY.md)에 커밋을 한 줄 추가하고 작업 브랜치(§1: 로비·게임모드는 `JY-lobby`, 퀸 오브 더 힐 래그돌 기능은 `JY-ragdoll_v2`, 그 밖은 `Network`)에 커밋·푸시한다.

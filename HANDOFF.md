# ChessFight HANDOFF — 모든 작업은 여기서 시작한다

**2026-11-09 · 빈티지 기억 타자기:** 검은 본체·원형 황동 키·롤러·종이·내부 통로로 개편. 진행로의 번호 힌트5개를 기억해 각 단의 정답 키를 밟는다. 시작/F3 무작위 글자·안전 경로 갱신. 키 눌림/직접 점프 반동, 거인 코스 진입 바닥 중복 제거. 사용자 씬/타일 설정 보존. Unity 플레이 확인 대기. [상세](Docs/PawnRush/WAR_ROOM_COMPLETE.md). Gameplay 자동50검사 통과.

**2026-11-08 · Fork 타자기 계단:** 병력 대열→문서 CHECK를 읽는 20키/5단 타자기 계단 교체. 안전 글자만 밟기, 오답 뒤/위 타격·빨간 반응·발 접지/쿨다운. 실제 프리팹/생성기 갱신, 사용자 수치 보존. 자동49·런타임9개 컴파일, Unity 대기. [설정](Docs/PawnRush/WAR_ROOM_COMPLETE.md).


**2026-11-08 · 지도 수평 발판 보정:** 지도 수평 전개 수정: 조각 완성 후 표시하던 방식을 연속 확장으로 변경. 목표Y에서 수평 발판이 롤러까지 즉시 이어지고 렌더러/충돌 범위 일치. 양쪽100단계 경계 검사·런타임 컴파일 통과, Unity 확인 대기.


**2026-11-08 · Fork 작전실 동작:** 도장 상승2.5초/급강하0.18초, 지도 측면 위→가파른 대각 하강→수평 펼침 구현. 기존 씬 수치 유지. 자동47·런타임9개 컴파일, Unity 확인 대기. [설정](Docs/PawnRush/WAR_ROOM_COMPLETE.md).


**2026-11-08 · GM 기보 40개 적용:** 씬 시작/F3 무작위 선택(직전 제외), 액자/표시 동기화, 3,764반수 대조·선택 10,000회·Gameplay45·런타임9개 컴파일 통과. Unity 확인 대기. [경기 목록](Docs/PawnRush/GIANT_CHESS_GAME_LIST.md).

**2026-11-08 · Fork 병합 복구 완료:** 28개 충돌 파일과 추가 클래스/API 연결 복구. [변경·검사·Unity 확인 절차](Docs/Environment/UPSTREAM_MERGE_FIX_20261108.md).


이 파일 전체를 읽고 §6에서 현재 작업에 필요한 상세 문서만 읽는다.
현재 지침과 사용자의 최신 요청이 과거 기록보다 우선한다.
작업이 끝나면 §8에 따라 코드·문서·검사 결과를 함께 마감한다.

최종 갱신: **2026-11-08 · R506 봇은 움직이지 않게 하기로 결정(DECISIONS K14) · R505 네트워크 고침 R502~R504를 upstream main에 커밋 · 푸시, 네트워크 안내 · 월요일 시험 문서**.
다음 빈 요청 번호는 **R507**이다(R498~R501은 seoungtest `f33c51c9`가 씀).
R506: 승규 님 결정 "봇은 실제로 움직이게 하지 않을 거야"를 DECISIONS K14 · BOTS에 적었다. 폰 러쉬 봇은 출발점에 서 있고 소드 파이트 봇은 더미로 남는다. 봇 행동은 할 일이 아니며, 12명 시험의 부하 · 느낌은 사람이나 `-autopilot` PC로 본다. 안내 문서의 봇 설명도 고쳤다. 승규 님 "응, 올려 줘"로 upstream main에 커밋 · 푸시(`git log --grep=R506`).
R505: 승규 님 요청으로 R502~R504(네트워크 검토 고침 18가지, 프로토콜 v27)를 upstream main에 커밋 · 푸시했다(`git log --grep=R504`, main은 그사이 바뀌지 않아 `f296ace1` 위에 그대로 이어 붙임). 네트워크를 모르는 사람도 읽을 수 있는 구조 · 기능 · 한계 · 월요일(11-09) 팀원 시험 문서를 Claude 문서로 만들었다([네트워크 안내와 월요일 시험](https://claude.ai/artifact/Btut2SApfso4Gfkm7aNi1N)). 승규 님 Unity(`C:\Work\ChessFight`)는 아직 예전 main(`5f0c9832`)이라 새 main을 받아야 보인다. 월요일 시험에는 새 main으로 v27 개발 빌드를 새로 뽑아 모든 PC에 같은 것을 깔아야 한다(지금 있는 R498 · R306 · R297 빌드는 모두 v26 이하). 문서를 쓰다 찾은 VALIDATION R504 ⑭(실제 소드 파이트 경기에는 F12 지연 시뮬레이터가 없어 휴대폰 핫스팟 시험으로 바꿈)와 R297 메뉴 경로를 고쳤다. 아직 코드와 어긋난 문서: README 프로토콜 "v2", HOST_AUTHORITY §1.3 방장 넘김 1.15배(지금 1.0) · §8.4 3명 초당 30번(지금 60번) · §1.1 입력 40바이트(지금 42 · 46), SESSION §3 이른 시작이 폰 러쉬만인 듯 적힘(소드 파이트도 됨) · §8 "5 대 5는 비공개 방만"(R148로 낡음).
R504: 같은 검토의 작은 문제 아홉을 R503 위에 이어 고쳤다. 다음 방장 차례인 PC도 멈추면 그다음 순번이 3초 뒤 건너뛰고 이어받기(member data `past`), 방장 교체 뒤 소드 파이트 남은 시간 · 폰 러쉬 장애물이 뒤로 가던 것, 방 주인 이전 중 경기 끝 기록이 사라지던 것, 소드 파이트 경기 글 순서, 내 파티 초대를 받으면 파티장을 잃던 것, 개발 빌드의 기물 요청 되풀이, 명단 할당, 로컬 시험 자리 번호다. 프로토콜은 v27 그대로(R503과 함께 아직 빌드한 적 없음). R505에서 upstream main에 커밋 · 푸시. Core165 · 모의 세션65 · 전체 외부 컴파일 통과, Unity · Steam · 여러 PC 확인 전. [VALIDATION R504](Docs/Network/VALIDATION.md) · [HOST R504](Docs/Network/HOST.md).
R503: 같은 검토의 중간 문제 여섯을 R502 위에 이어 고쳤다. 대기실을 막 넘겨받은 방장의 이른 시작 · 봇, 다음 방장 PC 혼자 끊겼을 때의 잘못된 이어받기(member data `quiet`, **프로토콜 v26 → v27**), 순간이동 · 부활 때 모두의 재생 지연이 사라지던 것, 방 번호 참가 거절 뒤 원하지 않은 공개 검색, 대기실이 닫히면 다른 파티가 매칭을 그만두던 것, 잠깐의 Steam 로그오프에 바로 취소하던 것이다. R505에서 upstream main에 커밋 · 푸시. Core163 · 모의 세션62 · 전체 외부 컴파일 통과, Unity · Steam · 여러 PC 확인 전. [VALIDATION R503](Docs/Network/VALIDATION.md) · [SESSION §6](Docs/Network/SESSION.md).
R502: 승규 님의 네트워크 검토 요청에서 나온 큰 문제 셋을 고쳤다. 로딩 중 방장이 바뀌면 경기가 시작하지 않던 것, 소드 파이트 방장 교체 뒤 부활하지 못하던 것, 폰 러쉬 방장 교체 뒤 체크포인트 · 물 부활이 사라지던 것이다. `C:\Work\ChessFight-R305`의 `r502-migration-fixes`(upstream main `f296ace1` 기준)에서 고쳐 R505에서 upstream main에 커밋 · 푸시. Core163 · 모의 세션57 · 전체 외부 컴파일 통과, Unity · Steam · 여러 PC 확인 전. [VALIDATION R502](Docs/Network/VALIDATION.md) · [HOST R502](Docs/Network/HOST.md).

R498: 사용자는 팀원 맵 시험 중 `Particle Orbital Velocity curves must all be in the same mode` 오류를 제보했다.
작전 회의실 공기 대포의 런타임 공전 XYZ 모드를 맞췄고, 실제 Unity 자동 검사에서 수정 전 오류30건·수정 후0건을 기록했다.
기존 루트의 SeoungTest_PawnRush Play에서 작전 회의실 화면과 Console 오류0을 AI가 관찰했고, 사용자 직접 입력·연출 적합성 확인은 남아 있다.

R496까지의 원본 HANDOFF 전체는 [이전 인계 기록](Docs/Archive/HANDOFF_BEFORE_R497.md)에 보존했다.
당시의 기획·결정·튜닝 근거가 필요할 때만 그 기록을 읽는다.
정리 범위·경로·예외·복구 방법은 [R497 정리 안내](Docs/Architecture/PROJECT_CLEANUP_R497.md)를 따른다.

> **Fork 병합 복구 (2026-11-08):** 충돌 표식과 잘못 병합된 체스홀 프리팹을 복구한다. 기존 작전실·체스 타이머·하중 장애물 변경은 [Fork 인계 기록](Docs/Archive/FORK_HANDOFF_BEFORE_MERGE_FIX.md)을 함께 참조한다. 이 클라우드 작업은 현재 `/workspace` 체크아웃에서 진행하며 커밋 메시지는 사용자 요청대로 한국어다.

## 1. 현재 상태 스냅샷

| 항목 | 현재 상태 |
|---|---|
| 작업 위치 | **`C:\Users\dua07\GitHub\ChessFighter`의 `main`에서 직접 작업**. 사용자가 새로 요청하기 전에는 추가 워크트리를 만들거나 사용하지 않는다(T13, R496) |
| Unity | 6000.3.11f1, Windows, URP 17.3, 레거시 Input Manager(Old) |
| 개발 중인 모드 | 폰 러시·소드 파이트 두 개. Queen of the Hill은 R497에 폐기·등록/전용 맵/스킬/시험 자원 제거 |
| 폰 러시 현재 맵 | 구간 프리팹 조합: 정원·대계단 홀·미니게임10종·작전 회의실. 슬롯5 후반 장애물은 아직 없음 |
| 폰 러시 온라인 경기 | 기존 Course01을 Prototype에 보관하되 온라인 호환 씬으로 유지. 새 구간 조합 코스의 온라인 연결은 아직 없음 |
| 구형 맵 | `Assets/Prototype/PawnRush`: Course01 씬, 옛 맵 코드41개·기존 Opening 프리팹24개. GUID·기존 어셈블리 소속 유지 |
| 기타 프로토타입 | 체스복싱은 `Assets/Prototype/ChessBoxing`, 기물 미리보기는 `Assets/Prototype/Previews` |
| 공용 물리·기물 | 래그돌·Hook/Rope·승격·폰 러시/소드 스킬 유지. 옛 Queen 시험대는 PhysicsMechanicsBed, 개척 규칙은 PioneerCourse/PioneerRules로 재분류 |
| 공용 스킬 자원 | `Resources/SharedSkills/Fx` 셰이더5개·`Sfx` 음원3개는 GUID 유지. 실제 사용되는 공유 자원만 남김 |
| Unity 메뉴 | ChessFight 아래 **Pawn Rush / Sword Fight / Prototype / Development** 네 그룹. 상세 개발 도구는 Development로 |
| 공기 대포 VFX | R498: `LibraryAirCannonFx` 공전 XYZ를 모두 TwoConstants(X/Y=0~0, Z=2.5~5)로 설정한 뒤 모듈 활성화. Local 공간·기존 속도 유지, 자산 재굽기 없음 |
| 현재 Unity 편집 상태 | R498 AI가 기존 루트 `SeoungTest_PawnRush` Play에서 캐릭터·작전 회의실·컨베이어·대포를 약25초 관찰한 뒤 정지. 같은 씬 편집 상태로 사용자 시험 가능, Console 빨간 오류0 |
| 문서 | `Docs/PawnRush`가 현재 자료. 옛 맵 문서9개는 `Docs/PawnRush/Prototype`, 폐기 모드 자료는 `Docs/Archive/QueenOfTheHill` |
| 브랜치 | 2026-10-06 22:09:42 KST 이전 마지막 커밋 기준 원격8개·로컬8개 삭제. 원격24→16, 로컬15→7. main·최근 브랜치 보존 |
| 자동 검사 | R498 Core162·모의 세션55·전체 대상 외부 컴파일 통과. 대포 Native Play 3방향·독립 그룹 대조군 PASS, issues0. R497 저장10프리팹/시험 씬·Play10종 검사 기록은 별도 유지 |
| 사용자 확인 | R498 대포 충전·발사 화면/오류 재발과 R497 이후 Unity 입력·온라인·실제 퍼즐 완주·12명 성능은 사용자 확인 전 |
| 네트워크 방장 교체 (R502 · R503 · R504) | R502: 시작 시각 `go`는 `SteamSession`이 방장일 때 정함(로딩 화면은 기다리기만). 소드 파이트 새 방장은 가장 새 칼 프레임 + `SwordMatchRules.Adopt`로 규칙이 몸을 따름. 폰 러쉬는 모든 PC가 체크포인트 기록, 새 방장이 0.3초 뒤 부활 다시 예약. R503: 대기실 새 방장은 방 주인 + `resv`를 읽은 뒤에만 시작 · 봇, 다음 방장은 다른 사람도 방장을 못 들을 때만(`quiet`) 이어받음, 순간이동한 말만 그대로 그림, 번호 참가 거절은 로비로, 닫힌 대기실은 다시 찾기, Steam 로그오프 10초 기다림. R504: 다음 방장이 멈추면 그다음이 3초 뒤 `past`를 적고 건너뜀(한 칸만), 소드 파이트 화면 시계는 마지막 글 뒤 1초까지 · 새 방장은 화면 시간에서, 폰 러쉬 장애물 시계는 지우지 않음(`Hold`), `EndMatch`는 방 주인이 될 때까지 들고 있음, 경기 글 순서, 내 파티 초대 무시, 개발 빌드 기물 요청 되풀이 막음, 명단 `Dictionary`, 로컬 시험 빈자리. **프로토콜 v27**(v26 빌드와 섞이지 않음). R505에서 upstream main에 커밋 · 푸시(`git log --grep=R504`), 사용자 확인 전 |

브랜치 삭제 전16개 참조를 bundle·manifest·복구 안내로 보관하고 bundle 검사를 통과했다.
보관 위치: `C:\Users\dua07\Desktop\개발일지\2학기 프로젝트수업\체스파이트\Backups\R497-Branches-20261106-221133`.
약7.9 MB의 Git bundle이며 LFS 바이너리를 별도 포함하지 않는다. 기존 LFS 캐시는 보존했다.
R496의 사용자 프리팹2개 보존본·명명 stash·당시 옛 브랜치 기록도 정리 작업이 임의로 버리지 않는다.

## 2. 무엇이 되고 무엇이 안 되는가

- **현재 편집할 폰 러시**: `ChessFight → Pawn Rush → Open Section Test`.
- **미니게임만 선택**: `ChessFight → Pawn Rush → Open Mini-game Prefab Test`; 숫자1~0, F5 팀, F8 초기화.
- C/D/A는 초반, B/E/F/J는 중반, G/H/I는 후반 후보로 슬롯2/4/6에3/4/3개 등록돼 있다.
- 슬롯1 정원 → 슬롯2 초반 미니게임 → 슬롯3 홀 → 슬롯4 중반 미니게임 → 슬롯6 후반 미니게임 → 슬롯7 작전 회의실로 이어진다.
- 미니게임의 건축·장식은 저장된 프리팹이다. Play에는 Game socket의 기능 게임만 다시 설치한다.
- 자동 완료(F6/ForceComplete)는 이벤트·팀 문 연결 검사다. 실제 장치를 풀어서 달성했다는 뜻이 아니다.
- 정원·홀·작전 회의실의 일부 아트는 Play에 만들어진다. 편집 모드의 빈 루트만 보고 자산 누락으로 판단하지 않는다.
- **Prototype Course01**은 기존 온라인 경기가 호출하는 씬이다. 씬 이름 `PawnRush_Course01`, 로비 키 `kingrush`, 기존 통신 구조는 유지한다.
- `GameModes.KingRush` 이름과 `ModeRetiredQueenHill = 1` 같은 예약 통신 값은 파일 정리 때문에 바꾸지 않는다.
- Queen of the Hill을 새 모드로 다시 등록하지 않는다. Queen 기물 자체는 다른 모드가 쓰므로 유지한다.
- 새 Sections 코스를 기존 Course01 온라인 흐름에 연결했다고 말하지 않는다. 기존 Steam 흐름의 변경도 이번 범위가 아니다.
- R497 실제 Unity 저장10프리팹/시험 씬 실패0, Play10종의 기능 캐시·자기 팀 문·2번 초기화·씨앗 재생성·A 인스턴스 분리·I 외형 유지 자동 검사 통과. [VALIDATION](Docs/Network/VALIDATION.md)에 범위와 한계를 기록했다.
- R498 실제 대포 Build·Charge·Fire·입자 시뮬레이션을 3방향에서 자동 검사했다. 수정 전 Constant/Constant/TwoConstants로 원문 오류30건, 수정 후 XYZ TwoConstants로 issues0. 저장 자산145개/입자계22개의 곡선 그룹 불일치0이며 씬·프리팹을 다시 저장하지 않았다. 자세한 범위·로그는 [VALIDATION](Docs/Network/VALIDATION.md) 최상단을 따른다.
- R498 기존 루트의 SeoungTest_PawnRush Play 화면을 AI가 약25초 관찰했다. Console 빨간 오류0, 새 GUI 로그의 원문 오류·errorCS·runtime Exception header0. 기존 그림자 atlas/InputManager 경고는 유지하며 사용자 입력·맵 완주·연출 적합성 확인은 아니다.
- AI의 컴파일·자동 검사·렌더 관찰과 사용자가 Unity/Steam에서 직접 보고 조작한 확인을 구분한다.

## 3. 진행 중인 일과 다음 할 일

1. 기존 프로젝트의 Open Section Test에서 작전 회의실 대포 충전·발사를 직접 보고 원문 오류가 재발하지 않는지 확인한다. R498 자동 검사는 사용자 시각·입력·Steam 확인을 대신하지 않는다.
2. 자동 검사와 별개로 새 메뉴·로비 두 모드·현재/Prototype 장면을 Unity GUI에서 살펴보고 [확인 목록](Docs/Network/VALIDATION.md)에 사용자 결과를 기록한다.
3. 사용자는 기존 프로젝트에서 네 메뉴 그룹, 미니게임 선택·팀 전환·초기화, Intro→Lobby→두 모드를 확인한다.
4. 폰 러시 각 퍼즐의 실제 입력·문 통과·복귀 동선과12명 성능을 확인한다.
5. 구형 맵을 고칠 때는 Prototype 문서를 먼저 읽는다. 현재 구간 코스와 구형 온라인 코스를 같은 것으로 취급하지 않는다.
6. 새 구간 코스의 온라인 연결은 별도 설계·요청으로 진행한다. 이번 정리로 프로토콜을 새로 정의하지 않았다.
7. 다음 새 요청은 **R499**부터 기록한다.

다른 채팅의 진행 상태와 과거 제한은 원본 아카이브의 날짜·브랜치를 확인해 해석한다.
현재 사용자 작업 위치(main/기존 경로)와 최신 사용자 지시가 우선한다.

## 4. 절대 규칙 — 어기면 과거에 실제로 사고가 났던 것들

1. **Active Input Handling은 `Input Manager (Old)`(0)로 둔다. Input System 패키지는 설치하지 않는다**(`JY-lobby`, R20: 설치돼 있으면 켤 때마다 백엔드를 켜라는 창이 뜬다). uGUI가 없어서 Both/New로 바꾸면 HUD 클릭과 입력이 전부 죽는다. `InputSettingsGuard`가 자동으로 되돌린다. → [PITFALLS §1](Docs/Environment/PITFALLS.md)
2. **프로젝트는 선택 패키지가 하나도 없어도 컴파일되어야 한다.** Safe Mode에서는 설치 스크립트가 돌지 않는다. 선택 패키지 코드는 전용 asmdef + `versionDefines` + `defineConstraints`로 가둔다. `ENABLE_INPUT_SYSTEM`을 패키지 설치 여부로 쓰지 않는다.
3. **`Packages/manifest.json`·`packages-lock.json`이 바뀌면 Discard하지 말고 커밋한다.**
4. **폴더 하나 = 어셈블리 하나.** `Game`·`Gameplay`는 Steam을 참조하지 않는다. `Core`는 Unity도 참조하지 않는다. → [Architecture/STRUCTURE](Docs/Architecture/STRUCTURE.md)
5. **온라인 씬 흐름의 Steam 초기화·종료 소유자는 `NetworkRuntime` 하나다.** SteamManager를 따로 추가하지 않는다. R114 사용자 요청의 체스타이머 단독 Play는 기존 래그돌 테스트 씬처럼 독립 세션을 소유하되, `NetworkRuntime`이 있으면 접속기를 붙이지 않아 중복 초기화를 금지한다.
6. **HUD에는 기본 테마가 없다.** ScrollView·Dropdown 같은 복합 컨트롤은 보이지 않는다. 평범한 VisualElement만 쓴다.
7. **장애물 위치·회전은 `ObstacleClock` 시간의 순수 함수로만 계산한다(스스로 움직이는 kinematic 물체는 `KinematicObstacle : Obstacle`을 상속(R70, 이식 장치는 `ObstacleMotion : Obstacle`), 테스트 스크립트가 검사. 플레이어 상태로 움직이는 것은 `StateDrivenMover:` 주석을 달고 오프라인 전용).** 프레임마다 누적하지 않는다. 온라인 래그돌 참가자는 방장이 스냅샷에 실은 장애물 시각을 쓴다(R51).
8. **클라이언트는 입력만 보낸다.** 좌표를 권위로 받지 않는다. 파티는 같은 팀에 통째로 예약한다.
9. **네트워크 비호환 변경을 하면 `SteamSession.Protocol`을 올린다.** 패킷 구조를 바꾸면 `MotionProtocol` magic도 올린다.
10. **검증 표기는 정직하게 한다.** 사용자가 Unity나 Steam에서 직접 본 것만 VALIDATION에 '성공'으로 적는다. 코드 작성과 테스트 통과는 검증이 아니다.
11. **`.meta`는 항상 같이 커밋한다.** Unity 밖에서 파일을 만들면 `Tools/Generators/mkmeta.py`로 만든다.
12. **같은 `.unity` 씬을 두 사람이 동시에 고치지 않는다.** 맵은 구간 프리팹으로 나눈다.
13. **네트워크·게임 코드(`Assets/Scripts`의 Core·Network·Game·Gameplay·Bootstrap·Input·Editor)는 래그돌 타입을 참조하지 않고, 래그돌(`Assets/Scripts/RagdollLab`, 10-08 폴더 정리 전 `Assets/ChessFight/RagdollLab`)은 Steam을 참조하지 않는다.** 랩을 Steam에 잇는 코드는 `Assets/Scripts/RagdollLabSteam/`(Bootstrap과 같은 다리)에만 둔다. 캐릭터는 `ICharacterDriver`로만 부른다. `Tools/run-tests-linux.sh`의 경계 검사가 확인한다.
14. **물리 콜백(`OnCollision*`·`OnTrigger*`) 안에서 순간이동하거나 관절을 즉시 지우지 않는다.** Unity가 `DestroyImmediate`를 거부해 잡기 관절이 남는다. 알리기만 하고 다음 Update에서 처리한다(`WaterZone` → 시험대·`PlaytestSpawner`). → [PITFALLS §19](Docs/Environment/PITFALLS.md)
15. **(`claude/host-migration`부터) 경기 상태를 방장 PC에만 두지 않는다.** 방장은 경기 중에 바뀔 수 있다([HOST](Docs/Network/HOST.md)). 점수·타이머·모드 상태는 스냅샷이나 로비 데이터에 실어 모두가 갖고 있게 한다. 방장만 아는 값은 이전 때 사라진다.

## 5. 문서 트리

```text
HANDOFF.md                           현재 상태·규칙·작업 경로
Docs/
├─ Project/                          REQUIREMENTS · DECISIONS · HISTORY
├─ Architecture/                     STRUCTURE · SCENES · UI · PROJECT_CLEANUP_R497
├─ Environment/                      SETUP · AI_WORKFLOW · PITFALLS
├─ Network/                          SESSION · MOTION · HOST · HOST_AUTHORITY · VALIDATION
├─ Player/                           MOVEMENT_INPUT · RAGDOLL
├─ RagdollLab/                       공용 물리 시험·어댑터 설명
├─ Skills/                           폰 러시·소드 파이트 스킬
├─ VFX/                              공용 효과·작업대
├─ PawnRush/                         현재 폰 러시 맵·구간·미니게임 자료
│  ├─ PAWN_RUSH_SECTIONS.md           구간 목록·연결·시험
│  ├─ PAWN_RUSH_MINIGAME_SECTIONS.md  실제10종 프리팹·여는 법·수정 주의
│  ├─ PAWN_RUSH_MINIGAME_MODELING.md  건축·소품·피벗·이동 공간
│  ├─ Prototype/                     Course01·이전 Opening/Castle/Final 기획
│  └─ ConceptArt/                    모델링 이미지·검사 자료
├─ GameModes/SwordFight/              소드 파이트 기획·프로토타입·시연
└─ Archive/                          이전 HANDOFF 전체·폐기 Queen 모드 문서
Assets/
├─ Scripts/                          종류별 어셈블리·현재 코드
├─ Scenes/                           현재 온라인 흐름·시험 씬
├─ Prefabs/PawnRushSections/          현재 구간·미니게임 프리팹
├─ Prototype/                        구형 맵·체스복싱·기물 미리보기
├─ Resources/SharedSkills/            현재 모드가 쓰는 공용 효과·소리
└─ Art · Materials · Settings · StreamingAssets · VFX
ArtSource/                           Blender 원본·내보내기 스크립트
Tools/                               생성기·검사·AI 연결 등 개발 도구
```

Prototype의41개 C# 파일은7개 asmref로 기존 어셈블리에 속한다.
폴더를 옮겼다는 이유로 같은 타입을 다른 어셈블리에 복제하거나 새 asmdef를 만들지 않는다.
과거 문서는 [전체 아카이브](Docs/Archive/HANDOFF_BEFORE_R497.md)에서 당시 경로와 함께 찾는다.

## 6. 어디를 읽을까 — 작업 분야별 안내

| 작업 | 먼저 읽을 것 | 그다음 코드 |
|---|---|---|
| 무엇이든 처음 | 이 파일 → [REQUIREMENTS](Docs/Project/REQUIREMENTS.md)·[DECISIONS](Docs/Project/DECISIONS.md)·[PITFALLS](Docs/Environment/PITFALLS.md) | 현재 main의 관련 파일만 |
| 파일·메뉴·Prototype·브랜치 정리 | [R497 정리 안내](Docs/Architecture/PROJECT_CLEANUP_R497.md) → [STRUCTURE](Docs/Architecture/STRUCTURE.md) | 경로 문자열·asmref·메타·생성기·검사 스크립트 |
| 현재 폰 러시 맵 | [PawnRush README](Docs/PawnRush/README.md) → [구간](Docs/PawnRush/PAWN_RUSH_SECTIONS.md) | PawnRushRound·PawnRushSection·SectionPrefabMenu·구간 프리팹 |
| 미니게임10종 | [실제 프리팹](Docs/PawnRush/PAWN_RUSH_MINIGAME_SECTIONS.md) → [기존 규칙](Docs/PawnRush/PAWN_RUSH_MINIGAMES_v1.md)·[모델링](Docs/PawnRush/PAWN_RUSH_MINIGAME_MODELING.md) | MiniGameSection*·MiniGamePalaceArt*·MissionStation·Mg*·PaintBoard |
| 정원·홀·작전 회의실 | [정원](Docs/PawnRush/PAWN_RUSH_GARDEN.md)·[홀 아트](Docs/PawnRush/PAWN_RUSH_GRAND_STAIR_HALL_ART.md)·[구간](Docs/PawnRush/PAWN_RUSH_SECTIONS.md) | RoyalWaterGardenBuilder*·GrandStairHall*·WarRoomSection·Course01v4Builder.* |
| 구형 맵·기존 폰 러시 온라인 | [Prototype Course01](Docs/PawnRush/Prototype/PAWN_RUSH_COURSE01.md) → [Prototype 기획](Docs/PawnRush/Prototype/DESIGN.md) | Assets/Prototype/PawnRush·SteamPawnRushLink·PawnRushNetState |
| 소드 파이트 | [PROTOTYPE](Docs/GameModes/SwordFight/PROTOTYPE.md) → [명세](Docs/GameModes/SwordFight/PROTOTYPE_SPEC_v1.0.md)·[SKILLS_ONLINE](Docs/Network/SKILLS_ONLINE.md) | SwordFightGame·SwordFightMatch*·SwordFightSkills*·LabNetLink.Sword |
| 공용 래그돌·입력·이동 | [RAGDOLL](Docs/Player/RAGDOLL.md) → [MOVEMENT_INPUT](Docs/Player/MOVEMENT_INPUT.md)·[랩](Docs/RagdollLab/README.md) | RagdollPawn*·RagdollDriver·ICharacterDriver·PhysicsMechanicsBed |
| 스킬·효과 | [Skills](Docs/Skills/README.md)·[VFX](Docs/VFX/README.md) → [SKILLS_ONLINE](Docs/Network/SKILLS_ONLINE.md) | PawnRushSkills·SwordFightSkills·SkillKit·SharedSkills·VFX |
| 매칭·파티·음성 | [SESSION](Docs/Network/SESSION.md)·[게임 모드](Docs/GameModes/README.md) | SteamSession·GameModes·TeamReservations·SteamVoice |
| 이동 동기화·방장·지연 | [MOTION](Docs/Network/MOTION.md)·[HOST](Docs/Network/HOST.md)·[HOST_AUTHORITY](Docs/Network/HOST_AUTHORITY.md) | SteamMotion·RagdollLabSteam·HostElection·스냅샷 프로토콜 |
| 로비·HUD·씬 흐름 | [UI](Docs/Architecture/UI.md)·[SCENES](Docs/Architecture/SCENES.md) | Game·Bootstrap·SceneNames·EditorBuildSettings |
| Safe Mode·컴파일·클릭 오류 | [PITFALLS](Docs/Environment/PITFALLS.md)·[SETUP](Docs/Environment/SETUP.md) | 관련 asmdef·Editor·입력 경로 |
| 검사·빌드 | [AI_WORKFLOW](Docs/Environment/AI_WORKFLOW.md)·[VALIDATION](Docs/Network/VALIDATION.md) | Tools/Test-NetworkCore.ps1·Test-NetworkCompile.ps1·MiniGamePrefabValidation·AirCannonFxValidation(R498 대포 회귀 검사) |
| GPT·Claude 계정 연결 | [AI_WORKFLOW](Docs/Environment/AI_WORKFLOW.md) → [연결 사용법](Tools/AiBridge/README.md) | Tools/AiBridge |
| 폐기 모드의 과거 근거 | [아카이브](Docs/Archive/HANDOFF_BEFORE_R497.md)·[Queen 기록](Docs/Archive/QueenOfTheHill/README.md) | 현재 모드 등록이나 새 작업 지시로 재사용하지 않음 |

## 7. 사용자와 일하는 방식

- **한국어로 답한다.** 코드 주석은 영어, 커밋 메시지는 한국어다.
- 사용자는 메인 기획자이며 코드를 직접 쓰지 않는다. 결과·영향·여는 순서를 쉽게 설명한다.
- 작업은 **기존 `C:\Users\dua07\GitHub\ChessFighter`의 main에서 직접** 한다. 추가 워크트리는 새 명시 요청이 있을 때만 쓴다.
- 구현·정리 요청은 실제 변경·적절한 검사·main 커밋·푸시까지 완료한다. PR은 요청할 때만 만든다.
- 새 사용자 지시가 과거 브랜치·보존 범위·번호 규칙보다 우선한다. 과거 기록의 "커밋 금지"를 현재 전체 작업 제한으로 오해하지 않는다.
- 사용자 수정본은 덮어쓰기 전에 보존한다. 파일 백업·패치·stash와 현재 main에 적용한 결과를 구분한다.
- **사용자가 Unity나 Steam에서 직접 보고 조작한 것만 '검증됨/성공'이라고 기록한다.** AI 자동 검사·렌더·코드 관찰은 그 근거와 한계를 쓴다.
- UI 자동화가 사용자 입력·Escape로 중단되면 추가 화면 조작을 멈추고 완료된 범위와 남은 범위를 구분한다.
- 공유 캐릭터 물리·카메라·네트워크는 요청 범위 없이 바꾸지 않는다. 파일 정리로 게임 규칙이나 프로토콜을 바꾸지 않는다.
- 사용자가 싫어하는 것은 클릭 불가 UI, Safe Mode, 저절로 생기는 변경점, 읽기 힘든 중첩 폴더, 근거 없는 완료 주장이다.

## 8. 작업 종료 체크리스트

1. Windows는 `Tools/Test-NetworkCore.ps1`, 필요하면 `Tools/Test-NetworkCompile.ps1 -SteamRuntimeSources <Runtime 경로>`를 실행한다. Linux는 `Tools/run-tests-linux.sh --compile`을 따른다.
2. 요청은 [REQUIREMENTS](Docs/Project/REQUIREMENTS.md)에 날짜·내용·결과·커밋·상태로 기록한다.
3. 새 결정은 [DECISIONS](Docs/Project/DECISIONS.md), 실제 겪은 함정은 [PITFALLS](Docs/Environment/PITFALLS.md)에 남긴다.
4. 변경 분야의 상세 문서와 실제 코드·파일·메뉴·리소스 경로를 맞춘다.
5. 사용자 Unity·Steam 확인 항목은 [VALIDATION](Docs/Network/VALIDATION.md) 최상단에 미확인 표로 남긴다.
6. 이 파일의 최신 날짜·번호와 §1 상태·§2 범위·§3 다음 작업·§6 경로를 갱신한다. 과거 긴 이력은 아카이브에 둔다.
7. [HISTORY](Docs/Project/HISTORY.md)에 결과를 한 줄 기록하고 **main에 커밋·푸시**한다. 관련 없는 사용자 변경을 함께 stage하지 않는다.
8. 검사를 끄거나 경고를 숨겨 통과시키지 않는다. 자동 검사·사용자 확인·플레이어 빌드·Steam·성능을 각각 구분해 보고한다.

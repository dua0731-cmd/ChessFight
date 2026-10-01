# 변경 이력

커밋마다 한 줄. 무엇을 왜 바꿨는지는 [REQUIREMENTS](REQUIREMENTS.md)의 같은 번호를 본다.
`git log --oneline`과 같은 순서(최신이 위)다.

`JY-gpt_gamemode`·`JY-kingrush` 줄의 요청 번호(R44~)는 [REQUIREMENTS §1-b](REQUIREMENTS.md#1-b-jy-gpt_gamemodejy-kingrush-계열-준영-님-채팅-소드파이트킹-러쉬) 표, 나머지는 §1 표다.

| 커밋 | 날짜 | 요청 | 내용 |
|---|---|---|---|
| (이 커밋, `claude/last-scene`) | 10-01 | R62 | 문서: UI §10 결과 화면, SCENES(LastScene), STRUCTURE, DECISIONS U9(영상 대신 코드로 만든 3D 장면)·U10(목표 그림체 = PAWN RUSH 참고 그림), VALIDATION 결과 화면 표(12개), REQUIREMENTS R62·조건 2줄, ROADMAP(결과를 어디서 보여 줄지 결정 대기), HANDOFF. R61 "푸시 전" 표기를 "10-01 푸시 `e64012e`"로 |
| `c968a10` (`claude/last-scene`) | 10-01 | R62 | Unity에서 보고 고침: 진 팀 화면에 비가 없던 것(3.4초부터 비스듬한 파티클 비), 마지막 장면에서 왼쪽 끝 쓰러진 말이 잘리던 것(카메라를 더 뒤로, 줄 간격 좁힘), 에디터 창이 뒤에 있으면 시간이 멈추던 것(`runInBackground`) |
| `1e92fc0` (`claude/last-scene`) | 10-01 | R62 | 결과 화면 `LastScene.unity`(메뉴 Last Scene): PAWN RUSH 참고 그림체 무대(성·왕관 아치·체크 광장·거대 비숍과 룩)와 다리 달린 말을 코드로, 이긴 팀 세리머니·꽃가루 대포 / 진 팀 왕관 튕김·도미노, 결과판 HUD(팀 목표, 도착 순서와 필요 인원 금색·승리 확정 깃발, 미도착 남은 거리, H 숨기기, 12초 자동 로비, 다시 매칭·로비로). Core `MatchResult` + 테스트 2개(Core 81·세션 39·실제 DLL 컴파일 통과). 경기 흐름에는 아직 연결 안 함 |
| `e64012e` (`claude/pawnrush-loading`) | 09-30 | R61 2차 | 문서: PITFALLS 24(UIDocument를 부모·자식 오브젝트에 두면 로딩 화면이 안 뜸), VALIDATION·REQUIREMENTS·HANDOFF에 로딩 화면 문제와 수정 |
| `3bf8981` (`claude/pawnrush-loading`) | 09-30 | R61 2차 | 채팅을 `NetworkRuntime` 아래 자기 오브젝트(`Chat`)로: 런타임 오브젝트에 붙인 채팅의 UIDocument 때문에 그 자식인 로딩 화면이 Assert로 안 떴다(`c738e87`부터). AI가 Unity에서 로딩 화면(폰 러시, 12/12 준비)과 오류 0을 확인 |
| `b579c38` (`claude/pawnrush-loading`) | 09-30 | R61 2차 | 경기 화면의 채팅 창을 불투명하게(소드 파이트 씬은 UI가 밝게 그려져 뒤 글씨가 비쳤다). 문서: AI가 Unity에서 본 것(로비, 킹 러시·소드 파이트 경기 화면, 코드로 열고 닫음)을 VALIDATION·REQUIREMENTS·HANDOFF에 |
| `fbcb265` (`claude/pawnrush-loading`) | 09-30 | R61 2차 | Unity에서 보고 고침: 열린 창을 더 진하게, "입력 중 · 이동 멈춤"에 어두운 바탕, 킹 러시 안내 카드의 "Tab 채/팅" 줄바꿈을 따로 한 줄로, 다시 열 때 남은 글이 통째로 선택되던 것(`selectAllOnFocus` 끔) |
| `4fab695` (`claude/pawnrush-loading`) | 09-30 | R61 2차 | 문서: UI §9(Tab으로 여는 패널 하나), DECISIONS U8, VALIDATION 채팅 표(2차 기준 14개), REQUIREMENTS R61, HANDOFF |
| `c738e87` (`claude/pawnrush-loading`) | 09-30 | R61 2차 | 사용자 테스트 뒤: 채팅을 씬 HUD에서 빼 게임 전체에 패널 하나(`NetworkRuntime`의 `ChatBox`, 정렬 순서 50, 로비 왼쪽·모든 경기 씬 오른쪽 아래)로. 평소엔 `Tab 채팅`과 8초 뜨는 새 줄만, Tab 열기·Tab 채널(파티 → 팀 → 전체)·Enter 보내기·Esc/닫기 버튼, 경기에서는 보내면 닫힘. `ChatBox.KeysHeld`로 로비 단축키·경기 Esc·캐릭터 이동·소드 파이트 Esc 메뉴·F6·이동이 채팅 중 멈춤. 검사: Core 79·모의 세션 39·실제 DLL 컴파일 통과, Unity 미확인 |
| `02e17f0` (`claude/pawnrush-loading`) | 09-30 | R61 | 채팅 Esc: 입력 칸 자체 키 이벤트(Esc·취소)로도 닫음(쓰던 글 지움). 문서: UI §9·SESSION §9·DECISIONS K11·U8·VALIDATION 채팅 표·HANDOFF·STRUCTURE, R58~R60 "푸시 전" 표기를 "09-30 푸시 `6b4118a`"로 |
| `6859007` (`claude/pawnrush-loading`) | 09-30 | R61 | 로비 채팅에서 막힌 줄의 안내("1초에 한 번까지…")를 입력 줄 끝에 빨간 글씨로(패널 높이가 고정이라 아래에 두면 안 보였다) |
| `1a8ef5f` (`claude/pawnrush-loading`) | 09-30 | R61 | 한 줄 보낸 뒤에도 입력 칸 유지: 입력 칸이 Enter를 받아 포커스를 놓던 것(다음 Enter가 게임 시작이 됐다)을 채팅이 먼저 받음 |
| `5bcfd10` (`claude/pawnrush-loading`) | 09-30 | R61 | 로비 대체 클릭 경로를 한 프레임 늦춤: 세션의 첫 클릭이 진짜 클릭과 함께 대체 경로로도 눌려, 버튼이 아닌 곳(채팅 칸)을 누르면 다른 버튼(소드 파이트 카드)이 눌리던 기존 문제. 로비 채팅 패널 380×176, 더 진하게 |
| `d933858` (`claude/pawnrush-loading`) | 09-30 | R61 | 채팅: Core `Chat.cs`(`ChatText`·`ChatThrottle`·`ChatLog`), `SteamSession.Chat`·`CanChat`·`Say`(Steam 로비 채팅, 파티/경기 방, 팀 줄은 같은 팀만, Steam 이름, 알림 줄), `Game/ChatBox.cs`·`Resources/ChatHud.uxml`·`NetworkHud.uss` "chat"(로비 왼쪽 패널, 경기 오른쪽 아래), 로비·경기 화면 연결(쓰는 동안 이동 멈춤, Esc가 경기 나가기로 번지지 않음). 검사: Core 79·모의 세션 39·실제 DLL 컴파일 통과, 프로토콜 v14 그대로 |
| `6b4118a` (`claude/pawnrush-loading`) | 09-30 | R58 수정 | 로딩 화면: 흑팀 준비 점이 짙은 색이라 남색 배경에서 안 보이던 것 → 금색으로 채움, 100%일 때 % 숫자와 8랭크 퀸이 겹치던 것 → 문구 줄과 진행 줄 사이 18 → 58px(말이 40px + 뛸 때 16px 올라옴). `NetworkHud.uss`만. **Unity 미확인** |
| `6eefd07` (`claude/pawnrush-loading`) | 09-30 | R60 | 빈자리를 받는 시간 4분(`Backfill.OpenSeconds` 240), 시작한 경기에서는 나간 사람만 나감(파티원·파티장·방장 모두. 방장이면 방장 이전, 파티원은 경기에 남음), 먼저 나온 파티원은 파티에서 기다림, 파티장은 경기 중인 파티원이 돌아올 때까지 매칭 불가(파티 멤버 데이터 `match`), 로비 문구·버튼. 검사: Core 77·모의 세션 37·실제 Unity 6000.3.11f1 DLL·Steamworks 소스 전 어셈블리 컴파일 통과, **Unity·Steam 미확인** |
| `6061934` (`claude/pawnrush-loading`) | 09-30 | R59 | 경기 중 빈자리 채우기: Core `Backfill`(팀마다 시작 인원까지, 파티는 한 팀, 오는 파티원 자리 25초, 공유 출발 시각부터 180초)·`TeamReservations.ValidRequest`, `SteamSession`(경기 `open`·`seats`·`held`, 검색 `open=1`·빈자리 경기 먼저, 혼자 기다리던 대기실 방장도 이동, 시작한 경기의 요청은 `SeatLate`, 자리 받기 전에는 `Started` 아님, 새 방장이 이어받음, `HostNote` → `Note`), 로딩 화면 부제 "· 경기 중 합류", 로비 매칭 패널 문구, 프로토콜 v14. 검사: Core 77·모의 세션 33·실제 Unity 6000.3.11f1 DLL·Steamworks 소스 전 어셈블리 컴파일 통과, **Unity·Steam 미확인** |
| `a47d3e1` (`claude/pawnrush-loading`) | 09-30 | R58 | 로딩 화면(A 시안 UI + B 시안 배경)과 동시 출발: `MatchLoader`(비동기 경기 씬 로드·프레임 안정 대기·공유 시각 `go`에 동시 해제·입력 차단), `LoadingScreenView`·`LoadingBackdrop`·`LoadingContent`·`LoadingHud.uxml`·NetworkHud.uss, Core `MatchStart`·`LoadProgress`·`WarmupMeter`, `SteamSession` `load`/`go`, 프로토콜 v13, `StageKit.Darkening`(인트로와 공유). 검사: Core 75·모의 세션 29·실제 Unity 6000.3.11f1 DLL·Steamworks 소스 전 어셈블리 컴파일 통과, **Unity·Steam 미확인** |
| `d530da4` (`JY-kingrush`) | 09-28 | R57 | `claude/host-migration`(`514cedf`, 방장 선정·이전, `JY-lobby` 계열 R50)을 합침. 충돌: `SteamSession.Protocol`(v11 + 방장 이전 → **v12**), `StartGame`(시작 조건은 `CanStartGame`, 그 뒤에 방장 선정), `SessionFlowTests`(양쪽 테스트 모두 유지), 문서(HANDOFF·HISTORY·SESSION·VALIDATION). 병합 뒤 검사: Core 71·모의 세션 28·실제 Unity 6000.3.11f1 DLL·Steamworks 소스 전 어셈블리 컴파일·경계 검사 통과, **래그돌 자동 점검·Unity·Steam은 안 돌림** |
| `c499844` (`JY-kingrush`) | 09-28 | R57 | `feature/ui-sample-b`(`c82abfa` = `JY-lobby` R49 + 로비·인트로 샘플 B)를 합침. 충돌: `SteamSession.Protocol`(v10 + v7 → **v11**), HANDOFF·REQUIREMENTS(R 번호가 두 계열이라 §1-b로 분리)·HISTORY·VALIDATION·퀸 오브 더 힐 README 문서. 코드는 자동 병합. 병합 뒤 Core 66·모의 세션 20·실제 Unity DLL·Steamworks 소스 전 어셈블리 컴파일·경계 검사 통과, **래그돌 자동 점검·Unity는 안 돌림** |
| `514cedf` (`claude/host-migration`) | 09-28 | R50 | 방장 선정·이전: Core `HostElection`(기계·현재 점수, 평균 핑 할인, 순위, 후계자, 느린 방장 `Takeover`, `FrameMonitor`), `HostFitnessProbe`, `SteamSession` epoch·successors·claim(시작 때 적합한 PC, 나가기·크래시·응답 없음 4초에 후계자, 로비 주인이 방장을 따라감, 느린 방장 넘김), `SteamMotion`·`SteamRagdollLink` 역할 전환, 스냅샷 일정 전송·수신 끝까지·래그돌 방장 과부하 보호, 프로토콜 v8, 테스트 Core +5·세션 +8, `Docs/Network/HOST.md` |
| `51a4036` (`JY-kingrush`) | 09-27 | R56 | 왕의 계단 회전부 추락 보완/최종 자동 검사·인수인계 정리. 새 코스29/0·기존 기능80/0,역순29/0·이전 코스68/0,Core62/세션20·컴파일 통과. 공통 물리/로비/온라인 보존. 상세는 FINAL_COURSE, 사용자 테스트는 나중에 |
| `a58665a` (`JY-kingrush`, 사용자 중간 저장) | 09-27 | R56 | 개발 중인 왕의 계단/최종 집결/왕좌 결승,6프리팹·16체크포인트·승격2/3/4자리, T0·누적 왕관/붕괴·재진입/초읽기·승패를 저장. 이 시점에는 계단 순서별 자동 검사 실패가 남아 있었으며 위 후속 커밋에서 보완 |
| `a477c22` (`JY-kingrush`) | 09-27 | R55 | 승격 결정→10초→양 팀 미도착 인원 집결→벽 개방, 성벽 갈림길/시소 미션 연결. 기존 씬에 구간7개 추가, 공통 물리/온라인 보존. 자동 전체68/0·집중34/0·기존80/0, Core56·세션20·컴파일 통과. 한계는 CASTLE_COURSE, 사용자 재확인 대기 |
| `03a3346` (`JY-kingrush`) | 09-27 | R54 | 사용자 우선순위 변경: 능력 대신 첫 연결 맵. 구간 프리팹11개·장난감 코스·개인 체크포인트·승격·실제 상자 포획/선반/6칸 다리/팀 문·연장·F4 더미 시험. 기존 조작/능력 시험장/온라인 라우팅 보존. 자동 결과는 OPENING_COURSE. 이후 일반 긍정 피드백/진행 정정→R55 |
| `878a353` (`JY-kingrush`) | 09-27 | R53 | R52 일반 동작 사용자 확인 기록. 룩 대포: 실제 잡기/조준/버둥 탈출·포물선·착지 차이·쿨타임·F5 시험, 모드 전용 앵커 해제와 짧은 발사자 충돌 유예/복구. 공통 기본기/튜닝/소드파이트 보존. 자동 결과는 KingRush README, R53 사용자 미확인 |
| `2230658` (`JY-kingrush`) | 09-27 | R52 | `JY-gpt_gamemode@852eb2b`에서 분리. 킹 러쉬 원문 기획·별도 오프라인 6v6 시험장, 동일 몸·승격 쟁탈·능력 구역·팀 문/폰 복귀·물 부활, 체크! 1차/F4 더미 배치. 공통 물리·소드파이트·로비 보존. 범위/자동 결과는 KingRush README |
| `852eb2b` (`JY-gpt_gamemode`) | 09-27 | R51 | 보이는 검 각도·무게 기반 보정 드래그·강한 회전 베기만 넉다운. 느린 접촉 반동의 오인 방지, 시점부터 물리 명중까지 회귀 검사. 클릭/F6/공통 물리 보존, Steam v10. 자동 결과는 SwordFight README, 사용자 손맛/2PC 미확인 |
| `d830885` (`JY-gpt_gamemode`) | 09-27 | R50 | 클릭/물리 조작 F6·Esc 전환·빠른 발도·조준 추종·시점 감속/입력 회전 제한 철회·CFS3/v9. R49 물리 동작 사용자 확인/손맛 불만족 반영. 공통 물리 보존. 자동 결과는 SwordFight README. 후속 사용자 검 가림/반응 피드백 → R51 |
| `9ae9022` (`JY-gpt_gamemode`) | 09-27 | R49 | 물리 칼 발도/수납·시점 드래그 베기·감도/회전 상한·실제 칼 충돌 판정·CFS2/v8 자세 전달. Core41·모의 Steam20·Windows 플레이어32/0 두 번·전체 빌드 통과. 더미 이용 가능 사용자 확인 반영, 공통 물리/씬 보존. 당시 새 칼 사용자 미확인 |
| `96cd80d` (`JY-gpt_gamemode`) | 09-27 | R48 | 소드파이트 정지 더미·팀별 인원 ±·혼자 비공개 테스트 시작. 공개 12인/사람/파티 예약 보존, 공통 물리 변경 없음. Core 40·모의 세션 20·DLL 컴파일 통과. 사용자 로비 선택→매칭 진입 확인 기록 |
| `01562e4` (`JY-gpt_gamemode`) | 09-27 | R47 | 폰 소드파이트 플레이 버전: 모드 전용 칼/입력·연속 피격 허용·장외/부활/결과·봇·UI Toolkit HUD, 로비 씬 연결, 기존 세션 기반 방장 권위 동기화(v7/CFS1). 공유 물리 튜닝·원본 작업 보존, 최종 맵은 후속. 사용자 미확인 |
| `4bdff3c` (`JY-gpt_gamemode`) | 09-27 | R45·R46 | 소드 파이트를 GPT 우선 모드로. 참고 영상 주요 프레임 기반 기획 초안·경기장 콘셉트 1장·프롬프트. 누운 상대 연속 타격도 재미로 허용하고 성립 난도·빈도를 조절. 게임 코드 변경 없음 |
| `82a44bd` (`JY-gpt_gamemode`) | 09-27 | R44 | `JY-ragdoll_v2`의 `cdcc9fe`에서 GPT 게임모드 브랜치 분리. 원본 미완성 작업 보존, 별도 worktree와 새 브랜치 규칙 기록. 코드 변경 없음; Core 32·모의 세션 15 통과 |
| `5905eca` (`JY-lobby`) | 09-27 | R49 | 퀸 오브 더 힐 맵 7차: 7개 층 + 8랭크, 층마다 두세 갈래 길. 맵을 데이터로(`Tools/QueenHill/build_layout.py` → JSON → `QueenHillLevel`), `OrbitPlatform`·`PhaseToggle`, three.js 미리보기, `QueenHillCourse` 랭크 높이·테스트 |
| `6250132` (`JY-lobby`) | 09-27 | R48 | 퀸 오브 더 힐 그레이박스 맵: `QueenHillCourse`(Core, 테스트 2개), `QueenHillLevel`(Play 때 160 m 맵 생성), 구간 기록 패널, `OrbitCamera`, `PlaytestSpawner` 팀·카메라 기준 이동·부활·추락·스테미나, `ITeamAssignable`·`IStaminaReadout`, 씬 `QueenOfTheHill.unity`와 생성기, 문서 `GRAYBOX.md` |
| `dad2635` (`JY-lobby`) | 09-27 | R47 | `JY-ragdoll_v2`(`41b7d11`)를 `JY-lobby`로 빨리 감기 병합, Core `QueenHillRules`를 mcs 호환으로(foreach 튜플 분해 제거) |
| `41b7d11` (`JY-ragdoll_v2`) | 09-27 | R46 | M11 승격·기물 성능: Core `ChessPieces`, `PromotionPad`, 래그돌 무게·이동·등반·질주·면역 배율, 머리 위 표시, 스냅샷 기물 1바이트(v7), [7j] 시험대, 자동 점검 5개 |
| `ddad4e6` (`JY-ragdoll_v2`) | 09-27 | R45 | M13 상태 효과: `IStatusReceiver`, 찌그러짐(조작 불가·매달림 놓기·면역)·비틀·면역 플래그, 시험대 Shift+F5·F6, 자동 점검 4개 |
| `987b6c0` (`JY-ragdoll_v2`) | 09-27 | R44 | M6 밧줄·사슬·그네: `RopeLine`(시계의 함수로 흔들림), 래그돌 밧줄 매달리기(W/S·A/D·Space·꼭대기 올라서기), 0.5 m 기둥은 벽 등반으로 됨, [7i] 시험대, 자동 점검 4개 |
| `cdcc9fe` (`JY-ragdoll_v2`) | 09-27 | R43 | M7 도약대·태엽 스프링: `LaunchPad`(정해진 곡선, 스프링은 주기마다 모두 같은 속도), 래그돌 `Launch`(앵커가 상쇄하던 세 군데 수정, 공중 조종 0), [7h] 시험대, 자동 점검 3개 |
| `3858b3f` (`JY-ragdoll_v2`) | 09-27 | R42 | M8 개척의 종·열리는 길(20초 연 팀 독점, 다른 팀은 통과), M9 체크포인트(내 최고·팀 최고−1), Core 규칙과 테스트, 랩 네트워크 전달, [7g] 개척의 탑 |
| `51f0651` (`JY-ragdoll_v2`) | 09-27 | R42 | M10 팀: 백·흑·없음, P1 백·P2 흑, 온라인은 명단 팀, 앙파상은 적만 |
| `213612f` (`JY-ragdoll_v2`) | 09-27 | R41 | 등반: 멈추면 스테미나 안 줆·오르기 1.0/초, Space = 스테미나 1.2로 벽에 붙은 채 1 m 도약, 벽에서 키를 카메라 기준 그대로(W 위·A/D 옆, 모서리는 A/D로만). 갈고리 조준 어깨 너머 카메라, 밧줄 흔들림. 자동 점검 3개(전체 66/0) |
| `ad5ee7a` (`JY-ragdoll_v2`) | 09-27 | R40 | M5 갈고리 + 앙파상: E 꺼내기, 좌클릭 꾹 = 돌리며 게이지, 떼면 게이지만큼 던짐, 어느 면에나 박힘, 끌려가 올라서기·벽 매달림·천장, 앙파상 F 0.4초, 궤적 미리보기, [7f] 연습장, 프로토콜 v6, 자동 점검 7개(전체 63/0) |
| `3d74c56` (`JY-ragdoll_v2`) | 09-27 | R39 | 결정 2개(45° 구르기 동률, 역경사 레인 모양)를 그대로 두기로 기록 |
| `ea37106` (`JY-ragdoll_v2`) | 09-27 | R38 | R37 브랜치를 `JY-ragdoll_v2`에 병합: 겹친 수정(질주 1스텝·등반 첫 프레임 팔)은 하나로, 다르게 고친 것(역경사 등반 시작, 턱 되튐)은 둘 다 유지, 점검 이름 스위치 `-ragdollOnly`·`-ragdollAutoTestOnly` 둘 다, 등반 팔 점검 기준 0.05로 복귀, 확인 목록 하나로. 자동 점검 56 통과 / 0 실패 |
| `0915a3c` (`claude/nice-agnesi-cf67b2`, 병합으로 `JY-ragdoll_v2`에 들어옴) | 09-27 | R37 | 래그돌 자동 점검 기존 실패 9개 정리: 점검 버그 4(질주 지침·45° 출발선·2 m 벽 뒤로 달림·옆 이동 팔) 수정, 동작 버그 5(역경사 등반 시작 위쪽 레이·머리 레이, 등반 첫 프레임 팔 제한, 손 홀드 앞서 잡기·벽에 붙을 때까지 다시 놓기, 벽 되튐 제동 조건 1 m/s, 질주·지침 1스텝) 수정, 새 점검 1, `-ragdollOnly`, 네트워크 점검 경로. 56/0, 튜닝 값 그대로 |
| `78e790a` (`JY-ragdoll_v2`) | 09-27 | R36 | 조작감 개선: 버둥대기 클릭 게이지, 머리부터 다이빙 태클, 이동 튜닝, 질주·등반 버그, 조작감 녹화 도구(`-ragdollFeel`, `feel_sheets.py`). 2차: 기상 중 끌림, 다이빙 회전·팔·기상 조건, 태클 누움, 부딪힘 튕김, 버둥대기 모습·온라인 게이지(프로토콜 v5), 역경사 매달림·턱 튕김 버그, `-ragdollAutoTestOnly`. 자동 점검 55/55 |
| `b580e2f` (`JY-ragdoll_v2`) | 09-26 | R35 | 사용자 Unity 확인(1~12 성공) 반영: 피격 넘어짐을 착지 후부터 세기, 물에 5초 둥둥 뜨기·좌클릭 버둥, 비숍 돌조각 기획 메모 |
| `ff184da` (`JY-ragdoll_v2`) | 09-26 | R34 | 퀸 오브 더 힐 M1~M4: 입력 확장(능력·상호작용·조준, 프로토콜 v4), 탈것(`IMovingSurface`·`MovingPlatform`), 피격(`IHitReceiver`), 물·부활(`WaterZone`, `Teleport` 전부 해제), [7] 시험대, 자동 점검 12개, `PawnHand` 관절 누수 수정 |
| `d3d617d` (`JY-lobby`) | 09-26 | R33 | 기물 재디자인·밸런스(킹 신규, 퀸 사냥), 새로 필요한 기능 개발 목록 `MECHANICS_TODO.md` |
| `c3f1e0b` (`JY-lobby`) | 09-26 | R32 | 레벨 6차 "하늘 궁전": 이동 단축 시스템(탈것·개척자가 여는 길·체크포인트), 자유 조준 갈고리, 흑백 팀에 맞춘 맵 색, 이미지 프롬프트 6차 |
| `378031e` (`JY-lobby`) | 09-26 | R31 | 레벨 5차 승인 기록, 중앙 구조물 아트 콘셉트 후보 6개와 프롬프트 |
| `4699520` (`JY-lobby`) | 09-26 | R30 | 레벨 5차: 128 m 퀸의 첨탑, 행마를 탈것으로 바꾼 구간, 위로 끌어 올리는 출발 밧줄, 이미지 프롬프트 5차 |
| `dad5e0a` (`JY-lobby`) | 09-26 | R29 | 레벨 4차: 2차 탑을 키운 64 m 왕관 첨탑, 끊긴 다리와 출발 밧줄(2칸 이동), 앙파상 재정의, 이미지 프롬프트 4차 |
| `f8e950b` (`JY-lobby`) | 09-26 | R28 | 레벨 3차: 물 위 64 m 왕관 탑과 양쪽 출발 다리, 구간 S1~S8, 아트 방향, 이미지 프롬프트 3차 |
| `d95650a` (`JY-lobby`) | 09-26 | R27 | 레벨 디자인 개정: 체스판 가운데 등뼈 "센터 탑", 모듈식 구간 Z1~Z7, 이미지 프롬프트 2차 |
| `b6f9a3a` (`JY-lobby`) | 09-26 | R26 | 퀸 오브 더 힐 게임 흐름·기물 밸런스, 이미지 프롬프트 5장 |
| `e7aa717` (`JY-lobby`) | 09-25 | R25 | 퀸 오브 더 힐 기획 초안 `DESIGN.md`(레벨 디자인, 폰 능력) |
| `3098851` (`JY-lobby`) | 09-25 | R24 | 퀸 오브 더 힐 인수인계 문서(아스트라용), N4 원문 보관 |
| `9d49b81` (`JY-lobby`) | 09-25 | R22 | HUD 루트를 화면 전체로 늘림(아래쪽 카드가 안 보이던 문제) |
| `d55f67d` (`JY-lobby`) | 09-25 | R20 | Input System 패키지 제거, 자동 설치에서 제외 |
| `ec6bc1f` (`JY-lobby`) | 09-25 | R18, R19 | 게임 모드 목록(`GameModes`)·모드별 매칭(프로토콜 v3)·모드 씬 로드(`MatchSceneView`), 참고 영상풍 새 로비(`LobbyStage`, HUD 재작성), 타이틀 재구성, 세션 테스트 +3·Core +1, `Docs/GameModes/` |
| `0df4403` | 09-25 | R17 | 랩 Steam 연결 → `RagdollLabSteam/`(경계 규칙), LabGame 있는 씬에서 부팅, 테스트 스크립트가 그 어셈블리 컴파일·`simulationMode` 매핑, 인수인계 문서 |
| (병합) | 09-25 | R17 | `JY-ragdoll` 두 번째 병합: 달리기/질주·스테미나, 슬라이딩 태클, 카메라·방향 전환, 등반 재작성, 랩 온라인 |
| `a43791c` | 09-25 | R16 | 랩 씬 → RagdollTest, `RagdollDriver`, 랩 어셈블리 Gameplay 참조, 커서 복구, `Assets/ChessFight.meta`, 경계 검사·Roslyn 컴파일, 문서 |
| `fc006b8` | 09-25 | R16 | `JY-ragdoll` 병합 (래그돌 파일 118개 추가) |
| `444d698` | 09-24 | R15 | 인수인계 커밋 해시 기록 |
| `cffe4a3` | 09-24 | R15 | 인수인계 체계: 루트 `HANDOFF.md`, AI 도구별 안내 파일, `Docs/` 분야별 트리, 요구사항·결정·함정 기록, `Tools/run-tests-linux.sh`, `Tools/Generators/` |
| `01dd655` | 09-24 | R14 | 호스트 끊김 경고(0.5/2/12초), 점프 누른 횟수, build 검사, 공개 매치 봇 규칙, 핑·응답 표시, F8 지연 시뮬레이터, Rich Presence. 프로토콜 v2 |
| `72ddf9e` | 09-24 | R11 | Intro·Lobby·KingRush·RagdollTest 씬, `NetworkRuntime`, `ChessFight.Gameplay`, 팀 가이드 |
| `fef9d42` | 09-24 | R10 | 게임 내 친구 초대 패널 |
| `d6ff4f9` | 09-24 | R9 | 입력 백엔드와 상관없이 HUD가 눌리도록 대체 클릭 경로 |
| `7d1ca63` | 09-24 | R8 | Active Input Handling을 Old로 복구, `InputSettingsGuard` |
| `1c0da4a` | 09-24 | R7 | 한국어 전체 화면 HUD, 단계형 시작 |
| `a070367` | 09-24 | R6 | Input System 1.20.0 고정, ScrollView 제거 ← **main이 여기** |
| `475e040` | 09-24 | R5 | Input System 코드를 패키지 존재로 게이트 |
| `d22e3ec` | 09-22 | R4 | EventSystem 부트스트랩 제거 |
| `8649011` | 09-22 | R3 | 폴더 평탄화, SampleScene 정리, UI 보강 |
| `3abe230` | 09-22 | R2 | AI 봇, 코드 생성 → 프리팹·UXML·Input System |
| `cf29671` | 09-22 | — | 네트워크 인수인계 문서 (이전 도구) |
| `c9c1e6a` | 09-21 | — | Steamworks 의존성 고정 |
| `87805f0` | 09-21 | — | Steam 파티, 6v6 매칭, 이동 테스트 |
| `5af7c8c` | 09-15 | — | 프로젝트 환경 구축 시작 |

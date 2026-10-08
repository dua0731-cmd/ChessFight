# ChessFight HANDOFF — 모든 작업은 여기서 시작한다

> **AI·개발자 공통 규칙:** 새 채팅, 다른 AI 도구, 새 팀원 모두 **이 파일을 가장 먼저 끝까지 읽는다.**
> 그다음 아래 [6. 어디를 읽을까](#6-어디를-읽을까--작업-분야별-안내)에서 작업 분야 문서만 골라 읽고 코드로 간다.
> 작업을 마치면 [8. 작업 종료 체크리스트](#8-작업-종료-체크리스트)대로 **이 파일과 요구사항 기록을 갱신한다.** 그래야 다음 도구가 같은 지점에서 이어 간다.

최종 갱신: **2026-10-08**(R86 폰 러시 코스 01을 v0.4(공용 길 + 미션 광장 2곳, 판마다 미니게임 두 개 뽑기)로. 그 전 R85 이름 바꾸기: 인트로의 "이름 바꾸기"와 로비 오른쪽 위 내 이름에서 언제든, 같은 이름 화면이 바꾸기 모드로 열리고 원래 화면으로 돌아옴. 그 전 R84 시작 로고·이름 설정 화면·장면 전환: 게임을 켜면 로고 영상(소리 A), 처음 한 번 이름 설정, 인트로·이름 화면·로비 사이는 광택 체스판이 킹 구멍으로 닫히고 나이트 구멍으로 열림, 고른 이름은 멤버 데이터 `nick`으로 다른 사람에게도. 그 전 R83 로비 UI를 A 시안으로: 이름 설정 화면 말투, 아이보리 게임 시작, 3D 무대는 그대로. 그 전 R82 스킬 8차: 퀸 예고 원 금색·원에서 금빛 불씨 연기, 비숍 줄이 지나가는 적을 따라 늘어남, 나이트 착지 미리보기를 도는 하얀 원형 표식으로. 그 전 R81 스킬 7차: 퀸 진짜 체스말 모양이 빙글빙글 돌다 팡, 룩 슈퍼맨, 비숍 줄이 다리를 잡아당김, 나이트 공중 도약. 그 전 R80 스킬 6차: 퀸 금색 체스말 팡, 룩 공중 돌진·반투명 조준선, 비숍 조준 파란색 없앰, 나이트 "다~당". 그 전 R79 스킬 이펙트를 3D 빛 이펙트로 다시 만듦, 별 모양 전부 삭제. 그 전 R78 이펙트 공방 그림(되돌림). 그 전 R76 폰 러쉬 결과 화면: `JY-kingrush`에 올린 결과 씬 둘(거기서는 R65)을 이 브랜치에 병합하고 `LastScene` 삭제. 그 전 R75 스킬 3차 수정: 룩 조준 중 걷기(질주 X)·조각 이펙트 삭제·벽 흔들림, 나이트 감지 3 m·표시 한 벌. 그 전 R74 스킬 2차 수정: F 통일·룩/비숍 좌클릭 조준·비숍 근거리·나이트 자동 머리 찍기·이펙트 글자 제거. 그 전 R73 타격감 이펙트 퀸 A·룩 A·비숍 B·나이트 B + 녹화 도구. 그 전 R72 시험용 쿨 2초·룩 4명 뭉치·이펙트 시안. 그 전 R71 폰 러쉬 스킬 시험 씬과 스킬 5개. 그 전 R70 기물 스킬 기획서. 그 전 R69 폰 러시 코스 01을 v0.2(탑을 감아 오르는 3층 고리)로. 그 전 R68 v0.1 새 씬. 그 전 R65 **`main` 병합**: `JY-kingrush` 기준 + `JY-ragdoll_v2`의 폰러시 맵 시험 씬·장애물 + `JY-lobby`의 방장 장애물 시각. 그 전: R64 설정 창: `claude/settings-window` → 원격 `JY-kingrush`에 푸시. R63 메뉴 디자인 C 수정안: `claude/menu-c` → 원격 `JY-kingrush`에 푸시. R62 결과 화면은 10-01 원격 `JY-kingrush`에 푸시(`33f1648`). R61 채팅은 10-01 원격 `JY-kingrush`에 푸시(`e64012e`). R60 나간 사람만 나가기·빈자리 4분, R59 경기 중 빈자리 채우기, R58 로딩 화면·동시 출발은 09-30 원격 `JY-kingrush`에 푸시(`6b4118a`). 그 전 09-28: `feature/ui-sample-b`, `claude/host-migration`을 `JY-kingrush`에 합침) · 이 폴더의 작업 브랜치 **`JY-kingrush`**(R56, 출발점 `JY-gpt_gamemode@852eb2b`).

> **작업 브랜치 = `main` (R66, 사용자 10-06).** 이 아래와 다른 문서에 남은 예전 브랜치 지시(`Network`, `JY-lobby`, `JY-gpt_gamemode`, `JY-kingrush`에만 커밋 등)는 **모두 이 결정으로 대체**됐다. AI 작업은 `main`에 커밋·푸시한다. 다른 브랜치는 기록용으로 남는다.
>
> **최신 R86(사용자, 10-08): 폰 러시 코스 01을 레벨 디자인 v0.4로(같은 씬), 미니게임은 판마다 랜덤** [PAWN_RUSH_COURSE01](Docs/KingRush/PAWN_RUSH_COURSE01.md) ([v0.4 원문](Docs/KingRush/PAWN_RUSH_COURSE01_DESIGN_v0.4.md)). 팀 구간이 없어지고 두 팀이 같은 길을 달린다: 출발 → 공용 A → **미션 광장 ①** → 공용 B → **미션 광장 ②** → 공용 C → 탑 결승. 광장마다 백·흑 스테이션(20×20 m)이 4 m 통로를 두고 나란히, 각 팀은 자기 스테이션만 풀고 상대는 걸어 들어와 방해한다. 팀 문(`TeamGate`)은 자기 팀 완료로만 열리고 자기 팀만 지나간다. **미니게임 다섯 개(A 칠하기·B 판자 다리·C 도개교·D 캡스턴·E 종탑)** 를 `MissionStation`에 끼우고, **`MissionPicker`가 판마다 두 개를 뽑는다**(서로 다름·직전 판 제외·두 팀 같은 게임, 규칙은 Core `PawnRushMissions` + 테스트). 손 잡기는 코스 코드에서 안 보여 위치·걷는 방향으로 판정(문서 4절). `Course01v4Builder`, 메뉴 **Build Course01 v4**, v0.4 검증기(장애물이 길 폭 전체를 덮는지 광선으로 잼). 플레이테스트 F6 완료·F7 반대 입력·F8 새 판·F9/F10 게임 바꾸기. **Linux 컴파일·파이썬 좌표 점검만, Unity 미확인** → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 v0.4 표. 기획서와 다른 곳(같은 씬, 북쪽 벽 10 m, 문은 모든 게임에서 완료까지 닫힘 등)은 문서 7절
>
> **R85(승규 님, 10-08): 이름 바꾸기.** "이름은 바꿀 수 있게, 로비 말고 인트로에서도(아직 테스트라 한 번 보고 안 보이면 테스트가 안 됨)". **인트로의 "클릭해서 시작" 옆 "👑 이름 바꾸기"**와 **로비 오른쪽 위 내 이름**(버튼이 됨)에서 언제든: 누른 곳에서 킹 구멍이 닫히고 카드 "이름 바꾸기" → R84의 이름 화면이 바꾸기 모드("이름 바꾸기" 제목, 지금 이름, "이 이름으로 바꾸기", "← 돌아가기", Esc = 돌아가기) → 다시 누른 화면으로(인트로는 버튼, 로비는 내 나이트에서 열림). 바꾸는 동안 로비 단축키·채팅은 쉼, 매칭 중엔 막음. Shift+클릭은 없앰, 오프라인 로비도 고른 이름(`NameChange`·`NameScreen`·`IntroController`·`LobbyBootstrap`·`NetworkHudView`). **AI가 Unity에서 인트로 → 바꾸기 → 돌아가기/바꾸기, 로비 → 바꾸기 → 로비를 캡처로 확인**, 마우스·키보드는 사람 확인 전 → [UI §15](Docs/Architecture/UI.md#15-시작-로고이름-설정-화면장면-전환-r84-2026-10-08) "이름 바꾸기", 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md) 최상단
>
> **R84(승규 님, 10-08): 시작 로고·이름 설정 화면·장면 전환.** [선정 시안 3개](https://claude.ai/artifact/EyfYVsT2XNbk9Wiv4jzWaK)를 Unity에. ① 게임을 켜면 **로고 모션 그래픽**: 글자가 "팍! 팍팍 파파파팍" 쾅 박히고, 가운데 띠가 레이스 트랙처럼 펼쳐지고, C 위 왕관이 "띵". 시안을 찍은 1080p 영상(고른 **소리 샘플 A** 포함, `StreamingAssets/LogoIntro.mp4`)을 인트로 위에서 한 번, 클릭·키로 건너뜀(`LogoIntro`). ② **처음 한 번 이름 설정 화면**(인트로 판 클릭 → 이름 없으면): 나무색 말 무늬 배경(시안에서 찍은 그림), 24초에 한 바퀴 도는 유리 폰, 은색 제목, 밑줄 입력칸, **"4 / 12" 옆 "추천 이름" → 추천 이름이 입력칸 위 말풍선, 누르면 들어감**, Steam 이름 버튼 없음, 아이보리 시작·Enter(`NameScreen`·`GlassPawn`·`Core/PlayerNames`). 이름은 `PlayerPrefs` + 파티·경기 방 멤버 데이터 **`nick`** → 로비·채팅 등 모든 화면에 고른 이름(없으면 Steam 이름, 프로토콜 그대로). ③ **장면 전환**: 광택 체스판(시안 그림)이 킹 구멍으로 닫히고 "다음 화면" 카드 뒤 다음 화면이 준비되면 나이트 구멍으로 열림(인트로 → 이름 화면 → 로비, 로비는 내 나이트 위에서)(`SceneTransition`). (R85에서 Shift+클릭 대신 "이름 바꾸기" 버튼). 새 에셋 3개(StreamingAssets, LFS). Windows 테스트 코어 88 · 세션 40 · 실제 Unity DLL 전체 컴파일 통과 → [UI §15](Docs/Architecture/UI.md#15-시작-로고이름-설정-화면장면-전환-r84-2026-10-08), 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md) 최상단. **AI가 이 PC Unity에서 Play해 로고·전환·이름 화면·로비를 캡처로 확인**(버튼은 코드로 누름, 소리는 못 들음). Play 직후 로고가 영상 전에 사라지던 것은 고침(`a78f5d6`, PITFALLS 32). **사람 확인 전**
>
> **R83(승규 님, 10-08): 로비 UI A 시안.** 로비의 3D 무대·체스판·조명은 그대로 두고 **UI만** 이름 설정 화면과 같은 말투로 바꿨다: 어두운 판에 가는 금 테, 선 아이콘, 금색 GAME MODE·PARTY, 은색 "게임 모드", 고른 왕관 로고, 그리고 노란 대신 **아이보리 "게임 시작 →"**(아래 ENTER·ESC 안내). 매칭 카드·방 배너·친구·코드·모드 창도 같은 판. 로비에서는 두꺼운 판 버튼 대신 테두리 버튼. 시안 3개 중 A([캔버스](https://claude.ai/artifact/CriXrXYV7WeUPaVvr3aoPE)). 반투명 값은 Unity 선형 색 공간에 맞게 바꿈(PITFALLS 30). **AI가 이 PC Unity에서 로비를 Play해 캡처로 비교, 버튼 누르기·매칭·사용자 미확인** → [UI §14](Docs/Architecture/UI.md#14-로비-ui-a-시안-r83-2026-10-08), 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md) 최상단
>
> **R82(승규 님, 10-08): 스킬 8차 수정.** 퀸 예고 원이 파란색 대신 **터지는 빛과 같은 금색**, 그 바닥 원에서 **작은 금빛 불씨와 연기가 피어오름**(터질 때 퍼지는 고리에서도). 비숍은 적이 줄을 지나가면 **줄이 다리에 걸려 고무줄처럼 늘어남**: 달려온 적은 붙잡혀 느려지다 0.75 m에서 다리를 잡아채여 넘어지고 줄은 튕겨 돌아옴, 이미 걸렸던 적 등은 늘였다 빠져나감(`SkillTripwire`). 나이트 착지 미리보기는 파란 원 대신 **바닥에서 도는 하얀 반투명 원형 표식**(`SkillMarks.Reticle`). 녹화 `PawnRush_Skill_R82c.mp4` → [Skills README](Docs/Skills/README.md). Windows 테스트 코어 83 · 세션 39 통과. **사람 확인 전**
>
> **R81(승규 님, 10-08): 스킬 7차 수정.** 퀸의 금색 말을 **진짜 체스말 모양**으로(코드로 만든 스탠턴 6종: 나이트 말 머리·룩 성벽 톱니·퀸 왕관 구슬·킹 십자가, `PawnRushSkillFx.Chess.cs`), 조금 작게, **팽이처럼 돌며 퀸 둘레를 빙글빙글 모였다가 같은 방향 소용돌이로 팡**. 룩 공중 돌진은 **슈퍼맨**(몸을 진행 방향으로 눕히고 두 주먹을 앞으로). 비숍은 적이 걸리면 **줄이 다리를 뒤로 잡아채고** 그 줄이 다리를 따라 늘어났다 튕겨 돌아옴(`SkillTripwire`). 나이트는 **점프 중에도 F**로 공중에서 바로 도약. 녹화 `PawnRush_Skill_R81c.mp4` → [Skills README](Docs/Skills/README.md). **사람 확인 전**
>
> **R80(승규 님, 10-08): 스킬 6차 수정.** 퀸은 마법 대신 **작은 금색 체스말이 모여들었다가 팡 터짐**(선반으로 깎은 금속 금색 말, 바닥에 튕김). 룩은 조준선이 **반투명 흰색**이고 **공중에서도 F**: 마우스 위아래로 3D 조준 → 좌클릭 = 잠깐 멈췄다 그 방향으로 돌진, 아래면 바닥에 **내리꽂음**, 위로 갈 땐 팔을 위로·아래로 갈 땐 다이빙 자세(`SkillPose`, `RagdollPawn.cs` Pose에 고리 1줄). 비숍 설치 조준에서 **파란색 없앰**, 흰 선 굵고 진하게(반투명). 나이트 공중 두 번째 F(적 없음)는 꺾든 직진이든 **다시 차고 나감**("다~당": 7.5 m/s·위 4 m/s, 멈춤·빛 고리; 꺾기 옆 4.0 → 6.8 m, 직진 9.5 m). 녹화 `PawnRush_Skill_R80b.mp4` → [Skills README](Docs/Skills/README.md). **사람 확인 전**
>
> **R79(승규 님, 10-08): 스킬 이펙트를 3D 빛 이펙트로 다시.** R78 공방 그림은 "3D 입체적인 느낌이 아니다"로 되돌림. 이펙트를 코드로 만든 3D 조각과 입자로 다시 만듦. 빛의 벽·기둥·빛줄기·빛 껍질·마법진, 튕기는 불꽃·빛 알갱이·연기, 실제 순간 조명, 궤적, 번개가 들어가고, 직접 만든 빛 번짐(블룸)을 씀(기본 렌더러라 패키지 없이, `PawnRushSkillBloom`). **별 모양은 전부 삭제**(어지러움은 빛 구슬과 빛 고리). 밝은 바닥에서도 색이 보이게 몸통은 칠하고 가는 속만 번지게 함 → [Skills/EFFECTS §3D 빛 이펙트](Docs/Skills/EFFECTS.md#3d-빛-이펙트-r79-10-08). 녹화 `PawnRush_Skill_R79c.mp4`. **사람 확인 전**
>
> **R78(승규 님, 10-07, R79로 대체): 스킬 이펙트 = 이펙트 공방 그림.** 승규 님이 이펙트 공방(PXF, 커넥터 연결)에서 AI가 준 영어 문구로 만든 연속 그림 10개를 `PawnRush_SkillTest`에 넣음(`Resources/PawnRushSkillFx`, 바닥 그림은 눕히고 불꽃·별은 카메라를 봄). **부딪힐 때 뜨던 별은 삭제**(절대 쓰지 않음), 별만 있던 룩 맞힘·벽에는 주황 불꽃. 녹화로 보고 게임 화면에 맞춤. **R79에서 그림과 묶는 도구 모두 지우고 3D 빛 이펙트로 바꿈**(위).
>
> **R76(승규 님, 10-07): 폰 러쉬 결과 화면을 이 브랜치에.** `JY-kingrush`에 10-06 올린 결과 화면을 병합해 R62 `LastScene`을 지우고 **`PawnRushVictory`(이긴 팀)·`PawnRushLose`(진 팀)** 두 씬을 넣었다(메뉴 **ChessFight > Scenes > Pawn Rush Victory / Lose (result preview)**). 승규 님이 고른 [시안](https://claude.ai/artifact/9rH56RC5cR2uamzdZd9iNK) "결승 중계": 램프 아래 **나무 탁자 체스판**에 두 팀(이긴 팀 세리머니, 진 팀 킹은 왕관이 날아가고 쓰러짐), 위 띠에 **결승 순서 카드**, 아래 **팀 점수판**과 미도착·12초·다시 매칭·로비로, H로 숨기면 작은 점수 표시. 스킬·코스 작업과 겹치는 코드는 없었다. **번호 주의:** `JY-kingrush`의 문서·커밋에서는 이 일이 **R65**지만 이 브랜치의 R65는 `main` 병합이라 **R76**으로 적는다. 병합 뒤 Windows에서 Core 83·모의 세션 39·실제 Unity DLL 전체 어셈블리 컴파일 통과(`Test-NetworkCompile.ps1`이 스킬 하위 폴더 `RagdollLab/Scripts/PawnRushSkills`를 안 읽어 이 브랜치에서 실패하던 것을 같이 고침). **이 브랜치를 연 Unity·키·버튼·사용자 미확인. 경기 흐름 미연결(결정 대기)** → [UI §13](Docs/Architecture/UI.md#13-폰-러쉬-결과-화면-r76-2026-10-06), 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md) 최상단
>
> **R75(승규 님, 10-07): 스킬 3차 수정** — 룩은 **F 조준 중에도 평소처럼 걷기(Shift 질주만 안 됨)**, 좌클릭 뒤 0.3초부터 제자리. 부딪힐 때 **튀는 조각 이펙트 삭제**. 화면 흔들림이 거의 안 보이던 것을 고침(위치 + 살짝 회전), **룩이 벽에 박히면 크게 흔들림**. 나이트 머리 찍기 **감지 3 m**(4 → 3), 감지 표시는 **주황**, 두 번째 F 뒤에는 **파란 착지 원을 숨겨** 표시가 두 벌로 보이지 않게. 녹화 `PawnRush_Skill_R75b.mp4`. **사람 확인 전**
>
> **R74(승규 님, 10-07): 스킬 2차 수정** — 스킬 키 **F로 통일**(임시, 이 씬에서 F 상호작용 끔). **룩·비숍은 F로 조준 → 마우스로 방향/위치 → 좌클릭**(우클릭/F 취소). 비숍 **근거리 4.5 m**, 설치 전 반투명 X·보라 칸·거리 원. **나이트 공중 F: 4 m 안 적이 표시되면 자동으로 머리 찍고 옆에 착지, 없으면 90° 꺾기.** 이펙트의 글자·숫자(뿅·쿵·덜컥·1·2·3) 전부 뺌. 퀸 그대로. 자동 시험 전부 통과, 녹화 `PawnRush_Skill_R74b.mp4` → [Skills/README](Docs/Skills/README.md). **사람 확인 전**
>
> **R73(승규 님, 10-07): 타격감 이펙트** — 고른 시안 **퀸 A · 룩 A · 비숍 B · 나이트 B**를 `PawnRush_SkillTest`에 만듦(`PawnRushSkillFx`, 시험장만). 맞는 순간 멈춤(지금은 게임 전체)·흔들기·흰 번쩍·글자 + 기물별 그림. 폰은 아직 안 고름, 소리 없음. 녹화 도구 `PawnRushSkillFilm`으로 AI가 mp4를 찍어 보여 줌 → [Skills/README](Docs/Skills/README.md#이펙트-녹화-r73). **사람 확인 전**
>
> **R72(승규 님, 10-07): 시험 편하게** — 시험용 쿨타임 **모든 기물 2초**(Inspector `Test Cooldown` = 0이면 기획 쿨), **V = 더미 4명을 내 앞에 2×2로 뭉쳐 세움**(룩을 고르면 저절로, 룩 돌진 물리 시험용). 타격감 이펙트 **시안 기물마다 3개** → [Skills/EFFECTS](Docs/Skills/EFFECTS.md)(만든 것 없음, 고르면 만듦)
>
> **R71(승규 님, 10-07): 폰 러쉬 스킬 시험 씬과 스킬 5개** [Skills/README](Docs/Skills/README.md). `RagdollTest`를 복사한 **`Assets/Scenes/PawnRush/PawnRush_SkillTest.unity`**(메뉴 **ChessFight → Pawn Rush → Open Skill Test**): 화면 왼쪽 창에서 **폰(기본)·퀸·룩·비숍·나이트**로 바꾸고(Z/X), **G**(임시 키)로 스킬. 폰 첫 두 걸음(부축), 퀸 팔방 밀치기, 룩 직선 돌파(바리케이드), 비숍 교차 밧줄, 나이트 꺾어 도약. **킹은 안 만듦(D1)**, 수치는 v0.1 그대로 + 느려지지 않게(D2), 기존 키 그대로(D3). **스킬은 이 씬에서만 켜짐**(DECISIONS SK1): 퀸 오브 더 힐·소드 파이트·네트워크는 그대로. 이 PC Unity에 **MCP 다리 연결**(포트 6401)해 AI가 컴파일·Play·자동 시험 도구 `PawnRushSkillProbe`로 수치를 잼(기획 수치와 맞음, 표는 README). **사람 확인 전** → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 스킬 표
>
> **R70(승규 님, 10-07): 기물 스킬 기획서** [Skills/DESIGN](Docs/Skills/DESIGN.md) ([인수인계](Docs/Skills/README.md)). 스킬 프리비즈 영상·카드(v0.1 수치)를 폰 러쉬에 맞춘 상세 기획: 폰 첫 두 걸음, 나이트 꺾어 도약, 비숍 교차 밧줄, 룩 직선 돌파, 퀸 팔방 밀치기, 킹 왕의 한 걸음 + 공통 규칙·네트워크·RagdollTest 시험 계획. **문서만, 코드 없음.** 이 채팅의 작업 브랜치는 **`claude/bold-johnson-8ez95n`**, 시험 씬은 **`Assets/Scenes/RagdollTest.unity`**(사용자 지정). 결정 대기 D1~D11, 먼저 D1(폰 러쉬에 킹 없음)·D2(프리비즈 수치가 지금 래그돌보다 느린 몸 기준)·D3(우클릭 → E)
>
> **R69(사용자, 10-07, R86으로 대체): 폰 러시 코스 01을 레벨 디자인 v0.2로, 같은 씬에서** [PAWN_RUSH_COURSE01](Docs/KingRush/PAWN_RUSH_COURSE01.md) ([v0.2 원문](Docs/KingRush/PAWN_RUSH_COURSE01_DESIGN_v0.2.md)). 일자 코스가 맵 가운데 8랭크 탑(36 m)을 한 바퀴 감아 오르는 3층 고리가 됐다: 공용 A(지상) → 서쪽 바깥 날개(팀 구간 1) → 공용 B(A의 위층) → 안쪽 날개(팀 구간 2) → 공용 C(탑 뒤) → 탑(북벽 직등 또는 나선 경사로) → 꼭대기 왕관 원에서 결승. `Course01v2Builder`가 월드 좌표로 만들고 메뉴 **Build Course01 v2**(루트를 지우고 다시 만듦). 신규: **낙차 8 m 복귀**(`FallDistanceRespawn`), **팀 구역**(`TeamZone`), 원형 결승(`FinishZone` 골반만), `ProgressPath`, v0.2 검증기. v0.1 모듈 이어 붙이기 코드는 삭제. **Linux 컴파일·파이썬 좌표 점검만, Unity 미확인** → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 v0.2 표. 기획서와 다른 곳 10가지(탑 북벽 턱을 계단식으로, 낙차 복귀를 즉시로 등)는 문서 6절
>
> **R68(사용자, 10-06, R69로 대체): 폰 러시 코스 01 「여덟 번째 랭크」를 새 씬으로** [PAWN_RUSH_COURSE01](Docs/KingRush/PAWN_RUSH_COURSE01.md). 성한 님 레벨 디자인 v0.1([원문](Docs/KingRush/PAWN_RUSH_COURSE01_DESIGN_v0.1.md))의 모듈 15개 + 확장 3개(꺼짐)를 표 좌표 그대로 코드로 만들고(`Assets/Maps/PawnRush/Course01`), `Assets/Scenes/PawnRush/PawnRush_Course01.unity`에서 Play하면 조립된다. 메뉴 **ChessFight → Pawn Rush → Build Course01**은 모듈 프리팹을 굽고 씬에 배치·검증. 새 공용 기능: **등반 불가 면(`NoClimbSurface`, 래그돌 등반·잡기에 연결)**, 낙사 영역(`KillVolume` → 1.5초 뒤 체크포인트), 팀 장벽, 미니게임 슬롯·자리 표시 버튼, 장애물 초 단위 위상, F5 팀 바꾸기, 모듈 CSV. **Linux 컴파일(에디터 포함)만, Unity 미확인** → 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md) 최상단. 기획서 "먼저 결정할 것" 5개와 "결정할 것" 5개는 사용자 결정 대기
>
> **R67(사용자, 10-06): 폰 러시 맵 기획 재료집** [PAWN_RUSH_MAP_KIT](Docs/KingRush/PAWN_RUSH_MAP_KIT.md). 장애물 시험 씬(`ImportedChessFightMap`)의 구역·좌표, 장애물 19개와 씬 장치 29개의 실제 설정값, 래그돌 능력 수치를 모아 채팅 AI가 맵을 기획하게 한 문서(프롬프트·결과 양식 포함). 다음 단계: 사용자가 채팅에서 기획 → 구간별 표를 Claude Code에 주면 구현.
>
> **R65(사용자, 10-06): `main` 병합.** `main`은 이제 **`JY-kingrush`(UI·메뉴 디자인 C·설정 창·채팅·로딩·결과 화면·방장 선정·이전·빈자리 채우기·킹 러쉬 코스) 전체** + **`JY-ragdoll_v2`의 지성 님 맵 이식**(`ImportedChessFightMap.unity` = 폰러시 맵용 장애물 배치 시험 씬, 장애물 프리팹 19개, `Gameplay/ImportedObstacles`, 메뉴 `ChessFight → Imported Map`) + **`JY-lobby`의 R51**(RagdollTest 온라인에서 방장이 아닌 PC의 장애물이 방장 판정과 다르게 보이던 문제: 방장이 스냅샷에 장애물 시각을 싣고 참가자가 그 시각으로 장애물을 돌림, `HostObstacleClock`, magic `CFR6`)이다. 프로토콜 **v15**. 가져오지 않은 것: `JY-ragdoll_v2`의 `f476968`(나이트 능력 등), `JY-lobby`의 퀸 오브 더 힐 맵 8차·로비 재설계 등. Linux 자동 검사 통과, **Unity·Steam 미확인** → 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md) 최상단, [DECISIONS T5](Docs/Project/DECISIONS.md), [REQUIREMENTS R65](Docs/Project/REQUIREMENTS.md)
>
> **R64(승규 님, 10-05): 설정 창.** 인트로는 "클릭해서 시작" 판 클릭으로만 시작하고, **Esc는 인트로·로비·입장 화면에서 설정 창**(입장 화면 Esc는 더 이상 매칭을 취소하지 않음). 설정 창은 탭 5개·항목 10개(소리·화면·조작(키 바꾸기)·채팅·기타), `PlayerPrefs` 저장. 두꺼운 버튼 클릭 감 강화. **사용자 미확인** → [UI §12](Docs/Architecture/UI.md#12-설정-창-r64-2026-10-05), 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md) 최상단
>
> **R63(승규 님, 10-03): 메뉴 디자인 C "그랜드 아레나" 수정안.** 인트로·로비·채팅창·로딩 화면을 결과 화면과 같은 말과 **어두운 경기장 + 단풍나무·호두나무 체스판·놋쇠** 그림으로 다시 만들었다. 인트로 = 호두나무 받침대 위 킹과 금색 CHESS FIGHT, 로비 = 나무 체스판 무대·전광판, 오른쪽 모드 카드 **폰 러쉬·퀸 오브 더 힐·소드 파이트**(MODE 글씨 없음), **"상대 팀 찾는 중" 배너는 위 가운데**, 채팅 = 호두나무 판, **로딩 = 플레이어 입장**(준비된 사람부터 나무 무대로 걸어 들어오고 다 모이면 "모두 입장 완료 · 곧 출발!"). 모든 버튼은 누르면 쑥 들어갔다 튀어 오르는 입체 판(`ChunkyButtons`). 텍스처는 코드로 그리고 새 에셋은 글꼴 Black Han Sans(OFL) 하나. 모드 화면 이름 "킹 러시" → "폰 러쉬"(키·프로토콜 그대로). **2차(같은 날, 사용자가 Unity에서 본 뒤)**: 인트로는 예전 구성 + 나무 체스판·호두나무 판, 로비 전광판 삭제, **공개 매칭은 게임 시작부터 입장 화면**(배너 없음, 들어온 사람이 걸어 들어옴, 매칭 취소·Esc, 그대로 로딩으로), 경기 중 채팅창 투명. **3차**: 입장 화면에 "봇 추가 +"(개발 빌드·방장, 1명씩), 게임 시작 버튼은 "게임 시작"만. **AI가 이 PC Unity에서 화면을 봄(실제 공개 검색·취소, 비공개 방 + 봇 경기 진입 포함, 키·마우스는 안 누름), 사용자 미확인** → [UI §11](Docs/Architecture/UI.md#11-메뉴-디자인-c-그랜드-아레나-수정안-r63-2026-10-03), 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md) 최상단

> **R62(승규 님, 10-01, 10-01 푸시 `33f1648`): 결과 화면(라스트 씬). R76(`JY-kingrush`에서는 R65)에서 지우고 폰 러쉬 결과 씬 둘로 바꿈(위).** 경기가 끝나면 **이긴 팀은 모든 팀원이 승리 세리머니, 진 팀은 모든 팀원이 패배**를 본다. 새 씬 **`LastScene.unity`**(메뉴 **ChessFight > Scenes > Last Scene (result preview)**): Play하면 `LastSceneDirector`가 **승규 님이 준 PAWN RUSH 참고 그림체**의 결승 무대(크림 성·산호색 왕관 아치·체크 광장, 관중 없음)와 다리 달린 말, 결과판을 코드로 만든다. 이긴 팀 = 기물마다 다른 세리머니 + 꽃가루 대포, 진 팀 = 왕관이 튕겨 나가고 킹이 넘어진 뒤 도미노 + 비. 결과판 = 팀 목표, 결승 도착 순서(이긴 팀 필요 인원 4명 금색·4번째 "승리 확정"), 미도착자 남은 거리, H 숨기기, 12초 뒤 자동 로비, 다시 매칭·로비로. 혼자 열면 예시 경기로 미리보기(F1 이긴 팀, F2 진 팀, R 다시 재생). **경기 흐름에는 아직 안 이어짐**: 결과를 코스 결승에서 보여 줄지 이 씬으로 넘어갈지 승규 님이 아직 안 정함([ROADMAP §4](Docs/Project/ROADMAP.md)). Higgsfield 영상은 안 쓰기로 함([DECISIONS U9·U10](Docs/Project/DECISIONS.md)). **AI가 이 PC Unity에서 두 화면을 봄(키·버튼은 안 누름), 사용자 미확인** → [UI §10](Docs/Architecture/UI.md#10-결과-화면-r62-2026-10-01), 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md) 최상단

> **R61(승규 님, 09-30, 10-01 푸시): 채팅.** 채팅창 시안 A "기보 로그". 게임 전체에 **패널 하나**: **로비는 왼쪽**, **모든 경기 화면(킹 러시·소드 파이트, 나중의 퀸 오브 더 힐)은 오른쪽 아래**. 평소엔 `Tab 채팅`과 8초 동안 뜨는 새 줄만, **Tab으로 열고, Tab으로 파티 → 팀 → 전체, Enter 보내기, Esc나 닫기 버튼으로 닫기**(경기에서는 보내면 닫힘, 열린 동안 캐릭터 멈춤). 보낸 사람은 **Steam 이름**, 1초에 한 줄·80자. 서버 없이 Steam 로비 채팅(파티 로비·경기 방), 팀 줄은 같은 팀 화면에만. 프로토콜 v14 그대로. 로비 첫 클릭이 엉뚱한 버튼을 누르던 기존 대체 클릭 문제도 고침. 1차(늘 보이는 로비 창)는 사용자가 Unity에서 써 보고 지금 모양으로 바꾸라고 함. **2차는 AI가 이 PC Unity에서 로비·킹 러시·소드 파이트 화면을 봄(키는 못 눌러 코드로 열고 닫음). 키 입력·한국어 입력기·두 PC·사용자 미확인**. 테스트 중 채팅 때문에 로딩 화면이 안 뜨던 문제를 찾아 고침(`3bf8981`, [PITFALLS 24](Docs/Environment/PITFALLS.md)) → [UI §9](Docs/Architecture/UI.md#9-채팅-r61-2026-09-30), [SESSION §9](Docs/Network/SESSION.md#9-채팅-r61-2026-09-30), 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md)의 채팅 표

> **R60(승규 님, 09-30): 시작한 경기에서는 나간 사람만 나가고, 빈자리는 4분까지 받는다.** 파티원·파티장·방장 누구든 경기 시작 뒤 나가면 그 사람만 빠지고 파티원은 경기를 계속한다(방장이면 방장 이전). 먼저 나온 파티원은 파티에서 기다리고, 파티장은 경기 중인 파티원이 돌아올 때까지 매칭을 못 한다. 시작 전(검색·대기실)은 예전처럼 파티가 함께 취소 → [SESSION §6](Docs/Network/SESSION.md#6-취소퇴장호스트-이탈), [DECISIONS K10](Docs/Project/DECISIONS.md)

> **R59(승규 님, 09-30): 경기 중 빈자리 채우기.** 경기가 시작된 뒤 누가 나가면 그 자리를 **매칭 중인 다른 사람**이 채운다. 팀마다 **시작 인원까지만**(6 대 6이면 6명, 5 대 5면 5명), 사람이 빠진 팀에, 파티는 한 팀에 통째로, 공유 출발 시각부터 4분까지(R60). 매칭은 빈자리 경기를 먼저 시도하고, 방장이 바뀌어도 자리 정보(`seats`·`held`)는 방에 있어 이어진다. 프로토콜 **v14**(검색 필터 `open=1`). **코드·테스트만, Unity·Steam 미확인** → [SESSION §8](Docs/Network/SESSION.md#8-빈자리-채우기-r59-2026-09-30), 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md)의 빈자리 표(PC 2대 이상 필요)

> **R58(승규 님, 09-30): 로비 → 경기 씬 로딩 화면과 동시 출발.** 예전 동기 `LoadScene`이 모든 PC를 5~10초 멈추던 것을 `MatchLoader`가 로딩 화면 뒤 `LoadSceneAsync`로 바꿈. 화면은 시안 A의 UI(PAWN RUSH 제목·과반 규칙·8칸 줄에서 폰이 2→8랭크, 준비되면 퀸 승격·플레이어 점·팁) + 시안 B의 배경(메뉴 체스판 + 가운데 마주 보는 두 폰). 모두 준비(멤버 `load`=100)되거나 방장 준비 뒤 20초가 지나면 방장이 공유 Steam 시계 시각 `go`를 정하고 모두 같은 순간에 걷힌다. 프로토콜 **v13**(R59 뒤 v14). **코드·테스트만, Unity·Steam 미확인** → [UI §8](Docs/Architecture/UI.md#8-로딩-화면-r58-2026-09-30), 확인 목록 [VALIDATION](Docs/Network/VALIDATION.md)의 로딩 화면 표. 다른 채팅의 **`JY-gpt_gamemode`**(소드파이트)·**`JY-ragdoll_v2`**(Claude 물리 튜닝)는 보존. 기준 커밋: 이 파일을 갱신한 커밋(`git log -1 -- HANDOFF.md`)

> **병합(2026-09-28, 승규 님 요청):** `feature/ui-sample-b`(= `JY-lobby`의 R49 `c82abfa`: 퀸 오브 더 힐 래그돌 M6·M11·M13, 퀸 오브 더 힐 그레이박스 맵 7차, 로비·인트로 샘플 B 디자인)를 이 브랜치에 합쳤다. 네트워크 프로토콜은 두 쪽(소드파이트 v10, 기물 종류 스냅샷 v7)을 합쳐 **v11**. **R 번호 주의:** R44 이후는 두 계열이 따로 매겼다. 소드파이트·킹 러쉬 쪽(`JY-gpt_gamemode`·`JY-kingrush`)의 R44~R56과 퀸 오브 더 힐·로비 쪽(`JY-ragdoll_v2`·`JY-lobby`)의 R44~R49는 서로 다른 요청이다. [REQUIREMENTS](Docs/Project/REQUIREMENTS.md)에서 계열별 표로 나눠 두었다. **이어서 `claude/host-migration`(방장 선정·이전, `JY-lobby` 계열 R50)도 합쳐 프로토콜은 최종 v12**다 → [Network/HOST](Docs/Network/HOST.md).

> **최신 R56:** 사용자는 직접 테스트를 나중으로 미루고 다음 단계 진행을 요청했다. 같은 `KingRushOpening.unity`에 **왕의 계단→4자리 승격/10초 집결→체크메이트 결승**을 연결했다. **F8 계단 / Shift+F8 결승**, 기존 F7 집결/F5 성벽/Shift+F5 시소/F4 상자 유지. [R56 실행·규칙·자동 결과](Docs/KingRush/FINAL_COURSE.md). 왕좌8초 누적/시간별 붕괴/탑 재진입/초읽기/승패의 오프라인1차 구현. **R55/R56 사용자 확인 대기**, 공통 물리/로비/온라인 보존. 최종 아트·음향·자세/나머지 능력은 후속이다.

> **(옛 결정, R66으로 대체) 이 작업 폴더의 결정(R52):** 킹 러쉬는 **`JY-kingrush`**에서만 작업·커밋한다. 사용자 정정에 따라 **`JY-gpt_gamemode@852eb2b`**(소드파이트·로비 연결 포함)에서 분리했다. 폴더는 `C:/Users/trews/.codex/worktrees/jy-kingrush/ChessFight`. 다른 채팅의 소드파이트와 Claude 미완성 변경은 건드리지 않는다. **오프라인 능력 시험장(R52/R53)과 첫 연결 코스(R54)**를 구현했고 로비 KingRush 선택은 아직 예전 캡슐 코스다. [KingRush README](Docs/KingRush/README.md)를 먼저 읽는다. 알려진 CLI 푸시403은 재시도하지 않고 사용자 GitHub Desktop에서 Push한다.

> **최신 브랜치 결정(R44):** 사용자는 `JY-lobby`로 합치지 않고, `JY-ragdoll_v2`의 마지막 커밋 `cdcc9fe`에서 **`JY-gpt_gamemode`**를 만들도록 요청했다. 아래와 다른 문서의 예전 "게임모드는 JY-lobby" 지시보다 이 결정이 우선한다. 물리 튜닝은 끝난 것으로 보지 않으며 GPT가 임의로 이어서 수정하지 않는다.

> **최신 요청(R51): 검이 몸에 가리지 않게 조금 비껴 들고, 무게감 있는 선 보정형 드래그, 강한 베기만 넉다운.** 오른쪽24°·위12° 검 자세, 시점 목표를 감쇠 추종/따라잡기, 회전 에너지·드래그 폭·평활 입력 속도로 약한 접촉 배제. 클릭 베기/F6·Esc 전환·빠른 발도 유지. 공통 Pawn/카메라 코드·튜닝 보존. CFS3/CFR4 그대로, **Steam v10**(병합 뒤 v11). R50 사용자 검 가림/손맛 피드백 반영이며 **R51 사용자/두 PC 미확인**. 맵은 마지막, 누운 적 강한 추가타 허용. [실행·검사](Docs/GameModes/SwordFight/README.md).

> **합쳐 온 `JY-lobby` 쪽 기록(R49까지):** 기준 브랜치 **`JY-lobby`**(로비·게임모드·퀸 오브 더 힐 맵 작업, `JY-ragdoll_v2`를 합침), **`JY-ragdoll_v2`**(퀸 오브 더 힐 래그돌 기능, 캐릭터 조작·물리 변경). 최종 R49(09-27): 퀸 오브 더 힐 맵 7차(7개 층 + 8랭크).

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
  - **(`JY-lobby`, R47·R48) `JY-ragdoll_v2`를 `JY-lobby`에 합침(빨리 감기, 09-27)**, 그 위에 **퀸 오브 더 힐 그레이박스 맵 7차(R49)**: **7개 층(1~7랭크) + 정상 8랭크(여덟 칸 승격)**, 층마다 두세 갈래 길(대계단·아치 회랑, 체크 곤돌라·사슬 사다리, 석판 나선·나이트·비숍, 기울어 도는 궤도 고리·룩의 탑, 시계 관람차·시계판·스프링, 공중 정원·그네, 두 갈래 나선 계단·빛의 계단). 맵은 데이터(`Tools/QueenHill/build_layout.py` → JSON)로, Play 때 조립. Unity 없이 미리보기 → [GRAYBOX](Docs/GameModes/QueenOfTheHill/GRAYBOX.md)
  - **(`JY-ragdoll_v2`) 래그돌 조작감 개선(R36)**: 버둥대기 클릭 게이지, 머리부터 다이빙 태클, 더 기민한 이동, 일어날 때 안 끌림, 부딪히면 튕김, 역경사·턱 등반 버그, 조작감 녹화 도구 → [RagdollLab README](Docs/RagdollLab/README.md#조작감-개선-2026-09-26-r36-jy-ragdoll_v2)
  - **(`JY-ragdoll_v2`, 병합으로 이 브랜치에도 들어옴) 퀸 오브 더 힐 래그돌 기능 M1~M11, M13**: **M11 승격·기물 성능, M13 찌그러짐·비틀, M6 밧줄·사슬·그네, M7 도약대·태엽 스프링, M8~M10 개척의 종·체크포인트·팀(AI가 이어서, Unity 미확인)**, **M5 갈고리(E 꺼내기·좌클릭 꾹 돌리기·던져 끌려가기)와 앙파상**, 탈것(움직이는 발판·벽), 입력 확장(능력 E·Q, 상호작용 F, 조준), 피격 약속, 물·부활, RagdollTest의 [7] 시험대 → [Player/RAGDOLL §8](Docs/Player/RAGDOLL.md#8-퀸-오브-더-힐-기능-m1m11-m13)
  - **(`JY-lobby`, 병합으로 들어옴) 퀸 오브 더 힐 그레이박스 맵 7차와 로비·인트로 샘플 B 디자인**(체스판 무대, 체스 말 캐릭터, 오른쪽 게임 모드 목록)
- **(`claude/host-migration`, `JY-lobby` 계열 R50, 승규 님 요청, 병합으로 들어옴) 방장 선정·이전**: 경기 시작 때 성능(벤치마크·하드웨어·프레임)과 핑으로 가장 적합한 PC가 방장, 방장이 나가거나 튕기거나 4초 응답이 없으면 다음 방장이 이어받아 경기 계속, 계속 느린 방장은 더 좋은 기계로 넘김. 캡슐·래그돌 랩 둘 다. 렉 줄이기(스냅샷 일정 전송, 수신 끝까지, 래그돌 방장 과부하 보호) 포함. 프로토콜 v12(이 브랜치, `claude/host-migration`에서는 v8). **코드·테스트만, Unity·Steam 미확인** → [HOST](Docs/Network/HOST.md)
- **로비에 연결된 킹러시는 아직 캡슐 기술 프로토타입**이다. 새 오프라인 킹러시는 능력 시험장과 **장난감/상자(R54)→성벽/시소·승격 집결(R55)→왕의 계단/결승(R56)**의 연결 씬을 제공한다. 온라인/최종 아트는 미구현. 소드파이트는 다른 채팅에서 계속하며 여기서는 변경하지 않는다. 퀸 오브 더 힐은 **오프라인 그레이박스 씬**만 있다(네트워크 래그돌 M14가 없어서 로비에서는 아직 "준비 중").
- **병합 주의:** 킹 러쉬(`KingRushPiece`/`KingRushPawn`)는 M6·M11·M13이 없던 `852eb2b`를 기준으로 만들어졌다. 병합 뒤 두 기능이 한 래그돌에서 함께 도는지는 **자동 점검·Unity 모두 아직 안 봤다.**
- 이 프로젝트의 AI 작업은 사용자(메인 기획자, GitHub `dua0731-cmd`)의 요청으로 진행되어 왔다. **요청 이력 전체는 [요구사항 기록](Docs/Project/REQUIREMENTS.md)에 있다.**

## 1. 현재 상태 스냅샷

| 항목 | 상태 |
|---|---|
| **폰 러시 코스 01 v0.4(10-08, R86)** | 같은 `PawnRush_Course01.unity`. `Course01v4Builder`(+`.Sections`), `MissionStation`, `TeamGate`, `MissionGames` + `MgPaint`·`MgPlanks`·`MgDrawbridge`·`MgCapstan`·`MgBells`, `MissionPicker`, `PlazaEntry`, `ObstacleSpan`, v0.4 `Course01Validator`, 메뉴 Build Course01 v4. Core `PawnRushMissions`(+테스트 3). 재질 `Board_Light`·`Board_Dark`. 삭제: `Course01v2Builder*`, `MiniGameSlot`·`SlotEntry`·`SlotDoor`·`MiniGamePlaceholder`. asmdef `ChessFight.PawnRush`(·`.Editor`)가 `ChessFight.Network.Core` 참조. Core 91·세션 40, PawnRush 런타임·에디터 컴파일 통과(이 브랜치의 Game은 Unity 6 전용 API라 Linux `--compile`이 Game에서 멈춤 — 그 전 Game DLL로 따로 확인). 브랜치 `claude/bold-johnson-8ez95n`. **Unity 미확인** |
| **이름 바꾸기(R85, 10-08)** | 인트로 "이름 바꾸기"·로비 오른쪽 위 내 이름 → `NameChange` → 이름 화면 바꾸기 모드(`NameScreen.Build(이름, true)`, `Cancelled`) → 누른 화면으로. 바꾸는 동안 로비 단축키·대체 클릭·채팅 쉼(`NameScreen.Showing`), 매칭 중 막음·파티장이 매칭을 시작하면 닫힘(`NameChange.Abort`). Shift+클릭 없앰. 실제 DLL 컴파일 통과, AI가 Unity 캡처로 흐름 확인. **사용자 미확인** |
| **시작 로고·이름 설정 화면·장면 전환(R84, 10-08)** | `claude/intro-name-transition` → `claude/bold-johnson-8ez95n`. 게임을 켜면 `LogoIntro`가 시안 영상(`StreamingAssets/LogoIntro.mp4`, 소리 A)을 인트로 위에서 한 번. 인트로 판 클릭 → 이름이 없으면 같은 씬의 `NameScreen`(배경 그림 `NameBackdrop.jpg`, 도는 유리 폰 `GlassPawn`, 추천 이름 말풍선, 규칙 `Core/PlayerNames`) → `SceneTransition`(그림 `TransitionBoard.jpg`, 킹 구멍 닫힘 → 카드 → 나이트 구멍 열림) 뒤 로비. 이름은 `PlayerProfile`(PlayerPrefs `cf.name`) → `SteamSession.LocalName` → 멤버 데이터 `nick`(프로토콜 그대로). 스타일은 `IntroFlow.uss`(로비 `NetworkHud.uss` 안 건드림). Core 88·세션 40·실제 DLL 컴파일 통과, AI가 Unity Play 캡처로 흐름 확인(`a78f5d6`에서 로고 시작 고침). **사용자 미확인** |
| **로비 UI A 시안(10-08, R83)** | `claude/bold-johnson-8ez95n`. `NetworkHud.uxml`·`.uss` 로비 부분, `NetworkHudView.Dress`, `MenuArt.Ramp`·`LineCard`, `MenuMarks.IconMark`, 로고 `Resources/Menu/LobbyLogo.png`(LFS). 3D 무대·인트로·입장·결과·설정 창은 그대로. Core·세션·실제 DLL 컴파일 통과. AI가 Unity에서 로비를 Play해 캡처로 시안과 비교. **버튼 누르기·매칭·사용자 미확인** |
| **폰 러쉬 결과 화면(R76, 10-06 → 10-07 이 브랜치에 병합)** | `claude/menu-c` → 원격 `JY-kingrush`(거기서는 R65, `5972c7e`·`12772fe`) → 10-07 `claude/bold-johnson-8ez95n`에 병합. R62 `LastScene`(씬·연출·결과판 코드)을 지우고 **`PawnRushVictory`·`PawnRushLose`**(같은 `PawnRushResultDirector`, `victory`만 다름): 램프 아래 탁자 체스판·두 팀·세리머니/쓰러짐, 결승 중계 결과판(결승 순서 띠·점수판·미도착·12초·다시 매칭·로비로·H 숨기기). 시안 three.js·CSS 그대로, 새 에셋 없음, 프로토콜 그대로. 병합 전 Core 81·세션 39·실제 DLL 컴파일 통과, AI가 Unity에서 두 씬을 시간대별로 캡처해 시안과 비교. 병합 뒤 Windows에서 Core 83·모의 세션 39·실제 Unity DLL 전체 어셈블리 컴파일 통과(`Test-NetworkCompile.ps1`이 스킬 하위 폴더 `RagdollLab/Scripts/PawnRushSkills`를 안 읽어 이 브랜치에서 실패하던 것을 같이 고침). **경기 흐름 미연결(결정 대기), 키·버튼·사용자 미확인** |
| **기물 스킬(10-07, R70 기획 · R71 구현)** | 시험 씬 `PawnRush_SkillTest.unity` + `RagdollLab/Scripts/PawnRushSkills/`(스킬 partial·수치·시험장 창·밧줄·바리케이드·예고선·자동 시험 도구) + 메뉴. 폰·퀸·룩·비숍·나이트, 킹 없음. 이 씬에서만 켜짐. AI가 Unity MCP로 자동 시험(실측이 기획 수치와 맞음). **사용자 미확인**. 남은 결정 D4~D11, 스킬 키 재설정, 폰 이펙트 고르기·소리(R73에 퀸 A·룩 A·비숍 B·나이트 B 만듦) |
| **폰 러시 코스 01 v0.2(10-07, R69, R86으로 대체)** | 같은 `PawnRush_Course01.unity`. `Course01v2Builder`(+`.Sections`) 월드 좌표, `ProgressPath`, `CourseFloor`, v0.2 `Course01Validator`, 메뉴 Build Course01 v2. Gameplay: `FallDistanceRespawn`·`TeamZone` 추가, `FinishZone` 아무 트리거·골반만, `PlaytestSpawner` 낙차·팀 구역 복귀. 삭제: `Course01Modules*`, `Course01Layout`(+에셋), `CourseModule`, `Course01ModuleBuilder`. Core 83·세션 39·PawnRush 런타임·에디터 컴파일 통과. 브랜치 `claude/bold-johnson-8ez95n`. **Unity 미확인** |
| **폰 러시 코스 01(10-06, R68)** | `PawnRush_Course01.unity` + `Assets/Maps/PawnRush/Course01`(asmdef `ChessFight.PawnRush`·`.Editor`): 모듈 18개(코드, Build Course01로 프리팹), 키트 스크립트 18개, 셰이더 2·재질 8, 생성기 `gen_pawnrush_course01.py`. 공용 변경: `NoClimbSurface`·`KillVolume`(Gameplay/Course), `RagdollPawn`·`PawnHand` 등반 불가, 장애물 `phaseOffset`·원판 방향, `PlaytestSpawner` 낙사·`SetTeam`, `Checkpoint.Configure`. Core 83·세션 39·전 어셈블리 + PawnRush 에디터 컴파일 통과. 브랜치 `claude/bold-johnson-8ez95n`(세션 지정, `main` 병합은 사용자 확인 뒤). **Unity 미확인** |
| **`main` 병합(10-06, R65)** | `main` = `JY-kingrush`(`f3b5cc8`) + `a54b89e`(맵 이식, 원본 `487f62b`) + `104b18c`(방장 장애물 시각, 원본 `JY-lobby` `27e052c`) + `deabde0`(Linux 검사 유지) + 문서. Core 83·세션 39·7개 어셈블리 Roslyn 컴파일(2021.3 참조 DLL) 통과. **Unity·두 PC 미확인** |
| **설정 창(10-05, R64)** | `claude/settings-window` → 원격 `JY-kingrush`에 푸시. 인트로 판 클릭 시작, Esc 설정 창(인트로·로비·입장 화면), 탭 5개·항목 10개, `GameSettings`(`PlayerPrefs`), 클릭 감 강화. Core 81·세션 39·DLL 컴파일 통과. **사용자 미확인** |
| **메뉴 디자인 C 수정안(10-03, R63, 3차 포함)** | `claude/menu-c`(`33f1648`에서 갈라짐) → 원격 `JY-kingrush`에 푸시. 인트로(예전 구성 + 나무)·로비(전광판 없음, 게임 시작 버튼 글자만)·공개 매칭 = 입장 화면(봇 추가 +) → 그대로 로딩·채팅(경기 중 투명)을 호두나무·놋쇠 그림으로, 입체 판 버튼, 결과 화면 말. 새 에셋은 글꼴 하나(`Resources/Fonts`, LFS). 프로토콜 그대로. Core 81·세션 39·실제 DLL 컴파일 통과. AI가 Unity에서 화면을 봄. **키·버튼·실제 매칭·사용자 미확인** |
| **채팅(09-30, R61)** | `claude/pawnrush-loading`(10-01 원격 `JY-kingrush`에 푸시 `e64012e`): 파티·팀·전체 채팅, 게임 전체에 패널 하나(`NetworkRuntime`의 `ChatBox`): 로비 왼쪽, 모든 경기 씬 오른쪽 아래. 평소 숨김, Tab 열기·Tab 채널·Enter·Esc/닫기 버튼, Steam 이름. Steam 로비 채팅으로 전달, 프로토콜 **v14 그대로**. Core 79·세션 39·실제 DLL 컴파일 통과. 1차(늘 보이는 창)는 사용자가 Unity에서 봄. 2차는 AI가 Unity에서 로비·킹 러시·소드 파이트 화면을 봄. **키 입력·사용자·두 PC 미확인** |
| **빈자리 채우기·나간 사람만 나가기(09-30, R59·R60)** | `claude/pawnrush-loading`: 시작한 경기에서 나간 자리를 매칭 중인 사람이 채움. 팀마다 시작 인원까지(공개는 6 대 6), 파티는 한 팀, 4분까지. 경기 중에는 나간 사람만 나가고 방장이면 방장 이전. 프로토콜 **v14**(경기 `open`·`seats`·`held`, 파티 멤버 `match`). Core 77·세션 37·실제 DLL 컴파일 통과. **Unity·Steam 미확인** |
| **로딩 화면(09-30, R58)** | `claude/pawnrush-loading`: 로비 → 경기 씬을 로딩 화면 뒤 비동기로, 모두 준비 뒤 공유 시각에 동시 출발. 프로토콜 **v13**(멤버 `load`, 경기 `go`). Core 75·세션 29·실제 DLL 컴파일 통과. **Unity·Steam 미확인** |
| **병합(09-28)** | `feature/ui-sample-b`(`c82abfa`)를 합침. 네트워크 프로토콜 **v11**. 병합 뒤 자동 검사 결과는 [HISTORY](Docs/Project/HISTORY.md)의 병합 줄. **Unity·두 PC 미확인** |
| **이 폴더: 킹 러쉬 R56** | **`JY-kingrush` / 기반 `852eb2b`**. `KingRushOpening.unity`: 구간 프리팹24개, 체크포인트16곳, 승격2/3/4자리. 왕의 계단/최종 집결/왕좌 쟁탈·붕괴·재진입·승패 추가. **R55/R56 사용자 확인 대기**, 자동 결과는 FINAL_COURSE. 밧줄은 임시 다리; 나머지4능력·온라인·최종 아트는 후속 |
| 개발 브랜치 | **`main`(R66, 10-06 사용자). 모든 AI 작업은 `main`에 커밋·푸시한다.** (옛 기록) **GPT 게임모드 작업은 `JY-gpt_gamemode`에만 커밋·푸시**(09-27 사용자, R44). 출발점은 로컬 `JY-ragdoll_v2`의 `cdcc9fe`. 원본 폴더의 브랜치는 `JY-ragdoll_v2`로 유지하고, 새 브랜치는 별도 작업 폴더(worktree)에서 작업한다. `JY-lobby`·`main`·`Network`로 병합하거나 푸시하지 않는다. 미완성 튜닝의 후속 반영은 별도 결정 |
| GPT의 현재 작업 | **R51 보이는 검·무게감 있는 보정 드래그·강한 베기만 넉다운**. 이전 클릭/F6 전환·빠른 발도·정지 더미·장외/부활 유지. 공통 물리 튜닝 변경 없음. [실행](Docs/GameModes/SwordFight/README.md) |
| GPT 프로토콜·자동 검사 | **v10/CFS3**(병합 뒤 v11), 최대506바이트 칼/경기 상태에 방식 선택 포함. CFR4 레이아웃 유지, 소드파이트 채널에서만 ability2=선택 절대값. Core **42**·모의 Steam **20**·실제 DLL 컴파일 통과. Windows 플레이어 **51/0**, 시점 보정부터 강한 명중까지·약한 접촉·검 가시성·전환·12칼 검사. [최종 실행/측정](Docs/GameModes/SwordFight/README.md). R51 사용자/2PC 미확인. 아래 v6·래그돌 숫자는 원본 기록 |
| 합쳐 온 `JY-lobby` 쪽 브랜치 규칙 | `JY-lobby` = `JY-ragdoll_v2`(`41b7d11`)를 빨리 감기로 합친 것 + 그 뒤 작업(R47). 퀸 오브 더 힐 맵은 `JY-lobby`에서 만든다(R48). 로비·게임모드 작업은 `JY-lobby`에 커밋·푸시(R18. `main`·`Network`·`JY-ragdoll`에는 푸시 금지). 퀸 오브 더 힐의 래그돌 쪽 기능은 `JY-ragdoll_v2`(R34). 그 밖의 AI 작업은 `Network` |
| `main` | **10-06 R65 병합 결과**(위 줄). 그 전에는 `0df4403`(R17 병합)에 멈춰 있었다. `JY-kingrush`·`JY-lobby`·`JY-ragdoll_v2`는 모두 `0df4403`에서 갈라졌다. `Network`(`0ecd3b6`)는 `main`보다 뒤에 있다 |
| 다른 원격 브랜치 | `Network`(`0ecd3b6`, main에 포함됨), `JY-ragdoll`(준영, 래그돌 랩·**물리 튜닝용으로 유지**, R17), `킹을-지켜라`(**비호환**: Unity 6000.3.12f1·URP·uGUI·자체 Steam 전송), `SteamNetworkTest`(옛 실험) |
| 네트워크 프로토콜 | **`main`(10-06, R65): v15** = v14 + 래그돌 랩 스냅샷 헤더에 방장의 장애물 시각(magic `CFR6`, 헤더 22 → 30바이트. `JY-lobby`에서는 v8). **R59(09-30) 뒤: v14** = v13 + 빈자리 채우기(경기 `open`·`seats`·`held`, 검색 필터 `open=1`, 패킷 그대로). **R58(09-30) 뒤: v13** = 아래 v12 + 로딩 화면 동시 출발(멤버 `load`, 경기 `go`, 패킷 그대로). **이 브랜치(병합 뒤): v12** = 소드파이트 규칙(v10, CFS3) + 래그돌 랩 스냅샷의 기물 종류(v7, `CFR5`)(여기까지 v11) + 방장 선정·이전(`claude/host-migration`의 v8). **`JY-lobby`: v7**(09-27 `JY-ragdoll_v2`를 합쳐서. v3에서 게임 모드 추가). **`JY-ragdoll_v2`: v7**(래그돌 랩 입력 패킷 27바이트에 좌클릭 누르고 있기 비트, 스냅샷 폰당 74바이트에 버둥대기 게이지·갈고리·기물 종류 포함, magic `CFR5`). `main`·`Network`는 v2. 캡슐 입력 패킷 magic `CFF2`(변경 없음). 다른 프로토콜 빌드와는 매칭 불가 |
| 방장 이전 브랜치 | **`claude/host-migration`**: `JY-lobby`(R49 `c82abfa`)에서 갈라져 R50 방장 선정·이전만 더한 것. 승규 님 채팅에서 만듦. 2026-09-28 사용자 지시로 이 브랜치(`JY-kingrush`)에 합침. **푸시는 사용자 지시가 있을 때만** |
| 자동 테스트 | **이 브랜치(두 번째 병합 뒤, 09-28): Core 71·모의 세션 28·실제 Unity DLL·Steamworks 소스 전 어셈블리 컴파일·경계 검사 통과. 래그돌 자동 점검은 병합한 코드로 아직 안 돌림.** **`claude/host-migration`(09-28): Core 41·세션 23·실제 DLL 컴파일 통과.** **`JY-lobby`(09-27, R49): 경계 검사 + 퀸 오브 더 힐 맵 JSON 최신 검사 + Core 36개(퀸 오브 더 힐 코스 2개) + 모의 세션 15개 + 7개 어셈블리 Roslyn 컴파일 통과.** 이전 기록: 어셈블리 경계 검사 + Core 34개(09-27 `JY-ragdoll_v2`, 퀸 오브 더 힐 규칙 3개·기물 2개 포함; `JY-lobby`는 29개) + 모의 Steam 세션 15개 통과, 래그돌 포함 7개 어셈블리 Roslyn 컴파일 통과 (2026-09-25, `JY-lobby`). **`JY-ragdoll_v2`(09-26)**: 같은 테스트 + **실제 Unity 6000.3.11f1 DLL로 전 어셈블리 컴파일**(랩 Steam 다리·빌더 포함) 통과, **빌드한 랩 플레이어의 래그돌 자동 점검 56 통과 / 0 실패**(09-27, R36 조작감 개선 + R37 원래부터 실패하던 9개 정리를 합친 뒤, Core 29·세션 15 통과 → [RagdollLab README](Docs/RagdollLab/README.md#자동-점검)) |
| Unity 실기 확인 | 2026-09-24까지: 로비 HUD 표시·한글·클릭·친구 초대 동작 (사용자 보고) |
| Unity 실기 확인 (추가) | **퀸 오브 더 힐 M1~M4 시험대 1~12번 성공**(2026-09-26 사용자, `JY-ragdoll_v2`). 두 PC(13번)는 못 함 |
| Unity 실기 확인 (GPT) | **소드파이트 선택→매칭 정상, R48 더미로 혼자 시험 가능, R49 물리 시스템은 잘 구현됨**(09-27 사용자). R50 플레이 뒤 **검 가림·반응·약한 접촉** 피드백. R51 개선은 사용자 재확인 대기, 전환/네트워크 전체 성공으로 확대하지 않음 |
| **Unity 미확인** | **로비 UI A 시안(10-08, R83: AI만 화면을 봄)**, **폰 러쉬 결과 화면(R76: AI만 화면을 봄)**, **메뉴 디자인 C(10-03, R63: AI만 화면을 봄)**, **이 병합 전체(09-28)**, **방장 선정·이전(09-28, `claude/host-migration`)**, **퀸 오브 더 힐 맵 7차(09-27, `JY-lobby` R49)** (R48 그레이박스는 사용자가 Unity에서 봄 → 단조롭다는 의견으로 7차로 교체), **로비·인트로 샘플 B(`c82abfa`)**, **래그돌 등반·질주 수정(09-27, R36: 등반 시작·손 높이·턱 위 되튐·곡면 박치기·질주 지침)**, **퀸 오브 더 힐 2차 수정(피격 뒤 1초 누워 있기, 물에 5초 둥둥 뜨기·버둥, 09-26)**, **새 로비·게임 모드(09-25, JY-lobby)**, 씬 분리(72ddf9e), 연결 품질 기능(01dd655), 래그돌 병합(09-25)은 **아직 Unity에서 사람이 보지 않았다.** 확인 목록: [VALIDATION.md](Docs/Network/VALIDATION.md) 최상단 표들 |
| 두 PC 확인 | 파티 입장·공개 매칭 성사 확인(2026-09-22). **양방향 이동은 미보고** |

## 2. 무엇이 되고 무엇이 안 되는가

| 기능 | 상태 | 상세 문서 |
|---|---|---|
| **이름 바꾸기**(R85): 인트로의 "이름 바꾸기"·로비 오른쪽 위 내 이름에서 언제든, 바꾸기 모드의 이름 화면 → 원래 화면으로 | 코드·AI의 Unity 캡처, 사용자 미확인 | [UI §15](Docs/Architecture/UI.md#15-시작-로고이름-설정-화면장면-전환-r84-2026-10-08) "이름 바꾸기" |
| **시작 로고·이름 설정·장면 전환**(R84): 게임 실행 때 로고 영상(소리 A, 건너뛰기), 처음 한 번 이름 설정(추천 이름 말풍선·규칙), 인트로·이름·로비 사이 광택 체스판 전환(킹 구멍 → 카드 → 나이트 구멍), 고른 이름을 다른 사람에게도(`nick`) | 코드·자동 검사·AI의 Unity 캡처, 사용자 미확인 | [UI §15](Docs/Architecture/UI.md#15-시작-로고이름-설정-화면장면-전환-r84-2026-10-08) |
| **킹 러쉬 연결 코스 R54~R56, 능력 시험장R52/R53** | 장난감/상자→성벽/시소→왕의 계단/왕좌 결승, 승격 집결10초·자동 합류. R54 일반 피드백 수신, **R55/R56 사용자 미확인**. 기존 조작/튜닝·다른 모드/로비 보존 | [R56](Docs/KingRush/FINAL_COURSE.md), [R55](Docs/KingRush/CASTLE_COURSE.md), [능력 시험장](Docs/KingRush/README.md) |
| **로딩 화면·동시 출발**(R58): 로비 → 경기 씬 비동기 로드, A 시안 UI + B 시안 배경, 모두 준비 또는 20초 뒤 공유 시각 `go`에 동시 해제 | 코드·테스트만, Unity·Steam 미확인 | [UI §8](Docs/Architecture/UI.md#8-로딩-화면-r58-2026-09-30) |
| **경기 중 빈자리 채우기**(R59): 나간 자리를 매칭 중인 사람이 채움(팀마다 시작 인원까지, 파티는 한 팀, 시작 뒤 4분까지, 비공개 방은 방 번호로). R60: 경기 중에는 나간 사람만 나감(방장이면 방장 이전) | 코드·테스트만, Unity·Steam 미확인 | [SESSION §8](Docs/Network/SESSION.md#8-빈자리-채우기-r59-2026-09-30) |
| **메뉴 디자인 C 수정안**(R63): 인트로·로비·매칭 배너(위 가운데)·채팅·로딩(플레이어 입장), 호두나무·놋쇠, 입체 판 버튼, 모드 이름 폰 러쉬·퀸 오브 더 힐·소드 파이트 | 코드·테스트. AI가 Unity에서 화면을 봄(매칭·로딩은 가짜 상태), 키·버튼·실제 매칭·사용자 미확인 | [UI §11](Docs/Architecture/UI.md#11-메뉴-디자인-c-그랜드-아레나-수정안-r63-2026-10-03) |
| **로비 UI A 시안**(R83): 이름 설정 화면 말투(어두운 판·금 테·선 아이콘·은색 제목·아이보리 게임 시작), 3D 무대는 그대로 | 코드·컴파일. AI가 Unity에서 로비 화면과 매칭·창 상태를 캡처로 봄, 버튼 누르기·사용자 미확인 | [UI §14](Docs/Architecture/UI.md#14-로비-ui-a-시안-r83-2026-10-08) |
| **폰 러쉬 결과 화면**(R76, R62를 대신함): `PawnRushVictory`(이긴 팀)·`PawnRushLose`(진 팀) 씬, 램프 아래 탁자 체스판에 두 팀(세리머니 / 왕관 날아가고 쓰러짐·주저앉음), 결승 중계 결과판(결승 순서 카드·팀 점수판·미도착·H 숨기기·12초 자동 로비·다시 매칭·로비로) | 코드·테스트. AI가 Unity에서 두 씬을 시간대별로 멈춰 봄(키·버튼 안 누름), 사용자 미확인. **경기 흐름에 안 이어짐**(결과를 어디서 보여 줄지 결정 대기) | [UI §13](Docs/Architecture/UI.md#13-폰-러쉬-결과-화면-r76-2026-10-06) |
| **채팅**(R61): 파티·팀·전체, 로비 왼쪽·모든 경기 화면 오른쪽, 평소 숨김·Tab으로 열기·Esc로 닫기, Steam 이름, 1초에 한 줄 | 코드·테스트. 1차는 사용자가 로비에서 봄. 2차(Tab 방식·경기 화면)는 AI가 Unity 화면으로 봄, 키 입력·두 PC는 미확인 | [UI §9](Docs/Architecture/UI.md#9-채팅-r61-2026-09-30), [SESSION §9](Docs/Network/SESSION.md#9-채팅-r61-2026-09-30) |
| Steam 파티(최대 6)·초대·번호 입장 | 동작 확인 | [Network/SESSION](Docs/Network/SESSION.md) |
| 공개 자동 매칭 6v6 (파티 단위 같은 팀 예약) | 두 PC 성사 확인, 12인 미확인 | [Network/SESSION](Docs/Network/SESSION.md) |
| 비공개 테스트 방 | 동작 확인 | 〃 |
| 게임 내 친구 초대 패널 | 동작 확인(옛 로비). 새 로비의 친구 카드는 Unity 미확인 | [Architecture/UI](Docs/Architecture/UI.md) |
| **새 로비**(참고 영상풍: 3D 파티 라인업, 모드 카드, 게임 시작 = 바로 매칭, 매칭 패널, 파티 바) | 코드·컴파일만, Unity 미확인 | [Architecture/UI](Docs/Architecture/UI.md) |
| **게임 모드**(킹 러시·소드 파이트 선택 가능, 퀸 오브 더 힐 준비 중), 모드별 매칭, 모드 씬 로드 | **소드파이트 선택→매칭 진입 사용자 확인(09-27)**. 전투 씬 실기 확인은 대기 | [GameModes](Docs/GameModes/README.md), [Network/SESSION](Docs/Network/SESSION.md) |
| **소드 파이트** | R51 검 가시성·무게감 있는 보정 드래그·강한 베기 판정. 클릭/물리 전환·빠른 발도 유지. **더미·R49 물리 동작은 사용자 확인**, R50 검 가림/손맛 피드백 반영. R51/두 PC 미확인. 평평한 시험 발판만 | [SwordFight/README](Docs/GameModes/SwordFight/README.md) |
| 네트워크 이동(호스트 판정·클라 예측) | 1인 캡슐 표시 확인, 양방향 미보고. **`JY-lobby`에서는 로비에서 안 움직이고 경기 씬에서만 움직인다** | [Network/MOTION](Docs/Network/MOTION.md) |
| AI 봇(파티 봇, 방 채우기) | 코드·테스트만 | [Network/BOTS](Docs/Network/BOTS.md) |
| 끊김 경고, 점프 누른 횟수, 버전 검사, 핑, F8 시뮬레이터, Rich Presence | 코드·테스트만 | [Network/MOTION](Docs/Network/MOTION.md), [기획안 반영](Docs/Network/PLAN_V0.1_STATUS.md) |
| 씬 흐름 Intro → Lobby → KingRush 또는 SwordFight → Lobby | 코드·자동 검사만, 사용자 확인 대기 | [Architecture/SCENES](Docs/Architecture/SCENES.md) |
| 킹러시 오프라인 플레이테스트(임시 캡슐 캐릭터) | 코드만 | [KingRush](Docs/KingRush/README.md) |
| 장애물 3종(시간의 함수) | 코드만 | [KingRush/OBSTACLES](Docs/KingRush/OBSTACLES.md) |
| **폰 러시 코스 01 v0.4**(R86): 93 × 246 m, 높이 0 → 15 m, 한 길(경로 점 398 m) + 미션 광장 2곳(백·흑 스테이션 20×20 m, 팀 문), 판마다 미니게임 A~E 중 두 개, 체크포인트 9(모두 공용), 장애물이 길 폭 전체를 덮음, 탑 두 길, 원형 결승, F5~F10 키·그림 카드·진척 막대, 검증기·CSV | 코드·컴파일·파이썬 좌표 점검만, Unity 미확인. 미니게임은 잡기 대신 위치·걷는 방향 판정. 온라인(뽑기·진척 전송)·승격/결승 규칙·장애물 힘의 방장 판정은 없음 | [PAWN_RUSH_COURSE01](Docs/KingRush/PAWN_RUSH_COURSE01.md) |
| 폰러시 맵 시험 씬 `ImportedChessFightMap`·장애물 프리팹 19개(지성 님, R65로 `main`에) | 오프라인 래그돌로 배치·장애물 시험. 상태형(무게·접근·붕괴)은 오프라인 전용, 체크포인트·온라인 미연결. Unity 미확인 | [KingRush/MAP_IMPORT](Docs/KingRush/MAP_IMPORT.md) |
| 래그돌 랩 온라인: 참가자 장애물을 방장 시각에 맞춤(`JY-lobby` R51, R65로 `main`에) | 코드·테스트만, 두 PC 미확인 | [Player/RAGDOLL](Docs/Player/RAGDOLL.md), [RagdollLab README](Docs/RagdollLab/README.md) |
| **퀸 오브 더 힐 래그돌 기능 M1~M11, M13**(`JY-ragdoll_v2`, 병합으로 들어옴): **M11 승격·기물 성능([7j] Shift+F9), M13 찌그러짐·비틀(Shift+F5·F6), M6 밧줄·그네([7i] Shift+F8), M7 도약대([7h] Shift+F10), M8~M10 종·체크포인트·팀([7g] F10)**, **M5 갈고리·앙파상(자동 점검 7개, Unity 미확인, [7f] 연습장 F8)**, 탈것·움직이는 벽, 능력·상호작용·조준 입력, 피격(`IHitReceiver`), 물·부활(`WaterZone`), RagdollTest [7] 시험대. (괄호 없는 R 번호는 `JY-lobby` 계열: M6=R44, M13=R45, M11=R46) | 1차 Unity 확인 성공(09-26, 1~12). 2차 수정(누워 있기·물에 뜨기)은 자동 점검 통과, Unity 재확인 대기. M12, M14는 시작 안 함. **킹 러쉬 쪽 시험 키(F4·F5·F7·F8)와 RagdollTest 시험대 키가 한 씬에서 겹치는지 병합 뒤 확인 안 함** | [Player/RAGDOLL §8](Docs/Player/RAGDOLL.md#8-퀸-오브-더-힐-기능-m1m11-m13), [MECHANICS_TODO](Docs/GameModes/QueenOfTheHill/MECHANICS_TODO.md) |
| 래그돌 (RagdollTest = 래그돌 랩, 2인 로컬 + 랩 전용 Steam 2인 호스트 판정) | 병합·코드·컴파일만, Unity 미확인. 빌드한 랩 플레이어의 자동 점검: `JY-ragdoll_v2` 86/0(09-27, M11까지), 킹 러쉬 쪽 기능 시험장 80/0(R56). **병합한 코드로는 아직 돌리지 않았다.** 게임 씬(KingRush) 네트워크 래그돌은 없음. 조작·등반 등 상세는 [RagdollLab README](Docs/RagdollLab/README.md) | [Player/RAGDOLL](Docs/Player/RAGDOLL.md) |
| **퀸 오브 더 힐 그레이박스 맵 7차**(`JY-lobby` R49, 병합으로 들어옴): 7개 층 + 8랭크(156 m), 층마다 두세 갈래 길, 4·7층 공용, 종·빛의 기둥·체크포인트, 8랭크 여덟 칸 승격, 오프라인 래그돌 1인 + 마우스 카메라·층별 기록. 맵은 데이터(JSON), Unity 없는 미리보기 | 코드·컴파일·Core 테스트·미리보기 그림만, **Unity 미확인** (R48판은 사용자 확인 → 교체) | [GRAYBOX](Docs/GameModes/QueenOfTheHill/GRAYBOX.md) |
| **방장 선정·이전**(`claude/host-migration`, `JY-lobby` 계열 R50, 병합으로 들어옴): 시작 때 성능·핑 기준 방장, 나가기·크래시·응답 없음 4초에 후계자, 느린 방장 넘김, 캡슐·래그돌 랩 역할 전환, 렉 줄이기 | 코드·모의 테스트·실제 DLL 컴파일만, **Unity·Steam 미확인** | [Network/HOST](Docs/Network/HOST.md) |
| **아직 없음** | 전체 기물 선택·스킬, 킹러시 네트워크 코스/승패 전달, 공통 재접속(로딩 동기화는 위 R58, 호스트 이전은 위 방장 줄), 신뢰 이벤트 채널. 킹러시 승패는 R56 오프라인만. **소드파이트만** R47의 폰 전투·장외 점수·부활·결과·래그돌 전달이 있음 | [ROADMAP](Docs/Project/ROADMAP.md), [소드파이트](Docs/GameModes/SwordFight/README.md) |

## 3. 진행 중인 일과 다음 할 일

**이 작업 폴더는 킹 러쉬 전용(R56)**
- `JY-kingrush`에서만 작업한다. 실제 기준은 `JY-gpt_gamemode@852eb2b`이며 첨부 기획이 전제로 쓴 최신 QotH M6/M11/M13은 없다. `KingRushPiece`/`KingRushPawn`으로 격리했고 공유 Pawn/튜닝/프로토콜은 바꾸지 않았다.
- Unity 메뉴 **ChessFight → King Rush → Open Mechanics Test** → Play. F4 → E로 킹+더미 시험. 승격은 F3 후 노란 발판, 문은 F7/F8 시험 완료 버튼. [확인 목록](Docs/Network/VALIDATION.md) R52.
- R52 첫 시험장은 사용자가 잘 작동한다고 보고(09-27). R53 룩 대포: F5 적 / Shift+F5 아군 → 우클릭으로 잡고 E, 아래 조준은 가까이. **사용자 확인 대기**. 대포 비행 중에만 걷기 앵커의 선형 드라이브를 풀어 실제 포물선과 표시를 맞춘다(`RagdollPawn.KingRush.cs`, 모드 종료 다음 물리 스텝 원복). 공유 튜닝/카메라·일반 발사대는 보존.
- **지금은 맵·미션 우선(사용자 R54).** Open Opening Course에서 첫 연결 코스를 직접 시험한다. F4로 정지 더미와 상자 미션, R 개인 체크포인트, F6 다음 체크포인트(개발용). 기본 잡기/던지기로 득점하도록 상자 정면에 짧은 경사를 추가했고 공통 물리는 건드리지 않았다.
- R54 자동 검사: **Core51·모의 세션20·DLL 컴파일/참조 경계 통과**, 최종 Windows 코스 **33/0**, 기존 기능 시험장 회귀 **80/0**. 실제 입력으로 출발→승격까지 연속 통과/상자 운반 득점/다리 출구 통과, 제자리 점프0.98m. 자동 렌더 확인까지이며 **사용자 확인을 대신하지 않는다**.
- **R55:** 첫/다음 승격2/3자리가 결정되면10초. 끝나는 순간 미도착 양 팀을 모으고 벽을 연다. 도착자는 그대로, 물/이전 상자 대기는 정리. 성벽 두 갈래/X다리/성문과 시소24m/몸 무게/4명 완료/구제 다리를 연결했다. **F7** 정상 집결, **F5** 성벽, **Shift+F5** 시소. 밧줄은 임시 통로. 기존 씬은 재생성하지 않고7구간을 추가했다. [상세/자동 기록](Docs/KingRush/CASTLE_COURSE.md).
- **R55 자동 검사:** Core56·모의 세션20·실제 Unity DLL 컴파일/참조 경계 통과. Windows 전체 코스 **68/0**와 기존 기능 **80/0**(빌드9,1280×720), 새 구간 먼저 **34/0**와 기존 기능 **80/0**(빌드8,1600×900). 전체 순서에서 발견한 시소 착지 전 조기 판정은 실제 착지/통과를 기다리도록 수정했다. 공통 이동 발판 점프는 보존. **사용자 실기 확인은 별도 대기**.
- **R56:** 사용자 요청으로 직접 테스트를 미루고 왕의 계단/결승 구현. 외나무·긴 계단/복귀 칸/손, 마지막4자리 승격도10초 집결. 왕좌8초 누적,50/100/140초 붕괴,140초 전5초 탑 재진입,초읽기2배/킹 추락 패배,180초 비교. 동률/동시 추락은 시험용 무승부다. [범위·자동 결과](Docs/KingRush/FINAL_COURSE.md).
- **R56 자동 검사:** Core62·모의 세션20·실제 DLL 컴파일/참조 경계 통과. 최종 Windows 빌드7 새 코스/결승 **29/0**, 이어서 기존 기능 **80/0**. 빌드6 결승 먼저 **29/0**, 빌드4 이전 연결 코스 전체 **68/0**. 회전부에서 넘어진 몸의 추락은 코스 난간으로 보완했고 공통 물리는 보존했다. **R55/R56 사용자 확인은 대기**, 직접 테스트는 나중에 하기로 했다.
- 다음: 사용자가 플레이할 때 R55/R56 집결·길이/난이도·시야·목표 안내를 함께 확인해 조정한다. 능력4종(비숍/폰/나이트/퀸)·룩 외형/매달림·킹 자세·최종 아트/음향은 보류이며 사용자 지시 없이 능력 하나씩 작업으로 되돌아가지 않는다. 전체 모드/온라인 완료가 아니다.
- 미션1은 `KingRushCaptureRules`, 결승은 `KingRushFinalRules`의 T0/게이지/승패로 분리한다. KR1은 여전히 문/승격만 전달하며 미션·결승 온라인/K11은 미구현. 아래 소드파이트 목록은 분기 당시 기록이며 이 채팅의 자동 착수 지시가 아니다.

**GPT 게임모드 작업 공간(R44)**
- 원본: `C:\Users\trews\OneDrive\문서\GitHub\ChessFight` — `JY-ragdoll_v2`. 미커밋 `RagdollPawn.Rope.cs`, `RopeLine.cs`는 이곳에 그대로 보존했다. 삭제·이동·커밋하지 않았고, 새 브랜치에도 복사하지 않았다.
- 게임모드: `C:\Users\trews\.codex\worktrees\jy-gpt-gamemode\ChessFight` — **`JY-gpt_gamemode`**. 이 폴더를 명시해서 명령·파일 편집을 실행한다. Unity로 게임모드를 시험할 때도 이 폴더를 연다.
- R50 사용자 실기 피드백 → **R51 검 방향을 조금 비껴 가시성 확보, 선 보정처럼 무게감 있는 시점/검 추종, 약한 접촉과 강한 베기 구분**. 느린 접촉 반동을 오인하던 첫 검사 실패도 수정. CFS3/v10. 클릭/F6·빠른 발도·공유 물리/원본은 보존.
- 다음: [VALIDATION R51](Docs/Network/VALIDATION.md)에서 검 가시성·보정 반응·약한 접촉/강한 베기·전환/복원·두 PC 손맛을 비교한다. 맵/기물 스킬/퀸 모드는 자동 착수하지 않는다. 원격 Push는 기존403 때문에 재시도하지 않으며 사용자가 GitHub Desktop에서 한다.

**사용자가 할 일 — 순서대로**
00000000000000000. **(새, R86) 폰 러시 코스 01 v0.4 + 미니게임:** 이 브랜치(`claude/bold-johnson-8ez95n`)를 Pull → Unity → **ChessFight → Pawn Rush → Open Course01** → Play → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 **v0.4 표(14개)**. 특히 미니게임 다섯 개(5번: F9/F10으로 바꿔 가며 혼자 걸린 시간), 상대 스테이션 방해(6번), 팀 문(7번), F8 새 판 뽑기(11번). 결정 대기: 기획서 "결정할 것"(판 시간, C·D 반대 입력, E 탑 수), `main` 병합 여부
0000000000000000. **(새, R85, 승규 님) 이름 바꾸기:** Intro 씬 Play → 시작 화면 "클릭해서 시작" 옆 **"이름 바꾸기"** → 바꾸기 화면에서 바꾸기·돌아가기·Esc → 로비 오른쪽 위 **내 이름**도 같은지 → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 **R85 표(9개)**. 마우스로 누르기·Enter·Esc와 파티 2명 경우(7~9번)는 사람만 볼 수 있다
000000000000000. **(새, R84, 승규 님) 시작 로고·이름 설정·장면 전환:** 이 브랜치(`claude/bold-johnson-8ez95n`)를 연 Unity에서 Intro 씬 Play(소리 켜고) → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 **R84 표(14개)**. 처음 상태는 저장된 이름이 없을 때만 나오니, 이미 이름을 정했으면 R85의 **"이름 바꾸기"**로 같은 화면을 본다. 화면은 AI가 Unity 캡처로 봤으니(표의 "AI가 봄"), 특히 사람만 볼 수 있는 것: 로고 소리와 박자(1·3번), 클릭으로 건너뛰기(2번), 한국어 입력(6번), 마우스로 추천 이름·말풍선 누르기(7·8번), Esc(14번). 마음에 안 드는 크기·위치·속도는 스크린샷으로 알려 주면 고친다.
00000000000000. **(새, R83, 승규 님) 로비 UI A 시안:** 이 브랜치(`claude/bold-johnson-8ez95n`) → Unity → Intro → 로비 → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 **로비 UI 표(10개)**. 특히 게임 시작과 배경 색이 어울리는지(10번), 테두리 버튼 누르는 맛(4번). 인트로·입장 화면·결과 화면·설정 창도 같은 말투로 바꿀지 정하면 이어서 한다
0000000000000. **(새, R76, 승규 님) 폰 러쉬 결과 화면:** 이 브랜치(`claude/bold-johnson-8ez95n`)를 연 Unity에서 `ChessFight > Scenes > Pawn Rush Victory (result preview)`와 `Pawn Rush Lose (result preview)` → 각각 Play → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 **폰 러쉬 결과 화면 표(12개)**. 혼자, Steam 없이 된다. 특히 **움직임(2·3·8번), H·R·버튼(5·7·10번)**은 AI가 멈춘 화면으로만 봤다. 결정 1개: 결과를 **코스 결승 그 자리에서** 보여 줄지 **이 씬으로 넘어갈지**(정해지면 경기 끝 → 팀에 맞는 결과 씬 → 다시 매칭·로비로를 잇는다)
000000000000. **(새, R71, 승규 님) 폰 러쉬 스킬 시험:** 이 브랜치(`claude/bold-johnson-8ez95n`) → Unity → **ChessFight → Pawn Rush → Open Skill Test** → Play → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 **스킬 표(12개)**. 왼쪽 창에서 기물 고르고 F(시험용 쿨 2초, 룩·비숍은 F 뒤 좌클릭), 룩은 V로 4명 뭉치. 이펙트 시안([Skills/EFFECTS](Docs/Skills/EFFECTS.md))에서 기물마다 하나 고르면 만든다. 손맛·예고·밸런스 의견을 주면 수치(Inspector `Pawn Rush Skill Bed`)부터 고친다. 남은 결정: 스킬 키, D4~D11. D10(킹 러시 기획의 "체스 이동을 옮긴 능력은 하지 않는다"를 바꾸는지)은 팀장님 확인
0000000000. **(새, R65) `main` 병합 확인:** `main`을 Pull(LFS 포함) → Unity로 열고 [VALIDATION](Docs/Network/VALIDATION.md) 최상단 **`main` 병합 표(5개)**. 모든 PC가 같은 `main`이어야 온라인이 된다(프로토콜 v15).
000000000. **(새, 승규 님) 설정 창(R64):** Intro 씬 Play → Esc(설정 창) → "클릭해서 시작" → 로비 → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 **설정 창 표**.
00000000. **(새, 승규 님) 메뉴 디자인 C(R63):** Intro 씬 Play → "클릭해서 시작" → 로비 → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 **메뉴 표**. 특히 **버튼 누르는 맛(4번)**, 게임 시작 뒤 **곧바로 뜨는 입장 화면과 매칭 취소·Esc(6·6-b번)**, 경기 중 **투명 채팅(8-b번)**, (3차) 입장 화면 **봇 추가 +(6-d번)**를 봐 주세요. 마음에 안 드는 색·크기는 스크린샷으로 알려 주시면 고칩니다. 정할 것: 버튼 소리(Kenney UI Audio) 넣을지
000000. **(새, 승규 님) 채팅(R61):** 로비 → [VALIDATION](Docs/Network/VALIDATION.md) **채팅 표(14개)**. 1~8번은 로비, 9번 킹 러시(비공개 방 + 봇 12명 채우기 → 경기 시작), 10번 소드 파이트(더미 테스트), 11~14번은 PC 2대. 특히 **한국어 입력기로 친 마지막 글자(3번), Esc(5·9·10번)**를 봐 주세요.
00000. **(새, 승규 님) 빈자리 채우기·나간 사람만 나가기(R59·R60):** 서로 다른 Steam 계정 PC 2~3대 → [VALIDATION](Docs/Network/VALIDATION.md) **빈자리 표(13개)**. 1~9번은 각자 1인 파티로: A가 매칭 → B가 매칭(같은 대기실) → A가 12명 채우기 → 경기 중 B가 Esc → B가 다시 게임 시작 → 같은 팀·자리로 돌아오는지. 10~13번은 같은 파티로: 파티원이나 방장이 나가도 나머지는 경기를 계속하는지
0000. **(새, 승규 님) 로딩 화면(R58):** 로비 → 킹 러시(폰 러시) 비공개 방 + 봇으로 경기 시작 → [VALIDATION](Docs/Network/VALIDATION.md) **로딩 화면 표(9개)**. 1~6번은 혼자, 7~9번은 PC 2대. 결정 1개: 느린 사람 최대 대기(지금 20초, `MatchStart.MaxWait`)
000. **(새, 승규 님) 방장 선정·이전(`claude/host-migration`, 이 브랜치에 합쳐짐):** 이 브랜치(`JY-kingrush`)를 연 Unity로 빌드해 **서로 다른 Steam 계정 PC 2~3대**에서 [VALIDATION](Docs/Network/VALIDATION.md) **방장 표(11개)**. 특히 3번(방장 강제 종료 후 몇 초 만에 새 방장)과 1번(성능 좋은 PC가 방장). 결과를 보고 **`JY-lobby`·`main`에도 넣을지 팀장님이 결정**
00. **(새, `JY-lobby`) 퀸 오브 더 힐 맵 7차:** Pull → (JY-lobby 워크트리 폴더를 연 Unity에서) `ChessFight > Scenes > Queen of the Hill (offline graybox)` → Play → [GRAYBOX §4](Docs/GameModes/QueenOfTheHill/GRAYBOX.md#4-unity-확인-순서-처음-한-번) 순서대로. V 자유 카메라로 7개 층을 둘러보고, 층마다 길 하나 이상 해 본 뒤 **층별 시간과 막힌 곳·재미없는 곳**을 알려 준다. 결정 1개: 떨어져 아래층에 부딪히면 물에 빠진 것처럼 칠지([GRAYBOX §5](Docs/GameModes/QueenOfTheHill/GRAYBOX.md#5-알고-있는-빈-곳))
0. (래그돌, `JY-ragdoll_v2`) `Ragdoll Test` → Play → [VALIDATION.md](Docs/Network/VALIDATION.md) 최상단 **승격 표(4개, Shift+F9: 받침대·킹·룩 등반)**, **찌그러짐 표(3개, Shift+F5·F6)**, **밧줄 표(6개, Shift+F8: 사슬 오르기·그네 8 m·기둥)**, **도약대 표(5개, Shift+F10: L자 도약대·태엽 스프링)**, **개척의 탑 표(6개, F10: 종·승강기·독점·체크포인트·팀)**, **등반 표(4개: 멈추면 스테미나 유지, Space 벽 점프, W는 위로만)**, **갈고리 표(11개, F8 연습장)**, 그다음 **조작감·등반 확인 표(15개, 09-27: 멈춤·부딪힘·다이빙·버둥대기 클릭 수·질주·등반 레인 넷·온라인 막대)**, 이어서 F9 → **퀸 오브 더 힐 2차 재확인 표(5개: F5 뒤 1초 누워 있기, 물에 5초 둥둥·좌클릭 버둥)**. 1차 표는 1~12 성공(09-26), 13번(두 PC)은 친구와 할 때
1. `JY-lobby`를 Pull(LFS 포함)한 뒤 Unity에서 열고 [VALIDATION.md](Docs/Network/VALIDATION.md) 최상단 **새 로비 표(11개)**를 확인한다. 이어서 씬 분리·래그돌 병합 표. 막히면 증상·스크린샷과 Console 첫 오류를 AI에게 준다.
2. 두 PC로 양방향 이동을 확인한다(가장 오래 미뤄진 검증). 이제 경기 씬에서 한다([Network/README §2](Docs/Network/README.md)).
3. 퀸 오브 더 힐을 시작하기 전에 결정: 인원 구성(6 대 6 안에서?), 래그돌 기준 구조물 크기 → [GameModes §3](Docs/GameModes/README.md).
4. 로비 작업을 `main`에 넣을지(병합 시점) 결정한다.

**팀원(이번 주 배정)**
- 준영: 래그돌은 병합됨. 이후 작업은 Network/main 기준, 멀티 규칙 준수 → [Player/RAGDOLL](Docs/Player/RAGDOLL.md)
- 진호·지성: 킹러시 맵과 장애물, 장애물별 기획서 작성 → [KingRush](Docs/KingRush/README.md)
- 승규: 네트워크 방어 기획. 추천 작업과 AI 프롬프트 → [기획안 반영 상태 §4](Docs/Network/PLAN_V0.1_STATUS.md#4-승규-님께-요청할-다음-작업)

**AI의 다음 작업 후보 (사용자 지시가 있을 때만 착수)**
0. **병합 확인(09-28):** 병합한 코드로 래그돌 자동 점검을 다시 돌리고(킹 러쉬 80/0, `JY-ragdoll_v2` 86/0이 그대로인지), Unity에서 킹 러쉬 코스·RagdollTest·로비가 열리는지 본다. 문제가 나오면 이것부터 고친다.
1. Unity 확인 중 나오는 오류 수정 (최우선). R48 더미·R49 물리 동작은 확인됨. 이제 R51 검 가시성·무게감/강한 베기·두 조작 비교/2PC를 확인한다. 새 로비(샘플 B 포함)는 Unity에서 한 번도 안 열었다: 배치·겹침·색 조정이 나올 수 있다.
2. **소드 파이트 우선(R47)**: [폰 플레이 버전](Docs/GameModes/SwordFight/README.md)을 사용자와 시험하고 칼/연속타 감각부터 다듬는다. 맵은 마지막. 아래 퀸 모드 목록은 인계 참고이며 자동 착수 지시가 아니다.
2-b. (`JY-lobby` 계열, 참고) **퀸 오브 더 힐 맵 R49:** 사용자 피드백으로 `Tools/QueenHill/build_layout.py`의 층별 수치·구성 조정 → 층마다 다른 열리는 길 → 퀸 보석판(d8) → 층별 프리팹·아트 모듈. 퀸 오브 더 힐 기획은 **GPT 아스트라가 이어받는다**(R24): [GameModes/QueenOfTheHill/README](Docs/GameModes/QueenOfTheHill/README.md). 자동 착수 지시가 아니다.
3. 경기 흐름: 기물 선택 화면(참고 영상), 라운드 소개, 결과 → 로비
   - **퀸 오브 더 힐 개발 목록(R33):** 래그돌·게임플레이에 새로 필요한 기능 M1~M14와 우선순위·시작 프롬프트 → [MECHANICS_TODO](Docs/GameModes/QueenOfTheHill/MECHANICS_TODO.md). **M1~M11, M13은 `JY-ragdoll_v2`에서 구현됨(R34 Unity 1차 확인 성공, R35 수정 재확인 대기, M5는 R40 — 조작은 사용자가 바꿈, M8·M9·M10은 R42, M7은 R43, M6은 R44, M13은 R45, M11은 R46에 AI가 이어서).** 다음은 §3 순서대로 **M12**(기물 능력: 나이트 L자 도약·밟기, 룩 돌진, 킹 체크!·캐슬링·가호, 비숍 호버·돌조각·활공, 퀸 칼·돌진·호버·시선) → M14(네트워크, 사용자 결정 필요: 경기에서도 방장이 래그돌을 전부 계산하는가). 래그돌 작업 브랜치는 `JY-ragdoll_v2`
   - **조작감(R36)**: 버둥대기 클릭 게이지, 머리부터 다이빙, 이동 반응성, 질주·등반 버그를 고쳤고, 2차로 기상 중 끌림·다이빙 회전·부딪힘 튕김·역경사 매달림·턱 튕김을 고쳤다(자동 점검 55/55, 등반 점검은 4가지 순서로 확인). 자동 점검이 **앞선 점검 순서에 따라** 결과가 바뀌면 운에 기댄 것이니 원인을 고친다(`-ragdollAutoTestOnly`로 순서를 바꿔 돌린다, DECISIONS G15). 사용자의 Unity 손맛 확인 뒤 다음 후보: 카메라 거리·시야(지금은 휠로 조절), 등반 손동작 크기(준영 님이 정한 "빠르게 짧게 짚기"와 상의), 던지기 힘. 조작감을 바꿀 때는 `-ragdollFeel` 녹화로 전후를 비교한다 → [RagdollLab README "조작감 개선"](Docs/RagdollLab/README.md#조작감-개선-2026-09-26-r36-jy-ragdoll_v2)
   - **래그돌 자동 점검 기존 실패 9개는 R37(09-27)에 고쳐 R38에서 합침**(합친 뒤 56 통과 / 0 실패, Unity 미확인 → [VALIDATION](Docs/Network/VALIDATION.md) 최상단 표). **사용자 결정(09-27, R39): 둘 다 그대로 둔다** — ① 경사 45° "몸 던지기가 더 빠른가"는 공정하게 재면 사실상 동률(0.01~0.03초, 통과하지만 불안정)이지만 튜닝하지 않는다 ② 역경사 20도 레인은 실제로는 윗부분이 멀어지는 경사판이지만 씬을 다시 만들지 않는다(DECISIONS G16) → [RagdollLab README "자동 점검"](Docs/RagdollLab/README.md#자동-점검)
   - **팀 색 정정(R32):** 이 게임의 두 팀은 **백팀·흑팀**이다(캐릭터 자체가 흰 말·검은 말). 코드는 아직 청팀·주황팀(재질 `TeamBlue`/`TeamOrange`, HUD 문구 "청팀/주황팀", 매칭 패널 칸 색). 바꾸려면 사용자 확인 후 작업
4. Network → main 병합 PR (현재는 main이 Network를 포함하므로 불필요할 수 있음)
5. 기획안의 "결정 필요" 항목: 신뢰 이벤트 채널 → 로딩 동기화 → 호스트 끊김 2단계(라운드 무효, 파티 유지). (3단계 호스트 이전은 `claude/host-migration`, R50. 두 PC 확인에서 나오는 문제 수정이 먼저: 크래시 뒤 Steam이 방에서 빼기까지 걸리는 실제 시간, 래그돌 이어받을 때의 멈칫 정도)
6. 네트워크 경기에서 래그돌로 코스 달리기(래그돌 권한 결정 필요. 병합은 끝남)

## 4. 절대 규칙 — 어기면 과거에 실제로 사고가 났던 것들

1. **Active Input Handling은 `Input Manager (Old)`(0)로 둔다. Input System 패키지는 설치하지 않는다**(`JY-lobby`, R20: 설치돼 있으면 켤 때마다 백엔드를 켜라는 창이 뜬다). uGUI가 없어서 Both/New로 바꾸면 HUD 클릭과 입력이 전부 죽는다. `InputSettingsGuard`가 자동으로 되돌린다. → [PITFALLS §1](Docs/Environment/PITFALLS.md)
2. **프로젝트는 선택 패키지가 하나도 없어도 컴파일되어야 한다.** Safe Mode에서는 설치 스크립트가 돌지 않는다. 선택 패키지 코드는 전용 asmdef + `versionDefines` + `defineConstraints`로 가둔다. `ENABLE_INPUT_SYSTEM`을 패키지 설치 여부로 쓰지 않는다.
3. **`Packages/manifest.json`·`packages-lock.json`이 바뀌면 Discard하지 말고 커밋한다.**
4. **폴더 하나 = 어셈블리 하나.** `Game`·`Gameplay`는 Steam을 참조하지 않는다. `Core`는 Unity도 참조하지 않는다. → [Architecture/STRUCTURE](Docs/Architecture/STRUCTURE.md)
5. **Steam 초기화·종료 소유자는 `NetworkRuntime` 하나다.** SteamManager를 따로 추가하지 않는다.
6. **HUD에는 기본 테마가 없다.** ScrollView·Dropdown 같은 복합 컨트롤은 보이지 않는다. 평범한 VisualElement만 쓴다.
7. **장애물 위치·회전은 `ObstacleClock` 시간의 순수 함수로만 계산한다(스스로 움직이는 kinematic 물체는 `Obstacle`을 상속, 테스트 스크립트가 검사. 플레이어 상태로 움직이는 것은 `StateDrivenMover:` 주석을 달고 오프라인 전용).** 프레임마다 누적하지 않는다. 온라인 래그돌 참가자는 방장이 스냅샷에 실은 장애물 시각을 쓴다(R51).
8. **클라이언트는 입력만 보낸다.** 좌표를 권위로 받지 않는다. 파티는 같은 팀에 통째로 예약한다.
9. **네트워크 비호환 변경을 하면 `SteamSession.Protocol`을 올린다.** 패킷 구조를 바꾸면 `MotionProtocol` magic도 올린다.
10. **검증 표기는 정직하게 한다.** 사용자가 Unity나 Steam에서 직접 본 것만 VALIDATION에 '성공'으로 적는다. 코드 작성과 테스트 통과는 검증이 아니다.
11. **`.meta`는 항상 같이 커밋한다.** Unity 밖에서 파일을 만들면 `Tools/Generators/mkmeta.py`로 만든다.
12. **같은 `.unity` 씬을 두 사람이 동시에 고치지 않는다.** 맵은 구간 프리팹으로 나눈다.
13. **`Assets/Scripts`(네트워크·게임 코드)는 래그돌 타입을 참조하지 않고, 래그돌(`Assets/ChessFight/RagdollLab`)은 Steam을 참조하지 않는다.** 랩을 Steam에 잇는 코드는 `Assets/ChessFight/RagdollLabSteam/`(Bootstrap과 같은 다리)에만 둔다. 캐릭터는 `ICharacterDriver`로만 부른다. `Tools/run-tests-linux.sh`의 경계 검사가 확인한다.
14. **물리 콜백(`OnCollision*`·`OnTrigger*`) 안에서 순간이동하거나 관절을 즉시 지우지 않는다.** Unity가 `DestroyImmediate`를 거부해 잡기 관절이 남는다. 알리기만 하고 다음 Update에서 처리한다(`WaterZone` → 시험대·`PlaytestSpawner`). → [PITFALLS §19](Docs/Environment/PITFALLS.md)
15. **(`claude/host-migration`부터) 경기 상태를 방장 PC에만 두지 않는다.** 방장은 경기 중에 바뀔 수 있다([HOST](Docs/Network/HOST.md)). 점수·타이머·모드 상태는 스냅샷이나 로비 데이터에 실어 모두가 갖고 있게 한다. 방장만 아는 값은 이전 때 사라진다.

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
│  └─ UI.md                     HUD(UI Toolkit), 폰트, 클릭 대체 경로, 새 로비 배치, 친구 카드, 버튼 표, 로딩·채팅·결과 화면
├─ Network/                     멀티플레이
│  ├─ README.md                 네트워크 개요와 사람용 테스트 방법
│  ├─ SESSION.md                파티, 매칭, 예약, 로비 데이터, 취소
│  ├─ MOTION.md                 이동 동기화, 패킷, 연결 품질
│  ├─ HOST.md                   방장 선정(성능·핑)과 방장 이전 (`claude/host-migration`)
│  ├─ BOTS.md                   AI 봇
│  ├─ PLAN_V0.1_STATUS.md       승규 기획안 v0.1 항목별 반영 상태, 승규 다음 작업
│  └─ VALIDATION.md             ★ 실제 확인 기록과 확인 목록
├─ Player/                      플레이어 캐릭터
│  ├─ MOVEMENT_INPUT.md         입력 경로, 네트워크 이동, 오프라인 캐릭터
│  └─ RAGDOLL.md                래그돌 병합 상태, RagdollTest, 네트워크 안전성, 어댑터, 멀티 규칙
├─ RagdollLab/README.md         준영 님 랩 문서 (조작, 구조, 측정 근거)
├─ Skills/                      ★ 기물 스킬: 인수인계(README), 기획서(DESIGN, 폰 러쉬 v0.2 상세판)
├─ GameModes/README.md          게임 모드 목록(킹 러시·퀸 오브 더 힐·소드 파이트), 모드별 매칭, 새 모드 추가법
│  └─ QueenOfTheHill/           ★ 퀸 오브 더 힐: 인수인계(README), 기획(DESIGN), 그레이박스 맵(GRAYBOX), 새 기능 개발 목록(MECHANICS_TODO), 아트 후보(ART_CONCEPTS), 이미지 프롬프트, N4 참고 원문
├─ KingRush/                    첫 미니게임
│  ├─ README.md                 맵 구성, 코스 컴포넌트, 네트워크 한계
│  ├─ OPENING_COURSE.md         R54 장난감 코스·상자 미션
│  ├─ CASTLE_COURSE.md          R55 승격10초/미도착 양 팀 집결·성벽/시소
│  ├─ FINAL_COURSE.md           R56 왕의 계단/마지막 집결·왕좌/붕괴/승패
│  ├─ DESIGN.md                 사용자 제공 1차 기획 원문 (확정 UK / 미션 1차 안 구분)
│  ├─ MECHANICS_TODO.md          개발 목록 원문 + R52 범위/기준 차이
│  ├─ OBSTACLES.md              장애물 규칙과 만드는 법
│  └─ OBSTACLE_TEMPLATE.md      장애물 기획서 양식
├─ TEAM_GUIDE_KO.md             팀원용 한 권 안내서 (역할별 장, AI 시작 프롬프트)
└─ AI/                          옛 경로 (새 문서로 안내만 함)
Tools/
├─ run-tests-linux.sh           Unity 없는 환경의 테스트 + 컴파일 검사 (+ 퀸 오브 더 힐 맵 JSON 최신 검사)
├─ QueenHill/                   퀸 오브 더 힐 맵 원본 스크립트(build_layout.py)와 Unity 없는 미리보기(preview/)
├─ Test-NetworkCore.ps1 / Test-NetworkCompile.ps1   Windows(Unity 설치 PC)용 같은 검사
└─ Generators/                  씬·프리팹 YAML 생성기, .meta 생성기
```

## 6. 어디를 읽을까 — 작업 분야별 안내

| 작업 | 먼저 읽을 것 | 그다음 코드 |
|---|---|---|
| 무엇이든 처음 | 이 파일 → [REQUIREMENTS](Docs/Project/REQUIREMENTS.md) → [DECISIONS](Docs/Project/DECISIONS.md) → [PITFALLS](Docs/Environment/PITFALLS.md) | — |
| Unity 오류, Safe Mode, 클릭 안 됨 | [PITFALLS](Docs/Environment/PITFALLS.md), [SETUP](Docs/Environment/SETUP.md) | `Scripts/Editor/`, asmdef |
| 파티, 매칭, 초대, 경기 중 빈자리 | [Network/SESSION](Docs/Network/SESSION.md) | `Network/SteamSession.cs`, `Core/TeamReservations.cs`, `Core/Backfill.cs` |
| 이동 동기화, 지연, 끊김 | [Network/MOTION](Docs/Network/MOTION.md) | `Network/SteamMotion.cs`, `Core/MotionProtocol.cs`, `Core/LinkQuality.cs` |
| 방장 선정·이전, 방장 PC 성능 | [Network/HOST](Docs/Network/HOST.md) | `Core/HostElection.cs`, `Network/HostFitnessProbe.cs`, `Network/SteamSession.cs`(`FollowHost`·`StartGame`), `RagdollLabSteam/SteamRagdollLink.cs`(`OnHostChanged`) |
| 봇 | [Network/BOTS](Docs/Network/BOTS.md) | `Core/BotIdentity.cs`, `Core/BotBrain.cs` |
| 씬 추가, 씬 전환 | [Architecture/SCENES](Docs/Architecture/SCENES.md) | `Bootstrap/NetworkRuntime.cs`, `Game/SceneNames.cs` |
| 게임 모드 추가, 모드별 매칭 | [GameModes](Docs/GameModes/README.md), [Network/SESSION](Docs/Network/SESSION.md) | `Core/GameModes.cs`, `Network/SteamSession.cs`, `Bootstrap/MatchSceneView.cs` |
| **퀸 오브 더 힐** | [GameModes/QueenOfTheHill](Docs/GameModes/QueenOfTheHill/README.md) → [RagdollLab README](Docs/RagdollLab/README.md) "등반" | 위 문서 §6 파일 지도 |
| **퀸 오브 더 힐 맵(그레이박스)** | [GRAYBOX](Docs/GameModes/QueenOfTheHill/GRAYBOX.md) → [Tools/QueenHill/README](Tools/QueenHill/README.md) → [DESIGN §3](Docs/GameModes/QueenOfTheHill/DESIGN.md) | `Tools/QueenHill/build_layout.py`(맵 원본), `Gameplay/QueenHill/QueenHillLevel.cs`, `Core/QueenHillCourse.cs` |
| HUD, UI, 로비 | [Architecture/UI](Docs/Architecture/UI.md) (배치는 §11 디자인 C, 로비 모양은 §14 A 시안) | `Game/NetworkHudView.cs`, `Game/LobbyStage.cs`, `Game/MenuArt.cs`·`ChunkyButtons.cs`·`MenuMarks.cs`, `Game/LoadingStudio.cs`, `Bootstrap/LobbyBootstrap.cs`, `Resources/*.uxml/uss` |
| 시작 로고·이름 설정 화면·장면 전환(R84), 이름 바꾸기(R85) | [UI §15](Docs/Architecture/UI.md#15-시작-로고이름-설정-화면장면-전환-r84-2026-10-08), [SCENES](Docs/Architecture/SCENES.md) "Intro", [SESSION](Docs/Network/SESSION.md) 표 `nick` | `Game/LogoIntro.cs`(영상), `Game/NameChange.cs`(이름 바꾸기), `Game/NameScreen.cs`·`GlassPawn.cs`, `Game/SceneTransition.cs`(`ChessOutlines`), `Game/PlayerProfile.cs`·`StreamingArt.cs`·`IntroFlowStyle.cs`, `Core/PlayerNames.cs`, `Bootstrap/IntroController.cs`(흐름), `Network/SteamSession.cs`(`Name`·`LocalName`), `Resources/LogoHud.uxml`·`NameHud.uxml`·`TransitionHud.uxml`·`IntroFlow.uss`, `Assets/StreamingAssets/` |
| 폰 러쉬 결과 화면(R76, 라스트 씬을 대신함) | [UI §13](Docs/Architecture/UI.md#13-폰-러쉬-결과-화면-r76-2026-10-06), [SCENES](Docs/Architecture/SCENES.md) "PawnRushVictory / PawnRushLose" | `Game/PawnRushResultDirector.cs`(진행·키·이벤트·미리보기), `Game/PawnRushResultStage.cs`(탁자·램프·빛·말·연출·카메라·배경), `Game/PawnRushResultHud.cs`(결승 중계 결과판), `Game/LastSceneFigure.cs`·`LastSceneArt.cs`(말·메시), `Core/MatchResult.cs`, `Tools/Generators/gen_pawnrush_result.py` |
| 채팅 | [UI §9](Docs/Architecture/UI.md#9-채팅-r61-2026-09-30), [SESSION §9](Docs/Network/SESSION.md#9-채팅-r61-2026-09-30) | `Core/Chat.cs`, `Game/ChatBox.cs`, `Resources/ChatHud.uxml`, `Network/SteamSession.cs`(`Say`·`Hear`·`FollowChat`), `Bootstrap/NetworkRuntime.cs`(채팅 위치) |
| 플레이어 입력, 조작 | [Player/MOVEMENT_INPUT](Docs/Player/MOVEMENT_INPUT.md) | `Game/MoveInputSource.cs`, `Input/` |
| 래그돌 | [Player/RAGDOLL](Docs/Player/RAGDOLL.md), [RagdollLab/README](Docs/RagdollLab/README.md) | `Assets/ChessFight/RagdollLab/Scripts/`, `Gameplay/Characters/ICharacterDriver.cs` |
| **기물 스킬** | [Skills/README](Docs/Skills/README.md) → [Skills/DESIGN](Docs/Skills/DESIGN.md) → [RagdollLab/README](Docs/RagdollLab/README.md) | `RagdollLab/Scripts/PawnRushSkills/`(`RagdollPawn.PawnRushSkills.cs` 본체, `PawnRushSkillParams`, `PawnRushSkillBed` 창·키, `SkillTripwire`, `SkillBarricade`, `SkillMarks`, `PawnRushSkillProbe` 자동 시험), `RagdollPawn.cs`(스킬 연결 4곳), `.Abilities.cs`(퀸 오브 더 힐 나이트), `Core/ChessPieces.cs`(무게) |
| **폰 러시 코스(맵) 01** | [PAWN_RUSH_COURSE01](Docs/KingRush/PAWN_RUSH_COURSE01.md) → [기획서 v0.4 원문](Docs/KingRush/PAWN_RUSH_COURSE01_DESIGN_v0.4.md) → [재료집](Docs/KingRush/PAWN_RUSH_MAP_KIT.md) | `Assets/Maps/PawnRush/Course01/Scripts/Course01v4Builder*.cs`(월드 좌표), `MissionStation.cs`·`TeamGate.cs`·`Mg*.cs`·`MissionPicker.cs`, `Assets/Scripts/Core/PawnRushMissions.cs`, `PawnRushCourse.cs`, `ProgressPath.cs`, `Course01Validator.cs`, `Editor/Course01Assembler.cs`, `Gameplay/Course/NoClimbSurface.cs` |
| 킹러시 맵, 장애물 | [KingRush](Docs/KingRush/README.md), [OBSTACLES](Docs/KingRush/OBSTACLES.md) | `Gameplay/Obstacles`, `Gameplay/Course` |
| 테스트, 검증 | [AI_WORKFLOW §3](Docs/Environment/AI_WORKFLOW.md), [VALIDATION](Docs/Network/VALIDATION.md) | `Tests/Network`, `Tools/` |

## 7. 사용자와 일하는 방식

- **대답은 한국어로 한다.** 코드 주석과 커밋 메시지는 영어다(기존 관례).
- 사용자는 메인 기획자다. 개념은 **쉽게, 표와 단계로** 설명한다. 코드 용어는 괄호로 보충한다.
- "구현해줘"는 **구현 → 테스트 → `main`에 커밋·푸시**까지다(R66, §1). **PR은 요청할 때만** 만든다.
- 확인·검토 요청("체크해줘", "어떻게 생각해")에는 먼저 평가만 하고, 구현 여부를 묻는다.
- 사용자는 Windows에서 Unity Hub로 `C:\Users\dua07\GitHub\ChessFighter`를 연다. 결과는 스크린샷이나 증상으로 알려준다. AI는 Unity를 직접 실행할 수 없는 경우가 많으므로 **Unity에서 확인할 항목을 명확히 적어 준다.**
- 사용자가 싫어한 것:
  - 클릭 안 되는 UI
  - Safe Mode
  - 저절로 생기는 변경점
  - 읽기 힘든 중첩 폴더
  - 추측으로 "된다"고 말하기
- 게임모드 브랜치(`JY-gpt_gamemode`, 이전 `JY-lobby`)에서는 **캐릭터 조작·물리 튜닝을 임의로 바꾸지 않는다**(R18·R44). 요청자는 초보라서 설명은 쉽게, 코드 주석은 영어로, Unity 확인은 순서로 적는다.
- 팀원 이름: 준영(래그돌), 진호·지성(킹러시 맵·장애물), 승규(네트워크 기획). 팀원은 ChatGPT(아스트라) 등 다른 AI를 쓴다. 그래서 이 문서 체계가 도구에 상관없이 읽혀야 한다.

## 8. 작업 종료 체크리스트

AI든 사람이든 작업을 끝낼 때:

1. 테스트: `Tools/run-tests-linux.sh --compile`(Linux) 또는 `Tools/Test-NetworkCore.ps1`(Windows). 결과 수를 기록한다.
2. **[REQUIREMENTS.md](Docs/Project/REQUIREMENTS.md)에 요청 한 줄을 추가한다**(날짜, 요청 요약, 결과, 커밋, 상태).
3. 결정이 새로 생겼으면 [DECISIONS.md](Docs/Project/DECISIONS.md)에, 새 함정을 겪었으면 [PITFALLS.md](Docs/Environment/PITFALLS.md)에 추가한다.
4. 바꾼 분야의 상세 문서(Network/…, KingRush/… 등)를 코드와 맞춘다.
5. Unity에서 확인해야 할 것이 생겼으면 [VALIDATION.md](Docs/Network/VALIDATION.md) 최상단에 '미확인' 표로 추가한다.
6. **이 파일의 §1 스냅샷, §2 표, §3 다음 할 일을 갱신하고 "최종 갱신" 날짜를 바꾼다.**
7. [HISTORY.md](Docs/Project/HISTORY.md)에 커밋을 한 줄 추가하고 **`main`에 커밋·푸시한다**(R66). 다른 채팅의 `JY-gpt_gamemode`·원본 `JY-ragdoll_v2` 미완성 작업은 건드리지 않는다. 푸시 제한은 아래를 따른다.

**R52 예외:** 이 폴더의 커밋 대상은 `JY-kingrush`. 다른 두 폴더를 수정하거나 자동 병합하지 않는다. 알려진 CLI 푸시403은 재시도/자격증명 변경 없이 GitHub Desktop Push를 안내한다.

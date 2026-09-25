# 문서 지도

**시작점은 저장소 루트의 [`HANDOFF.md`](../HANDOFF.md)다.** 이 파일은 그 문서 트리의 상세 지도다.

## 트리

```text
Docs/
├─ Project/            무엇을·왜 — 모든 작업 전에
│  ├─ OVERVIEW.md          제품, 팀·역할, 브랜치, 저장소 경로
│  ├─ REQUIREMENTS.md      사용자 요구사항 전체 이력 (요청 → 결과 → 커밋 → 상태)
│  ├─ DECISIONS.md         확정 결정과 이유, 바꾸면 생기는 일
│  ├─ ROADMAP.md           검증·병합·네트워크·게임플레이 남은 일, 기술 부채
│  └─ HISTORY.md           커밋별 변경 이력
├─ Environment/        어떻게 작업하나
│  ├─ SETUP.md             Unity·패키지·Steam·LFS·병합 도구·빌드·메뉴
│  ├─ PITFALLS.md          실제 사고: 증상 → 원인 → 해결
│  └─ AI_WORKFLOW.md       AI 작업 절차, 테스트, 생성기, 보고 방식
├─ Architecture/       전체 구조
│  ├─ STRUCTURE.md         폴더, 어셈블리, 의존 방향, define, 주요 파일
│  ├─ SCENES.md            Intro·Lobby·KingRush·RagdollTest, NetworkRuntime, 새 씬 추가법
│  └─ UI.md                UI Toolkit HUD, 폰트, 클릭 이중 장치, 새 로비 배치, 친구 카드
├─ Network/            멀티플레이
│  ├─ README.md            개요, 2인 테스트 방법, 한계
│  ├─ SESSION.md           파티·매칭·예약·로비 데이터·취소·Rich Presence·버전 검사
│  ├─ MOTION.md            이동 동기화·패킷·끊김 단계·핑·F8
│  ├─ BOTS.md              AI 봇, 공개 매치 규칙
│  ├─ PLAN_V0.1_STATUS.md  승규 기획안 반영 상태, 승규 다음 작업
│  └─ VALIDATION.md        실제 확인 기록, Unity 확인 목록
├─ Player/             플레이어 캐릭터
│  ├─ MOVEMENT_INPUT.md    입력 경로, 이동 모델 두 가지, 표시
│  └─ RAGDOLL.md           래그돌 병합·어댑터·멀티 조건
├─ GameModes/README.md 게임 모드 목록, 모드별 매칭, 새 모드 추가법
│  └─ QueenOfTheHill/     퀸 오브 더 힐 인수인계(README: 기획 결정·개발 순서·시작 프롬프트), N4 참고 원문
├─ KingRush/           첫 미니게임
│  ├─ README.md            씬 배치, 코스 컴포넌트, 네트워크 한계, 협업 규칙
│  ├─ OBSTACLES.md         장애물 규칙과 만드는 법
│  └─ OBSTACLE_TEMPLATE.md 장애물 기획서 양식 (완성본은 Obstacles/이름.md)
├─ TEAM_GUIDE_KO.md    팀원용 한 권 안내서 (역할별 장, 팀원 AI 시작 프롬프트)
└─ AI/                 옛 경로. 새 문서로 안내만 한다
```

## 문서 원칙

1. **한 가지 사실은 한 곳에만** 자세히 쓴다. 다른 곳에서는 링크한다. 팀 가이드는 팀원 편의를 위해 요약을 겹쳐 두지만, 어긋나면 분야 문서가 기준이다.
2. **코드가 문서보다 우선한다.** 어긋나면 코드를 확인하고 문서를 고친다.
3. **확인된 것과 추정을 구분한다.** 실제 Unity·Steam 결과는 VALIDATION에만 '성공'으로 쓴다.
4. 새 분야가 생기면(예: 퀸 오브 더 힐 규칙이 커질 때, 기물·스킬) `Docs/<분야>/README.md`를 만들고 이 트리와 [HANDOFF §5·§6](../HANDOFF.md)에 추가한다.
5. 날짜는 `YYYY-MM-DD`(KST), 커밋은 짧은 해시로 적는다.

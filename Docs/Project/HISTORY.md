# 변경 이력

커밋마다 한 줄. 무엇을 왜 바꿨는지는 [REQUIREMENTS](REQUIREMENTS.md)의 같은 번호를 본다.
`git log --oneline`과 같은 순서(최신이 위)다.

| 커밋 | 날짜 | 요청 | 내용 |
|---|---|---|---|
| (이 커밋, `JY-lobby`) | 09-26 | R33 | 기물 재디자인·밸런스(킹 신규, 퀸 사냥), 새로 필요한 기능 개발 목록 `MECHANICS_TODO.md` |
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

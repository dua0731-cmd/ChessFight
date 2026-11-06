# 구형 폰 러시 맵

현재 맵 작업은 [폰 러시 안내](../README.md)에서 시작한다.
이 폴더는 이전 맵의 규칙·코드·기획을 다시 찾기 위한 보관함이다.

| 대상 | 위치·문서 |
|---|---|
| 이전 Course01 | `Assets/Prototype/PawnRush/Scenes/PawnRush_Course01.unity` · [규칙·구현](PAWN_RUSH_COURSE01.md) |
| Opening 구간 프리팹 | `Assets/Prototype/PawnRush/Prefabs/OpeningCourse/` |
| 구형 맵 코드·HUD | `Assets/Prototype/PawnRush/Scripts/`·`Resources/` |
| 초기 기획 | [DESIGN](DESIGN.md) · [Opening](OPENING_COURSE.md) · [Castle](CASTLE_COURSE.md) · [Final](FINAL_COURSE.md) |
| 이전 시작 문서 | [당시 README](README_HISTORICAL.md) |

Unity의 **ChessFight → Prototype → Pawn Rush**에서 Course01 관련 도구를 연다.
구형 C# 41개는 asmref 7개로 기존 어셈블리와 GUID를 유지한다.
현재 구간에서 공유하는 재료·미니게임·정원·홀·작전 회의실 빌더는 현재 코드 위치에 남아 있다.

Course01은 기존 온라인 경기의 호환 씬이므로 씬 이름과 로비 연결을 유지한다.
새 `PawnRush_Sections`·`PawnRush_MiniGames`의 온라인 구현과 구분한다.
이 폴더의 과거 문서에 적힌 옛 브랜치·작업 폴더·메뉴는 당시 기록이며 현재 작업 위치 지시가 아니다.

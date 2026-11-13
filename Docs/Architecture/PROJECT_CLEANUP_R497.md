# main 파일·메뉴 정리 기록

2026-11-06 · R497 · 기존 `C:\Users\dua07\GitHub\ChessFighter`의 `main`에서 진행.

기존 종류별 폴더와 어셈블리 경계는 유지하고, 구형 맵은 `Prototype`으로 분리했다.
현재 프로젝트를 여는 기준은 [HANDOFF](../../HANDOFF.md)와 [폴더 구조](STRUCTURE.md)다.
이동한 Unity 자산은 원래 `.meta`와 GUID를 유지했다.

## 1. 현재 작업 위치

| 작업 | 위치·여는 방법 |
|---|---|
| 현재 폰 러시 맵 | `Assets/Scenes/PawnRush_Sections.unity` · **ChessFight → Pawn Rush → Open Section Test** |
| 미니게임 10종 | `Assets/Prefabs/PawnRushSections/*_MG_*` · **Pawn Rush → Open Mini-game Prefab Test** |
| 현재 폰 러시 자료 | [Docs/PawnRush](../PawnRush/README.md) |
| 구형 폰 러시 맵 | `Assets/Prototype/PawnRush/` · [프로토타입 안내](../PawnRush/Prototype/README.md) |
| 체스복싱 시험작 | `Assets/Prototype/ChessBoxing/` · **Prototype → Chess Boxing** |
| 기물 미리보기 | `Assets/Prototype/Previews/PiecePreview.unity` |
| 과거 인계·폐기 모드 문서 | `Docs/Archive/` · 현재 작업 지시와 구분 |

`KingRush`라는 옛 문서 폴더 이름은 `PawnRush`로 바꿨다.
이전 Opening·Castle·Final·Course01 기획 9개는 `Docs/PawnRush/Prototype/`에 모았다.
이전 HANDOFF 원문은 [아카이브](../Archive/HANDOFF_BEFORE_R497.md)에 보존하고, 현재 HANDOFF는 상태·규칙·읽을 문서 중심으로 줄였다.

## 2. Prototype의 범위

- 구형 Course01 씬, Opening 구간 프리팹 24개, 구형 맵 C# 41개, 전용 HUD 자원 5개를 모았다.
- C#은 asmref 7개로 원래 어셈블리 소속을 유지한다. 현재 정원·홀·작전 회의실에서 사용하는 공유 빌더는 현재 코드 위치에 남겼다.
- Windows·Linux 검사 도구도 Prototype 소스를 같은 어셈블리로 수집한다.
- 체스복싱은 자체 어셈블리와 메타를 보존해 Prototype으로 이동했다.
- 참조·빌드 등록이 없는 자동 복구 씬 6개와 메타는 삭제했다. 이전 내용은 Git 이력에 남아 있다. 자동 복구·임시 미리보기 원래 경로는 Git 제외 목록에 추가했다.
- `Arena.prefab`은 Lobby가 직접 참조하므로 유지했다.

**기존 온라인 경기의 Course01 연결은 유지한다.** 파일은 Prototype으로 옮겼지만 씬 이름 `PawnRush_Course01`과 통신 키 `kingrush`를 바꾸지 않았다.
새 구간 조합 코스가 온라인 경기에 연결됐다는 의미는 아니다. 결과 씬 Victory/Lose도 빌드 도구의 도달 가능한 씬 목록에 포함했다.

## 3. 메뉴

ChessFight 바로 아래는 네 그룹이다.

| 그룹 | 용도 |
|---|---|
| Pawn Rush | 현재 구간·미니게임·스킬 시험. 재굽기와 검사는 Build/Validate 하위 |
| Sword Fight | 현재 소드 파이트 경기·스킬 시험 |
| Development | 씬·맵 제작·공유 아트·네트워크·물리·VFX·설치·캡처 도구 |
| Prototype | 구형 Course01·체스복싱·이전 시안 도구 |

SeoungTest의 맵 제작 기능은 **Development → Map Authoring**에 보존했다.
현재 맵과 스킬이 사용하는 공유 도구를 지우지 않고 개발 메뉴 아래로 모았다.
정적 메뉴 검사에서는 실행 명령 81개·검증 함수 37개, 중복 경로·고아 검증 함수·Queen 메뉴 각각 0개였다.

## 4. Queen of the Hill 폐기

현재 모드 목록은 폰 러시·소드 파이트 두 개다.
Queen 전용 등록·맵·씬·스킬·시험·생성 도구를 제거하고, 이전 문서는 [기록 보관함](../Archive/QueenOfTheHill/README.md)으로 옮겼다.

현재 모드가 사용하는 Hook/Rope·승격·개척 체크포인트·기물 효과는 공용 기능으로 유지한다.
물리 시험대는 `PhysicsMechanicsBed`, 개척 규칙은 `PioneerCourse`·`PioneerRules`, 공유 셰이더 5개와 음원 3개는 `Resources/SharedSkills/`로 재분류했다.
삭제한 Queen 자산 GUID 63개가 현재 Unity 자산에 남긴 참조는 0개였다.
폐기 모드의 통신 값 1과 플래그 128은 예약값으로 남겨 패킷 구조와 기존 번호를 유지했다.

## 5. 오래된 브랜치

마지막 커밋이 **2026-10-06 22:09:42 KST 이전**인 구현 브랜치를 정리했다.
main·현재 사용 중인 브랜치·최근 브랜치·태그·stash는 보존했다.

| 구분 | 삭제 | 남은 수 |
|---|---|---|
| 원격 | 8개 | 24 → 16개 |
| 로컬 | 8개 | 15 → 7개 |

원격 삭제: `BJH_KingRushTest`, `JY-gpt_gamemode`, `JY-kingrush`, `JY-lobby`, `JY-pieces`, `JY-ragdoll`, `Network`, `claude/quirky-gates-5ov6q7`.

로컬 삭제: `BJH_KingRushTest`, `JY-kingrush`, `JY-lobby`, `JY-ragdoll`, `Network`, `SteamNetworkTest`, `claude/quirky-gates-5ov6q7`, `킹을-지켜라`.

삭제 직전 참조·SHA·날짜를 보관하고, 원격은 예상 SHA가 같을 때만 삭제했다.
GitHub API와 Git 재조회로 결과를 대조했다.

복구 보관 위치:
`C:\Users\dua07\Desktop\개발일지\2학기 프로젝트수업\체스파이트\Backups\R497-Branches-20261106-221133`

여기에 약 7.9 MB의 Git bundle·manifest·결과·`RESTORE.txt`가 있다.
bundle은 Git 이력과 LFS 포인터를 보관하며 LFS 바이너리는 별도 포함하지 않는다. 기존 LFS 캐시는 변경하지 않았다.

## 6. 검사와 남은 확인

| 검사 | 기록 |
|---|---|
| Windows Core·모의 세션 | 162개·55개 통과. 폐기 맵 전용 검사 2개만 제거해 Core가 164→162개 |
| 의존성·장애물 시계 | Git Bash 문법 및 `--checks-only` 통과. 줄 끝 주석의 타입 이름은 코드 의존성으로 세지 않도록 보완 |
| 실제 Unity DLL 외부 컴파일 | Prototype 소스 수집을 포함한 전체 대상 어셈블리 통과 |
| Unity 저장 자산 검사 | 10개 프리팹·시험 씬 참조 검사 통과, 실패 0 |
| Unity Play 자동 검사 | 10종 로드·자기 팀 문·반복 초기화·씨앗 재생성·A 공유판 분리·I 외형 유지 통과 |
| 실제 GUI·퍼즐·Steam | 기존 경로의 Lobby Play 화면은 AI가 관찰. 사용자 입력 감지로 모드 버튼 이후 화면 조작 중지. 사용자 확인 전. 자동 완료는 문 연결 검사이며 장치를 실제 입력으로 풀었다는 뜻이 아님 |

원본 로그는 로컬 `Logs/R497-*`와 `Logs/MiniGameValidation/`에 있으며 Git에서 제외한다.
마감 상태와 수동 확인 항목은 [VALIDATION](../Network/VALIDATION.md)의 R497 기록을 따른다.

Unity 가져오기에서 기본 속성을 다시 직렬화한 `GC_bark.mat` 변경은 정리 범위와 무관해 커밋에서 제외하고 로컬에 보존했다.
